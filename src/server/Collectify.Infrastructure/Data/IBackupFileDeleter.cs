namespace Collectify.Infrastructure.Data;

public interface IBackupFileDeleter
{
    void Delete(string path);
}
