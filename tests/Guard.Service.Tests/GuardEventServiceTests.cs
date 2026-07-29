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

    private static GuardOptions CreateOptions()
    {
        var options = new GuardOptions
        {
            LogDirectory = Path.Combine(Path.GetTempPath(), "acsg-tests", Guid.NewGuid().ToString("N"))
        };
        return options;
    }

    private static GuardEventService CreateService(GuardOptions options)
    {
        var wrapped = Options.Create(options);
        var logger = new FileLogger(wrapped);
        var shutdown = new ShutdownService(wrapped, logger, NullLogger<ShutdownService>.Instance);
        return new GuardEventService(wrapped, logger, shutdown);
    }
}
