using System.Net;
using System.Text;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Igdb;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class IgdbGameProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _dbOptions;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public IgdbGameProviderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_dbOptions);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private IgdbGameProvider NewProvider(HttpMessageHandler handler, FakeAuth? auth = null, MetadataLookupOptions? overrideOptions = null, FakeUpcClient? upc = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.igdb.com/v4/") };
        var cache = new LookupCache(new CollectifyDbContext(_dbOptions), _clock);
        var options = overrideOptions ?? new MetadataLookupOptions
        {
            Igdb = new IgdbOptions { TwitchClientId = "client", TwitchClientSecret = "secret" },
        };
        return new IgdbGameProvider(http, auth ?? new FakeAuth("client", "tok"), upc ?? FakeUpcClient.NotRecognised(), cache, Options.Create(options), NullLogger<IgdbGameProvider>.Instance);
    }

    private const string SingleGameJson = """
        [
          {
            "id": 1942,
            "name": "The Witcher 3: Wild Hunt",
            "first_release_date": 1431993600,
            "summary": "A monster hunter searches for his adopted daughter.",
            "cover": { "image_id": "co1wyy" },
            "involved_companies": [
              { "company": { "name": "CD Projekt Red" }, "developer": true,  "publisher": false },
              { "company": { "name": "Warner Bros" },    "developer": false, "publisher": true  }
            ],
            "platforms": [ { "name": "PC" }, { "name": "PlayStation 4" } ],
            "genres":    [ { "name": "RPG" }, { "name": "Adventure" } ]
          }
        ]
        """;

    private async Task SeedRawCacheRowAsync(string provider, string key, string json)
    {
        using var ctx = new CollectifyDbContext(_dbOptions);
        ctx.LookupCache.Add(new LookupCacheEntry
        {
            Provider = provider,
            Key = key,
            JsonResponse = json,
            FetchedAt = _clock.GetUtcNow().UtcDateTime,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public void IsConfigured_ReflectsBothCredentials()
    {
        var both = NewProvider(new StubHandler("[]"));
        var missingSecret = NewProvider(new StubHandler("[]"), overrideOptions: new MetadataLookupOptions
        {
            Igdb = new IgdbOptions { TwitchClientId = "client", TwitchClientSecret = null },
        });
        var missingId = NewProvider(new StubHandler("[]"), overrideOptions: new MetadataLookupOptions
        {
            Igdb = new IgdbOptions { TwitchClientId = "  ", TwitchClientSecret = "secret" },
        });

        Assert.True(both.IsConfigured);
        Assert.False(missingSecret.IsConfigured);
        Assert.False(missingId.IsConfigured);
    }

    [Fact]
    public async Task SearchAsync_WithoutCredentials_ShortCircuitsToEmpty_AndDoesNotCallIgdb()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, overrideOptions: new MetadataLookupOptions());

        Assert.Empty(await provider.SearchAsync("witcher"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_BlankQuery_ReturnsEmptyWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler);

        Assert.Empty(await provider.SearchAsync("   "));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_PostsApicalypseBodyWithSearchAndFields_AndAuthHeaders()
    {
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.SearchAsync("witcher");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://api.igdb.com/v4/games", req.Url);
        Assert.Contains("search \"witcher\";", req.Body);
        Assert.Contains("fields name,first_release_date,cover.image_id", req.Body);
        Assert.Contains("limit 10;", req.Body);
        Assert.Equal("client", req.ClientId);
        Assert.Equal("Bearer tok", req.Authorization);
    }

    [Fact]
    public async Task SearchAsync_MapsGame_IncludingDeveloperPublisherCoverPlatformAndYear()
    {
        var provider = NewProvider(new StubHandler(SingleGameJson));

        var hit = Assert.Single(await provider.SearchAsync("witcher"));

        Assert.Equal("igdb", hit.Provider);
        Assert.Equal("1942", hit.ProviderKey);
        Assert.Equal("The Witcher 3: Wild Hunt", hit.Title);
        Assert.Equal(2015, hit.Year);
        Assert.Equal(GamePlatform.Pc, hit.Platform);
        Assert.Equal("CD Projekt Red", hit.Developer);
        Assert.Equal("Warner Bros", hit.Publisher);
        Assert.Equal("RPG, Adventure", hit.Genres);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co1wyy.jpg", hit.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_GameWithoutCover_ReturnsNullImageUrl()
    {
        const string json = """
            [ { "id": 1, "name": "Indie", "platforms": [ { "name": "PC" } ] } ]
            """;
        var hit = (await NewProvider(new StubHandler(json)).SearchAsync("indie")).Single();

        Assert.Null(hit.ImageUrl);
        Assert.Null(hit.Year);
        Assert.Null(hit.Developer);
        Assert.Null(hit.Publisher);
    }

    [Fact]
    public async Task SearchByPlatformAsync_Pc_AppendsSourcePlatformWhereClause()
    {
        // The Witcher 3 case from production: IGDB's fuzzy search ranks console
        // re-releases above the plain PC SKU, so a top-10 all-platform window
        // never surfaces it. The platform-scoped search must filter AT THE
        // SOURCE with `where platforms = (6,3)` (PC = 6, plus Linux = 3 since
        // Linux folds into Pc, #102) so IGDB runs the fuzzy search within the
        // PC family and the PC release appears.
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.SearchByPlatformAsync("The Witcher 3: Wild Hunt", GamePlatform.Pc);

        var req = Assert.Single(handler.Requests);
        Assert.Contains("search \"The Witcher 3: Wild Hunt\";", req.Body);
        Assert.Contains("limit 10;", req.Body);
        Assert.Contains(" where platforms = (6,3);", req.Body);
    }

    [Theory]
    [InlineData(GamePlatform.Pc, "(6,3)")] // PC = Windows (6) + Linux (3), #102
    [InlineData(GamePlatform.Mac, "(14)")]
    [InlineData(GamePlatform.Ps4, "(48)")]
    [InlineData(GamePlatform.Ps5, "(167)")]
    [InlineData(GamePlatform.XboxSeriesXS, "(169)")]
    [InlineData(GamePlatform.Switch, "(130)")]
    [InlineData(GamePlatform.XboxOne, "(49)")]
    public async Task SearchByPlatformAsync_KnownPlatform_AppendsIdClause(GamePlatform platform, string expectedClause)
    {
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.SearchByPlatformAsync("witcher", platform);

        var req = Assert.Single(handler.Requests);
        Assert.Contains($" where platforms = {expectedClause};", req.Body);
    }

    [Theory]
    [InlineData(GamePlatform.Other)]
    [InlineData(GamePlatform.Mobile)] // Android/iOS split — no single id
    public async Task SearchByPlatformAsync_NoCanonicalId_DoesNotAppendWhereClause_AndFiltersInMemory(GamePlatform platform)
    {
        // IGDB returns a PC + PS4 entry; neither maps to Mobile/Other,
        // so the in-memory IsOn filter must leave the result set empty.
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        var results = await provider.SearchByPlatformAsync("witcher", platform);

        Assert.Empty(results);
        var req = Assert.Single(handler.Requests);
        Assert.DoesNotContain(" where platforms =", req.Body);
    }

    [Fact]
    public async Task SearchByPlatformAsync_UsesPlatformScopedCacheKey_NotSharedWithUnscoped()
    {
        // The platform-scoped cache key must be distinct from the unscoped one:
        // an unscoped "v3:search:witcher" entry must NOT satisfy a PC-scoped
        // "v3:search:witcher|Pc" request (or the platform filter would be wrong).
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler);
        var p2 = NewProvider(handler); // shared sqlite cache

        await p1.SearchAsync("witcher");                            // unscoped: key "v3:search:witcher"
        await p2.SearchByPlatformAsync("witcher", GamePlatform.Pc); // PC: key "v3:search:witcher|Pc"

        // Two distinct cache keys -> two upstream calls, not a cache hit.
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(" where platforms =", handler.Requests[0].Body); // unscoped first call
        Assert.Contains(" where platforms = (6,3);", handler.Requests[1].Body);  // scoped second call (PC = 6 + Linux 3)
    }

    [Fact]
    public async Task SearchByPlatformAsync_RepeatedCall_ServesFromCache()
    {
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.SearchByPlatformAsync("witcher", GamePlatform.Pc);
        await provider.SearchByPlatformAsync("witcher", GamePlatform.Pc); // same scoped key

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_FirstPlatformUnmapped_FallsBackToNextRecognised()
    {
        // IGDB sometimes lists a region-specific or obscure platform
        // first. Map should skip it and pick the first one we can
        // canonicalise, not silently leave Platform null.
        const string json = """
            [ {
                "id": 1, "name": "Spelunky",
                "platforms": [
                  { "name": "Some Obscure Platform" },
                  { "name": "PC" }
                ]
            } ]
            """;
        var hit = (await NewProvider(new StubHandler(json)).SearchAsync("spelunky")).Single();
        Assert.Equal(GamePlatform.Pc, hit.Platform);
    }

    [Fact]
    public async Task SearchAsync_AllPlatformsUnmapped_ReturnsNullPlatform()
    {
        const string json = """
            [ { "id": 1, "name": "X", "platforms": [ { "name": "3DO" }, { "name": "Atari Jaguar" } ] } ]
            """;
        var hit = (await NewProvider(new StubHandler(json)).SearchAsync("x")).Single();
        // Null (not Other) so the form's dropdown stays unset and the
        // user notices they need to pick one.
        Assert.Null(hit.Platform);
    }

    [Fact]
    public async Task SearchAsync_EscapesQuotesInQuery()
    {
        var handler = new StubHandler("[]");
        var provider = NewProvider(handler);

        await provider.SearchAsync("foo \"bar\" baz");

        var req = Assert.Single(handler.Requests);
        Assert.Contains("search \"foo \\\"bar\\\" baz\";", req.Body);
    }

    [Fact]
    public async Task SearchAsync_RepeatedQuery_ServesFromCache()
    {
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler);
        var p2 = NewProvider(handler); // shared sqlite cache

        await p1.SearchAsync("witcher");
        await p2.SearchAsync("WITCHER"); // case-insensitive cache key

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_WithStaleIncompatibleCachedResult_TreatsCacheAsMissAndRefreshes()
    {
        // Seed a cache row under the v2 schema key. The provider now reads
        // v3:search:..., so this v2 row is stale-schema and must be treated as
        // a miss -> the provider hits IGDB afresh and returns the real row.
        await SeedRawCacheRowAsync(
            "igdb",
            "v2:search:witcher",
            "[{\"provider\":\"igdb\",\"providerKey\":\"old\",\"title\":\"Old\",\"platform\":\"Windows 95\"}]");
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        var hit = Assert.Single(await provider.SearchAsync("witcher"));

        Assert.Equal("The Witcher 3: Wild Hunt", hit.Title);
        Assert.Equal(GamePlatform.Pc, hit.Platform);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_UnversionedStaleCacheRow_IsNotServed_AndRefreshes()
    {
        // Regression for the stale-cache bug: a cached row written before the
        // `Platforms` DTO field existed (key "search:witcher", no v-prefix)
        // must NOT be served. The versioned key the current code reads
        // (v3:search:witcher) won't match it, so the provider hits IGDB afresh
        // and returns fully-shaped results — this is what fixes the prod case
        // where every result came back `platforms: []`.
        await SeedRawCacheRowAsync(
            "igdb",
            "search:witcher", // OLD schema key (unversioned)
            "[{\"provider\":\"igdb\",\"providerKey\":\"old\",\"title\":\"Old\",\"platform\":\"Windows 95\"}]");
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        var hit = Assert.Single(await provider.SearchAsync("witcher"));

        Assert.Equal("The Witcher 3: Wild Hunt", hit.Title);
        Assert.Equal(GamePlatform.Pc, hit.Platform);
        // The stale unversioned row was ignored -> one fresh upstream call.
        Assert.Single(handler.Requests);
        // And the fresh result carries the full platform set (would've been []).
        Assert.Contains(GamePlatform.Pc, hit.Platforms);
    }

    [Fact]
    public async Task SearchAsync_OnUpstreamFailure_ReturnsEmptyAndDoesNotCache()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var provider = NewProvider(handler);

        Assert.Empty(await provider.SearchAsync("x"));
        await provider.SearchAsync("x"); // should retry, not serve a cached []
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetByIdAsync_PostsApicalypseWhereClause()
    {
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler);

        await provider.GetByIdAsync("1942");

        var req = Assert.Single(handler.Requests);
        Assert.Contains("where id = 1942;", req.Body);
        Assert.Contains("limit 1;", req.Body);
        Assert.DoesNotContain("search ", req.Body);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyResponse_ReturnsNullWithoutCaching()
    {
        var handler = new StubHandler("[]");
        var provider = NewProvider(handler);

        Assert.Null(await provider.GetByIdAsync("1942"));
        await provider.GetByIdAsync("1942");
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonNumericKey_ReturnsNullWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler);

        Assert.Null(await provider.GetByIdAsync("not-a-number"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetByIdAsync_RepeatedCallsServeFromCache()
    {
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler);
        var p2 = NewProvider(handler);

        var first = await p1.GetByIdAsync("1942");
        var second = await p2.GetByIdAsync("1942");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("The Witcher 3: Wild Hunt", second!.Title);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetByIdAsync_AndSearchAsync_UseSeparateCacheNamespaces()
    {
        // A search whose query happens to equal an IGDB id must not satisfy
        // a by-id lookup for that same id.
        var searchHandler = new StubHandler("""
            [ { "id": 9999, "name": "Different", "platforms": [ { "name": "PC" } ] } ]
            """);
        await NewProvider(searchHandler).SearchAsync("1942");
        Assert.Single(searchHandler.Requests);

        var idHandler = new StubHandler(SingleGameJson);
        var byId = await NewProvider(idHandler).GetByIdAsync("1942");

        Assert.Equal("The Witcher 3: Wild Hunt", byId!.Title);
        Assert.Single(idHandler.Requests);
    }

    [Fact]
    public async Task PostGames_On401_RefreshesTokenAndRetriesOnce()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.Unauthorized, "{}"),
            (HttpStatusCode.OK, SingleGameJson));
        var auth = new FakeAuth("client", "stale-tok") { OnRefresh = "fresh-tok" };
        var provider = NewProvider(handler, auth);

        var hits = await provider.SearchAsync("witcher");

        Assert.Single(hits);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer stale-tok", handler.Requests[0].Authorization);
        Assert.Equal("Bearer fresh-tok", handler.Requests[1].Authorization);
        Assert.Equal(1, auth.RefreshCount);
    }

    [Fact]
    public async Task PostGames_OnDouble401_GivesUpAndDoesNotLoop()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.Unauthorized, "{}"),
            (HttpStatusCode.Unauthorized, "{}"));
        var auth = new FakeAuth("client", "bad");
        var provider = NewProvider(handler, auth);

        Assert.Empty(await provider.SearchAsync("witcher"));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task PostGames_WhenAuthReturnsNoToken_ShortCircuitsWithoutCalling()
    {
        var handler = new StubHandler("never called");
        var auth = new FakeAuth("client", token: null);
        var provider = NewProvider(handler, auth);

        Assert.Empty(await provider.SearchAsync("witcher"));
        Assert.Empty(handler.Requests);
    }

    // ---------- SearchByBarcodeAsync ----------

    [Fact]
    public async Task SearchByBarcodeAsync_NotConfigured_ShortCircuitsAndDoesNotHitUpc()
    {
        var upc = FakeUpcClient.Returning("Hades");
        var provider = NewProvider(new StubHandler(SingleGameJson), overrideOptions: new MetadataLookupOptions(), upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("0123456789012"));
        Assert.Empty(upc.RequestedBarcodes);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_BlankBarcode_ReturnsEmptyWithoutCalling()
    {
        var upc = FakeUpcClient.Returning("Hades");
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("   "));
        Assert.Empty(upc.RequestedBarcodes);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_UpcMiss_ReturnsEmptyWithoutTitleSearch()
    {
        var upc = FakeUpcClient.NotRecognised();
        var handler = new StubHandler("never called");
        var provider = NewProvider(handler, upc: upc);

        Assert.Empty(await provider.SearchByBarcodeAsync("0000000000000"));
        Assert.Single(upc.RequestedBarcodes);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_DispatchesToIgdbTitleSearch_WithUpcTitle()
    {
        var upc = FakeUpcClient.Returning("The Witcher 3");
        var handler = new StubHandler(SingleGameJson);
        var provider = NewProvider(handler, upc: upc);

        var hits = await provider.SearchByBarcodeAsync("0883929473076");

        Assert.Single(hits);
        var req = Assert.Single(handler.Requests);
        // The title surfaced by UPC drives the Apicalypse search clause.
        Assert.Contains("search \"The Witcher 3\";", req.Body);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_RepeatedCallsServeFromCache()
    {
        var upc = FakeUpcClient.Returning("The Witcher 3");
        var handler = new StubHandler(SingleGameJson);
        var p1 = NewProvider(handler, upc: upc);
        var p2 = NewProvider(handler, upc: upc); // shared sqlite cache

        await p1.SearchByBarcodeAsync("0883929473076");
        await p2.SearchByBarcodeAsync("0883929473076");

        Assert.Single(handler.Requests);
    }

    private sealed class FakeAuth : IIgdbAuth
    {
        private string? _token;
        public int RefreshCount { get; private set; }
        public string? OnRefresh { get; init; }
        public string? ClientId { get; }

        public FakeAuth(string clientId, string? token)
        {
            ClientId = clientId;
            _token = token;
        }

        public Task<string?> GetTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
        {
            if (forceRefresh)
            {
                RefreshCount++;
                if (OnRefresh is not null) _token = OnRefresh;
            }
            return Task.FromResult(_token);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Url, string Body, string? ClientId, string? Authorization);

    private static async Task<CapturedRequest> CaptureAsync(HttpRequestMessage request)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
        request.Headers.TryGetValues("Client-ID", out var clientIds);
        return new CapturedRequest(
            request.Method,
            request.RequestUri!.AbsoluteUri,
            body,
            clientIds?.FirstOrDefault(),
            request.Headers.Authorization?.ToString());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public List<CapturedRequest> Requests { get; } = new();

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await CaptureAsync(request));
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _queue;
        public List<CapturedRequest> Requests { get; } = new();

        public SequenceHandler(params (HttpStatusCode Status, string Body)[] items)
        {
            _queue = new Queue<(HttpStatusCode, string)>(items);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await CaptureAsync(request));
            var (status, body) = _queue.Count > 0 ? _queue.Dequeue() : (HttpStatusCode.OK, "[]");
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
