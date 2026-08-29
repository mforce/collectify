using Collectify.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public sealed class DatabaseMigrationCoordinatorTests : IDisposable
{
    private const string InitialMigration = "20260505174247_InitialCreate";
    private const string RetentionKey = "Collectify:Database:BackupRetention";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"collectify-migration-coordinator-{Guid.NewGuid():N}");

    public DatabaseMigrationCoordinatorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task MigrateAsync_OldSqlite_BackupContainsOldSchemaBeforeLiveMigration()
    {
        var path = Path.Combine(_directory, "ordered.db");
        await using var db = NewContext(path);
        await MigrateToInitialAndSeedAsync(db);
        var backup = NewBackup();
        var coordinator = NewCoordinator(backup);

        await coordinator.MigrateAsync(db);

        Assert.NotEqual(InitialMigration, await LastMigrationAsync(path));
        var snapshot = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(_directory, "backups"), "*.db"));
        Assert.Equal(InitialMigration, await LastMigrationAsync(snapshot));
        Assert.Equal("pre-migration", await ScalarAsync(snapshot, "SELECT Value FROM BackupSentinel"));
    }

    [Fact]
    public async Task MigrateAsync_BackupFailure_DoesNotApplyPendingMigration()
    {
        var path = Path.Combine(_directory, "backup-failure.db");
        await using var db = NewContext(path);
        await MigrateToInitialAndSeedAsync(db);
        File.WriteAllText(Path.Combine(_directory, "backups"), "blocks directory creation");

        await Assert.ThrowsAsync<IOException>(() => NewCoordinator(NewBackup()).MigrateAsync(db));

        Assert.Equal(InitialMigration, await LastMigrationAsync(path));
    }

    [Fact]
    public async Task MigrateAsync_VerifierFailure_DoesNotApplyPendingMigration()
    {
        var path = Path.Combine(_directory, "verify-failure.db");
        await using var db = NewContext(path);
        await MigrateToInitialAndSeedAsync(db);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => NewCoordinator(NewBackup(new RejectingVerifier())).MigrateAsync(db));

        Assert.Equal(InitialMigration, await LastMigrationAsync(path));
    }

    [Fact]
    public async Task MigrateAsync_ReadOnlyMigrationFailure_DoesNotPruneOlderSnapshots()
    {
        var dataDirectory = Path.Combine(_directory, "read-only");
        Directory.CreateDirectory(dataDirectory);
        var path = Path.Combine(dataDirectory, "collectify.db");
        await using (var setup = NewContext(path))
            await MigrateToInitialAndSeedAsync(setup);

        var backupDirectory = Path.Combine(dataDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);
        var older1 = CreateBackupFile(backupDirectory, "OldA", "20260101T000000000Z");
        var older2 = CreateBackupFile(backupDirectory, "OldB", "20260201T000000000Z");
        await using var readOnly = NewContext($"Data Source={path};Mode=ReadOnly");
        var coordinator = NewCoordinator(NewBackup(), retention: "1");

        await Assert.ThrowsAsync<SqliteException>(() => coordinator.MigrateAsync(readOnly));

        Assert.True(File.Exists(older1));
        Assert.True(File.Exists(older2));
        Assert.Equal(3, Directory.EnumerateFiles(backupDirectory, "*.db").Count());
        Assert.Equal(InitialMigration, await LastMigrationAsync(path));
    }

    [Fact]
    public async Task MigrateAsync_InMemorySqlite_CallsBackupAndMigrates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewContext(connection);
        var backup = new RecordingBackup();

        await NewCoordinator(backup).MigrateAsync(db);

        Assert.Equal(1, backup.CreateCalls);
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-an-int")]
    public async Task MigrateAsync_InvalidRetention_FailsBeforeAnyInMemoryShortcut(string value)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewContext(connection);
        var backup = new RecordingBackup();
        var coordinator = NewCoordinator(backup, value);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.MigrateAsync(db));

        Assert.Contains(RetentionKey, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backup.CreateCalls);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task MigrateAsync_PruneDeletionFailure_DoesNotFailStartup()
    {
        var dataDirectory = Path.Combine(_directory, "delete-failure");
        Directory.CreateDirectory(dataDirectory);
        var path = Path.Combine(dataDirectory, "collectify.db");
        await using var db = NewContext(path);
        await MigrateToInitialAndSeedAsync(db);
        var backupDirectory = Path.Combine(dataDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);
        var old = CreateBackupFile(backupDirectory, "Old", "20260101T000000000Z");
        var backup = NewBackup(fileDeleter: new ThrowingDeleter());

        await NewCoordinator(backup, retention: "1").MigrateAsync(db);

        Assert.NotEqual(InitialMigration, await LastMigrationAsync(path));
        Assert.True(File.Exists(old));
        Assert.Equal(2, Directory.EnumerateFiles(backupDirectory, "*.db").Count());
    }

    private DatabaseMigrationCoordinator NewCoordinator(
        ISqliteMigrationBackup backup,
        string retention = "10")
        => new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RetentionKey] = retention,
            }).Build(),
            backup,
            NullLogger<DatabaseMigrationCoordinator>.Instance);

    private SqliteMigrationBackup NewBackup(
        ISqliteBackupVerifier? verifier = null,
        IBackupFileDeleter? fileDeleter = null)
        => new(
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 34, 56, 789, TimeSpan.Zero)),
            verifier ?? new SqliteBackupVerifier(),
            fileDeleter ?? new BackupFileDeleter(),
            NullLogger<SqliteMigrationBackup>.Instance);

    private static CollectifyDbContext NewContext(string pathOrConnectionString)
    {
        var connectionString = pathOrConnectionString.Contains('=')
            ? pathOrConnectionString
            : $"Data Source={pathOrConnectionString}";
        return new CollectifyDbContext(new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly("Collectify.Infrastructure"))
            .Options);
    }

    private static CollectifyDbContext NewContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite(connection, sqlite =>
                sqlite.MigrationsAssembly("Collectify.Infrastructure"))
            .Options);

    private static async Task MigrateToInitialAndSeedAsync(CollectifyDbContext db)
    {
        await db.GetService<IMigrator>().MigrateAsync(InitialMigration);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE BackupSentinel(Value TEXT NOT NULL);");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO BackupSentinel(Value) VALUES ('pre-migration');");
    }

    private static async Task<object?> ScalarAsync(string path, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static async Task<string?> LastMigrationAsync(string path)
        => (string?)await ScalarAsync(
            path,
            "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1");

    private static string CreateBackupFile(string directory, string migration, string timestamp)
    {
        var path = Path.Combine(directory, $"collectify-{migration}-{timestamp}.db");
        File.WriteAllText(path, migration);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class RecordingBackup : ISqliteMigrationBackup
    {
        public int CreateCalls { get; private set; }

        public Task<string?> CreateIfNeededAsync(
            CollectifyDbContext db,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult<string?>(null);
        }

        public Task PruneAsync(
            string newestBackupPath,
            int retention,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RejectingVerifier : ISqliteBackupVerifier
    {
        public Task VerifyAsync(string path, CancellationToken cancellationToken = default)
            => throw new InvalidDataException("intentional verifier rejection");
    }

    private sealed class ThrowingDeleter : IBackupFileDeleter
    {
        public void Delete(string path) => throw new IOException("intentional deletion failure");
    }
}
