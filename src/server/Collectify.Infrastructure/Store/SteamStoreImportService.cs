using System.Security.Cryptography;
using System.Text;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collectify.Infrastructure.Store;

/// <summary>
/// Orchestrates the Steam store-import lifecycle: connect request / callback
/// completion, owned-title preview, transactional import, and disconnect.
/// Takes a trusted ownerId (never HttpContext / Identity types) so the
/// endpoints stay thin and all ownership + reconciliation logic lives here.
///
/// The ImportedTitle ledger is the idempotency + provenance source of truth;
/// a disconnecting user keeps both the ledger and the imported Game rows (only
/// the GameStoreConnection is removed), so a reconnect can't duplicate games.
/// </summary>
public sealed class SteamStoreImportService
{
    private readonly CollectifyDbContext _db;
    private readonly ISteamClient _steam;
    private readonly SteamOptions.SteamSubOptions _options;
    private readonly ILogger<SteamStoreImportService> _log;

    private static readonly DigitalStore SteamStore = DigitalStore.Steam;

    public SteamStoreImportService(
        CollectifyDbContext db,
        ISteamClient steam,
        IOptions<SteamOptions> options,
        ILogger<SteamStoreImportService> log)
    {
        _db = db;
        _steam = steam;
        _options = options.Value.Steam;
        _log = log;
    }

    public bool IsSteamConfigured => _steam.IsConfigured;

    /// <summary>Hex SHA-256 of a plaintext token (what we store).</summary>
    public static string HashState(string state) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state))).ToLowerInvariant();

    /// <summary>CSPRNG hex string of <paramref name="bytes"/> random bytes.</summary>
    private static string CryptoRandomHex(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }

    /// <summary>
    /// Persist a fresh one-time auth request for an owner and return the
    /// (state, cookieHalf) pair. The stored hash binds BOTH halves together:
    /// hash(state + ":" + cookieHalf). At the callback we require the cookie
    /// half to be present in the browser AND the combined hash to match the
    /// stored row — so a leaked return_to URL alone cannot complete a link.
    /// The plaintext state travels in return_to; the cookie half in an
    /// HttpOnly Secure cookie.
    /// </summary>
    public (string State, string CookieHalf) CreateAuthRequest(string ownerId, TimeSpan lifetime)
    {
        // CSPRNG-backed tokens, not Guid (which is random-ish but not a
        // contractual crypto RNG); these are the security tokens for the
        // OpenID dance.
        var state = CryptoRandomHex(32);
        var cookieHalf = CryptoRandomHex(24);
        _db.SteamAuthRequests.Add(new SteamAuthRequest
        {
            StateHash = HashState(state + ":" + cookieHalf),
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            Consumed = false,
        });
        _db.SaveChanges();
        return (state, cookieHalf);
    }

    /// <summary>
    /// Consumes the one-time auth request bound to (state, cookieHalf) and
    /// returns the owner it was minted for, or null if unknown/expired/
    /// already-consumed/cookie-mismatch. MUST be called only after the OpenID
    /// assertion has been verified by SteamOpenIdVerifier.
    /// </summary>
    public string? ConsumeAuthRequest(string state, string? cookieHalf, CancellationToken ct = default)
    {
        var combined = HashState(state + ":" + (cookieHalf ?? string.Empty));
        var row = _db.SteamAuthRequests.FirstOrDefault(r => r.StateHash == combined);
        if (row is null || row.Consumed || row.ExpiresAt <= DateTime.UtcNow) return null;
        row.Consumed = true;
        _db.SaveChanges();
        return row.OwnerId;
    }

    /// <summary>
    /// Read-only existence check used to short-circuit the public callback
    /// BEFORE any outbound Steam round trip: the hashed (state, cookie) must
    /// map to an unconsumed, unexpired request. Doesn't consume anything.
    /// </summary>
    public bool HasValidAuthRequest(string state, string? cookieHalf)
    {
        var combined = HashState(state + ":" + (cookieHalf ?? string.Empty));
        return _db.SteamAuthRequests.Any(r =>
            r.StateHash == combined && !r.Consumed && r.ExpiresAt > DateTime.UtcNow);
    }

    /// <summary>
    /// Atomically claims the one-time auth request and upserts the owner's
    /// Steam connection in a single transaction, so a failure between the two
    /// can never consume the state without linking the account (or vice
    /// versa). The state claim is a conditional update so two concurrent
    /// callbacks can't both consume the same request. Persona lookup is
    /// expected to have happened before this call (best-effort network I/O).
    /// Returns null if the state is unknown/expired/already-consumed.
    /// </summary>
    public async Task<GameStoreConnection?> CompleteConnectAtomicAsync(
        string state, string? cookieHalf, string steamId64, string? personaName, CancellationToken ct = default)
    {
        var combined = HashState(state + ":" + (cookieHalf ?? string.Empty));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Claim the unconsumed, unexpired request atomically. Exactly one row
        // must flip; if none does, the request is invalid or already used.
        var claimed = await _db.SteamAuthRequests
            .Where(r => r.StateHash == combined && !r.Consumed && r.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Consumed, true), ct);
        if (claimed != 1)
            return null;

        var row = await _db.SteamAuthRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.StateHash == combined, ct);
        var ownerId = row?.OwnerId;
        if (ownerId is null)
            return null;

        var connection = await _db.GameStoreConnections
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.Store == SteamStore, ct);
        if (connection is null)
        {
            connection = new GameStoreConnection
            {
                OwnerId = ownerId,
                Store = SteamStore,
                ExternalAccountId = steamId64,
                LinkedAt = DateTime.UtcNow,
            };
            _db.GameStoreConnections.Add(connection);
        }
        else
        {
            connection.ExternalAccountId = steamId64;
            connection.LinkedAt = DateTime.UtcNow;
        }
        connection.ExternalDisplayName = personaName ?? connection.ExternalDisplayName;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return connection;
    }

    /// <summary>The owner's current Steam connection, if any.</summary>
    public async Task<GameStoreConnection?> GetConnectionAsync(string ownerId, CancellationToken ct = default)
        => await _db.GameStoreConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.Store == SteamStore, ct);

    /// <summary>Best-effort persona name for a SteamID64 (null on any failure).</summary>
    public Task<string?> GetPersonaNameAsync(string steamId64, CancellationToken ct = default)
        => _steam.GetPersonaNameAsync(steamId64, ct);

    /// <summary>
    /// Owned titles for the preview: trusted Steam fetch joined against the
    /// owner's ledger to tag import state. Ordered by playtime desc.
    /// </summary>
    public async Task<SteamPreviewResult> GetOwnedTitlesAsync(string ownerId, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ownerId, ct);
        if (connection is null)
            return new SteamPreviewResult(SteamPreviewStatus.NotConnected, [], false);

        var fetch = await _steam.GetOwnedGamesAsync(connection.ExternalAccountId, ct);
        if (fetch.Status == SteamFetchStatus.Unavailable)
            return new SteamPreviewResult(SteamPreviewStatus.Unavailable, [], false);

        // Successful fetch: bump LastSyncedAt so we don't keep re-pulling a
        // private/empty library as though nothing had synced.
        await _db.GameStoreConnections
            .Where(c => c.OwnerId == ownerId && c.Store == SteamStore)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastSyncedAt, DateTime.UtcNow), ct);

        var ledger = await _db.GameStoreOwnedTitles.AsNoTracking()
            .Where(t => t.OwnerId == ownerId && t.Store == SteamStore)
            .Select(t => new { t.ExternalGameId, t.GameId })
            .ToListAsync(ct);
        var imported = ledger.Where(i => i.GameId != null).Select(i => i.ExternalGameId).ToHashSet();

        var all = fetch.Games
            .Select(g => new SteamOwnedTitle(
                g.AppId.ToString(),
                g.Name ?? string.Empty,
                g.PlaytimeForever / 60,
                IconUrl(g.AppId, g.ImgIconUrl),
                imported.Contains(g.AppId.ToString()) ? SteamTitleImportState.Imported : SteamTitleImportState.Importable))
            .OrderByDescending(t => t.PlaytimeMinutes)
            .ToList();

        // Bound the preview so a huge library stays navigable server-side; the
        // client gets a "truncated" flag to show "show more"/search, and can
        // request the rest later. PreviewCap covers the common self-hosted case.
        var truncated = all.Count > _options.PreviewCap;
        var titles = truncated ? all.Take(_options.PreviewCap).ToList() : all;
        return new SteamPreviewResult(SteamPreviewStatus.Ok, titles, truncated);
    }

    private static string? IconUrl(uint appId, string? iconHash)
        => string.IsNullOrWhiteSpace(iconHash)
            ? null
            : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconHash}.jpg";

    /// <summary>
    /// Transactional import of selected game ids. Only ids present in the
    /// trusted Steam fetch are imported; the ledger + Game are created in one
    /// transaction so a failure never leaves an orphaned Game. Idempotent:
    /// already-imported ids are reported and skipped.
    /// </summary>
    public async Task<(IReadOnlyList<SteamImportResultItem> Results, IReadOnlyList<Game> CreatedGames)> ImportAsync(
        string ownerId, IEnumerable<string> requestedIds, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ownerId, ct);
        if (connection is null) return (Results: [], CreatedGames: []);

        // Normalize + bound the request (cap is enforced by the endpoint; this
        // is a defensive floor so the service never exceeds the configured cap).
        var requested = requestedIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => id.Length > 0 && id.All(char.IsAsciiDigit))
            .Distinct()
            .Take(_options.ImportCap)
            .ToList();
        if (requested.Count == 0) return (Results: [], CreatedGames: []);

        // Trusted source of truth for ownership + titles. If Steam is
        // unavailable right now, nothing can be imported safely (we refuse to
        // import on stale/unverified ownership).
        var fetch = await _steam.GetOwnedGamesAsync(connection.ExternalAccountId, ct);
        if (fetch.Status == SteamFetchStatus.Unavailable)
            return (Results: requested.Select(a => new SteamImportResultItem(a, false, false)).ToList(), CreatedGames: []);
        var ownedByAppId = fetch.Games
            .Where(g => g.AppId > 0)
            .ToDictionary(g => g.AppId.ToString(), g => g, StringComparer.Ordinal);

        var results = new List<SteamImportResultItem>();
        var created = new List<Game>();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var appIdStr in requested)
        {
            if (!ownedByAppId.TryGetValue(appIdStr, out var game))
            {
                // Not owned per the trusted fetch — report, don't import.
                results.Add(new SteamImportResultItem(appIdStr, false, false));
                continue;
            }

            // Guard against a concurrent importer winning the (OwnerId, Store,
            // ExternalGameId) unique race. If the ledger insert collides, the
            // failed SaveChanges aborts this transaction — catch it, clear
            // tracking, and report the winner as already-imported rather than
            // surfacing a 500. This is a tiny single-user race window; the
            // important contract is "idempotent, never a crash".
            try
            {
                var existing = await _db.GameStoreOwnedTitles
                    .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.Store == SteamStore && t.ExternalGameId == appIdStr, ct);

                var createdGame = false;
                Game? newGame;

                if (existing is null)
                {
                    // Save the Game FIRST so its Id is real before the ledger row
                    // references it (the ledger's composite FK needs a genuine
                    // Games.Id). Inside the same transaction as the ledger insert.
                    newGame = NewImportedGame(ownerId, game, appIdStr);
                    _db.Games.Add(newGame);
                    await _db.SaveChangesAsync(ct);

                    _db.GameStoreOwnedTitles.Add(new GameStoreOwnedTitle
                    {
                        OwnerId = ownerId,
                        Store = SteamStore,
                        ExternalGameId = appIdStr,
                        ExternalAccountId = connection.ExternalAccountId,
                        Title = Truncate(game.Name ?? appIdStr, 500),
                        GameId = newGame.Id,
                        ImportedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                    createdGame = true;
                }
                else
                {
                    newGame = existing.GameId is { } gid
                        ? await _db.Games.FirstOrDefaultAsync(g => g.Id == gid && g.OwnerId == ownerId, ct)
                        : null;

                    if (newGame is null)
                    {
                        // Ledger row present but its Game was deleted — re-import:
                        // save the Game first, then relink the existing ledger row.
                        newGame = NewImportedGame(ownerId, game, appIdStr);
                        _db.Games.Add(newGame);
                        await _db.SaveChangesAsync(ct);

                        existing.GameId = newGame.Id;
                        existing.ImportedAt = DateTime.UtcNow;
                        existing.UpdatedAt = DateTime.UtcNow;
                        createdGame = true;
                    }
                }

                await _db.SaveChangesAsync(ct);

                if (createdGame && newGame is not null) created.Add(newGame);
                results.Add(new SteamImportResultItem(appIdStr, createdGame, !createdGame && existing is { GameId: not null }));
            }
            catch (DbUpdateException)
            {
                // Unique-constraint race: another import created this ledger row
                // between our read and write. The failed save aborts the
                // transaction; clear tracking and report the winner as
                // already-imported instead of a 500.
                _db.ChangeTracker.Clear();

                var winner = await _db.GameStoreOwnedTitles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.Store == SteamStore && t.ExternalGameId == appIdStr, ct);
                results.Add(new SteamImportResultItem(appIdStr, false, winner is { GameId: not null }));
            }
        }

        await tx.CommitAsync(ct);

        if (requested.Count > _options.ImportCap)
            _log.LogWarning("Steam import request exceeded cap ({Count} > {Cap}); truncated", requested.Count, _options.ImportCap);

        return (Results: results, CreatedGames: created);
    }

    private static Game NewImportedGame(string ownerId, SteamOwnedGame game, string appIdStr) => new()
    {
        OwnerId = ownerId,
        Title = Truncate(game.Name ?? appIdStr, 500),
        Platform = GamePlatform.Pc,
        IsDigital = true,
        DigitalStore = SteamStore,
        Status = CollectionStatus.Owned,
        HoursPlayed = (int)Math.Min(int.MaxValue, game.PlaytimeForever / 60),
        AcquisitionSource = "Steam Import",
    };

    /// <summary>
    /// Remove the Steam connection but KEEP imported games and the ledger
    /// (the provenance record), so a later reconnect cannot duplicate them.
    /// </summary>
    public async Task DisconnectAsync(string ownerId, CancellationToken ct = default)
    {
        await _db.GameStoreConnections
            .Where(c => c.OwnerId == ownerId && c.Store == SteamStore)
            .ExecuteDeleteAsync(ct);
    }

    internal static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max]);
}
