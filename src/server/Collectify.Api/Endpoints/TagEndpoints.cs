using Collectify.Domain.Entities;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class TagEndpoints
{
    public record TagDto(int Id, string Name);
    public record CreateTagRequest(string Name);

    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tags").RequireAuthorization();

        group.MapGet("/", async (CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var tags = await db.Tags.AsNoTracking()
                .Where(t => t.OwnerId == ownerId)
                .OrderBy(t => t.Name)
                .Select(t => new TagDto(t.Id, t.Name))
                .ToListAsync();
            return Results.Ok(tags);
        });

        group.MapPost("/", async ([FromBody] CreateTagRequest req, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var ownerId = users.GetUserId(ctx.User)!;
            var name = req.Name.Trim().ToLowerInvariant();

            var existing = await db.Tags.FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.Name == name);
            if (existing is not null)
                return Results.Ok(new TagDto(existing.Id, existing.Name));

            var tag = new Tag { OwnerId = ownerId, Name = name };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tags/{tag.Id}", new TagDto(tag.Id, tag.Name));
        });

        group.MapDelete("/{id:int}", async (int id, CollectifyDbContext db, UserManager<AppUser> users, HttpContext ctx) =>
        {
            var ownerId = users.GetUserId(ctx.User)!;
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId);
            if (tag is null) return Results.NotFound();
            db.Tags.Remove(tag);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
