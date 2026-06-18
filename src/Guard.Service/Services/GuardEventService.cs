using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class GuardEventService
{
    private readonly GuardOptions _options;
    private readonly FileLogger _fileLogger;
    private readonly ShutdownService _shutdownService;

    public GuardEventService(IOptions<GuardOptions> options, FileLogger fileLogger, ShutdownService shutdownService)
    {
        _options = options.Value;
        _fileLogger = fileLogger;
        _shutdownService = shutdownService;
    }

    public async Task HandleAsync(GuardEvent guardEvent, CancellationToken cancellationToken)
    {
        var action = ResolveAction(guardEvent.EventKind);
        guardEvent.ActionTaken = _options.DryRun ? $"DryRun:{action}" : action.ToString();
        await _fileLogger.LogAsync("WARN", Describe(guardEvent), guardEvent, cancellationToken);

        if (_options.DryRun || action != GuardAction.Shutdown)
        {
            return;
        }

        await _shutdownService.ShutdownNowAsync(cancellationToken);
    }

    private GuardAction ResolveAction(GuardEventKind eventKind)
    {
        var configured = eventKind switch
        {
            GuardEventKind.UnsafeNetworkPosture => _options.NetworkPosture.ActionOnUnsafePosture,
            GuardEventKind.WorkVpnDetected => _options.ProcessRules.ActionOnWorkVpnDetected,
            GuardEventKind.TamperDetected or GuardEventKind.DnsBypassAttempt => _options.Enforcement.ActionOnTamper,
            _ => _options.Enforcement.ActionOnViolation
        };

        return Enum.TryParse<GuardAction>(configured, ignoreCase: true, out var action)
            ? action
            : GuardAction.Shutdown;
    }

    private static string Describe(GuardEvent guardEvent)
    {
        return guardEvent.EventKind switch
        {
            GuardEventKind.BlockedDomain => $"Blocked adult domain: {guardEvent.Domain}",
            GuardEventKind.TamperDetected => $"Tamper detected: {guardEvent.Reason}",
            GuardEventKind.DnsBypassAttempt => $"DNS bypass attempt: {guardEvent.Reason}",
            GuardEventKind.WorkVpnDetected => $"Work VPN detected: {guardEvent.Reason}",
            GuardEventKind.UnsafeNetworkPosture => $"Unsafe network posture: {guardEvent.Reason}",
            GuardEventKind.PolicyViolation => $"Policy violation: {guardEvent.Reason}",
            GuardEventKind.LegacyExtensionViolation => "Legacy extension violation received.",
            _ => guardEvent.Reason ?? guardEvent.EventKind.ToString()
        };
    }
}
