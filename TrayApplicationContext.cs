namespace Tokenometer;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string Category = "Tray";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);

    private readonly UsageClient _usageClient = new();
    private readonly UsagePoller _poller;
    private readonly GaugePopupForm _popup = new();
    private readonly NotifyIcon _trayIcon;

    private UsageSnapshot? _lastSnapshot;
    private Exception? _lastError;

    public TrayApplicationContext()
    {
        _poller = new UsagePoller(_usageClient, PollInterval);
        _poller.UsageUpdated += OnUsageUpdated;
        _poller.FetchFailed += OnFetchFailed;

        var menu = new ContextMenuStrip();
        var loginItem = menu.Items.Add("Log in to claude.ai...");
        var logoutItem = menu.Items.Add("Log out");
        menu.Items.Add(new ToolStripSeparator());
        var setOrgItem = menu.Items.Add("Set Organization ID...");
        var gaugeColorsItem = menu.Items.Add("Gauge Colors...");
        menu.Items.Add(new ToolStripSeparator());
        var checkNowItem = menu.Items.Add("Check now");
        var viewLogItem = menu.Items.Add("View log...");
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = menu.Items.Add("Exit");

        loginItem.Click += (_, _) => { Logger.Log(Category, "Menu: Log in clicked."); ShowLogin(); };
        logoutItem.Click += (_, _) => { Logger.Log(Category, "Menu: Log out clicked."); LogOut(); };
        setOrgItem.Click += (_, _) => { Logger.Log(Category, "Menu: Set Organization ID clicked."); ShowSetOrganizationId(); };
        gaugeColorsItem.Click += (_, _) => { Logger.Log(Category, "Menu: Gauge Colors clicked."); ShowGaugeSettings(); };
        checkNowItem.Click += async (_, _) => { Logger.Log(Category, "Menu: Check now clicked."); await _poller.PollOnceAsync(); };
        viewLogItem.Click += (_, _) => ViewLog();
        exitItem.Click += (_, _) => { Logger.Log(Category, "Menu: Exit clicked."); ExitApplication(); };

        _trayIcon = new NotifyIcon
        {
            Icon = GaugeRenderer.RenderTrayIcon(0, 0, isStale: true),
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

        _trayIcon.Icon?.Dispose();
        _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(snapshot.SessionPercent, snapshot.WeeklyPercent, isStale: false);
        _trayIcon.Text = isAuthenticated
            ? $"5-hour: {snapshot.SessionPercent:0}% · Weekly: {snapshot.WeeklyPercent:0}%"
            : $"[mock] 5-hour: {snapshot.SessionPercent:0}% · Weekly: {snapshot.WeeklyPercent:0}%";

        if (_popup.Visible)
            _popup.UpdateSnapshot(snapshot, isAuthenticated);
    }

    private void OnFetchFailed(object? sender, Exception ex)
    {
        _lastError = ex;
        Logger.Log(Category, $"FetchFailed: {ex.Message}");

        _trayIcon.Icon?.Dispose();
        _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(0, 0, isStale: true);
        _trayIcon.Text = "Tokenometer — fetch failed, click for details";

        if (_popup.Visible)
            _popup.ShowFetchError(ex);
    }

    private void ShowPopup()
    {
        Logger.Log(Category, $"Popup opened. lastError={_lastError is not null}, hasSnapshot={_lastSnapshot is not null}");
        if (_lastError is { } error)
            _popup.ShowFetchError(error);
        else if (_lastSnapshot is { } snapshot)
            _popup.UpdateSnapshot(snapshot, _usageClient.IsAuthenticated);
        _popup.ShowNear(Cursor.Position);
    }

    private void ShowLogin()
    {
        Logger.Log(Category, "Opening LoginForm dialog.");
        using var loginForm = new LoginForm();
        DialogResult result = loginForm.ShowDialog();
        Logger.Log(Category, $"LoginForm closed with result={result}.");
        if (result == DialogResult.OK)
            _ = _poller.PollOnceAsync();
    }

    private async void LogOut()
    {
        Logger.Log(Category, "Logging out — clearing stored cookie and browser session.");
        CookieStore.Clear();
        try
        {
            await _usageClient.ClearBrowserSessionAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(Category, $"Failed to clear browser session: {ex}");
        }
        _ = _poller.PollOnceAsync();
    }

    private void ShowSetOrganizationId()
    {
        using var prompt = new PromptForm(
            "Set Organization ID",
            "Find this in DevTools: the usage request's URL looks like\n" +
            "/api/organizations/{id}/usage — paste the {id} part below.",
            OrganizationSettings.Load() ?? "");

        DialogResult result = prompt.ShowDialog();
        Logger.Log(Category, $"Set Organization ID dialog closed with result={result}.");
        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(prompt.InputText))
            return;

        OrganizationSettings.Save(prompt.InputText);
        _ = _poller.PollOnceAsync();
    }

    private void ShowGaugeSettings()
    {
        using var form = new GaugeSettingsForm(GaugeSettings.Load());
        DialogResult result = form.ShowDialog();
        Logger.Log(Category, $"Gauge Colors dialog closed with result={result}.");
        if (result != DialogResult.OK || form.Result is null)
            return;

        GaugeSettings.Save(form.Result);
        RefreshGaugeDisplay();
    }

    private void RefreshGaugeDisplay()
    {
        if (_lastError is { } error)
        {
            _trayIcon.Icon?.Dispose();
            _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(0, 0, isStale: true);
            if (_popup.Visible)
                _popup.ShowFetchError(error);
            return;
        }

        if (_lastSnapshot is not { } snapshot)
            return;

        bool isAuthenticated = _usageClient.IsAuthenticated;
        _trayIcon.Icon?.Dispose();
        _trayIcon.Icon = GaugeRenderer.RenderTrayIcon(snapshot.SessionPercent, snapshot.WeeklyPercent, isStale: false);
        if (_popup.Visible)
            _popup.UpdateSnapshot(snapshot, isAuthenticated);
    }

    private void ViewLog()
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
            MessageBox.Show($"Couldn't open log file: {ex.Message}", "Tokenometer",
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
