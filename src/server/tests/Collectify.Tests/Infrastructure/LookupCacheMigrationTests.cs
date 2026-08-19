using Collectify.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Verifies the DropLookupCacheEntries migration: the SQLite LookupCache
/// table is removed on Up (with representative non-cache tables preserved)
/// and fully restored (columns + unique (Provider, Key) index) on Down.
/// </summary>
public class LookupCacheMigrationTests
{
    private static (string ConnectionString, string DbPath) NewFileBackedSqlite()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"collectify-migration-{Guid.NewGuid():N}.db");
        return ($"Data Source={dbPath}", dbPath);
    }

    private static CollectifyDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CollectifyDbContext(options);
    }

    private static List<string> GetTableNames(CollectifyDbContext context)
    {
        var names = new List<string>();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        context.Database.OpenConnection();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    private static List<(string Name, string Sql)> GetIndexes(CollectifyDbContext context, string table)
    {
        var indexes = new List<(string, string)>();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name = $table AND name NOT LIKE 'sqlite_%';";
        command.Parameters.Add(new SqliteParameter("$table", table));
        context.Database.OpenConnection();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            indexes.Add((reader.GetString(0), reader.GetString(1)));
        return indexes;
    }

    [Fact]
    public async Task Up_DropsLookupCache_AndPreservesRepresentativeTables()
    {
        var (connectionString, dbPath) = NewFileBackedSqlite();
        try
        {
            await using var context = NewContext(connectionString);
            await context.Database.MigrateAsync();

            var tables = GetTableNames(context);
            Assert.DoesNotContain("LookupCache", tables);
            // Representative non-cache tables survive the migration.
            Assert.Contains("Movies", tables);
            Assert.Contains("MusicAlbums", tables);
            Assert.Contains("Games", tables);
            Assert.Contains("Tags", tables);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Down_RestoresLookupCacheColumnsAndUniqueIndex()
    {
        var (connectionString, dbPath) = NewFileBackedSqlite();
        try
        {
            await using var context = NewContext(connectionString);
            await context.Database.MigrateAsync();
            // Migrate down through DropLookupCacheEntries to the preceding migration.
            await context.GetService<IMigrator>().MigrateAsync("20260818193439_ConvertLinuxToPc");

            var indexes = GetIndexes(context, "LookupCache");
            var uniqueIndex = indexes.SingleOrDefault(i => i.Name == "IX_LookupCache_Provider_Key");
            Assert.NotEqual(default, uniqueIndex);
            Assert.Contains("UNIQUE", uniqueIndex.Sql.ToUpperInvariant());

            // Verify the original columns via PRAGMA table_info.
            var columns = new List<string>();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info(LookupCache);";
            context.Database.OpenConnection();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    columns.Add(reader.GetString(1));
            }

            Assert.Contains("Id", columns);
            Assert.Contains("Provider", columns);
            Assert.Contains("Key", columns);
            Assert.Contains("JsonResponse", columns);
            Assert.Contains("FetchedAt", columns);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
