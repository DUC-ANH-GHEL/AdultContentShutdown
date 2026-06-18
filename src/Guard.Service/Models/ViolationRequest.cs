namespace AdultContentShutdownGuard.Guard.Service.Models;

public sealed class ViolationRequest
{
    public string? Url { get; set; }

    public string? Host { get; set; }

    public string? Title { get; set; }

    public string? Reason { get; set; }

    public List<string> MatchedRules { get; set; } = new();

    public DateTimeOffset DetectedAt { get; set; }
}
