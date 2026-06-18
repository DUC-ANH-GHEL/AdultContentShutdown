using System.Diagnostics;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class ProcessMonitorService
{
    private readonly GuardOptions _options;
    private readonly GuardEventService _guardEventService;
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reportedWorkVpn = new(StringComparer.OrdinalIgnoreCase);
    private Task? _monitorTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public ProcessMonitorService(IOptions<GuardOptions> options, GuardEventService guardEventService)
    {
        _options = options.Value;
        _guardEventService = guardEventService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.ProcessRules.Enabled || _monitorTask is not null)
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
        var blocked = _options.ProcessRules.BlockedProcessNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var workVpn = _options.ProcessRules.AllowedWorkVpnProcesses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    if (workVpn.Contains(process.ProcessName) && _reportedWorkVpn.Add(process.ProcessName))
                    {
                        await _guardEventService.HandleAsync(new GuardEvent
                        {
                            EventKind = GuardEventKind.WorkVpnDetected,
                            Reason = $"Allowed work VPN process is running: {process.ProcessName}",
                            MatchedRule = process.ProcessName
                        }, cancellationToken);
                        continue;
                    }

                    if (!blocked.Contains(process.ProcessName) || !_reported.Add(process.ProcessName))
                    {
                        continue;
                    }

                    await _guardEventService.HandleAsync(new GuardEvent
                    {
                        EventKind = GuardEventKind.DnsBypassAttempt,
                        Reason = $"Blocked bypass process is running: {process.ProcessName}",
                        MatchedRule = process.ProcessName
                    }, cancellationToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.ProcessRules.CheckIntervalSeconds)), cancellationToken);
        }
    }
}
