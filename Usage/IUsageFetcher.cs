namespace Tokenometer;

internal interface IUsageFetcher : IDisposable
{
    Task<(bool Ok, int Status, string Body)> FetchJsonAsync(string url, CancellationToken cancellationToken);

    Task ClearCookiesAsync(CancellationToken cancellationToken);
}
