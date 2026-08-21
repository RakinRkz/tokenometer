using System.Text.Json;

namespace Tokenometer;

internal sealed class GaugeSettings : IGaugeSettings
{
    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tokenometer");

    private readonly string _filePath;

    public GaugeSettings() : this(DefaultFolder)
    {
    }

    // Folder injectable so the caching and fallback behaviour below can be tested
    // against a temp directory instead of the real %AppData%.
    internal GaugeSettings(string folder) => _filePath = Path.Combine(folder, "gauge-settings.json");

    /// <summary>
    /// The tray icon, tooltip and popup each re-read thresholds on every poll and
    /// every redraw, and this app is the file's only writer — so it's read from
    /// disk once and served from memory after that, with <see cref="Save"/>
    /// refreshing it. The tradeoff: editing gauge-settings.json by hand while the
    /// app is running won't be picked up until restart. The settings dialog is the
    /// supported way to change these.
    /// </summary>
    private GaugeThresholds? _cached;

    public GaugeThresholds Load() => _cached ??= ReadFromDisk();

    private GaugeThresholds ReadFromDisk()
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
            Logger.Warn("GaugeSettings", $"Failed to load, using defaults: {ex.Message}");
            return GaugeThresholds.Default;
        }
    }

    public void Save(GaugeThresholds thresholds)
    {
        // Cache before writing so the new thresholds take effect on screen even if
        // persisting them fails — a settings change is never silently ignored.
        _cached = thresholds;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(thresholds));
            Logger.Info("GaugeSettings",
                $"Saved: amberAt={thresholds.AmberAt}, redAt={thresholds.RedAt}, invert={thresholds.Invert}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Load() already catches its own I/O failures; this is the matching half.
            // The setting is live for this session, only persistence across restart is lost.
            Logger.Warn("GaugeSettings", $"Failed to save — applies this session only: {ex}");
        }
    }
}
