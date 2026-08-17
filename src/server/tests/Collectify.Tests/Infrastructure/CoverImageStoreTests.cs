using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Collectify.Tests.Infrastructure;

public class CoverImageStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _options;

    public CoverImageStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_options);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private CoverImageStore NewStore(StubHandler handler) =>
        new(new CollectifyDbContext(_options), new SingleClientFactory(handler), NullLogger<CoverImageStore>.Instance);

    [Fact]
    public async Task EnsureLocalAsync_WithNullOrBlank_ReturnsNull()
    {
        var store = NewStore(new StubHandler());

        Assert.Null(await store.EnsureLocalAsync(null));
        Assert.Null(await store.EnsureLocalAsync(""));
        Assert.Null(await store.EnsureLocalAsync("   "));
    }

    [Theory]
    [InlineData("/covers/abc123")]
    [InlineData("local/path.png")]
    [InlineData("data/covers/already.webp")]
    public async Task EnsureLocalAsync_WithLocalPath_PassesThrough(string local)
    {
        var handler = new StubHandler();
        var store = NewStore(handler);

        var result = await store.EnsureLocalAsync(local);

        Assert.Equal(local, result);
        Assert.Empty(handler.RequestedUrls);
    }

    // Minimal JPEG magic bytes (FF D8 FF) so the download passes the store's
    // real-image validation; the exact bytes are what the tests assert on.
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

    [Fact]
    public async Task EnsureLocalAsync_WithRemoteUrl_DownloadsAndStoresRowReturningPublicUrl()
    {
        var payload = JpegMagic;
        var store = NewStore(new StubHandler(payload, mediaType: "image/jpeg"));

        var result = await store.EnsureLocalAsync("https://image.tmdb.org/t/p/w342/poster.jpg");

        Assert.NotNull(result);
        Assert.StartsWith("/covers/", result);
        var hash = result!.Substring("/covers/".Length);
        Assert.Matches("^[0-9a-f]{16}$", hash);

        using var verify = new CollectifyDbContext(_options);
        var row = await verify.CoverImages.AsNoTracking().FirstAsync();
        Assert.Equal(hash, row.Hash);
        Assert.Equal("image/jpeg", row.ContentType);
        Assert.Equal(payload, row.Bytes);
    }

    [Fact]
    public async Task EnsureLocalAsync_RepeatedRemoteUrl_DoesNotRefetch()
    {
        var handler = new StubHandler(payload: JpegMagic);
        var first = await NewStore(handler).EnsureLocalAsync("https://image.tmdb.org/x.jpg");
        var second = await NewStore(handler).EnsureLocalAsync("https://image.tmdb.org/x.jpg");

        Assert.Equal(first, second);
        Assert.Single(handler.RequestedUrls);

        using var verify = new CollectifyDbContext(_options);
        Assert.Equal(1, await verify.CoverImages.CountAsync());
    }

    [Fact]
    public async Task EnsureLocalAsync_OnDownloadFailure_ReturnsRemoteUrlAndStoresNothing()
    {
        var url = "https://image.tmdb.org/oops.jpg";
        var result = await NewStore(new StubHandler(status: HttpStatusCode.InternalServerError)).EnsureLocalAsync(url);

        Assert.Equal(url, result); // graceful degrade: browser can still load it

        using var verify = new CollectifyDbContext(_options);
        Assert.Equal(0, await verify.CoverImages.CountAsync());
    }

    [Fact]
    public async Task EnsureLocalAsync_PreservesContentTypeFromResponseHeader()
    {
        var store = NewStore(new StubHandler(payload: JpegMagic, mediaType: "image/webp"));

        var result = await store.EnsureLocalAsync("https://cdn.example/poster.jpg");

        using var verify = new CollectifyDbContext(_options);
        var row = await verify.CoverImages.AsNoTracking().FirstAsync();
        Assert.Equal("image/webp", row.ContentType); // header wins over URL extension
    }

    [Fact]
    public async Task EnsureLocalAsync_FallsBackToExtensionWhenContentTypeHeaderIsAbsent()
    {
        var store = NewStore(new StubHandler(payload: JpegMagic, mediaType: null));

        await store.EnsureLocalAsync("https://cdn.example/poster.png");

        using var verify = new CollectifyDbContext(_options);
        var row = await verify.CoverImages.AsNoTracking().FirstAsync();
        Assert.Equal("image/png", row.ContentType);
    }

    [Fact]
    public async Task EnsureLocalAsync_HashIsAlwaysSafeHex_NoPathTraversal()
    {
        // Even with a malicious-looking poster path, the row's Hash is
        // SHA256-derived hex; the URL never leaks into the public path.
        var store = NewStore(new StubHandler(payload: JpegMagic));

        var result = await store.EnsureLocalAsync("https://cdn.example/../../../etc/passwd.jpg");

        Assert.NotNull(result);
        var hash = result!.Substring("/covers/".Length);
        Assert.Matches("^[0-9a-f]{16}$", hash);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[]? _payload;
        private readonly HttpStatusCode _status;
        private readonly string? _mediaType;
        public List<string> RequestedUrls { get; } = new();

        public StubHandler(byte[]? payload = null, HttpStatusCode status = HttpStatusCode.OK, string? mediaType = "image/jpeg")
        {
            _payload = payload;
            _status = status;
            _mediaType = mediaType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            var response = new HttpResponseMessage(_status);
            var content = _payload is not null
                ? (HttpContent)new ByteArrayContent(_payload)
                : new StringContent(string.Empty, Encoding.UTF8);
            if (_mediaType is not null)
                content.Headers.ContentType = new MediaTypeHeaderValue(_mediaType);
            response.Content = content;
            return Task.FromResult(response);
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
