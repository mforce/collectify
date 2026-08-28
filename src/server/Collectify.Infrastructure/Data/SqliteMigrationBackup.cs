using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Data;

public sealed class SqliteMigrationBackup : ISqliteMigrationBackup
{
    private const string TimestampFormat = "yyyyMMdd'T'HHmmssfff'Z'";
    private static readonly Regex CompletedBackupPattern = new(
        @"^collectify-[A-Za-z0-9_]+-(?<timestamp>\d{8}T\d{9}Z)\.db$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly TimeProvider _timeProvider;
    private readonly ISqliteBackupVerifier _verifier;
    private readonly IBackupFileDeleter _fileDeleter;
    private readonly ILogger<SqliteMigrationBackup> _logger;

    public SqliteMigrationBackup(
        TimeProvider timeProvider,
        ISqliteBackupVerifier verifier,
        IBackupFileDeleter fileDeleter,
        ILogger<SqliteMigrationBackup> logger)
    {
        _timeProvider = timeProvider;
        _verifier = verifier;
        _fileDeleter = fileDeleter;
        _logger = logger;
    }

    public async Task<string?> CreateIfNeededAsync(
        CollectifyDbContext db,
        CancellationToken cancellationToken = default)
    {
        var source = db.Database.GetDbConnection() as SqliteConnection
            ?? throw new InvalidOperationException("SQLite migration backup requires a SQLite connection.");

        if (IsInMemory(source)) return null;

        var sourcePath = Path.GetFullPath(source.DataSource);
        if (!File.Exists(sourcePath)) return null;

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        if (!pending.Any()) return null;

        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        var oldMigration = SanitizeMigrationName(applied.LastOrDefault() ?? "unversioned");
        var backupDirectory = Path.Combine(Path.GetDirectoryName(sourcePath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = _timeProvider.GetUtcNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var finalPath = Path.Combine(
            backupDirectory,
            $"collectify-{oldMigration}-{timestamp}.db");
        var temporaryPath = finalPath + ".tmp";

        await using (new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.None))
        {
        }

        try
        {
            var sourceWasOpen = source.State == ConnectionState.Open;
            try
            {
                if (!sourceWasOpen)
                    await db.Database.OpenConnectionAsync(cancellationToken);

                var destinationConnectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = temporaryPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString();
                await using var destination = new SqliteConnection(destinationConnectionString);
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }
            finally
            {
                if (!sourceWasOpen && source.State == ConnectionState.Open)
                    await db.Database.CloseConnectionAsync();
            }

            await _verifier.VerifyAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, finalPath);
            _logger.LogInformation("Created pre-migration SQLite backup at {BackupPath}", finalPath);
            return finalPath;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(
                    cleanupException,
                    "Could not remove incomplete SQLite backup {BackupPath}",
                    temporaryPath);
            }

            throw;
        }
    }

    public Task PruneAsync(
        string newestBackupPath,
        int retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newestBackupPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retention);

        var directory = Path.GetDirectoryName(Path.GetFullPath(newestBackupPath))
            ?? throw new InvalidOperationException("The SQLite backup path has no parent directory.");
        if (!Directory.Exists(directory)) return Task.CompletedTask;

        var completed = Directory.EnumerateFiles(directory)
            .Select(path => (Path: path, Match: CompletedBackupPattern.Match(Path.GetFileName(path))))
            .Where(item => item.Match.Success)
            .Select(item => (
                item.Path,
                Timestamp: DateTimeOffset.ParseExact(
                    item.Match.Groups["timestamp"].Value,
                    TimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)))
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Path, StringComparer.Ordinal)
            .Skip(retention)
            .ToArray();

        foreach (var item in completed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _fileDeleter.Delete(item.Path);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not remove old SQLite migration backup {BackupPath}",
                    item.Path);
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsInMemory(SqliteConnection connection)
    {
        if (string.Equals(connection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            return true;

        var builder = new SqliteConnectionStringBuilder(connection.ConnectionString);
        return builder.Mode == SqliteOpenMode.Memory
            || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeMigrationName(string migration)
    {
        var value = new StringBuilder(migration.Length);
        foreach (var character in migration)
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
                value.Append(character);
        }

        return value.Length == 0 ? "unversioned" : value.ToString();
    }
}
