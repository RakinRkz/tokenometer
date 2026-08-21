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
internal sealed class BrowserUsageFetcher : IUsageFetcher
{
    private const string Category = "BrowserUsageFetcher";
    private const string HomeUrl = "https://claude.ai/settings/usage";

    private readonly Form _hiddenHost;
    private readonly WebView2 _webView = new();
    private Task? _readyTask;

    /// <summary>
    /// Serialises everything that touches the browser. The in-page fetch hands its
    /// result back through a single window global, so two overlapping fetches would
    /// share one slot: the second kickoff nulls the first's result, and both pollers
    /// then read whichever response happens to land. Overlap is easy to trigger —
    /// "Check now" during a slow poll, or a login completing while a timer tick is
    /// still in flight. Holding this for the whole call also means only one caller
    /// is ever inside EnsureReadyAsync, so the cached _readyTask can't be observed
    /// mid-initialisation by a second caller.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

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
    private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(30);

    public async Task<(bool Ok, int Status, string Body)> FetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await FetchJsonCoreAsync(url, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(bool Ok, int Status, string Body)> FetchJsonCoreAsync(string url, CancellationToken cancellationToken)
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
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureReadyAsync(cancellationToken);
            _webView.CoreWebView2.CookieManager.DeleteAllCookies();
            Logger.Log(Category, "Cleared all cookies in the shared browser profile.");
        }
        finally
        {
            _gate.Release();
        }
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
        // An independent deadline on top of the caller's token. Two reasons it can't
        // rely on the caller alone: ClearCookiesAsync is reached from logout with no
        // token at all, and none of these steps carry their own timeout. A host that
        // never raises NavigationCompleted would otherwise hang forever — and since
        // _readyTask is cached and awaited by every later call, that single hang
        // wedges all subsequent fetches with no error ever reaching the tray.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(InitTimeout);

        Logger.Log(Category, "Initializing hidden WebView2 host.");
        try
        {
            CoreWebView2Environment environment =
                await SharedBrowserEnvironment.GetAsync().WaitAsync(deadline.Token);
            await _webView.EnsureCoreWebView2Async(environment).WaitAsync(deadline.Token);

            var navigationComplete = new TaskCompletionSource();
            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                Logger.Log(Category, $"Hidden host navigation completed. Success={args.IsSuccess}");
                navigationComplete.TrySetResult();
            }
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.Navigate(HomeUrl);

            await navigationComplete.Task.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our deadline, not the caller's cancellation — report it as a timeout so
            // EnsureReadyAsync clears _readyTask and the next poll starts a fresh attempt.
            Logger.Log(Category, $"Init timed out after {InitTimeout.TotalSeconds:0}s.");
            throw new TimeoutException(
                $"The embedded browser didn't become ready within {InitTimeout.TotalSeconds:0}s.");
        }

        Logger.Log(Category, "Hidden WebView2 host ready.");
    }

    public void Dispose()
    {
        _gate.Dispose();
        _webView.Dispose();
        _hiddenHost.Dispose();
    }
}
