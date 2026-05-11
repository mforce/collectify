using System.Net;
using System.Net.Http.Json;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

public class AuthEndpointsTests
{
    private record AuthState(bool NeedsSetup, bool IsAuthenticated, string? UserName, bool AllowRegistration);

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

    // ---------- /api/auth/me allowRegistration ----------

    [Fact]
    public async Task GetMe_ExposesAllowRegistrationFromConfig()
    {
        await using var off = new CollectifyApiFactory();
        var meOff = await off.CreateClient().GetFromJsonAsync<AuthState>("/api/auth/me");
        Assert.False(meOff!.AllowRegistration);

        await using var on = new CollectifyApiFactory { AllowRegistration = true };
        var meOn = await on.CreateClient().GetFromJsonAsync<AuthState>("/api/auth/me");
        Assert.True(meOn!.AllowRegistration);
    }

    // ---------- /api/auth/register ----------

    [Fact]
    public async Task Register_WhenDisabled_Returns404()
    {
        // Default factory: AllowRegistration=false. The endpoint should
        // act like it doesn't exist so the client can use a single
        // signal (404) to decide whether to surface the link.
        await using var factory = new CollectifyApiFactory();
        await factory.CreateAuthenticatedUserAsync("alice");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "bob", Password = "Test-Password-2" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenEnabledAndNoUsersExist_ReturnsBadRequest()
    {
        // First-run still belongs to /setup. /register refuses before
        // the admin bootstrap has run so a stranger hitting the raw URL
        // doesn't preempt the install owner.
        await using var factory = new CollectifyApiFactory { AllowRegistration = true };
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "first", Password = "Test-Password-2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenEnabledAndSetupDone_CreatesUserAndSignsIn()
    {
        await using var factory = new CollectifyApiFactory { AllowRegistration = true };
        await factory.CreateAuthenticatedUserAsync("alice"); // admin already exists

        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "bob", Password = "Test-Password-2" });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        // The endpoint signs the new user in -- the same HttpClient
        // should now see itself as bob.
        var me = await client.GetFromJsonAsync<AuthState>("/api/auth/me");
        Assert.True(me!.IsAuthenticated);
        Assert.Equal("bob", me.UserName);
    }

    [Fact]
    public async Task Register_WithDuplicateUserName_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory { AllowRegistration = true };
        await factory.CreateAuthenticatedUserAsync("alice");

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "alice", Password = "Test-Password-2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithBlankFields_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory { AllowRegistration = true };
        await factory.CreateAuthenticatedUserAsync("alice");

        var client = factory.CreateClient();
        var noName = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "", Password = "Test-Password-2" });
        var noPass = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "bob", Password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPass.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory { AllowRegistration = true };
        await factory.CreateAuthenticatedUserAsync("alice");

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "bob", Password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
