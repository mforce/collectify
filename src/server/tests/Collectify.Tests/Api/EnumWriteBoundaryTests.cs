using System.Net;
using System.Text;
using Collectify.Tests.Infrastructure;

namespace Collectify.Tests.Api;

/// <summary>
/// Issue #115 — the JSON write boundary must reject enum values that are not
/// defined members, regardless of whether the client sends the member as a
/// string ("Owned") or as an integer (its underlying value), and must reject
/// arbitrary integers for [Flags] bitmask fields. Pre-#115 the default
/// JsonStringEnumConverter(allowIntegerValues: true) bound any integer to an
/// unnamed enum value and persisted it (e.g. "status": 999) — the row only
/// "healed" at next restart.
///
/// Each enum is exercised once, through the endpoint that exposes it. Bodies
/// carry only the exact token under test, on top of a minimal valid record so
/// a 400 cannot be confused with a missing-required-field response.
/// </summary>
public class EnumWriteBoundaryTests
{
    private static string MovieBody(string fieldToken) =>
        $@"{{""title"":""Inception"",""formats"":2,""watchCount"":0,{fieldToken}}}";

    private static string MusicBody(string fieldToken) =>
        $@"{{""title"":""Kind of Blue"",""artistName"":""Miles Davis"",{fieldToken}}}";

    private static string GameBody(string fieldToken) =>
        $@"{{""title"":""Hades"",""platform"":""Pc"",""isDigital"":true,{fieldToken}}}";

    /// <summary>(resource/userName key, path, field, valid-integer body, valid-string body)</summary>
    public static IEnumerable<object[]> EnumEndpoints => new List<object[]>
    {
        new object[] { "movie", "/api/movies/", "status",          MovieBody(@"""status"":0"),            MovieBody(@"""status"":""OnOrder""") },
        new object[] { "movie", "/api/movies/", "watchStatus",     MovieBody(@"""watchStatus"":2"),      MovieBody(@"""watchStatus"":""Unwatched""") },
        new object[] { "movie", "/api/movies/", "condition",       MovieBody(@"""condition"":0"),        MovieBody(@"""condition"":""Poor""") },
        new object[] { "music", "/api/music/",  "format",          MusicBody(@"""format"":0"),           MusicBody(@"""format"":""Vinyl""") },
        new object[] { "game",  "/api/games/",  "completionStatus",GameBody(@"""completionStatus"":2"), GameBody(@"""completionStatus"":""NotStarted""") },
        new object[] { "game",  "/api/games/",  "digitalStore",    GameBody(@"""digitalStore"":0"),      GameBody(@"""digitalStore"":""Epic""") },
    };

    /// <summary>(resource/userName key, path, valid body with the field set to an undefined integer)</summary>
    public static IEnumerable<object[]> BadIntBodies => new List<object[]>
    {
        new object[] { "movie", "/api/movies/", MovieBody(@"""status"":999") },
        new object[] { "movie", "/api/movies/", MovieBody(@"""watchStatus"":999") },
        new object[] { "movie", "/api/movies/", MovieBody(@"""condition"":999") },
        new object[] { "music", "/api/music/",  MusicBody(@"""format"":999") },
        new object[] { "game",  "/api/games/",  GameBody(@"""completionStatus"":999") },
        new object[] { "game",  "/api/games/",  GameBody(@"""digitalStore"":999") },
    };

    /// <summary>(resource/userName key, path, valid body with the field set to an unknown string)</summary>
    public static IEnumerable<object[]> BadStringBodies => new List<object[]>
    {
        new object[] { "movie", "/api/movies/", MovieBody(@"""status"":""NotARealMember""") },
        // A numeric-looking string ("999") is NOT a defined member name; the
        // string branch's Enum.IsDefined must reject it, not TryParse-pass it
        // into an unnamed enum value.
        new object[] { "movie", "/api/movies/", MovieBody(@"""status"":""999""") },
        new object[] { "movie", "/api/movies/", MovieBody(@"""watchStatus"":""NotARealMember""") },
        new object[] { "movie", "/api/movies/", MovieBody(@"""condition"":""NotARealMember""") },
        new object[] { "music", "/api/music/",  MusicBody(@"""format"":""NotARealMember""") },
        new object[] { "game",  "/api/games/",  GameBody(@"""completionStatus"":""NotARealMember""") },
        new object[] { "game",  "/api/games/",  GameBody(@"""digitalStore"":""NotARealMember""") },
    };

    // A defined-integer send must continue to bind (the pre-existing contract),
    // and a string-name send must bind (what the real client sends).
    [Theory]
    [MemberData(nameof(EnumEndpoints))]
    public async Task DefinedEnum_IntegerAndString_BothBind(string resource, string path, string field, string validIntBody, string validStringBody)
    {
        await using var factory = new CollectifyApiFactory();
        var user = await factory.CreateAuthenticatedUserAsync($"u_{resource}{field}_def");

        var asInt = await user.Client.PostAsync(path, Json(validIntBody));
        Assert.Equal(HttpStatusCode.Created, asInt.StatusCode);

        var asString = await user.Client.PostAsync(path, Json(validStringBody));
        Assert.Equal(HttpStatusCode.Created, asString.StatusCode);
    }

    // The core #115 regression: an arbitrary integer must not persist.
    [Theory]
    [MemberData(nameof(BadIntBodies))]
    public async Task UndefinedInteger_ReturnsBadRequest(string resource, string path, string bodyWithBadInt)
    {
        await using var factory = new CollectifyApiFactory();
        var user = await factory.CreateAuthenticatedUserAsync($"u_{resource}{bodyWithBadInt.Length}_undef");

        var response = await user.Client.PostAsync(path, Json(bodyWithBadInt));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Unknown strings already 400 pre-#115 (the default converter throws);
    // pinned here so the future can't silently regress to persisting them.
    [Theory]
    [MemberData(nameof(BadStringBodies))]
    public async Task UnknownString_ReturnsBadRequest(string resource, string path, string bodyWithBadString)
    {
        await using var factory = new CollectifyApiFactory();
        var user = await factory.CreateAuthenticatedUserAsync($"u_{resource}{bodyWithBadString.Length}_str");

        var response = await user.Client.PostAsync(path, Json(bodyWithBadString));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovieFormats_UndefinedBits_ReturnsBadRequest()
    {
        await using var factory = new CollectifyApiFactory();
        var user = await factory.CreateAuthenticatedUserAsync("u_fmt_undef");

        // 999 = bits outside Dvd|BluRay|UhdBluRay|Vhs|Digital.
        var response = await user.Client.PostAsync("/api/movies/",
            Json(@"{""title"":""Inception"",""formats"":999,""watchStatus"":""Unwatched"",""watchCount"":0}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovieFormats_ValidCombination_ReturnsCreated()
    {
        await using var factory = new CollectifyApiFactory();
        var user = await factory.CreateAuthenticatedUserAsync("u_fmt_ok");

        // Dvd|BluRay = 3 is a valid flags combination.
        var response = await user.Client.PostAsync("/api/movies/",
            Json(@"{""title"":""Inception"",""formats"":3,""watchStatus"":""Unwatched"",""watchCount"":0}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MovieFormats_None_ReturnsCreated()
    {
        await using var factory = new CollectifyApiFactory();
        var user = await factory.CreateAuthenticatedUserAsync("u_fmt_none");

        var response = await user.Client.PostAsync("/api/movies/",
            Json(@"{""title"":""Inception"",""formats"":0,""watchStatus"":""Unwatched"",""watchCount"":0}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
