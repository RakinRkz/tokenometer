namespace Tokenometer;

/// <summary>
/// Turns a raw usage percent into the number the gauge actually draws, honouring the
/// invert setting. Kept separate from GaugeRenderer's GDI+ drawing — like
/// GaugeColorSelector — so the decision can be unit tested without System.Drawing.
/// </summary>
internal static class GaugeDisplay
{
    /// <summary>
    /// The arc length and centre number to draw. Inverting shows what is left rather
    /// than what is spent; colour is deliberately not derived from this value.
    /// </summary>
    public static double ToDisplayPercent(double usedPercent, bool invert)
    {
        double used = Math.Clamp(usedPercent, 0, 100);
        return invert ? 100 - used : used;
    }

    /// <summary>
    /// Suffix appended to a gauge's caption so "12%" is not ambiguous once inverted.
    /// </summary>
    public static string Caption(string label, bool invert) =>
        invert ? $"{label} left" : label;
}
