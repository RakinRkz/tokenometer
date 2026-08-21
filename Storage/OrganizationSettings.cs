namespace Tokenometer;

/// <summary>
/// The claude.ai usage endpoint is scoped by organization id, which is per-account
/// and not a secret — stored as plain text, unlike the session cookie.
/// </summary>
internal sealed class OrganizationSettings : IOrganizationSettings
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "organization-id.txt");

    public string? Load()
    {
        if (!File.Exists(_filePath))
        {
            Logger.Debug("OrganizationSettings", "Load: no organization-id file present.");
            return null;
        }
        string value = File.ReadAllText(_filePath).Trim();
        Logger.Debug("OrganizationSettings", value.Length == 0 ? "Load: file is empty." : $"Load: {value}");
        return value.Length == 0 ? null : value;
    }

    public void Save(string organizationId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, organizationId.Trim());
        Logger.Info("OrganizationSettings", $"Saved: {organizationId.Trim()}");
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
            Logger.Info("OrganizationSettings", "Cleared organization-id file.");
        }
    }
}
