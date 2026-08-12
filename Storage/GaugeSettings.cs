using System.Text.Json;

namespace Tokenometer;

internal sealed class GaugeSettings : IGaugeSettings
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "gauge-settings.json");

    public GaugeThresholds Load()
    {
        if (!File.Exists(_filePath))
            return GaugeThresholds.Default;

        try
        {
            GaugeThresholds? thresholds = JsonSerializer.Deserialize<GaugeThresholds>(File.ReadAllText(_filePath));
            return GaugeThresholds.IsValid(thresholds) ? thresholds! : GaugeThresholds.Default;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Logger.Log("GaugeSettings", $"Failed to load, using defaults: {ex.Message}");
            return GaugeThresholds.Default;
        }
    }

    public void Save(GaugeThresholds thresholds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(thresholds));
        Logger.Log("GaugeSettings", $"Saved: amberAt={thresholds.AmberAt}, redAt={thresholds.RedAt}");
    }
}
