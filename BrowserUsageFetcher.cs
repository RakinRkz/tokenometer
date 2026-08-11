using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Tokenometer;

/// <summary>
/// claude.ai sits behind Cloudflare's bot-management challenge, which a bare
/// HttpClient can never pass — its TLS/network fingerprint doesn't match a real
/// browser, no matter what cookies are attached. Instead, this hosts a hidden
/// WebView2 control on the shared, already-logged-in profile and runs fetch()
/// *inside* that browser engine, so the request carries the same fingerprint and
/// cookies that already passed the challenge during login.
/// </summary>
internal sealed class BrowserUsageFetcher : IDisposable
{
    private const string Category = "BrowserUsageFetcher";
    private const string HomeUrl = "https://claude.ai/settings/usage";

    private readonly Form _hiddenHost;
    private readonly WebView2 _webView = new();
    private Task? _readyTask;

    public BrowserUsageFetcher()
    {
        _hiddenHost = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000),
            Size = new Size(1, 1),
        };
        _hiddenHost.Controls.Add(_webView);
        // Force handle creation without ever calling Show() — the control never
        // becomes visible on screen or in the taskbar.
        _ = _hiddenHost.Handle;
    }

    private const int PollIntervalMs = 150;
    private const int MaxPollAttempts = 40; // ~6s timeout

    public async Task<(bool Ok, int Status, string Body)> FetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        // ExecuteScriptAsync in this WebView2 runtime does NOT auto-await a
        // returned Promise — confirmed by probing with a 2s setTimeout-Promise,
        // which came back in ~50ms holding the un-awaited Promise object. So
        // instead of relying on its return value, kick off the fetch and have the
        // page write the result to a global; then poll that global for it.
        string kickoffScript = $$"""
            window.__tokenometerResult = null;
            fetch({{JsonSerializer.Serialize(url)}}, { credentials: 'include' })
                .then(response => response.text().then(text => {
                    window.__tokenometerResult = JSON.stringify({ ok: response.ok, status: response.status, body: text });
                }))
                .catch(error => {
                    window.__tokenometerResult = JSON.stringify({ ok: false, status: -1, body: String(error) });
                });
            """;
        await _webView.CoreWebView2.ExecuteScriptAsync(kickoffScript);
        Logger.Log(Category, $"Kicked off in-page fetch for {url}; polling for result.");

        string? resultJson = null;
        for (int attempt = 1; attempt <= MaxPollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(PollIntervalMs, cancellationToken);

            string raw = await _webView.CoreWebView2.ExecuteScriptAsync("window.__tokenometerResult");
            if (raw == "null")
                continue;

            resultJson = JsonSerializer.Deserialize<string>(raw);
            Logger.Log(Category, $"Poll #{attempt}: result ready after ~{attempt * PollIntervalMs}ms.");
            break;
        }

        if (resultJson is null)
        {
            Logger.Log(Category, $"Timed out after {MaxPollAttempts * PollIntervalMs}ms waiting for in-page fetch.");
            throw new TimeoutException("The in-page fetch didn't complete within the timeout.");
        }

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        bool ok = doc.RootElement.GetProperty("ok").GetBoolean();
        int status = doc.RootElement.GetProperty("status").GetInt32();
        string body = doc.RootElement.GetProperty("body").GetString() ?? string.Empty;

        Logger.Log(Category, $"fetch({url}) -> ok={ok} status={status} bodyLen={body.Length}");
        return (ok, status, body);
    }

    public async Task ClearCookiesAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        _webView.CoreWebView2.CookieManager.DeleteAllCookies();
        Logger.Log(Category, "Cleared all cookies in the shared browser profile.");
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        _readyTask ??= InitializeAsync(cancellationToken);
        try
        {
            await _readyTask;
        }
        catch
        {
            // Don't wedge the fetcher forever on a transient init failure — retry next call.
            _readyTask = null;
            throw;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Logger.Log(Category, "Initializing hidden WebView2 host.");
        CoreWebView2Environment environment = await SharedBrowserEnvironment.GetAsync();
        await _webView.EnsureCoreWebView2Async(environment);

        var navigationComplete = new TaskCompletionSource();
        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            Logger.Log(Category, $"Hidden host navigation completed. Success={args.IsSuccess}");
            navigationComplete.TrySetResult();
        }
        _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _webView.CoreWebView2.Navigate(HomeUrl);

        using (cancellationToken.Register(() => navigationComplete.TrySetCanceled()))
        {
            await navigationComplete.Task;
        }

        Logger.Log(Category, "Hidden WebView2 host ready.");
    }

    public void Dispose()
    {
        _webView.Dispose();
        _hiddenHost.Dispose();
    }
}
