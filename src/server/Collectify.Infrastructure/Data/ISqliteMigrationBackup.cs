namespace Collectify.Infrastructure.Data;

public interface ISqliteMigrationBackup
{
    Task<string?> CreateIfNeededAsync(
        CollectifyDbContext db,
        CancellationToken cancellationToken = default);

    Task PruneAsync(
        string newestBackupPath,
        int retention,
        CancellationToken cancellationToken = default);
}
