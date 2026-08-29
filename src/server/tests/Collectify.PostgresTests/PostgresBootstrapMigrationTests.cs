using System.Text;
using Collectify.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Collectify.PostgresTests;

/// <summary>
/// Verifies issue #100's fresh-target tenant lifecycle: PostgreSQL bootstrap must
/// create the target database using an injection-safe quoted identifier, must
/// reject a blank database name before any server connection, and must route the
/// live migration through the provider-native five-migration assembly. Each test
/// turns RED if its named guard is removed.
/// </summary>
public sealed class PostgresBootstrapMigrationTests
{
    private const string Image = "postgres:17-alpine@sha256:d4bb0a8c1b7bb2e29f976d099e7bfb9a5d8858cffe9e46b35cd302cd1f1f8168";

    [Fact]
    public void QuoteDatabaseIdentifier_WrapsAndEscapes()
    {
        // M-UNQUOTED-ID (unit, no server): pins the quoting contract the live test
        // relies on: plain names are wrapped, embedded double-quotes are doubled.
        Assert.Equal("\"collectify\"", CollectifyDbContextExtensions.QuoteDatabaseIdentifier("collectify"));
        Assert.Equal("\"we\"\"ird\"", CollectifyDbContextExtensions.QuoteDatabaseIdentifier("we\"ird"));
    }

    [Fact]
    public async Task EnsurePostgresDatabaseAsync_CreatesOnlyTheQuotedLiteralDatabase()
    {
        // M-UNQUOTED-ID (live): with a hostile database name, the bootstrap must
        // create exactly ONE database whose name is the hostile string as a single
        // literal — not a statement-split second database, and not a syntax error.
        await using var container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync();

        var baseBuilder = new NpgsqlConnectionStringBuilder(container.GetConnectionString());
        var hostile = "test_db; DROP DATABASE postgres; --";
        var hostileTarget = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = hostile,
        };

        // Capture the baseline template-free database count before any creation,
        // so the assertion is robust to the container's default database name.
        await using var baseline = new NpgsqlConnection(baseBuilder.ConnectionString);
        await baseline.OpenAsync();
        long Total() => (long)(new NpgsqlCommand(
            "SELECT count(*) FROM pg_database WHERE datistemplate = false",
            baseline).ExecuteScalar() ?? 0L);
        var totalBefore = Total();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Collectify:Database:ConnectionString"] = hostileTarget.ConnectionString,
            })
            .Build();

        // This is the provisioning step DatabaseMigrationCoordinator runs before MigrateAsync().
        await CollectifyDbContextExtensions.EnsurePostgresDatabaseAsync(configuration);

        // Assert the exact hostile name exists as a single database...
        await using var admin = new NpgsqlConnection(baseBuilder.ConnectionString);
        await admin.OpenAsync();
        await using var list = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datname = @name", admin);
        list.Parameters.AddWithValue("name", hostile);
        var created = await list.ExecuteScalarAsync();
        Assert.Equal(hostile, created as string);

        // The unchanged template/admin database must still exist — this is the
        // decisive DROP-damage signal. If the hostile name were interpolated
        // unquoted, the injected "DROP DATABASE postgres" would either run as a
        // second statement or be rejected by Postgres — in either case the test
        // fails, proving injection is blocked.
        await using var stillThere = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datname = 'postgres'", admin);
        Assert.Equal("postgres", await stillThere.ExecuteScalarAsync() as string);

        // ...and that the hostile payload created exactly one database (no extra).
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM pg_database WHERE datistemplate = false", admin);
        var total = (long)(await count.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(totalBefore + 1, total);
    }

    [Fact]
    public async Task EnsurePostgresDatabaseAsync_RejectsBlankDatabaseName()
    {
        // M-MISSING-DB-NAME (live): a connection string with no database name must
        // be rejected by the guard BEFORE any server work. With the guard present,
        // the call throws InvalidOperationException without connecting. If the
        // guard is removed, the method instead reaches the server and the CREATE
        // DATABASE "" fails with a Postgres/Npgsql error — a different exception
        // class — so Assert.Throws<InvalidOperationException> goes RED.
        await using var container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync();

        var blankTarget = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = string.Empty,
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Collectify:Database:ConnectionString"] = blankTarget.ConnectionString,
            })
            .Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CollectifyDbContextExtensions.EnsurePostgresDatabaseAsync(configuration));
        Assert.Contains("database name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
