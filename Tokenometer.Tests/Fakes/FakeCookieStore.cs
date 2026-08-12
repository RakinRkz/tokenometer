using Tokenometer;

namespace Tokenometer.Tests.Fakes;

internal sealed class FakeCookieStore : ICookieStore
{
    private string? _cookie;

    public FakeCookieStore(string? initialCookie = null) => _cookie = initialCookie;

    public void Save(string cookieHeader) => _cookie = cookieHeader;

    public string? Load() => _cookie;

    public void Clear() => _cookie = null;
}
