using Collectify.Domain.Enums;

namespace Collectify.Tests.TestSupport;

/// <summary>
/// Common shape shared by the three collection response DTOs (movies, music,
/// games) so <c>CollectionEndpointsTestsBase&lt;TEntity, TResponse&gt;</c> can
/// assert on it generically without knowing the concrete media type.
/// </summary>
public interface ICollectionResponse
{
    int Id { get; }
    string Title { get; }
    int? Year { get; }
    int? PersonalRating { get; }
    string? AcquisitionCurrency { get; }
    string? ImagePath { get; }
    string[] Tags { get; }
    string[] Genres { get; }
    DateTime AddedAt { get; }
    DateTime UpdatedAt { get; }
}

public record MovieResponse(
    int Id, string Title, string? OriginalTitle, int? Year,
    int Formats, string? Director, int? RuntimeMinutes,
    string? Studio, string[] Genres, string? Barcode,
    string? TmdbId, string? ImdbId, string? ImagePath, string? Description, string? Notes,
    int? PersonalRating, CollectionStatus Status, Condition? Condition,
    DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
    WatchStatus WatchStatus, DateOnly? LastWatchedOn, int WatchCount,
    string[] Tags,
    DateTime AddedAt, DateTime UpdatedAt,
    DateOnly? ReleaseDate, string? Cast, float? ProviderRating) : ICollectionResponse;

public record AlbumResponse(
    int Id, string Title, string ArtistName, int? Year,
    MusicFormat Format, string? Label, string[] Genres, string? Barcode,
    string? MusicBrainzReleaseId, string? DiscogsId, string? ImagePath, string? Description, string? Notes,
    int? PersonalRating, CollectionStatus Status, Condition? Condition,
    DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
    int ListenCount, DateOnly? LastPlayedOn,
    string[] Tags,
    DateTime AddedAt, DateTime UpdatedAt,
    DateOnly? ReleaseDate) : ICollectionResponse;

public record GameResponse(
    int Id, string Title, GamePlatform Platform, string? PlatformLegacy, int? Year,
    string? Publisher, string? Developer, int DigitalStores,
    string[] Genres,
    string? Barcode, string? IgdbId, string? ImagePath, string? Description, string? Notes,
    int? PersonalRating, CollectionStatus Status, Condition? Condition,
    DateOnly? AcquiredOn, decimal? AcquisitionPrice, string? AcquisitionCurrency, string? AcquisitionSource,
    CompletionStatus CompletionStatus, int? HoursPlayed, DateOnly? LastPlayedOn,
    string[] Tags,
    DateTime AddedAt, DateTime UpdatedAt,
    DateOnly? ReleaseDate, string? AgeRating) : ICollectionResponse;

/// <summary>Per-type sample-DTO factories, kept in one place so the three
/// endpoint test classes stop each carrying their own copy.</summary>
public static class MovieTestSupport
{
    public static object Sample(
        string title = "Inception",
        int? year = 2010,
        MovieFormat formats = MovieFormat.BluRay,
        int? rating = null,
        CollectionStatus status = CollectionStatus.Owned,
        Condition? condition = null,
        string? currency = null,
        WatchStatus watchStatus = WatchStatus.Unwatched,
        int watchCount = 0,
        string[]? tags = null,
        string[]? genres = null) => new
        {
            Title = title,
            OriginalTitle = (string?)null,
            Year = year,
            Formats = (int)formats,
            Director = "Christopher Nolan",
            RuntimeMinutes = 148,
            Studio = "Warner Bros.",
            Genres = genres ?? new[] { "Sci-Fi", "Thriller" },
            Barcode = (string?)null,
            TmdbId = (string?)null,
            ImdbId = (string?)null,
            ImagePath = (string?)null,
            Description = "A heist on the subconscious.",
            Notes = (string?)null,
            PersonalRating = rating,
            Status = status,
            Condition = condition,
            AcquiredOn = (DateOnly?)new DateOnly(2024, 1, 15),
            AcquisitionPrice = (decimal?)19.99m,
            AcquisitionCurrency = currency,
            AcquisitionSource = "Amazon",
            WatchStatus = watchStatus,
            LastWatchedOn = (DateOnly?)new DateOnly(2024, 6, 1),
            WatchCount = watchCount,
            Tags = tags,
        };
}

public static class MusicTestSupport
{
    public static object Sample(
        string title = "OK Computer",
        string artist = "Radiohead",
        int? year = 1997,
        MusicFormat format = MusicFormat.Cd,
        int? rating = null,
        string? currency = "GBP",
        string[]? tags = null,
        int listenCount = 0,
        string[]? genres = null) => new
        {
            Title = title,
            ArtistName = artist,
            Year = year,
            Format = format,
            Label = (string?)null,
            Genres = genres,
            Barcode = (string?)null,
            MusicBrainzReleaseId = (string?)null,
            DiscogsId = (string?)null,
            ImagePath = (string?)null,
            Description = "Third studio album.",
            Notes = (string?)null,
            PersonalRating = rating,
            Status = CollectionStatus.Owned,
            Condition = (Condition?)Domain.Enums.Condition.Good,
            AcquiredOn = (DateOnly?)new DateOnly(2024, 1, 15),
            AcquisitionPrice = (decimal?)12.50m,
            AcquisitionCurrency = currency,
            AcquisitionSource = "Rough Trade",
            ListenCount = listenCount,
            LastPlayedOn = (DateOnly?)new DateOnly(2024, 8, 1),
            Tags = tags,
        };
}

public static class GameTestSupport
{
    public static object Sample(
        string title = "Hades",
        GamePlatform platform = GamePlatform.Pc,
        bool isDigital = true,
        DigitalStore? store = DigitalStore.Steam,
        int? rating = null,
        string? currency = "USD",
        CompletionStatus completion = CompletionStatus.NotStarted,
        int? hours = null,
        string[]? tags = null,
        string[]? genres = null) => new
        {
            Title = title,
            Platform = platform,
            Year = (int?)2020,
            Publisher = "Supergiant Games",
            Developer = "Supergiant Games",
            DigitalStores = isDigital ? (store is { } s ? (int)s : (int)DigitalStore.Other) : 0,
            Genres = genres,
            Barcode = (string?)null,
            IgdbId = (string?)null,
            ImagePath = (string?)null,
            Description = "Roguelike from Supergiant.",
            Notes = (string?)null,
            PersonalRating = rating,
            Status = CollectionStatus.Owned,
            Condition = (Condition?)null,
            AcquiredOn = (DateOnly?)new DateOnly(2024, 2, 10),
            AcquisitionPrice = (decimal?)24.99m,
            AcquisitionCurrency = currency,
            AcquisitionSource = "Steam Sale",
            CompletionStatus = completion,
            HoursPlayed = hours,
            LastPlayedOn = (DateOnly?)new DateOnly(2024, 9, 1),
            Tags = tags,
        };
}
