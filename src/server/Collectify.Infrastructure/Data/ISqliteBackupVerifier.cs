namespace Collectify.Infrastructure.Data;

public interface ISqliteBackupVerifier
{
    Task VerifyAsync(string path, CancellationToken cancellationToken = default);
}
