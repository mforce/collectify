using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class GamesEndpoints
{
    public record GameDto(
        int? Id,
        string Title,
        GamePlatform Platform,
        string? PlatformLegacy,
        int? Year,
        string? Publisher,
        string? Developer,
        bool IsDigital,
        DigitalStore? DigitalStore,
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
        DateTime? UpdatedAt) : ICollectionEntryDto;

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
            var platformFilter = ResolvePlatform(request.Query["platform"]);
            if (platformFilter.HasValue) q = q.Where(g => g.Platform == platformFilter.Value);

            if (request.Query.ContainsKey("digital"))
            {
                if (!bool.TryParse(request.Query["digital"], out var digital))
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'digital'." }));
                q = q.Where(g => g.IsDigital == digital);
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

            if (request.Query.ContainsKey("completionStatus"))
            {
                if (Enum.TryParse<CompletionStatus>(request.Query["completionStatus"], ignoreCase: true, out var completionStatus)
                    && Enum.IsDefined(completionStatus))
                    q = q.Where(g => g.CompletionStatus == completionStatus);
                else
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'completionStatus'." }));
            }

            if (request.Query.ContainsKey("digitalStore"))
            {
                if (Enum.TryParse<DigitalStore>(request.Query["digitalStore"], ignoreCase: true, out var digitalStore)
                    && Enum.IsDefined(digitalStore))
                    q = q.Where(g => g.DigitalStore == digitalStore);
                else
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'digitalStore'." }));
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

    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app) =>
        app.MapCollectionEndpoints(Config);

    private static IResult? Validate(GameDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (dto.PersonalRating is { } r && (r < 1 || r > 10))
            return Results.BadRequest(new { error = "PersonalRating must be between 1 and 10." });
        if (dto.AcquisitionCurrency is { Length: > 0 } c && c.Length != 3)
            return Results.BadRequest(new { error = "AcquisitionCurrency must be a 3-letter ISO 4217 code." });
        return null;
    }

    private static GameDto ToDto(Game g) => new(
        g.Id, g.Title, g.Platform, g.PlatformLegacy, g.Year, g.Publisher, g.Developer, g.IsDigital, g.DigitalStore,
        g.Barcode, g.IgdbId, g.ImagePath, g.Description, g.Notes,
        g.PersonalRating, g.Status, g.Condition,
        g.AcquiredOn, g.AcquisitionPrice, g.AcquisitionCurrency, g.AcquisitionSource,
        g.CompletionStatus, g.HoursPlayed, g.LastPlayedOn,
        TagResolver.ToNameArray(g.Tags),
        g.AddedAt, g.UpdatedAt);

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
        g.IsDigital = dto.IsDigital;
        g.DigitalStore = dto.DigitalStore;
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
    }
}
