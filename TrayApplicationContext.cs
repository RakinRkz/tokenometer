namespace Tokenometer;

/// <summary>
/// Composition root: owns the concrete storage/fetcher implementations and wires
/// them into UsageClient, then handles the tray icon/menu/popup UI on top.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string Category = "Tray";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);

    private readonly ISignInState _signInState = new SignInState();
    private readonly IOrganizationSettings _organizationSettings = new OrganizationSettings();
    private readonly IGaugeSettings _gaugeSettings = new GaugeSettings();

    private readonly UsageClient _usageClient;
    private readonly UsagePoller _poller;
    private readonly GaugePopupForm _popup = new();
    private readonly NotifyIcon _trayIcon;

    private UsageSnapshot? _lastSnapshot;
    private Exception? _lastError;

    public TrayApplicationContext()
    {
        _usageClient = new UsageClient(_signInState, _organizationSettings, new BrowserUsageFetcher());
        _poller = new UsagePoller(_usageClient, PollInterval);
        _poller.UsageUpdated += OnUsageUpdated;
        _poller.FetchFailed += OnFetchFailed;

        var menu = new ContextMenuStrip();
        var checkNowItem = menu.Items.Add("Check now");
        menu.Items.Add(new ToolStripSeparator());
        var settingsItem = menu.Items.Add("Settings...");
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = menu.Items.Add("Exit");

        checkNowItem.Click += async (_, _) => { Logger.Log(Category, "Menu: Check now clicked."); await _poller.PollOnceAsync(); };
        settingsItem.Click += (_, _) => { Logger.Log(Category, "Menu: Settings clicked."); ShowSettings(); };
        exitItem.Click += (_, _) => { Logger.Log(Category, "Menu: Exit clicked."); ExitApplication(); };

        _trayIcon = new NotifyIcon
        {
            Icon = GaugeRenderer.RenderTrayIcon(0, 0, isStale: true, _gaugeSettings.Load()),
            Text = "Tokenometer — fetching usage...",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowPopup();
        };

        _popup.LoginRequested += (_, _) =>
        {
            Logger.Log(Category, "Popup login link clicked.");
            ShowLogin();
        };

        Logger.Log(Category, "TrayApplicationContext constructed, starting poller.");
        _poller.Start();
    }

    private void OnUsageUpdated(object? sender, UsageSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _lastError = null;
        bool isAuthenticated = _usageClient.IsAuthenticated;
        Logger.Log(Category,
            $"UsageUpdated: session={snapshot.SessionPercent:0}%, weekly={snapshot.WeeklyPercent:0}%, authenticated={isAuthenticated}");

        GaugeThresholds thresholds = _gaugeSettings.Load();
        _trayIcon.Icon?.Dispose();
        _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(snapshot.SessionPercent, snapshot.WeeklyPercent, isStale: false, thresholds);
        _trayIcon.Text = BuildTooltip(snapshot, isAuthenticated, thresholds);

        if (_popup.Visible)
            _popup.UpdateSnapshot(snapshot, isAuthenticated, thresholds);
    }

    private static string BuildTooltip(UsageSnapshot snapshot, bool isAuthenticated, GaugeThresholds thresholds)
    {
        double session = GaugeDisplay.ToDisplayPercent(snapshot.SessionPercent, thresholds.Invert);
        double weekly = GaugeDisplay.ToDisplayPercent(snapshot.WeeklyPercent, thresholds.Invert);
        string suffix = thresholds.Invert ? " left" : "";
        string prefix = isAuthenticated ? "" : "[mock] ";
        return $"{prefix}5-hour: {session:0}%{suffix} · Weekly: {weekly:0}%{suffix}";
    }

    private void OnFetchFailed(object? sender, Exception ex)
    {
        _lastError = ex;
        Logger.Log(Category, $"FetchFailed: {ex.Message}");

        GaugeThresholds thresholds = _gaugeSettings.Load();
        _trayIcon.Icon?.Dispose();
        _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(0, 0, isStale: true, thresholds);
        _trayIcon.Text = "Tokenometer — fetch failed, click for details";

        if (_popup.Visible)
            _popup.ShowFetchError(ex, thresholds);
    }

    private void ShowPopup()
    {
        Logger.Log(Category, $"Popup opened. lastError={_lastError is not null}, hasSnapshot={_lastSnapshot is not null}");
        GaugeThresholds thresholds = _gaugeSettings.Load();
        if (_lastError is { } error)
            _popup.ShowFetchError(error, thresholds);
        else if (_lastSnapshot is { } snapshot)
            _popup.UpdateSnapshot(snapshot, _usageClient.IsAuthenticated, thresholds);
        _popup.ShowNear(Cursor.Position);
    }

    private void ShowSettings()
    {
        Logger.Log(Category, "Opening SettingsForm.");
        using var settingsForm = new SettingsForm(
            onLogin: ShowLogin,
            onLogout: LogOut,
            onGaugeDisplay: ShowGaugeSettings,
            onViewLog: ViewLog);
        settingsForm.ShowDialog();
        Logger.Log(Category, "SettingsForm closed.");
    }

    // owner is null when reached from the popup's login link, which has no window
    // worth parenting to; SettingsForm passes itself.
    private void ShowLogin(IWin32Window? owner = null)
    {
        Logger.Log(Category, "Opening LoginForm dialog.");
        using var loginForm = new LoginForm(_signInState, _organizationSettings);
        DialogResult result = loginForm.ShowDialog(owner);
        Logger.Log(Category, $"LoginForm closed with result={result}.");
        if (result == DialogResult.OK)
            _ = _poller.PollOnceAsync();
    }

    private async void LogOut()
    {
        Logger.Log(Category, "Logging out — clearing sign-in marker, organization id, and browser session.");
        // Everything that can throw stays inside the try: this is async void, so an
        // escaping exception would surface on the ThreadException handler rather
        // than being reported against the logout that caused it.
        try
        {
            _signInState.Clear();
            // The organization id is per-account, so it goes too. Keeping it would
            // point the next account's fetch at the previous organization whenever
            // login fails to re-capture lastActiveOrg — and clearing it is what
            // makes the documented "log out and back in to retry org detection"
            // actually retry rather than reuse the old value.
            _organizationSettings.Clear();
            await _usageClient.ClearBrowserSessionAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(Category, $"Logout failed: {ex}");
        }
        _ = _poller.PollOnceAsync();
    }

    private void ShowGaugeSettings(IWin32Window? owner = null)
    {
        using var form = new GaugeSettingsForm(_gaugeSettings.Load());
        DialogResult result = form.ShowDialog(owner);
        Logger.Log(Category, $"Gauge Display dialog closed with result={result}.");
        if (result != DialogResult.OK || form.Result is null)
            return;

        _gaugeSettings.Save(form.Result);
        RefreshGaugeDisplay();
    }

    private void RefreshGaugeDisplay()
    {
        GaugeThresholds thresholds = _gaugeSettings.Load();

        if (_lastError is { } error)
        {
            _trayIcon.Icon?.Dispose();
            _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(0, 0, isStale: true, thresholds);
            if (_popup.Visible)
                _popup.ShowFetchError(error, thresholds);
            return;
        }

        if (_lastSnapshot is not { } snapshot)
            return;

        bool isAuthenticated = _usageClient.IsAuthenticated;
        _trayIcon.Icon?.Dispose();
        _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(snapshot.SessionPercent, snapshot.WeeklyPercent, isStale: false, thresholds);
        _trayIcon.Text = BuildTooltip(snapshot, isAuthenticated, thresholds);
        if (_popup.Visible)
            _popup.UpdateSnapshot(snapshot, isAuthenticated, thresholds);
    }

    private void ViewLog(IWin32Window? owner = null)
    {
        try
        {
            if (!File.Exists(Logger.LogFilePath))
                File.WriteAllText(Logger.LogFilePath, string.Empty);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Logger.LogFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Log(Category, $"Failed to open log file: {ex}");
            MessageBox.Show(owner, $"Couldn't open log file: {ex.Message}", "Tokenometer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExitApplication()
    {
        Logger.Log(Category, "Exiting application.");
        _trayIcon.Visible = false;
        _poller.Dispose();
        _usageClient.Dispose();
        _trayIcon.Dispose();
        _popup.Dispose();
        Application.Exit();
    }
}
