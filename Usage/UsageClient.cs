using System.Text.Json;

namespace Tokenometer;

/// <summary>
/// Fetches usage data from claude.ai's internal Settings -> Usage endpoint.
/// This is not a documented, stable API — the URL shape and JSON fields were
/// captured from DevTools and can change without notice. Requests go through
/// an <see cref="IUsageFetcher"/> (an in-browser fetch()) rather than a bare
/// HttpClient, since claude.ai sits behind a Cloudflare challenge that only a
/// real browser engine can pass.
/// </summary>
internal sealed class UsageClient : IDisposable
{
    private const string UsageUrlTemplate = "https://claude.ai/api/organizations/{0}/usage";
    private const string Category = "UsageClient";

    private readonly ICookieStore _cookieStore;
    private readonly IOrganizationSettings _organizationSettings;
    private readonly IUsageFetcher _fetcher;

    public UsageClient(ICookieStore cookieStore, IOrganizationSettings organizationSettings, IUsageFetcher fetcher)
    {
        _cookieStore = cookieStore;
        _organizationSettings = organizationSettings;
        _fetcher = fetcher;
    }

    public bool IsAuthenticated => _cookieStore.Load() is not null;

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        string? sessionCookie = _cookieStore.Load();
        if (sessionCookie is null)
        {
            Logger.Log(Category, "No session cookie stored — returning mock data.");
            return GetMockUsage();
        }

        string? organizationId = _organizationSettings.Load();
        if (organizationId is null)
        {
            Logger.Log(Category, "No organization id stored — cannot build usage URL.");
            throw new UsageFetchException(
                "Organization ID not set — it's normally captured automatically from " +
                "Settings > Log in to claude.ai. Try logging out and back in.");
        }

        string usageUrl = string.Format(UsageUrlTemplate, organizationId);
        Logger.Log(Category, $"Fetching {usageUrl} via embedded browser.");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        bool ok;
        int status;
        string body;
        try
        {
            (ok, status, body) = await _fetcher.FetchJsonAsync(usageUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Log(Category, $"Browser fetch threw after {stopwatch.ElapsedMilliseconds}ms: {ex}");
            throw new UsageFetchException($"Browser fetch failed: {ex.Message}", ex);
        }

        Logger.Log(Category, $"Fetch completed in {stopwatch.ElapsedMilliseconds}ms: ok={ok} status={status} bodyLen={body.Length}");

        if (!ok)
        {
            Logger.Log(Category, $"Failure body: {body}");
            string snippet = body.Length > 300 ? body[..300] : body;
            throw new UsageFetchException($"HTTP {status} — {snippet}");
        }

        try
        {
            UsageSnapshot snapshot = UsageResponseParser.Parse(body, DateTimeOffset.Now);
            Logger.Log(Category,
                $"Parsed OK: session={snapshot.SessionPercent}% (resets {snapshot.SessionResetsAt}), " +
                $"weekly={snapshot.WeeklyPercent}% (resets {snapshot.WeeklyResetsAt})");
            return snapshot;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            Logger.Log(Category, $"Parse failure: {ex}\r\nBody: {body}");
            throw new UsageFetchException($"Response shape changed — couldn't parse JSON: {ex.Message}", ex);
        }
    }

    public Task ClearBrowserSessionAsync(CancellationToken cancellationToken = default) =>
        _fetcher.ClearCookiesAsync(cancellationToken);

    private static readonly Random MockRandom = new();

    private static UsageSnapshot GetMockUsage() => new(
        SessionPercent: MockRandom.Next(5, 95),
        WeeklyPercent: MockRandom.Next(5, 95),
        SessionResetsAt: DateTimeOffset.Now.AddHours(3),
        WeeklyResetsAt: DateTimeOffset.Now.AddDays(2),
        FetchedAt: DateTimeOffset.Now);

    public void Dispose()
    {
        Logger.Log(Category, "Disposed.");
        _fetcher.Dispose();
    }
}
