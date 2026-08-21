namespace Tokenometer;

internal sealed class UsagePoller : IDisposable
{
    // Comfortably longer than the browser's own init (30s) and in-page fetch (~6s)
    // deadlines, so this only fires when something below has stalled in a way those
    // don't cover — it's a backstop, not the primary timeout.
    private static readonly TimeSpan DefaultPollTimeout = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _pollTimeout;
    private readonly UsageClient _client;
    private readonly System.Windows.Forms.Timer _timer;

    public event EventHandler<UsageSnapshot>? UsageUpdated;
    public event EventHandler<Exception>? FetchFailed;

    public UsagePoller(UsageClient client, TimeSpan interval, TimeSpan? pollTimeout = null)
    {
        _pollTimeout = pollTimeout ?? DefaultPollTimeout;
        _client = client;
        _timer = new System.Windows.Forms.Timer { Interval = (int)interval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await PollOnceAsync();
        Logger.Debug("UsagePoller", $"Created with interval {interval}.");
    }

    public void Start()
    {
        Logger.Debug("UsagePoller", "Start() — timer armed, firing an immediate poll.");
        _timer.Start();
        _ = PollOnceAsync();
    }

    public async Task PollOnceAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Debug("UsagePoller", "Poll starting.");

        // Give the poll a real deadline. Passed nothing, UsageClient and
        // BrowserUsageFetcher threaded CancellationToken.None all the way down, so
        // every cancellation check below was inert.
        using var deadline = new CancellationTokenSource(_pollTimeout);
        try
        {
            UsageSnapshot snapshot = await _client.GetUsageAsync(deadline.Token);
            Logger.Debug("UsagePoller", $"Poll succeeded in {stopwatch.ElapsedMilliseconds}ms.");
            UsageUpdated?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            Logger.Error("UsagePoller", $"Poll timed out after {stopwatch.ElapsedMilliseconds}ms.");
            FetchFailed?.Invoke(this, new UsageFetchException(
                $"Timed out after {_pollTimeout.TotalSeconds:0}s waiting for the embedded browser."));
        }
        catch (Exception ex)
        {
            Logger.Error("UsagePoller", $"Poll failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            FetchFailed?.Invoke(this, ex);
        }
    }

    public void Dispose()
    {
        Logger.Debug("UsagePoller", "Disposed.");
        _timer.Dispose();
    }
}
