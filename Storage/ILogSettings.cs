namespace Tokenometer;

internal interface ILogSettings
{
    bool Verbose { get; }

    void SetVerbose(bool verbose);
}
