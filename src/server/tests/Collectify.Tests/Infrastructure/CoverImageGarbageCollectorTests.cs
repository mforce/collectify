using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class CoverImageGarbageCollectorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _options;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    public CoverImageGarbageCollectorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_options);
        seed.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private CoverImageGarbageCollector NewGc() =>
        new(_clock, NullLogger<CoverImageGarbageCollector>.Instance);

    private static CoverImage Cover(string hash, DateTime addedAt) => new()
    {
        Hash = hash,
        ContentType = "image/jpeg",
        Bytes = [0xFF, 0xD8, 0xFF],
        AddedAt = addedAt,
    };

    [Fact]
    public async Task SweepAsync_DeletesUnreferencedCoversPastTheGraceWindow()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        await using (var db = new CollectifyDbContext(_options))
        {
            db.CoverImages.AddRange(
                Cover("aaaa1111aaaa1111", now.AddDays(-10)), // referenced by movie
                Cover("bbbb2222bbbb2222", now.AddDays(-10)), // referenced by album
                Cover("cccc3333cccc3333", now.AddDays(-10)), // referenced by game
                Cover("dddd4444dddd4444", now.AddDays(-10)) // orphan
            );
            db.Movies.Add(new Movie { OwnerId = "alice", Title = "Movie", ImagePath = "/covers/aaaa1111aaaa1111" });
            db.MusicAlbums.Add(new MusicAlbum { OwnerId = "alice", Title = "Album", ArtistName = "x", Format = MusicFormat.Cd, ImagePath = "/covers/bbbb2222bbbb2222" });
            db.Games.Add(new Game { OwnerId = "alice", Title = "Game", Platform = GamePlatform.Pc, ImagePath = "/covers/cccc3333cccc3333" });
            await db.SaveChangesAsync();
        }

        await using (var db = new CollectifyDbContext(_options))
        {
            var deleted = await NewGc().SweepAsync(db);
            Assert.Equal(1, deleted);
        }

        await using (var assert = new CollectifyDbContext(_options))
        {
            var hashes = assert.CoverImages.Select(c => c.Hash).OrderBy(h => h).ToList();
            Assert.Equal(new[] { "aaaa1111aaaa1111", "bbbb2222bbbb2222", "cccc3333cccc3333" }, hashes);
        }
    }

    [Fact]
    public async Task SweepAsync_KeepsRecentlyAddedOrphansUntilTheyAgeOut()
    {
        // A cover that's an orphan *right now* but was added 5 minutes
        // ago might belong to an entity that's still mid-save. Default
        // grace is 1 hour, so it survives the sweep.
        var now = _clock.GetUtcNow().UtcDateTime;
        await using (var db = new CollectifyDbContext(_options))
        {
            db.CoverImages.Add(Cover("ffff9999ffff9999", now.AddMinutes(-5)));
            await db.SaveChangesAsync();
        }

        await using (var db = new CollectifyDbContext(_options))
        {
            var deleted = await NewGc().SweepAsync(db);
            Assert.Equal(0, deleted);
        }

        await using (var assert = new CollectifyDbContext(_options))
        {
            Assert.Single(assert.CoverImages);
        }
    }

    [Fact]
    public async Task SweepAsync_IgnoresImagePathsThatArentCoverHashes()
    {
        // Legacy filesystem paths and raw provider URLs aren't pointers
        // into the CoverImages table; they must not pin anything.
        var now = _clock.GetUtcNow().UtcDateTime;
        await using (var db = new CollectifyDbContext(_options))
        {
            db.CoverImages.Add(Cover("eeee5555eeee5555", now.AddDays(-2)));
            db.Movies.Add(new Movie
            {
                OwnerId = "alice",
                Title = "Old",
                ImagePath = "https://example.com/some-poster.jpg", // raw URL
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new CollectifyDbContext(_options))
        {
            var deleted = await NewGc().SweepAsync(db);
            Assert.Equal(1, deleted);
        }

        await using (var assert = new CollectifyDbContext(_options))
        {
            Assert.Empty(assert.CoverImages);
        }
    }

    [Fact]
    public async Task SweepAsync_OnEmptyOrAllReferenced_IsANoOp()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        await using (var db = new CollectifyDbContext(_options))
        {
            db.CoverImages.Add(Cover("aaaa1111aaaa1111", now.AddDays(-2)));
            db.Movies.Add(new Movie { OwnerId = "alice", Title = "M", ImagePath = "/covers/aaaa1111aaaa1111" });
            await db.SaveChangesAsync();
        }

        await using var db2 = new CollectifyDbContext(_options);
        Assert.Equal(0, await NewGc().SweepAsync(db2));
    }

    [Theory]
    [InlineData("/covers/abc12345", "abc12345")]
    [InlineData("/covers/deadbeefcafe1234", "deadbeefcafe1234")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("https://image.tmdb.org/t/p/w342/poster.jpg", null)]
    [InlineData("/covers/", null)]
    [InlineData("/covers/HASWITHNONHEX", null)]
    [InlineData("/covers/way-too-long-to-possibly-be-a-cover-hash", null)]
    public void TryExtractHash_OnlyAcceptsTheCoverPathShape(string? input, string? expected)
    {
        var got = CoverImageGarbageCollector.TryExtractHash(input, out var hash);
        if (expected is null)
        {
            Assert.False(got);
        }
        else
        {
            Assert.True(got);
            Assert.Equal(expected, hash);
        }
    }
}
