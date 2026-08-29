using Collectify.Infrastructure;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Collectify.PostgresTests;

public sealed class DatabaseMigrationCoordinatorPostgresTests
{
    private const string Image = "postgres:17-alpine@sha256:d4bb0a8c1b7bb2e29f976d099e7bfb9a5d8858cffe9e46b35cd302cd1f1f8168";

    [Fact]
    public async Task MigrateAsync_MissingPostgresTarget_ProvisionsThenMigratesWithoutSqliteBackup()
    {
        await using var container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync();

        var target = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"collectify_backup_{Guid.NewGuid():N}",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseOptions.ConnectionStringKey] = target.ConnectionString,
            })
            .Build();
        var options = new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseNpgsql(target.ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(DatabaseOptions.PostgresMigrationsAssembly);
                npgsql.MigrationsHistoryTable(
                    DatabaseOptions.MigrationsHistoryTable,
                    DatabaseOptions.PostgresSchema);
            })
            .Options;
        await using var db = new CollectifyDbContext(options);
        var backup = new RecordingBackup();
        var coordinator = new DatabaseMigrationCoordinator(
            configuration,
            backup,
            NullLogger<DatabaseMigrationCoordinator>.Instance);

        await coordinator.MigrateAsync(db);

        Assert.Equal(0, backup.CreateCalls);
        var known = db.Database.GetMigrations().ToArray();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(known);
        Assert.Equal(known, applied);
    }

    private sealed class RecordingBackup : ISqliteMigrationBackup
    {
        public int CreateCalls { get; private set; }

        public Task<string?> CreateIfNeededAsync(
            CollectifyDbContext db,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            throw new InvalidOperationException("SQLite backup must not run for PostgreSQL.");
        }

        public Task PruneAsync(
            string newestBackupPath,
            int retention,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SQLite pruning must not run for PostgreSQL.");
    }
}
