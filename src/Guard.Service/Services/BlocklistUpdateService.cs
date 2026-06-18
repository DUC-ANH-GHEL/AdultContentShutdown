using System.Security.Cryptography;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class BlocklistUpdateService
{
    private readonly GuardOptions _options;
    private readonly FileLogger _fileLogger;
    private readonly ILogger<BlocklistUpdateService> _logger;
    private readonly HttpClient _httpClient = new();
    private DomainBlocklist _current = DomainBlocklist.Empty;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;

    public BlocklistUpdateService(IOptions<GuardOptions> options, FileLogger fileLogger, ILogger<BlocklistUpdateService> logger)
    {
        _options = options.Value;
        _fileLogger = fileLogger;
        _logger = logger;
    }

    public DomainBlocklist Current => _current;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(force: true, cancellationToken);
    }

    public async Task RefreshIfDueAsync(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow - _lastRefresh < TimeSpan.FromMinutes(Math.Max(1, _options.BlocklistUpdates.RefreshIntervalMinutes)))
        {
            return;
        }

        await RefreshAsync(force: false, cancellationToken);
    }

    private async Task RefreshAsync(bool force, CancellationToken cancellationToken)
    {
        _lastRefresh = DateTimeOffset.UtcNow;
        var localFile = Path.Combine(AppContext.BaseDirectory, "Config", "adult-domains.txt");
        var files = new List<string> { localFile };

        if (_options.BlocklistUpdates.Enabled)
        {
            await TryUpdateRemoteCacheAsync(cancellationToken);
            if (File.Exists(_options.BlocklistUpdates.CacheFilePath))
            {
                files.Add(_options.BlocklistUpdates.CacheFilePath);
            }
        }

        _current = await DomainBlocklist.LoadAsync(files, cancellationToken);
        if (force)
        {
            await _fileLogger.LogAsync("INFO", "Blocklist initialized.", cancellationToken: cancellationToken);
        }
    }

    private async Task TryUpdateRemoteCacheAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BlocklistUpdates.RemoteUrl))
        {
            return;
        }

        if (!Uri.TryCreate(_options.BlocklistUpdates.RemoteUrl, UriKind.Absolute, out var remoteUri) ||
            remoteUri.Scheme != Uri.UriSchemeHttps)
        {
            await _fileLogger.LogAsync("ERROR", "Remote blocklist rejected because URL must be absolute HTTPS.", cancellationToken: cancellationToken);
            return;
        }

        try
        {
            using var response = await _httpClient.GetAsync(remoteUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            if (!HasExpectedHash(content))
            {
                await _fileLogger.LogAsync("ERROR", "Remote blocklist rejected because SHA-256 did not match.", cancellationToken: cancellationToken);
                return;
            }

            var cachePath = _options.BlocklistUpdates.CacheFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? ".");
            await File.WriteAllBytesAsync(cachePath, content, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Remote blocklist update failed; using cached/local list.");
            await _fileLogger.LogAsync("WARN", $"Remote blocklist update failed: {exception.Message}", cancellationToken: cancellationToken);
        }
    }

    private bool HasExpectedHash(byte[] content)
    {
        var expected = _options.BlocklistUpdates.Sha256;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var actual = Convert.ToHexString(SHA256.HashData(content));
        return string.Equals(actual, expected.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }
}
