using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class DomainBlocklist
{
    private static readonly Regex DomainLabel = new(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private readonly HashSet<string> _domains;

    private DomainBlocklist(IEnumerable<string> domains)
    {
        _domains = new HashSet<string>(domains, StringComparer.OrdinalIgnoreCase);
    }

    public static DomainBlocklist Empty { get; } = new(Array.Empty<string>());

    public static DomainBlocklist FromDomains(IEnumerable<string> domains)
    {
        var normalized = domains
            .SelectMany(ExtractDomains)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return new DomainBlocklist(normalized);
    }

    public static async Task<DomainBlocklist> LoadAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken)
    {
        var domains = new List<string>();
        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            await using var fileStream = File.OpenRead(filePath);
            await using Stream input = filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? new GZipStream(fileStream, CompressionMode.Decompress)
                : fileStream;
            using var reader = new StreamReader(input);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                domains.AddRange(ExtractDomains(line));
            }
        }

        return FromDomains(domains);
    }

    public bool IsBlocked(string? host, out string? matchedRule)
    {
        matchedRule = null;
        var normalizedHost = NormalizeDomain(host);
        if (string.IsNullOrWhiteSpace(normalizedHost))
        {
            return false;
        }

        var candidate = normalizedHost;
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            if (_domains.Contains(candidate))
            {
                matchedRule = candidate;
                return true;
            }

            var dotIndex = candidate.IndexOf('.', StringComparison.Ordinal);
            if (dotIndex < 0)
            {
                break;
            }

            candidate = candidate[(dotIndex + 1)..];
        }

        return false;
    }

    private static string StripComment(string value)
    {
        var hashIndex = value.IndexOf('#', StringComparison.Ordinal);
        return (hashIndex >= 0 ? value[..hashIndex] : value).Trim();
    }

    private static IEnumerable<string> ExtractDomains(string? line)
    {
        var clean = StripComment(line ?? string.Empty);
        foreach (var token in clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryNormalizeDomain(token, out var domain))
            {
                yield return domain;
            }
        }
    }

    private static string NormalizeDomain(string? host)
    {
        return TryNormalizeDomain(host, out var domain) ? domain : string.Empty;
    }

    private static bool TryNormalizeDomain(string? host, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var trimmed = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (trimmed.Length is 0 or > 253 || IPAddress.TryParse(trimmed, out _))
        {
            return false;
        }

        try
        {
            domain = new IdnMapping().GetAscii(trimmed).ToLowerInvariant();
        }
        catch
        {
            return false;
        }

        var labels = domain.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (labels.Length < 2 || labels.Any(label => !DomainLabel.IsMatch(label)))
        {
            domain = string.Empty;
            return false;
        }

        return true;
    }
}
