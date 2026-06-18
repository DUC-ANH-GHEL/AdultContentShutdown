using System.Net;
using System.Net.Sockets;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class LocalDnsResolverService
{
    private readonly GuardOptions _options;
    private readonly BlocklistUpdateService _blocklistUpdateService;
    private readonly GuardEventService _guardEventService;
    private readonly FileLogger _fileLogger;
    private readonly ILogger<LocalDnsResolverService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private UdpClient? _udpListener;
    private TcpListener? _tcpListener;
    private Task? _udpTask;
    private Task? _tcpTask;

    public LocalDnsResolverService(
        IOptions<GuardOptions> options,
        BlocklistUpdateService blocklistUpdateService,
        GuardEventService guardEventService,
        FileLogger fileLogger,
        ILogger<LocalDnsResolverService> logger)
    {
        _options = options.Value;
        _blocklistUpdateService = blocklistUpdateService;
        _guardEventService = guardEventService;
        _fileLogger = fileLogger;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Dns.Enabled || _udpListener is not null)
        {
            return Task.CompletedTask;
        }

        var listenAddress = IPAddress.Parse(_options.Dns.ListenAddress);
        var endpoint = new IPEndPoint(listenAddress, _options.Dns.ListenPort);
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _udpListener = new UdpClient(endpoint);
        _tcpListener = new TcpListener(endpoint);
        _tcpListener.Start();
        _udpTask = Task.Run(() => UdpLoopAsync(_cancellationTokenSource.Token), CancellationToken.None);
        _tcpTask = Task.Run(() => TcpLoopAsync(_cancellationTokenSource.Token), CancellationToken.None);
        _logger.LogInformation("Local DNS resolver started on {Endpoint}", endpoint);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        _udpListener?.Dispose();
        _tcpListener?.Stop();
        await WaitAsync(_udpTask, cancellationToken);
        await WaitAsync(_tcpTask, cancellationToken);
    }

    private async Task UdpLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpListener!.ReceiveAsync(cancellationToken);
                _ = Task.Run(() => HandleUdpQueryAsync(result, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                await _fileLogger.LogAsync("ERROR", $"DNS UDP listener error: {exception.Message}", cancellationToken: cancellationToken);
            }
        }
    }

    private async Task TcpLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _tcpListener!.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleTcpClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                await _fileLogger.LogAsync("ERROR", $"DNS TCP listener error: {exception.Message}", cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleUdpQueryAsync(UdpReceiveResult result, CancellationToken cancellationToken)
    {
        var response = await ResolveAsync(result.Buffer, result.RemoteEndPoint.Address.ToString(), cancellationToken);
        if (response is not null)
        {
            await _udpListener!.SendAsync(response, result.RemoteEndPoint, cancellationToken);
        }
    }

    private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var tcpClient = client;
        using var stream = tcpClient.GetStream();
        var lengthPrefix = new byte[2];
        if (await stream.ReadAsync(lengthPrefix, cancellationToken) != 2)
        {
            return;
        }

        var length = (lengthPrefix[0] << 8) | lengthPrefix[1];
        var query = new byte[length];
        var read = 0;
        while (read < length)
        {
            var chunk = await stream.ReadAsync(query.AsMemory(read, length - read), cancellationToken);
            if (chunk == 0)
            {
                return;
            }

            read += chunk;
        }

        var response = await ResolveAsync(query, tcpClient.Client.RemoteEndPoint?.ToString(), cancellationToken);
        if (response is null)
        {
            return;
        }

        await stream.WriteAsync(new[] { (byte)(response.Length >> 8), (byte)(response.Length & 0xFF) }, cancellationToken);
        await stream.WriteAsync(response, cancellationToken);
    }

    private async Task<byte[]?> ResolveAsync(byte[] query, string? clientAddress, CancellationToken cancellationToken)
    {
        await _blocklistUpdateService.RefreshIfDueAsync(cancellationToken);
        if (!DnsMessage.TryParseQuery(query, out var message) || message is null)
        {
            return null;
        }

        if (_blocklistUpdateService.Current.IsBlocked(message.QuestionName, out var matchedRule))
        {
            await _guardEventService.HandleAsync(new GuardEvent
            {
                EventKind = GuardEventKind.BlockedDomain,
                Domain = message.QuestionName,
                ClientAddress = clientAddress,
                MatchedRule = matchedRule,
                Reason = "DNS query matched adult domain blocklist."
            }, cancellationToken);

            if (_options.Dns.ReturnNxDomain)
            {
                return message.CreateNxDomainResponse();
            }

            return message.CreateSinkholeResponse(IPAddress.Parse(_options.Dns.SinkholeAddress));
        }

        return await ForwardAsync(query, cancellationToken);
    }

    private async Task<byte[]?> ForwardAsync(byte[] query, CancellationToken cancellationToken)
    {
        foreach (var upstream in _options.Dns.UpstreamServers)
        {
            try
            {
                using var client = new UdpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Math.Max(250, _options.Dns.UpstreamTimeoutMilliseconds));
                await client.SendAsync(query, new IPEndPoint(IPAddress.Parse(upstream), 53), timeout.Token);
                var result = await client.ReceiveAsync(timeout.Token);
                return result.Buffer;
            }
            catch
            {
            }
        }

        return null;
    }

    private static async Task WaitAsync(Task? task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.WaitAsync(cancellationToken);
        }
        catch
        {
        }
    }
}
