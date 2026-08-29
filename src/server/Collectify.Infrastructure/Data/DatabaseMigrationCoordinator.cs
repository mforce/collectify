using System.Globalization;
using Collectify.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Collectify.Infrastructure.Data;

public sealed class DatabaseMigrationCoordinator
{
    private readonly IConfiguration _configuration;
    private readonly ISqliteMigrationBackup _sqliteBackup;
    private readonly ILogger<DatabaseMigrationCoordinator> _logger;

    public DatabaseMigrationCoordinator(
        IConfiguration configuration,
        ISqliteMigrationBackup sqliteBackup,
        ILogger<DatabaseMigrationCoordinator> logger)
    {
        _configuration = configuration;
        _sqliteBackup = sqliteBackup;
        _logger = logger;
    }

    public async Task MigrateAsync(
        CollectifyDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsNpgsql())
        {
            await CollectifyDbContextExtensions.EnsurePostgresDatabaseAsync(_configuration);
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        if (!db.Database.IsSqlite())
        {
            throw new InvalidOperationException(
                $"Unsupported EF Core database provider '{db.Database.ProviderName ?? "(unknown)"}'.");
        }

        var retention = ReadRetention();
        var snapshot = await _sqliteBackup.CreateIfNeededAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (snapshot is not null)
        {
            await _sqliteBackup.PruneAsync(snapshot, retention, cancellationToken);
            _logger.LogInformation(
                "SQLite migration completed with pre-migration backup {BackupPath}",
                snapshot);
        }
    }

    private int ReadRetention()
    {
        var configured = _configuration[DatabaseOptions.BackupRetentionKey];
        if (configured is null) return DatabaseOptions.DefaultBackupRetention;

        if (!int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retention)
            || retention <= 0)
        {
            throw new InvalidOperationException(
                $"'{DatabaseOptions.BackupRetentionKey}' must be a positive integer; received '{configured}'.");
        }

        return retention;
    }
}
