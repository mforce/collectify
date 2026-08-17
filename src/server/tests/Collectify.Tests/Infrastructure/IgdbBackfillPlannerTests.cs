using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Igdb;
using Xunit;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Table-driven tests for the pure backfill planner: title normalisation +
/// skip-uncertain, per-game-platform-aware candidate selection. No DB, no HTTP.
/// </summary>
public class IgdbBackfillPlannerTests
{
    private static Game Game(string title, GamePlatform platform = GamePlatform.Pc, int? year = null)
        => new() { Title = title, Platform = platform, Year = year };

    private static GameLookupResult Hit(string title, GamePlatform? platform = null, string key = "100", int? year = 2015)
        => new(Provider: "igdb", ProviderKey: key, Title: title, Platform: platform,
            Year: year, Publisher: "Pub", Developer: "Dev", Description: "Summary",
            ImageUrl: "https://images.igdb.com/x.jpg", Genres: "RPG, Adventure");

    // ---- Normalisation (case / whitespace / punctuation / diacritic insensitive) ----

    [Theory]
    [InlineData("The Witcher 3: Wild Hunt", "thewitcher3wildhunt")]
    [InlineData("  Hades  ", "hades")]
    [InlineData("Celeste", "celeste")]
    [InlineData("Hollow Knight™", "hollowknight")]
    [InlineData("Baldur's Gate 3", "baldursgate3")]
    [InlineData("DOOM (2016)", "doom2016")]
    public void NormalizeTitle_IsCaseWhitespacePunctuationInsensitive(string input, string expected)
    {
        Assert.Equal(expected, IgdbBackfillPlanner.NormalizeTitle(input));
    }

    [Theory]
    [InlineData("Pokémon", "pokemon")]     // composed accent
    [InlineData("Deja Vu", "dejavu")]
    [InlineData("Déjà Vu", "dejavu")]      // composed + decomposed accents
    public void NormalizeTitle_FoldsDiacritics(string input, string expected)
    {
        // "Pokémon" (composed) and a decomposed "Pokemon" (é as e + combining
        // acute) both normalise to "pokemon", so they compare equal.
        Assert.Equal(expected, IgdbBackfillPlanner.NormalizeTitle(input));
    }

    // ---- Exact matches ----

    [Fact]
    public void BestMatch_ExactNormalizedHit_ReturnsExact()
    {
        var match = IgdbBackfillPlanner.BestMatch(Game("Hades"), [Hit("Hades", GamePlatform.Pc, "1")]);
        Assert.NotNull(match);
        Assert.Equal(MatchTier.Exact, match.Tier);
        Assert.Equal("1", match.Result.ProviderKey);
    }

    [Fact]
    public void BestMatch_DiacriticDifference_IsExact()
    {
        // Local title has no accent; IGDB title is the accented canonical form.
        var match = IgdbBackfillPlanner.BestMatch(Game("Pokemon"), [Hit("Pokémon", GamePlatform.Pc, "7")]);
        Assert.NotNull(match);
        Assert.Equal("7", match.Result.ProviderKey);
    }

    // ---- Platform-aware accent (bias toward the game's OWN platform) ----

    [Fact]
    public void BestMatch_PcGame_PrefersPcCandidate()
    {
        // Steam-imported game is Pc; IGDB ranks PS5 first but a PC SKU exists.
        var match = IgdbBackfillPlanner.BestMatch(
            Game("Elden Ring", GamePlatform.Pc),
            [Hit("Elden Ring", GamePlatform.Ps5, "1"), Hit("Elden Ring", GamePlatform.Pc, "2")]);
        Assert.NotNull(match);
        Assert.Equal("2", match.Result.ProviderKey);
    }

    [Fact]
    public void BestMatch_SwitchGame_DoesNotStealPcCandidate()
    {
        // Manually entered Switch game must NOT get the PC SKU's identity.
        var match = IgdbBackfillPlanner.BestMatch(
            Game("Hades", GamePlatform.Switch),
            [Hit("Hades", GamePlatform.Pc, "1"), Hit("Hades", GamePlatform.Switch, "2")]);
        Assert.NotNull(match);
        Assert.Equal("2", match.Result.ProviderKey);
    }

    // ---- Year discriminator (identical titles, different releases) ----

    [Fact]
    public void BestMatch_UsesLocalYear_ToDisambiguateIdenticalTitles()
    {
        // DOOM 1993 vs DOOM 2016 — identical name, both PC. Local year picks it.
        var match = IgdbBackfillPlanner.BestMatch(
            Game("DOOM", GamePlatform.Pc, year: 2016),
            [
                Hit("DOOM", GamePlatform.Pc, "1993", year: 1993),
                Hit("DOOM", GamePlatform.Pc, "2016", year: 2016),
            ]);
        Assert.NotNull(match);
        Assert.Equal("2016", match.Result.ProviderKey);
    }

    [Fact]
    public void BestMatch_NoYearSignal_DuplicateIdenticalTitles_Declines()
    {
        // Two identical PC entries, no local year, no platform difference ->
        // ambiguous, decline rather than pick IGDB's first arbitrarily.
        Assert.Null(IgdbBackfillPlanner.BestMatch(
            Game("DOOM", GamePlatform.Pc, year: null),
            [
                Hit("DOOM", GamePlatform.Pc, "1993", year: 1993),
                Hit("DOOM", GamePlatform.Pc, "2016", year: 2016),
            ]));
    }

    [Fact]
    public void BestMatch_KnownLocalYear_ContradictedByCandidateYear_Declines()
    {
        // Local 2016 DOOM, but the only exact-title result is the 1993 SKU and
        // no 2016 entry is present in IGDB's limited response. The explicit
        // contradictory year is evidence of a wrong link: decline instead of
        // permanently locking the game (and IgdbId) to the wrong release.
        Assert.Null(IgdbBackfillPlanner.BestMatch(
            Game("DOOM", GamePlatform.Pc, year: 2016),
            [Hit("DOOM", GamePlatform.Pc, "1993", year: 1993)]));
    }

    [Fact]
    public void BestMatch_KnownLocalYear_AllCandidateYearsUnknown_FallsThrough()
    {
        // Local year is known but IGDB lacks dates for the candidates — no
        // contradiction evidence, so the normal single-candidate accept applies.
        var match = IgdbBackfillPlanner.BestMatch(
            Game("Hades", GamePlatform.Pc, year: 2020),
            [Hit("Hades", GamePlatform.Pc, "9", year: null)]);
        Assert.NotNull(match);
        Assert.Equal("9", match.Result.ProviderKey);
    }

    // ---- Skip-uncertain / no match ----

    [Fact]
    public void BestMatch_ContentDifference_IsNotAMatch()
    {
        Assert.Null(IgdbBackfillPlanner.BestMatch(
            Game("The Witcher 3"),
            [Hit("The Witcher 3: Wild Hunt", GamePlatform.Pc, "1")]));
    }

    [Fact]
    public void BestMatch_NoCandidates_ReturnsNull()
    {
        Assert.Null(IgdbBackfillPlanner.BestMatch(Game("Hades"), []));
    }

    [Fact]
    public void BestMatch_EmptyOrBlankGameTitle_ReturnsNull()
    {
        Assert.Null(IgdbBackfillPlanner.BestMatch(Game("   "), [Hit("Hades", GamePlatform.Pc)]));
        Assert.Null(IgdbBackfillPlanner.BestMatch(Game(""), [Hit("Hades", GamePlatform.Pc)]));
    }

    [Fact]
    public void BestMatch_AmbiguousDifferentSku_ReturnsNull()
    {
        Assert.Null(IgdbBackfillPlanner.BestMatch(
            Game("Dark Souls II"),
            [Hit("Dark Souls II: Scholar of the First Sin", GamePlatform.Ps4, "1")]));
    }

    [Fact]
    public void BestMatch_UnrelatedTitle_ReturnsNull()
    {
        Assert.Null(IgdbBackfillPlanner.BestMatch(
            Game("Stardew Valley"),
            [Hit("Celeste", GamePlatform.Pc, "1")]));
    }

    [Fact]
    public void BestMatch_GamePlatformSignal_Does_NotWritePlatform_OnlyDisambiguates()
    {
        // The planner returns the matched candidate; it must NOT mutate game.
        var g = Game("Hades", GamePlatform.Switch);
        var match = IgdbBackfillPlanner.BestMatch(g, [Hit("Hades", GamePlatform.Switch, "2")]);
        Assert.NotNull(match);
        // Planner is pure: the local Game is left untouched.
        Assert.Equal(GamePlatform.Switch, g.Platform);
    }
}
