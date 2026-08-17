namespace Tokenometer;

internal sealed class GaugePopupForm : Form
{
    private readonly PictureBox _sessionGauge = new() { Size = new Size(140, 140) };
    private readonly PictureBox _weeklyGauge = new() { Size = new Size(140, 140) };
    private readonly Label _statusLabel = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly Label _resetLabel = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly LinkLabel _loginLink = new() { Text = "Log in to claude.ai...", AutoSize = true };

    public event EventHandler? LoginRequested;

    public GaugePopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(340, 260);
        BackColor = Color.FromArgb(32, 32, 36);
        TopMost = true;
        ShowInTaskbar = false;

        _sessionGauge.Location = new Point(20, 20);
        _weeklyGauge.Location = new Point(180, 20);
        _statusLabel.Location = new Point(20, 175);
        _resetLabel.Location = new Point(20, 198);
        _loginLink.Location = new Point(20, 224);
        _loginLink.LinkColor = Color.LightSkyBlue;
        _loginLink.Visible = false;
        _loginLink.LinkClicked += (_, _) => LoginRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(_sessionGauge);
        Controls.Add(_weeklyGauge);
        Controls.Add(_statusLabel);
        Controls.Add(_resetLabel);
        Controls.Add(_loginLink);

        Deactivate += (_, _) => Hide();
    }

    public void ShowNear(Point trayIconLocation)
    {
        var workingArea = Screen.FromPoint(trayIconLocation).WorkingArea;
        int x = Math.Min(trayIconLocation.X, workingArea.Right - Width);
        int y = Math.Min(trayIconLocation.Y, workingArea.Bottom - Height);
        Location = new Point(Math.Max(x, workingArea.Left), Math.Max(y, workingArea.Top));
        Show();
        Activate();
    }

    public void UpdateSnapshot(UsageSnapshot snapshot, bool isAuthenticated, GaugeThresholds thresholds)
    {
        _sessionGauge.Image?.Dispose();
        _weeklyGauge.Image?.Dispose();
        _sessionGauge.Image = GaugeRenderer.RenderLargeGauge(
            140, snapshot.SessionPercent, GaugeDisplay.Caption("5-hour", thresholds.Invert), isStale: false, thresholds);
        _weeklyGauge.Image = GaugeRenderer.RenderLargeGauge(
            140, snapshot.WeeklyPercent, GaugeDisplay.Caption("Weekly", thresholds.Invert), isStale: false, thresholds);

        _statusLabel.Text = isAuthenticated
            ? $"Updated {snapshot.FetchedAt:t}"
            : $"Mock data — updated {snapshot.FetchedAt:t}";
        // resetsAt comes from claude.ai as a UTC timestamp (e.g. "...+00:00");
        // ToLocalTime() converts it to the user's clock before formatting —
        // formatting a DateTimeOffset directly uses its own embedded offset,
        // not the local one, which showed UTC time mislabeled as local.
        _resetLabel.Text = snapshot.SessionResetsAt is { } resetsAt
            ? $"5-hour resets {resetsAt.ToLocalTime():t}"
            : string.Empty;
        _loginLink.Visible = !isAuthenticated;
    }

    public void ShowFetchError(Exception ex, GaugeThresholds thresholds)
    {
        _sessionGauge.Image?.Dispose();
        _weeklyGauge.Image?.Dispose();
        _sessionGauge.Image = GaugeRenderer.RenderLargeGauge(140, 0, "5-hour", isStale: true, thresholds);
        _weeklyGauge.Image = GaugeRenderer.RenderLargeGauge(140, 0, "Weekly", isStale: true, thresholds);
        _statusLabel.Text = "Fetch failed";
        _resetLabel.Text = ex.Message;
        _loginLink.Visible = true;
        _loginLink.Text = "Re-check login...";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionGauge.Image?.Dispose();
            _weeklyGauge.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
