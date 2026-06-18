using System.Diagnostics.Eventing.Reader;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class PassiveDnsMonitorService
{
    private readonly GuardOptions _options;
    private readonly BlocklistUpdateService _blocklistUpdateService;
    private readonly GuardEventService _guardEventService;
    private readonly FileLogger _fileLogger;
    private Task? _monitorTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private long _lastRecordId;
    private bool _unavailableLogged;

    public PassiveDnsMonitorService(
        IOptions<GuardOptions> options,
        BlocklistUpdateService blocklistUpdateService,
        GuardEventService guardEventService,
        FileLogger fileLogger)
    {
        _options = options.Value;
        _blocklistUpdateService = blocklistUpdateService;
        _guardEventService = guardEventService;
        _fileLogger = fileLogger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.PassiveDnsMonitor.Enabled || _monitorTask is not null)
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
            await PollAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PassiveDnsMonitor.PollIntervalSeconds)), cancellationToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _blocklistUpdateService.RefreshIfDueAsync(cancellationToken);
            var milliseconds = Math.Max(1, _options.PassiveDnsMonitor.LookbackMinutesOnStartup) * 60 * 1000;
            var query = $"*[System[TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]";
            var eventQuery = new EventLogQuery(_options.PassiveDnsMonitor.EventLogName, PathType.LogName, query)
            {
                ReverseDirection = true
            };
            using var reader = new EventLogReader(eventQuery);
            var newestRecordId = _lastRecordId;
            var processed = 0;

            for (EventRecord? record = reader.ReadEvent(); record is not null && processed < 200; record = reader.ReadEvent())
            {
                using (record)
                {
                    var recordId = record.RecordId ?? 0;
                    if (recordId <= _lastRecordId)
                    {
                        break;
                    }

                    newestRecordId = Math.Max(newestRecordId, recordId);
                    processed++;
                    await HandleRecordAsync(record, cancellationToken);
                }
            }

            _lastRecordId = Math.Max(_lastRecordId, newestRecordId);
        }
        catch (Exception exception)
        {
            if (!_unavailableLogged)
            {
                _unavailableLogged = true;
                await _fileLogger.LogAsync("WARN", $"Passive DNS monitor unavailable: {exception.Message}", cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleRecordAsync(EventRecord record, CancellationToken cancellationToken)
    {
        var text = SafeReadXml(record);
        if (!PassiveDnsEventParser.TryExtractDomain(text, out var domain))
        {
            text = SafeFormatDescription(record);
        }

        if (!PassiveDnsEventParser.TryExtractDomain(text, out domain))
        {
            return;
        }

        if (_blocklistUpdateService.Current.IsBlocked(domain, out var matchedRule))
        {
            await _guardEventService.HandleAsync(new GuardEvent
            {
                EventKind = GuardEventKind.BlockedDomain,
                Domain = domain,
                MatchedRule = matchedRule,
                Reason = "Passive DNS monitor observed a blocked domain query."
            }, cancellationToken);
        }
    }

    private static string? SafeReadXml(EventRecord record)
    {
        try
        {
            return record.ToXml();
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeFormatDescription(EventRecord record)
    {
        try
        {
            return record.FormatDescription();
        }
        catch
        {
            return null;
        }
    }
}
