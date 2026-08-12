using Tokenometer;

namespace Tokenometer.Tests;

public class GaugeThresholdsTests
{
    [Fact]
    public void Default_IsValid()
    {
        Assert.True(GaugeThresholds.IsValid(GaugeThresholds.Default));
    }

    [Fact]
    public void Null_IsNotValid()
    {
        Assert.False(GaugeThresholds.IsValid(null));
    }

    [Theory]
    [InlineData(70, 90)] // ordinary case
    [InlineData(0, 100)] // full range
    [InlineData(89, 90)] // adjacent
    public void AmberBelowRedWithinRange_IsValid(double amber, double red)
    {
        Assert.True(GaugeThresholds.IsValid(new GaugeThresholds(amber, red)));
    }

    [Theory]
    [InlineData(90, 70)] // reversed
    [InlineData(80, 80)] // equal — amber must be strictly below red
    [InlineData(-1, 90)] // amber out of range
    [InlineData(70, 101)] // red out of range
    public void InvalidCombinations_AreRejected(double amber, double red)
    {
        Assert.False(GaugeThresholds.IsValid(new GaugeThresholds(amber, red)));
    }
}
