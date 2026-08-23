using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Collectify.Domain.Entities;
using Collectify.Tests.Infrastructure;
using Collectify.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

/// <summary>
/// Shared skeleton for the three collection endpoint test classes (movies,
/// music, games). Carries every scenario whose behavior is identical across
/// media types; each concrete subclass supplies the per-type shape via the
/// abstract members below and keeps only its own type-specific tests (e.g.
/// movie <c>Formats</c> bitmask round-trips, game platform filters).
///
/// Concrete subclasses use <see cref="CollectifyApiFactory"/> as a
/// class-level <c>IClassFixture</c>: the host (and its in-memory SQLite
/// connection) is shared across every test in the class. Every test creates
/// its own uniquely-named user(s) so ownership-scoping assertions still hold
/// against the shared database, and xUnit runs tests within one class
/// sequentially by default, so the shared DB is never mutated concurrently.
/// </summary>
public abstract class CollectionEndpointsTestsBase<TEntity, TResponse>
    where TEntity : class
    where TResponse : ICollectionResponse
{
    protected readonly CollectifyApiFactory Factory;

    protected CollectionEndpointsTestsBase(CollectifyApiFactory factory) => Factory = factory;

    protected abstract string RoutePrefix { get; }

    protected abstract object Sample(
        string? title = null, string[]? tags = null, string? currency = null, int? rating = null);

    protected abstract object MinimalWithImage(string? imagePath);

    protected abstract TEntity NewMinimalEntity(string ownerId, string title);

    protected abstract int IdOf(TEntity entity);
    protected abstract string OwnerIdOf(TEntity entity);
    protected abstract string TitleOf(TEntity entity);
    protected abstract DateTime UpdatedAtOf(TEntity entity);

    private static string UniqueUser(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected Task<TestExtensions.TestUser> NewAliceAsync() =>
        Factory.CreateAuthenticatedUserAsync(UniqueUser("alice"));

    protected Task<TestExtensions.TestUser> NewBobAsync() =>
        Factory.CreateAuthenticatedUserAsync(UniqueUser("bob"));

    private async Task<TEntity> FindByIdAsync(int id)
    {
        var all = await Factory.WithDbAsync(db => db.Set<TEntity>().AsNoTracking().ToListAsync());
        return all.First(e => IdOf(e) == id);
    }

    private async Task<bool> ExistsAsync(int id)
    {
        var all = await Factory.WithDbAsync(db => db.Set<TEntity>().AsNoTracking().ToListAsync());
        return all.Any(e => IdOf(e) == id);
    }

    // -------- Auth --------

    [Fact]
    public async Task List_Unauthenticated_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(RoutePrefix);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(RoutePrefix, Sample());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------- CRUD happy path --------

    [Fact]
    public async Task Create_AsAuthenticatedUser_Returns201WithBody()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(title: "Created Title"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync<TResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Id > 0);
        Assert.Equal("Created Title", body.Title);
    }

    [Fact]
    public async Task Create_PersistsOwnerIdFromAuthenticatedUser()
    {
        var alice = await NewAliceAsync();

        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample()))
            .ReadJsonAsync<TResponse>();

        var stored = await FindByIdAsync(created!.Id);
        Assert.Equal(alice.Id, OwnerIdOf(stored));
    }

    [Fact]
    public async Task Get_OwnRow_ReturnsRow()
    {
        var alice = await NewAliceAsync();
        var seeded = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Heat"));

        var body = await alice.Client.GetJsonAsync<TResponse>($"{RoutePrefix}{IdOf(seeded)}");

        Assert.Equal(IdOf(seeded), body!.Id);
        Assert.Equal("Heat", body.Title);
    }

    [Fact]
    public async Task Get_NonExistentId_Returns404()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.GetAsync($"{RoutePrefix}999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnRow_PersistsChangesAndBumpsUpdatedAt()
    {
        var alice = await NewAliceAsync();
        var seeded = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Old Title"));
        var originalUpdatedAt = UpdatedAtOf(seeded);

        var response = await alice.Client.PutAsJsonAsync($"{RoutePrefix}{IdOf(seeded)}",
            Sample(title: "New Title"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync<TResponse>();
        Assert.Equal("New Title", body!.Title);
        Assert.True(body.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task Delete_OwnRow_Returns204AndRemovesRow()
    {
        var alice = await NewAliceAsync();
        var seeded = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Heat"));

        var response = await alice.Client.DeleteAsync($"{RoutePrefix}{IdOf(seeded)}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await ExistsAsync(IdOf(seeded)));
    }

    // -------- Ownership boundary --------

    [Fact]
    public async Task Get_OtherUsersRow_Returns404()
    {
        var alice = await NewAliceAsync();
        var bob = await NewBobAsync();
        var aliceRow = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Heat"));

        var response = await bob.Client.GetAsync($"{RoutePrefix}{IdOf(aliceRow)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OtherUsersRow_Returns404()
    {
        var alice = await NewAliceAsync();
        var bob = await NewBobAsync();
        var aliceRow = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Heat"));

        var response = await bob.Client.PutAsJsonAsync($"{RoutePrefix}{IdOf(aliceRow)}",
            Sample(title: "Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var stored = await FindByIdAsync(IdOf(aliceRow));
        Assert.Equal("Heat", TitleOf(stored));
    }

    [Fact]
    public async Task Delete_OtherUsersRow_Returns404AndKeepsRow()
    {
        var alice = await NewAliceAsync();
        var bob = await NewBobAsync();
        var aliceRow = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Heat"));

        var response = await bob.Client.DeleteAsync($"{RoutePrefix}{IdOf(aliceRow)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(await ExistsAsync(IdOf(aliceRow)));
    }

    [Fact]
    public async Task List_OnlyReturnsRowsOwnedByCurrentUser()
    {
        var alice = await NewAliceAsync();
        var bob = await NewBobAsync();
        await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Alice-Row"));
        await Factory.SeedAsync(NewMinimalEntity(bob.Id, "Bob-Row"));

        var aliceList = await alice.Client.GetJsonAsync<TResponse[]>(RoutePrefix);

        Assert.Single(aliceList!);
        Assert.Equal("Alice-Row", aliceList![0].Title);
    }

    // -------- Validation --------

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsBadRequest()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithWhitespaceTitle_ReturnsBadRequest()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(title: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithEmptyTitle_ReturnsBadRequest()
    {
        var alice = await NewAliceAsync();
        var seeded = await Factory.SeedAsync(NewMinimalEntity(alice.Id, "Heat"));

        var response = await alice.Client.PutAsJsonAsync($"{RoutePrefix}{IdOf(seeded)}",
            Sample(title: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Create_WithRatingOutsideRange_ReturnsBadRequest(int rating)
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(rating: rating));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Create_WithRatingAtBoundary_Returns201(int rating)
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(rating: rating));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithCurrencyOfWrongLength_ReturnsBadRequest()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(currency: "EU"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NormalizesCurrencyToUppercase()
    {
        var alice = await NewAliceAsync();

        var body = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(currency: "eur")))
            .ReadJsonAsync<TResponse>();

        Assert.Equal("EUR", body!.AcquisitionCurrency);
    }

    // -------- Cover image caching --------

    [Fact]
    public async Task Create_WithRemoteImageUrl_StoresLocalCoverPath()
    {
        var alice = await NewAliceAsync();

        var dto = MinimalWithImage("https://image.tmdb.org/t/p/w342/poster.jpg");

        var body = await (await alice.Client.PostAsJsonAsync(RoutePrefix, dto))
            .ReadJsonAsync<TResponse>();

        Assert.NotNull(body);
        Assert.NotNull(body!.ImagePath);
        Assert.StartsWith("/covers/", body.ImagePath);
        Assert.DoesNotContain("image.tmdb.org", body.ImagePath);
    }

    [Fact]
    public async Task Create_WithLocalImagePath_PassesThroughUntouched()
    {
        var alice = await NewAliceAsync();

        var dto = MinimalWithImage("/covers/already-cached.jpg");

        var body = await (await alice.Client.PostAsJsonAsync(RoutePrefix, dto))
            .ReadJsonAsync<TResponse>();

        Assert.Equal("/covers/already-cached.jpg", body!.ImagePath);
    }

    // -------- Tags --------

    [Fact]
    public async Task Create_WithTags_CreatesTagsAndAttachesThem()
    {
        var alice = await NewAliceAsync();

        var body = await (await alice.Client.PostAsJsonAsync(RoutePrefix,
            Sample(tags: ["Sci-Fi", "Heist", "Nolan"])))
            .ReadJsonAsync<TResponse>();

        Assert.Equal(new[] { "heist", "nolan", "sci-fi" }, body!.Tags);

        var tagCount = await Factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        Assert.Equal(3, tagCount);
    }

    [Fact]
    public async Task Create_WithDuplicateTagsInArray_DeduplicatesIgnoreCase()
    {
        var alice = await NewAliceAsync();

        var body = await (await alice.Client.PostAsJsonAsync(RoutePrefix,
            Sample(tags: ["Sci-Fi", "sci-fi", "  Sci-Fi  ", "Heist"])))
            .ReadJsonAsync<TResponse>();

        Assert.Equal(new[] { "heist", "sci-fi" }, body!.Tags);
    }

    [Fact]
    public async Task Update_ReplacesTagSetRatherThanMerging()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix,
            Sample(tags: ["Sci-Fi", "Heist"])))
            .ReadJsonAsync<TResponse>();

        var updated = await (await alice.Client.PutAsJsonAsync($"{RoutePrefix}{created!.Id}",
            Sample(tags: ["Drama"])))
            .ReadJsonAsync<TResponse>();

        Assert.Equal(new[] { "drama" }, updated!.Tags);
    }

    [Fact]
    public async Task Update_WithEmptyTagArray_RemovesAllTags()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix,
            Sample(tags: ["Sci-Fi", "Heist"])))
            .ReadJsonAsync<TResponse>();

        var updated = await (await alice.Client.PutAsJsonAsync($"{RoutePrefix}{created!.Id}",
            Sample(tags: Array.Empty<string>())))
            .ReadJsonAsync<TResponse>();

        Assert.Empty(updated!.Tags);
    }

    [Fact]
    public async Task Delete_RemovesJoinRowsButKeepsTagEntity()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix,
            Sample(tags: ["Sci-Fi"])))
            .ReadJsonAsync<TResponse>();

        var delete = await alice.Client.DeleteAsync($"{RoutePrefix}{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var tagsLeft = await Factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        Assert.Equal(1, tagsLeft);
    }

    [Fact]
    public async Task Tags_AreOwnerScoped_BetweenUsers()
    {
        var alice = await NewAliceAsync();
        var bob = await NewBobAsync();

        await alice.Client.PostAsJsonAsync(RoutePrefix, Sample(tags: ["sci-fi"]));
        await bob.Client.PostAsJsonAsync(RoutePrefix, Sample(title: "Bob's", tags: ["sci-fi"]));

        // Owner-scoped rather than a global count: the class-level shared
        // host fixture means the in-memory DB accumulates rows from every
        // test in this class, so only per-owner counts stay meaningful.
        var aliceTags = await Factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == alice.Id));
        var bobTags = await Factory.WithDbAsync(db =>
            db.Tags.CountAsync(t => t.OwnerId == bob.Id));
        Assert.Equal(1, aliceTags);
        Assert.Equal(1, bobTags);
    }

    // -------- Bulk update --------

    private async Task<int> CreateAndGetIdAsync(HttpClient client, string? title = null, int? rating = null)
    {
        var response = await client.PostAsJsonAsync(RoutePrefix, Sample(title: title, rating: rating));
        var body = await response.ReadJsonAsync<TResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task BulkUpdate_AsOwner_SetsOnlyNamedFields()
    {
        var alice = await NewAliceAsync();
        var id1 = await CreateAndGetIdAsync(alice.Client, "Alpha", rating: 7);
        var id2 = await CreateAndGetIdAsync(alice.Client, "Beta", rating: 7);
        var id3 = await CreateAndGetIdAsync(alice.Client, "Gamma", rating: 7);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id1, id2, id3 }, updates = new { status = "Sold" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetArrayLength());
        foreach (var item in body.EnumerateArray())
        {
            Assert.Equal("Sold", item.GetProperty("status").GetString());
            // Pins the partial-update guarantee: a status-only update must
            // not disturb an unrelated field already set on the row.
            Assert.Equal(7, item.GetProperty("personalRating").GetInt32());
        }

        Assert.Equal("Alpha", TitleOf(await FindByIdAsync(id1)));
        Assert.Equal("Beta", TitleOf(await FindByIdAsync(id2)));
        Assert.Equal("Gamma", TitleOf(await FindByIdAsync(id3)));
    }

    [Fact]
    public async Task BulkUpdate_Tags_ViaBulkEndpoint()
    {
        var alice = await NewAliceAsync();
        var id1 = await CreateAndGetIdAsync(alice.Client, "Alpha");
        var id2 = await CreateAndGetIdAsync(alice.Client, "Beta");

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id1, id2 }, updates = new { tags = new[] { "classic" } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetArrayLength());
        foreach (var item in body.EnumerateArray())
        {
            var tags = item.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToArray();
            Assert.Contains("classic", tags);
        }
    }

    [Fact]
    public async Task BulkUpdate_IncludesForeignOrUnknownId_Returns404()
    {
        var alice = await NewAliceAsync();
        var bob = await NewBobAsync();
        var aliceId1 = await CreateAndGetIdAsync(alice.Client, "Alice One");
        var aliceId2 = await CreateAndGetIdAsync(alice.Client, "Alice Two");
        var bobId = await CreateAndGetIdAsync(bob.Client, "Bob One");

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { aliceId1, aliceId2, bobId }, updates = new { status = "Sold" } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Alice One", TitleOf(await FindByIdAsync(aliceId1)));
        Assert.Equal("Alice Two", TitleOf(await FindByIdAsync(aliceId2)));
        Assert.Equal("Bob One", TitleOf(await FindByIdAsync(bobId)));
    }

    [Fact]
    public async Task BulkUpdate_UndefinedEnumValue_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { status = "NotARealStatus" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_UndefinedEnumNumeric_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { status = 99 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_EnumValue_Overflows_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { status = "999999999999999999999999" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_HoursPlayed_Negative_Returns400()
    {
        // hoursPlayed is games-only; kept here (not a games-specific test
        // class) per the allow-listed file for this fix.
        if (RoutePrefix != "/api/games/") return;

        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { hoursPlayed = -1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entity = await FindByIdAsync(id);
        if (entity is Game game) Assert.Null(game.HoursPlayed);
    }

    [Fact]
    public async Task BulkUpdate_HoursPlayed_NonNegative_Returns200()
    {
        if (RoutePrefix != "/api/games/") return;

        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { hoursPlayed = 3 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entity = await FindByIdAsync(id);
        if (entity is Game game) Assert.Equal(3, game.HoursPlayed);
    }

    [Fact]
    public async Task BulkUpdate_UnknownField_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { nonexistentField = 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("nonexistentField", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task BulkUpdate_Tags_MalformedValue_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { tags = "not-an-array" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_UnknownFieldWithTags_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { tags = new[] { "x" }, bogusField = 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("bogusField", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task BulkUpdate_EmptyIds_Returns400()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = Array.Empty<int>(), updates = new { status = "Sold" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_DuplicateIds_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id, id }, updates = new { status = "Sold" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_EmptyUpdates_Returns400()
    {
        var alice = await NewAliceAsync();
        var id = await CreateAndGetIdAsync(alice.Client);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { id }, updates = new { } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_Unauthenticated_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();

        var response = await client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { 1 }, updates = new { status = "Sold" } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
