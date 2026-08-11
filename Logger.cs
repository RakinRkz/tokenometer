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

    public static void Log(string category, string message)
    {
        lock (WriteLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            RotateIfNeeded();
            File.AppendAllText(LogFilePath,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{category}] {message}\r\n");
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(LogFilePath);
        if (!file.Exists || file.Length < MaxSizeBytes)
            return;

        File.Copy(LogFilePath, RotatedFilePath, overwrite: true);
        File.WriteAllText(LogFilePath, string.Empty);
    }
}
