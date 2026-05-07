using System.Net;
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
}
