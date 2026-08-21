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
        Action<IWin32Window> onLogin,
        Action onLogout,
        Action<IWin32Window> onGaugeDisplay,
        Action<IWin32Window> onViewLog,
        bool verboseLogging,
        Action<bool> onVerboseLoggingChanged)
    {
        Text = "Tokenometer Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(280, 255);

        var loginButton = new Button { Text = "Log in to claude.ai...", Location = new Point(15, 15), Width = 250 };
        var logoutButton = new Button { Text = "Log out", Location = new Point(15, 50), Width = 250 };
        var gaugeDisplayButton = new Button { Text = "Gauge Display...", Location = new Point(15, 85), Width = 250 };
        var viewLogButton = new Button { Text = "View log...", Location = new Point(15, 120), Width = 250 };
        var verboseCheck = new CheckBox
        {
            Text = "Verbose logging",
            AutoSize = true,
            Checked = verboseLogging,
            Location = new Point(15, 158),
        };
        var verboseHint = new Label
        {
            Text = "Records every poll, not just errors. On by default. Unticking keeps "
                   + "startup, logins and failures but drops the per-poll detail.",
            AutoSize = true,
            MaximumSize = new Size(250, 0),
            ForeColor = SystemColors.GrayText,
            Location = new Point(32, 178),
        };
        var closeButton = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(175, 218), Width = 90 };

        // Pass this form as the owner so the dialogs these open are modal to it and
        // can't end up behind it in the z-order.
        loginButton.Click += (_, _) => onLogin(this);
        logoutButton.Click += (_, _) => onLogout();
        gaugeDisplayButton.Click += (_, _) => onGaugeDisplay(this);
        viewLogButton.Click += (_, _) => onViewLog(this);
        // Applied immediately rather than on close, so the very next poll is captured.
        verboseCheck.CheckedChanged += (_, _) => onVerboseLoggingChanged(verboseCheck.Checked);

        CancelButton = closeButton;

        Controls.Add(loginButton);
        Controls.Add(logoutButton);
        Controls.Add(gaugeDisplayButton);
        Controls.Add(viewLogButton);
        Controls.Add(verboseCheck);
        Controls.Add(verboseHint);
        Controls.Add(closeButton);
    }
}
