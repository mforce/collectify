using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class GamesEndpoints
{
    public record GameDto(
        int? Id,
        string Title,
        string? Platform,
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
        DateTime? UpdatedAt);

    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games").RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] string? platform,
            [FromQuery] bool? digital,
            CollectifyDbContext db,
            UserManager<AppUser> users,
            HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var q = db.Games.AsNoTracking().Include(g => g.Tags).Where(g => g.OwnerId == ownerId);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var like = $"%{query}%";
                q = q.Where(g => EF.Functions.Like(g.Title, like)
                              || (g.Publisher != null && EF.Functions.Like(g.Publisher, like))
                              || (g.Developer != null && EF.Functions.Like(g.Developer, like)));
            }
            if (!string.IsNullOrWhiteSpace(platform)) q = q.Where(g => g.Platform == platform);
            if (digital.HasValue) q = q.Where(g => g.IsDigital == digital.Value);

            var items = await q.OrderByDescending(g => g.AddedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(ToDto));
        });

        group.MapGet("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var g = await db.Games.AsNoTracking().Include(x => x.Tags)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            return g is null ? Results.NotFound() : Results.Ok(ToDto(g));
        });

        group.MapPost("/", async ([FromBody] GameDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var g = new Game { OwnerId = ownerId };
            ApplyDto(g, dto);
            g.ImagePath = await covers.EnsureLocalAsync(g.ImagePath, ct);
            g.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
            db.Games.Add(g);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/games/{g.Id}", ToDto(g));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] GameDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var g = await db.Games.Include(x => x.Tags)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
            if (g is null) return Results.NotFound();
            ApplyDto(g, dto);
            g.ImagePath = await covers.EnsureLocalAsync(g.ImagePath, ct);
            g.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
            g.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(g));
        });

        group.MapDelete("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var g = await db.Games.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (g is null) return Results.NotFound();
            db.Games.Remove(g);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

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
        g.Id, g.Title, g.Platform, g.Year, g.Publisher, g.Developer, g.IsDigital, g.DigitalStore,
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
