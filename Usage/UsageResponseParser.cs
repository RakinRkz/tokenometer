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

        JsonElement fiveHour = GetLimitObject(root, "five_hour");
        JsonElement sevenDay = GetLimitObject(root, "seven_day");

        double sessionPercent = GetUtilization(fiveHour, "five_hour");
        double weeklyPercent = GetUtilization(sevenDay, "seven_day");
        DateTimeOffset? sessionResetsAt = TryGetDate(fiveHour, "resets_at");
        DateTimeOffset? weeklyResetsAt = TryGetDate(sevenDay, "resets_at");

        return new UsageSnapshot(sessionPercent, weeklyPercent, sessionResetsAt, weeklyResetsAt, fetchedAt);
    }

    /// <summary>
    /// GetProperty throws KeyNotFoundException for an absent key, but a key that is
    /// present and null hands back a Null element whose own GetProperty/GetDouble
    /// then throw InvalidOperationException — which UsageClient does not translate,
    /// so the "response shape changed" message would be bypassed entirely. Real
    /// responses do carry null limit objects (seven_day_oauth_apps is null in every
    /// capture so far), so a null five_hour is a plausible shape for an account
    /// without that limit. Normalise it into the JsonException callers handle.
    /// </summary>
    private static JsonElement GetLimitObject(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Object)
            throw new JsonException($"Expected '{propertyName}' to be an object, got {value.ValueKind}.");
        return value;
    }

    private static double GetUtilization(JsonElement limit, string limitName)
    {
        if (!limit.TryGetProperty("utilization", out JsonElement value))
            throw new KeyNotFoundException($"'{limitName}.utilization' is missing.");
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double utilization))
            throw new JsonException($"Expected '{limitName}.utilization' to be a number, got {value.ValueKind}.");
        return utilization;
    }

    private static DateTimeOffset? TryGetDate(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset result) ? result : null;
    }
}
