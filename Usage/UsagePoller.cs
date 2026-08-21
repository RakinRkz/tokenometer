namespace Tokenometer;

internal sealed class UsagePoller : IDisposable
{
    // Comfortably longer than the browser's own init (30s) and in-page fetch (~6s)
    // deadlines, so this only fires when something below has stalled in a way those
    // don't cover — it's a backstop, not the primary timeout.
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(60);

    private readonly UsageClient _client;
    private readonly System.Windows.Forms.Timer _timer;

    public event EventHandler<UsageSnapshot>? UsageUpdated;
    public event EventHandler<Exception>? FetchFailed;

    public UsagePoller(UsageClient client, TimeSpan interval)
    {
        _client = client;
        _timer = new System.Windows.Forms.Timer { Interval = (int)interval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await PollOnceAsync();
        Logger.Log("UsagePoller", $"Created with interval {interval}.");
    }

    public void Start()
    {
        Logger.Log("UsagePoller", "Start() — timer armed, firing an immediate poll.");
        _timer.Start();
        _ = PollOnceAsync();
    }

    public async Task PollOnceAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Log("UsagePoller", "Poll starting.");

        // Give the poll a real deadline. Passed nothing, UsageClient and
        // BrowserUsageFetcher threaded CancellationToken.None all the way down, so
        // every cancellation check below was inert.
        using var deadline = new CancellationTokenSource(PollTimeout);
        try
        {
            UsageSnapshot snapshot = await _client.GetUsageAsync(deadline.Token);
            Logger.Log("UsagePoller", $"Poll succeeded in {stopwatch.ElapsedMilliseconds}ms.");
            UsageUpdated?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            Logger.Log("UsagePoller", $"Poll timed out after {stopwatch.ElapsedMilliseconds}ms.");
            FetchFailed?.Invoke(this, new UsageFetchException(
                $"Timed out after {PollTimeout.TotalSeconds:0}s waiting for the embedded browser."));
        }
        catch (Exception ex)
        {
            Logger.Log("UsagePoller", $"Poll failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            FetchFailed?.Invoke(this, ex);
        }
    }

    public void Dispose()
    {
        Logger.Log("UsagePoller", "Disposed.");
        _timer.Dispose();
    }
}
