namespace Tokenometer.Tests;

/// <summary>
/// A throwaway directory standing in for %AppData%\Tokenometer, so the file-backed
/// stores can be exercised without touching real user data.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "Tokenometer.Tests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Write(string name, string contents) => System.IO.File.WriteAllText(File(name), contents);

    public bool Exists(string name) => System.IO.File.Exists(File(name));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}
