using Tokenometer;

namespace Tokenometer.Tests;

public class SignInStateTests
{
    [Fact]
    public void FreshInstall_IsNotSignedIn()
    {
        using var folder = new TempFolder();

        Assert.False(new SignInState(folder.Path).IsSignedIn);
    }

    [Fact]
    public void MarkSignedIn_PersistsAcrossInstances()
    {
        using var folder = new TempFolder();

        new SignInState(folder.Path).MarkSignedIn();

        Assert.True(new SignInState(folder.Path).IsSignedIn);
        Assert.True(folder.Exists("signed-in.marker"));
    }

    [Fact]
    public void Clear_RemovesTheMarker()
    {
        using var folder = new TempFolder();
        var state = new SignInState(folder.Path);
        state.MarkSignedIn();

        state.Clear();

        Assert.False(state.IsSignedIn);
        Assert.False(folder.Exists("signed-in.marker"));
    }

    [Fact]
    public void Clear_WhenNotSignedIn_DoesNotThrow()
    {
        using var folder = new TempFolder();

        new SignInState(folder.Path).Clear();
    }

    [Fact]
    public void MarkSignedIn_CreatesTheFolderIfItIsMissing()
    {
        using var folder = new TempFolder();
        string nested = Path.Combine(folder.Path, "does-not-exist-yet");

        new SignInState(nested).MarkSignedIn();

        Assert.True(new SignInState(nested).IsSignedIn);
    }

    // --- the session.dat migration: runs once on every existing user's machine ---

    [Fact]
    public void LegacyCookieFile_IsDeletedAndCountsAsSignedIn()
    {
        using var folder = new TempFolder();
        folder.Write("session.dat", "encrypted-cookie-bytes");

        var state = new SignInState(folder.Path);

        Assert.True(state.IsSignedIn);                  // the old file meant "signed in"
        Assert.False(folder.Exists("session.dat"));     // and the stale secret is gone
        Assert.True(folder.Exists("signed-in.marker"));
    }

    [Fact]
    public void LegacyCookieFile_AlongsideAnExistingMarker_IsStillDeleted()
    {
        using var folder = new TempFolder();
        folder.Write("signed-in.marker", "");
        folder.Write("session.dat", "encrypted-cookie-bytes");

        var state = new SignInState(folder.Path);

        Assert.True(state.IsSignedIn);
        Assert.False(folder.Exists("session.dat"));
    }

    [Fact]
    public void NoLegacyCookieFile_LeavesAFreshInstallSignedOut()
    {
        using var folder = new TempFolder();

        var state = new SignInState(folder.Path);

        Assert.False(state.IsSignedIn);
        Assert.False(folder.Exists("signed-in.marker")); // migration must not invent one
    }

    [Fact]
    public void MigrationDoesNotResurrectSignInAfterAnExplicitLogout()
    {
        using var folder = new TempFolder();
        folder.Write("session.dat", "encrypted-cookie-bytes");
        new SignInState(folder.Path).Clear();   // migrate, then log out

        Assert.False(new SignInState(folder.Path).IsSignedIn);
    }
}
