using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    // ---------- POST /api/covers (upload) ----------

    private record UploadResponse(string ImagePath);

    // Minimum-valid JPEG: magic bytes + JFIF / SOI marker. ZXing-y tiny
    // dummies are fine because the endpoint only sniffs the leading
    // signature, not the rest of the structure.
    private static readonly byte[] TinyJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00 };
    private static readonly byte[] TinyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    private static readonly byte[] TinyWebp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };

    private static MultipartFormDataContent FilePart(byte[] bytes, string contentType, string filename = "cover.bin")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(content, "file", filename);
        return form;
    }

    [Fact]
    public async Task Upload_Unauthenticated_Returns401()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/covers", FilePart(TinyJpeg, "image/jpeg"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_HappyPath_StoresBytesAndReturnsCoversPath()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var post = await alice.Client.PostAsync("/api/covers", FilePart(TinyJpeg, "image/jpeg"));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("/covers/", body!.ImagePath);

        // The same hash is retrievable via the GET endpoint without
        // auth (img tags don't carry cookies in some browsers).
        var get = await factory.CreateClient().GetAsync(body.ImagePath);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/jpeg", get.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TinyJpeg, await get.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_AcceptsPngAndWebp_RoundTrippingContentType()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var png = await alice.Client.PostAsync("/api/covers", FilePart(TinyPng, "image/png"));
        var pngBody = await png.Content.ReadFromJsonAsync<UploadResponse>();
        var pngGet = await factory.CreateClient().GetAsync(pngBody!.ImagePath);
        Assert.Equal("image/png", pngGet.Content.Headers.ContentType?.MediaType);

        var webp = await alice.Client.PostAsync("/api/covers", FilePart(TinyWebp, "image/webp"));
        var webpBody = await webp.Content.ReadFromJsonAsync<UploadResponse>();
        var webpGet = await factory.CreateClient().GetAsync(webpBody!.ImagePath);
        Assert.Equal("image/webp", webpGet.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_SameBytesTwice_DedupesToOneRow()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var first = (await (await alice.Client.PostAsync("/api/covers", FilePart(TinyJpeg, "image/jpeg")))
            .Content.ReadFromJsonAsync<UploadResponse>())!;
        var second = (await (await alice.Client.PostAsync("/api/covers", FilePart(TinyJpeg, "image/jpeg")))
            .Content.ReadFromJsonAsync<UploadResponse>())!;

        // Same bytes → same hash → same /covers/{hash} path.
        Assert.Equal(first.ImagePath, second.ImagePath);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsync("/api/covers", FilePart(Array.Empty<byte>(), "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedMimeType_Returns415()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsync("/api/covers",
            FilePart(System.Text.Encoding.UTF8.GetBytes("hi"), "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Upload_DeclaredImageButGarbageBytes_Returns415()
    {
        // Lying about content-type: declared image/png but the payload
        // is text. The magic-byte sniff must reject this.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var response = await alice.Client.PostAsync("/api/covers",
            FilePart(System.Text.Encoding.UTF8.GetBytes("<html>not a png</html>"), "image/png"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Upload_OversizeFile_Returns413()
    {
        // 5 MiB cap; 6 MiB JPEG (well, 6 MiB starting with the JPEG magic
        // bytes) blows past it.
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");
        var oversized = new byte[6 * 1024 * 1024];
        oversized[0] = 0xFF; oversized[1] = 0xD8; oversized[2] = 0xFF;

        var response = await alice.Client.PostAsync("/api/covers", FilePart(oversized, "image/jpeg"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
