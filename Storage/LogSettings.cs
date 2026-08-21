using System.Text.Json;

namespace Tokenometer;

/// <summary>
/// Whether the log records per-poll detail (<see cref="LogLevel.Debug"/>) on top of
/// errors, warnings and lifecycle events. Off by default — see <see cref="Logger"/>
/// for why. Meant to be switched on only while actively troubleshooting, which is
/// why SettingsForm confirms before turning it on.
///
/// Stored as JSON rather than a marker file so an unreadable setting has an
/// unambiguous fallback to record in the log, rather than a marker's mere
/// presence/absence silently meaning one thing or the other. The folder is
/// injectable so this is testable without writing into the real %AppData%.
/// </summary>
internal sealed class LogSettings : ILogSettings
{
    private const string Category = "LogSettings";

    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tokenometer");

    private readonly string _folder;
    private readonly string _filePath;

    public LogSettings() : this(DefaultFolder)
    {
    }

    internal LogSettings(string folder)
    {
        _folder = folder;
        _filePath = Path.Combine(folder, "log-settings.json");
    }

    private sealed record Stored(bool Verbose);

    public bool Verbose
    {
        get
        {
            if (!File.Exists(_filePath))
                return false;

            try
            {
                return JsonSerializer.Deserialize<Stored>(File.ReadAllText(_filePath))?.Verbose ?? false;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // Falling back to quiet rather than verbose: an unreadable setting
                // should not silently start recording detail nobody asked for.
                Logger.Warn(Category, $"Couldn't read logging setting, defaulting to off: {ex.Message}");
                return false;
            }
        }
    }

    public void SetVerbose(bool verbose)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(new Stored(verbose)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The caller has already applied the level in memory, so the choice holds
            // for this session either way; only persistence across restart is lost.
            Logger.Warn(Category, $"Couldn't persist logging setting: {ex.Message}");
        }
    }
}
