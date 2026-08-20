using System.Net;
using System.Net.Http.Json;
using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Tests.Infrastructure;
using Collectify.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Tests.Api;

public class MusicEndpointsTests : CollectionEndpointsTestsBase<MusicAlbum, AlbumResponse>, IClassFixture<CollectifyApiFactory>
{
    public MusicEndpointsTests(CollectifyApiFactory factory) : base(factory)
    {
    }

    protected override string RoutePrefix => "/api/music/";

    protected override object Sample(string? title = null, string[]? tags = null, string? currency = null, int? rating = null) =>
        MusicTestSupport.Sample(title: title ?? "OK Computer", tags: tags, currency: currency ?? "GBP", rating: rating);

    protected override object MinimalWithImage(string? imagePath) => new
    {
        Title = "OK Computer",
        ArtistName = "Radiohead",
        Format = MusicFormat.Cd,
        Status = CollectionStatus.Owned,
        ListenCount = 0,
        ImagePath = imagePath,
        Tags = (string[]?)null,
    };

    protected override MusicAlbum NewMinimalEntity(string ownerId, string title) => new()
    {
        OwnerId = ownerId,
        Title = title,
        ArtistName = "Radiohead",
        UpdatedAt = DateTime.UtcNow.AddDays(-1),
    };

    protected override int IdOf(MusicAlbum entity) => entity.Id;
    protected override string OwnerIdOf(MusicAlbum entity) => entity.OwnerId;
    protected override string TitleOf(MusicAlbum entity) => entity.Title;
    protected override DateTime UpdatedAtOf(MusicAlbum entity) => entity.UpdatedAt;

    // -------- Validation --------

    [Fact]
    public async Task Create_WithEmptyArtist_ReturnsBadRequest()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/music/", MusicTestSupport.Sample(artist: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------- List filters --------

    [Fact]
    public async Task List_FiltersByQuery_MatchesArtistSubstring()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Kid A", ArtistName = "Radiohead" });
        await Factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Funeral", ArtistName = "Arcade Fire" });

        var hits = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?query=radio");

        Assert.Single(hits!);
        Assert.Equal("Radiohead", hits![0].ArtistName);
    }

    [Fact]
    public async Task List_FiltersByYearRange_ArtistLabelGenreStatusRating()
    {
        var alice = await NewAliceAsync();
        await Factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "OK Computer", ArtistName = "Radiohead",   Year = 1997, Label = "Parlophone", Genres = "rock", PersonalRating = 9, Status = CollectionStatus.Owned });
        await Factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Funeral",     ArtistName = "Arcade Fire", Year = 2004, Label = "Merge",      Genres = "indie", PersonalRating = 7, Status = CollectionStatus.Owned });
        await Factory.SeedAsync(new MusicAlbum { OwnerId = alice.Id, Title = "Pet Sounds",  ArtistName = "Beach Boys",  Year = 1966, Label = "Capitol",    Genres = "pop", PersonalRating = 10, Status = CollectionStatus.Wishlist });

        var byYear = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?yearFrom=1990&yearTo=2000");
        var only = Assert.Single(byYear!);
        Assert.Equal("OK Computer", only.Title);

        var byArtist = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?artist=Radiohead");
        Assert.Single(byArtist!);

        var byLabel = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?label=Merge");
        Assert.Single(byLabel!);

        var byGenre = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?genre=indie");
        Assert.Single(byGenre!);

        var byStatus = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?status=Wishlist");
        Assert.Single(byStatus!);

        var byRating = await alice.Client.GetJsonAsync<AlbumResponse[]>("/api/music/?ratingMin=9");
        Assert.Equal(2, byRating!.Length);
    }

    // -------- Personal / acquisition / listen fields round-trip --------

    [Fact]
    public async Task Create_RoundTripsAllNewScalarFields()
    {
        var alice = await NewAliceAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/music/",
            MusicTestSupport.Sample(rating: 10, listenCount: 42));

        var body = await response.ReadJsonAsync<AlbumResponse>();
        Assert.Equal("Radiohead", body!.ArtistName);
        Assert.Equal(MusicFormat.Cd, body.Format);
        Assert.Equal("Third studio album.", body.Description);
        Assert.Equal(10, body.PersonalRating);
        Assert.Equal(CollectionStatus.Owned, body.Status);
        Assert.Equal(Condition.Good, body.Condition);
        Assert.Equal(new DateOnly(2024, 1, 15), body.AcquiredOn);
        Assert.Equal(12.50m, body.AcquisitionPrice);
        Assert.Equal("GBP", body.AcquisitionCurrency);
        Assert.Equal("Rough Trade", body.AcquisitionSource);
        Assert.Equal(42, body.ListenCount);
        Assert.Equal(new DateOnly(2024, 8, 1), body.LastPlayedOn);
    }
}
