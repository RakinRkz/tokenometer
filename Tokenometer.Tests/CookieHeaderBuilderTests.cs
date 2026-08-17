using Tokenometer;

namespace Tokenometer.Tests;

public class CookieHeaderBuilderTests
{
    private static readonly (string Name, string Value)[] SampleCookies =
    [
        ("sessionKey", "abc123"),
        ("lastActiveOrg", "d2980d4d-20ff-4343-9337-e27dcaadfe08"),
        ("cf_clearance", "xyz"),
    ];

    [Fact]
    public void BuildHeader_JoinsEveryCookieNotJustTheSessionOne()
    {
        string header = CookieHeaderBuilder.BuildHeader(SampleCookies);

        Assert.Equal("sessionKey=abc123; lastActiveOrg=d2980d4d-20ff-4343-9337-e27dcaadfe08; cf_clearance=xyz", header);
    }

    [Fact]
    public void Contains_FindsAMatchingNonEmptyCookie()
    {
        Assert.True(CookieHeaderBuilder.Contains(SampleCookies, "sessionKey"));
    }

    [Fact]
    public void Contains_ReturnsFalseWhenAbsent()
    {
        Assert.False(CookieHeaderBuilder.Contains(SampleCookies, "missingCookie"));
    }

    [Fact]
    public void Contains_TreatsEmptyValueAsAbsent()
    {
        (string Name, string Value)[] cookies = [("sessionKey", "")];

        Assert.False(CookieHeaderBuilder.Contains(cookies, "sessionKey"));
    }

    [Fact]
    public void FindValue_ReturnsTheOrganizationIdFromTheActiveOrgCookie()
    {
        string? value = CookieHeaderBuilder.FindValue(SampleCookies, "lastActiveOrg");

        Assert.Equal("d2980d4d-20ff-4343-9337-e27dcaadfe08", value);
    }

    [Fact]
    public void FindValue_ReturnsNullWhenCookieIsAbsent()
    {
        string? value = CookieHeaderBuilder.FindValue(SampleCookies, "notPresent");

        Assert.Null(value);
    }

    [Fact]
    public void FindValue_ReturnsNullForAnEmptyValueInsteadOfTheEmptyString()
    {
        (string Name, string Value)[] cookies = [("lastActiveOrg", "")];

        Assert.Null(CookieHeaderBuilder.FindValue(cookies, "lastActiveOrg"));
    }
}
