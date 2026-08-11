using System.Text.Json;

namespace Tokenometer;

/// <summary>
/// Fetches usage data from claude.ai's internal Settings -> Usage endpoint.
/// This is not a documented, stable API — the URL shape and JSON fields were
/// captured from DevTools and can change without notice. Requests go through
/// <see cref="BrowserUsageFetcher"/> (an in-browser fetch()) rather than a bare
/// HttpClient, since claude.ai sits behind a Cloudflare challenge that only a
/// real browser engine can pass.
/// </summary>
internal sealed class UsageClient : IDisposable
{
    private const string UsageUrlTemplate = "https://claude.ai/api/organizations/{0}/usage";
    private const string Category = "UsageClient";

    private readonly BrowserUsageFetcher _fetcher = new();

    public bool IsAuthenticated => CookieStore.Load() is not null;

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        string? sessionCookie = CookieStore.Load();
        if (sessionCookie is null)
        {
            Logger.Log(Category, "No session cookie stored — returning mock data.");
            return GetMockUsage();
        }

        string? organizationId = OrganizationSettings.Load();
        if (organizationId is null)
        {
            Logger.Log(Category, "No organization id stored — cannot build usage URL.");
            throw new UsageFetchException(
                "Organization ID not set — use \"Set Organization ID...\" in the tray menu " +
                "(find it in the usage request's URL in DevTools: /organizations/{id}/usage).");
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
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            JsonElement fiveHour = root.GetProperty("five_hour");
            JsonElement sevenDay = root.GetProperty("seven_day");

            double sessionPercent = fiveHour.GetProperty("utilization").GetDouble();
            double weeklyPercent = sevenDay.GetProperty("utilization").GetDouble();
            DateTimeOffset? sessionResetsAt = TryGetDate(fiveHour, "resets_at");
            DateTimeOffset? weeklyResetsAt = TryGetDate(sevenDay, "resets_at");

            Logger.Log(Category,
                $"Parsed OK: session={sessionPercent}% (resets {sessionResetsAt}), weekly={weeklyPercent}% (resets {weeklyResetsAt})");
            return new UsageSnapshot(sessionPercent, weeklyPercent, sessionResetsAt, weeklyResetsAt, DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            Logger.Log(Category, $"Parse failure: {ex}\r\nBody: {body}");
            throw new UsageFetchException($"Response shape changed — couldn't parse JSON: {ex.Message}", ex);
        }
    }

    public Task ClearBrowserSessionAsync(CancellationToken cancellationToken = default) =>
        _fetcher.ClearCookiesAsync(cancellationToken);

    private static DateTimeOffset? TryGetDate(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset result) ? result : null;
    }

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
