using Tokenometer;

namespace Tokenometer.Tests;

public class GaugeDisplayTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(37.4, 37.4)]
    [InlineData(100, 100)]
    public void NotInverted_ShowsUsedUnchanged(double used, double expected)
    {
        Assert.Equal(expected, GaugeDisplay.ToDisplayPercent(used, invert: false));
    }

    [Theory]
    [InlineData(0, 100)] // nothing spent yet — the whole budget is left
    [InlineData(25, 75)]
    [InlineData(100, 0)] // limit exhausted — nothing left
    public void Inverted_ShowsRemaining(double used, double expected)
    {
        Assert.Equal(expected, GaugeDisplay.ToDisplayPercent(used, invert: true));
    }

    [Theory]
    [InlineData(-10, 0, 100)] // clamped before inverting, so we never exceed 100
    [InlineData(150, 100, 0)]
    public void OutOfRangeInput_IsClampedBeforeInverting(double used, double expectedUpright, double expectedInverted)
    {
        Assert.Equal(expectedUpright, GaugeDisplay.ToDisplayPercent(used, invert: false));
        Assert.Equal(expectedInverted, GaugeDisplay.ToDisplayPercent(used, invert: true));
    }

    [Fact]
    public void Inverting_IsReversible()
    {
        const double used = 63;
        double remaining = GaugeDisplay.ToDisplayPercent(used, invert: true);
        Assert.Equal(used, GaugeDisplay.ToDisplayPercent(remaining, invert: true));
    }

    [Fact]
    public void Caption_MarksInvertedGaugesAsRemaining()
    {
        Assert.Equal("Weekly", GaugeDisplay.Caption("Weekly", invert: false));
        Assert.Equal("Weekly left", GaugeDisplay.Caption("Weekly", invert: true));
    }
}
