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
    public void ExtractIgdbId_AlwaysReturnsNull()
    {
        // IGDB accepts only numeric IDs; slugs can't be resolved.
        Assert.Null(UrlRouter.ExtractIgdbId(new Uri("https://www.igdb.com/games/the-witcher-3-wild-hunt")));
        Assert.Null(UrlRouter.ExtractIgdbId(new Uri("https://example.com/games/some-game")));
    }

    [Fact]
    public void ExtractIgdbSlug_ObsoleteAlwaysReturnsNull()
    {
#pragma warning disable CS0618 // ExtractIgdbSlug is obsolete
        Assert.Null(UrlRouter.ExtractIgdbSlug(new Uri("https://www.igdb.com/games/the-witcher-3")));
#pragma warning restore CS0618
    }
}
