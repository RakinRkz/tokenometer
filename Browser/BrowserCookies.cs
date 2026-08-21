namespace Tokenometer;

/// <summary>
/// Pure queries over a browser's cookie list, kept separate from LoginForm's
/// WebView2 plumbing so they're testable without a live browser control.
/// </summary>
/// <remarks>
/// This was CookieHeaderBuilder and also assembled a "Name=Value; ..." header for
/// the session store. Nothing sent that header — the fetch runs inside the browser
/// and carries the profile's own cookies — so it went when the stored cookie did.
/// What remains is detection: has a session appeared, and which organization is it.
/// </remarks>
internal static class BrowserCookies
{
    public static bool Contains(IEnumerable<(string Name, string Value)> cookies, string name) =>
        cookies.Any(c => c.Name == name && !string.IsNullOrEmpty(c.Value));

    /// <summary>
    /// Returns the value of the first cookie matching <paramref name="name"/>, or
    /// null if absent/empty. Used to pull claude.ai's "lastActiveOrg" cookie so the
    /// organization id can be set automatically instead of via manual DevTools lookup.
    /// </summary>
    public static string? FindValue(IEnumerable<(string Name, string Value)> cookies, string name)
    {
        foreach ((string cookieName, string value) in cookies)
        {
            if (cookieName == name && !string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }
}
