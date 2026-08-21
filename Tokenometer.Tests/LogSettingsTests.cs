using Tokenometer;

namespace Tokenometer.Tests;

public class LogSettingsTests
{
    [Fact]
    public void DefaultsToQuietWhenNothingIsStored()
    {
        using var folder = new TempFolder();

        Assert.False(new LogSettings(folder.Path).Verbose);
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
    public void MalformedJson_FallsBackToQuietRatherThanVerbose()
    {
        using var folder = new TempFolder();
        folder.Write("log-settings.json", "{ not json");

        // An unreadable setting should not silently start recording detail nobody
        // asked for, so it falls back to the same off state as a fresh install.
        Assert.False(new LogSettings(folder.Path).Verbose);
    }

    [Fact]
    public void VerboseCanBeTurnedBackOff()
    {
        using var folder = new TempFolder();
        var settings = new LogSettings(folder.Path);

        settings.SetVerbose(true);
        settings.SetVerbose(false);

        Assert.False(new LogSettings(folder.Path).Verbose);
    }
}
