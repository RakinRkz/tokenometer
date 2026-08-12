namespace Tokenometer;

internal interface IGaugeSettings
{
    GaugeThresholds Load();

    void Save(GaugeThresholds thresholds);
}
