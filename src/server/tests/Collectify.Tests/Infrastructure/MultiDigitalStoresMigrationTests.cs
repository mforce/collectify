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

    [Fact]
    public async Task Up_RemapsSteamLedgerDiscriminators()
    {
        // #91 renumbers DigitalStore (Steam 0→1, ... Other 99→64). The Steam
        // ledger tables persist Store as a single-value DigitalStore
        // discriminator; a pre-#91 Steam connection/ownership row holds Store=0,
        // which after the renumber must read as DigitalStore.Steam (1) or the
        // import treats it as disconnected / re-importable.
        var (connectionString, dbPath) = NewFileBackedSqlite();
        try
        {
            await using var context = NewContext(connectionString);
            await context.GetService<IMigrator>().MigrateAsync("20260819014459_DropLookupCacheEntries");
            context.Database.OpenConnection();
            var seedSql = """
                INSERT INTO "GameStoreConnections" ("OwnerId","Store","ExternalAccountId","LinkedAt")
                VALUES ('u1', 0, '1001', '2026-01-01 00:00:00');
                INSERT INTO "GameStoreOwnedTitles" ("OwnerId","Store","ExternalGameId","Title","UpdatedAt")
                VALUES ('u1', 0, 'appid1', 'Dota 2', '2026-01-01 00:00:00');
                """;
            using var seed = context.Database.GetDbConnection().CreateCommand();
            seed.CommandText = seedSql;
            await seed.ExecuteNonQueryAsync();
            context.Database.CloseConnection();

            await context.GetService<IMigrator>().MigrateAsync();

            var (connStore, ownedStore) = (int.MaxValue, int.MaxValue);
            using var readConn = context.Database.GetDbConnection().CreateCommand();
            readConn.CommandText = "SELECT \"Store\" FROM \"GameStoreConnections\" LIMIT 1;";
            context.Database.OpenConnection();
            connStore = Convert.ToInt32(await readConn.ExecuteScalarAsync());
            context.Database.CloseConnection();

            using var readOwned = context.Database.GetDbConnection().CreateCommand();
            readOwned.CommandText = "SELECT \"Store\" FROM \"GameStoreOwnedTitles\" LIMIT 1;";
            context.Database.OpenConnection();
            ownedStore = Convert.ToInt32(await readOwned.ExecuteScalarAsync());
            context.Database.CloseConnection();

            Assert.Equal(1, connStore);   // old Steam (0) -> Steam (1)
            Assert.Equal(1, ownedStore);  // old Steam (0) -> Steam (1)
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Down_RestoresOldSchemaAndLedgerDiscriminators()
    {
        // Round-trip: migrate up to the new bitmask model, then down to the
        // pre-#91 schema. The Games columns must be back to DigitalStore +
        // IsDigital, and a Steam ledger row (old Store=0) must round-trip to
        // the new Steam (1) on Up and back to 0 on Down. Guards the SQLite
        // AlterColumn/RenameColumn ordering in Down.
        var (connectionString, dbPath) = NewFileBackedSqlite();
        try
        {
            await using var context = NewContext(connectionString);

            // Build the pre-#91 schema with a realistic old-format Steam
            // connection (Store=0), then bring it current.
            await context.GetService<IMigrator>().MigrateAsync("20260819014459_DropLookupCacheEntries");
            await InsertConnectionAsync(context, store: 0);
            await context.GetService<IMigrator>().MigrateAsync(); // up

            var afterUp = await ReadConnectionStoreAsync(context);
            Assert.Equal(1, afterUp); // old Steam (0) -> Steam (1)

            // Migrate down through MultiDigitalStores to the pre-#91 schema.
            await context.GetService<IMigrator>().MigrateAsync("20260819014459_DropLookupCacheEntries");

            var columns = new List<string>();
            using (var info = context.Database.GetDbConnection().CreateCommand())
            {
                info.CommandText = "PRAGMA table_info(Games);";
                context.Database.OpenConnection();
                using (var reader = await info.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        columns.Add(reader.GetString(1));
                }
            }
            Assert.Contains("DigitalStore", columns);
            Assert.Contains("IsDigital", columns);
            Assert.DoesNotContain(columns, c => c == "DigitalStores");

            var afterDown = await ReadConnectionStoreAsync(context);
            Assert.Equal(0, afterDown); // new Steam (1) -> old Steam (0)
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static async Task InsertConnectionAsync(CollectifyDbContext context, int store)
    {
        context.Database.OpenConnection();
        using var seed = context.Database.GetDbConnection().CreateCommand();
        seed.CommandText = """
            INSERT INTO "GameStoreConnections" ("OwnerId","Store","ExternalAccountId","LinkedAt")
            VALUES ('u1', $store, '1001', '2026-01-01 00:00:00');
            """;
        seed.Parameters.Add(new SqliteParameter("$store", store));
        await seed.ExecuteNonQueryAsync();
        context.Database.CloseConnection();
    }

    private static async Task<int> ReadConnectionStoreAsync(CollectifyDbContext context)
    {
        context.Database.OpenConnection();
        using var readConn = context.Database.GetDbConnection().CreateCommand();
        readConn.CommandText = "SELECT \"Store\" FROM \"GameStoreConnections\" LIMIT 1;";
        var value = Convert.ToInt32(await readConn.ExecuteScalarAsync());
        context.Database.CloseConnection();
        return value;
    }
}
