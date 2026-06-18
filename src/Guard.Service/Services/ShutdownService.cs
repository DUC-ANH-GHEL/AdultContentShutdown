using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class ShutdownService
{
    private readonly GuardOptions _options;
    private readonly FileLogger _fileLogger;
    private readonly ILogger<ShutdownService> _logger;

    public ShutdownService(IOptions<GuardOptions> options, FileLogger fileLogger, ILogger<ShutdownService> logger)
    {
        _options = options.Value;
        _fileLogger = fileLogger;
        _logger = logger;
    }

    public async Task ShutdownNowAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetFileName(_options.ShutdownCommand), "shutdown.exe", StringComparison.OrdinalIgnoreCase))
        {
            var invalidCommandMessage = $"Refused unsafe shutdown command: {_options.ShutdownCommand}";
            _logger.LogError(invalidCommandMessage);
            await _fileLogger.LogAsync("ERROR", invalidCommandMessage, cancellationToken: cancellationToken);
            return;
        }

        var message = $"Executing immediate shutdown: {_options.ShutdownCommand} {_options.ShutdownArguments}";
        _logger.LogWarning(message);
        await _fileLogger.LogAsync("WARN", message, cancellationToken: cancellationToken);

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _options.ShutdownCommand,
                Arguments = _options.ShutdownArguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(processStartInfo);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to execute shutdown command.");
            await _fileLogger.LogAsync("ERROR", $"Failed to execute shutdown command: {exception.Message}", cancellationToken: cancellationToken);
        }
    }
}
