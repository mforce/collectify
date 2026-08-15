using Collectify.Infrastructure.Store;

namespace Collectify.Tests.Infrastructure;

public class SteamId64Tests
{
    private const string Known = "76561198000000000";

    [Theory]
    // Canonical documented form -> accepted.
    [InlineData("http://steamcommunity.com/openid/id/76561198000000000", "76561198000000000")]
    [InlineData("https://steamcommunity.com/openid/id/76561198000000000", "76561198000000000")]
    // Trailing slash is tolerated.
    [InlineData("https://steamcommunity.com/openid/id/76561198000000000/", "76561198000000000")]
    // Reject malformed / forged URLs -> null.
    [InlineData("https://steamcommunity.com/openid/id/", null)]                                  // empty tail
    [InlineData("https://steamcommunity.com/openid/id/abc", null)]                               // non-numeric
    [InlineData("https://steamcommunity.com/openid/id/0", null)]                                 // zero
    [InlineData("https://steamcommunity.com/wrongpath/id/76561198000000000", null)]              // wrong segment 0
    [InlineData("https://steamcommunity.com/openid/wrong/76561198000000000", null)]              // wrong segment 1
    [InlineData("https://steamcommunity.com/openid/id/76561198000000000/extra", null)]           // extra segment
    [InlineData("https://lookalike.com/openid/id/76561198000000000", null)]                      // wrong host
    [InlineData("https://evilsteamcommunity.com/openid/id/76561198000000000", null)]             // lookalike host
    [InlineData("https://steamcommunity.com/openid/id/76561198000000000?foo=1", null)]           // query
    [InlineData("https://steamcommunity.com/openid/id/76561198000000000#frag", null)]            // fragment
    [InlineData("", null)]                                                                       // empty
    [InlineData("not a url", null)]                                                              // not absolute
    public void FromClaimedId_RoundTrips(string? claimed, string? expected)
        => Assert.Equal(expected, SteamId64.FromClaimedId(claimed));

    [Fact]
    public void FromClaimedId_Rejects_SteamIdBeyondInt64()
    {
        // > long.MaxValue -> rejected (we only store positive int64-range ids).
        Assert.Null(SteamId64.FromClaimedId("http://steamcommunity.com/openid/id/9223372036854775808"));
    }

    [Fact]
    public void FromClaimedId_ApiDoesNotLeakState()
        => Assert.Equal(Known, SteamId64.FromClaimedId($"http://steamcommunity.com/openid/id/{Known}"));
}
