using System.Net;
using System.Text;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Tmdb;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class TmdbMovieProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _dbOptions;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public TmdbMovieProviderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_dbOptions);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private TmdbMovieProvider NewProvider(StubHandler handler, MetadataLookupOptions? overrideOptions = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var cache = new LookupCache(new CollectifyDbContext(_dbOptions), _clock);
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            Tmdb = new TmdbOptions { ApiKey = "key-xyz" },
        };
        return new TmdbMovieProvider(http, cache, Options.Create(options), NullLogger<TmdbMovieProvider>.Instance);
    }

    [Fact]
    public async Task IsConfigured_ReflectsApiKeyPresence()
    {
        var configured = NewProvider(new StubHandler("{ \"results\": [] }"));
        var unconfigured = NewProvider(new StubHandler("never called"), new MetadataLookupOptions());

        Assert.True(configured.IsConfigured);
        Assert.False(unconfigured.IsConfigured);
    }

    [Fact]
    public async Task SearchAsync_WithoutApiKey_ShortCircuitsToEmpty_AndDoesNotCallTmdb()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, new MetadataLookupOptions());

        var results = await provider.SearchAsync("inception");

        Assert.Empty(results);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_WithBlankQuery_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler);

        var results = await provider.SearchAsync("   ");

        Assert.Empty(results);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task SearchAsync_HitsTmdbWithUrlEncodedQuery_AndIncludesApiKey()
    {
        var handler = new StubHandler("{ \"results\": [] }");
        var provider = NewProvider(handler);

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
        var provider = NewProvider(new StubHandler(body));

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
            """));

        var results = await provider.SearchAsync("x");

        Assert.Null(results[0].ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_WithUnparseableReleaseDate_LeavesYearNull()
    {
        var provider = NewProvider(new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "" } ] }
            """));

        var results = await provider.SearchAsync("x");

        Assert.Null(results[0].Year);
    }

    [Fact]
    public async Task SearchAsync_RepeatedQuery_HitsCacheOnSecondCall()
    {
        var handler = new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "1999-01-01" } ] }
            """);
        var provider1 = NewProvider(handler);
        var provider2 = NewProvider(handler); // same cache (shared sqlite), fresh provider instance

        var first = await provider1.SearchAsync("x");
        var second = await provider2.SearchAsync("X"); // case-insensitive cache key

        Assert.Single(first);
        Assert.Single(second);
        Assert.Single(handler.RequestedUrls); // second call served from cache
    }

    [Fact]
    public async Task SearchAsync_AfterTtlExpires_RefreshesFromTmdb()
    {
        var handler = new StubHandler("""
            { "results": [ { "id": 1, "title": "X", "release_date": "1999-01-01" } ] }
            """);
        var provider = NewProvider(handler);

        await provider.SearchAsync("x");
        _clock.Advance(TimeSpan.FromDays(31));
        await provider.SearchAsync("x");

        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task SearchAsync_OnUpstreamFailure_ReturnsEmpty_AndDoesNotCache()
    {
        var handler = new StubHandler("error", HttpStatusCode.InternalServerError);
        var provider = NewProvider(handler);

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
        var provider = NewProvider(handler, new MetadataLookupOptions());

        var result = await provider.GetByIdAsync("27205");

        Assert.Null(result);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByIdAsync_WithBlankProviderKey_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler);

        Assert.Null(await provider.GetByIdAsync(""));
        Assert.Null(await provider.GetByIdAsync("   "));
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetByIdAsync_HitsTmdbWithAppendCredits_AndApiKey()
    {
        var handler = new StubHandler(DetailJson);
        var provider = NewProvider(handler);

        await provider.GetByIdAsync("27205");

        var url = Assert.Single(handler.RequestedUrls);
        Assert.Contains("movie/27205", url);
        Assert.Contains("api_key=key-xyz", url);
        Assert.Contains("append_to_response=credits", url);
    }

    [Fact]
    public async Task GetByIdAsync_MapsDetailIncludingDirectorAndRuntime()
    {
        var provider = NewProvider(new StubHandler(DetailJson));

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
        var provider = NewProvider(new StubHandler(body));

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
        var provider = NewProvider(new StubHandler(body));

        var result = await provider.GetByIdAsync("1");

        Assert.Null(result!.Director);
    }

    [Fact]
    public async Task GetByIdAsync_With404FromTmdb_ReturnsNullWithoutCaching()
    {
        var handler = new StubHandler("not found", HttpStatusCode.NotFound);
        var provider = NewProvider(handler);

        Assert.Null(await provider.GetByIdAsync("999999"));

        // Re-issue: should hit TMDB again rather than serve a cached null.
        await provider.GetByIdAsync("999999");
        Assert.Equal(2, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task GetByIdAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(DetailJson);
        var p1 = NewProvider(handler);
        var p2 = NewProvider(handler); // shared sqlite cache, fresh provider instance

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
        var searchProvider = NewProvider(searchHandler);
        await searchProvider.SearchAsync("27205");
        Assert.Single(searchHandler.RequestedUrls);

        var idHandler = new StubHandler(DetailJson);
        var idProvider = NewProvider(idHandler);
        var byId = await idProvider.GetByIdAsync("27205");

        Assert.Equal("Inception", byId!.Title);
        Assert.Single(idHandler.RequestedUrls); // not satisfied from the search cache
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
}
