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
    // Anthropic hasn't published this — verify the cookie name in DevTools
    // (Application -> Cookies -> claude.ai) if capture stalls after a real login.
    private const string LoginUrl = "https://claude.ai/login";
    private const string CookieDomain = "https://claude.ai";
    private const string SessionCookieName = "sessionKey";

    private readonly WebView2 _webView = new();
    private readonly System.Windows.Forms.Timer _cookiePollTimer = new() { Interval = 1000 };
    private int _pollTickCount;

    public string? CapturedCookieHeader { get; private set; }

    public LoginForm()
    {
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
            Logger.Log("LoginForm", $"Window closed. DialogResult={DialogResult}, captured={CapturedCookieHeader is not null}");
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

        List<CoreWebView2Cookie> cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(CookieDomain);

        // Log a heartbeat every ~10s so we can see the poll is alive without spamming every tick.
        if (_pollTickCount % 10 == 0)
        {
            Logger.Log("LoginForm",
                $"Poll #{_pollTickCount}: {cookies.Count} cookies present for {CookieDomain}: " +
                string.Join(",", cookies.Select(c => c.Name)));
        }

        if (!cookies.Any(c => c.Name == SessionCookieName && !string.IsNullOrEmpty(c.Value)))
            return;

        // Forward every cookie the browser has for the domain, not just sessionKey —
        // the API may depend on CSRF/device cookies set alongside it during login.
        string cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
        Logger.Log("LoginForm",
            $"{SessionCookieName} found after {_pollTickCount} polls — captured {cookies.Count} cookies: " +
            string.Join(",", cookies.Select(c => c.Name)));

        _cookiePollTimer.Stop();
        CapturedCookieHeader = cookieHeader;
        CookieStore.Save(cookieHeader);
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
