using System.Net;
using System.Net.Http.Headers;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Lookup.Vision;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class VisionLookupEndpointsTests
{
    private record LookupResponse<T>(string Provider, bool Configured, T[] Results, string? Hint);
    private static readonly byte[] TinyJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

    private static MultipartFormDataContent FilePart(byte[] bytes, string contentType = "image/jpeg", string filename = "cover.jpg")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", filename);
        return form;
    }

    // --- Auth ---

    [Theory]
    [InlineData("/api/lookup/movies/by-image")]
    [InlineData("/api/lookup/music/by-image")]
    [InlineData("/api/lookup/games/by-image")]
    public async Task ByImage_Unauthenticated_Returns401(string url)
    {
        await using var factory = new CollectifyApiFactory();
        var response = await factory.CreateClient().PostAsync(url, FilePart(TinyJpeg));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Not configured ---

    [Fact]
    public async Task ByImage_VisionNotConfigured_ReturnsConfiguredFalse()
    {
        await using var factory = new CollectifyApiFactory
        {
            VisionClient = FakeVisionClient.NotConfigured()
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<object>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    // --- OCR path ---

    [Fact]
    public async Task ByImage_OcrPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", "Inception", 2010, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithText("INCEPTION", "2010")
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
        Assert.Equal("Inception", body.Results[0].Title);
    }

    // --- Web entity path ---

    [Fact]
    public async Task ByImage_WebEntityPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", null, 2010, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithEntities(
                new WebEntitySignal("Inception (2010 film)", 0.95f))
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
    }

    // --- URL routing ranked first ---

    [Fact]
    public async Task ByImage_UrlRouting_RankedAboveSearchResults()
    {
        var directHit = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", null, 2010, "Christopher Nolan", 148, null, null, null);
        var searchHit = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "634649", "Dune Part Two", null, 2024, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider
            {
                SearchResults = [searchHit],
                ById = directHit
            },
            VisionClient = new FakeVisionClient
            {
                DetectedText = ["SOME", "RANDOM", "TEXT"],
                MatchingUrls = [new MatchingUrlSignal(
                    new Uri("https://www.themoviedb.org/movie/27205-inception"),
                    "pagesWithMatchingImages")]
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Results);
        // Direct ID match should be first
        Assert.Equal("27205", body.Results[0].ProviderKey);
    }

    // --- Deduplication ---

    [Fact]
    public async Task ByImage_DeduplicatesByProviderKey()
    {
        var sameResult = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            "tmdb", "27205", "Inception", null, 2010, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = new ScriptedMovieProvider { SearchResults = [sameResult], ById = sameResult },
            VisionClient = new FakeVisionClient
            {
                DetectedText = ["INCEPTION", "2010"],
                WebEntities = [new WebEntitySignal("Inception", 0.9f)],
                MatchingUrls = [new MatchingUrlSignal(
                    new Uri("https://www.themoviedb.org/movie/27205-inception"),
                    "pagesWithMatchingImages")]
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MovieLookupResult>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        // All three paths resolve to same ProviderKey; should appear once
        Assert.Single(body!.Results);
    }

    // --- All paths empty -> hint ---

    [Fact]
    public async Task ByImage_AllPathsEmpty_ReturnsHint()
    {
        await using var factory = new CollectifyApiFactory
        {
            MovieProvider = ScriptedMovieProvider.NotFound(),
            VisionClient = new FakeVisionClient
            {
                DetectedText = ["X"],
                WebEntities = [],
                MatchingUrls = []
            }
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<object>>(
            "/api/lookup/movies/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.Empty(body.Results);
        Assert.NotNull(body.Hint);
    }

    // --- Upload validation ---

    [Fact]
    public async Task ByImage_EmptyFile_Returns400()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var response = await alice.Client.PostAsync(
            "/api/lookup/movies/by-image", FilePart(Array.Empty<byte>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ByImage_WrongContentType_Returns415()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var response = await alice.Client.PostAsync(
            "/api/lookup/movies/by-image",
            FilePart(System.Text.Encoding.UTF8.GetBytes("hi"), "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // --- All three media types ---

    [Fact]
    public async Task ByImage_Music_OcrPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MusicLookupResult(
            "musicbrainz", "f4e51c80", "OK Computer", "Radiohead", 1997, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            MusicProvider = new ScriptedMusicProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithText("OK", "COMPUTER", "RADIOHEAD")
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.MusicLookupResult>>(
            "/api/lookup/music/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
    }

    [Fact]
    public async Task ByImage_Games_OcrPath_ReturnsCandidates()
    {
        var seeded = new Collectify.Infrastructure.Lookup.GameLookupResult(
            "igdb", "1942", "The Witcher 3", null, 2015, null, null, null, null, null);
        await using var factory = new CollectifyApiFactory
        {
            GameProvider = new ScriptedGameProvider { SearchResults = [seeded] },
            VisionClient = FakeVisionClient.WithText("WITCHER", "THREE")
        };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var body = await alice.Client.PostMultipartAndReadJsonAsync<LookupResponse<Collectify.Infrastructure.Lookup.GameLookupResult>>(
            "/api/lookup/games/by-image", FilePart(TinyJpeg));
        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.NotEmpty(body.Results);
    }
}
