using Tokenometer;

namespace Tokenometer.Tests.Fakes;

internal sealed class FakeUsageFetcher : IUsageFetcher
{
    private readonly (bool Ok, int Status, string Body)? _canned;
    private readonly Exception? _throwOnFetch;
    private readonly bool _hang;

    public bool CookiesCleared { get; private set; }
    public bool Disposed { get; private set; }
    public string? LastRequestedUrl { get; private set; }

    /// <summary>The token the fetcher was handed — used to prove one actually arrives.</summary>
    public CancellationToken LastToken { get; private set; }

    public static FakeUsageFetcher Returning(bool ok, int status, string body) =>
        new(canned: (ok, status, body), throwOnFetch: null, hang: false);

    public static FakeUsageFetcher Throwing(Exception exception) =>
        new(canned: null, throwOnFetch: exception, hang: false);

    /// <summary>Never completes on its own — stands in for a wedged browser.</summary>
    public static FakeUsageFetcher Hanging() =>
        new(canned: null, throwOnFetch: null, hang: true);

    private FakeUsageFetcher((bool Ok, int Status, string Body)? canned, Exception? throwOnFetch, bool hang)
    {
        _canned = canned;
        _throwOnFetch = throwOnFetch;
        _hang = hang;
    }

    public async Task<(bool Ok, int Status, string Body)> FetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        LastRequestedUrl = url;
        LastToken = cancellationToken;

        if (_throwOnFetch is not null)
            throw _throwOnFetch;

        if (_hang)
            await Task.Delay(Timeout.Infinite, cancellationToken);

        return _canned!.Value;
    }

    public Task ClearCookiesAsync(CancellationToken cancellationToken)
    {
        CookiesCleared = true;
        return Task.CompletedTask;
    }

    public void Dispose() => Disposed = true;
}
