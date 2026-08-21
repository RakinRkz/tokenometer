namespace Tokenometer;

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
    /// Never throws. Log is called from Program's unhandled-exception handlers, from
    /// constructors, and from inside catch blocks — so an I/O failure here would
    /// replace the error being reported with a different one, or take down the very
    /// handler meant to record it. Losing a diagnostic line is always the better
    /// outcome. Genuine programming errors are deliberately not swallowed.
    /// </summary>
    public static void Log(string category, string message)
    {
        try
        {
            lock (WriteLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                TryRotate();
                File.AppendAllText(LogFilePath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{category}] {message}\r\n");
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
