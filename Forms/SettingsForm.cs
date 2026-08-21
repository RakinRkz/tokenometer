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
            Text = "Off by default. Only turn this on if you're troubleshooting a problem — "
                   + "typically because whoever is helping you asked to see a detailed log.",
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
        // Turning it on is confirmed first — it records request URLs, response sizes
        // and timing every three minutes, which is more than someone ticking a box
        // in passing is likely to expect. Turning it back off needs no confirmation.
        verboseCheck.CheckedChanged += (_, _) =>
        {
            if (verboseCheck.Checked)
            {
                DialogResult confirm = MessageBox.Show(
                    this,
                    "Verbose logging records the details of every check — request URLs, response "
                        + "sizes and timing — every few minutes, not just failures.\n\n"
                        + "This is meant for troubleshooting a specific problem, usually at the request "
                        + "of whoever is helping you look into it. Turn it off again once you're done.\n\n"
                        + "Turn on verbose logging?",
                    "Tokenometer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (confirm != DialogResult.Yes)
                {
                    verboseCheck.Checked = false; // re-enters this handler with Checked=false, then returns here
                    return;
                }
            }

            onVerboseLoggingChanged(verboseCheck.Checked);
        };

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
