using System.Data;
using Collectify.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public sealed class SqliteMigrationBackupTests : IDisposable
{
    private const string InitialMigration = "20260505174247_InitialCreate";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"collectify-migration-backup-{Guid.NewGuid():N}");

    public SqliteMigrationBackupTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task CreateIfNeededAsync_PendingMigration_CreatesOldSchemaSnapshot()
    {
        var path = Path.Combine(_directory, "collectify.db");
        await using var db = NewContext(path);
        await MigrateToInitialAndSeedAsync(db, "old-schema-sentinel");

        var backup = NewBackup();
        var snapshot = await backup.CreateIfNeededAsync(db);

        Assert.NotNull(snapshot);
        Assert.True(File.Exists(snapshot));
        Assert.Equal("old-schema-sentinel", await ScalarAsync(snapshot!, "SELECT Value FROM BackupSentinel"));
        Assert.Equal(InitialMigration, await LastMigrationAsync(snapshot!));
    }

    [Fact]
    public async Task CreateIfNeededAsync_UncheckpointedWal_CapturesCommittedFrames()
    {
        var path = Path.Combine(_directory, "wal.db");
        await using var source = new SqliteConnection($"Data Source={path}");
        await source.OpenAsync();
        await using var db = NewContext(source);
        await db.GetService<IMigrator>().MigrateAsync(InitialMigration);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE BackupSentinel(Value TEXT NOT NULL);");

        await ExecuteAsync(source, "PRAGMA wal_checkpoint(TRUNCATE);");
        await ExecuteAsync(source, "PRAGMA journal_mode=WAL;");
        await ExecuteAsync(source, "PRAGMA wal_autocheckpoint=0;");
        await ExecuteAsync(source, "INSERT INTO BackupSentinel(Value) VALUES ('wal-only-sentinel');");

        Assert.True(File.Exists(path + "-wal"));
        var rawCopy = Path.Combine(_directory, "raw-copy.db");
        File.Copy(path, rawCopy);
        Assert.Equal(0L, Convert.ToInt64(await ScalarAsync(rawCopy, "SELECT COUNT(*) FROM BackupSentinel")));

        var snapshot = await NewBackup().CreateIfNeededAsync(db);

        Assert.NotNull(snapshot);
        Assert.Equal("wal-only-sentinel", await ScalarAsync(snapshot!, "SELECT Value FROM BackupSentinel"));
        Assert.Equal(ConnectionState.Open, source.State);
    }

    [Fact]
    public async Task CreateIfNeededAsync_CurrentDatabase_DoesNotCreateBackupDirectory()
    {
        var path = Path.Combine(_directory, "current.db");
        await using var db = NewContext(path);
        await db.Database.MigrateAsync();

        var snapshot = await NewBackup().CreateIfNeededAsync(db);

        Assert.Null(snapshot);
        Assert.False(Directory.Exists(Path.Combine(_directory, "backups")));
    }

    [Fact]
    public async Task CreateIfNeededAsync_MissingFile_ReturnsNullWithoutCreatingDirectory()
    {
        var dataDirectory = Path.Combine(_directory, "missing");
        Directory.CreateDirectory(dataDirectory);
        var path = Path.Combine(dataDirectory, "collectify.db");
        await using var db = NewContext(path);

        var snapshot = await NewBackup().CreateIfNeededAsync(db);

        Assert.Null(snapshot);
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.Combine(dataDirectory, "backups")));
    }

    [Fact]
    public async Task CreateIfNeededAsync_InMemory_ReturnsNullAndLeavesConnectionOpen()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = NewContext(connection);

        var snapshot = await NewBackup().CreateIfNeededAsync(db);

        Assert.Null(snapshot);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task CreateIfNeededAsync_VerifierRejects_DeletesTempAndPublishesNoCompletedBackup()
    {
        var path = Path.Combine(_directory, "rejected.db");
        await using var db = NewContext(path);
        await MigrateToInitialAndSeedAsync(db, "sentinel");
        var backup = NewBackup(new RejectingVerifier());

        await Assert.ThrowsAsync<InvalidDataException>(() => backup.CreateIfNeededAsync(db));

        var backupDirectory = Path.Combine(_directory, "backups");
        Assert.True(Directory.Exists(backupDirectory));
        Assert.Empty(Directory.EnumerateFiles(backupDirectory));
        Assert.Equal(InitialMigration, await LastMigrationAsync(path));
    }

    [Fact]
    public async Task PruneAsync_UsesParsedTimestampAcrossMigrationPrefixes()
    {
        var backupDirectory = Path.Combine(_directory, "backups-order");
        Directory.CreateDirectory(backupDirectory);
        var oldest = CreateBackupFile(backupDirectory, "ZMigration", "20260101T000000000Z");
        var middle = CreateBackupFile(backupDirectory, "AMigration", "20260201T000000000Z");
        var newest = CreateBackupFile(backupDirectory, "MMigration", "20260301T000000000Z");

        await NewBackup().PruneAsync(newest, retention: 2);

        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public async Task PruneAsync_IgnoresTempUnrelatedAndNearMissFiles()
    {
        var backupDirectory = Path.Combine(_directory, "backups-match");
        Directory.CreateDirectory(backupDirectory);
        var keep = CreateBackupFile(backupDirectory, "Current", "20260301T000000000Z");
        var remove = CreateBackupFile(backupDirectory, "Old", "20260101T000000000Z");
        var temp = Path.Combine(backupDirectory, "collectify-Old-20250101T000000000Z.db.tmp");
        var unrelated = Path.Combine(backupDirectory, "other-Old-20240101T000000000Z.db");
        var nearMiss = Path.Combine(backupDirectory, "collectify-Old-20230101T000000000Z.db.bak");
        File.WriteAllText(temp, "temp");
        File.WriteAllText(unrelated, "unrelated");
        File.WriteAllText(nearMiss, "near-miss");

        await NewBackup().PruneAsync(keep, retention: 1);

        Assert.True(File.Exists(keep));
        Assert.False(File.Exists(remove));
        Assert.True(File.Exists(temp));
        Assert.True(File.Exists(unrelated));
        Assert.True(File.Exists(nearMiss));
    }

    [Fact]
    public async Task PruneAsync_DeletionFailure_LogsAndCompletes()
    {
        var backupDirectory = Path.Combine(_directory, "backups-delete");
        Directory.CreateDirectory(backupDirectory);
        var keep = CreateBackupFile(backupDirectory, "Current", "20260301T000000000Z");
        var protectedPath = CreateBackupFile(backupDirectory, "Old", "20260101T000000000Z");
        var backup = NewBackup(fileDeleter: new ThrowingDeleter());

        await backup.PruneAsync(keep, retention: 1);

        Assert.True(File.Exists(keep));
        Assert.True(File.Exists(protectedPath));
    }

    private SqliteMigrationBackup NewBackup(
        ISqliteBackupVerifier? verifier = null,
        IBackupFileDeleter? fileDeleter = null)
        => new(
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 34, 56, 789, TimeSpan.Zero)),
            verifier ?? new SqliteBackupVerifier(),
            fileDeleter ?? new BackupFileDeleter(),
            NullLogger<SqliteMigrationBackup>.Instance);

    private static CollectifyDbContext NewContext(string path)
        => new(new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite($"Data Source={path}", sqlite =>
                sqlite.MigrationsAssembly("Collectify.Infrastructure"))
            .Options);

    private static CollectifyDbContext NewContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite(connection, sqlite =>
                sqlite.MigrationsAssembly("Collectify.Infrastructure"))
            .Options);

    private static async Task MigrateToInitialAndSeedAsync(CollectifyDbContext db, string value)
    {
        await db.GetService<IMigrator>().MigrateAsync(InitialMigration);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE BackupSentinel(Value TEXT NOT NULL);");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO BackupSentinel(Value) VALUES ({value});");
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

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

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
