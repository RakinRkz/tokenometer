namespace Tokenometer;

/// <summary>
/// Persisted gauge display settings: the percent-of-limit points at which a gauge
/// shifts from green to amber to red, plus whether gauges count down remaining
/// instead of up used. Validation lives here so the settings store and the settings
/// dialog share one source of truth instead of duplicating the same rule.
/// </summary>
/// <param name="Invert">
/// When true the gauges render remaining (100 - used) rather than used. This is a
/// display choice only — the amber/red thresholds still describe *usage*, so a nearly
/// exhausted limit stays red whichever way the number is shown.
/// </param>
internal sealed record GaugeThresholds(double AmberAt, double RedAt, bool Invert = false)
{
    public static readonly GaugeThresholds Default = new(AmberAt: 70, RedAt: 90);

    public static bool IsValid(GaugeThresholds? thresholds) =>
        thresholds is not null
        && thresholds.AmberAt is >= 0 and <= 100
        && thresholds.RedAt is >= 0 and <= 100
        && thresholds.AmberAt < thresholds.RedAt;
}
