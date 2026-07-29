using System.Runtime.InteropServices;

namespace AdultContentShutdownGuard.Guard.Overlay;

internal sealed class CountdownOverlayForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(12, 16, 23);
    private static readonly Color AccentColor = Color.FromArgb(255, 193, 7);
    private readonly Label _countdownLabel;
    private bool _allowProgrammaticClose;

    public CountdownOverlayForm(Rectangle bounds)
    {
        Bounds = bounds;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.None;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            ForeColor = Color.White,
            Text = "ĐANG TẠM DỪNG"
        };

        var message = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 15, FontStyle.Regular),
            ForeColor = Color.FromArgb(220, 225, 235),
            Text = "Quy tắc an toàn vừa được kích hoạt.\nMàn hình sẽ tự mở lại sau"
        };

        _countdownLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Consolas", 68, FontStyle.Bold),
            ForeColor = AccentColor,
            Text = "05:00"
        };

        var guidance = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 13, FontStyle.Regular),
            ForeColor = Color.FromArgb(166, 177, 194),
            Text = "Hãy rời khỏi trang vừa mở và chờ hết thời gian."
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = BackgroundColor,
            Padding = new Padding(48)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(message, 0, 1);
        layout.Controls.Add(_countdownLabel, 0, 2);
        layout.Controls.Add(guidance, 0, 3);
        layout.SetCellPosition(title, new TableLayoutPanelCellPosition(0, 0));
        layout.SetCellPosition(message, new TableLayoutPanelCellPosition(0, 1));
        layout.SetCellPosition(_countdownLabel, new TableLayoutPanelCellPosition(0, 2));
        layout.SetCellPosition(guidance, new TableLayoutPanelCellPosition(0, 3));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(layout);
        layout.Location = new Point(
            Math.Max(24, (ClientSize.Width - layout.PreferredSize.Width) / 2),
            Math.Max(24, (ClientSize.Height - layout.PreferredSize.Height) / 2));
        Resize += (_, _) => layout.Location = new Point(
            Math.Max(24, (ClientSize.Width - layout.PreferredSize.Width) / 2),
            Math.Max(24, (ClientSize.Height - layout.PreferredSize.Height) / 2));
    }

    public void UpdateCountdown(TimeSpan remaining)
    {
        var roundedUpSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        _countdownLabel.Text = TimeSpan.FromSeconds(roundedUpSeconds).ToString(@"mm\:ss");
    }

    public void EnsureTopmost()
    {
        TopMost = true;
        SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    public void AllowProgrammaticClose()
    {
        _allowProgrammaticClose = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowProgrammaticClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            EnsureTopmost();
            return;
        }

        base.OnFormClosing(e);
    }

    private const int HwndTopmost = -1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        int hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
