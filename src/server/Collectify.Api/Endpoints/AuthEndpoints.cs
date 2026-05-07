using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class AuthEndpoints
{
    public record SetupRequest(string UserName, string Password);
    public record LoginRequest(string UserName, string Password);
    public record AuthState(bool NeedsSetup, bool IsAuthenticated, string? UserName);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapGet("/me", async (HttpContext ctx, UserManager<AppUser> users) =>
        {
            var anyUser = await users.Users.AnyAsync();
            if (!anyUser)
                return Results.Ok(new AuthState(true, false, null));

            if (ctx.User?.Identity?.IsAuthenticated == true)
                return Results.Ok(new AuthState(false, true, ctx.User.Identity.Name));

            return Results.Ok(new AuthState(false, false, null));
        });

        group.MapPost("/setup", async (
            [FromBody] SetupRequest req,
            UserManager<AppUser> users,
            SignInManager<AppUser> signIn) =>
        {
            if (await users.Users.AnyAsync())
                return Results.BadRequest(new { error = "Setup already complete." });

            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Username and password required." });

            var user = new AppUser { UserName = req.UserName };
            var result = await users.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Ok(new { ok = true });
        });

        group.MapPost("/login", async (
            [FromBody] LoginRequest req,
            SignInManager<AppUser> signIn) =>
        {
            var result = await signIn.PasswordSignInAsync(req.UserName, req.Password, isPersistent: true, lockoutOnFailure: false);
            return result.Succeeded
                ? Results.Ok(new { ok = true })
                : Results.Unauthorized();
        });

        group.MapPost("/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok(new { ok = true });
        });

        return app;
    }
}
