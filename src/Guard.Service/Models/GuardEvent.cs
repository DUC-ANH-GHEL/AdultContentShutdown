namespace AdultContentShutdownGuard.Guard.Service.Models;

public enum GuardEventKind
{
    BlockedDomain,
    TamperDetected,
    PolicyViolation,
    DnsBypassAttempt,
    WorkVpnDetected,
    UnsafeNetworkPosture,
    LegacyExtensionViolation,
    HealthCheck
}

public sealed class GuardEvent
{
    public GuardEventKind EventKind { get; set; }

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Domain { get; set; }

    public string? ClientAddress { get; set; }

    public string? MatchedRule { get; set; }

    public string? Reason { get; set; }

    public string? ActionTaken { get; set; }

    public string[] RestoredSettings { get; set; } = [];

    public string[] MatchedRules { get; set; } = [];

    public ViolationRequest? LegacyRequest { get; set; }
}
