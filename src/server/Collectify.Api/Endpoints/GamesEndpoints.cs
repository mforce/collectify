using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class GamesEndpoints
{
    // Union of every defined DigitalStore flag bit. Derived from the enum so
    // a future member is covered automatically; computed once, not per write
    // request. None (0) is excluded because it is the "no store" state.
    private static readonly int ValidDigitalStoreBits =
        Enum.GetValues<DigitalStore>().Aggregate(0, (mask, s) => mask | (int)s);

    public record GameDto(
        int? Id,
        string Title,
        GamePlatform Platform,
        string? PlatformLegacy,
        int? Year,
        string? Publisher,
        string? Developer,
        int DigitalStores,
        string? Barcode,
        string? IgdbId,
        string? ImagePath,
        string? Description,
        string? Notes,
        int? PersonalRating,
        CollectionStatus Status,
        Condition? Condition,
        DateOnly? AcquiredOn,
        decimal? AcquisitionPrice,
        string? AcquisitionCurrency,
        string? AcquisitionSource,
        CompletionStatus CompletionStatus,
        int? HoursPlayed,
        DateOnly? LastPlayedOn,
        string[]? Tags,
        DateTime? AddedAt,
        DateTime? UpdatedAt,
        DateOnly? ReleaseDate,
        string? AgeRating) : ICollectionEntryDto;

    private static readonly CollectionEndpointConfig<Game, GameDto> Config = new()
    {
        RoutePrefix = "/api/games",
        Set = db => db.Games,
        ToDto = ToDto,
        Apply = ApplyDto,
        Validate = Validate,
        SearchFilter = (q, query) =>
        {
            var like = $"%{query}%";
            return q.Where(g => EF.Functions.Like(g.Title, like)
                              || (g.Publisher != null && EF.Functions.Like(g.Publisher, like))
                              || (g.Developer != null && EF.Functions.Like(g.Developer, like)));
        },
        ExtraFilters = (q, request) =>
        {
            // NOT a present-but-invalid 400 case: an undefined/retired numeric
            // (e.g. "3", "999") that resolves to neither a defined member nor
            // a mapping alias must still degrade to "no filter", per the
            // #96 oracle test (List_FiltersByPlatform_RetiredOrUndefinedNumeric_IsIgnoredNotStaleValue) —
            // a stale bookmarked platform value should not 400 the whole list.
            var platformValues = request.Query["platform"];
            if (platformValues.Count > 1)
                return (q, Results.BadRequest(new { error = "Query parameter 'platform' must have a single value." }));
            var platformFilter = ResolvePlatform(platformValues);
            if (platformFilter.HasValue) q = q.Where(g => g.Platform == platformFilter.Value);

            if (request.Query.TryGetValue("digital", out var digitalValues))
            {
                if (digitalValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'digital' must have a single value." }));
                if (!bool.TryParse(digitalValues.ToString(), out var digital))
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'digital'." }));
                // "Digital" == owning at least one store; derived from the
                // bitmask (the IsDigital column no longer exists, #91).
                q = q.Where(g => (g.DigitalStores != DigitalStore.None) == digital);
            }

            if (request.Query.TryGetValue("publisher", out var publisherValues))
            {
                if (publisherValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'publisher' must have a single value." }));
                var publisher = publisherValues.ToString();
                if (!string.IsNullOrWhiteSpace(publisher))
                {
                    var like = $"%{publisher}%";
                    q = q.Where(g => g.Publisher != null && EF.Functions.Like(g.Publisher, like));
                }
            }

            if (request.Query.TryGetValue("developer", out var developerValues))
            {
                if (developerValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'developer' must have a single value." }));
                var developer = developerValues.ToString();
                if (!string.IsNullOrWhiteSpace(developer))
                {
                    var like = $"%{developer}%";
                    q = q.Where(g => g.Developer != null && EF.Functions.Like(g.Developer, like));
                }
            }

            if (request.Query.TryGetValue("completionStatus", out var completionStatusValues))
            {
                if (completionStatusValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'completionStatus' must have a single value." }));
                if (Enum.TryParse<CompletionStatus>(completionStatusValues, ignoreCase: true, out var completionStatus)
                    && Enum.IsDefined(completionStatus))
                    q = q.Where(g => g.CompletionStatus == completionStatus);
                else
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'completionStatus'." }));
            }

            if (request.Query.TryGetValue("digitalStore", out var digitalStoreValues))
            {
                if (digitalStoreValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'digitalStore' must have a single value." }));
                var stores = ResolveDigitalStores(digitalStoreValues.ToString());
                if (stores is null)
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'digitalStore'." }));
                if (stores.Value != DigitalStore.None)
                    q = q.Where(g => (g.DigitalStores & stores.Value) != 0);
            }

            return (q, null);
        },
        OnDelete = (db, id, ownerId) =>
        {
            // Imported games are linked from GameStoreOwnedTitle via an
            // ownership-preserving composite FK with Restrict delete behavior.
            // Clear the ledger link first (same owner-scoped transaction) so
            // the delete never trips the FK constraint or a NOT NULL violation
            // on OwnerId. Rows whose Game is deleted revert to "importable".
            foreach (var link in db.GameStoreOwnedTitles.Where(l => l.GameId == id && l.OwnerId == ownerId))
            {
                link.GameId = null;
                link.ImportedAt = null;
                link.UpdatedAt = DateTime.UtcNow;
            }

            // DLC -> base-game self-reference is Restrict, so deleting a base game
            // with DLC children would otherwise trip the FK and 500. Detach them
            // first (owner-scoped): the DLC children survive as standalone games
            // rather than being silently deleted or blocking the parent's removal.
            foreach (var child in db.Games.Where(x => x.ParentGameId == id && x.OwnerId == ownerId))
            {
                child.ParentGameId = null;
                child.UpdatedAt = DateTime.UtcNow;
            }
        },
    };

    // Bound as a raw string (not the enum) so a stale/legacy value
    // (e.g. a bookmarked "?platform=Linux" from before Linux folded
    // into Pc, #102) degrades via GamePlatformMapping rather than
    // failing enum binding and 400-ing the whole list request.
    // First try a direct member name (handles Other/Pc/Ps5/... as the
    // enum binder did), requiring it to be a DEFINED member so a
    // retired/unnamed numeric like "3" or "999" doesn't bind to a
    // stale value; otherwise fall back to the free-text mapping so
    // aliases like "linux" -> Pc still resolve.
    private static GamePlatform? ResolvePlatform(string? raw)
    {
        if (Enum.TryParse<GamePlatform>(raw, ignoreCase: true, out var direct)
            && Enum.IsDefined(direct))
            return direct;
        return GamePlatformMapping.TryParse(raw);
    }

    /// <summary>
    /// Resolve the <c>?digitalStore=</c> filter to a <c>DigitalStore</c>
    /// bitmask, mirroring the write-boundary's defined-member-only semantics
    /// (same shape as MoviesEndpoints.ResolveFormat). Accepts a single member
    /// name ("Steam"), a comma-joined combo of member names ("Steam,Gog"), or
    /// a numeric flags combination whose bits are all defined ("5"). Returns
    /// null for an undefined bit, an undefined member, or an empty member —
    /// the caller turns that into a 400 (a present-but-invalid filter value
    /// must not be silently dropped).
    /// </summary>
    private static DigitalStore? ResolveDigitalStores(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (int.TryParse(raw, out var asInt))
        {
            if ((asInt & ~ValidDigitalStoreBits) == 0) return (DigitalStore)asInt;
            return null; // undefined bit(s) in a numeric combo
        }
        var parts = raw.Split(',', StringSplitOptions.TrimEntries);
        var acc = DigitalStore.None;
        foreach (var p in parts)
        {
            if (p.Length == 0)
                return null; // empty member (',', 'Steam,', 'Steam,,Gog') — reject the whole filter
            if (Enum.TryParse<DigitalStore>(p, ignoreCase: true, out var one) && Enum.IsDefined(one))
                acc |= one; // None (0) ORs in as a no-op; named members set their bit
            else
                return null; // any undefined member name — reject the whole filter
        }
        return acc;
    }

    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app) =>
        app.MapCollectionEndpoints(Config);

    private static IResult? Validate(GameDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (dto.PersonalRating is { } r && (r < 1 || r > 10))
            return Results.BadRequest(new { error = "PersonalRating must be between 1 and 10." });
        // DigitalStores is bound as an int (the client sends the flags bitmask
        // as a number), so the enum converters never see it. Guard the unchecked
        // (DigitalStore)dto.DigitalStores cast at the boundary: an arbitrary
        // integer with bits outside the defined flag set must not persist an
        // undefined DigitalStore. None (0) and any combination of defined bits
        // are valid (ValidDigitalStoreBits, see above).
        if ((dto.DigitalStores & ~ValidDigitalStoreBits) != 0)
            return Results.BadRequest(new { error = "DigitalStores contains an undefined DigitalStore bit." });
        if (dto.AcquisitionCurrency is { Length: > 0 } c && c.Length != 3)
            return Results.BadRequest(new { error = "AcquisitionCurrency must be a 3-letter ISO 4217 code." });
        return null;
    }

    private static GameDto ToDto(Game g) => new(
        g.Id, g.Title, g.Platform, g.PlatformLegacy, g.Year, g.Publisher, g.Developer, (int)g.DigitalStores,
        g.Barcode, g.IgdbId, g.ImagePath, g.Description, g.Notes,
        g.PersonalRating, g.Status, g.Condition,
        g.AcquiredOn, g.AcquisitionPrice, g.AcquisitionCurrency, g.AcquisitionSource,
        g.CompletionStatus, g.HoursPlayed, g.LastPlayedOn,
        TagResolver.ToNameArray(g.Tags),
        g.AddedAt, g.UpdatedAt,
        g.ReleaseDate, g.AgeRating);

    private static void ApplyDto(Game g, GameDto dto)
    {
        g.Title = dto.Title?.Trim() ?? string.Empty;
        g.Platform = dto.Platform;
        // Saving the form clears any legacy free-text once the user has
        // picked a real enum value; we don't echo back what they typed.
        g.PlatformLegacy = null;
        g.Year = dto.Year;
        g.Publisher = dto.Publisher;
        g.Developer = dto.Developer;
        g.DigitalStores = (DigitalStore)dto.DigitalStores;
        g.Barcode = dto.Barcode;
        g.IgdbId = dto.IgdbId;
        g.ImagePath = dto.ImagePath;
        g.Description = dto.Description;
        g.Notes = dto.Notes;
        g.PersonalRating = dto.PersonalRating;
        g.Status = dto.Status;
        g.Condition = dto.Condition;
        g.AcquiredOn = dto.AcquiredOn;
        g.AcquisitionPrice = dto.AcquisitionPrice;
        g.AcquisitionCurrency = string.IsNullOrWhiteSpace(dto.AcquisitionCurrency) ? null : dto.AcquisitionCurrency.ToUpperInvariant();
        g.AcquisitionSource = dto.AcquisitionSource;
        g.CompletionStatus = dto.CompletionStatus;
        g.HoursPlayed = dto.HoursPlayed;
        g.LastPlayedOn = dto.LastPlayedOn;
        g.ReleaseDate = dto.ReleaseDate;
        g.AgeRating = dto.AgeRating;
    }
}
