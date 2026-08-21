using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Tokenometer;

/// <summary>
/// Hosts an embedded browser pointed at claude.ai's login page. Once the user
/// finishes logging in, the resulting session cookie is captured automatically —
/// no manual DevTools copy/paste. The WebView2 profile persists under %AppData%,
/// so this window is only needed again once the session actually expires.
/// </summary>
internal sealed class LoginForm : Form
{
    // Anthropic hasn't published either of these — verify in DevTools
    // (Application -> Cookies -> claude.ai) if capture stalls after a real login.
    private const string LoginUrl = "https://claude.ai/login";
    private const string CookieDomain = "https://claude.ai";
    private const string SessionCookieName = "sessionKey";
    private const string ActiveOrganizationCookieName = "lastActiveOrg";

    /// <summary>
    /// How many extra 1s polls to allow after sessionKey appears, waiting for the
    /// organization cookie. sessionKey is set the instant authentication succeeds,
    /// but lastActiveOrg only lands once claude.ai redirects into the organization —
    /// a second or two later. Closing on first sight of sessionKey therefore missed
    /// it on a genuinely fresh profile every time. A returning user's profile already
    /// had the cookie, which is why this only ever bit first-time logins.
    /// </summary>
    private const int MaxOrganizationWaitTicks = 20;

    private readonly ISignInState _signInState;
    private readonly IOrganizationSettings _organizationSettings;
    private readonly WebView2 _webView = new();
    private readonly System.Windows.Forms.Timer _cookiePollTimer = new() { Interval = 1000 };
    private int _pollTickCount;
    private bool _captured;
    private int _ticksWaitingForOrganization = -1;

    public LoginForm(ISignInState signInState, IOrganizationSettings organizationSettings)
    {
        _signInState = signInState;
        _organizationSettings = organizationSettings;
        Text = "Log in to Claude.ai";
        Width = 480;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        _cookiePollTimer.Tick += async (_, _) => await CheckForSessionCookieAsync();
        FormClosed += (_, _) =>
        {
            _cookiePollTimer.Stop();
            Logger.Log("LoginForm", $"Window closed. DialogResult={DialogResult}, captured={_captured}");
        };
        Load += OnLoadAsync;

        Logger.Log("LoginForm", "Window constructed.");
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        try
        {
            CoreWebView2Environment environment = await SharedBrowserEnvironment.GetAsync();
            await _webView.EnsureCoreWebView2Async(environment);
            Logger.Log("LoginForm", "WebView2 ready (shared environment).");
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            Logger.Log("LoginForm", $"WebView2 runtime not found: {ex}");
            MessageBox.Show(
                this,
                "The WebView2 runtime isn't installed. Install the Evergreen runtime from " +
                "https://developer.microsoft.com/microsoft-edge/webview2/ and try again.",
                "Tokenometer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        _webView.CoreWebView2.NavigationStarting += (_, args) =>
            Logger.Log("LoginForm", $"Navigating to {args.Uri}");
        _webView.CoreWebView2.NavigationCompleted += (_, args) =>
            Logger.Log("LoginForm", $"Navigation completed. Success={args.IsSuccess}, WebErrorStatus={args.WebErrorStatus}, url={_webView.CoreWebView2.Source}");

        Logger.Log("LoginForm", $"Navigating to login page: {LoginUrl}");
        _webView.CoreWebView2.Navigate(LoginUrl);
        _cookiePollTimer.Start();
    }

    private async Task CheckForSessionCookieAsync()
    {
        _pollTickCount++;
        if (_webView.CoreWebView2 is null)
            return;

        List<CoreWebView2Cookie> rawCookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(CookieDomain);
        var cookies = rawCookies.Select(c => (c.Name, c.Value)).ToList();

        // Log a heartbeat every ~10s so we can see the poll is alive without spamming every tick.
        if (_pollTickCount % 10 == 0)
        {
            Logger.Log("LoginForm",
                $"Poll #{_pollTickCount}: {cookies.Count} cookies present for {CookieDomain}: " +
                string.Join(",", cookies.Select(c => c.Name)));
        }

        if (!BrowserCookies.Contains(cookies, SessionCookieName))
            return;

        // claude.ai sets this to the active organization's id during login — used
        // instead of requiring a manual DevTools lookup.
        string? organizationId = BrowserCookies.FindValue(cookies, ActiveOrganizationCookieName);

        // Don't close the moment sessionKey shows up: the organization cookie trails
        // it by a second or two, and leaving without it strands the user with no
        // usage URL and no way to set one by hand. Keep polling for a bounded window.
        if (organizationId is null)
        {
            _ticksWaitingForOrganization++;
            if (_ticksWaitingForOrganization == 0)
            {
                Logger.Log("LoginForm",
                    $"{SessionCookieName} found after {_pollTickCount} polls; waiting up to " +
                    $"{MaxOrganizationWaitTicks}s for {ActiveOrganizationCookieName}.");
            }

            if (_ticksWaitingForOrganization < MaxOrganizationWaitTicks)
                return;
        }

        Logger.Log("LoginForm",
            $"Capturing after {_pollTickCount} polls — {cookies.Count} cookies present: " +
            string.Join(",", cookies.Select(c => c.Name)));

        _cookiePollTimer.Stop();
        _captured = true;
        _signInState.MarkSignedIn();

        if (organizationId is not null)
        {
            _organizationSettings.Save(organizationId);
            Logger.Log("LoginForm",
                $"Auto-detected organization id from {ActiveOrganizationCookieName} cookie " +
                $"after {Math.Max(_ticksWaitingForOrganization, 0)}s: {organizationId}");
        }
        else
        {
            Logger.Log("LoginForm",
                $"No {ActiveOrganizationCookieName} cookie after waiting {MaxOrganizationWaitTicks}s — " +
                "organization id not auto-detected.");
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cookiePollTimer.Dispose();
            _webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
