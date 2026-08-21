using Tokenometer;

namespace Tokenometer.Tests;

public class GaugeSettingsStoreTests
{
    [Fact]
    public void NoFile_LoadsDefaultsWithoutCreatingOne()
    {
        using var folder = new TempFolder();

        Assert.Equal(GaugeThresholds.Default, new GaugeSettings(folder.Path).Load());
        Assert.False(folder.Exists("gauge-settings.json"));
    }

    [Fact]
    public void SavedThresholds_SurviveToANewInstance()
    {
        using var folder = new TempFolder();
        var saved = new GaugeThresholds(AmberAt: 55, RedAt: 80, Invert: true);

        new GaugeSettings(folder.Path).Save(saved);

        Assert.Equal(saved, new GaugeSettings(folder.Path).Load());
    }

    [Fact]
    public void MalformedJson_FallsBackToDefaults()
    {
        using var folder = new TempFolder();
        folder.Write("gauge-settings.json", "{ not json");

        Assert.Equal(GaugeThresholds.Default, new GaugeSettings(folder.Path).Load());
    }

    [Fact]
    public void ThresholdsThatFailValidation_FallBackToDefaults()
    {
        using var folder = new TempFolder();
        folder.Write("gauge-settings.json", """{"AmberAt":90,"RedAt":70}"""); // amber must be below red

        Assert.Equal(GaugeThresholds.Default, new GaugeSettings(folder.Path).Load());
    }

    [Fact]
    public void ZeroAndOneHundred_AreAcceptedFromDisk()
    {
        using var folder = new TempFolder();
        folder.Write("gauge-settings.json", """{"AmberAt":0,"RedAt":100}""");

        GaugeThresholds loaded = new GaugeSettings(folder.Path).Load();

        Assert.Equal(0, loaded.AmberAt);
        Assert.Equal(100, loaded.RedAt);
    }

    // --- the cache: Load is called on every poll and every redraw ---

    [Fact]
    public void LoadIsCached_SoTheFileIsNotRereadOnEveryCall()
    {
        using var folder = new TempFolder();
        var store = new GaugeSettings(folder.Path);
        store.Save(new GaugeThresholds(55, 80));

        // Change the file behind the store's back; the cached value must win.
        folder.Write("gauge-settings.json", """{"AmberAt":10,"RedAt":20}""");

        Assert.Equal(new GaugeThresholds(55, 80), store.Load());
    }

    [Fact]
    public void SaveRefreshesTheCache()
    {
        using var folder = new TempFolder();
        var store = new GaugeSettings(folder.Path);
        store.Load();                                   // prime with defaults

        store.Save(new GaugeThresholds(40, 60));

        Assert.Equal(new GaugeThresholds(40, 60), store.Load());
    }
}
