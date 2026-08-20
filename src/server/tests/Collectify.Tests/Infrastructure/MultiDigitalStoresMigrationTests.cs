using Collectify.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Verifies the MultiDigitalStores migration (#91): a Game moves from a
/// nullable DigitalStore enum value + bool IsDigital to a non-nullable
/// DigitalStores [Flags] bitmask. The backfill is hand-written SQL, so this
/// pins that the old persisted values and the IsDigital flag transform into
/// the correct new bits — and that nothing is silently lost.
/// </summary>
public class MultiDigitalStoresMigrationTests
{
    private static (string ConnectionString, string DbPath) NewFileBackedSqlite()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"collectify-mig-ds-{Guid.NewGuid():N}.db");
        return ($"Data Source={dbPath}", dbPath);
    }

    private static CollectifyDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CollectifyDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CollectifyDbContext(options);
    }

    /// <summary>Migrate to the pre-#91 schema, seed old-format rows, migrate up, and return the DigitalStores values by title.</summary>
    private static async Task<Dictionary<string, int>> MigrateUpAndReadAsync(string connectionString)
    {
        await using var context = NewContext(connectionString);
        // Pre-#91 schema: Games still has DigitalStore (nullable) and IsDigital.
        await context.GetService<IMigrator>().MigrateAsync("20260819014459_DropLookupCacheEntries");
        context.Database.OpenConnection();
        var seedSql = """
            INSERT INTO "Games" ("Title","OwnerId","Platform","Status","AddedAt","UpdatedAt","CompletionStatus","IsDigital","DigitalStore")
            VALUES
                ('SteamGame',   'u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 0),  -- Steam
                ('GogGame',     'u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 1),  -- Gog
                ('EpicGame',    'u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 2),  -- Epic
                ('XboxGame',    'u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 3),  -- Xbox
                ('PsnGame',     'u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 4),  -- Psn
                ('NintendoGame','u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 5),  -- Nintendo
                ('OtherGame',   'u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, 99), -- Other
                ('DigitalNoStore','u1',0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 1, NULL), -- digital, no store -> Other
                ('PhysicalGame','u1', 0, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 0, 0)   -- physical, unused Steam value -> None
            """;
        using var seed = context.Database.GetDbConnection().CreateCommand();
        seed.CommandText = seedSql;
        await seed.ExecuteNonQueryAsync();
        context.Database.CloseConnection();

        // Apply the MultiDigitalStores migration (+ any later ones).
        await context.GetService<IMigrator>().MigrateAsync();

        var result = new Dictionary<string, int>();
        using var read = context.Database.GetDbConnection().CreateCommand();
        read.CommandText = "SELECT \"Title\", \"DigitalStores\" FROM \"Games\";";
        context.Database.OpenConnection();
        using var reader = await read.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetInt32(1);
        context.Database.CloseConnection();
        return result;
    }

    [Fact]
    public async Task Up_BackfillsOldStoreValuesAndIsDigitalFlagIntoBitmask()
    {
        var (connectionString, dbPath) = NewFileBackedSqlite();
        try
        {
            var byTitle = await MigrateUpAndReadAsync(connectionString);

            Assert.Equal(1,  byTitle["SteamGame"]);    // old 0  -> bit 1
            Assert.Equal(2,  byTitle["GogGame"]);      // old 1  -> bit 2
            Assert.Equal(4,  byTitle["EpicGame"]);     // old 2  -> bit 4
            Assert.Equal(8,  byTitle["XboxGame"]);     // old 3  -> bit 8
            Assert.Equal(16, byTitle["PsnGame"]);      // old 4  -> bit 16
            Assert.Equal(32, byTitle["NintendoGame"]); // old 5  -> bit 32
            Assert.Equal(64, byTitle["OtherGame"]);    // old 99 -> bit 64
            Assert.Equal(64, byTitle["DigitalNoStore"]); // digital, no store -> Other
            Assert.Equal(0,  byTitle["PhysicalGame"]);   // physical -> None (0)
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Up_DropsIsDigitalAndMakesDigitalStoresNotNull()
    {
        var (connectionString, dbPath) = NewFileBackedSqlite();
        try
        {
            await using var context = NewContext(connectionString);
            await context.GetService<IMigrator>().MigrateAsync("20260819014459_DropLookupCacheEntries");
            await context.GetService<IMigrator>().MigrateAsync();

            var columns = new List<(string Name, string Type, int NotNull)>();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info(Games);";
            context.Database.OpenConnection();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    columns.Add((reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
            }
            context.Database.CloseConnection();

            var digitalStores = Assert.Single(columns, c => c.Name == "DigitalStores");
            Assert.Equal(1, digitalStores.NotNull); // NOT NULL
            Assert.DoesNotContain(columns, c => c.Name == "IsDigital");
            Assert.DoesNotContain(columns, c => c.Name == "DigitalStore");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
