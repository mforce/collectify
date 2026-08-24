using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Collectify.Api.Endpoints;

// The generic handlers need to read the tag names off TDto (`dto.Tags`) without
// knowing the concrete DTO type; each per-type record already declares a
// `Tags` property of this shape, so implementing this marker interface costs
// nothing and keeps the handler bodies free of reflection.
public interface ICollectionEntryDto
{
    string[]? Tags { get; }
    string[]? Genres { get; }
}

/// <summary>A bulk-updatable field. Authoring the value to a strongly-typed
/// JsonElement keeps the write boundary explicit per field: each setter
/// validates its value and returns a human error message on failure, or null
/// and applies it. Returns do not persist until the surrounding handler's
/// single SaveChangesAsync commits, so a failure anywhere in a batch keeps the
/// whole request atomic (nothing is written).</summary>
public sealed class BulkField<TEntity> where TEntity : class, ICollectionEntry
{
    /// <summary>JSON key the client sends (matches the DTO property name).</summary>
    public required string Name { get; init; }
    /// <summary>Returns null on success (value applied) or an error message.</summary>
    public required Func<TEntity, JsonElement, string?> Apply { get; init; }
}

/// <summary>Request body for a bulk PATCH. <see cref="Updates"/> is a partial
/// map — only named fields are set on each row; absent fields are untouched.</summary>
public sealed record BulkUpdateRequest(int[] Ids, Dictionary<string, JsonElement> Updates);

public sealed class CollectionEndpointConfig<TEntity, TDto>
    where TEntity : class, ICollectionEntry, new()
    where TDto : ICollectionEntryDto
{
    public required string RoutePrefix { get; init; }
    public required Func<CollectifyDbContext, DbSet<TEntity>> Set { get; init; }
    public required Func<TEntity, TDto> ToDto { get; init; }
    public required Action<TEntity, TDto> Apply { get; init; }
    public required Func<TDto, IResult?> Validate { get; init; }
    public Func<IQueryable<TEntity>, string, IQueryable<TEntity>>? SearchFilter { get; init; }
    // A present-but-invalid filter value must signal a 400 rather than being
    // silently dropped (mirrors the old strongly-typed [FromQuery] binder's
    // behavior); the delegate returns the (possibly filtered) query plus an
    // error result that, when non-null, short-circuits the list response.
    public Func<IQueryable<TEntity>, HttpRequest, (IQueryable<TEntity> Query, IResult? Error)>? ExtraFilters { get; init; }
    public Action<CollectifyDbContext, int, string>? OnDelete { get; init; }
    // Bulk-updatable fields keyed by their JSON name (see BulkField<TEntity>).
    // Absent (or empty) => the resource has no bulk PATCH surface.
    public IReadOnlyDictionary<string, BulkField<TEntity>>? BulkFields { get; init; }
}

public static class CollectionEndpoints
{
    public static IEndpointRouteBuilder MapCollectionEndpoints<TEntity, TDto>(
        this IEndpointRouteBuilder app, CollectionEndpointConfig<TEntity, TDto> cfg)
        where TEntity : class, ICollectionEntry, new()
        where TDto : ICollectionEntryDto
    {
        var group = app.MapGroup(cfg.RoutePrefix).RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] int? year,
            [FromQuery] int? yearFrom,
            [FromQuery] int? yearTo,
            [FromQuery] CollectionStatus? status,
            [FromQuery] int? ratingMin,
            [FromQuery(Name = "tag")] string[]? tag,
            CollectifyDbContext db,
            UserManager<AppUser> users,
            HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var q = cfg.Set(db).AsNoTracking().Include(e => e.Tags).Include(e => e.Genres).Where(e => e.OwnerId == ownerId);

            if (!string.IsNullOrWhiteSpace(query))
                q = cfg.SearchFilter!(q, query);

            if (year.HasValue) q = q.Where(e => e.Year == year);
            if (yearFrom.HasValue) q = q.Where(e => e.Year != null && e.Year >= yearFrom);
            if (yearTo.HasValue) q = q.Where(e => e.Year != null && e.Year <= yearTo);
            if (status.HasValue) q = q.Where(e => e.Status == status.Value);
            if (ratingMin is { } rm) q = q.Where(e => e.PersonalRating != null && e.PersonalRating >= rm);
            if (tag is { Length: > 0 })
            {
                var normalised = tag.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).ToArray();
                if (normalised.Length > 0)
                    q = q.Where(e => e.Tags.Any(t => normalised.Contains(t.Name)));
            }

            if (cfg.ExtraFilters is { } extra)
            {
                var (filtered, error) = extra(q, ctx.Request);
                if (error is { } e) return e;
                q = filtered;
            }

            var items = await q.OrderByDescending(e => e.AddedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(cfg.ToDto));
        });

        group.MapGet("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var e = await cfg.Set(db).AsNoTracking().Include(x => x.Tags).Include(x => x.Genres)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            return e is null ? Results.NotFound() : Results.Ok(cfg.ToDto(e));
        });

        group.MapPost("/", async ([FromBody] TDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (cfg.Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var e = new TEntity { OwnerId = ownerId };
            cfg.Apply(e, dto);
            e.ImagePath = await covers.EnsureLocalAsync(e.ImagePath, ct);
            e.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
            e.Genres = await GenreResolver.ResolveAsync(db, ownerId, dto.Genres);
            cfg.Set(db).Add(e);
            await db.SaveChangesAsync(ct);
            return Results.Created($"{cfg.RoutePrefix}/{e.Id}", cfg.ToDto(e));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] TDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (cfg.Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var e = await cfg.Set(db).Include(x => x.Tags).Include(x => x.Genres)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
            if (e is null) return Results.NotFound();
            cfg.Apply(e, dto);
            e.ImagePath = await covers.EnsureLocalAsync(e.ImagePath, ct);
            e.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
            e.Genres = await GenreResolver.ResolveAsync(db, ownerId, dto.Genres);
            e.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(cfg.ToDto(e));
        });

        group.MapDelete("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var e = await cfg.Set(db).FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (e is null) return Results.NotFound();
            cfg.OnDelete?.Invoke(db, id, ownerId);
            cfg.Set(db).Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---- PATCH /bulk — set the SAME field(s) to the same value(s) across many ids ----
        if (cfg.BulkFields is { Count: > 0 } bulk)
        {
            group.MapMethods("/bulk", new[] { "PATCH" },
                async (BulkUpdateRequest req, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx, CancellationToken ct) =>
            {
                if (req.Ids is not { Length: > 0 })
                    return Results.BadRequest(new { error = "ids must be a non-empty array." });
                if (req.Ids.Distinct().Count() != req.Ids.Length)
                    return Results.BadRequest(new { error = "ids must not contain duplicates." });
                if (req.Updates is not { Count: > 0 })
                    return Results.BadRequest(new { error = "updates must not be empty." });

                foreach (var key in req.Updates.Keys)
                    if (key != "tags" && key != "genres" && !bulk.ContainsKey(key))
                        return Results.BadRequest(new { error = $"Unknown bulk-update field '{key}'." });

                var ownerId = users.GetUserId(ctx.User)!;
                var rows = await cfg.Set(db)
                    .Include(e => e.Tags)
                    .Include(e => e.Genres)
                    .Where(e => req.Ids.Contains(e.Id) && e.OwnerId == ownerId)
                    .ToListAsync(ct);

                // Atomic ownership/isolation: every requested id must exist and
                // belong to the caller, or the whole request is rejected (no
                // partial application).
                if (rows.Count != req.Ids.Length)
                    return Results.NotFound();

                // Tag resolution first (establishes any missing Tag rows for
                // the owner); everything else applies in-memory and persists in
                // the single SaveChangesAsync below.
                if (req.Updates.TryGetValue("tags", out var tagsEl))
                {
                    string[]? names;
                    try
                    {
                        names = tagsEl.ValueKind == JsonValueKind.Null
                            ? null
                            : JsonSerializer.Deserialize<string[]?>(tagsEl.GetRawText());
                    }
                    catch (JsonException)
                    {
                        return Results.BadRequest(new { error = "tags: invalid value for tags." });
                    }
                    var resolved = await TagResolver.ResolveAsync(db, ownerId, names);
                    foreach (var row in rows) row.Tags = resolved;
                }

                if (req.Updates.TryGetValue("genres", out var genresEl))
                {
                    string[]? names;
                    try
                    {
                        names = genresEl.ValueKind == JsonValueKind.Null
                            ? null
                            : JsonSerializer.Deserialize<string[]?>(genresEl.GetRawText());
                    }
                    catch (JsonException)
                    {
                        return Results.BadRequest(new { error = "genres: invalid value for genres." });
                    }
                    var resolved = await GenreResolver.ResolveAsync(db, ownerId, names);
                    foreach (var row in rows) row.Genres = resolved;
                }

                foreach (var (key, value) in req.Updates)
                {
                    if (key == "tags" || key == "genres") continue;
                    var field = bulk[key];
                    foreach (var row in rows)
                    {
                        if (field.Apply(row, value) is { } error)
                            return Results.BadRequest(new { error = $"{key}: {error}" });
                    }
                }

                foreach (var row in rows) row.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(rows.Select(cfg.ToDto));
            });
        }

        return app;
    }
}
