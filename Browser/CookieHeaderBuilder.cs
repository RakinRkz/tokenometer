namespace Tokenometer;

/// <summary>
/// Pure operations over a browser's cookie list, kept separate from LoginForm's
/// WebView2 plumbing so they're testable without a live browser control.
/// </summary>
internal static class CookieHeaderBuilder
{
    /// <summary>
    /// Builds a "Name=Value; Name=Value" header string forwarding every cookie —
    /// the usage API may depend on CSRF/device cookies set alongside the session,
    /// not just the session cookie itself.
    /// </summary>
    public static string BuildHeader(IEnumerable<(string Name, string Value)> cookies) =>
        string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

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
