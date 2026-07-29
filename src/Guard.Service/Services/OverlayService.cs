using System.Diagnostics;
using AdultContentShutdownGuard.Guard.Service.Models;
using Microsoft.Extensions.Options;

namespace AdultContentShutdownGuard.Guard.Service.Services;

/// <summary>
/// Yêu cầu Task Scheduler mở màn hình tạm dừng trong phiên người dùng đang đăng nhập.
/// Dịch vụ Windows không tự vẽ giao diện vì nó chạy ở Session 0.
/// </summary>
public sealed class OverlayService : IOverlayLauncher
{
    private const string TaskName = "AdultContentShutdownGuard Overlay";
    private static readonly string SchtasksPath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
    private readonly GuardOptions _options;
    private readonly SystemCommandRunner _commandRunner;
    private readonly FileLogger _fileLogger;
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private long _nextLaunchAllowedTimestamp;

    public OverlayService(
        IOptions<GuardOptions> options,
        SystemCommandRunner commandRunner,
        FileLogger fileLogger)
    {
        _options = options.Value;
        _commandRunner = commandRunner;
        _fileLogger = fileLogger;
    }

    public async Task TriggerForViolationAsync(GuardEvent guardEvent, CancellationToken cancellationToken)
    {
        if (!_options.Overlay.Enabled || !ShouldShowOverlay(guardEvent.EventKind))
        {
            return;
        }

        if (_options.Overlay.DurationSeconds is < 5 or > 300)
        {
            await _fileLogger.LogAsync("ERROR", "Thời lượng bộ đếm phải từ 5 đến 300 giây; không khởi động bộ đếm.", cancellationToken: cancellationToken);
            return;
        }

        await _launchLock.WaitAsync(cancellationToken);
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (now < Interlocked.Read(ref _nextLaunchAllowedTimestamp))
            {
                await _fileLogger.LogAsync("INFO", "Bỏ qua yêu cầu mới vì bộ đếm hiện tại vẫn đang chạy.", cancellationToken: cancellationToken);
                return;
            }

            var exitCode = await _commandRunner.RunAsync(
                SchtasksPath,
                ["/Run", "/TN", TaskName],
                logNonZeroExit: false,
                cancellationToken);

            var message = exitCode == 0
                ? $"Đã yêu cầu hiển thị bộ đếm cho sự kiện {guardEvent.EventKind}."
                : $"Không khởi động được bộ đếm cho sự kiện {guardEvent.EventKind}; schtasks.exe trả về mã {exitCode}.";
            await _fileLogger.LogAsync(exitCode == 0 ? "INFO" : "WARN", message, cancellationToken: cancellationToken);

            if (exitCode == 0)
            {
                var cooldownSeconds = _options.Overlay.DurationSeconds + Math.Max(15, _options.DebounceSeconds);
                Interlocked.Exchange(ref _nextLaunchAllowedTimestamp, now + (long)(cooldownSeconds * Stopwatch.Frequency));
            }
        }
        finally
        {
            _launchLock.Release();
        }
    }

    private static bool ShouldShowOverlay(GuardEventKind eventKind)
    {
        return eventKind is GuardEventKind.BlockedDomain or GuardEventKind.PolicyViolation;
    }
}
