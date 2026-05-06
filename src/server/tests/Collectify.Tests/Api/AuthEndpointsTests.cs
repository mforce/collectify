using System.Net;
using System.Net.Http.Json;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class AuthEndpointsTests
{
    private record AuthState(bool NeedsSetup, bool IsAuthenticated, string? UserName);

    [Fact]
    public async Task GetMe_BeforeAnyUserExists_ReturnsNeedsSetup()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<AuthState>();
        Assert.NotNull(state);
        Assert.True(state!.NeedsSetup);
        Assert.False(state.IsAuthenticated);
        Assert.Null(state.UserName);
    }

    [Fact]
    public async Task Setup_FirstTime_CreatesUserAndSignsIn()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var setup = await client.PostAsJsonAsync("/api/auth/setup",
            new { UserName = "alice", Password = "Test-Password-1" });
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        var me = await client.GetFromJsonAsync<AuthState>("/api/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.NeedsSetup);
        Assert.True(me.IsAuthenticated);
        Assert.Equal("alice", me.UserName);
    }

    [Fact]
    public async Task Setup_WhenUserAlreadyExists_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.CreateAuthenticatedUserAsync("alice");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/setup",
            new { UserName = "bob", Password = "Test-Password-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Setup_WithMissingUsername_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/setup",
            new { UserName = "", Password = "Test-Password-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Setup_WithShortPassword_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/setup",
            new { UserName = "alice", Password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_AuthenticatesSubsequentRequests()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.CreateAuthenticatedUserAsync("alice");

        var freshClient = factory.CreateClient();
        var login = await freshClient.PostAsJsonAsync("/api/auth/login",
            new { UserName = "alice", Password = TestExtensions.DefaultPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await freshClient.GetFromJsonAsync<AuthState>("/api/auth/me");
        Assert.True(me!.IsAuthenticated);
        Assert.Equal("alice", me.UserName);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.CreateAuthenticatedUserAsync("alice");

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = "alice", Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        await using var factory = new CollectifyApiFactory();
        await factory.CreateAuthenticatedUserAsync("alice");

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = "ghost", Password = TestExtensions.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsAuthenticationCookie()
    {
        await using var factory = new CollectifyApiFactory();
        var alice = await factory.CreateAuthenticatedUserAsync("alice");

        var logout = await alice.Client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var me = await alice.Client.GetFromJsonAsync<AuthState>("/api/auth/me");
        Assert.False(me!.IsAuthenticated);
    }
}
