using AdultContentShutdownGuard.Guard.Service.Models;

namespace AdultContentShutdownGuard.Guard.Service.Services;

public interface IOverlayLauncher
{
    Task TriggerForViolationAsync(GuardEvent guardEvent, CancellationToken cancellationToken);
}
