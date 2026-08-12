namespace Tokenometer;

internal sealed class UsagePoller : IDisposable
{
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

    public void Stop()
    {
        Logger.Log("UsagePoller", "Stop().");
        _timer.Stop();
    }

    public async Task PollOnceAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Log("UsagePoller", "Poll starting.");
        try
        {
            UsageSnapshot snapshot = await _client.GetUsageAsync();
            Logger.Log("UsagePoller", $"Poll succeeded in {stopwatch.ElapsedMilliseconds}ms.");
            UsageUpdated?.Invoke(this, snapshot);
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
