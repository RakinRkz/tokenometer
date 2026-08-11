namespace Tokenometer;

/// <summary>
/// The claude.ai usage endpoint is scoped by organization id, which is per-account
/// and not a secret — stored as plain text, unlike the session cookie.
/// </summary>
internal static class OrganizationSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "organization-id.txt");

    public static string? Load()
    {
        if (!File.Exists(FilePath))
        {
            Logger.Log("OrganizationSettings", "Load: no organization-id file present.");
            return null;
        }
        string value = File.ReadAllText(FilePath).Trim();
        Logger.Log("OrganizationSettings", value.Length == 0 ? "Load: file is empty." : $"Load: {value}");
        return value.Length == 0 ? null : value;
    }

    public static void Save(string organizationId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, organizationId.Trim());
        Logger.Log("OrganizationSettings", $"Saved: {organizationId.Trim()}");
    }

    public static void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
            Logger.Log("OrganizationSettings", "Cleared organization-id file.");
        }
    }
}
