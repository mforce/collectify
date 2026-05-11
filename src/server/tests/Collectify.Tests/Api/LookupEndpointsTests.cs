using System.Net;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class LookupEndpointsTests
{
    private record LookupResponse<T>(string Provider, bool Configured, T[] Results);
    private record MovieLookupResult(
        string Provider, string ProviderKey, string Title, string? OriginalTitle,
        int? Year, string? Director, int? RuntimeMinutes, string? Description,
        string? ImageUrl, string? Genres);

    [Theory]
    [InlineData("/api/lookup/movies?q=inception")]
    [InlineData("/api/lookup/music?q=radiohead")]
    [InlineData("/api/lookup/games?q=hades")]
    public async Task Search_Unauthenticated_ReturnsUnauthorized(string url)
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/lookup/movies")]
    [InlineData("/api/lookup/movies?q=")]
    [InlineData("/api/lookup/movies?q=a")]
    public async Task Search_WithMissingOrShortQuery_ReturnsBadRequest(string url)
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchMovies_WithoutConfiguredProvider_ReturnsEmpty()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies?q=inception");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task SearchMusic_WithStubProvider_ReportsNotConfiguredAndEmpty()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<object>>(
            "/api/lookup/music?q=radiohead");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task SearchGames_WithStubProvider_ReportsNotConfiguredAndEmpty()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<object>>(
            "/api/lookup/games?q=hades");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    // ---------- /api/lookup/movies/by-id/{providerKey} ----------

    [Fact]
    public async Task GetMovieById_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookup/movies/by-id/27205");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMovieById_WithoutConfiguredProvider_ReturnsConfiguredFalseAndEmpty()
    {
        // The default test factory leaves the TMDB API key unset so the
        // registered provider reports IsConfigured = false. The endpoint
        // must not 404; it returns the same shape as search so the
        // frontend can hint "set the provider key".
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-id/27205");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task GetMovieById_WithConfiguredProviderAndKnownId_ReturnsOneResult()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            Provider: "tmdb",
            ProviderKey: "27205",
            Title: "Inception",
            OriginalTitle: "Inception",
            Year: 2010,
            Director: "Christopher Nolan",
            RuntimeMinutes: 148,
            Description: "A heist on the subconscious.",
            ImageUrl: "https://image.tmdb.org/t/p/w342/poster123.jpg",
            Genres: null);
        await using var factory = new CollectifyApiFactory { MovieProvider = ScriptedMovieProvider.WithFoundResult(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-id/27205");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("Inception", hit.Title);
        Assert.Equal("Christopher Nolan", hit.Director);
        Assert.Equal(148, hit.RuntimeMinutes);
    }

    [Fact]
    public async Task GetMovieById_WithConfiguredProviderAndUnknownId_ReturnsConfiguredTrueAndEmpty()
    {
        await using var factory = new CollectifyApiFactory { MovieProvider = ScriptedMovieProvider.NotFound() };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-id/9999999");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.Empty(body.Results);
    }

    // ---------- /api/lookup/movies/by-imdb-id/{imdbId} ----------

    [Fact]
    public async Task GetMovieByImdbId_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookup/movies/by-imdb-id/tt1375666");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMovieByImdbId_WithoutConfiguredProvider_ReturnsConfiguredFalseAndEmpty()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-imdb-id/tt1375666");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task GetMovieByImdbId_WithConfiguredProviderAndKnownId_ReturnsOneResult()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            Provider: "tmdb",
            ProviderKey: "27205",
            Title: "Inception",
            OriginalTitle: "Inception",
            Year: 2010,
            Director: "Christopher Nolan",
            RuntimeMinutes: 148,
            Description: "A heist on the subconscious.",
            ImageUrl: "https://image.tmdb.org/t/p/w342/poster123.jpg",
            Genres: null);
        await using var factory = new CollectifyApiFactory { MovieProvider = ScriptedMovieProvider.WithImdbResult(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-imdb-id/tt1375666");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("Inception", hit.Title);
        Assert.Equal("Christopher Nolan", hit.Director);
    }

    [Fact]
    public async Task GetMovieByImdbId_WithConfiguredProviderAndUnknownId_ReturnsConfiguredTrueAndEmpty()
    {
        // ScriptedMovieProvider.NotFound() leaves ByImdbId at its default null.
        await using var factory = new CollectifyApiFactory { MovieProvider = ScriptedMovieProvider.NotFound() };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-imdb-id/tt9999999");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.Empty(body.Results);
    }

    // ---------- /api/lookup/music/by-id/{providerKey} ----------

    private record MusicLookupResult(
        string Provider, string ProviderKey, string Title, string ArtistName,
        int? Year, string? Label, string? Description, string? ImageUrl, string? Genres);

    [Fact]
    public async Task GetMusicById_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookup/music/by-id/f4e51c80");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMusicById_WithoutConfiguredProvider_ReturnsConfiguredFalseAndEmpty()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MusicLookupResult>>(
            "/api/lookup/music/by-id/f4e51c80-99e2-39e1-8062-c9b8e2685bdf");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task GetMusicById_WithConfiguredProviderAndKnownId_ReturnsOneResult()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MusicLookupResult(
            Provider: "musicbrainz",
            ProviderKey: "f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
            Title: "OK Computer",
            ArtistName: "Radiohead",
            Year: 1997,
            Label: "Parlophone",
            Description: null,
            ImageUrl: "https://coverartarchive.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf/front-500",
            Genres: null);
        await using var factory = new CollectifyApiFactory { MusicProvider = ScriptedMusicProvider.WithFoundResult(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MusicLookupResult>>(
            "/api/lookup/music/by-id/f4e51c80-99e2-39e1-8062-c9b8e2685bdf");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("OK Computer", hit.Title);
        Assert.Equal("Radiohead", hit.ArtistName);
        Assert.Equal("Parlophone", hit.Label);
    }

    [Fact]
    public async Task GetMusicById_WithConfiguredProviderAndUnknownId_ReturnsConfiguredTrueAndEmpty()
    {
        await using var factory = new CollectifyApiFactory { MusicProvider = ScriptedMusicProvider.NotFound() };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MusicLookupResult>>(
            "/api/lookup/music/by-id/00000000-0000-0000-0000-000000000000");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.Empty(body.Results);
    }

    // ---------- /api/lookup/games/by-id/{providerKey} ----------

    private record GameLookupResult(
        string Provider, string ProviderKey, string Title, GamePlatform? Platform,
        int? Year, string? Publisher, string? Developer, string? Description,
        string? ImageUrl, string? Genres);

    [Fact]
    public async Task GetGameById_Unauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/lookup/games/by-id/1942");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGameById_WithoutConfiguredProvider_ReturnsConfiguredFalseAndEmpty()
    {
        // No Twitch credentials set in the test config, so the registered
        // IgdbGameProvider reports IsConfigured = false. The endpoint
        // returns configured=false rather than 404 so the frontend can hint
        // "set the provider key" the same way it does for TMDB / MusicBrainz.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<GameLookupResult>>(
            "/api/lookup/games/by-id/1942");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task GetGameById_WithConfiguredProviderAndKnownId_ReturnsOneResult()
    {
        var seeded = new Collectify.Infrastructure.Lookup.GameLookupResult(
            Provider: "igdb",
            ProviderKey: "1942",
            Title: "The Witcher 3: Wild Hunt",
            Platform: GamePlatform.Pc,
            Year: 2015,
            Publisher: "Warner Bros",
            Developer: "CD Projekt Red",
            Description: "A monster hunter searches for his adopted daughter.",
            ImageUrl: "https://images.igdb.com/igdb/image/upload/t_cover_big/co1wyy.jpg",
            Genres: "RPG, Adventure");
        await using var factory = new CollectifyApiFactory { GameProvider = ScriptedGameProvider.WithFoundResult(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<GameLookupResult>>(
            "/api/lookup/games/by-id/1942");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("The Witcher 3: Wild Hunt", hit.Title);
        Assert.Equal("CD Projekt Red", hit.Developer);
        Assert.Equal(2015, hit.Year);
    }

    [Fact]
    public async Task GetGameById_WithConfiguredProviderAndUnknownId_ReturnsConfiguredTrueAndEmpty()
    {
        await using var factory = new CollectifyApiFactory { GameProvider = ScriptedGameProvider.NotFound() };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<GameLookupResult>>(
            "/api/lookup/games/by-id/9999999");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        Assert.Empty(body.Results);
    }

    // ---------- /api/lookup/{type}/by-barcode/{code} ----------

    [Theory]
    [InlineData("/api/lookup/movies/by-barcode/0883929473076")]
    [InlineData("/api/lookup/music/by-barcode/634904012623")]
    [InlineData("/api/lookup/games/by-barcode/0883929473076")]
    public async Task GetByBarcode_Unauthenticated_ReturnsUnauthorized(string url)
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMovieByBarcode_WithoutConfiguredProvider_ReturnsConfiguredFalseAndEmpty()
    {
        // Default factory leaves TMDB unconfigured; the endpoint must not
        // 404 -- it returns configured=false so the UI can hint.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-barcode/0883929473076");

        Assert.NotNull(body);
        Assert.False(body!.Configured);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task GetMovieByBarcode_WithConfiguredProvider_ReturnsScriptedResults()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MovieLookupResult(
            Provider: "tmdb",
            ProviderKey: "27205",
            Title: "Inception",
            OriginalTitle: "Inception",
            Year: 2010,
            Director: null,
            RuntimeMinutes: null,
            Description: null,
            ImageUrl: null,
            Genres: null);
        await using var factory = new CollectifyApiFactory { MovieProvider = ScriptedMovieProvider.WithBarcodeResults(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MovieLookupResult>>(
            "/api/lookup/movies/by-barcode/0883929473076");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("Inception", hit.Title);
    }

    [Fact]
    public async Task GetMusicByBarcode_WithConfiguredProvider_ReturnsScriptedResults()
    {
        var seeded = new Collectify.Infrastructure.Lookup.MusicLookupResult(
            Provider: "musicbrainz",
            ProviderKey: "f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
            Title: "OK Computer",
            ArtistName: "Radiohead",
            Year: 1997,
            Label: "Parlophone",
            Description: null,
            ImageUrl: null,
            Genres: null);
        await using var factory = new CollectifyApiFactory { MusicProvider = ScriptedMusicProvider.WithBarcodeResults(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<MusicLookupResult>>(
            "/api/lookup/music/by-barcode/634904012623");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("OK Computer", hit.Title);
        Assert.Equal("Radiohead", hit.ArtistName);
    }

    [Fact]
    public async Task GetGameByBarcode_WithConfiguredProvider_ReturnsScriptedResults()
    {
        var seeded = new Collectify.Infrastructure.Lookup.GameLookupResult(
            Provider: "igdb",
            ProviderKey: "1942",
            Title: "The Witcher 3: Wild Hunt",
            Platform: GamePlatform.Pc,
            Year: 2015,
            Publisher: null,
            Developer: "CD Projekt Red",
            Description: null,
            ImageUrl: null,
            Genres: null);
        await using var factory = new CollectifyApiFactory { GameProvider = ScriptedGameProvider.WithBarcodeResults(seeded) };
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var body = await alice.Client.GetJsonAsync<LookupResponse<GameLookupResult>>(
            "/api/lookup/games/by-barcode/0883929473076");

        Assert.NotNull(body);
        Assert.True(body!.Configured);
        var hit = Assert.Single(body.Results);
        Assert.Equal("The Witcher 3: Wild Hunt", hit.Title);
    }
}
