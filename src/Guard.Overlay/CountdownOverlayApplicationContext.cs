using System.Diagnostics;

namespace AdultContentShutdownGuard.Guard.Overlay;

internal sealed class CountdownOverlayApplicationContext : ApplicationContext
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly TimeSpan _duration;
    private readonly List<CountdownOverlayForm> _forms = [];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 200 };
    private bool _isFinishing;

    public CountdownOverlayApplicationContext(TimeSpan duration)
    {
        _duration = duration;

        foreach (var screen in Screen.AllScreens)
        {
            var form = new CountdownOverlayForm(screen.Bounds);
            _forms.Add(form);
            form.Show();
        }

        RefreshForms();
        _timer.Tick += (_, _) => RefreshForms();
        _timer.Start();
    }

    private void RefreshForms()
    {
        var remaining = _duration - _elapsed.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            Finish();
            return;
        }

        foreach (var form in _forms)
        {
            form.UpdateCountdown(remaining);
            form.EnsureTopmost();
        }
    }

    private void Finish()
    {
        if (_isFinishing)
        {
            return;
        }

        _isFinishing = true;
        _timer.Stop();

        foreach (var form in _forms)
        {
            form.AllowProgrammaticClose();
            form.Close();
        }

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            foreach (var form in _forms)
            {
                form.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
