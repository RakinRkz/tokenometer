using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Tokenometer;

internal static class GaugeRenderer
{
    private const int TrayIconSize = 32;

    public static Icon RenderTrayIcon(double sessionPercent, double weeklyPercent, bool isStale)
    {
        GaugeThresholds thresholds = GaugeSettings.Load();

        using var bitmap = new Bitmap(TrayIconSize, TrayIconSize);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        DrawRing(g, new RectangleF(2, 2, 28, 28), weeklyPercent, 5f, isStale ? Color.Gray : GetColor(weeklyPercent, thresholds));
        DrawRing(g, new RectangleF(8, 8, 16, 16), sessionPercent, 4f, isStale ? Color.DarkGray : GetColor(sessionPercent, thresholds));

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    public static Bitmap RenderLargeGauge(int size, double percent, string label, bool isStale)
    {
        var bitmap = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float thickness = size * 0.12f;
        var bounds = new RectangleF(thickness / 2, thickness / 2, size - thickness, size - thickness);
        Color color = isStale ? Color.Gray : GetColor(percent, GaugeSettings.Load());
        DrawRing(g, bounds, percent, thickness, color);

        string valueText = isStale ? "—" : $"{percent:0}%";
        using var valueFont = new Font("Segoe UI", size * 0.16f, FontStyle.Bold);
        SizeF valueSize = g.MeasureString(valueText, valueFont);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(valueText, valueFont, textBrush,
            new PointF((size - valueSize.Width) / 2, (size - valueSize.Height) / 2 - size * 0.05f));

        using var labelFont = new Font("Segoe UI", size * 0.07f);
        SizeF labelSize = g.MeasureString(label, labelFont);
        g.DrawString(label, labelFont, textBrush,
            new PointF((size - labelSize.Width) / 2, size * 0.66f));

        return bitmap;
    }

    private static void DrawRing(Graphics g, RectangleF bounds, double percent, float thickness, Color color)
    {
        percent = Math.Clamp(percent, 0, 100);

        using var trackPen = new Pen(Color.FromArgb(60, Color.Gray), thickness);
        g.DrawEllipse(trackPen, bounds);

        if (percent <= 0)
            return;

        using var valuePen = new Pen(color, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float sweep = (float)(360.0 * percent / 100.0);
        g.DrawArc(valuePen, bounds, -90, sweep);
    }

    private static Color GetColor(double percent, GaugeThresholds thresholds)
    {
        if (percent >= thresholds.RedAt) return Color.FromArgb(220, 53, 69);
        if (percent >= thresholds.AmberAt) return Color.FromArgb(255, 193, 7);
        return Color.FromArgb(40, 167, 69);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}
