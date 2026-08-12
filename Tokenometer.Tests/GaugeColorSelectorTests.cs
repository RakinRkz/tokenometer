using System.Drawing;
using Tokenometer;

namespace Tokenometer.Tests;

public class GaugeColorSelectorTests
{
    private static readonly GaugeThresholds Thresholds = new(AmberAt: 70, RedAt: 90);

    private static readonly Color Green = Color.FromArgb(40, 167, 69);
    private static readonly Color Amber = Color.FromArgb(255, 193, 7);
    private static readonly Color Red = Color.FromArgb(220, 53, 69);

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(69.9)]
    public void BelowAmberThreshold_IsGreen(double percent)
    {
        Assert.Equal(Green, GaugeColorSelector.GetColor(percent, Thresholds));
    }

    [Theory]
    [InlineData(70)] // exactly at the threshold counts as crossed
    [InlineData(80)]
    [InlineData(89.9)]
    public void AtOrAboveAmberButBelowRed_IsAmber(double percent)
    {
        Assert.Equal(Amber, GaugeColorSelector.GetColor(percent, Thresholds));
    }

    [Theory]
    [InlineData(90)] // exactly at the threshold counts as crossed
    [InlineData(95)]
    [InlineData(100)]
    public void AtOrAboveRedThreshold_IsRed(double percent)
    {
        Assert.Equal(Red, GaugeColorSelector.GetColor(percent, Thresholds));
    }

    [Fact]
    public void CustomThresholds_AreRespectedInsteadOfDefaults()
    {
        var lenient = new GaugeThresholds(AmberAt: 40, RedAt: 60);

        Assert.Equal(Green, GaugeColorSelector.GetColor(39, lenient));
        Assert.Equal(Amber, GaugeColorSelector.GetColor(40, lenient));
        Assert.Equal(Red, GaugeColorSelector.GetColor(60, lenient));
    }
}
