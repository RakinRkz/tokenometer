namespace Tokenometer;

/// <summary>
/// Pure percent-to-color mapping, kept separate from GaugeRenderer's GDI+ drawing
/// so the actual decision logic can be unit tested without touching System.Drawing.
/// </summary>
internal static class GaugeColorSelector
{
    public static Color GetColor(double percent, GaugeThresholds thresholds)
    {
        if (percent >= thresholds.RedAt) return Color.FromArgb(220, 53, 69);
        if (percent >= thresholds.AmberAt) return Color.FromArgb(255, 193, 7);
        return Color.FromArgb(40, 167, 69);
    }
}
