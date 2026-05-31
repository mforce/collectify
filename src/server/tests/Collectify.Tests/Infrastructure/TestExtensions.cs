using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Collectify.Infrastructure.Data;
using Collectify.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Collectify.Tests.Infrastructure;

public static class TestExtensions
{
    public const string DefaultPassword = "Test-Password-1";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<T?> GetJsonAsync<T>(this HttpClient client, string url) =>
        client.GetFromJsonAsync<T>(url, JsonOptions);

    public static Task<T?> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(JsonOptions);

    public sealed record TestUser(string Id, string UserName, HttpClient Client);

    public static async Task<TestUser> CreateAuthenticatedUserAsync(
        this CollectifyApiFactory factory,
        string userName,
        string password = DefaultPassword)
    {
        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var existing = await users.FindByNameAsync(userName);
            if (existing is not null)
            {
                userId = existing.Id;
            }
            else
            {
                var user = new AppUser { UserName = userName };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Could not create test user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
                userId = user.Id;
            }
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { UserName = userName, Password = password });
        login.EnsureSuccessStatusCode();
        return new TestUser(userId, userName, client);
    }

    public static async Task<T> SeedAsync<T>(this CollectifyApiFactory factory, T entity) where T : class
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectifyDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public static async Task<TResult> WithDbAsync<TResult>(this CollectifyApiFactory factory, Func<CollectifyDbContext, Task<TResult>> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectifyDbContext>();
        return await action(db);
    }

    public static async Task<T?> PostMultipartAndReadJsonAsync<T>(
        this HttpClient client, string url, HttpContent content)
    {
        var response = await client.PostAsync(url, content);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}
