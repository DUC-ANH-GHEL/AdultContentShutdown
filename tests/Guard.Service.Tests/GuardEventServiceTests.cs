using AdultContentShutdownGuard.Guard.Service.Models;
using AdultContentShutdownGuard.Guard.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guard.Service.Tests;

public sealed class GuardEventServiceTests
{
    [Fact]
    public async Task HandleAsync_uses_log_only_for_unsafe_network_posture_by_default()
    {
        var options = CreateOptions();
        var service = CreateService(options);
        var guardEvent = new GuardEvent
        {
            EventKind = GuardEventKind.UnsafeNetworkPosture,
            Reason = "posture risk"
        };

        await service.HandleAsync(guardEvent, CancellationToken.None);

        Assert.Equal("LogOnly", guardEvent.ActionTaken);
    }

    [Fact]
    public async Task HandleAsync_uses_log_only_for_blocked_domain_by_default()
    {
        var options = CreateOptions();
        var service = CreateService(options);
        var guardEvent = new GuardEvent
        {
            EventKind = GuardEventKind.BlockedDomain,
            Domain = "pornhub.com"
        };

        await service.HandleAsync(guardEvent, CancellationToken.None);

        Assert.Equal("LogOnly", guardEvent.ActionTaken);
    }

    [Fact]
    public async Task HandleAsync_uses_log_only_for_work_vpn_detected_by_default()
    {
        var options = CreateOptions();
        var service = CreateService(options);
        var guardEvent = new GuardEvent
        {
            EventKind = GuardEventKind.WorkVpnDetected,
            MatchedRule = "protonvpn"
        };

        await service.HandleAsync(guardEvent, CancellationToken.None);

        Assert.Equal("LogOnly", guardEvent.ActionTaken);
    }

    [Fact]
    public async Task HandleAsync_never_uses_shutdown_without_explicit_machine_shutdown_opt_in()
    {
        var options = CreateOptions();
        options.DryRun = true;
        options.Enforcement.ActionOnTamper = "Shutdown";
        var service = CreateService(options);
        var guardEvent = new GuardEvent
        {
            EventKind = GuardEventKind.DnsBypassAttempt,
            MatchedRule = "tor"
        };

        await service.HandleAsync(guardEvent, CancellationToken.None);

        Assert.Equal("DryRun:LogOnly", guardEvent.ActionTaken);
    }

    [Fact]
    public async Task HandleAsync_allows_shutdown_only_when_explicitly_opted_in()
    {
        var options = CreateOptions();
        options.DryRun = true;
        options.AllowMachineShutdown = true;
        options.Enforcement.ActionOnTamper = "Shutdown";
        var service = CreateService(options);
        var guardEvent = new GuardEvent
        {
            EventKind = GuardEventKind.DnsBypassAttempt,
            MatchedRule = "tor"
        };

        await service.HandleAsync(guardEvent, CancellationToken.None);

        Assert.Equal("DryRun:Shutdown", guardEvent.ActionTaken);
    }

    [Fact]
    public async Task HandleAsync_requests_overlay_for_a_blocked_domain_when_not_in_dry_run()
    {
        var options = CreateOptions();
        var overlay = new RecordingOverlayLauncher();
        var service = CreateService(options, overlay);
        var guardEvent = new GuardEvent
        {
            EventKind = GuardEventKind.BlockedDomain,
            Domain = "example.test"
        };

        await service.HandleAsync(guardEvent, CancellationToken.None);

        Assert.Single(overlay.Events);
        Assert.Same(guardEvent, overlay.Events[0]);
    }

    [Fact]
    public async Task HandleAsync_does_not_request_overlay_in_dry_run_or_for_non_violation_events()
    {
        var options = CreateOptions();
        options.DryRun = true;
        var overlay = new RecordingOverlayLauncher();
        var service = CreateService(options, overlay);

        await service.HandleAsync(new GuardEvent { EventKind = GuardEventKind.BlockedDomain }, CancellationToken.None);
        options.DryRun = false;
        await service.HandleAsync(new GuardEvent { EventKind = GuardEventKind.TamperDetected }, CancellationToken.None);

        Assert.Empty(overlay.Events);
    }

    private static GuardOptions CreateOptions()
    {
        var options = new GuardOptions
        {
            LogDirectory = Path.Combine(Path.GetTempPath(), "acsg-tests", Guid.NewGuid().ToString("N"))
        };
        return options;
    }

    private static GuardEventService CreateService(GuardOptions options, IOverlayLauncher? overlayLauncher = null)
    {
        var wrapped = Options.Create(options);
        var logger = new FileLogger(wrapped);
        var shutdown = new ShutdownService(wrapped, logger, NullLogger<ShutdownService>.Instance);
        var commandRunner = new SystemCommandRunner(logger, NullLogger<SystemCommandRunner>.Instance);
        var overlay = overlayLauncher ?? new OverlayService(wrapped, commandRunner, logger);
        return new GuardEventService(wrapped, logger, shutdown, overlay);
    }

    private sealed class RecordingOverlayLauncher : IOverlayLauncher
    {
        public List<GuardEvent> Events { get; } = [];

        public Task TriggerForViolationAsync(GuardEvent guardEvent, CancellationToken cancellationToken)
        {
            Events.Add(guardEvent);
            return Task.CompletedTask;
        }
    }
}
