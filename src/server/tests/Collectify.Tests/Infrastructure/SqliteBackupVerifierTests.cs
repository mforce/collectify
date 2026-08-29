using Collectify.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace Collectify.Tests.Infrastructure;

public sealed class SqliteBackupVerifierTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"collectify-backup-verifier-{Guid.NewGuid():N}");

    public SqliteBackupVerifierTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task VerifyAsync_HealthyDatabase_Completes()
    {
        var path = Path.Combine(_directory, "healthy.db");
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Items(Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);";
            await command.ExecuteNonQueryAsync();
        }

        await new SqliteBackupVerifier().VerifyAsync(path);
    }

    [Fact]
    public async Task VerifyAsync_MalformedSchema_ThrowsIntegrityFailure()
    {
        var path = Path.Combine(_directory, "malformed.db");
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Items(Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);
                INSERT INTO Items(Value) VALUES ('sentinel');
                PRAGMA writable_schema=ON;
                UPDATE sqlite_master SET rootpage=999999 WHERE name='Items';
                PRAGMA writable_schema=OFF;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using (var readOnly = new SqliteConnection($"Data Source={path};Mode=ReadOnly"))
        {
            await readOnly.OpenAsync();
            Assert.Equal(System.Data.ConnectionState.Open, readOnly.State);
        }

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => new SqliteBackupVerifier().VerifyAsync(path));
        Assert.Contains("integrity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
