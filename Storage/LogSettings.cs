using System.Text.Json;

namespace Tokenometer;

/// <summary>
/// Whether the log records per-poll detail (<see cref="LogLevel.Debug"/>) on top of
/// errors, warnings and lifecycle events. On by default — see <see cref="Logger"/>
/// for why.
///
/// Stored as JSON rather than a marker file: with verbose as the default, a bare
/// marker would have to mean "not the default", which reads backwards from its own
/// name. An explicit value survives the default changing again. The folder is
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
                return true;

            try
            {
                return JsonSerializer.Deserialize<Stored>(File.ReadAllText(_filePath))?.Verbose ?? true;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                Logger.Warn(Category, $"Couldn't read logging setting, defaulting to verbose: {ex.Message}");
                return true;
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
