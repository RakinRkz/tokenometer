namespace Tokenometer;

/// <summary>
/// Hub for the less-frequently-used tray actions (login, gauge display, log
/// viewing) — kept out of the tray context menu so that only "Check now" and
/// "Settings..." need to live there day to day. Buttons here delegate back to
/// TrayApplicationContext's existing logic rather than duplicating it, so this
/// is purely a UI reorganization.
/// </summary>
internal sealed class SettingsForm : Form
{
    public SettingsForm(
        Action onLogin,
        Action onLogout,
        Action onGaugeDisplay,
        Action onViewLog)
    {
        Text = "Tokenometer Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(280, 215);

        var loginButton = new Button { Text = "Log in to claude.ai...", Location = new Point(15, 15), Width = 250 };
        var logoutButton = new Button { Text = "Log out", Location = new Point(15, 50), Width = 250 };
        var gaugeDisplayButton = new Button { Text = "Gauge Display...", Location = new Point(15, 85), Width = 250 };
        var viewLogButton = new Button { Text = "View log...", Location = new Point(15, 120), Width = 250 };
        var closeButton = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(175, 160), Width = 90 };

        loginButton.Click += (_, _) => onLogin();
        logoutButton.Click += (_, _) => onLogout();
        gaugeDisplayButton.Click += (_, _) => onGaugeDisplay();
        viewLogButton.Click += (_, _) => onViewLog();

        CancelButton = closeButton;

        Controls.Add(loginButton);
        Controls.Add(logoutButton);
        Controls.Add(gaugeDisplayButton);
        Controls.Add(viewLogButton);
        Controls.Add(closeButton);
    }
}
