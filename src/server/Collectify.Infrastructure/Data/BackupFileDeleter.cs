namespace Collectify.Infrastructure.Data;

public sealed class BackupFileDeleter : IBackupFileDeleter
{
    public void Delete(string path) => File.Delete(path);
}
