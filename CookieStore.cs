using System.Security.Cryptography;
using System.Text;

namespace Tokenometer;

internal static class CookieStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "session.dat");

    public static void Save(string sessionCookieValue)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        byte[] plain = Encoding.UTF8.GetBytes(sessionCookieValue);
        byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
        // Never log the cookie value itself — only that a save happened and its length.
        Logger.Log("CookieStore", $"Saved cookie header ({sessionCookieValue.Length} chars) to {FilePath}");
    }

    public static string? Load()
    {
        if (!File.Exists(FilePath))
        {
            Logger.Log("CookieStore", "Load: no session file present.");
            return null;
        }

        try
        {
            byte[] encrypted = File.ReadAllBytes(FilePath);
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

    public static void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
            Logger.Log("CookieStore", "Cleared session file.");
        }
    }
}
