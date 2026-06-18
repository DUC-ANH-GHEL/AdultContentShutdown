namespace AdultContentShutdownGuard.Guard.Service.Models;

public sealed record RuleMatch(string Rule, int Score, string Detail);
