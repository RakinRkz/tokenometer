using Tokenometer;

namespace Tokenometer.Tests;

public class LogSettingsTests
{
    [Fact]
    public void DefaultsToVerboseWhenNothingIsStored()
    {
        using var folder = new TempFolder();

        Assert.True(new LogSettings(folder.Path).Verbose);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChoiceSurvivesToANewInstance(bool verbose)
    {
        using var folder = new TempFolder();

        new LogSettings(folder.Path).SetVerbose(verbose);

        Assert.Equal(verbose, new LogSettings(folder.Path).Verbose);
    }

    [Fact]
    public void MalformedJson_FallsBackToVerboseRatherThanGoingQuiet()
    {
        using var folder = new TempFolder();
        folder.Write("log-settings.json", "{ not json");

        // Silently dropping to Info would lose exactly the detail someone is
        // troubleshooting with, so an unreadable setting errs toward more logging.
        Assert.True(new LogSettings(folder.Path).Verbose);
    }

    [Fact]
    public void QuietCanBeTurnedBackOn()
    {
        using var folder = new TempFolder();
        var settings = new LogSettings(folder.Path);

        settings.SetVerbose(false);
        settings.SetVerbose(true);

        Assert.True(new LogSettings(folder.Path).Verbose);
    }
}
