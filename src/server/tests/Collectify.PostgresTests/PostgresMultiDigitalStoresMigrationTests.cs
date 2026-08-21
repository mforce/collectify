using System.Security.Cryptography;
using System.Text.Json;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Collectify.PostgresTests;

public sealed class PostgresMultiDigitalStoresMigrationTests
{
    private const string Before = "20260818000000_DropLookupCache";
    private const string Migration = "20260820000000_MultiDigitalStores";

    [Fact]
    public async Task MultiDigitalStores_Up_PreservesEveryLegacyValue()
    {
        using var sentinels = LoadSentinels();
        await using var database = await MigrationDatabase.StartAsync();
        await database.MigrateAsync(Before);
        var up = sentinels.RootElement.GetProperty("up");
        var stores = up.GetProperty("games").GetProperty("oldDigitalStoreValues").EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.Null ? (int?)null : x.GetInt32()).ToArray();
        await database.ExecuteAsync(UserSql("up-owner"));
        var id = 1;
        foreach (var store in stores)
            foreach (var digital in new[] { false, true })
                await database.ExecuteAsync("""
                    INSERT INTO "Games" ("Id","OwnerId","Title","Platform","Status","CompletionStatus","IsDigital","DigitalStore","AddedAt","UpdatedAt")
                    VALUES (@id,'up-owner',@title,0,0,0,@digital,@store,now(),now())
                    """, new NpgsqlParameter("id", id++), new NpgsqlParameter("title", $"game-{id}"),
                    new NpgsqlParameter("digital", digital), new NpgsqlParameter("store", (object?)store ?? DBNull.Value));
        var ledgerStores = up.GetProperty("ledger").GetProperty("oldStoreValues").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        foreach (var store in ledgerStores)
        {
            await database.ExecuteAsync("""
                INSERT INTO "GameStoreConnections" ("OwnerId","Store","ExternalAccountId","LinkedAt")
                VALUES ('up-owner',@store,@account,now())
                """, new NpgsqlParameter("store", store), new NpgsqlParameter("account", $"account-{store}"));
            await database.ExecuteAsync("""
                INSERT INTO "GameStoreOwnedTitles" ("OwnerId","Store","ExternalGameId","Title","UpdatedAt")
                VALUES ('up-owner',@store,'collision-key',@title,now())
                """, new NpgsqlParameter("store", store), new NpgsqlParameter("title", $"title-{store}"));
        }
        await database.MigrateAsync(Migration);

        var games = await database.QueryAsync("SELECT \"Title\",\"DigitalStores\" FROM \"Games\" ORDER BY \"Id\"", r => (r.GetString(0), r.GetInt32(1)));
        var physical = up.GetProperty("games").GetProperty("physicalExpected").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var digitalExpected = up.GetProperty("games").GetProperty("digitalExpected").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(18, games.Count);
        for (var i = 0; i < stores.Length; i++)
        {
            Assert.Equal($"game-{i * 2 + 2}", games[i * 2].Item1);
            Assert.Equal(physical[i], games[i * 2].Item2);
            Assert.Equal($"game-{i * 2 + 3}", games[i * 2 + 1].Item1);
            Assert.Equal(digitalExpected[i], games[i * 2 + 1].Item2);
        }
        var expectedLedger = up.GetProperty("ledger").GetProperty("expectedStoreValues").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(expectedLedger, await database.QueryAsync("SELECT \"Store\" FROM \"GameStoreConnections\" ORDER BY \"Id\"", r => r.GetInt32(0)));
        Assert.Equal(expectedLedger, await database.QueryAsync("SELECT \"Store\" FROM \"GameStoreOwnedTitles\" ORDER BY \"Id\"", r => r.GetInt32(0)));
    }

    [Fact]
    public async Task MultiDigitalStores_Down_RestoresEveryRepresentableValue()
    {
        using var sentinels = LoadSentinels();
        await using var database = await MigrationDatabase.StartAsync();
        await database.MigrateAsync();
        var down = sentinels.RootElement.GetProperty("down");
        var stores = down.GetProperty("games").GetProperty("digitalStoresValues").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        await database.ExecuteAsync(UserSql("down-owner"));
        for (var i = 0; i < stores.Length; i++)
            await database.ExecuteAsync("""
                INSERT INTO "Games" ("Id","OwnerId","Title","Platform","Status","CompletionStatus","DigitalStores","AddedAt","UpdatedAt")
                VALUES (@id,'down-owner',@title,0,0,0,@store,now(),now())
                """, new NpgsqlParameter("id", i + 1), new NpgsqlParameter("title", $"game-{i}"), new NpgsqlParameter("store", stores[i]));
        var ledgerStores = down.GetProperty("ledger").GetProperty("currentStoreValues").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        foreach (var store in ledgerStores)
        {
            await database.ExecuteAsync("INSERT INTO \"GameStoreConnections\" (\"OwnerId\",\"Store\",\"ExternalAccountId\",\"LinkedAt\") VALUES ('down-owner',@store,@account,now())",
                new NpgsqlParameter("store", store), new NpgsqlParameter("account", $"account-{store}"));
            await database.ExecuteAsync("INSERT INTO \"GameStoreOwnedTitles\" (\"OwnerId\",\"Store\",\"ExternalGameId\",\"Title\",\"UpdatedAt\") VALUES ('down-owner',@store,'collision-key',@title,now())",
                new NpgsqlParameter("store", store), new NpgsqlParameter("title", $"title-{store}"));
        }
        await database.MigrateAsync(Before);

        var games = await database.QueryAsync("SELECT \"IsDigital\",\"DigitalStore\" FROM \"Games\" ORDER BY \"Id\"", r => (r.GetBoolean(0), r.IsDBNull(1) ? (int?)null : r.GetInt32(1)));
        var expectedDigital = down.GetProperty("games").GetProperty("expectedIsDigital").EnumerateArray().Select(x => x.GetBoolean()).ToArray();
        var expectedStores = down.GetProperty("games").GetProperty("expectedOldDigitalStore").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(expectedDigital, games.Select(x => x.Item1));
        Assert.Equal(expectedStores.Select(x => (int?)x), games.Select(x => x.Item2));
        var expectedLedger = down.GetProperty("ledger").GetProperty("expectedOldStoreValues").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(expectedLedger, await database.QueryAsync("SELECT \"Store\" FROM \"GameStoreConnections\" ORDER BY \"Id\"", r => r.GetInt32(0)));
        Assert.Equal(expectedLedger, await database.QueryAsync("SELECT \"Store\" FROM \"GameStoreOwnedTitles\" ORDER BY \"Id\"", r => r.GetInt32(0)));
        var columns = await database.QueryAsync("SELECT column_name,is_nullable,data_type FROM information_schema.columns WHERE table_schema='public' AND table_name='Games' AND column_name IN ('DigitalStore','IsDigital') ORDER BY column_name", r => (r.GetString(0), r.GetString(1), r.GetString(2)));
        Assert.Equal(new[] { ("DigitalStore", "YES", "integer"), ("IsDigital", "NO", "boolean") }, columns);
    }

    private static JsonDocument LoadSentinels()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "multi-digital-stores-sentinels.json"));
        Assert.Equal("eb4117a59b523097d03407a731d5a72dcdff7576822137efadd43f30a844e96b",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return JsonDocument.Parse(bytes);
    }

    private static string UserSql(string id) => $"""
        INSERT INTO "AspNetUsers" ("Id","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
        VALUES ('{id}',false,false,false,false,0)
        """;

    private sealed class MigrationDatabase : IAsyncDisposable
    {
        private readonly PostgreSqlContainer _container;
        private MigrationDatabase(PostgreSqlContainer container) => _container = container;
        public static async Task<MigrationDatabase> StartAsync()
        {
            var container = new PostgreSqlBuilder("postgres:17-alpine@sha256:d4bb0a8c1b7bb2e29f976d099e7bfb9a5d8858cffe9e46b35cd302cd1f1f8168").Build();
            await container.StartAsync();
            return new MigrationDatabase(container);
        }
        private CollectifyDbContext Context()
        {
            var options = new DbContextOptionsBuilder<CollectifyDbContext>()
                .UseNpgsql(_container.GetConnectionString(), x => x.MigrationsAssembly("Collectify.PostgresMigrations")).Options;
            return new CollectifyDbContext(options);
        }
        public async Task MigrateAsync(string? target = null)
        {
            await using var context = Context();
            await context.Database.MigrateAsync(target ?? "20260821000000_AddRichDetailFields");
        }
        public async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
        {
            await using var connection = new NpgsqlConnection(_container.GetConnectionString()); await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddRange(parameters); await command.ExecuteNonQueryAsync();
        }
        public async Task<List<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> read)
        {
            await using var connection = new NpgsqlConnection(_container.GetConnectionString()); await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
            var result = new List<T>(); while (await reader.ReadAsync()) result.Add(read(reader)); return result;
        }
        public async ValueTask DisposeAsync() => await _container.DisposeAsync();
    }
}
