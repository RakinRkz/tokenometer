namespace Tokenometer;

/// <summary>
/// Whether the log records per-poll detail (<see cref="LogLevel.Debug"/>) as well as
/// errors, warnings and lifecycle events. Off by default — see <see cref="Logger"/>
/// for why.
///
/// Persisted as an empty marker file rather than JSON because it is one bit, and it
/// reads the same way as the sign-in marker next to it. The folder is injectable so
/// this is testable without writing into the real %AppData%.
/// </summary>
internal sealed class LogSettings : ILogSettings
{
    private const string Category = "LogSettings";

    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tokenometer");

    private readonly string _folder;
    private readonly string _markerPath;

    public LogSettings() : this(DefaultFolder)
    {
    }

    internal LogSettings(string folder)
    {
        _folder = folder;
        _markerPath = Path.Combine(folder, "verbose-logging.marker");
    }

    public bool Verbose => File.Exists(_markerPath);

    public void SetVerbose(bool verbose)
    {
        try
        {
            if (verbose)
            {
                Directory.CreateDirectory(_folder);
                File.WriteAllText(_markerPath, string.Empty);
            }
            else if (File.Exists(_markerPath))
            {
                File.Delete(_markerPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The caller has already applied the level in memory, so the choice holds
            // for this session either way; only persistence across restart is lost.
            Logger.Warn(Category, $"Couldn't persist verbose-logging setting: {ex.Message}");
        }
    }
}
