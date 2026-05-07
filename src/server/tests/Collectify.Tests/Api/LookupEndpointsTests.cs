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
}
