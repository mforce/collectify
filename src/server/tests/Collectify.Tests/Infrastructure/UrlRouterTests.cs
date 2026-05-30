using Collectify.Infrastructure.Lookup.Vision;

namespace Collectify.Tests.Infrastructure;

public class UrlRouterTests
{
    [Fact]
    public void ExtractTmdbId_StandardUrl_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/27205-inception");
        Assert.Equal("27205", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_NoSlug_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/634649");
        Assert.Equal("634649", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_WithTrailingSlash_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/27205-inception/");
        Assert.Equal("27205", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_WithQuerystring_ReturnsId()
    {
        var uri = new Uri("https://www.themoviedb.org/movie/27205-inception?language=en");
        Assert.Equal("27205", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_NoWww_ReturnsId()
    {
        var uri = new Uri("https://themoviedb.org/movie/634649-dune-part-two");
        Assert.Equal("634649", UrlRouter.ExtractTmdbId(uri));
    }

    [Fact]
    public void ExtractTmdbId_NonMoviePage_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractTmdbId(new Uri("https://www.themoviedb.org/person/27205-christopher-nolan")));
    }

    [Fact]
    public void ExtractTmdbId_NonTmdbDomain_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractTmdbId(new Uri("https://example.com/movie/27205-inception")));
    }

    [Fact]
    public void ExtractTmdbId_NonNumericId_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractTmdbId(new Uri("https://www.themoviedb.org/movie/abc-inception")));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_StandardUrl_ReturnsMbid()
    {
        var uri = new Uri("https://musicbrainz.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf");
        Assert.Equal("f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
            UrlRouter.ExtractMusicBrainzReleaseId(uri));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_WithTrailingSlash_ReturnsMbid()
    {
        var uri = new Uri("https://musicbrainz.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf/");
        Assert.Equal("f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
            UrlRouter.ExtractMusicBrainzReleaseId(uri));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_NonReleasePage_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractMusicBrainzReleaseId(
            new Uri("https://musicbrainz.org/artist/f4e51c80-99e2-39e1-8062-c9b8e2685bdf")));
    }

    [Fact]
    public void ExtractMusicBrainzReleaseId_NonMbDomain_ReturnsNull()
    {
        Assert.Null(UrlRouter.ExtractMusicBrainzReleaseId(
            new Uri("https://example.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf")));
    }

    [Fact]
    public void ResolveIgdbUrl_SlugReturnsSearchSlug()
    {
        var resolution = UrlRouter.ResolveIgdbUrl(new Uri("https://www.igdb.com/games/the-witcher-3-wild-hunt"));
        Assert.NotNull(resolution);
        Assert.Null(resolution.Id);
        Assert.Equal("the-witcher-3-wild-hunt", resolution.SearchSlug);
    }

    [Fact]
    public void ResolveIgdbUrl_NumericIdReturnsDirectId()
    {
        var resolution = UrlRouter.ResolveIgdbUrl(new Uri("https://www.igdb.com/games/1942"));
        Assert.NotNull(resolution);
        Assert.Equal("1942", resolution.Id);
        Assert.Null(resolution.SearchSlug);
    }

    [Fact]
    public void ResolveIgdbUrl_NonIgdbDomainReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveIgdbUrl(new Uri("https://example.com/games/some-game")));
    }

    [Fact]
    public void ResolveIgdbUrl_NonGamesPathReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveIgdbUrl(new Uri("https://www.igdb.com/companies/123")));
    }

    // --- Wikipedia ---

    [Fact]
    public void ResolveWikipediaGameUrl_StandardUrl_ReturnsTitle()
    {
        var resolution = UrlRouter.ResolveWikipediaGameUrl(new Uri("https://en.wikipedia.org/wiki/LittleBigPlanet"));
        Assert.NotNull(resolution);
        Assert.Null(resolution.Id);
        Assert.Equal("LittleBigPlanet", resolution.SearchSlug);
    }

    [Fact]
    public void ResolveWikipediaGameUrl_Underscores_ReturnsSpaces()
    {
        // Underscores -> spaces; hyphens kept as-is in the raw slug
        var resolution = UrlRouter.ResolveWikipediaGameUrl(new Uri("https://en.wikipedia.org/wiki/Paw_Patrol_Rescue_Wheels_Championship"));
        Assert.NotNull(resolution);
        Assert.Equal("Paw Patrol Rescue Wheels Championship", resolution.SearchSlug);
    }

    [Fact]
    public void ResolveWikipediaGameUrl_NonEnglishDomain_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveWikipediaGameUrl(new Uri("https://fr.wikipedia.org/wiki/The_Witcher_3")));
    }

    [Fact]
    public void ResolveWikipediaGameUrl_NonWikiPath_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveWikipediaGameUrl(new Uri("https://en.wikipedia.org/wiki/Special:Search")));
    }

    // --- PlayStation Store ---

    [Fact]
    public void ResolvePlayStationStoreUrl_ProductSlug_ReturnsTitle()
    {
        var resolution = UrlRouter.ResolvePlayStationStoreUrl(new Uri("https://store.playstation.com/product/PS5-LittleBigPlanet"));
        Assert.NotNull(resolution);
        Assert.Equal("Ps5 Littlebigplanet", resolution.SearchSlug);
    }

    [Fact]
    public void ResolvePlayStationStoreUrl_SkuSlug_ReturnsTitle()
    {
        var resolution = UrlRouter.ResolvePlayStationStoreUrl(new Uri("https://store.playstation.com/product/PS5/CUSA28598_00-LITTLEBIGPLANET0000/"));
        Assert.NotNull(resolution);
        Assert.NotNull(resolution.SearchSlug);
    }

    [Fact]
    public void ResolvePlayStationStoreUrl_NonProductPath_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolvePlayStationStoreUrl(new Uri("https://store.playstation.com/en-us")));
    }

    // --- Target ---

    [Fact]
    public void ResolveTargetUrl_ProductSlug_ReturnsTitle()
    {
        var resolution = UrlRouter.ResolveTargetUrl(new Uri("https://www.target.com/p/paw-patrol-rescue-wheels-championship-nintendo-switch/-/A-94802526"));
        Assert.NotNull(resolution);
        Assert.Equal("Paw Patrol Rescue Wheels Championship Nintendo Switch", resolution.SearchSlug);
    }

    [Fact]
    public void ResolveTargetUrl_NonProductPath_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveTargetUrl(new Uri("https://www.target.com/c/video-games/-/N-5xtg6")));
    }

    // --- Walmart ---

    [Fact]
    public void ResolveWalmartUrl_ProductSlug_ReturnsTitle()
    {
        var resolution = UrlRouter.ResolveWalmartUrl(new Uri("https://www.walmart.com/ip/PAW-Patrol-Rescue-Wheels-Championship-Nintendo-Switch/16904655730"));
        Assert.NotNull(resolution);
        Assert.Equal("Paw Patrol Rescue Wheels Championship Nintendo Switch", resolution.SearchSlug);
    }

    [Fact]
    public void ResolveWalmartUrl_NonProductPath_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveWalmartUrl(new Uri("https://www.walmart.com/browse/electronics/3944_3958")));
    }

    // --- Amazon ---

    [Fact]
    public void ResolveAmazonUrl_SlugReturnsTitle()
    {
        var resolution = UrlRouter.ResolveAmazonUrl(new Uri("https://www.amazon.com/Paw-Patrol-Rescue-Wheels-Championship/dp/B0CDEF1234"));
        Assert.NotNull(resolution);
        Assert.Equal("Paw Patrol Rescue Wheels Championship", resolution.SearchSlug);
    }

    [Fact]
    public void ResolveAmazonUrl_WithAsinOnly_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveAmazonUrl(new Uri("https://www.amazon.com/dp/B0CDEF1234")));
    }

    [Fact]
    public void ResolveAmazonUrl_NonAmazonDomain_ReturnsNull()
    {
        Assert.Null(UrlRouter.ResolveAmazonUrl(new Uri("https://www.bestbuy.com/site/game/12345")));
    }

    // --- SlugToTitle (indirectly tested via above, but verify edge case) ---

    [Fact]
    public void SlugToTitle_HyphensAndUnderscores_ReturnsTitleCase()
    {
        // Hyphens kept as-is in Wikipedia URLs; underscores -> spaces
        var resolution = UrlRouter.ResolveWikipediaGameUrl(new Uri("https://en.wikipedia.org/wiki/The_Witcher_3_Wild_Hunt"));
        Assert.NotNull(resolution);
        Assert.Equal("The Witcher 3 Wild Hunt", resolution.SearchSlug);
    }
}
