using System.Diagnostics;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public sealed class SystemCommandRunner
{
    private readonly FileLogger _fileLogger;
    private readonly ILogger<SystemCommandRunner> _logger;

    public SystemCommandRunner(FileLogger fileLogger, ILogger<SystemCommandRunner> logger)
    {
        _fileLogger = fileLogger;
        _logger = logger;
    }

    public async Task<int> RunPowerShellAsync(string script, CancellationToken cancellationToken)
    {
        return await RunPowerShellAsync(script, logNonZeroExit: true, cancellationToken);
    }

    public async Task<int> RunPowerShellAsync(string script, bool logNonZeroExit, CancellationToken cancellationToken)
    {
        return await RunAsync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script], logNonZeroExit, cancellationToken);
    }

    public async Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return await RunAsync(fileName, arguments, logNonZeroExit: true, cancellationToken);
    }

    public async Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, bool logNonZeroExit, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return -1;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0 && logNonZeroExit)
            {
                await _fileLogger.LogAsync("WARN", $"{fileName} exited with {process.ExitCode}: {error}", cancellationToken: cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("{FileName}: {Output}", fileName, output.Trim());
            }

            return process.ExitCode;
        }
        catch (Exception exception)
        {
            await _fileLogger.LogAsync("ERROR", $"Command failed: {fileName}: {exception.Message}", cancellationToken: cancellationToken);
            return -1;
        }
    }
}
