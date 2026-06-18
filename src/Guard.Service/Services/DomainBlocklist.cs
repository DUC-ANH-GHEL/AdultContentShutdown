using System.Globalization;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class DomainBlocklist
{
    private readonly HashSet<string> _domains;

    private DomainBlocklist(IEnumerable<string> domains)
    {
        _domains = new HashSet<string>(domains, StringComparer.OrdinalIgnoreCase);
    }

    public static DomainBlocklist Empty { get; } = new(Array.Empty<string>());

    public static DomainBlocklist FromDomains(IEnumerable<string> domains)
    {
        var normalized = domains
            .Select(NormalizeDomain)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
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

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            foreach (var line in lines)
            {
                var clean = StripComment(line);
                foreach (var domain in clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    domains.Add(domain);
                }
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

    private static string NormalizeDomain(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var trimmed = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return new IdnMapping().GetAscii(trimmed).ToLowerInvariant();
        }
        catch
        {
            return trimmed;
        }
    }
}
