using Microsoft.Data.Sqlite;

namespace Collectify.Infrastructure.Data;

public sealed class SqliteBackupVerifier : ISqliteBackupVerifier
{
    public async Task VerifyAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";

            var results = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                results.Add(reader.GetString(0));

            if (results.Count != 1 || !string.Equals(results[0], "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SQLite backup integrity check failed: {string.Join("; ", results)}");
            }
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException("SQLite backup integrity check failed.", exception);
        }
    }
}
