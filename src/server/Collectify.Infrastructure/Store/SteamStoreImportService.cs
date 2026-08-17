using System.Security.Cryptography;
using System.Text;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Lookup.Images;
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
    private readonly ICoverImageStore _covers;
    private readonly SteamOptions.SteamSubOptions _options;
    private readonly ILogger<SteamStoreImportService> _log;

    private static readonly DigitalStore SteamStore = DigitalStore.Steam;

    public SteamStoreImportService(
        CollectifyDbContext db,
        ISteamClient steam,
        ICoverImageStore covers,
        IOptions<SteamOptions> options,
        ILogger<SteamStoreImportService> log)
    {
        _db = db;
        _steam = steam;
        _covers = covers;
        _options = options.Value.Steam;
        _log = log;
    }

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
    /// Heuristic: is this EF update a UNIQUE constraint violation (the only
    /// race we treat as "another importer won") rather than some other DB
    /// failure we should surface? Covers both SQLite ("UNIQUE constraint
    /// failed") and PostgreSQL ("duplicate key value violates unique
    /// constraint", SQLSTATE 23505), walking the inner-exception chain so
    /// provider exceptions nested under DbUpdateException are seen.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (Exception? inner = ex; inner is not null; inner = inner.InnerException)
        {
            if (inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)) return true;
            if (inner.Message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase)) return true;
            if (inner.Message.Contains("23505", StringComparison.Ordinal)) return true; // PostgreSQL unique_violation SQLSTATE
        }
        return false;
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
    public async Task<(string State, string CookieHalf)> CreateAuthRequestAsync(string ownerId, TimeSpan lifetime, CancellationToken ct = default)
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
        await _db.SaveChangesAsync(ct);
        return (state, cookieHalf);
    }

    /// <summary>
    /// Read-only existence check used to short-circuit the public callback
    /// BEFORE any outbound Steam round trip: the hashed (state, cookie) must
    /// map to an unconsumed, unexpired request. Doesn't consume anything.
    /// </summary>
    public async Task<bool> HasValidAuthRequestAsync(string state, string? cookieHalf, CancellationToken ct = default)
    {
        var combined = HashState(state + ":" + (cookieHalf ?? string.Empty));
        return await _db.SteamAuthRequests.AnyAsync(r =>
            r.StateHash == combined && !r.Consumed && r.ExpiresAt > DateTime.UtcNow, ct);
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
    /// Pass an optional <paramref name="search"/> term to filter server-side by
    /// title across the FULL library (not just the capped preview slice), so
    /// users with more than <see cref="SteamOptions.SteamSubOptions.PreviewCap"/>
    /// games can still find, import, or repair lower-playtime titles.
    /// </summary>
    public async Task<SteamPreviewResult> GetOwnedTitlesAsync(string ownerId, string? search = null, CancellationToken ct = default)
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

        var searchTerm = search?.Trim().ToLowerInvariant();
        var all = fetch.Games
            .Select(g => new SteamOwnedTitle(
                g.AppId.ToString(),
                g.Name ?? string.Empty,
                // Steam's playtime_forever is already MINUTES (same unit the
                // preview field claims). The client formats it directly, so no
                // /60 here — that conversion belongs to the import path, which
                // sets HoursPlayed = PlaytimeForever / 60.
                g.PlaytimeForever,
                IconUrl(g.AppId, g.ImgIconUrl),
                LogoUrl(g.AppId, g.ImgLogoUrl),
                LastPlayedAt(g.RtimeLastPlayed),
                imported.Contains(g.AppId.ToString()) ? SteamTitleImportState.Imported : SteamTitleImportState.Importable))
            .OrderByDescending(t => t.PlaytimeMinutes)
            .ToList();

        // Apply an optional server-side search over the WHOLE library first so a
        // matching lower-playtime title outside the capped preview is reachable.
        if (!string.IsNullOrEmpty(searchTerm))
            all = all.Where(t => t.Title.ToLowerInvariant().Contains(searchTerm)).ToList();

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

    private static string? LogoUrl(uint appId, string? logoHash)
        => string.IsNullOrWhiteSpace(logoHash)
            ? null
            : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{logoHash}.jpg";

    /// <summary>
    /// Unix seconds to a UTC <see cref="DateTimeOffset"/>, or null when the
    /// value is invalid (≤ 0 or outside the representable range). Range-guarded
    /// so a malformed/absurd provider timestamp can never throw and abort the
    /// whole import batch — the import is best-effort and fails soft.
    /// </summary>
    private static DateTimeOffset? FromUnixTimeSecondsOrNull(long unixSeconds)
    {
        if (unixSeconds <= 0) return null;
        // DateTimeOffset.FromUnixTimeSeconds throws ArgumentOutOfRangeException
        // outside +/- ~292 billion years; clamp to a sane 1970..9999 range so a
        // bogus provider value degrades to null instead of an exception.
        if (unixSeconds < 0 || unixSeconds > 253_402_300_799) return null; // 1970-01-01 .. 9999-12-31T23:59:59
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    /// <summary>Unix timestamp of last play to UTC, or null when never played (or invalid).</summary>
    private static DateTimeOffset? LastPlayedAt(long unixSeconds)
        => FromUnixTimeSecondsOrNull(unixSeconds);

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
        // Dedupe by appid up-front (DistinctBy) — Steam should never return
        // duplicate appids, but if it ever does, a naive ToDictionary would
        // throw and nuke the whole batch instead of failing soft.
        var ownedByAppId = fetch.Games
            .Where(g => g.AppId > 0)
            .DistinctBy(g => g.AppId)
            .ToDictionary(g => g.AppId.ToString(), g => g, StringComparer.Ordinal);

        // Rich metadata (developer/publisher/year/description) from Steam's
        // keyless storefront endpoint, fetched in bulk BEFORE the loop so the
        // import request maps it onto new games as they're created (no page
        // refresh needed — the data is committed with the game). Best-effort:
        // a metadata outage returns an empty lookup and import still proceeds
        // with just what GetOwnedGames gave (cover/playtime/last-played).
        var metadataByAppId = await FetchMetadataAsync(requested, ct);

        // Pre-read the owner's current ledger + matching games ONCE, before any
        // write transaction, so we can (a) localize covers with correct knowledge
        // of what's already imported and (b) keep the write transaction short.
        var ledgerByAppId = await _db.GameStoreOwnedTitles.AsNoTracking()
            .Where(t => t.OwnerId == ownerId && t.Store == SteamStore && requested.Contains(t.ExternalGameId))
            .ToDictionaryAsync(t => t.ExternalGameId, t => t, StringComparer.Ordinal, ct);
        var existingGameIds = ledgerByAppId.Values.Where(l => l.GameId is not null).Select(l => l.GameId!.Value).ToList();
        var existingGamesById = existingGameIds.Count == 0
            ? new Dictionary<int, Game>()
            : await _db.Games.AsNoTracking()
                .Where(g => g.OwnerId == ownerId && existingGameIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g, ct);

        // Pre-localize covers OUTSIDE the write transaction. Network I/O (HTTP
        // fetch + store) happens here so the transaction below stays a short,
        // local DB-only critical section — a 500-game import no longer holds a
        // write lock across 500 sequential downloads. Only items that actually
        // need a cover (not owned → none; existing game with a good local cover
        // → keep it, no download) get one.
        var coverByAppId = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var appIdStr in requested)
        {
            if (!ownedByAppId.TryGetValue(appIdStr, out var owned)) continue; // unowned — no cover
            if (ledgerByAppId.TryGetValue(appIdStr, out var ledger) && ledger.GameId is { } gid
                && existingGamesById.TryGetValue(gid, out var existingGame) && HasGoodLocalCover(existingGame))
            {
                // Already imported with a good local cover (user/IGDB set) —
                // keep it; do NOT download or overwrite.
                coverByAppId[appIdStr] = existingGame.ImagePath;
                continue;
            }
            coverByAppId[appIdStr] = await LocalizeCoverAsync(metadataByAppId.GetValueOrDefault(appIdStr), owned, ct);
        }

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
            // ExternalGameId) unique race. Each item runs on its own savepoint:
            // a conflict rolls back both the ledger insert AND the Game row saved
            // just before it (no orphaned duplicate Game), and we report the
            // winner as already-imported. Works on SQLite and Postgres alike — no
            // reliance on a provider-specific "transaction stays aborted" rule.
            var savepoint = $"import_{appIdStr}";
            try
            {
                await tx.CreateSavepointAsync(savepoint, ct);

                var existing = await _db.GameStoreOwnedTitles
                    .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.Store == SteamStore && t.ExternalGameId == appIdStr, ct);

                var createdGame = false;
                Game? newGame;

                if (existing is null)
                {
                    // Save the Game FIRST so its Id is real before the ledger row
                    // references it (the ledger's composite FK needs a genuine
                    // Games.Id). Inside the same transaction as the ledger insert.
                    newGame = NewImportedGame(
                        ownerId,
                        game,
                        appIdStr,
                        coverByAppId.GetValueOrDefault(appIdStr),
                        metadataByAppId.GetValueOrDefault(appIdStr));
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
                        newGame = NewImportedGame(
                            ownerId,
                            game,
                            appIdStr,
                            coverByAppId.GetValueOrDefault(appIdStr),
                            metadataByAppId.GetValueOrDefault(appIdStr));
                        _db.Games.Add(newGame);
                        await _db.SaveChangesAsync(ct);

                        existing.GameId = newGame.Id;
                        existing.ImportedAt = DateTime.UtcNow;
                        existing.UpdatedAt = DateTime.UtcNow;
                        createdGame = true;
                    }
                    else if (!HasGoodLocalCover(newGame) && coverByAppId.TryGetValue(appIdStr, out var healed))
                    {
                        // Game already imported but its cover is missing or still
                        // a raw remote URL (e.g. imported before the 600x900 /
                        // hash-path fix). Re-derive the local cover now — we only
                        // ever fill a MISSING cover, never overwrite one the user
                        // set manually. The download already happened pre-transaction.
                        newGame.ImagePath = healed;
                        newGame.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                    }
                }

                await _db.SaveChangesAsync(ct);

                if (createdGame && newGame is not null) created.Add(newGame);
                results.Add(new SteamImportResultItem(appIdStr, createdGame, !createdGame && existing is { GameId: not null }));
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Unique-constraint race: another import created this ledger row
                // between our read and write. Roll back this item's savepoint —
                // this also undoes the Game row saved moments ago, so SQLite
                // won't commit an orphaned duplicate Game — then re-read the
                // winner and report it as already-imported.
                await tx.RollbackToSavepointAsync(savepoint, ct);
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

    /// <summary>
    /// Localize a Steam cover to the app's /covers/ store. Prefers the
    /// high-res 600x900 library cover via the storefront <c>assets</c>
    /// metadata (correct 2:3 portrait and handles the hash-pathed art that new
    /// apps use), falling back to the logo banner then the small icon, and
    /// downloads it through <see cref="ICoverImageStore"/> (same path the edit
    /// flow uses). Fail-soft: no cover or a download failure leaves ImagePath
    /// null.
    /// </summary>
    private async Task<string?> LocalizeCoverAsync(SteamStoreBrowseItem? meta, SteamOwnedGame game, CancellationToken ct)
    {
        var coverUrl = StoreAssetUrl(meta)
            ?? LibraryCoverUrl(game.AppId)
            ?? LogoUrl(game.AppId, game.ImgLogoUrl)
            ?? IconUrl(game.AppId, game.ImgIconUrl);
        return await _covers.EnsureLocalAsync(coverUrl, ct);
    }

    /// <summary>
    /// A cover qualifies as "good" when it is stored locally under /covers/.
    /// Anything else (null, empty, or a stale raw remote URL) is repairable and
    /// will be re-localized. A local /covers/ image — however it got there (the
    /// import itself, the user, or IGDB) — is never overwritten.
    /// </summary>
    private static bool HasGoodLocalCover(Game? game)
        => !string.IsNullOrEmpty(game?.ImagePath) && game.ImagePath.StartsWith("/covers/", StringComparison.Ordinal);

    /// <summary>
    /// Resolve the app's real 600x900 library cover from the GetItems
    /// <c>assets</c> block. Newer apps hash the asset directory, so the URL
    /// must come from the metadata's <c>asset_url_format</c> template +
    /// filename — a hardcoded appid-based URL 404s for those. Returns null
    /// when no assets were returned.
    /// </summary>
    public static string? StoreAssetUrl(SteamStoreBrowseItem? meta)
    {
        var assets = meta?.Assets;
        var filename = assets?.LibraryCapsule2x ?? assets?.LibraryCapsule;
        var format = assets?.AssetUrlFormat;
        if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(format)) return null;
        // The template must contain exactly one ${FILENAME} placeholder. A format
        // that doesn't (or a filename/format with path chars that could escape the
        // asset root) is rejected so we never construct a malformed/off-host URL.
        const string token = "${FILENAME}";
        var first = format.IndexOf(token, StringComparison.Ordinal);
        if (first < 0 || format.IndexOf(token, first + token.Length, StringComparison.Ordinal) >= 0) return null;
        if (filename.Contains("..", StringComparison.Ordinal) || filename.Contains("://", StringComparison.Ordinal)) return null;
        var path = format.Replace(token, filename, StringComparison.Ordinal);
        return $"https://shared.akamai.steamstatic.com/store_item_assets/{path}";
    }

    /// <summary>
    /// Steam's canonical library cover for an app: a 600x900 portrait used by
    /// the Steam client library. Deterministic from the appid; kept only as a
    /// fallback when GetItems returned no assets (older apps expose this
    /// un-hashed path). Returns null for invalid appids.
    /// </summary>
    private static string? LibraryCoverUrl(uint appId)
        => appId == 0 ? null : $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_600x900_2x.jpg";

    private static Game NewImportedGame(string ownerId, SteamOwnedGame game, string appIdStr, string? coverPath, SteamStoreBrowseItem? meta) => new()
    {
        OwnerId = ownerId,
        Title = FirstNonEmpty(meta?.Name, game.Name, appIdStr),
        Platform = GamePlatform.Pc,
        IsDigital = true,
        DigitalStore = SteamStore,
        Status = CollectionStatus.Owned,
        HoursPlayed = (int)Math.Min(int.MaxValue, game.PlaytimeForever / 60),
        ImagePath = coverPath,
        LastPlayedOn = ToDateOnly(game.RtimeLastPlayed),
        Developer = FirstOrNull(meta?.BasicInfo?.Developers?.FirstOrDefault()?.Name, 500),
        Publisher = FirstOrNull(meta?.BasicInfo?.Publishers?.FirstOrDefault()?.Name, 500),
        Year = ToYear(meta?.Release?.SteamReleaseDate ?? 0),
        Description = FirstOrNull(meta?.BasicInfo?.ShortDescription, 2000),
        AcquisitionSource = "Steam Import",
    };

    /// <summary>Value truncated to <paramref name="max"/>, or null when null/empty.</summary>
    private static string? FirstOrNull(string? s, int max)
        => string.IsNullOrWhiteSpace(s) ? null : Truncate(s, max);

    /// <summary>Unix release timestamp to the release year, or null when unknown/invalid.</summary>
    private static int? ToYear(long unixSeconds)
        => FromUnixTimeSecondsOrNull(unixSeconds)?.UtcDateTime.Year;

    /// <summary>
    /// Bulk-rich-metadata lookup keyed by appid string. Chunks the requested
    /// ids (GetItems supports up to ~40 appids per request), fail-soft: any
    /// error returns an empty lookup and import proceeds without rich metadata.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, SteamStoreBrowseItem>> FetchMetadataAsync(IEnumerable<string> requested, CancellationToken ct)
    {
        // Only request ids the user actually asked to import (already bounded
        // by ImportCap), skipping the bookkeeping/0 appids.
        var ids = requested
            .Select(id => (uint.TryParse(id, out var a) ? a : 0u))
            .Where(a => a > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return new Dictionary<string, SteamStoreBrowseItem>();

        const int batchSize = SteamClient.BatchSize;
        var items = new List<SteamStoreBrowseItem>();
        foreach (var chunk in Chunk(ids, batchSize))
        {
            var batch = await _steam.GetItemsAsync(chunk, ct);
            items.AddRange(batch);
        }

        return items
            .Where(i => i.AppId > 0)
            // DistinctBy up-front — a naive ToDictionary throws (and would abort
            // the whole import) if Steam ever returned a duplicate appid in a
            // batch; we want that to fail soft, not take down the request.
            .DistinctBy(i => i.AppId)
            .ToDictionary(i => i.AppId.ToString(), i => i, StringComparer.Ordinal);
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }

    /// <summary>Steam's unix-epoch last-played to a UTC date, or null when never played (or invalid).</summary>
    private static DateOnly? ToDateOnly(long unixSeconds)
        => FromUnixTimeSecondsOrNull(unixSeconds) is { } dto ? DateOnly.FromDateTime(dto.UtcDateTime) : null;

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

    /// <summary>First non-null, non-empty value, truncated to 500 chars.</summary>
    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) is { } hit ? Truncate(hit, 500) : string.Empty;
}
