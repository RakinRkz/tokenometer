using System.Text.Json;

namespace Tokenometer;

internal sealed record GaugeThresholds(double AmberAt, double RedAt)
{
    public static readonly GaugeThresholds Default = new(AmberAt: 70, RedAt: 90);
}

internal static class GaugeSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Tokenometer", "gauge-settings.json");

    public static GaugeThresholds Load()
    {
        if (!File.Exists(FilePath))
            return GaugeThresholds.Default;

        try
        {
            GaugeThresholds? thresholds = JsonSerializer.Deserialize<GaugeThresholds>(File.ReadAllText(FilePath));
            return IsValid(thresholds) ? thresholds! : GaugeThresholds.Default;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Logger.Log("GaugeSettings", $"Failed to load, using defaults: {ex.Message}");
            return GaugeThresholds.Default;
        }
    }

    public static void Save(GaugeThresholds thresholds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(thresholds));
        Logger.Log("GaugeSettings", $"Saved: amberAt={thresholds.AmberAt}, redAt={thresholds.RedAt}");
    }

    private static bool IsValid(GaugeThresholds? thresholds) =>
        thresholds is not null
        && thresholds.AmberAt is >= 0 and <= 100
        && thresholds.RedAt is >= 0 and <= 100
        && thresholds.AmberAt < thresholds.RedAt;
}
