using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class ContentViolationService
{
    private static readonly string[] StrongKeywords =
    [
        "porn",
        "porno",
        "xxx",
        "sex",
        "nude",
        "naked",
        "hentai",
        "jav",
        "blowjob",
        "anal",
        "pussy",
        "dick",
        "tits"
    ];

    private readonly GuardOptions _options;
    private readonly FileLogger _fileLogger;
    private readonly GuardEventService _guardEventService;
    private readonly object _debounceLock = new();
    private readonly Dictionary<string, DateTimeOffset> _recentViolations = new(StringComparer.OrdinalIgnoreCase);

    public ContentViolationService(IOptions<GuardOptions> options, FileLogger fileLogger, GuardEventService guardEventService)
    {
        _options = options.Value;
        _fileLogger = fileLogger;
        _guardEventService = guardEventService;
    }

    public async Task<bool> HandleViolationAsync(ViolationRequest request, CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            await _fileLogger.LogAsync("WARN", "Ignored invalid violation request.", request, cancellationToken: cancellationToken);
            return false;
        }

        var detectedAt = request.DetectedAt == default ? DateTimeOffset.UtcNow : request.DetectedAt;
        request.DetectedAt = detectedAt;

        if (IsKeywordOnlyFalsePositive(request))
        {
            await _fileLogger.LogAsync("INFO", "Ignored keyword-only browser false positive.", request, cancellationToken: cancellationToken);
            return false;
        }

        var debounceKey = BuildDebounceKey(request);
        if (IsDebounced(debounceKey, detectedAt))
        {
            await _fileLogger.LogAsync("INFO", "Ignored duplicate violation inside debounce window.", request, cancellationToken: cancellationToken);
            return false;
        }

        var matchedRules = request.MatchedRules.Count == 0 ? Array.Empty<string>() : request.MatchedRules.ToArray();
        await _fileLogger.LogAsync("WARN", "Adult content violation received.", request, matchedRules, cancellationToken);

        await _guardEventService.HandleAsync(new GuardEvent
        {
            EventKind = GuardEventKind.LegacyExtensionViolation,
            Domain = request.Host,
            MatchedRule = matchedRules.FirstOrDefault(),
            Reason = request.Reason,
            MatchedRules = matchedRules,
            LegacyRequest = request
        }, cancellationToken);
        return true;
    }

    private bool IsValid(ViolationRequest request)
    {
        return request is not null &&
               (!string.IsNullOrWhiteSpace(request.Url) ||
                !string.IsNullOrWhiteSpace(request.Host) ||
                !string.IsNullOrWhiteSpace(request.Title) ||
                !string.IsNullOrWhiteSpace(request.Reason));
    }

    private bool IsKeywordOnlyFalsePositive(ViolationRequest request)
    {
        if (!_options.EnableKeywordRules || request.MatchedRules.Count == 0)
        {
            return false;
        }

        if (request.MatchedRules.Any(rule => rule.StartsWith("known adult domain:", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var keywordOnly = request.MatchedRules.All(rule =>
            rule.StartsWith("url keywords:", StringComparison.OrdinalIgnoreCase) ||
            rule.StartsWith("title keywords:", StringComparison.OrdinalIgnoreCase));
        if (!keywordOnly)
        {
            return false;
        }

        return ScoreBrowserUrlAndTitle(request) < _options.MinScoreToShutdown;
    }

    private static int ScoreBrowserUrlAndTitle(ViolationRequest request)
    {
        var score = 0;

        if (ContainsDelimitedKeyword(request.Url, StrongKeywords))
        {
            score += 60;
        }

        if (ContainsDelimitedKeyword(request.Title, StrongKeywords))
        {
            score += 50;
        }

        return score;
    }

    private static bool ContainsDelimitedKeyword(string? text, IEnumerable<string> keywords)
    {
        var normalized = (text ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return keywords.Any(keyword => IsDelimitedKeywordMatch(normalized, keyword));
    }

    private static bool IsDelimitedKeywordMatch(string normalizedText, string keyword)
    {
        var normalizedKeyword = (keyword ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedKeyword.Length == 0)
        {
            return false;
        }

        var phrasePattern = string.Join("[^a-z0-9]+", normalizedKeyword
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Regex.Escape));

        return Regex.IsMatch(
            normalizedText,
            $"(^|[^a-z0-9]){phrasePattern}(?=$|[^a-z0-9])",
            RegexOptions.CultureInvariant);
    }

    private bool IsDebounced(string debounceKey, DateTimeOffset detectedAt)
    {
        lock (_debounceLock)
        {
            CleanupOldEntries(detectedAt);

            if (_recentViolations.TryGetValue(debounceKey, out var lastDetectedAt) &&
                (detectedAt - lastDetectedAt).TotalSeconds < _options.DebounceSeconds)
            {
                return true;
            }

            _recentViolations[debounceKey] = detectedAt;
            return false;
        }
    }

    private void CleanupOldEntries(DateTimeOffset now)
    {
        var cutoff = now.AddMinutes(-10);
        var expiredKeys = _recentViolations.Where(entry => entry.Value < cutoff).Select(entry => entry.Key).ToList();

        foreach (var expiredKey in expiredKeys)
        {
            _recentViolations.Remove(expiredKey);
        }
    }

    private static string BuildDebounceKey(ViolationRequest request)
    {
        return string.Join("|", new[]
        {
            request.Url?.Trim().ToLowerInvariant(),
            request.Host?.Trim().ToLowerInvariant(),
            request.Title?.Trim().ToLowerInvariant(),
            request.Reason?.Trim().ToLowerInvariant()
        });
    }
}
