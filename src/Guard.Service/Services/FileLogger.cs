using System.Text.Json;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class FileLogger
{
    private readonly GuardOptions _options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = false
    };

    public FileLogger(IOptions<GuardOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(_options.LogDirectory);
    }

    public Task LogAsync(
        string level,
        string message,
        ViolationRequest? request = null,
        IEnumerable<string>? matchedRules = null,
        CancellationToken cancellationToken = default)
    {
        return WriteLineAsync(level, message, request, matchedRules, cancellationToken);
    }

    public Task LogAsync(
        string level,
        string message,
        GuardEvent guardEvent,
        CancellationToken cancellationToken = default)
    {
        return WriteEventLineAsync(level, message, guardEvent, cancellationToken);
    }

    private async Task WriteLineAsync(
        string level,
        string message,
        ViolationRequest? request,
        IEnumerable<string>? matchedRules,
        CancellationToken cancellationToken)
    {
        try
        {
            var filePath = Path.Combine(_options.LogDirectory, $"guard-{DateTime.UtcNow:yyyy-MM-dd}.log");

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                DeleteOlderLogFiles(filePath);

                var logEntry = new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    level,
                    message,
                    url = request?.Url,
                    host = request?.Host,
                    title = request?.Title,
                    reason = request?.Reason,
                    matchedRules = matchedRules?.ToArray() ?? request?.MatchedRules?.ToArray() ?? Array.Empty<string>()
                };

                var line = JsonSerializer.Serialize(logEntry, _jsonSerializerOptions);
                await File.AppendAllTextAsync(filePath, line + Environment.NewLine, cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
        }
    }

    private void DeleteOlderLogFiles(string currentFilePath)
    {
        try
        {
            var currentFileName = Path.GetFileName(currentFilePath);
            foreach (var file in Directory.EnumerateFiles(_options.LogDirectory, "guard-*.log"))
            {
                if (string.Equals(Path.GetFileName(file), currentFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private async Task WriteEventLineAsync(string level, string message, GuardEvent guardEvent, CancellationToken cancellationToken)
    {
        try
        {
            var filePath = Path.Combine(_options.LogDirectory, $"guard-{DateTime.UtcNow:yyyy-MM-dd}.log");

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                DeleteOlderLogFiles(filePath);

                var logEntry = new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    level,
                    message,
                    eventKind = guardEvent.EventKind.ToString(),
                    domain = guardEvent.Domain,
                    clientAddress = guardEvent.ClientAddress,
                    matchedRule = guardEvent.MatchedRule,
                    actionTaken = guardEvent.ActionTaken,
                    restoredSettings = guardEvent.RestoredSettings,
                    reason = guardEvent.Reason,
                    matchedRules = guardEvent.MatchedRules
                };

                var line = JsonSerializer.Serialize(logEntry, _jsonSerializerOptions);
                await File.AppendAllTextAsync(filePath, line + Environment.NewLine, cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
        }
    }
}
