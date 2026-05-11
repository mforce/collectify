using System.Net;
using System.Net.Http.Headers;
using Collectify.Domain.Entities;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class CoversEndpointsTests
{
    [Fact]
    public async Task Get_ExistingHash_Returns200WithStoredContentType()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "abc1234567890def",
            ContentType = "image/jpeg",
            Bytes = [0xFF, 0xD8, 0xFF],
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/covers/abc1234567890def");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Get_MissingHash_Returns404()
    {
        await using var factory = new CollectifyApiFactory();

        var response = await factory.CreateClient().GetAsync("/covers/0000000000000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    // These get URI-decoded to traversal attempts; must not reach the DB.
    [InlineData("/covers/..%2F..%2Fetc%2Fpasswd")]
    [InlineData("/covers/%2E%2E")]
    [InlineData("/covers/has-a-dash")]
    [InlineData("/covers/UPPERCASE")]
    [InlineData("/covers/short")]
    [InlineData("/covers/way-too-long-to-possibly-be-a-cover-hash")]
    public async Task Get_MalformedHash_Returns404(string url)
    {
        await using var factory = new CollectifyApiFactory();

        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_DoesNotRequireAuthentication()
    {
        // <img> tags need to load without an explicit fetch + cookie dance.
        // The hash is 16 hex chars derived from a URL the user already saw
        // in their own collection, so it's effectively unguessable.
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "deadbeefcafe1234",
            ContentType = "image/png",
            Bytes = [1, 2, 3],
        });

        var response = await factory.CreateClient().GetAsync("/covers/deadbeefcafe1234"); // no cookie

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------- ETag / Cache-Control ----------

    [Fact]
    public async Task Get_HitSetsImmutableCacheControlAndEtag()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "abc1234567890def",
            ContentType = "image/jpeg",
            Bytes = [0xFF, 0xD8, 0xFF],
        });

        var response = await factory.CreateClient().GetAsync("/covers/abc1234567890def");

        Assert.Equal("\"abc1234567890def\"", response.Headers.ETag?.ToString());
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Public);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl.MaxAge);
        // The "immutable" extension isn't a strongly-typed property on
        // CacheControlHeaderValue, so check the raw header.
        var raw = string.Join(",", response.Headers.GetValues("Cache-Control"));
        Assert.Contains("immutable", raw);
    }

    [Fact]
    public async Task Get_WithMatchingIfNoneMatch_Returns304NoBody()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "abc1234567890def",
            ContentType = "image/jpeg",
            Bytes = [0xFF, 0xD8, 0xFF],
        });

        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/covers/abc1234567890def");
        req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"abc1234567890def\""));
        var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        // Browsers + caches expect ETag + Cache-Control to come back on
        // 304s too, so they can re-prime their cache.
        Assert.Equal("\"abc1234567890def\"", response.Headers.ETag?.ToString());
        Assert.NotNull(response.Headers.CacheControl);
        // 304 must not carry a body.
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task Get_WithStarIfNoneMatch_Returns304()
    {
        // RFC 9110 §13.1.2: '*' matches any current representation.
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "abc1234567890def",
            ContentType = "image/jpeg",
            Bytes = [0xFF, 0xD8, 0xFF],
        });

        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/covers/abc1234567890def");
        req.Headers.TryAddWithoutValidation("If-None-Match", "*");
        var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithStaleIfNoneMatch_Returns200AndFreshEtag()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.SeedAsync(new CoverImage
        {
            Hash = "abc1234567890def",
            ContentType = "image/jpeg",
            Bytes = [0xFF, 0xD8, 0xFF],
        });

        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/covers/abc1234567890def");
        req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale1234567890\""));
        var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"abc1234567890def\"", response.Headers.ETag?.ToString());
    }
}
