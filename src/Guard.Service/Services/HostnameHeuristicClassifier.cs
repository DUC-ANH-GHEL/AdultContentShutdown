using System.Globalization;
using System.Text.RegularExpressions;
using AdultContentShutdownGuard.Guard.Service.Models;

namespace AdultContentShutdownGuard.Guard.Service.Services;

/// <summary>
/// Đánh giá tên miền tại chỗ để bắt các biến thể không có trong danh sách cố định.
/// Chỉ dùng tín hiệu đặc trưng cao; không suy đoán dựa trên nội dung trang HTTPS.
/// </summary>
public sealed class HostnameHeuristicClassifier
{
    private static readonly Regex SexToken = new(@"(?:^|[-\d])sex(?:$|[-\d])", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex JavAdultLabel = new(@"^jav(?:hd|hub|most)[a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly (string Marker, string Rule)[] StrongMarkers =
    [
        ("porn", "porn"),
        ("hentai", "hentai"),
        ("rule34", "rule34"),
        ("onlyfans", "onlyfans"),
        ("fansly", "fansly"),
        ("xvideos", "xvideos"),
        ("xnxx", "xnxx"),
        ("xhamster", "xhamster"),
        ("youporn", "youporn"),
        ("redtube", "redtube"),
        ("spankbang", "spankbang"),
        ("brazzers", "brazzers"),
        ("hanime", "hanime")
    ];

    private readonly HostnameHeuristicsOptions _options;

    public HostnameHeuristicClassifier(HostnameHeuristicsOptions options)
    {
        _options = options;
    }

    public bool IsBlocked(string? host, out string? matchedRule)
    {
        matchedRule = null;
        if (!_options.Enabled)
        {
            return false;
        }

        var normalizedHost = NormalizeDomain(host);
        if (string.IsNullOrWhiteSpace(normalizedHost) || IsAllowed(normalizedHost))
        {
            return false;
        }

        var score = 0;
        string? strongestRule = null;
        var labels = normalizedHost.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var label in labels)
        {
            foreach (var (marker, rule) in StrongMarkers)
            {
                if (label.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                    strongestRule ??= rule;
                }
            }

            if (JavAdultLabel.IsMatch(label))
            {
                score += 100;
                strongestRule ??= label.StartsWith("javhd", StringComparison.OrdinalIgnoreCase)
                    ? "jav-hd"
                    : label.StartsWith("javhub", StringComparison.OrdinalIgnoreCase)
                        ? "jav-hub"
                        : "jav-most";
            }

            if (label.Contains("xxx", StringComparison.OrdinalIgnoreCase))
            {
                score += 60;
                strongestRule ??= "xxx";
            }
        }

        if (labels.Any(label => string.Equals(label, "xxx", StringComparison.OrdinalIgnoreCase)))
        {
            score += 100;
            strongestRule ??= "adult-tld";
        }

        var hasSexMarker = labels.Any(label => SexToken.IsMatch(label));
        var hasAdultMediaMarker = labels.Any(label =>
            label.Contains("tube", StringComparison.OrdinalIgnoreCase)
            || label.Contains("video", StringComparison.OrdinalIgnoreCase)
            || label.Contains("cam", StringComparison.OrdinalIgnoreCase));
        if (hasSexMarker && hasAdultMediaMarker)
        {
            score += 100;
            strongestRule ??= "sex-media";
        }

        var minimumScore = Math.Clamp(_options.MinimumScoreToBlock, 100, 500);
        if (score < minimumScore || string.IsNullOrWhiteSpace(strongestRule))
        {
            return false;
        }

        matchedRule = "heuristic:" + strongestRule;
        return true;
    }

    private bool IsAllowed(string normalizedHost)
    {
        return _options.AllowedDomains
            .Select(NormalizeDomain)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Any(allowed => normalizedHost.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                            || normalizedHost.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDomain(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var trimmed = host.Trim().TrimEnd('.').ToLowerInvariant();
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
