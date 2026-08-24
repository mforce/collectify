using Collectify.Api.Endpoints;
using Collectify.Infrastructure.Data;
using Collectify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collectify.Tests.Api;

public class GenreResolverTests
{
    private static CollectifyDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite("DataSource=:memory:").Options;
        var db = new CollectifyDbContext(opts);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Resolve_CreatesMissing_And_ReusesExisting_PerOwner()
    {
        using var db = NewDb();
        db.Genres.Add(new Genre { OwnerId = "a", Name = "action" });
        await db.SaveChangesAsync();

        var byA = await GenreResolver.ResolveAsync(db, "a", new[] { "Action", "Drama ", "drama" });
        await db.SaveChangesAsync();
        Assert.Equal(2, byA.Count);
        Assert.Contains(byA, g => g.Name == "action");
        Assert.Contains(byA, g => g.Name == "drama");
        Assert.Equal(1, await db.Genres.CountAsync(g => g.OwnerId == "a" && g.Name == "drama"));

        var byB = await GenreResolver.ResolveAsync(db, "b", new[] { "action" });
        Assert.Single(byB);
        Assert.NotEqual(byA.First(g => g.Name == "action").Id, byB[0].Id);
    }
}
