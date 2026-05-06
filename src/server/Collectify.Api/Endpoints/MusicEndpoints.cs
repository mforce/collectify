using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class MusicEndpoints
{
    public record AlbumDto(
        int? Id,
        string Title,
        string ArtistName,
        int? Year,
        MusicFormat Format,
        string? Label,
        string? Genres,
        string? Barcode,
        string? MusicBrainzReleaseId,
        string? DiscogsId,
        string? ImagePath,
        string? Notes,
        DateTime? AddedAt,
        DateTime? UpdatedAt);

    public static IEndpointRouteBuilder MapMusicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/music").RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] MusicFormat? format,
            [FromQuery] int? year,
            CollectifyDbContext db,
            UserManager<AppUser> users,
            HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var q = db.MusicAlbums.AsNoTracking().Where(a => a.OwnerId == ownerId);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var like = $"%{query}%";
                q = q.Where(a => EF.Functions.Like(a.Title, like)
                              || EF.Functions.Like(a.ArtistName, like)
                              || (a.Label != null && EF.Functions.Like(a.Label, like)));
            }
            if (format.HasValue) q = q.Where(a => a.Format == format.Value);
            if (year.HasValue) q = q.Where(a => a.Year == year);

            var items = await q.OrderByDescending(a => a.AddedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(ToDto));
        });

        group.MapGet("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var a = await db.MusicAlbums.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            return a is null ? Results.NotFound() : Results.Ok(ToDto(a));
        });

        group.MapPost("/", async ([FromBody] AlbumDto dto, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var a = new MusicAlbum { OwnerId = ownerId };
            ApplyDto(a, dto);
            db.MusicAlbums.Add(a);
            await db.SaveChangesAsync();
            return Results.Created($"/api/music/{a.Id}", ToDto(a));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] AlbumDto dto, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var a = await db.MusicAlbums.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (a is null) return Results.NotFound();
            ApplyDto(a, dto);
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(a));
        });

        group.MapDelete("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var a = await db.MusicAlbums.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (a is null) return Results.NotFound();
            db.MusicAlbums.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static IResult? Validate(AlbumDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (string.IsNullOrWhiteSpace(dto.ArtistName))
            return Results.BadRequest(new { error = "Artist name is required." });
        return null;
    }

    private static AlbumDto ToDto(MusicAlbum a) => new(
        a.Id, a.Title, a.ArtistName, a.Year, a.Format, a.Label, a.Genres, a.Barcode,
        a.MusicBrainzReleaseId, a.DiscogsId, a.ImagePath, a.Notes, a.AddedAt, a.UpdatedAt);

    private static void ApplyDto(MusicAlbum a, AlbumDto dto)
    {
        a.Title = dto.Title?.Trim() ?? string.Empty;
        a.ArtistName = dto.ArtistName?.Trim() ?? string.Empty;
        a.Year = dto.Year;
        a.Format = dto.Format;
        a.Label = dto.Label;
        a.Genres = dto.Genres;
        a.Barcode = dto.Barcode;
        a.MusicBrainzReleaseId = dto.MusicBrainzReleaseId;
        a.DiscogsId = dto.DiscogsId;
        a.ImagePath = dto.ImagePath;
        a.Notes = dto.Notes;
    }
}
