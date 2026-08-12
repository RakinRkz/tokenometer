using System.Text.Json;

namespace Tokenometer;

/// <summary>
/// Pure JSON parsing for claude.ai's usage response shape, kept separate from
/// UsageClient's networking so it can be unit tested against fixture JSON
/// instead of a live fetch. Throws JsonException/KeyNotFoundException on an
/// unexpected shape — UsageClient is responsible for translating those into
/// a UsageFetchException for callers.
/// </summary>
internal static class UsageResponseParser
{
    public static UsageSnapshot Parse(string json, DateTimeOffset fetchedAt)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement fiveHour = root.GetProperty("five_hour");
        JsonElement sevenDay = root.GetProperty("seven_day");

        double sessionPercent = fiveHour.GetProperty("utilization").GetDouble();
        double weeklyPercent = sevenDay.GetProperty("utilization").GetDouble();
        DateTimeOffset? sessionResetsAt = TryGetDate(fiveHour, "resets_at");
        DateTimeOffset? weeklyResetsAt = TryGetDate(sevenDay, "resets_at");

        return new UsageSnapshot(sessionPercent, weeklyPercent, sessionResetsAt, weeklyResetsAt, fetchedAt);
    }

    private static DateTimeOffset? TryGetDate(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset result) ? result : null;
    }
}
