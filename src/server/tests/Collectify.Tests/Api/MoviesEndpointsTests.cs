using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Collectify.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class MoviesEndpointsTests : CollectionEndpointsTestsBase<Movie, MovieResponse>, IClassFixture<CollectifyApiFactory>
{
    public MoviesEndpointsTests(CollectifyApiFactory factory) : base(factory)
    {
    }

    protected override string RoutePrefix => "/api/movies/";

    protected override object Sample(string? title = null, string[]? tags = null, string[]? genres = null, string? currency = null, int? rating = null) =>
        MovieTestSupport.Sample(title: title ?? "Inception", tags: tags, genres: genres, currency: currency, rating: rating);

    protected override object MinimalWithImage(string? imagePath) => new
    {
        Title = "Inception",
        Formats = (int)MovieFormat.BluRay,
        Status = CollectionStatus.Owned,
        WatchStatus = WatchStatus.Unwatched,
        WatchCount = 0,
        ImagePath = imagePath,
        Tags = (string[]?)null,
    };

    protected override Movie NewMinimalEntity(string ownerId, string title) => new()
    {
        OwnerId = ownerId,
        Title = title,
        UpdatedAt = DateTime.UtcNow.AddDays(-1),
    };

    protected override Movie NewSortableEntity(
        string ownerId, string title, int? year = null, int? personalRating = null, DateTime? addedAt = null) => new()
    {
        OwnerId = ownerId,
        Title = title,
        Year = year,
        PersonalRating = personalRating,
        AddedAt = addedAt ?? DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow.AddDays(-1),
    };

    protected override int IdOf(Movie entity) => entity.Id;
    protected override string OwnerIdOf(Movie entity) => entity.OwnerId;
    protected override string TitleOf(Movie entity) => entity.Title;
    protected override DateTime UpdatedAtOf(Movie entity) => entity.UpdatedAt;

    protected override Task<int> GenreLinkCountAsync(int itemId) =>
        Factory.WithDbAsync(db => db.Set<Genre>().AsNoTracking().CountAsync(g => g.Movies.Any(m => m.Id == itemId)));

    // -------- Formats (flags enum as integer) --------

    [Fact]
    public async Task Create_WithFormatsAsInteger_RoundTripsFlags()
    {
        var alice = await NewAliceAsync();

        // Frontend sends formats as a bitwise integer (3 = Dvd | BluRay).
        // Use raw JSON to simulate what the browser actually sends —
        // PostAsJsonAsync would stringify enums as the server expects.
        var json = "{\"Title\":\"Inception\",\"Formats\":3,\"Status\":\"Owned\",\"WatchStatus\":\"Unwatched\",\"WatchCount\":0}";
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await alice.Client.PostAsync("/api/movies/", content);
        var raw = await response.Content.ReadAsStringAsync();

        // The response must return formats as an integer so the frontend
        // can use bitwise ops. A string like "BluyRay" breaks ((v & 1) !== 0).
        var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw);
        Assert.Equal(System.Text.Json.JsonValueKind.Number, parsed.GetProperty("formats").ValueKind);
        Assert.Equal(3, parsed.GetProperty("formats").GetInt32());

        // Verify the underlying entity stored the correct flags.
        var stored = await Factory.WithDbAsync(db =>
            db.Movies.Where(m => m.Title == "Inception" && m.OwnerId == alice.Id).FirstAsync());
        Assert.Equal(MovieFormat.Dvd | MovieFormat.BluRay, stored.Formats);
    }

    [Fact]
    public async Task Update_WithFormatsAsInteger_PersistsNewFlags()
    {
        var alice = await NewAliceAsync();
        var movie = await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Heat" });

        // Send all three flags as integer 7.
        var json = "{\"Title\":\"Heat\",\"Formats\":7,\"Status\":\"Owned\",\"WatchStatus\":\"Unwatched\",\"WatchCount\":0}";
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await alice.Client.PutAsync($"/api/movies/{movie.Id}", content);
        var raw = await response.Content.ReadAsStringAsync();

        var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw);
        Assert.Equal(System.Text.Json.JsonValueKind.Number, parsed.GetProperty("formats").ValueKind);
        Assert.Equal(7, parsed.GetProperty("formats").GetInt32());
    }

    [Fact]
    public async Task Create_WithNewFormats_VhsAndDigital()
    {
        var alice = await NewAliceAsync();

        // Vhs(8) | Digital(16) = 24.
        var json = "{\"Title\":\"Retro Movie\",\"Formats\":24,\"Status\":\"Owned\",\"WatchStatus\":\"Unwatched\",\"WatchCount\":0}";
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await alice.Client.PostAsync("/api/movies/", content);
        var raw = await response.Content.ReadAsStringAsync();

        var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw);
        Assert.Equal(System.Text.Json.JsonValueKind.Number, parsed.GetProperty("formats").ValueKind);
        Assert.Equal(24, parsed.GetProperty("formats").GetInt32());

        var createdId = parsed.GetProperty("id").GetInt32();
        var stored = await Factory.WithDbAsync(db =>
            db.Movies.FirstAsync(m => m.Id == createdId));
        Assert.Equal(MovieFormat.Vhs | MovieFormat.Digital, stored.Formats);
    }

    // -------- Personal / acquisition / watch fields round-trip --------

    [Fact]
    public async Task CreateAndGet_RoundTripsRichDetailFields()
    {
        var alice = await NewAliceAsync();
        var response = await alice.Client.PostAsJsonAsync("/api/movies/", new
        {
            Title = "Inception",
            Formats = (int)MovieFormat.BluRay,
            Status = CollectionStatus.Owned,
            WatchStatus = WatchStatus.Unwatched,
            WatchCount = 0,
            ReleaseDate = new DateOnly(2010, 7, 15),
            Cast = "Leonardo DiCaprio, Joseph Gordon-Levitt",
            ProviderRating = 8.4f,
        });
        var created = await response.ReadJsonAsync<MovieResponse>();

        var fetched = await alice.Client.GetJsonAsync<MovieResponse>($"/api/movies/{created!.Id}");

        Assert.Equal(new DateOnly(2010, 7, 15), fetched!.ReleaseDate);
        Assert.Equal("Leonardo DiCaprio, Joseph Gordon-Levitt", fetched.Cast);
        Assert.Equal(8.4f, fetched.ProviderRating);
    }

    [Theory]
    [InlineData(11f)]
    [InlineData(-1f)]
    public async Task Create_WithProviderRatingOutOfRange_ReturnsBadRequest(float providerRating)
    {
        var alice = await NewAliceAsync();
        var response = await alice.Client.PostAsJsonAsync("/api/movies/", new
        {
            Title = "Inception",
            Formats = (int)MovieFormat.BluRay,
            Status = CollectionStatus.Owned,
            WatchStatus = WatchStatus.Unwatched,
            WatchCount = 0,
            ProviderRating = providerRating,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ProviderRating must be between 0 and 10.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/movies/", MovieTestSupport.Sample(
            rating: 9,
            condition: Condition.LikeNew,
            currency: "USD",
            watchStatus: WatchStatus.Watched,
            watchCount: 3));

        var body = await response.ReadJsonAsync<MovieResponse>();
        Assert.Equal("A heist on the subconscious.", body!.Description);
        Assert.Equal(9, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(Condition.LikeNew, body.Condition);
        Assert.Equal(new DateOnly(2024, 1, 15), body.AcquiredOn);
        Assert.Equal(19.99m, body.AcquisitionPrice);
        Assert.Equal("USD", body.AcquisitionCurrency);
        Assert.Equal("Amazon", body.AcquisitionSource);
        Assert.Equal(WatchStatus.Watched, body.WatchStatus);
        Assert.Equal(new DateOnly(2024, 6, 1), body.LastWatchedOn);
        Assert.Equal(3, body.WatchCount);
    }

    // -------- List filters --------

    [Fact]
    public async Task List_FiltersByQuery_MatchesTitleSubstring()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Inception" });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Interstellar" });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "The Matrix" });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?query=inter");

        Assert.Single(hits!);
        Assert.Equal("Interstellar", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByYear_ReturnsExactMatches()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "A", Year = 2010 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "B", Year = 2010 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "C", Year = 2020 });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?year=2010");

        Assert.Equal(2, hits!.Length);
        Assert.All(hits, m => Assert.Equal(2010, m.Year));
    }

    [Fact]
    public async Task List_FiltersByYearRange_InclusiveBothEnds()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Old", Year = 1999 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Mid", Year = 2010 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Top", Year = 2020 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Future", Year = 2025 });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>(
            "/api/movies/?yearFrom=2000&yearTo=2020");

        Assert.Equal(2, hits!.Length);
        Assert.All(hits, m => Assert.InRange(m.Year ?? 0, 2000, 2020));
    }

    [Fact]
    public async Task List_FiltersByDirector_StudioGenre_ExactMembership()
    {
        var alice = await NewAliceAsync();

        await alice.Client.PostAsJsonAsync("/api/movies/",
            new { Title = "Inception", Director = "Christopher Nolan", Studio = "Warner Bros", Formats = (int)MovieFormat.BluRay, Status = CollectionStatus.Owned, Genres = new[] { "sci-fi", "action" } });
        await alice.Client.PostAsJsonAsync("/api/movies/",
            new { Title = "Tenet", Director = "Christopher Nolan", Studio = "Warner Bros", Formats = (int)MovieFormat.BluRay, Status = CollectionStatus.Owned, Genres = new[] { "sci-fi" } });
        await alice.Client.PostAsJsonAsync("/api/movies/",
            new { Title = "Goodfellas", Director = "Martin Scorsese", Studio = "Warner Bros", Formats = (int)MovieFormat.BluRay, Status = CollectionStatus.Owned, Genres = new[] { "crime" } });

        var byDirector = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?director=Nolan");
        Assert.Equal(2, byDirector!.Length);

        var byStudio = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?studio=Warner");
        Assert.Equal(3, byStudio!.Length);

        var byGenre = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?genre=sci-fi");
        Assert.Equal(2, byGenre!.Length);

        var byPartial = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?genre=sci");
        Assert.Empty(byPartial!);
    }

    [Fact]
    public async Task List_FiltersByStatusAndWatchStatusAndRatingMin()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Owned-Watched-9", Status = CollectionStatus.Owned, WatchStatus = WatchStatus.Watched, PersonalRating = 9 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Owned-Unwatched", Status = CollectionStatus.Owned, WatchStatus = WatchStatus.Unwatched, PersonalRating = 5 });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Wishlist",        Status = CollectionStatus.Wishlist });

        var byStatus = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?status=Wishlist");
        var hit = Assert.Single(byStatus!);
        Assert.Equal("Wishlist", hit.Title);

        var byWatch = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?watchStatus=Watched");
        Assert.Single(byWatch!);

        var byRating = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?ratingMin=7");
        Assert.Single(byRating!);
        Assert.Equal("Owned-Watched-9", byRating![0].Title);
    }

    [Fact]
    public async Task List_FiltersByTag_OrSemanticsAcrossMultipleValues()
    {
        var alice = await NewAliceAsync();

        await Factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "scifi" });
        await Factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "noir" });
        await Factory.SeedAsync(new Tag { OwnerId = alice.Id, Name = "comedy" });

        await alice.Client.PostAsJsonAsync("/api/movies/",
            new { Title = "Blade Runner", Year = 1982, Formats = (int)MovieFormat.BluRay, Status = CollectionStatus.Owned, WatchStatus = WatchStatus.Watched, WatchCount = 0, Tags = new[] { "scifi", "noir" } });
        await alice.Client.PostAsJsonAsync("/api/movies/",
            new { Title = "Airplane",     Year = 1980, Formats = (int)MovieFormat.Dvd,    Status = CollectionStatus.Owned, WatchStatus = WatchStatus.Watched, WatchCount = 0, Tags = new[] { "comedy" } });
        await alice.Client.PostAsJsonAsync("/api/movies/",
            new { Title = "Casablanca",   Year = 1942, Formats = (int)MovieFormat.Dvd,    Status = CollectionStatus.Owned, WatchStatus = WatchStatus.Watched, WatchCount = 0, Tags = new[] { "noir" } });

        // Single tag.
        var byScifi = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?tag=scifi");
        Assert.Single(byScifi!);
        Assert.Equal("Blade Runner", byScifi![0].Title);

        // Multi-value OR: scifi or noir matches Blade Runner *and* Casablanca.
        var byEither = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?tag=scifi&tag=noir");
        Assert.Equal(2, byEither!.Length);
        Assert.Contains(byEither, m => m.Title == "Blade Runner");
        Assert.Contains(byEither, m => m.Title == "Casablanca");
    }

    [Fact]
    public async Task List_CombinesFiltersWithAndSemantics()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Inception", Year = 2010, Director = "Nolan",     Status = CollectionStatus.Owned });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Tenet",     Year = 2020, Director = "Nolan",     Status = CollectionStatus.Owned });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Goodfellas",Year = 1990, Director = "Scorsese",  Status = CollectionStatus.Owned });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>(
            "/api/movies/?director=Nolan&yearFrom=2015");

        Assert.Single(hits!);
        Assert.Equal("Tenet", hits![0].Title);
    }

    [Fact]
    public async Task List_FiltersByFormat_FlagsCombination_ReturnsMoviesSharingAnyBit()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Inception", Formats = MovieFormat.Dvd | MovieFormat.BluRay });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Tenet", Formats = MovieFormat.Dvd });
        await Factory.SeedAsync(new Movie { OwnerId = alice.Id, Title = "Goodfellas", Formats = MovieFormat.Vhs });

        var hits = await alice.Client.GetJsonAsync<MovieResponse[]>("/api/movies/?format=3");

        var titles = hits!.Select(m => m.Title).ToArray();
        Assert.Contains("Inception", titles);
        Assert.Contains("Tenet", titles);
        Assert.DoesNotContain("Goodfellas", titles);
    }

    // -------- Bulk update (movie-specific whitelist) --------

    [Fact]
    public async Task BulkUpdate_MovieWatchStatus_SetsValue()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample()))
            .ReadJsonAsync<MovieResponse>();

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { created!.Id }, updates = new { watchStatus = "Watched" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await alice.Client.GetJsonAsync<MovieResponse>($"{RoutePrefix}{created.Id}");
        Assert.Equal(WatchStatus.Watched, fetched!.WatchStatus);
    }

    [Fact]
    public async Task BulkUpdate_MovieFormatsNotBulkUpdatable_Returns400()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample()))
            .ReadJsonAsync<MovieResponse>();

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { created!.Id }, updates = new { formats = 3 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unknown bulk-update field 'formats'.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task BulkUpdate_EnumValue_CaseInsensitive()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample()))
            .ReadJsonAsync<MovieResponse>();

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { created!.Id }, updates = new { status = "sold" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await alice.Client.GetJsonAsync<MovieResponse>($"{RoutePrefix}{created.Id}");
        Assert.Equal(CollectionStatus.Sold, fetched!.Status);
    }

    [Fact]
    public async Task BulkUpdate_NullableCondition_Clears()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix,
            MovieTestSupport.Sample(condition: Condition.LikeNew)))
            .ReadJsonAsync<MovieResponse>();
        Assert.Equal(Condition.LikeNew, created!.Condition);

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { created.Id }, updates = new { condition = (Condition?)null } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await alice.Client.GetJsonAsync<MovieResponse>($"{RoutePrefix}{created.Id}");
        Assert.Null(fetched!.Condition);
    }

    [Fact]
    public async Task BulkUpdate_NullableCondition_CaseInsensitive()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample()))
            .ReadJsonAsync<MovieResponse>();

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { created!.Id }, updates = new { condition = "poor" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await alice.Client.GetJsonAsync<MovieResponse>($"{RoutePrefix}{created.Id}");
        Assert.Equal(Condition.Poor, fetched!.Condition);
    }

    [Fact]
    public async Task BulkUpdate_NullableCondition_Overflows_Returns400()
    {
        var alice = await NewAliceAsync();
        var created = await (await alice.Client.PostAsJsonAsync(RoutePrefix, Sample()))
            .ReadJsonAsync<MovieResponse>();

        var response = await alice.Client.PatchAsJsonAsync($"{RoutePrefix}bulk",
            new { ids = new[] { created!.Id }, updates = new { condition = "999999999999999999999999" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
