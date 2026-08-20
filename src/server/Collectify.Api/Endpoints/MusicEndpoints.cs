using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class MusicEndpoints
{
    public record AlbumDto(
        int? Id,
        string Title,
        string ArtistName,
        int? Year,
        MusicFormat Format,
        string? Label,
        string? Genres,
        string? Barcode,
        string? MusicBrainzReleaseId,
        string? DiscogsId,
        string? ImagePath,
        string? Description,
        string? Notes,
        int? PersonalRating,
        CollectionStatus Status,
        Condition? Condition,
        DateOnly? AcquiredOn,
        decimal? AcquisitionPrice,
        string? AcquisitionCurrency,
        string? AcquisitionSource,
        int ListenCount,
        DateOnly? LastPlayedOn,
        string[]? Tags,
        DateTime? AddedAt,
        DateTime? UpdatedAt,
        DateOnly? ReleaseDate) : ICollectionEntryDto;

    private static readonly CollectionEndpointConfig<MusicAlbum, AlbumDto> Config = new()
    {
        RoutePrefix = "/api/music",
        Set = db => db.MusicAlbums,
        ToDto = ToDto,
        Apply = ApplyDto,
        Validate = Validate,
        SearchFilter = (q, query) =>
        {
            var like = $"%{query}%";
            return q.Where(a => EF.Functions.Like(a.Title, like)
                              || EF.Functions.Like(a.ArtistName, like)
                              || (a.Label != null && EF.Functions.Like(a.Label, like)));
        },
        ExtraFilters = (q, request) =>
        {
            if (request.Query.TryGetValue("format", out var formatValues))
            {
                if (formatValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'format' must have a single value." }));
                if (Enum.TryParse<MusicFormat>(formatValues, ignoreCase: true, out var format)
                    && Enum.IsDefined(format))
                    q = q.Where(a => a.Format == format);
                else
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'format'." }));
            }

            if (request.Query.TryGetValue("artist", out var artistValues))
            {
                if (artistValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'artist' must have a single value." }));
                var artist = artistValues.ToString();
                if (!string.IsNullOrWhiteSpace(artist))
                {
                    var like = $"%{artist}%";
                    q = q.Where(a => EF.Functions.Like(a.ArtistName, like));
                }
            }

            if (request.Query.TryGetValue("label", out var labelValues))
            {
                if (labelValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'label' must have a single value." }));
                var label = labelValues.ToString();
                if (!string.IsNullOrWhiteSpace(label))
                {
                    var like = $"%{label}%";
                    q = q.Where(a => a.Label != null && EF.Functions.Like(a.Label, like));
                }
            }

            if (request.Query.TryGetValue("genre", out var genreValues))
            {
                if (genreValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'genre' must have a single value." }));
                var genre = genreValues.ToString();
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    var like = $"%{genre}%";
                    q = q.Where(a => a.Genres != null && EF.Functions.Like(a.Genres, like));
                }
            }

            return (q, null);
        },
        OnDelete = null,
    };

    public static IEndpointRouteBuilder MapMusicEndpoints(this IEndpointRouteBuilder app) =>
        app.MapCollectionEndpoints(Config);

    private static IResult? Validate(AlbumDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (string.IsNullOrWhiteSpace(dto.ArtistName))
            return Results.BadRequest(new { error = "Artist name is required." });
        if (dto.PersonalRating is { } r && (r < 1 || r > 10))
            return Results.BadRequest(new { error = "PersonalRating must be between 1 and 10." });
        if (dto.AcquisitionCurrency is { Length: > 0 } c && c.Length != 3)
            return Results.BadRequest(new { error = "AcquisitionCurrency must be a 3-letter ISO 4217 code." });
        return null;
    }

    private static AlbumDto ToDto(MusicAlbum a) => new(
        a.Id, a.Title, a.ArtistName, a.Year, a.Format, a.Label, a.Genres, a.Barcode,
        a.MusicBrainzReleaseId, a.DiscogsId, a.ImagePath, a.Description, a.Notes,
        a.PersonalRating, a.Status, a.Condition,
        a.AcquiredOn, a.AcquisitionPrice, a.AcquisitionCurrency, a.AcquisitionSource,
        a.ListenCount, a.LastPlayedOn,
        TagResolver.ToNameArray(a.Tags),
        a.AddedAt, a.UpdatedAt,
        a.ReleaseDate);

    private static void ApplyDto(MusicAlbum a, AlbumDto dto)
    {
        a.Title = dto.Title?.Trim() ?? string.Empty;
        a.ArtistName = dto.ArtistName?.Trim() ?? string.Empty;
        a.Year = dto.Year;
        a.Format = dto.Format;
        a.Label = dto.Label;
        a.Genres = dto.Genres;
        a.Barcode = dto.Barcode;
        a.MusicBrainzReleaseId = dto.MusicBrainzReleaseId;
        a.DiscogsId = dto.DiscogsId;
        a.ImagePath = dto.ImagePath;
        a.Description = dto.Description;
        a.Notes = dto.Notes;
        a.PersonalRating = dto.PersonalRating;
        a.Status = dto.Status;
        a.Condition = dto.Condition;
        a.AcquiredOn = dto.AcquiredOn;
        a.AcquisitionPrice = dto.AcquisitionPrice;
        a.AcquisitionCurrency = string.IsNullOrWhiteSpace(dto.AcquisitionCurrency) ? null : dto.AcquisitionCurrency.ToUpperInvariant();
        a.AcquisitionSource = dto.AcquisitionSource;
        a.ListenCount = dto.ListenCount;
        a.LastPlayedOn = dto.LastPlayedOn;
        a.ReleaseDate = dto.ReleaseDate;
    }
}
