using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Lookup.Igdb;

/// <summary>
/// Scoped worker that performs a single IGDB backfill sweep: finds every
/// <see cref="Game"/> with <c>IgdbId == null</c>, resolves a confident IGDB
/// match per title (see <see cref="IgdbBackfillPlanner"/>), and fills the
/// metadata plus a locally-cached cover.
///
/// Idempotent: filled games are skipped, so a re-run is a no-op for anything
/// already resolved. Unmatched games are left for manual resolution in the UI —
/// there is deliberately no "attempted" marker (re-sweeps are cheap because
/// lookups hit the ILookupCache).
///
/// Merge semantics (fill-only, never clobber — hardened after code review):
/// every existing value on a Game wins. Backfill only fills fields that are
/// still null, so a Steam import's richer data or a user's hand-entered
/// description / uploaded cover is never overwritten. Only the IGDB linkage
/// (<c>IgdbId</c>) is always written.
///
/// Fail-soft and quota-aware: an unconfigured provider makes the whole sweep a
/// no-op; a per-game failure is isolated (the change tracker is cleared so a
/// failed game's partial state can never be flushed by a later save); the sweep
/// is paced, capped at <see cref="IgdbBackfillOptions.MaxGamesPerSweep"/>, and
/// aborts early when IGDB looks throttled (consecutive empty results).
/// </summary>
public sealed class IgdbBackfillRunner
{
    private readonly CollectifyDbContext _db;
    private readonly IGameMetadataProvider _provider;
    private readonly ICoverImageStore _covers;
    private readonly TimeProvider _clock;
    private readonly IgdbBackfillOptions _options;
    private readonly ILogger<IgdbBackfillRunner> _log;

    public IgdbBackfillRunner(
        CollectifyDbContext db,
        IGameMetadataProvider provider,
        ICoverImageStore covers,
        TimeProvider clock,
        IOptions<IgdbBackfillOptions> options,
        ILogger<IgdbBackfillRunner> log)
    {
        _db = db;
        _provider = provider;
        _covers = covers;
        _clock = clock;
        _options = options.Value;
        _log = log;
    }

    /// <summary>
    /// Run one sweep. Returns the number of games successfully backfilled.
    /// </summary>
    public async Task<int> RunSweepAsync(CancellationToken ct = default)
    {
        if (!_provider.IsConfigured)
        {
            _log.LogInformation("IGDB backfill skipped: IGDB/Twitch not configured");
            return 0;
        }

        // Deterministic, bounded, and NOT tracked: the pending list is only
        // used to drive iteration. Each game is re-read on its own scope/save
        // below, so we never hold the whole set in memory or leave stale
        // tracked state lying around.
        var pending = await _db.Games.AsNoTracking()
            .Where(g => g.IgdbId == null)
            .OrderBy(g => g.Id)
            .Take(_options.MaxGamesPerSweep)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var filled = 0;
        var consecutiveEmpty = 0;

        foreach (var gameId in pending)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var outcome = await BackfillOneAsync(gameId, ct);

                if (outcome.ProviderReturnedEmpty)
                {
                    // IGDB returned nothing — indistinguishable from throttling
                    // (429 is surfaced as [] by IgdbGameProvider). Bump the
                    // counter; if it climbs to the threshold we abort below.
                    consecutiveEmpty++;
                }
                else if (outcome.WasFilled)
                {
                    filled++;
                    consecutiveEmpty = 0;
                }
                else
                {
                    // Confident "no match" — leave for manual resolution. Not a
                    // throttle signal, so leave consecutiveEmpty untouched.
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // shutdown — stop promptly, don't drain the rest
            }
            catch (Exception ex)
            {
                // Fail-soft: isolate this game's failure. Clear the change
                // tracker so any mutation made before the throw can never be
                // flushed by a later game's save (the shared scoped DbContext
                // hazard). The game's IgdbId is only persisted in the single
                // atomic save, so a failure here leaves it null in the DB and
                // it is simply re-swept next run.
                _db.ChangeTracker.Clear();
                consecutiveEmpty = 0; // a hard failure isn't a throttle signal
                _log.LogWarning(ex, "IGDB backfill failed for game {GameId}; skipping", gameId);
            }

            // Pacing between provider calls (honours cancellation so shutdown
            // can't be held up by a long delay). Tests set PacingDelay to zero
            // to disable this (a fake TimeProvider never advances on its own).
            if (_options.PacingDelay > TimeSpan.Zero)
                await Task.Delay(_options.PacingDelay, _clock, ct);

            if (consecutiveEmpty >= _options.EmptyResultAbortThreshold)
            {
                _log.LogWarning(
                    "IGDB backfill aborting sweep: {N} consecutive empty results (likely IGDB throttling); resuming next interval",
                    consecutiveEmpty);
                break;
            }
        }

        _log.LogInformation("IGDB backfill sweep complete: filled {Filled} of {Pending} pending", filled, pending.Count);
        return filled;
    }

    /// <summary>
    /// Outcome of attempting to backfill a single game.
    /// </summary>
    private sealed record BackfillOutcome(bool WasFilled, bool ProviderReturnedEmpty);

    /// <summary>
    /// Backfill a single game by id.
    /// </summary>
    private async Task<BackfillOutcome> BackfillOneAsync(int gameId, CancellationToken ct)
    {
        // Re-read the game fresh on this context, filtered to still-pending so a
        // manual/concurrent IgdbId assignment made after the sweep read is
        // honoured (no concurrency token, so check right before persisting).
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId && g.IgdbId == null, ct);
        if (game is null) return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: false);

        var candidates = await _provider.SearchAsync(game.Title, ct);
        if (candidates.Count == 0)
        {
            // IGDB returned nothing — likely throttling, signal the caller.
            return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: true);
        }

        var match = IgdbBackfillPlanner.BestMatch(game, candidates);
        if (match is null)
        {
            _log.LogDebug("IGDB backfill: no confident match for \"{Title}\", leaving for manual resolution", game.Title);
            return new BackfillOutcome(WasFilled: false, ProviderReturnedEmpty: false);
        }

        // Localize the cover FIRST, before mutating the game. CoverImageStore
        // calls SaveChangesAsync internally on the same scoped DbContext; doing
        // this before we touch the game means that internal save never flushes
        // a partially-applied game.
        string? coverPath = null;
        if (!string.IsNullOrWhiteSpace(match.Result.ImageUrl))
            coverPath = await _covers.EnsureLocalAsync(match.Result.ImageUrl, ct);

        Apply(game, match.Result, coverPath);

        // Single atomic save: the whole game is committed at once. IgdbId is
        // only persisted here, so a failure before this line leaves the game
        // fully null and re-sweepable.
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("IGDB backfill: linked \"{Title}\" to IGDB id {IgdbId}", game.Title, match.Result.ProviderKey);
        return new BackfillOutcome(WasFilled: true, ProviderReturnedEmpty: false);
    }

    /// <summary>
    /// Fill-only merge: existing values always win; only IgdbId is always
    /// written. Never overwrites a developer/publisher/year/description/cover
    /// the user or a Steam import already set. Never writes Platform — the
    /// existing value (or Other, left for the user) is preserved.
    /// </summary>
    private void Apply(Game game, GameLookupResult r, string? coverPath)
    {
        game.IgdbId = r.ProviderKey;
        game.Developer ??= r.Developer;
        game.Publisher ??= r.Publisher;
        game.Year ??= r.Year;
        if (string.IsNullOrWhiteSpace(game.Description))
            game.Description = ComposeDescription(r.Description, r.Genres);
        if (string.IsNullOrWhiteSpace(game.ImagePath))
            game.ImagePath = coverPath;
        game.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Genres have no dedicated field on Game (issue #132), so they are folded
    /// into the description rather than creating user-visible tags — a
    /// deliberate decision to avoid polluting the global per-owner tag pool.
    /// </summary>
    internal static string? ComposeDescription(string? summary, string? genres)
    {
        var hasSummary = !string.IsNullOrWhiteSpace(summary);
        var hasGenres = !string.IsNullOrWhiteSpace(genres);

        if (hasSummary && hasGenres) return $"{summary}\n\nGenres: {genres}";
        if (hasGenres) return $"Genres: {genres}";
        return hasSummary ? summary : null;
    }
}
