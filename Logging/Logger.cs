namespace Tokenometer;

internal enum LogLevel
{
    /// <summary>Per-poll mechanics. Off unless verbose logging is switched on.</summary>
    Debug,

    /// <summary>Things that happen once, or once per user action: startup, login, logout, settings changes.</summary>
    Info,

    /// <summary>Degraded but recoverable — a setting that wouldn't save, an id that wasn't detected.</summary>
    Warning,

    /// <summary>A failure the user can see in the tray.</summary>
    Error,
}

/// <summary>
/// Writes to a single rolling file under %AppData%. Everything at
/// <see cref="MinimumLevel"/> and above is written; the rest is dropped.
///
/// The default is Info because a healthy poll used to emit ten lines every three
/// minutes — around 300 KB a day, 35,000 lines in eleven days — which buried the
/// failures the log exists to surface. Errors, warnings and lifecycle events are
/// always recorded, so an installed copy stays diagnosable without anyone having to
/// turn anything on first; Settings has a checkbox for the per-poll detail.
/// </summary>
internal static class Logger
{
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    public static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "log.txt");

    private static readonly string RotatedFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "log.old.txt");

    private static readonly object WriteLock = new();

    /// <summary>
    /// Lines below this are discarded. Assignable at any time so the Settings
    /// checkbox takes effect immediately rather than at the next restart.
    /// </summary>
    public static volatile LogLevel MinimumLevel = LogLevel.Info;

    public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message);

    public static void Info(string category, string message) => Write(LogLevel.Info, category, message);

    public static void Warn(string category, string message) => Write(LogLevel.Warning, category, message);

    public static void Error(string category, string message) => Write(LogLevel.Error, category, message);

    /// <summary>
    /// Never throws. Called from Program's unhandled-exception handlers, from
    /// constructors, and from inside catch blocks — so an I/O failure here would
    /// replace the error being reported with a different one, or take down the very
    /// handler meant to record it. Losing a diagnostic line is always the better
    /// outcome. Genuine programming errors are deliberately not swallowed.
    /// </summary>
    private static void Write(LogLevel level, string category, string message)
    {
        if (level < MinimumLevel)
            return;

        try
        {
            lock (WriteLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                TryRotate();
                File.AppendAllText(LogFilePath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Tag(level)}] [{category}] {message}\r\n");
            }
        }
        catch (Exception ex) when (ex is IOException
                                      or UnauthorizedAccessException
                                      or System.Security.SecurityException
                                      or NotSupportedException)
        {
            // Nowhere left to report this — the reporting channel is what failed.
        }
    }

    // Padded so the category column lines up when scanning the file by eye.
    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO ",
        LogLevel.Warning => "WARN ",
        _ => "ERROR",
    };

    /// <summary>
    /// Swallows its own failures separately so a log that can't be rotated (the old
    /// file locked by a viewer, say) still accepts new lines rather than going silent.
    /// </summary>
    private static void TryRotate()
    {
        try
        {
            var file = new FileInfo(LogFilePath);
            if (!file.Exists || file.Length < MaxSizeBytes)
                return;

            File.Copy(LogFilePath, RotatedFilePath, overwrite: true);
            File.WriteAllText(LogFilePath, string.Empty);
        }
        catch (Exception ex) when (ex is IOException
                                      or UnauthorizedAccessException
                                      or System.Security.SecurityException)
        {
            // Keep appending to the oversized file; it'll be retried next call.
        }
    }
}
