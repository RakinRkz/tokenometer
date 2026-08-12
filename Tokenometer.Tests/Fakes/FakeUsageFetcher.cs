using Tokenometer;

namespace Tokenometer.Tests.Fakes;

internal sealed class FakeUsageFetcher : IUsageFetcher
{
    private readonly (bool Ok, int Status, string Body)? _canned;
    private readonly Exception? _throwOnFetch;

    public bool CookiesCleared { get; private set; }
    public bool Disposed { get; private set; }
    public string? LastRequestedUrl { get; private set; }

    public static FakeUsageFetcher Returning(bool ok, int status, string body) =>
        new(canned: (ok, status, body), throwOnFetch: null);

    public static FakeUsageFetcher Throwing(Exception exception) =>
        new(canned: null, throwOnFetch: exception);

    private FakeUsageFetcher((bool Ok, int Status, string Body)? canned, Exception? throwOnFetch)
    {
        _canned = canned;
        _throwOnFetch = throwOnFetch;
    }

    public Task<(bool Ok, int Status, string Body)> FetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        LastRequestedUrl = url;

        if (_throwOnFetch is not null)
            throw _throwOnFetch;

        return Task.FromResult(_canned!.Value);
    }

    public Task ClearCookiesAsync(CancellationToken cancellationToken)
    {
        CookiesCleared = true;
        return Task.CompletedTask;
    }

    public void Dispose() => Disposed = true;
}
