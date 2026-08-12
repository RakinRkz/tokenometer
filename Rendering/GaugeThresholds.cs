namespace Tokenometer;

/// <summary>
/// The percent-of-limit points at which a gauge shifts from green to amber to red.
/// Validation lives here so the settings store and the settings dialog share one
/// source of truth instead of duplicating the same rule.
/// </summary>
internal sealed record GaugeThresholds(double AmberAt, double RedAt)
{
    public static readonly GaugeThresholds Default = new(AmberAt: 70, RedAt: 90);

    public static bool IsValid(GaugeThresholds? thresholds) =>
        thresholds is not null
        && thresholds.AmberAt is >= 0 and <= 100
        && thresholds.RedAt is >= 0 and <= 100
        && thresholds.AmberAt < thresholds.RedAt;
}
