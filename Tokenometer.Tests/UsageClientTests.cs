using Tokenometer;
using Tokenometer.Tests.Fakes;

namespace Tokenometer.Tests;

public class UsageClientTests
{
    private const string ValidUsageJson = """
        {
            "five_hour": { "utilization": 55.0, "resets_at": "2026-08-10T14:59:59+00:00" },
            "seven_day": { "utilization": 60.0, "resets_at": "2026-08-12T18:59:59+00:00" }
        }
        """;

    [Fact]
    public async Task NotSignedIn_ReturnsMockDataWithoutTouchingTheFetcher()
    {
        var signInState = new FakeSignInState(initiallySignedIn: false);
        var organizationSettings = new FakeOrganizationSettings("org-123");
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        var client = new UsageClient(signInState, organizationSettings, fetcher);

        UsageSnapshot snapshot = await client.GetUsageAsync();

        Assert.False(client.IsAuthenticated);
        Assert.InRange(snapshot.SessionPercent, 0, 100);
        Assert.InRange(snapshot.WeeklyPercent, 0, 100);
        Assert.Null(fetcher.LastRequestedUrl); // never called — mock path short-circuits before fetching
    }

    [Fact]
    public async Task SignedInButNoOrganizationId_ThrowsWithActionableMessage()
    {
        var signInState = new FakeSignInState(initiallySignedIn: true);
        var organizationSettings = new FakeOrganizationSettings(initialOrganizationId: null);
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        var client = new UsageClient(signInState, organizationSettings, fetcher);

        UsageFetchException ex = await Assert.ThrowsAsync<UsageFetchException>(() => client.GetUsageAsync());

        Assert.Contains("Organization ID", ex.Message);
    }

    [Fact]
    public async Task FetcherThrows_IsWrappedInUsageFetchException()
    {
        var signInState = new FakeSignInState(initiallySignedIn: true);
        var organizationSettings = new FakeOrganizationSettings("org-123");
        var fetcher = FakeUsageFetcher.Throwing(new InvalidOperationException("WebView2 not ready"));
        var client = new UsageClient(signInState, organizationSettings, fetcher);

        UsageFetchException ex = await Assert.ThrowsAsync<UsageFetchException>(() => client.GetUsageAsync());

        Assert.Contains("WebView2 not ready", ex.Message);
    }

    [Fact]
    public async Task FetcherReturnsNonOk_ThrowsWithStatusInMessage()
    {
        var signInState = new FakeSignInState(initiallySignedIn: true);
        var organizationSettings = new FakeOrganizationSettings("org-123");
        var fetcher = FakeUsageFetcher.Returning(ok: false, status: 403, body: "<html>Just a moment...</html>");
        var client = new UsageClient(signInState, organizationSettings, fetcher);

        UsageFetchException ex = await Assert.ThrowsAsync<UsageFetchException>(() => client.GetUsageAsync());

        Assert.Contains("403", ex.Message);
    }

    [Fact]
    public async Task SuccessfulFetch_ReturnsParsedSnapshotAndRequestsTheCorrectUrl()
    {
        var signInState = new FakeSignInState(initiallySignedIn: true);
        var organizationSettings = new FakeOrganizationSettings("org-123");
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        var client = new UsageClient(signInState, organizationSettings, fetcher);

        UsageSnapshot snapshot = await client.GetUsageAsync();

        Assert.True(client.IsAuthenticated);
        Assert.Equal(55.0, snapshot.SessionPercent);
        Assert.Equal(60.0, snapshot.WeeklyPercent);
        Assert.Equal("https://claude.ai/api/organizations/org-123/usage", fetcher.LastRequestedUrl);
    }

    [Fact]
    public async Task ClearBrowserSessionAsync_DelegatesToFetcher()
    {
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        var client = new UsageClient(new FakeSignInState(), new FakeOrganizationSettings(), fetcher);

        await client.ClearBrowserSessionAsync();

        Assert.True(fetcher.CookiesCleared);
    }

    [Fact]
    public void Dispose_DisposesTheFetcher()
    {
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        var client = new UsageClient(new FakeSignInState(), new FakeOrganizationSettings(), fetcher);

        client.Dispose();

        Assert.True(fetcher.Disposed);
    }
}
