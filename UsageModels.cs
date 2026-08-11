namespace Tokenometer;

internal sealed record UsageSnapshot(
    double SessionPercent,
    double WeeklyPercent,
    DateTimeOffset? SessionResetsAt,
    DateTimeOffset? WeeklyResetsAt,
    DateTimeOffset FetchedAt);

internal sealed class UsageFetchException : Exception
{
    public UsageFetchException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
