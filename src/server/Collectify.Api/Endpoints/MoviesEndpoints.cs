using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class MoviesEndpoints
{
    public record MovieDto(
        int? Id,
        string Title,
        string? OriginalTitle,
        int? Year,
        MovieFormat Formats,
        string? Director,
        int? RuntimeMinutes,
        string? Studio,
        string? Genres,
        string? Barcode,
        string? TmdbId,
        string? ImdbId,
        string? ImagePath,
        string? Notes,
        DateTime? AddedAt,
        DateTime? UpdatedAt);

    public static IEndpointRouteBuilder MapMoviesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/movies").RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] MovieFormat? format,
            [FromQuery] int? year,
            CollectifyDbContext db,
            UserManager<AppUser> users,
            HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var q = db.Movies.AsNoTracking().Where(m => m.OwnerId == ownerId);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var like = $"%{query}%";
                q = q.Where(m => EF.Functions.Like(m.Title, like)
                              || (m.Director != null && EF.Functions.Like(m.Director, like))
                              || (m.OriginalTitle != null && EF.Functions.Like(m.OriginalTitle, like)));
            }
            if (format.HasValue && format.Value != MovieFormat.None)
                q = q.Where(m => (m.Formats & format.Value) != 0);
            if (year.HasValue) q = q.Where(m => m.Year == year);

            var items = await q.OrderByDescending(m => m.AddedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(ToDto));
        });

        group.MapGet("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var m = await db.Movies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            return m is null ? Results.NotFound() : Results.Ok(ToDto(m));
        });

        group.MapPost("/", async ([FromBody] MovieDto dto, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var m = new Movie { OwnerId = ownerId };
            ApplyDto(m, dto);
            db.Movies.Add(m);
            await db.SaveChangesAsync();
            return Results.Created($"/api/movies/{m.Id}", ToDto(m));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] MovieDto dto, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var m = await db.Movies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (m is null) return Results.NotFound();
            ApplyDto(m, dto);
            m.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(m));
        });

        group.MapDelete("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var m = await db.Movies.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (m is null) return Results.NotFound();
            db.Movies.Remove(m);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static MovieDto ToDto(Movie m) => new(
        m.Id, m.Title, m.OriginalTitle, m.Year, m.Formats, m.Director, m.RuntimeMinutes,
        m.Studio, m.Genres, m.Barcode, m.TmdbId, m.ImdbId, m.ImagePath, m.Notes, m.AddedAt, m.UpdatedAt);

    private static void ApplyDto(Movie m, MovieDto dto)
    {
        m.Title = dto.Title?.Trim() ?? string.Empty;
        m.OriginalTitle = dto.OriginalTitle;
        m.Year = dto.Year;
        m.Formats = dto.Formats;
        m.Director = dto.Director;
        m.RuntimeMinutes = dto.RuntimeMinutes;
        m.Studio = dto.Studio;
        m.Genres = dto.Genres;
        m.Barcode = dto.Barcode;
        m.TmdbId = dto.TmdbId;
        m.ImdbId = dto.ImdbId;
        m.ImagePath = dto.ImagePath;
        m.Notes = dto.Notes;
    }
}
