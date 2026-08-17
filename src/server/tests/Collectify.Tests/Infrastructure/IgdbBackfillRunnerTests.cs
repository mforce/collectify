using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup;
using Collectify.Infrastructure.Lookup.Igdb;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Collectify.Tests.Infrastructure;

public class IgdbBackfillRunnerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CollectifyDbContext> _options;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public IgdbBackfillRunnerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<CollectifyDbContext>().UseSqlite(_connection).Options;
        using var seed = new CollectifyDbContext(_options);
        seed.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static GameLookupResult Hit(string title, GamePlatform? platform = null, string key = "100", int? year = 2015, string? genres = "RPG, Adventure")
        => new(Provider: "igdb", ProviderKey: key, Title: title, Platform: platform,
            Year: year, Publisher: "CD Projekt Red", Developer: "CD Projekt Red",
            Description: "A rich story.", ImageUrl: "https://images.igdb.com/cover.jpg", Genres: genres);

    private IgdbBackfillRunner NewRunner(
        IGameMetadataProvider? provider = null,
        Func<string?, string?>? covers = null,
        IgdbBackfillOptions? options = null)
    {
        var store = new FakeLocalCoverStore(covers);
        var effective = options ?? new IgdbBackfillOptions { PacingDelay = TimeSpan.Zero };
        return new IgdbBackfillRunner(
            new CollectifyDbContext(_options),
            provider ?? new ScriptedGameProvider(),
            store,
            _clock,
            Options.Create(effective),
            NullLogger<IgdbBackfillRunner>.Instance);
    }

    private sealed class FakeLocalCoverStore : ICoverImageStore
    {
        private readonly Func<string?, string?>? _map;
        public FakeLocalCoverStore(Func<string?, string?>? map) => _map = map;
        public Task<string?> EnsureLocalAsync(string? imagePath, CancellationToken ct = default)
            => Task.FromResult(_map?.Invoke(imagePath) ?? imagePath);
        public Task<string> StoreBytesAsync(byte[] bytes, string contentType, CancellationToken ct = default)
            => Task.FromResult("/covers/fake");
    }

    [Fact]
    public async Task RunSweepAsync_FillsMatchedGameMetadata_AndLocalizesCover()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "The Witcher 3", Platform = GamePlatform.Pc });
            await seed.SaveChangesAsync();
        }

        var runner = NewRunner(
            provider: new ScriptedGameProvider { SearchResults = [Hit("The Witcher 3", GamePlatform.Pc, "1942")] },
            covers: _ => "/covers/abc");

        var filled = await runner.RunSweepAsync(CancellationToken.None);
        Assert.Equal(1, filled.Filled);

        await using var assert = new CollectifyDbContext(_options);
        var g = assert.Games.Single();
        Assert.Equal("1942", g.IgdbId);
        Assert.Equal("CD Projekt Red", g.Developer);
        Assert.Equal("CD Projekt Red", g.Publisher);
        Assert.Equal(2015, g.Year);
        Assert.Equal("A rich story.\n\nGenres: RPG, Adventure", g.Description); // genres in description, no tags
        Assert.Equal("/covers/abc", g.ImagePath);
        Assert.Equal(GamePlatform.Pc, g.Platform); // preserved, never re-written
        // Decision #1: NO auto-tags. True table-level check (Tags is a many-to-many
        // that a bare Games load would leave empty regardless).
        Assert.Empty(assert.Set<Tag>());
    }

    [Fact]
    public async Task RunSweepAsync_FillOnly_DoesNotOverwriteExistingValues()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game
            {
                OwnerId = "alice",
                Title = "Hades",
                Platform = GamePlatform.Switch,
                Developer = "Supergiant Games",      // user/manual value
                Description = "My hand-typed note.", // must survive
                ImagePath = "/covers/user",          // user cover must survive
                Year = 2020,
            });
            await seed.SaveChangesAsync();
        }

        // IGDB would supply different values (and nulls in some spots). Year is
        // null here so the year-contradiction guard doesn't decline (the local
        // game's known year 2020 is deliberately not what IGDB reports, which is
        // exactly the fill-only case being tested, not a matching concern).
        var runner = NewRunner(provider: new ScriptedGameProvider { SearchResults = [Hit("Hades", GamePlatform.Pc, "9", year: null, genres: null)] });

        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        var g = assert.Games.Single();
        Assert.Equal("9", g.IgdbId);               // linkage always written
        Assert.Equal("Supergiant Games", g.Developer); // existing wins
        Assert.Equal("My hand-typed note.", g.Description); // existing wins
        Assert.Equal("/covers/user", g.ImagePath); // existing cover wins
        Assert.Equal(2020, g.Year);                // existing year wins
    }

    [Fact]
    public async Task RunSweepAsync_IgdbNulls_DoNotEraseExistingValues()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades", Publisher = "Publisher", Year = 2020 });
            await seed.SaveChangesAsync();
        }

        // IGDB entry here has a null Publisher / Year (Map returns null).
        var nullPub = new GameLookupResult("igdb", "9", "Hades", GamePlatform.Pc, null, null, null, "Summary", "https://images.igdb.com/c.jpg", "RPG");
        var runner = NewRunner(provider: new ScriptedGameProvider { SearchResults = [nullPub] });
        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        var g = assert.Games.Single();
        Assert.Equal("Publisher", g.Publisher); // not erased by null
        Assert.Equal(2020, g.Year);             // not erased by null
    }

    [Fact]
    public async Task RunSweepAsync_DoesNotWritePlatform()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades", Platform = GamePlatform.Other });
            await seed.SaveChangesAsync();
        }

        // Even for Other (unset) games, backfill must NOT stamp an IGDB platform
        // (Map returns IGDB's arbitrary first platform).
        var runner = NewRunner(provider: new ScriptedGameProvider { SearchResults = [Hit("Hades", GamePlatform.Ps5, "9")] });
        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal(GamePlatform.Other, assert.Games.Single().Platform);
    }

    [Fact]
    public async Task RunSweepAsync_AlreadyFilledGame_IsSkipped()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades", IgdbId = "9", Platform = GamePlatform.Pc });
            await seed.SaveChangesAsync();
        }

        // Provider would match, but the game already has IgdbId -> skipped.
        var runner = NewRunner(provider: new ScriptedGameProvider { SearchResults = [Hit("Hades", GamePlatform.Pc, "9")] });
        var filled = await runner.RunSweepAsync(CancellationToken.None);

        Assert.Equal(0, filled.Filled);
        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal("9", assert.Games.Single().IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_UnmatchedGame_IsLeftForManualResolution()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Dark Souls II", Platform = GamePlatform.Pc });
            await seed.SaveChangesAsync();
        }

        var runner = NewRunner(provider: new ScriptedGameProvider { SearchResults = [Hit("Dark Souls II: Scholar of the First Sin", GamePlatform.Ps4, "1")] });
        var filled = await runner.RunSweepAsync(CancellationToken.None);

        Assert.Equal(0, filled.Filled);
        await using var assert = new CollectifyDbContext(_options);
        Assert.Null(assert.Games.Single().IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_ProviderThrow_FailsSoft_AndContinuesToNextGame()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Doomed" });   // throws for this one
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades" });    // succeeds for this one
            await seed.SaveChangesAsync();
        }

        var runner = NewRunner(provider: new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
        {
            ["Doomed"] = () => throw new InvalidOperationException("IGDB exploded"),
            ["Hades"] = () => [Hit("Hades", GamePlatform.Pc, "9")],
        }));

        var filled = await runner.RunSweepAsync(CancellationToken.None);

        Assert.Equal(1, filled.Filled); // the throwing game did NOT stop Hades from filling
        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal("9", assert.Games.Single(g => g.Title == "Hades").IgdbId);
        Assert.Null(assert.Games.Single(g => g.Title == "Doomed").IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_GameWithNoMatch_ContinuesToNextGame()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Obscure" });  // no IGDB match
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades" });    // matches
            await seed.SaveChangesAsync();
        }

        var runner = NewRunner(provider: new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
        {
            ["Obscure"] = () => [],
            ["Hades"] = () => [Hit("Hades", GamePlatform.Pc, "9")],
        }));

        var filled = await runner.RunSweepAsync(CancellationToken.None);
        Assert.Equal(1, filled.Filled);
        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal("9", assert.Games.Single(g => g.Title == "Hades").IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_FailedGamePartialMutation_DoesNotLeakToLaterSave()
    {
        // A failure AFTER a game is mutated (here: during cover localization,
        // which per the new ordering happens before the atomic save) must leave
        // that game fully un-linked, and must not contaminate a later game's
        // save on the shared scoped DbContext.
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Bad" });
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Good" });
            await seed.SaveChangesAsync();
        }

        var badMatch = Hit("Bad", GamePlatform.Pc, "1");
        var runner = NewRunner(
            provider: new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
            {
                ["Bad"] = () => [badMatch with { ImageUrl = "https://images.igdb.com/bad.jpg" }],
                ["Good"] = () => [Hit("Good", GamePlatform.Pc, "2")],
            }),
            covers: url => url != null && url.Contains("bad.jpg", StringComparison.OrdinalIgnoreCase) ? throw new InvalidOperationException("cover failed") : "/covers/ok");

        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        var bad = assert.Games.Single(g => g.Title == "Bad");
        var good = assert.Games.Single(g => g.Title == "Good");
        // "Bad" failed (cover failed) -> its IgdbId must stay null, and its
        // staged state must not be flushed by "Good"'s save.
        Assert.Null(bad.IgdbId);
        Assert.Equal("2", good.IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_UnconfiguredProvider_IsANoOp()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades" });
            await seed.SaveChangesAsync();
        }

        var runner = NewRunner(provider: new ScriptedGameProvider { SearchResults = [Hit("Hades", GamePlatform.Pc, "9")], IsConfigured = false });
        var filled = await runner.RunSweepAsync(CancellationToken.None);

        Assert.Equal(0, filled.Filled);
        await using var assert = new CollectifyDbContext(_options);
        Assert.Null(assert.Games.Single().IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_RespectsMaxGamesPerSweep()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.AddRange(
                new Game { OwnerId = "alice", Title = "A" },
                new Game { OwnerId = "alice", Title = "B" },
                new Game { OwnerId = "alice", Title = "C" });
            await seed.SaveChangesAsync();
        }

        // A and B both match; C would match too, but the cap of 2 must stop the
        // sweep before C is attempted.
        var runner = NewRunner(
            provider: new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
            {
                ["A"] = () => [Hit("A", GamePlatform.Pc, "1")],
                ["B"] = () => [Hit("B", GamePlatform.Pc, "2")],
                ["C"] = () => [Hit("C", GamePlatform.Pc, "3")],
            }),
            options: new IgdbBackfillOptions { MaxGamesPerSweep = 2, PacingDelay = TimeSpan.Zero });

        var filled = await runner.RunSweepAsync(CancellationToken.None);
        Assert.Equal(2, filled.Filled); // only the first 2 attempted (cap)

        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal(2, assert.Games.Count(g => g.IgdbId != null));
        Assert.Null(assert.Games.Single(g => g.Title == "C").IgdbId); // never reached
    }

    [Fact]
    public async Task RunSweepAsync_AbortsEarly_WhenIGdbLooksThrottled()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.AddRange(
                new Game { OwnerId = "alice", Title = "One" },
                new Game { OwnerId = "alice", Title = "Two" },
                new Game { OwnerId = "alice", Title = "Three" },
                new Game { OwnerId = "alice", Title = "Four" });
            await seed.SaveChangesAsync();
        }

        // Provider returns empty for every title (simulates 429 -> [] storm).
        // If the throttle guard failed, the sweep would march through all four;
        // here reaching "Three"/"Four" would throw, so a completed sweep with no
        // throw proves the guard stopped it after 2 consecutive empties.
        var runner = NewRunner(
            provider: new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
            {
                ["One"] = () => [],
                ["Two"] = () => [],
                ["Three"] = () => throw new InvalidOperationException("should not be reached"),
                ["Four"] = () => throw new InvalidOperationException("should not be reached"),
            }),
            options: new IgdbBackfillOptions { EmptyResultAbortThreshold = 2, PacingDelay = TimeSpan.Zero });

        var filled = await runner.RunSweepAsync(CancellationToken.None);
        Assert.Equal(0, filled.Filled); // nothing filled, sweep aborted rather than storming
        // Only 2 games were attempted before the throttle abort — proof that the
        // caller can advance the rotation by Attempted (not the full cap) so the
        // unattempted remainder isn't skipped next sweep.
        Assert.Equal(2, filled.Attempted);

        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal(4, assert.Games.Count(g => g.IgdbId == null));
    }

    [Fact]
    public async Task RunSweepAsync_NonEmptyNoMatch_ResetsEmptyAbortStreak()
    {
        // The throttle abort must count CONSECUTIVE empty results. A non-empty
        // response (even one with no confident match) proves IGDB is alive and
        // must reset the streak, otherwise alternating empty/success responses
        // would accumulate to the threshold and abort a healthy sweep.
        // Threshold 2: on a non-reset counter this aborts after "Three" (never
        // reaching "Four"); with the reset it processes and fills "Four".
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.AddRange(
                new Game { OwnerId = "alice", Title = "One" },
                new Game { OwnerId = "alice", Title = "Two" },
                new Game { OwnerId = "alice", Title = "Three" },
                new Game { OwnerId = "alice", Title = "Four" });
            await seed.SaveChangesAsync();
        }

        var runner = NewRunner(
            provider: new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
            {
                ["One"] = () => [],                                                    // empty (1)
                ["Two"] = () => [Hit("Different Title", GamePlatform.Pc, "x")],        // nonempty, no match -> reset
                ["Three"] = () => [],                                                  // empty (1 again)
                ["Four"] = () => [Hit("Four", GamePlatform.Pc, "4")],                  // would be skipped if streak wasn't reset
            }),
            options: new IgdbBackfillOptions { EmptyResultAbortThreshold = 2, PacingDelay = TimeSpan.Zero });

        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        // With the streak reset on the non-empty "Two", the sweep reaches "Four"
        // and fills it instead of aborting early.
        Assert.Equal("4", assert.Games.Single(g => g.Title == "Four").IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_SkipsCoverDownload_WhenImagePathAlreadySet()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades", ImagePath = "/covers/user" });
            await seed.SaveChangesAsync();
        }

        // A cover func that throws if called: the match has an ImageUrl, but
        // fill-only means it would be discarded, so the download must be skipped.
        var runner = NewRunner(
            provider: new ScriptedGameProvider { SearchResults = [Hit("Hades", GamePlatform.Pc, "9")] },
            covers: _ => throw new InvalidOperationException("cover must not be downloaded when ImagePath is set"));

        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        var g = assert.Games.Single();
        Assert.Equal("9", g.IgdbId);
        Assert.Equal("/covers/user", g.ImagePath); // preserved, not overwritten
    }

    [Fact]
    public async Task RunSweepAsync_ConcurrentIgdbAssignment_IsNotOverwritten()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "Hades" });
            await seed.SaveChangesAsync();
        }

        // Simulate a user manually assigning IGDB id 999 on another DbContext
        // while the sweep's search/cover I/O is in flight. The cover callback
        // runs between the sweep's game read and its save, letting us commit
        // the concurrent assignment into the DB at the contested moment.
        var runner = NewRunner(
            provider: new ScriptedGameProvider { SearchResults = [Hit("Hades", GamePlatform.Pc, "9")] },
            covers: _ =>
            {
                using var concurrent = new CollectifyDbContext(_options);
                var g = concurrent.Games.Single(x => x.Title == "Hades");
                g.IgdbId = "999";
                concurrent.SaveChanges();
                return "/covers/manual";
            });

        var filled = await runner.RunSweepAsync(CancellationToken.None);
        Assert.Equal(0, filled.Filled); // sweep correctly declined to overwrite

        await using var assert = new CollectifyDbContext(_options);
        // The user-assigned id 999 wins; the backfill's "9" must not clobber it.
        Assert.Equal("999", assert.Games.Single().IgdbId);
    }

    [Fact]
    public async Task RunSweepAsync_TitleChangedWhileMatching_IsNotStaleLinked()
    {
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.Add(new Game { OwnerId = "alice", Title = "DOOM" });
            await seed.SaveChangesAsync();
        }

        // The user renames DOOM -> Hades while the backfill's cover I/O is in
        // flight (the cover callback simulates the concurrent edit). The match
        // for "DOOM" must not be applied to the renamed row.
        var runner = NewRunner(
            provider: new ScriptedGameProvider { SearchResults = [Hit("DOOM", GamePlatform.Pc, "1993", year: 1993)] },
            covers: _ =>
            {
                using var concurrent = new CollectifyDbContext(_options);
                var g = concurrent.Games.Single(x => x.Title == "DOOM");
                g.Title = "Hades";
                concurrent.SaveChanges();
                return "/covers/doom";
            });

        await runner.RunSweepAsync(CancellationToken.None);

        await using var assert = new CollectifyDbContext(_options);
        var g = assert.Games.Single();
        Assert.Equal("Hades", g.Title);   // user's rename preserved
        Assert.Null(g.IgdbId);            // stale DOOM match NOT applied
    }

    [Fact]
    public async Task RunSweepAsync_RotatesWindow_SoHighIdGamesEventualySwept()
    {
        // 4 games, cap 2. Only C and D are matchable; A and B never match.
        // With a fixed head window this would repeatedly sweep A,B and silently
        // starve C,D forever. The runner rotates by `offset`, so advancing the
        // offset by the cap each sweep eventually reaches C and D.
        await using (var seed = new CollectifyDbContext(_options))
        {
            seed.Games.AddRange(
                new Game { OwnerId = "alice", Title = "A" },
                new Game { OwnerId = "alice", Title = "B" },
                new Game { OwnerId = "alice", Title = "C" },
                new Game { OwnerId = "alice", Title = "D" });
            await seed.SaveChangesAsync();
        }

        var options = new IgdbBackfillOptions { MaxGamesPerSweep = 2, PacingDelay = TimeSpan.Zero };
        var provider = new MultiplyGameProvider(new Dictionary<string, Func<IReadOnlyList<GameLookupResult>>>
        {
            ["C"] = () => [Hit("C", GamePlatform.Pc, "3")],
            ["D"] = () => [Hit("D", GamePlatform.Pc, "4")],
        });

        // Sweep 1 (offset 0): window = [A, B] — no matches, nothing filled.
        var sweep1 = NewRunner(provider, options: options);
        Assert.Equal(0, (await sweep1.RunSweepAsync(CancellationToken.None, offset: 0)).Filled);

        // Sweep 2 (offset offset+attempted=2): window = [C, D] — both filled.
        var sweep2 = NewRunner(provider, options: options);
        Assert.Equal(2, (await sweep2.RunSweepAsync(CancellationToken.None, offset: options.MaxGamesPerSweep)).Filled);

        await using var assert = new CollectifyDbContext(_options);
        Assert.Equal("3", assert.Games.Single(g => g.Title == "C").IgdbId);
        Assert.Equal("4", assert.Games.Single(g => g.Title == "D").IgdbId);
    }

    /// <summary>Query-aware provider: keys are the exact search title; a value's Func is invoked (may throw).</summary>
    private sealed class MultiplyGameProvider : IGameMetadataProvider
    {
        private readonly IReadOnlyDictionary<string, Func<IReadOnlyList<GameLookupResult>>> _byTitle;
        public MultiplyGameProvider(IReadOnlyDictionary<string, Func<IReadOnlyList<GameLookupResult>>> byTitle) => _byTitle = byTitle;
        public string Name => "igdb";
        public bool IsConfigured => true;
        public Task<IReadOnlyList<GameLookupResult>> SearchAsync(string query, CancellationToken ct = default)
            => _byTitle.TryGetValue(query, out var factory) ? Task.FromResult(factory()) : Task.FromResult<IReadOnlyList<GameLookupResult>>([]);
        public Task<GameLookupResult?> GetByIdAsync(string providerKey, CancellationToken ct = default) => Task.FromResult<GameLookupResult?>(null);
        public Task<IReadOnlyList<GameLookupResult>> SearchByBarcodeAsync(string barcode, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<GameLookupResult>>([]);
    }
}
