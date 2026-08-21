using Tokenometer;
using Tokenometer.Tests.Fakes;

namespace Tokenometer.Tests;

/// <summary>
/// Covers the poll deadline and how cancellation is reported. The browser's own
/// timeouts live inside BrowserUsageFetcher and need a real WebView2; this is the
/// layer above, where a fake fetcher can stand in for a wedged one.
/// </summary>
public class UsagePollerTests
{
    private const string ValidUsageJson = """
        {
            "five_hour": { "utilization": 55.0, "resets_at": "2026-08-10T14:59:59+00:00" },
            "seven_day": { "utilization": 60.0, "resets_at": "2026-08-12T18:59:59+00:00" }
        }
        """;

    private static UsageClient SignedInClient(IUsageFetcher fetcher) =>
        new(new FakeSignInState(initiallySignedIn: true), new FakeOrganizationSettings("org-123"), fetcher);

    [Fact]
    public async Task AFetchThatNeverCompletes_RaisesFetchFailedWithATimeoutMessage()
    {
        using var poller = new UsagePoller(
            SignedInClient(FakeUsageFetcher.Hanging()),
            interval: TimeSpan.FromMinutes(3),
            pollTimeout: TimeSpan.FromMilliseconds(150));
        Exception? failure = null;
        poller.FetchFailed += (_, ex) => failure = ex;

        await poller.PollOnceAsync();

        Assert.NotNull(failure);
        Assert.Contains("Timed out", failure!.Message);
    }

    [Fact]
    public async Task ATimedOutPoll_DoesNotAlsoRaiseUsageUpdated()
    {
        using var poller = new UsagePoller(
            SignedInClient(FakeUsageFetcher.Hanging()),
            interval: TimeSpan.FromMinutes(3),
            pollTimeout: TimeSpan.FromMilliseconds(150));
        bool updated = false;
        poller.UsageUpdated += (_, _) => updated = true;

        await poller.PollOnceAsync();

        Assert.False(updated);
    }

    [Fact]
    public async Task TheFetcherIsHandedACancellableToken()
    {
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        using var poller = new UsagePoller(SignedInClient(fetcher), TimeSpan.FromMinutes(3));

        await poller.PollOnceAsync();

        // Before the deadline existed this was CancellationToken.None, which made
        // every cancellation check below it inert.
        Assert.True(fetcher.LastToken.CanBeCanceled);
    }

    [Fact]
    public async Task ASuccessfulPoll_RaisesUsageUpdatedAndNotFetchFailed()
    {
        var fetcher = FakeUsageFetcher.Returning(ok: true, status: 200, body: ValidUsageJson);
        using var poller = new UsagePoller(SignedInClient(fetcher), TimeSpan.FromMinutes(3));
        UsageSnapshot? snapshot = null;
        Exception? failure = null;
        poller.UsageUpdated += (_, s) => snapshot = s;
        poller.FetchFailed += (_, ex) => failure = ex;

        await poller.PollOnceAsync();

        Assert.Null(failure);
        Assert.NotNull(snapshot);
        Assert.Equal(55.0, snapshot!.SessionPercent);
    }

    [Fact]
    public async Task AFailingPoll_IsReportedRatherThanThrown()
    {
        using var poller = new UsagePoller(
            SignedInClient(FakeUsageFetcher.Throwing(new InvalidOperationException("WebView2 not ready"))),
            TimeSpan.FromMinutes(3));
        Exception? failure = null;
        poller.FetchFailed += (_, ex) => failure = ex;

        await poller.PollOnceAsync();   // must not throw — it runs from a timer tick

        Assert.NotNull(failure);
        Assert.Contains("WebView2 not ready", failure!.Message);
    }

    [Fact]
    public async Task CallerCancellation_IsNotRebadgedAsAFetchFailure()
    {
        UsageClient client = SignedInClient(FakeUsageFetcher.Hanging());
        using var cts = new CancellationTokenSource(100);

        // UsageClient wraps fetcher exceptions in UsageFetchException, but must let
        // cancellation through untouched — otherwise the poller can't tell its own
        // deadline from a genuine browser error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetUsageAsync(cts.Token));
    }
}
