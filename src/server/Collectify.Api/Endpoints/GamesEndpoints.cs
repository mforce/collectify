using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
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
        string? Notes,
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
            var q = db.Games.AsNoTracking().Where(g => g.OwnerId == ownerId);
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
            var g = await db.Games.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            return g is null ? Results.NotFound() : Results.Ok(ToDto(g));
        });

        group.MapPost("/", async ([FromBody] GameDto dto, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var g = new Game { OwnerId = ownerId };
            ApplyDto(g, dto);
            db.Games.Add(g);
            await db.SaveChangesAsync();
            return Results.Created($"/api/games/{g.Id}", ToDto(g));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] GameDto dto, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var g = await db.Games.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (g is null) return Results.NotFound();
            ApplyDto(g, dto);
            g.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
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

    private static IResult? Validate(GameDto dto) =>
        string.IsNullOrWhiteSpace(dto.Title)
            ? Results.BadRequest(new { error = "Title is required." })
            : null;

    private static GameDto ToDto(Game g) => new(
        g.Id, g.Title, g.Platform, g.Year, g.Publisher, g.Developer, g.IsDigital, g.DigitalStore,
        g.Barcode, g.IgdbId, g.ImagePath, g.Notes, g.AddedAt, g.UpdatedAt);

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
        g.Notes = dto.Notes;
    }
}
