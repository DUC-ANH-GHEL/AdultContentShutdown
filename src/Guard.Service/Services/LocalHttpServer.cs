using System.Net;
using System.Text;
using System.Text.Json;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class LocalHttpServer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
    private const long MaxRequestBodyBytes = 64 * 1024;

    private readonly GuardOptions _options;
    private readonly ContentViolationService _contentViolationService;
    private readonly FileLogger _fileLogger;
    private readonly ILogger<LocalHttpServer> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCancellationSource;
    private Task? _listenerTask;

    public LocalHttpServer(
        IOptions<GuardOptions> options,
        ContentViolationService contentViolationService,
        FileLogger fileLogger,
        ILogger<LocalHttpServer> logger)
    {
        _options = options.Value;
        _contentViolationService = contentViolationService;
        _fileLogger = fileLogger;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listener is not null)
        {
            return Task.CompletedTask;
        }

        _listenerCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/");
        _listener.Start();
        _logger.LogInformation("Local HTTP server started on http://127.0.0.1:{Port}/", _options.Port);
        _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellationSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listenerCancellationSource?.Cancel();
        }
        catch
        {
        }

        if (_listener is not null)
        {
            try
            {
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }
            }
            catch
            {
            }

            try
            {
                _listener.Close();
            }
            catch
            {
            }
        }

        if (_listenerTask is not null)
        {
            try
            {
                await _listenerTask.WaitAsync(cancellationToken);
            }
            catch
            {
            }
        }

        _listenerCancellationSource?.Dispose();
        _listenerCancellationSource = null;
        _listener = null;
        _listenerTask = null;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await _fileLogger.LogAsync("ERROR", $"Listener error: {exception.Message}");
                }

                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            ApplyCorsHeaders(context.Response);

            var method = context.Request.HttpMethod.ToUpperInvariant();
            var path = context.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? string.Empty;

            if (method == "OPTIONS" && (path == "/health" || path == "/violation"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                await WriteEmptyResponseAsync(context.Response, cancellationToken);
                return;
            }

            if (path == "/health" && method == "GET")
            {
                await WriteJsonResponseAsync(context.Response, new
                {
                    status = "ok",
                    dryRun = _options.DryRun,
                    service = "AdultContentShutdownGuard",
                    dns = new
                    {
                        enabled = _options.Dns.Enabled,
                        listenAddress = _options.Dns.ListenAddress,
                        listenPort = _options.Dns.ListenPort
                    },
                    passiveDnsMonitor = new
                    {
                        enabled = _options.PassiveDnsMonitor.Enabled,
                        eventLogName = _options.PassiveDnsMonitor.EventLogName
                    },
                    networkPosture = new
                    {
                        enabled = _options.NetworkPosture.Enabled,
                        actionOnUnsafePosture = _options.NetworkPosture.ActionOnUnsafePosture
                    },
                    managedBrowser = new
                    {
                        enabled = _options.ManagedBrowserEndpoint.Enabled,
                        extensionEndpointEnabled = _options.ManagedBrowserEndpoint.Enabled,
                        chromeExtensionConfigured = !string.IsNullOrWhiteSpace(_options.ManagedBrowserEndpoint.ChromeExtensionId),
                        edgeExtensionConfigured = !string.IsNullOrWhiteSpace(_options.ManagedBrowserEndpoint.EdgeExtensionId),
                        updateUrl = _options.ManagedBrowserEndpoint.UpdateUrl,
                        updateManifestHosted = HasManagedBrowserFile(_options.ManagedBrowserEndpoint.UpdateManifestPath),
                        crxHosted = HasManagedBrowserFile(_options.ManagedBrowserEndpoint.CrxPath),
                        workVpnAllowed = _options.ProcessRules.AllowedWorkVpnProcesses.Length > 0
                    },
                    legacyExtensionEndpointEnabled = _options.LegacyExtensionEndpointEnabled
                }, cancellationToken);
                return;
            }

            if (method == "GET" && path == "/extensions/updates.xml")
            {
                await WriteManagedBrowserFileAsync(
                    context.Response,
                    _options.ManagedBrowserEndpoint.UpdateManifestPath,
                    "application/xml; charset=utf-8",
                    cancellationToken);
                return;
            }

            if (method == "GET" && path == "/extensions/adultcontentshutdownguard.crx")
            {
                await WriteManagedBrowserFileAsync(
                    context.Response,
                    _options.ManagedBrowserEndpoint.CrxPath,
                    "application/x-chrome-extension",
                    cancellationToken);
                return;
            }

            if (path == "/violation" && method == "POST")
            {
                if (!_options.ManagedBrowserEndpoint.Enabled && !_options.LegacyExtensionEndpointEnabled)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Gone;
                    await WriteJsonResponseAsync(context.Response, new { status = "managed_browser_endpoint_disabled" }, cancellationToken);
                    return;
                }

                if (!IsValidToken(context.Request.Headers["X-Guard-Token"]))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await WriteJsonResponseAsync(context.Response, new { status = "forbidden" }, cancellationToken);
                    return;
                }

                var body = await ReadRequestBodyAsync(context.Request, cancellationToken);
                if (string.IsNullOrWhiteSpace(body))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await WriteJsonResponseAsync(context.Response, new { status = "forbidden" }, cancellationToken);
                    return;
                }

                ViolationRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<ViolationRequest>(body, JsonSerializerOptions);
                }
                catch
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await WriteJsonResponseAsync(context.Response, new { status = "forbidden" }, cancellationToken);
                    return;
                }

                if (request is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await WriteJsonResponseAsync(context.Response, new { status = "forbidden" }, cancellationToken);
                    return;
                }

                var handled = await _contentViolationService.HandleViolationAsync(request, cancellationToken);
                await WriteJsonResponseAsync(context.Response, new
                {
                    status = handled ? "accepted" : "ignored"
                }, cancellationToken);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonResponseAsync(context.Response, new { status = "not_found" }, cancellationToken);
        }
        catch (Exception exception)
        {
            await _fileLogger.LogAsync("ERROR", $"Request handling failed: {exception.Message}");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                await WriteJsonResponseAsync(context.Response, new { status = "forbidden" }, cancellationToken);
            }
            catch
            {
            }
        }
        finally
        {
            try
            {
                context.Response.OutputStream.Close();
            }
            catch
            {
            }
        }
    }

    private bool IsValidToken(string? token)
    {
        return ManagedBrowserEndpointTokenValidator.IsValid(_options.Token, token);
    }

    private static bool HasManagedBrowserFile(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private static void ApplyCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Guard-Token";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, cancellationToken);
    }

    private static async Task WriteEmptyResponseAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        response.ContentLength64 = 0;
        await response.OutputStream.FlushAsync(cancellationToken);
    }

    private static async Task WriteManagedBrowserFileAsync(HttpListenerResponse response, string path, string contentType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonResponseAsync(response, new { status = "not_found" }, cancellationToken);
            return;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength64 > MaxRequestBodyBytes)
        {
            return string.Empty;
        }

        using var streamReader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var buffer = new char[MaxRequestBodyBytes + 1];
        var read = await streamReader.ReadBlockAsync(buffer, cancellationToken);
        if (read > MaxRequestBodyBytes)
        {
            return string.Empty;
        }

        return new string(buffer, 0, read);
    }
}
