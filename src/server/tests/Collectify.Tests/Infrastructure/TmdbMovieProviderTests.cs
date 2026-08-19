using System.Net;
using System.Text;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Tmdb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Collectify.Tests.Infrastructure;

public class TmdbMovieProviderTests
{
    private TmdbMovieProvider NewProvider(StubHandler handler, LookupCacheMockStorage storage, MetadataLookupOptions? overrideOptions = null, FakeUpcClient? upc = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            Tmdb = new TmdbOptions { ApiKey = "key-xyz" },
        };
        var expectedTtl = options.CacheTtl;
        storage.SetupStorage<List<MovieLookupResult>>(expectedTtl);
        storage.SetupStorage<MovieLookupResult>(expectedTtl);
        return new TmdbMovieProvider(http, upc ?? FakeUpcClient.NotRecognised(), storage.Mock.Object, Options.Create(options), NullLogger<TmdbMovieProvider>.Instance);
    }

    // Reused by the barcode tests below; the search-by-title pipeline that
    // backs SearchByBarcodeAsync hits /search/movie with whatever title the
    // UPC client surfaces, so a single stub body is enough.
    private const string BarcodeSearchJson = """
        {
          "results": [
            {
              "id": 27205,
              "title": "Inception",
              "original_title": "Inception",
              "release_date": "2010-07-15",
              "overview": "A heist on the subconscious.",
              "poster_path": "/poster123.jpg"
            }
          ]
        }
        """;

    [Fact]
    public async Task IsConfigured_ReflectsApiKeyPresence()
    {
        var configured = NewProvider(new StubHandler("{ \"results\": [] }"), new LookupCacheMockStorage());
        var unconfigured = NewProvider(new StubHandler("never called"), new LookupCacheMockStorage(), new MetadataLookupOptions());

        Assert.True(configured.IsConfigured);
        Assert.False(unconfigured.IsConfigured);
    }

    [Fact]
    public async Task SearchAsync_WithoutApiKey_ShortCircuitsToEmpty_AndDoesNotCallTmdb()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage(), new MetadataLookupOptions());

        var results = await provider.SearchAsync("inception");

        Assert.Empty(results);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_WithBlankQuery_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        var results = await provider.SearchAsync("   ");

        Assert.Empty(results);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_HitsTmdbWithUrlEncodedQuery_AndIncludesApiKey()
    {
        var handler = new StubHandler("{ \"results\": [] }");
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        await provider.SearchAsync("the matrix");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("search/movie", url);
        Assert.Contains("query=the%20matrix", url);
        Assert.Contains("api_key=key-xyz", url);
        Assert.Contains("include_adult=false", url);
    }

    [Fact]
    public async Task SearchAsync_MapsSummariesToLookupResults_WithImageUrlAndYear()
    {
        const string body = """
        {
          "results": [
            {
              "id": 27205,
              "title": "Inception",
              "original_title": "Inception",
              "release_date": "2010-07-15",
              "overview": "A heist on the subconscious.",
              "poster_path": "/poster123.jpg"
            }
          ]
        }
        """;
        var provider = NewProvider(new StubHandler(body), new LookupCacheMockStorage());

        var results = await provider.SearchAsync("inception");

        var hit = Assert.Single(results);
        Assert.Equal("tmdb", hit.Provider);
        Assert.Equal("27205", hit.ProviderKey);
        Assert.Equal("Inception", hit.Title);
        Assert.Equal("Inception", hit.OriginalTitle);
        Assert.Equal(2010, hit.Year);
        Assert.Equal("A heist on the subconscious.", hit.Description);
        Assert.Equal("https://image.tmdb.org/t/p/w342/poster123.jpg", hit.ImageUrl);
        Assert.Null(hit.Director);
        Assert.Null(hit.RuntimeMinutes);
    }

    [Fact]
    public async Task SearchAsync_WithMissingPoster_LeavesImageUrlNull()
    {
        var provider = NewProvider(new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "1999-01-01" } ] }
            """), new LookupCacheMockStorage());

        var results = await provider.SearchAsync("x");

        Assert.Null(results[0].ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_WithUnparseableReleaseDate_LeavesYearNull()
    {
        var provider = NewProvider(new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "" } ] }
            """), new LookupCacheMockStorage());

        var results = await provider.SearchAsync("x");

        Assert.Null(results[0].Year);
    }

    [Fact]
    public async Task SearchAsync_RepeatedQuery_HitsCacheOnSecondCall()
    {
        var handler = new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "1999-01-01" } ] }
            """);
        var storage = new LookupCacheMockStorage();
        var provider = NewProvider(handler, storage);

        var first = await provider.SearchAsync("x");
        var second = await provider.SearchAsync("X"); // case-insensitive cache key

        Assert.Single(first);
        Assert.Single(second);
        Assert.Single(handler.RequestedUrls); // second call served from cache
    }

    [Fact]
    public async Task SearchAsync_ForwardsConfiguredTtlOnWrite()
    {
        var expectedTtl = TimeSpan.FromMinutes(17);
        // TTL is now a write-time contract owned by the cache; verify the
        // search write forwards the configured metadata TTL. Real expiry is
        // covered by DistributedCacheAdapterTests.
        var handler = new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "1999-01-01" } ] }
            """);
        var storage = new LookupCacheMockStorage();
        var provider = NewProvider(handler, storage, new MetadataLookupOptions
        {
            CacheTtl = expectedTtl,
            Tmdb = new TmdbOptions { ApiKey = "key-xyz" },
        });

        await provider.SearchAsync("x");

        Assert.NotEmpty(storage.Writes);
        Assert.All(storage.Writes, w => Assert.Equal(expectedTtl, w.Ttl));
    }

    [Fact]
    public async Task SearchAsync_OnUpstreamFailure_ReturnsEmpty_AndDoesNotCache()
    {
        var handler = new StubHandler("error", HttpStatusCode.InternalServerError);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        var results = await provider.SearchAsync("x");
        Assert.Empty(results);

        // A subsequent call should attempt the network again rather than
        // returning an empty cached value.
        await provider.SearchAsync("x");
        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    // ---------- GetByIdAsync ----------

    private const string DetailJson = """
        {
          "id": 27205,
          "title": "Inception",
          "original_title": "Inception",
          "release_date": "2010-07-15",
          "runtime": 148,
          "overview": "A heist on the subconscious.",
          "poster_path": "/poster123.jpg",
          "credits": {
            "crew": [
              { "job": "Director of Photography", "name": "Wally Pfister" },
              { "job": "Director", "name": "Christopher Nolan" },
              { "job": "Editor", "name": "Lee Smith" }
            ]
          }
        }
        """;

    [Fact]
    public async Task GetByIdAsync_WithoutApiKey_ShortCircuitsToNull_AndDoesNotCallTmdb()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage(), new MetadataLookupOptions());

        var result = await provider.GetByIdAsync("27205");

        Assert.Null(result);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByIdAsync_WithBlankProviderKey_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Null(await provider.GetByIdAsync(""));
        Assert.Null(await provider.GetByIdAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByIdAsync_HitsTmdbWithAppendCredits_AndApiKey()
    {
        var handler = new StubHandler(DetailJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        await provider.GetByIdAsync("27205");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("movie/27205", url);
        Assert.Contains("api_key=key-xyz", url);
        Assert.Contains("append_to_response=credits", url);
    }

    [Fact]
    public async Task GetByIdAsync_MapsDetailIncludingDirectorAndRuntime()
    {
        var provider = NewProvider(new StubHandler(DetailJson), new LookupCacheMockStorage());

        var result = await provider.GetByIdAsync("27205");

        Assert.NotNull(result);
        Assert.Equal("tmdb", result!.Provider);
        Assert.Equal("27205", result.ProviderKey);
        Assert.Equal("Inception", result.Title);
        Assert.Equal("Inception", result.OriginalTitle);
        Assert.Equal(2010, result.Year);
        Assert.Equal(148, result.RuntimeMinutes);
        Assert.Equal("Christopher Nolan", result.Director);
        Assert.Equal("A heist on the subconscious.", result.Description);
        Assert.Equal("https://image.tmdb.org/t/p/w342/poster123.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task GetByIdAsync_WithMultipleDirectors_JoinsThemWithAmpersand()
    {
        const string body = """
            {
              "id": 1,
              "title": "X",
              "release_date": "1999-01-01",
              "credits": {
                "crew": [
                  { "job": "Director", "name": "Joel Coen" },
                  { "job": "Director", "name": "Ethan Coen" }
                ]
              }
            }
            """;
        var provider = NewProvider(new StubHandler(body), new LookupCacheMockStorage());

        var result = await provider.GetByIdAsync("1");

        Assert.Equal("Joel Coen & Ethan Coen", result!.Director);
    }

    [Fact]
    public async Task GetByIdAsync_WithoutDirectorInCrew_ReturnsNullDirector()
    {
        const string body = """
            {
              "id": 1,
              "title": "X",
              "release_date": "1999-01-01",
              "credits": { "crew": [{ "job": "Editor", "name": "Some Editor" }] }
            }
            """;
        var provider = NewProvider(new StubHandler(body), new LookupCacheMockStorage());

        var result = await provider.GetByIdAsync("1");

        Assert.Null(result!.Director);
    }

    [Fact]
    public async Task GetByIdAsync_With404FromTmdb_ReturnsNullWithoutCaching()
    {
        var handler = new StubHandler("not found", HttpStatusCode.NotFound);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Null(await provider.GetByIdAsync("999999"));

        // Re-issue: should hit TMDB again rather than serve a cached null.
        await provider.GetByIdAsync("999999");
        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task GetByIdAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(DetailJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewProvider(handler, storage);
        var p2 = NewProvider(handler, storage); // shared mock storage, fresh provider instance

        var first = await p1.GetByIdAsync("27205");
        var second = await p2.GetByIdAsync("27205");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Director, second!.Director);
        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByIdAsync_AndSearchAsync_UseSeparateCacheNamespaces()
    {
        // A search for "27205" must not satisfy a by-id lookup for the
        // movie whose TMDB id happens to be "27205".
        var searchHandler = new StubHandler("""
            { "results": [ { "id": 99, "title": "Different Movie", "release_date": "1990-01-01" } ] }
            """);
        var storage = new LookupCacheMockStorage();
        var searchProvider = NewProvider(searchHandler, storage);
        await searchProvider.SearchAsync("27205");
        Assert.Single(searchHandler.RequestedUrls);

        var idHandler = new StubHandler(DetailJson);
        var idProvider = NewProvider(idHandler, storage);
        var byId = await idProvider.GetByIdAsync("27205");

        Assert.Equal("Inception", byId!.Title);
        Assert.Single(idHandler.RequestedUrls); // not satisfied from the search cache
    }

    // ---------- GetByImdbIdAsync ----------

    [Fact]
    public async Task GetByImdbIdAsync_WithoutApiKey_ShortCircuitsToNull()
    {
        var handler = new RoutingStubHandler();
        var provider = NewProvider(handler, new LookupCacheMockStorage(), new MetadataLookupOptions());

        Assert.Null(await provider.GetByImdbIdAsync("tt1375666"));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByImdbIdAsync_WithBlankImdbId_ReturnsNullWithoutCalling()
    {
        var handler = new RoutingStubHandler();
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Null(await provider.GetByImdbIdAsync(""));
        Assert.Null(await provider.GetByImdbIdAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByImdbIdAsync_HitsFindEndpoint_WithExternalSourceImdbId()
    {
        var handler = new RoutingStubHandler()
            .When("find/", """{ "movie_results": [ { "id": 27205, "title": "Inception", "release_date": "2010-07-15" } ] }""")
            .When("movie/", DetailJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        await provider.GetByImdbIdAsync("tt1375666");

        var findUrl = handler.RequestedUrls.First(u => u.Contains("find/"));
        Assert.Contains("find/tt1375666", findUrl);
        Assert.Contains("external_source=imdb_id", findUrl);
        Assert.Contains("api_key=key-xyz", findUrl);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ResolvesToFullDetail_ViaChainedGetById()
    {
        var handler = new RoutingStubHandler()
            .When("find/", """{ "movie_results": [ { "id": 27205, "title": "Inception", "release_date": "2010-07-15" } ] }""")
            .When("movie/", DetailJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        var result = await provider.GetByImdbIdAsync("tt1375666");

        Assert.NotNull(result);
        Assert.Equal("27205", result!.ProviderKey);
        Assert.Equal("Inception", result.Title);
        Assert.Equal("Christopher Nolan", result.Director);
        Assert.Equal(148, result.RuntimeMinutes);

        // Two upstream calls: /find then /movie/27205
        Assert.Equal(2, handler.RequestedUrls.Count);
        Assert.Contains(handler.RequestedUrls, u => u.Contains("find/"));
        Assert.Contains(handler.RequestedUrls, u => u.Contains("movie/27205"));
    }

    [Fact]
    public async Task GetByImdbIdAsync_WithEmptyMovieResults_ReturnsNullAndDoesNotResolve()
    {
        var handler = new RoutingStubHandler()
            .When("find/", """{ "movie_results": [] }""");
        var provider = NewProvider(handler, new LookupCacheMockStorage());

        Assert.Null(await provider.GetByImdbIdAsync("tt9999999"));
        Assert.Single(handler.RequestedUrls); // no chained /movie/{id} call
    }

    [Fact]
    public async Task GetByImdbIdAsync_RepeatedCallsServeFromCache()
    {
        var handler = new RoutingStubHandler()
            .When("find/", """{ "movie_results": [ { "id": 27205, "title": "Inception", "release_date": "2010-07-15" } ] }""")
            .When("movie/", DetailJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewProvider(handler, storage);
        var p2 = NewProvider(handler, storage);

        var first = await p1.GetByImdbIdAsync("tt1375666");
        var second = await p2.GetByImdbIdAsync("tt1375666");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.ProviderKey, second!.ProviderKey);
        // Initial call: /find + /movie. Second call served from the imdb
        // cache namespace -- no additional upstream traffic.
        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ThenGetByIdAsync_ReusesTheChainedDetailCache()
    {
        // After resolving an IMDB id, the subsequent TMDB-id lookup for the
        // same movie is free -- the chained GetByIdAsync call already wrote
        // an "id:27205" cache entry.
        var handler = new RoutingStubHandler()
            .When("find/", """{ "movie_results": [ { "id": 27205, "title": "Inception", "release_date": "2010-07-15" } ] }""")
            .When("movie/", DetailJson);
        var storage = new LookupCacheMockStorage();
        var provider = NewProvider(handler, storage);

        await provider.GetByImdbIdAsync("tt1375666");
        Assert.Equal(2, handler.RequestedUrls.Count);

        var byId = await provider.GetByIdAsync("27205");
        Assert.NotNull(byId);
        Assert.Equal(2, handler.RequestedUrls.Count); // still 2 -- served from cache
    }

    // ---------- SearchByBarcodeAsync ----------

    [Fact]
    public async Task SearchByBarcodeAsync_NotConfigured_ShortCircuitsAndDoesNotHitUpc()
    {
        var upc = FakeUpcClient.Returning("Inception");
        var provider = NewProvider(new StubHandler(BarcodeSearchJson), new LookupCacheMockStorage(), new MetadataLookupOptions(), upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("0883929473076"));
        Assert.Empty(upc.RequestedBarcodes);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_BlankBarcode_ReturnsEmptyWithoutCalling()
    {
        var upc = FakeUpcClient.Returning("Inception");
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage(), upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("  "));
        Assert.Empty(upc.RequestedBarcodes);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_UpcMiss_ReturnsEmptyWithoutTitleSearch()
    {
        // UPCitemdb didn't recognise the code; we mustn't fall back to
        // searching TMDB with an empty string.
        var upc = FakeUpcClient.NotRecognised();
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new LookupCacheMockStorage(), upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("0000000000000"));
        Assert.Single(upc.RequestedBarcodes);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_DispatchesToTmdbTitleSearch_WithUpcTitle()
    {
        var upc = FakeUpcClient.Returning("Inception");
        var handler = new StubHandler(BarcodeSearchJson);
        var provider = NewProvider(handler, new LookupCacheMockStorage(), upc: upc);

        var hits = await provider.SearchByBarcodeAsync("0883929473076");

        Assert.Single(hits);
        Assert.Equal("Inception", hits[0].Title);
        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("search/movie", url);
        Assert.Contains("query=Inception", url);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_RepeatedCallsServeFromCache()
    {
        var upc = FakeUpcClient.Returning("Inception");
        var handler = new StubHandler(BarcodeSearchJson);
        var storage = new LookupCacheMockStorage();
        var p1 = NewProvider(handler, storage, upc: upc);
        var p2 = NewProvider(handler, storage, upc: upc); // shared mock storage

        await p1.SearchByBarcodeAsync("0883929473076");
        await p2.SearchByBarcodeAsync("0883929473076");

        // Single TMDB hit across both invocations -- the second call is
        // satisfied entirely by the barcode-namespaced cache entry.
        Assert.Single(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_ForwardsConfiguredTtlOnWrite()
    {
        var upc = FakeUpcClient.Returning("Inception");
        var handler = new StubHandler(BarcodeSearchJson);
        var storage = new LookupCacheMockStorage();
        var options = new MetadataLookupOptions
        {
            Tmdb = new TmdbOptions { ApiKey = "key-xyz" },
        };
        var provider = NewProvider(handler, storage, options, upc);

        await provider.SearchByBarcodeAsync("0883929473076");

        Assert.NotEmpty(storage.Writes);
        Assert.All(storage.Writes, w => Assert.Equal(options.CacheTtl, w.Ttl));
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
            // AbsoluteUri keeps percent-encoding intact; ToString unescapes %20 -> space.
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>
    /// Stub that picks a response body by matching a substring of the
    /// request URL. Used by the IMDB-lookup tests because that flow makes
    /// two upstream calls (/find and /movie/{id}) in a single
    /// GetByImdbIdAsync call and needs different payloads per URL.
    /// </summary>
    private sealed class RoutingStubHandler : HttpMessageHandler
    {
        private readonly List<(string urlContains, string body, HttpStatusCode status)> _routes = new();
        public List<string> RequestedUrls { get; } = new();

        public RoutingStubHandler When(string urlContains, string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _routes.Add((urlContains, body, status));
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            RequestedUrls.Add(url);
            foreach (var (contains, body, status) in _routes)
            {
                if (url.Contains(contains, StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(status)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    });
                }
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private TmdbMovieProvider NewProvider(RoutingStubHandler handler, LookupCacheMockStorage storage, MetadataLookupOptions? overrideOptions = null, FakeUpcClient? upc = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            Tmdb = new TmdbOptions { ApiKey = "key-xyz" },
        };
        var expectedTtl = options.CacheTtl;
        storage.SetupStorage<List<MovieLookupResult>>(expectedTtl);
        storage.SetupStorage<MovieLookupResult>(expectedTtl);
        return new TmdbMovieProvider(http, upc ?? FakeUpcClient.NotRecognised(), storage.Mock.Object, Options.Create(options), NullLogger<TmdbMovieProvider>.Instance);
    }
}
