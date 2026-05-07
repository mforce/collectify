using System.Net;
using System.Text;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.Extensions.Logging.Abstractions;

namespace Collectify.Tests.Infrastructure;

public class CoverImageStoreTests : IDisposable
{
    private readonly string _dir;

    public CoverImageStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cover-store-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private CoverImageStore NewStore(StubHandler handler)
    {
        var factory = new SingleClientFactory(handler);
        return new CoverImageStore(_dir, factory, NullLogger<CoverImageStore>.Instance);
    }

    [Fact]
    public async Task EnsureLocalAsync_WithNullOrBlank_ReturnsNull()
    {
        var store = NewStore(new StubHandler());

        Assert.Null(await store.EnsureLocalAsync(null));
        Assert.Null(await store.EnsureLocalAsync(""));
        Assert.Null(await store.EnsureLocalAsync("   "));
    }

    [Theory]
    [InlineData("/covers/abc123.jpg")]
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

    [Fact]
    public async Task EnsureLocalAsync_WithRemoteUrl_DownloadsAndReturnsLocalPublicUrl()
    {
        var handler = new StubHandler(payload: new byte[] { 1, 2, 3, 4 });
        var store = NewStore(handler);

        var result = await store.EnsureLocalAsync("https://image.tmdb.org/t/p/w342/poster.jpg");

        Assert.NotNull(result);
        Assert.StartsWith("/covers/", result);
        Assert.EndsWith(".jpg", result);
        var filename = result!.Substring("/covers/".Length);
        Assert.True(File.Exists(Path.Combine(_dir, filename)));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(_dir, filename)));
    }

    [Fact]
    public async Task EnsureLocalAsync_RepeatedRemoteUrl_ServesFromDiskOnSecondCall()
    {
        var handler = new StubHandler(payload: new byte[] { 9 });
        var store = NewStore(handler);

        var first = await store.EnsureLocalAsync("https://image.tmdb.org/x.jpg");
        var second = await store.EnsureLocalAsync("https://image.tmdb.org/x.jpg");

        Assert.Equal(first, second);
        Assert.Single(handler.RequestedUrls); // only one network call
    }

    [Fact]
    public async Task EnsureLocalAsync_OnDownloadFailure_ReturnsRemoteUrlAsFallback()
    {
        var handler = new StubHandler(status: HttpStatusCode.InternalServerError);
        var store = NewStore(handler);

        var url = "https://image.tmdb.org/oops.jpg";
        var result = await store.EnsureLocalAsync(url);

        Assert.Equal(url, result); // graceful degrade: browser can still load it
        // No file written:
        Assert.False(Directory.Exists(_dir) && Directory.EnumerateFiles(_dir).Any());
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    [InlineData(".gif")]
    public async Task EnsureLocalAsync_KeepsKnownExtensions(string ext)
    {
        var handler = new StubHandler(payload: new byte[] { 1 });
        var store = NewStore(handler);

        var result = await store.EnsureLocalAsync($"https://cdn.example/poster{ext}");

        Assert.NotNull(result);
        Assert.EndsWith(ext, result);
    }

    [Theory]
    [InlineData("https://cdn.example/poster.aspx")]
    [InlineData("https://cdn.example/poster.exe")]
    [InlineData("https://cdn.example/poster")]
    public async Task EnsureLocalAsync_WithUnknownOrMissingExtension_FallsBackToJpg(string url)
    {
        var handler = new StubHandler(payload: new byte[] { 1 });
        var store = NewStore(handler);

        var result = await store.EnsureLocalAsync(url);

        Assert.NotNull(result);
        Assert.EndsWith(".jpg", result);
    }

    [Fact]
    public async Task EnsureLocalAsync_FilenameIsAlwaysHashOnly_NoPathTraversal()
    {
        // Even with a malicious-looking poster path, the cached filename is
        // SHA256-derived hex; nothing about the URL leaks into the filename.
        var handler = new StubHandler(payload: new byte[] { 1 });
        var store = NewStore(handler);

        var result = await store.EnsureLocalAsync("https://cdn.example/../../../etc/passwd.jpg");

        Assert.NotNull(result);
        var filename = result!.Substring("/covers/".Length);
        // 16 hex chars + .jpg
        Assert.Matches("^[0-9a-f]{16}\\.jpg$", filename);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[]? _payload;
        private readonly HttpStatusCode _status;
        public List<string> RequestedUrls { get; } = new();

        public StubHandler(byte[]? payload = null, HttpStatusCode status = HttpStatusCode.OK)
        {
            _payload = payload;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            var response = new HttpResponseMessage(_status);
            if (_payload is not null) response.Content = new ByteArrayContent(_payload);
            else response.Content = new StringContent(string.Empty, Encoding.UTF8);
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
