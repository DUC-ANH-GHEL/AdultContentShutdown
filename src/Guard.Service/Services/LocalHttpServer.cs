using System.Net;
using System.Text;
using System.Text.Json;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

/// <summary>
/// Chi cong khai thong tin suc khoe doc duoc tren may local.
/// </summary>
public sealed class LocalHttpServer
{
    private readonly GuardOptions _options;
    private readonly FileLogger _fileLogger;
    private readonly ILogger<LocalHttpServer> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;

    public LocalHttpServer(IOptions<GuardOptions> options, FileLogger fileLogger, ILogger<LocalHttpServer> logger)
    {
        _options = options.Value;
        _fileLogger = fileLogger;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listener is not null)
        {
            return Task.CompletedTask;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/");
        _listener.Start();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cancellationTokenSource.Token), CancellationToken.None);
        _logger.LogInformation("Local health endpoint started on http://127.0.0.1:{Port}/health", _options.Port);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        _listener?.Close();

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

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _listener = null;
        _listenerTask = null;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener!.GetContextAsync().WaitAsync(cancellationToken);
                _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                await _fileLogger.LogAsync("ERROR", $"Health listener error: {exception.Message}", cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            if (context.Request.HttpMethod == "GET" && path == "/health")
            {
                await WriteJsonResponseAsync(context.Response, new
                {
                    status = "ok",
                    service = "AdultContentShutdownGuard",
                    dryRun = _options.DryRun,
                    dns = new
                    {
                        enabled = _options.Dns.Enabled,
                        listenAddresses = _options.Dns.ListenAddresses,
                        listenPort = _options.Dns.ListenPort
                    },
                    enforcement = new
                    {
                        applyOnStartup = _options.Enforcement.ApplyOnStartup,
                        dnsAdapters = _options.Enforcement.ConfigureDnsAdapters,
                        firewall = _options.Enforcement.ConfigureFirewallRules
                    },
                    browserPolicies = new
                    {
                        enabled = _options.BrowserPolicies.Enabled,
                        dnsOverHttpsDisabled = _options.BrowserPolicies.DisableDnsOverHttps,
                        quicDisabled = _options.BrowserPolicies.DisableQuic,
                        privateBrowsingDisabled = _options.BrowserPolicies.DisablePrivateBrowsing,
                        guestModeDisabled = _options.BrowserPolicies.DisableGuestMode
                    }
                }, cancellationToken);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonResponseAsync(context.Response, new { status = "not_found" }, cancellationToken);
        }
        catch (Exception exception)
        {
            await _fileLogger.LogAsync("ERROR", $"Health request failed: {exception.Message}", cancellationToken: cancellationToken);
        }
        finally
        {
            context.Response.OutputStream.Close();
        }
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, cancellationToken);
    }
}
