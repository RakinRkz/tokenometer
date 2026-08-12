using System.Security.Cryptography;
using System.Text;

namespace Tokenometer;

internal sealed class CookieStore : ICookieStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "session.dat");

    public void Save(string cookieHeader)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        byte[] plain = Encoding.UTF8.GetBytes(cookieHeader);
        byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
        // Never log the cookie value itself — only that a save happened and its length.
        Logger.Log("CookieStore", $"Saved cookie header ({cookieHeader.Length} chars) to {_filePath}");
    }

    public string? Load()
    {
        if (!File.Exists(_filePath))
        {
            Logger.Log("CookieStore", "Load: no session file present.");
            return null;
        }

        try
        {
            byte[] encrypted = File.ReadAllBytes(_filePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            string value = Encoding.UTF8.GetString(plain);
            Logger.Log("CookieStore", $"Load: decrypted cookie header ({value.Length} chars).");
            return value;
        }
        catch (CryptographicException ex)
        {
            // Encrypted with a different user/machine key, or corrupted — treat as logged out.
            Logger.Log("CookieStore", $"Load: DPAPI decrypt failed, treating as logged out: {ex.Message}");
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
            Logger.Log("CookieStore", "Cleared session file.");
        }
    }
}
