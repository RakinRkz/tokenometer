using Microsoft.Web.WebView2.Core;

namespace Tokenometer;

/// <summary>
/// LoginForm and the hidden fetch host both need a WebView2 profile pointed at the
/// same user-data folder (so login persists across them). Creating two separate
/// CoreWebView2Environment objects for the same folder at once risks profile-lock
/// conflicts, so both go through this single cached environment instead.
/// </summary>
internal static class SharedBrowserEnvironment
{
    public static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "WebView2");

    private static Task<CoreWebView2Environment>? _environmentTask;

    public static Task<CoreWebView2Environment> GetAsync()
    {
        return _environmentTask ??= CreateAsync();
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(UserDataFolder);
        Logger.Debug("SharedBrowserEnvironment", $"Creating shared WebView2 environment at {UserDataFolder}");
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(userDataFolder: UserDataFolder);
        Logger.Info("SharedBrowserEnvironment", $"Environment ready. Runtime version: {environment.BrowserVersionString}");
        return environment;
    }
}
