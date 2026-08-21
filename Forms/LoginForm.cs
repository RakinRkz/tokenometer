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

    private readonly ISignInState _signInState;
    private readonly IOrganizationSettings _organizationSettings;
    private readonly WebView2 _webView = new();
    private readonly System.Windows.Forms.Timer _cookiePollTimer = new() { Interval = 1000 };
    private int _pollTickCount;
    private bool _captured;

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

        Logger.Log("LoginForm",
            $"{SessionCookieName} found after {_pollTickCount} polls — {cookies.Count} cookies present: " +
            string.Join(",", cookies.Select(c => c.Name)));

        _cookiePollTimer.Stop();
        _captured = true;
        _signInState.MarkSignedIn();

        // claude.ai sets this to the active organization's id during login — use it
        // instead of requiring a manual DevTools lookup. Falls back to whatever's
        // already stored (or unset) if the cookie is ever renamed or missing.
        string? organizationId = BrowserCookies.FindValue(cookies, ActiveOrganizationCookieName);
        if (organizationId is not null)
        {
            _organizationSettings.Save(organizationId);
            Logger.Log("LoginForm", $"Auto-detected organization id from {ActiveOrganizationCookieName} cookie: {organizationId}");
        }
        else
        {
            Logger.Log("LoginForm", $"No {ActiveOrganizationCookieName} cookie found — organization id not auto-detected.");
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
