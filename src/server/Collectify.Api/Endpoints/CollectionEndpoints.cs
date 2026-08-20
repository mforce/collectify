using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Collectify.Infrastructure.Lookup.Images;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

// The generic handlers need to read the tag names off TDto (`dto.Tags`) without
// knowing the concrete DTO type; each per-type record already declares a
// `Tags` property of this shape, so implementing this marker interface costs
// nothing and keeps the handler bodies free of reflection.
public interface ICollectionEntryDto
{
    string[]? Tags { get; }
}

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
    public Func<IQueryable<TEntity>, HttpRequest, IQueryable<TEntity>>? ExtraFilters { get; init; }
    public Action<CollectifyDbContext, int, string>? OnDelete { get; init; }
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
            var q = cfg.Set(db).AsNoTracking().Include(e => e.Tags).Where(e => e.OwnerId == ownerId);

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

            if (cfg.ExtraFilters is { } extra) q = extra(q, ctx.Request);

            var items = await q.OrderByDescending(e => e.AddedAt).Take(500).ToListAsync();
            return Results.Ok(items.Select(cfg.ToDto));
        });

        group.MapGet("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var e = await cfg.Set(db).AsNoTracking().Include(x => x.Tags)
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
            cfg.Set(db).Add(e);
            await db.SaveChangesAsync(ct);
            return Results.Created($"{cfg.RoutePrefix}/{e.Id}", cfg.ToDto(e));
        });

        group.MapPut("/{id:int}", async (int id, [FromBody] TDto dto, CollectifyDbContext db, UserManager<AppUser> users, ICoverImageStore covers, HttpContext ctx, CancellationToken ct) =>
        {
            if (cfg.Validate(dto) is { } error) return error;
            var ownerId = users.GetUserId(ctx.User)!;
            var e = await cfg.Set(db).Include(x => x.Tags)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
            if (e is null) return Results.NotFound();
            cfg.Apply(e, dto);
            e.ImagePath = await covers.EnsureLocalAsync(e.ImagePath, ct);
            e.Tags = await TagResolver.ResolveAsync(db, ownerId, dto.Tags);
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

        return app;
    }
}
