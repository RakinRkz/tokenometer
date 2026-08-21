using System.Runtime.CompilerServices;
using Tokenometer;

namespace Tokenometer.Tests;

/// <summary>
/// Sends the log somewhere disposable before any test runs.
///
/// Logger is static and pointed at %AppData%\Tokenometer\log.txt, and
/// UsageClientTests exercises the real UsageClient — which logs. Every `dotnet test`
/// run therefore appended fixture data to the user's actual diagnostic log: 104 such
/// lines were found in a real one, including "WebView2 not ready" and fetches
/// against org-123, which look exactly like genuine failures when you are trying to
/// troubleshoot a live problem.
///
/// A module initializer rather than a fixture, because it has to win the race
/// against any test — or any static constructor — that logs on first touch.
/// </summary>
internal static class TestLogRedirect
{
    [ModuleInitializer]
    internal static void Redirect()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Tokenometer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Logger.RedirectTo(folder);
    }
}
