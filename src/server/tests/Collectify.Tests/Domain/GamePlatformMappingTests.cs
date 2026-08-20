using Collectify.Domain.Enums;

// Flat-rooted namespace on purpose: nesting under Collectify.Tests.Domain
// would shadow Collectify.Domain in any test file under that branch.
namespace Collectify.Tests;

public class GamePlatformMappingTests
{
    [Theory]
    [InlineData("PC", GamePlatform.Pc)]
    [InlineData("Microsoft Windows", GamePlatform.Pc)]
    [InlineData("PC (Microsoft Windows)", GamePlatform.Pc)]
    [InlineData("PC(Microsoft Windows)", GamePlatform.Pc)]
    [InlineData("Apple Macintosh", GamePlatform.Mac)]
    [InlineData("Mac OS", GamePlatform.Mac)]
    [InlineData("Linux", GamePlatform.Pc)] // Linux folds into Pc (#102)
    [InlineData("PlayStation 5", GamePlatform.Ps5)]
    [InlineData("playstation-5", GamePlatform.Ps5)]
    [InlineData(" PS_5 ", GamePlatform.Ps5)]
    [InlineData("PS5", GamePlatform.Ps5)]
    [InlineData("Xbox 360", GamePlatform.Xbox360)]
    [InlineData("xbox360", GamePlatform.Xbox360)]
    [InlineData("Xbox Series X", GamePlatform.XboxSeriesXS)]
    [InlineData("Xbox Series S", GamePlatform.XboxSeriesXS)]
    [InlineData("XSX", GamePlatform.XboxSeriesXS)]
    [InlineData("Nintendo Switch", GamePlatform.Switch)]
    [InlineData("Switch", GamePlatform.Switch)]
    [InlineData("Nintendo Switch 2", GamePlatform.Switch2)]
    [InlineData("Game Boy Advance", GamePlatform.GameBoyAdvance)]
    [InlineData("GBA", GamePlatform.GameBoyAdvance)]
    [InlineData("Mega Drive", GamePlatform.SegaGenesis)]
    [InlineData("Sega Genesis", GamePlatform.SegaGenesis)]
    // A Steam Deck is a PC (SteamOS/Linux or Windows); it classifies as Pc.
    // The delivery dimension is DigitalStores (Steam), not the
    // platform (see #103).
    [InlineData("Steam Deck", GamePlatform.Pc)]
    [InlineData("Steamdeck", GamePlatform.Pc)]
    public void TryParse_MapsKnownAliases_CaseAndPunctuationInsensitive(string raw, GamePlatform expected)
    {
        Assert.Equal(expected, GamePlatformMapping.TryParse(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("3DO")]
    [InlineData("Atari Jaguar")]
    [InlineData("Something Completely Made Up")]
    public void TryParse_UnknownOrBlank_ReturnsNull(string? raw)
    {
        Assert.Null(GamePlatformMapping.TryParse(raw));
    }
}
