using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup.Images;
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
        int Formats,
        string? Director,
        int? RuntimeMinutes,
        string? Studio,
        string? Genres,
        string? Barcode,
        string? TmdbId,
        string? ImdbId,
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
        WatchStatus WatchStatus,
        DateOnly? LastWatchedOn,
        int WatchCount,
        string[]? Tags,
        DateTime? AddedAt,
        DateTime? UpdatedAt);

    public static IEndpointRouteBuilder MapMoviesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/movies").RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] MovieFormat? format,
            [FromQuery] int? year,
            [FromQuery] int? yearFrom,
            [FromQuery] int? yearTo,
            [FromQuery] string? director,
            [FromQuery] string? studio,
            [FromQuery] string? genre,
            [FromQuery] CollectionStatus? status,
            [FromQuery] WatchStatus? watchStatus,
            [FromQuery] int? ratingMin,
            [FromQuery(Name = "tag")] string[]? tag,
            CollectifyDbContext db,
            UserManager<AppUser> users,
            HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var q = db.Movies.AsNoTracking().Include(m => m.Tags).Where(m => m.OwnerId == ownerId);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var like = $"%{query}%";
                q = q.Where(m => EF.Functions.Like(m.Title, like)
                              || (m.Director != null && EF.Functions.Like(m.Director, like))
                              || (m.OriginalTitle != null && EF.Functions.Like(m.OriginalTitle, like)));
            }
            if (format.HasValue && format.Value != MovieFormat.None)
                q = q.Where(m => (m.Formats & format.Value) != 0);
            // Legacy single-year stays for back-compat; yearFrom/yearTo
            // is the new range-style filter.
            if (year.HasValue) q = q.Where(m => m.Year == year);
            if (yearFrom.HasValue) q = q.Where(m => m.Year != null && m.Year >= yearFrom);
            if (yearTo.HasValue) q = q.Where(m => m.Year != null && m.Year <= yearTo);
            if (!string.IsNullOrWhiteSpace(director))
            {
                var like = $"%{director}%";
                q = q.Where(m => m.Director != null && EF.Functions.Like(m.Director, like));
            }
            if (!string.IsNullOrWhiteSpace(studio))
            {
                var like = $"%{studio}%";
                q = q.Where(m => m.Studio != null && EF.Functions.Like(m.Studio, like));
            }
            if (!string.IsNullOrWhiteSpace(genre))
            {
                // Genres is stored as a comma-separated string; substring
                // match is good enough for the volume here.
                var like = $"%{genre}%";
                q = q.Where(m => m.Genres != null && EF.Functions.Like(m.Genres, like));
            }
            if (status.HasValue) q = q.Where(m => m.Status == status.Value);
            if (watchStatus.HasValue) q = q.Where(m => m.WatchStatus == watchStatus.Value);
            if (ratingMin is { } rm) q = q.Where(m => m.PersonalRating != null && m.PersonalRating >= rm);
            if (tag is { Length: > 0 })
            {
                // OR semantics within the multi-value filter: an item
                // matches if any of its tags is in the requested set.
                // Normalised to lower-case to match TagResolver.
                var normalised = tag.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).ToArray();
                if (normalised.Length > 0)
                    q = q.Where(m => m.Tags.Any(t => normalised.Contains(t.Name)));
            }

            var items = await q.OrderByDescending(m => m.AddedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(ToDto));
        });

        group.MapGet("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var m = await db.Movies.AsNoTracking().Include(x => x.Tags)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            return m is null ? Results.NotFound() : Results.Ok(ToDto(m));
        });

        group.MapPost("/", async ([FromBody] MovieDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var m = new Movie { OwnerId = ownerId };
            ApplyDto(m, dto);
            m.ImagePath = await covers.EnsureLocalAsync(m.ImagePath, ct);
            m.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
            db.Movies.Add(m);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/movies/{m.Id}", ToDto(m));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] MovieDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var m = await db.Movies.Include(x => x.Tags)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
            if (m is null) return Results.NotFound();
            ApplyDto(m, dto);
            m.ImagePath = await covers.EnsureLocalAsync(m.ImagePath, ct);
            m.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
            m.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
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

    private static IResult? Validate(MovieDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (dto.PersonalRating is { } r && (r < 1 || r > 10))
            return Results.BadRequest(new { error = "PersonalRating must be between 1 and 10." });
        // MovieFormats is bound as an int (the client sends the flags bitmask
        // as a number), so the enum converters never see it. Guard the unchecked
        // (MovieFormat)dto.Formats cast at the boundary (issue #115): an
        // arbitrary integer with bits outside the defined flag set must not
        // persist an undefined MovieFormat. None (0) and any combination of
        // defined bits are valid. Derive the mask from the enum so a future
        // member is covered automatically (reviewer F1).
        var validMovieFormatBits = Enum.GetValues<MovieFormat>()
            .Aggregate(0, (mask, f) => mask | (int)f);
        if ((dto.Formats & ~validMovieFormatBits) != 0)
            return Results.BadRequest(new { error = "Formats contains an undefined MovieFormat bit." });
        if (dto.AcquisitionCurrency is { Length: > 0 } c && c.Length != 3)
            return Results.BadRequest(new { error = "AcquisitionCurrency must be a 3-letter ISO 4217 code." });
        return null;
    }

    private static MovieDto ToDto(Movie m) => new(
        m.Id, m.Title, m.OriginalTitle, m.Year, (int)m.Formats, m.Director, m.RuntimeMinutes,
        m.Studio, m.Genres, m.Barcode, m.TmdbId, m.ImdbId, m.ImagePath, m.Description, m.Notes,
        m.PersonalRating, m.Status, m.Condition,
        m.AcquiredOn, m.AcquisitionPrice, m.AcquisitionCurrency, m.AcquisitionSource,
        m.WatchStatus, m.LastWatchedOn, m.WatchCount,
        TagResolver.ToNameArray(m.Tags),
        m.AddedAt, m.UpdatedAt);

    private static void ApplyDto(Movie m, MovieDto dto)
    {
        m.Title = dto.Title?.Trim() ?? string.Empty;
        m.OriginalTitle = dto.OriginalTitle;
        m.Year = dto.Year;
        m.Formats = (MovieFormat)dto.Formats;
        m.Director = dto.Director;
        m.RuntimeMinutes = dto.RuntimeMinutes;
        m.Studio = dto.Studio;
        m.Genres = dto.Genres;
        m.Barcode = dto.Barcode;
        m.TmdbId = dto.TmdbId;
        m.ImdbId = dto.ImdbId;
        m.ImagePath = dto.ImagePath;
        m.Description = dto.Description;
        m.Notes = dto.Notes;
        m.PersonalRating = dto.PersonalRating;
        m.Status = dto.Status;
        m.Condition = dto.Condition;
        m.AcquiredOn = dto.AcquiredOn;
        m.AcquisitionPrice = dto.AcquisitionPrice;
        m.AcquisitionCurrency = string.IsNullOrWhiteSpace(dto.AcquisitionCurrency) ? null : dto.AcquisitionCurrency.ToUpperInvariant();
        m.AcquisitionSource = dto.AcquisitionSource;
        m.WatchStatus = dto.WatchStatus;
        m.LastWatchedOn = dto.LastWatchedOn;
        m.WatchCount = dto.WatchCount;
    }
}
