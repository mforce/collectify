using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Igdb;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Focused tests for <see cref="IgdbBackfillService"/> — Collectify's first
/// hosted service, so this establishes the convention. The heavy per-game
/// sweep behaviour is covered thoroughly by <see cref="IgdbBackfillRunnerTests"/>;
/// here we verify the service's decision points and that it actually resolves
/// and invokes the runner through DI once (no timer flakiness in unit tests —
/// the periodic loop is covered implicitly by the runner being exercised).
/// </summary>
public class IgdbBackfillServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public IgdbBackfillServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        // Build the schema once so both the provider's DbContext and the
        // per-test seed/assert contexts share tables. (Without this, seeding a
        // Game fails with "no such table: Games".)
        using var setup = new CollectifyDbContext(
            new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options);
        setup.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ServiceProvider BuildProvider(bool enabled, bool providerConfigured)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_clock);

        services.AddDbContext<CollectifyDbContext>(o => o.UseSqlite(_connection));

        services.AddSingleton<IGameMetadataProvider>(new ScriptedGameProvider
        {
            IsConfigured = providerConfigured,
            SearchResults = providerConfigured ? [new GameLookupResult("igdb", "42", "Hades", GamePlatform.Pc, 2020, "Pub", "Dev", "D", "https://images.igdb.com/c.jpg", "RPG")] : [],
        });
        services.AddSingleton<ICoverImageStore>(new PassthroughCoverStore());
        services.AddScoped<IgdbBackfillRunner>();

        // Options bound from a tiny config with Enabled as given (interval is
        // never reached in these tests, but must be valid for the Validate fn).
        services.AddOptions<IgdbBackfillOptions>()
            .Configure(o =>
            {
                o.Enabled = enabled;
                o.Interval = TimeSpan.FromHours(1);
                o.PacingDelay = TimeSpan.Zero;
            });

        services.AddSingleton(sp => new IgdbBackfillService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            _clock,
            sp.GetRequiredService<IOptionsMonitor<IgdbBackfillOptions>>(),
            NullLogger<IgdbBackfillService>.Instance));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_ReturnsWithoutSweeping()
    {
        using var provider = BuildProvider(enabled: false, providerConfigured: true);
        // Seed a pending game; if the service swept it, IgdbId would be set.
        await using (var seed = new CollectifyDbContext(
            new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options))
        {
            seed.Games.Add(new Collectify.Domain.Entities.Game { OwnerId = "alice", Title = "Hades" });
            await seed.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<IgdbBackfillService>();
        await service.StartAsync(CancellationToken.None); // must return, not sweep
        await service.StopAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(
            new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options);
        Assert.Null(assert.Games.Single().IgdbId);
    }

    [Fact]
    public async Task StartAsync_WhenIgdbUnconfigured_ReturnsWithoutSweeping()
    {
        using var provider = BuildProvider(enabled: true, providerConfigured: false);
        await using (var seed = new CollectifyDbContext(
            new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options))
        {
            seed.Games.Add(new Collectify.Domain.Entities.Game { OwnerId = "alice", Title = "Hades" });
            await seed.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<IgdbBackfillService>();
        await service.StartAsync(CancellationToken.None); // fail-soft: exits, no sweep
        await service.StopAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(
            new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options);
        Assert.Null(assert.Games.Single().IgdbId);
    }

    [Fact]
    public async Task StartAsync_WhenEnabledAndConfigured_SweepsImmediately()
    {
        // The startup sweep (before the periodic timer's first tick) is what
        // makes metadata appear right after a fresh import instead of up to a
        // full interval later. With a "Hades" game pending and the provider
        // configured, StartAsync must have resolved and linked it synchronously.
        using var provider = BuildProvider(enabled: true, providerConfigured: true);
        await using (var seed = new CollectifyDbContext(
            new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options))
        {
            seed.Games.Add(new Collectify.Domain.Entities.Game { OwnerId = "alice", Title = "Hades" });
            await seed.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<IgdbBackfillService>();
        await service.StartAsync(CancellationToken.None);

        // StartAsync returns once the BackgroundService task is STARTED, not
        // once ExecuteAsync completes, so the startup sweep runs on the
        // background task. Wait (real time, bounded, retrying connection
        // contention) for the sweep to link the game before stopping, otherwise
        // StopAsync's cancellation races it. Polling can transiently collide
        // with the sweep's own DbContext on the shared in-memory connection, so
        // treat that specific exception as "still sweeping, try again".
        var deadline = DateTime.UtcNow.AddSeconds(5);
        string? igdbId = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using (var assert = new CollectifyDbContext(
                    new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options))
                {
                    igdbId = assert.Games.AsNoTracking().Single().IgdbId;
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException) when (DateTime.UtcNow < deadline)
            {
                // Shared-connection collision with the live sweep; retry.
                await Task.Delay(20);
                continue;
            }
            if (igdbId is not null) break;
            await Task.Delay(20);
        }

        await service.StopAsync(CancellationToken.None);

        Assert.Equal("42", igdbId);
    }

    private sealed class PassthroughCoverStore : ICoverImageStore
    {
        public Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default) => Task.FromResult(imagePath);
        public Task<string> StoreBytesAsync(byte[] bytes, string contentType, CancellationToken ct = default) => Task.FromResult("/covers/fake");
    }
}
