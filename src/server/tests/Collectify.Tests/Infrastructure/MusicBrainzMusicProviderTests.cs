using System.Net;
using System.Text;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.MusicBrainz;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

public class MusicBrainzMusicProviderTests
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private MusicBrainzMusicProvider NewProvider(StubHandler handler, LookupCacheMockStorage storage, MetadataLookupOptions? overrideOptions = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/ws/2/") };
        storage.SetupStorage<List<MusicLookupResult>>(CacheTtl);
        storage.SetupStorage<MusicLookupResult>(CacheTtl);
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            MusicBrainz = new MusicBrainzOptions { UserAgent = "Collectify/1.0 (test@example.com)" },
        };
        return new MusicBrainzMusicProvider(http, storage.Mock.Object, Options.Create(options), NullLogger<MusicBrainzMusicProvider>.Instance);
    }

    private const string SearchJson = """
        {
          "releases": [
            {
              "id": "f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
              "title": "OK Computer",
              "date": "1997-05-21",
              "artist-credit": [
                { "name": "Radiohead" }
              ],
              "label-info": [
                { "label": { "name": "Parlophone" } }
              ]
            }
          ]
        }
        """;

    private const string ReleaseJson = """
        {
          "id": "f4e51c80-99e2-39e1-8062-c9b8e2685bdf",
          "title": "OK Computer",
          "date": "1997-05-21",
          "artist-credit": [
            { "name": "Radiohead" }
          ],
          "label-info": [
            { "label": { "name": "Parlophone" } }
          ]
        }
        """;

    [Fact]
    public async Task IsConfigured_ReflectsUserAgentPresence()
    {
        var configured = NewProvider(new StubHandler("{ \"releases\": [] }"), new LookupCacheMockStorage());
        var unconfigured = NewProvider(new StubHandler("never called"), new LookupCacheMockStorage(), new MetadataLookupOptions());

        Assert.True(configured.IsConfigured);
        Assert.False(unconfigured.IsConfigured);
    }

    [Fact]
    public async Task SearchAsync_WithoutUserAgent_ShortCircuitsToEmpty_AndDoesNotCallMb()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage(), new MetadataLookupOptions());

        var results = await provider.SearchAsync("ok computer");

        Assert.Empty(results);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_WithBlankQuery_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Empty(await provider.SearchAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_HitsReleaseEndpoint_WithFmtJsonAndLimit()
    {
        var handler = new StubHandler(SearchJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        await provider.SearchAsync("ok computer");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("release?", url);
        Assert.Contains("query=ok%20computer", url);
        Assert.Contains("fmt=json", url);
        Assert.Contains("limit=10", url);
    }

    [Fact]
    public async Task SearchAsync_MapsRelease_IncludingArtistAndLabelAndCoverArtUrl()
    {
        var provider = NewProvider(new StubHandler(SearchJson), new LookupCacheMockStorage());

        var results = await provider.SearchAsync("ok computer");

        var hit = Assert.Single(results);
        Assert.Equal("musicbrainz", hit.Provider);
        Assert.Equal("f4e51c80-99e2-39e1-8062-c9b8e2685bdf", hit.ProviderKey);
        Assert.Equal("OK Computer", hit.Title);
        Assert.Equal("Radiohead", hit.ArtistName);
        Assert.Equal(1997, hit.Year);
        Assert.Equal("Parlophone", hit.Label);
        Assert.Equal(
            "https://coverartarchive.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf/front-500",
            hit.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_JoinsCollaboratingArtistsViaJoinPhrases()
    {
        const string body = """
            {
              "releases": [
                {
                  "id": "abc",
                  "title": "Watch the Throne",
                  "date": "2011-08-08",
                  "artist-credit": [
                    { "name": "Jay-Z", "joinphrase": " & " },
                    { "name": "Kanye West" }
                  ]
                }
              ]
            }
            """;
        var provider = NewProvider(new StubHandler(body), new LookupCacheMockStorage());

        var hit = (await provider.SearchAsync("watch")).Single();

        Assert.Equal("Jay-Z & Kanye West", hit.ArtistName);
    }

    [Fact]
    public async Task SearchAsync_RepeatedQuery_ServesFromCache()
    {
        var handler = new StubHandler(SearchJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewProvider(handler, storage);
        var p2 = NewProvider(handler, storage); // shared mock storage

        await p1.SearchAsync("ok computer");
        await p2.SearchAsync("OK Computer"); // case-insensitive cache key

        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_ForwardsConfiguredTtlOnWrite()
    {
        var handler = new StubHandler(SearchJson);
        var storage = new LookupCacheMockStorage();
        var provider = NewProvider(handler, storage);

        await provider.SearchAsync("ok computer");

        Assert.NotEmpty(storage.Writes);
        Assert.All(storage.Writes, w => Assert.Equal(CacheTtl, w.Ttl));
    }

    [Fact]
    public async Task SearchAsync_OnUpstreamFailure_ReturnsEmptyAndDoesNotCache()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Empty(await provider.SearchAsync("x"));
        await provider.SearchAsync("x"); // should retry, not serve a cached []
        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task GetByIdAsync_HitsReleaseEndpoint_WithIncArtistAndLabels()
    {
        var handler = new StubHandler(ReleaseJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        await provider.GetByIdAsync("f4e51c80-99e2-39e1-8062-c9b8e2685bdf");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf", url);
        // MusicBrainz docs spell the inc list with literal '+' separators,
        // not URL-encoded "%2B"; we match their convention.
        Assert.Contains("inc=artist-credits+labels", url);
        Assert.Contains("fmt=json", url);
    }

    [Fact]
    public async Task GetByIdAsync_With404FromMb_ReturnsNullWithoutCaching()
    {
        var handler = new StubHandler("not found", HttpStatusCode.NotFound);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Null(await provider.GetByIdAsync("00000000-0000-0000-0000-000000000000"));
        await provider.GetByIdAsync("00000000-0000-0000-0000-000000000000");
        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task GetByIdAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(ReleaseJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewProvider(handler, storage);
        var p2 = NewProvider(handler, storage);

        var first = await p1.GetByIdAsync("f4e51c80-99e2-39e1-8062-c9b8e2685bdf");
        var second = await p2.GetByIdAsync("f4e51c80-99e2-39e1-8062-c9b8e2685bdf");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("OK Computer", second!.Title);
        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByIdAsync_AndSearchAsync_UseSeparateCacheNamespaces()
    {
        // A search whose query happens to equal an MBID must not satisfy a
        // by-id lookup for that MBID.
        var searchHandler = new StubHandler("""
            { "releases": [ { "id": "different", "title": "Other", "date": "1990-01-01" } ] }
            """);
        await NewProvider(searchHandler, new LookupCacheMockStorage()).SearchAsync("f4e51c80-99e2-39e1-8062-c9b8e2685bdf");
        Assert.Single(searchHandler.RequestedUrls);

        var idHandler = new StubHandler(ReleaseJson);
        var byId = await NewProvider(idHandler, new LookupCacheMockStorage()).GetByIdAsync("f4e51c80-99e2-39e1-8062-c9b8e2685bdf");

        Assert.Equal("OK Computer", byId!.Title);
        Assert.Single(idHandler.RequestedUrls);
    }

    // ---------- SearchByBarcodeAsync ----------

    [Fact]
    public async Task SearchByBarcodeAsync_NotConfigured_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage(), new MetadataLookupOptions());

        Assert.Empty(await provider.SearchByBarcodeAsync("0883929473076"));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_BlankBarcode_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Empty(await provider.SearchByBarcodeAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_HitsReleaseEndpointWithBarcodeQuery()
    {
        var handler = new StubHandler(SearchJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        await provider.SearchByBarcodeAsync("634904012623");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("release?", url);
        // MB's Lucene "barcode:" field uses a literal colon -- it's a
        // valid query-string sub-delim, no percent-escape needed.
        Assert.Contains("query=barcode:634904012623", url);
        Assert.Contains("fmt=json", url);
        Assert.Contains("limit=10", url);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(SearchJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewProvider(handler, storage);
        var p2 = NewProvider(handler, storage);

        await p1.SearchByBarcodeAsync("634904012623");
        await p2.SearchByBarcodeAsync("634904012623");

        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_AndSearchAsync_UseSeparateCacheNamespaces()
    {
        // A free-text search whose query happens to be a 12-digit number
        // must not satisfy a barcode lookup for the same digits.
        var searchHandler = new StubHandler("""
            { "releases": [ { "id": "different", "title": "Different", "date": "2000-01-01" } ] }
            """);
        await NewProvider(searchHandler, new LookupCacheMockStorage()).SearchAsync("634904012623");
        Assert.Single(searchHandler.RequestedUrls);

        var barcodeHandler = new StubHandler(SearchJson);
        var barcodeHits = await NewProvider(barcodeHandler, new LookupCacheMockStorage()).SearchByBarcodeAsync("634904012623");

        Assert.Single(barcodeHits);
        Assert.Equal("OK Computer", barcodeHits[0].Title);
        Assert.Single(barcodeHandler.RequestedUrls);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public List<string> RequestedUrls { get; } = new();

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
