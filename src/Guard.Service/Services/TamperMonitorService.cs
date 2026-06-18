using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class TamperMonitorService
{
    private readonly GuardOptions _options;
    private readonly SystemEnforcementService _systemEnforcementService;
    private readonly BrowserPolicyService _browserPolicyService;
    private readonly GuardEventService _guardEventService;
    private Task? _monitorTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public TamperMonitorService(
        IOptions<GuardOptions> options,
        SystemEnforcementService systemEnforcementService,
        BrowserPolicyService browserPolicyService,
        GuardEventService guardEventService)
    {
        _options = options.Value;
        _systemEnforcementService = systemEnforcementService;
        _browserPolicyService = browserPolicyService;
        _guardEventService = guardEventService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Tamper.Enabled || _monitorTask is not null)
        {
            return Task.CompletedTask;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = Task.Run(() => LoopAsync(_cancellationTokenSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.WaitAsync(cancellationToken);
            }
            catch
            {
            }
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.Tamper.CheckIntervalSeconds)), cancellationToken);
            var restored = new List<string>();

            if (_options.Tamper.RestoreSettings)
            {
                restored.AddRange(await _systemEnforcementService.RepairAsync(cancellationToken));
                restored.AddRange(await _browserPolicyService.RepairAsync(cancellationToken));
            }

            if (restored.Count > 0)
            {
                await _guardEventService.HandleAsync(new GuardEvent
                {
                    EventKind = GuardEventKind.TamperDetected,
                    Reason = "Periodic hardening check restored protected settings.",
                    RestoredSettings = restored.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                }, cancellationToken);
            }
        }
    }
}
