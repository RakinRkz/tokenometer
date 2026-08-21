namespace Tokenometer;

/// <summary>
/// Records that a claude.ai login has completed, which is what lets the app tell
/// "not signed in yet, show mock data" apart from "signed in, so a failed fetch is
/// a real error worth reporting".
///
/// This used to be a DPAPI-encrypted copy of the full cookie header in session.dat.
/// Nothing ever read the value — the request authenticates through the WebView2
/// profile's own cookie jar, and the stored copy was only ever compared against
/// null. Keeping a live session secret at rest to answer a yes/no question is cost
/// without benefit, so the marker is an empty file and any leftover session.dat is
/// deleted on startup.
/// </summary>
internal sealed class SignInState : ISignInState
{
    private const string Category = "SignInState";

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tokenometer");

    private readonly string _markerPath = Path.Combine(Folder, "signed-in.marker");
    private readonly string _legacyCookiePath = Path.Combine(Folder, "session.dat");

    public SignInState() => RemoveLegacyCookieFile();

    public bool IsSignedIn => File.Exists(_markerPath);

    public void MarkSignedIn()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(_markerPath, string.Empty);
        Logger.Info(Category, "Marked signed in.");
    }

    public void Clear()
    {
        if (File.Exists(_markerPath))
        {
            File.Delete(_markerPath);
            Logger.Info(Category, "Cleared sign-in marker.");
        }
    }

    /// <summary>
    /// Upgrade path from the versions that stored the cookie: the old file's mere
    /// existence carried the same meaning as the marker, so carry that across and
    /// then delete it — leaving a stale session secret on disk is the thing this
    /// class exists to stop.
    /// </summary>
    private void RemoveLegacyCookieFile()
    {
        if (!File.Exists(_legacyCookiePath))
            return;

        try
        {
            if (!File.Exists(_markerPath))
                MarkSignedIn();

            File.Delete(_legacyCookiePath);
            Logger.Info(Category, "Deleted legacy session.dat — its contents were never read.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn(Category, $"Couldn't remove legacy session.dat, will retry next start: {ex.Message}");
        }
    }
}
