using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class LookupCacheTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _options;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public LookupCacheTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_options);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private LookupCache NewCache() => new(new CollectifyDbContext(_options), _clock);

    private record Sample(string Title, int Year);

    [Fact]
    public async Task Get_BeforeSet_ReturnsDefault()
    {
        var cache = NewCache();
        var result = await cache.GetAsync<Sample>("tmdb", "550", TimeSpan.FromDays(30));
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_WithinTtl_ReturnsCachedValue()
    {
        await NewCache().SetAsync("tmdb", "550", new Sample("Fight Club", 1999));
        _clock.Advance(TimeSpan.FromHours(6));

        var result = await NewCache().GetAsync<Sample>("tmdb", "550", TimeSpan.FromDays(30));

        Assert.NotNull(result);
        Assert.Equal("Fight Club", result!.Title);
        Assert.Equal(1999, result.Year);
    }

    [Fact]
    public async Task Get_ExpiredEntry_ReturnsDefault()
    {
        await NewCache().SetAsync("tmdb", "550", new Sample("Fight Club", 1999));
        _clock.Advance(TimeSpan.FromDays(31));

        var result = await NewCache().GetAsync<Sample>("tmdb", "550", TimeSpan.FromDays(30));

        Assert.Null(result);
    }

    [Fact]
    public async Task Set_OnExistingKey_OverwritesAndRefreshesFetchedAt()
    {
        await NewCache().SetAsync("tmdb", "550", new Sample("Old", 1900));
        _clock.Advance(TimeSpan.FromDays(10));
        await NewCache().SetAsync("tmdb", "550", new Sample("New", 2000));

        // Single row with the latest payload.
        using var ctx = new CollectifyDbContext(_options);
        var rows = await ctx.LookupCache.AsNoTracking()
            .Where(e => e.Provider == "tmdb" && e.Key == "550")
            .ToListAsync();
        Assert.Single(rows);
        Assert.Contains("\"title\":\"New\"", rows[0].JsonResponse);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, rows[0].FetchedAt);
    }

    [Fact]
    public async Task ProviderAndKeyTogetherIdentifyEntries()
    {
        await NewCache().SetAsync("tmdb", "550", new Sample("Fight Club", 1999));
        await NewCache().SetAsync("imdb", "550", new Sample("Other thing", 2010));

        var tmdb = await NewCache().GetAsync<Sample>("tmdb", "550", TimeSpan.FromDays(30));
        var imdb = await NewCache().GetAsync<Sample>("imdb", "550", TimeSpan.FromDays(30));

        Assert.Equal("Fight Club", tmdb!.Title);
        Assert.Equal("Other thing", imdb!.Title);
    }

    [Fact]
    public async Task Set_PersistsThroughDbContextLifetime()
    {
        await NewCache().SetAsync("tmdb", "550", new Sample("Fight Club", 1999));

        // Verify a fresh context (separate scope) sees the row.
        using var ctx = new CollectifyDbContext(_options);
        var entry = await ctx.LookupCache.AsNoTracking().FirstAsync();
        Assert.Equal("tmdb", entry.Provider);
        Assert.Equal("550", entry.Key);
        Assert.IsType<LookupCacheEntry>(entry);
    }
}
