using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Infrastructure;

public class GamePlatformBackfillTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _options;

    public GamePlatformBackfillTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_options);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunAsync_ResolvesKnownLegacyValues_AndClearsPlatformLegacy()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.AddRange(
                new Game { OwnerId = "alice", Title = "Hades",       PlatformLegacy = "PC" },
                new Game { OwnerId = "alice", Title = "BotW",        PlatformLegacy = "Nintendo Switch" },
                new Game { OwnerId = "alice", Title = "Halo MCC",    PlatformLegacy = "xbox series x" });
            await seed.SaveChangesAsync();
        }

        await using (var db = new CollectifyDbContext(_options))
        {
            var resolved = await GamePlatformBackfill.RunAsync(db);
            Assert.Equal(3, resolved);
        }

        await using (var assert = new CollectifyDbContext(_options))
        {
            var games = assert.Games.OrderBy(g => g.Id).ToList();
            Assert.Equal(GamePlatform.Pc, games[0].Platform);
            Assert.Equal(GamePlatform.Switch, games[1].Platform);
            Assert.Equal(GamePlatform.XboxSeriesXS, games[2].Platform);
            Assert.All(games, g => Assert.Null(g.PlatformLegacy));
        }
    }

    [Fact]
    public async Task RunAsync_LeavesUnknownLegacyValuesUntouched()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Obscure", PlatformLegacy = "Atari Jaguar" });
            await seed.SaveChangesAsync();
        }

        await using (var db = new CollectifyDbContext(_options))
        {
            var resolved = await GamePlatformBackfill.RunAsync(db);
            Assert.Equal(0, resolved);
        }

        await using (var assert = new CollectifyDbContext(_options))
        {
            var g = assert.Games.Single();
            // Default Platform stays Other; legacy string preserved so the
            // user can see what they originally typed.
            Assert.Equal(GamePlatform.Other, g.Platform);
            Assert.Equal("Atari Jaguar", g.PlatformLegacy);
        }
    }

    [Fact]
    public async Task RunAsync_OnDbWithNoPendingRows_IsANoOp()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades", Platform = GamePlatform.Pc });
            await seed.SaveChangesAsync();
        }

        await using var db = new CollectifyDbContext(_options);
        Assert.Equal(0, await GamePlatformBackfill.RunAsync(db));
    }
}
