using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Collectify.PostgresTests;

public sealed class PostgresSnapshotParityTests
{
    [Fact]
    public async Task RuntimeModel_HasNoPendingChanges_AgainstLatestMigrationSnapshot()
    {
        await using var container = new PostgreSqlBuilder("postgres:17-alpine@sha256:d4bb0a8c1b7bb2e29f976d099e7bfb9a5d8858cffe9e46b35cd302cd1f1f8168").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CollectifyDbContext>().UseNpgsql(container.GetConnectionString(),
            x => x.MigrationsAssembly("Collectify.PostgresMigrations")).Options;
        await using var context = new CollectifyDbContext(options);

        // dotnet ef migrations has-pending-model-changes drives this exact API: it diffs the runtime
        // (design-time) model against the last migration's TargetModel, so it catches annotation-only
        // drift the DDL/catalog-manifest tests never touch, not just DDL-shape divergence.
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
