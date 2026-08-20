using Collectify.Domain.Entities;
using Collectify.Domain.Enums;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Collectify.Api.Endpoints;

public static class MoviesEndpoints
{
    // Union of every defined MovieFormat flag bit. Derived from the enum so a
    // future member is covered automatically (reviewer F1); computed once, not
    // per write request.
    private static readonly int ValidMovieFormatBits =
        Enum.GetValues<MovieFormat>().Aggregate(0, (mask, f) => mask | (int)f);

    public record MovieDto(
        int? Id,
        string Title,
        string? OriginalTitle,
        int? Year,
        int Formats,
        string? Director,
        int? RuntimeMinutes,
        string? Studio,
        string? Genres,
        string? Barcode,
        string? TmdbId,
        string? ImdbId,
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
        WatchStatus WatchStatus,
        DateOnly? LastWatchedOn,
        int WatchCount,
        string[]? Tags,
        DateTime? AddedAt,
        DateTime? UpdatedAt) : ICollectionEntryDto;

    private static readonly CollectionEndpointConfig<Movie, MovieDto> Config = new()
    {
        RoutePrefix = "/api/movies",
        Set = db => db.Movies,
        ToDto = ToDto,
        Apply = ApplyDto,
        Validate = Validate,
        SearchFilter = (q, query) =>
        {
            var like = $"%{query}%";
            return q.Where(m => EF.Functions.Like(m.Title, like)
                              || (m.Director != null && EF.Functions.Like(m.Director, like))
                              || (m.OriginalTitle != null && EF.Functions.Like(m.OriginalTitle, like)));
        },
        ExtraFilters = (q, request) =>
        {
            if (request.Query.ContainsKey("format"))
            {
                var format = ResolveFormat(request.Query["format"]);
                if (format is null)
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'format'." }));
                if (format.Value != MovieFormat.None)
                    q = q.Where(m => (m.Formats & format.Value) != 0);
            }

            if (request.Query.TryGetValue("director", out var directorValues))
            {
                if (directorValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'director' must have a single value." }));
                var director = directorValues.ToString();
                if (!string.IsNullOrWhiteSpace(director))
                {
                    var like = $"%{director}%";
                    q = q.Where(m => m.Director != null && EF.Functions.Like(m.Director, like));
                }
            }

            if (request.Query.TryGetValue("studio", out var studioValues))
            {
                if (studioValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'studio' must have a single value." }));
                var studio = studioValues.ToString();
                if (!string.IsNullOrWhiteSpace(studio))
                {
                    var like = $"%{studio}%";
                    q = q.Where(m => m.Studio != null && EF.Functions.Like(m.Studio, like));
                }
            }

            if (request.Query.TryGetValue("genre", out var genreValues))
            {
                if (genreValues.Count > 1)
                    return (q, Results.BadRequest(new { error = "Query parameter 'genre' must have a single value." }));
                // Genres is stored as a comma-separated string; substring
                // match is good enough for the volume here.
                var genre = genreValues.ToString();
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    var like = $"%{genre}%";
                    q = q.Where(m => m.Genres != null && EF.Functions.Like(m.Genres, like));
                }
            }

            if (request.Query.ContainsKey("watchStatus"))
            {
                if (Enum.TryParse<WatchStatus>(request.Query["watchStatus"], ignoreCase: true, out var watchStatus)
                    && Enum.IsDefined(watchStatus))
                    q = q.Where(m => m.WatchStatus == watchStatus);
                else
                    return (q, Results.BadRequest(new { error = "Invalid value for query parameter 'watchStatus'." }));
            }

            return (q, null);
        },
        OnDelete = null,
    };

    // Bound as a raw string (not the enum) so the filter mirrors the
    // write-boundary's defined-member-only semantics: a single member name,
    // a comma-joined combo of member names, or a numeric flags combination
    // whose bits are all defined resolves to that MovieFormat; anything with
    // an undefined bit or member returns null, which the caller turns into a
    // 400 (a present-but-invalid value must not be silently dropped).
    private static MovieFormat? ResolveFormat(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (int.TryParse(raw, out var asInt))
        {
            if ((asInt & ~ValidMovieFormatBits) == 0) return (MovieFormat)asInt;
            return null; // undefined bit(s) in a numeric combo
        }
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var acc = MovieFormat.None;
        foreach (var p in parts)
            if (Enum.TryParse<MovieFormat>(p, ignoreCase: true, out var one) && Enum.IsDefined(one))
                acc |= one;
            else
                return null; // any undefined member name — reject the whole filter
        return acc;
    }

    public static IEndpointRouteBuilder MapMoviesEndpoints(this IEndpointRouteBuilder app) =>
        app.MapCollectionEndpoints(Config);

    private static IResult? Validate(MovieDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (dto.PersonalRating is { } r && (r < 1 || r > 10))
            return Results.BadRequest(new { error = "PersonalRating must be between 1 and 10." });
        // MovieFormats is bound as an int (the client sends the flags bitmask
        // as a number), so the enum converters never see it. Guard the unchecked
        // (MovieFormat)dto.Formats cast at the boundary (issue #115): an
        // arbitrary integer with bits outside the defined flag set must not
        // persist an undefined MovieFormat. None (0) and any combination of
        // defined bits are valid (ValidMovieFormatBits, see above).
        if ((dto.Formats & ~ValidMovieFormatBits) != 0)
            return Results.BadRequest(new { error = "Formats contains an undefined MovieFormat bit." });
        if (dto.AcquisitionCurrency is { Length: > 0 } c && c.Length != 3)
            return Results.BadRequest(new { error = "AcquisitionCurrency must be a 3-letter ISO 4217 code." });
        return null;
    }

    private static MovieDto ToDto(Movie m) => new(
        m.Id, m.Title, m.OriginalTitle, m.Year, (int)m.Formats, m.Director, m.RuntimeMinutes,
        m.Studio, m.Genres, m.Barcode, m.TmdbId, m.ImdbId, m.ImagePath, m.Description, m.Notes,
        m.PersonalRating, m.Status, m.Condition,
        m.AcquiredOn, m.AcquisitionPrice, m.AcquisitionCurrency, m.AcquisitionSource,
        m.WatchStatus, m.LastWatchedOn, m.WatchCount,
        TagResolver.ToNameArray(m.Tags),
        m.AddedAt, m.UpdatedAt);

    private static void ApplyDto(Movie m, MovieDto dto)
    {
        m.Title = dto.Title?.Trim() ?? string.Empty;
        m.OriginalTitle = dto.OriginalTitle;
        m.Year = dto.Year;
        m.Formats = (MovieFormat)dto.Formats;
        m.Director = dto.Director;
        m.RuntimeMinutes = dto.RuntimeMinutes;
        m.Studio = dto.Studio;
        m.Genres = dto.Genres;
        m.Barcode = dto.Barcode;
        m.TmdbId = dto.TmdbId;
        m.ImdbId = dto.ImdbId;
        m.ImagePath = dto.ImagePath;
        m.Description = dto.Description;
        m.Notes = dto.Notes;
        m.PersonalRating = dto.PersonalRating;
        m.Status = dto.Status;
        m.Condition = dto.Condition;
        m.AcquiredOn = dto.AcquiredOn;
        m.AcquisitionPrice = dto.AcquisitionPrice;
        m.AcquisitionCurrency = string.IsNullOrWhiteSpace(dto.AcquisitionCurrency) ? null : dto.AcquisitionCurrency.ToUpperInvariant();
        m.AcquisitionSource = dto.AcquisitionSource;
        m.WatchStatus = dto.WatchStatus;
        m.LastWatchedOn = dto.LastWatchedOn;
        m.WatchCount = dto.WatchCount;
    }
}
