using System.Text.Json;
using Tokenometer;

namespace Tokenometer.Tests;

public class UsageResponseParserTests
{
    // Trimmed down from an actual claude.ai response captured via DevTools —
    // full-fidelity would include several other null fields this parser ignores.
    private const string RealShapeJson = """
        {
            "five_hour": {
                "utilization": 43.0,
                "resets_at": "2026-08-10T14:59:59.628402+00:00",
                "limit_dollars": null
            },
            "seven_day": {
                "utilization": 48.0,
                "resets_at": "2026-08-12T18:59:59.628425+00:00",
                "limit_dollars": null
            },
            "seven_day_oauth_apps": null
        }
        """;

    [Fact]
    public void RealResponseShape_ParsesUtilizationAndResetTimes()
    {
        var fetchedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        UsageSnapshot snapshot = UsageResponseParser.Parse(RealShapeJson, fetchedAt);

        Assert.Equal(43.0, snapshot.SessionPercent);
        Assert.Equal(48.0, snapshot.WeeklyPercent);
        Assert.Equal(DateTimeOffset.Parse("2026-08-10T14:59:59.628402+00:00"), snapshot.SessionResetsAt);
        Assert.Equal(DateTimeOffset.Parse("2026-08-12T18:59:59.628425+00:00"), snapshot.WeeklyResetsAt);
        Assert.Equal(fetchedAt, snapshot.FetchedAt);
    }

    [Fact]
    public void MissingResetsAt_ParsesAsNullInsteadOfThrowing()
    {
        const string json = """
            {
                "five_hour": { "utilization": 10.0 },
                "seven_day": { "utilization": 20.0, "resets_at": null }
            }
            """;

        UsageSnapshot snapshot = UsageResponseParser.Parse(json, DateTimeOffset.UtcNow);

        Assert.Null(snapshot.SessionResetsAt);
        Assert.Null(snapshot.WeeklyResetsAt);
    }

    [Fact]
    public void MissingFiveHourKey_ThrowsKeyNotFoundException()
    {
        const string json = """{ "seven_day": { "utilization": 20.0 } }""";

        Assert.Throws<KeyNotFoundException>(() => UsageResponseParser.Parse(json, DateTimeOffset.UtcNow));
    }

    // A key that is present but null is a different failure from an absent key:
    // JsonElement hands back a Null element rather than throwing, and reading
    // through it used to raise InvalidOperationException — which UsageClient does
    // not catch, so the "response shape changed" translation was skipped.
    [Fact]
    public void NullLimitObject_ThrowsJsonExceptionNotInvalidOperation()
    {
        const string json = """
            {
                "five_hour": null,
                "seven_day": { "utilization": 20.0 }
            }
            """;

        Assert.ThrowsAny<JsonException>(() => UsageResponseParser.Parse(json, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NullUtilization_ThrowsJsonException()
    {
        const string json = """
            {
                "five_hour": { "utilization": null },
                "seven_day": { "utilization": 20.0 }
            }
            """;

        Assert.ThrowsAny<JsonException>(() => UsageResponseParser.Parse(json, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NonNumericUtilization_ThrowsJsonException()
    {
        const string json = """
            {
                "five_hour": { "utilization": "43%" },
                "seven_day": { "utilization": 20.0 }
            }
            """;

        Assert.ThrowsAny<JsonException>(() => UsageResponseParser.Parse(json, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MissingUtilizationKey_ThrowsKeyNotFoundException()
    {
        const string json = """
            {
                "five_hour": { "resets_at": null },
                "seven_day": { "utilization": 20.0 }
            }
            """;

        Assert.Throws<KeyNotFoundException>(() => UsageResponseParser.Parse(json, DateTimeOffset.UtcNow));
    }

    // Every shape failure above must land on a type UsageClient actually translates,
    // otherwise it escapes GetUsageAsync unwrapped.
    [Theory]
    [InlineData("""{ "five_hour": null, "seven_day": { "utilization": 20.0 } }""")]
    [InlineData("""{ "five_hour": { "utilization": null }, "seven_day": { "utilization": 20.0 } }""")]
    [InlineData("""{ "five_hour": { "utilization": "x" }, "seven_day": { "utilization": 20.0 } }""")]
    [InlineData("""{ "seven_day": { "utilization": 20.0 } }""")]
    [InlineData("not json at all")]
    public void EveryShapeFailure_IsATypeUsageClientTranslates(string json)
    {
        Exception ex = Record.Exception(() => UsageResponseParser.Parse(json, DateTimeOffset.UtcNow))!;

        Assert.NotNull(ex);
        Assert.True(ex is JsonException or KeyNotFoundException,
            $"UsageClient only catches JsonException/KeyNotFoundException, but got {ex.GetType().Name}.");
    }

    [Fact]
    public void MalformedJson_ThrowsJsonException()
    {
        const string notJson = "<!DOCTYPE html><html>Just a moment...</html>";

        Assert.ThrowsAny<JsonException>(() => UsageResponseParser.Parse(notJson, DateTimeOffset.UtcNow));
    }
}
