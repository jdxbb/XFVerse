using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class MovieMetadataRefreshService : IMovieMetadataRefreshService
{
    private const int MovieTitleMaxLength = 300;
    private const int MovieOverviewMaxLength = 5000;
    private const int MoviePosterUrlMaxLength = 1200;
    private const int MovieCountryMaxLength = 120;
    private const int MovieLanguageMaxLength = 120;
    private const int MoviePersonTextMaxLength = 500;
    private const int MovieActorsTextMaxLength = 1000;
    private const int MovieGenresTextMaxLength = 1000;
    private const int MovieImdbIdMaxLength = 40;
    private const int CollectionTitleMaxLength = 256;
    private const int CollectionOverviewMaxLength = 4000;
    private const int CollectionPosterUrlMaxLength = 1024;
    private const int CollectionGenresTextMaxLength = 512;
    private const int CollectionCountryMaxLength = 128;
    private const int CollectionLanguageMaxLength = 64;
    private const int CollectionImdbIdMaxLength = 64;
    private static readonly ConcurrentDictionary<int, Task<MovieMetadataRefreshResult>> InFlightRefreshTasks = new();

    private readonly ITmdbService _tmdbService;
    private readonly IOmdbService _omdbService;

    public MovieMetadataRefreshService(ITmdbService tmdbService, IOmdbService omdbService)
    {
        _tmdbService = tmdbService;
        _omdbService = omdbService;
    }

    public async Task<MovieMetadataRefreshResult> RefreshMovieMetadataAsync(
        int movieId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (movieId <= 0)
        {
            return MovieMetadataRefreshResult.Failed(movieId, null, "invalid-movie-id");
        }

        var refreshTask = InFlightRefreshTasks.GetOrAdd(
            movieId,
            _ => RefreshMovieMetadataCoreAsync(movieId, forceRefresh, CancellationToken.None));
        try
        {
            return await refreshTask;
        }
        finally
        {
            if (InFlightRefreshTasks.TryGetValue(movieId, out var currentTask)
                && ReferenceEquals(currentTask, refreshTask))
            {
                InFlightRefreshTasks.TryRemove(movieId, out _);
            }
        }
    }

    private async Task<MovieMetadataRefreshResult> RefreshMovieMetadataCoreAsync(
        int movieId,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (await MetadataDetailRefreshCooldown.IsMovieCoolingDownAsync(movieId, cancellationToken))
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-tmdb-metadata-refresh-skipped movieId={movieId} skippedReason=\"cooldown\"");
            return MovieMetadataRefreshResult.Cooldown(movieId, null);
        }

        await using var readContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var key = await readContext.Movies
            .AsNoTracking()
            .Where(x => x.Id == movieId)
            .Select(x => new { x.Id, x.TmdbId })
            .FirstOrDefaultAsync(cancellationToken);
        if (key?.TmdbId is not > 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-tmdb-metadata-refresh-skipped movieId={movieId} skippedReason=\"missing-tmdb-id\"");
            return MovieMetadataRefreshResult.Failed(movieId, null, "missing-tmdb-id");
        }

        var details = await _tmdbService.GetMovieDetailsAsync(
            key.TmdbId.Value,
            cancellationToken,
            forceRefresh);
        if (details is null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-tmdb-metadata-refresh-failed movieId={movieId} tmdbId={key.TmdbId.Value} skippedReason=\"tmdb-detail-unavailable\"");
            return MovieMetadataRefreshResult.Failed(movieId, key.TmdbId.Value, "tmdb-detail-unavailable");
        }

        MovieRatingItem? omdbRating = null;
        if (!string.IsNullOrWhiteSpace(details.ImdbId))
        {
            try
            {
                omdbRating = await _omdbService.GetRatingAsync(details.ImdbId, cancellationToken);
            }
            catch
            {
                omdbRating = null;
            }
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var movie = await dbContext.Movies
            .Include(x => x.RatingSources)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie?.TmdbId is not > 0 || movie.TmdbId.Value != details.TmdbId)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-tmdb-metadata-refresh-failed movieId={movieId} tmdbId={details.TmdbId} skippedReason=\"movie-tmdb-mismatch\"");
            return MovieMetadataRefreshResult.Failed(movieId, details.TmdbId, "movie-tmdb-mismatch");
        }

        var now = DateTime.UtcNow;
        var metadataChanged = ApplyMovieMetadata(movie, details);
        var ratingChanged = UpsertRating(
            movie,
            "TMDB",
            details.TmdbRating,
            10d,
            details.TmdbVoteCount,
            $"https://www.themoviedb.org/movie/{details.TmdbId}",
            now);
        if (omdbRating is not null)
        {
            ratingChanged |= UpsertRating(
                movie,
                "OMDb",
                omdbRating.ScoreValue,
                omdbRating.ScoreScale,
                omdbRating.VoteCount,
                omdbRating.SourceUrl,
                now);
        }

        var hasActiveSource = await dbContext.MediaFiles
            .AnyAsync(
                x => x.MovieId == movie.Id
                     && !x.IsDeleted
                     && x.MediaType == MediaType.Video,
                cancellationToken);
        var collectionItems = await dbContext.UserMovieCollectionItems
            .Where(
                x => x.MovieId == movie.Id
                     || (movie.TmdbId.HasValue && x.TmdbId == movie.TmdbId.Value)
                     || (!string.IsNullOrWhiteSpace(movie.ImdbId) && x.ImdbId == movie.ImdbId)
                     || (x.Title == movie.Title && x.ReleaseYear == movie.ReleaseYear))
            .ToListAsync(cancellationToken);
        var collectionChanged = false;
        foreach (var item in collectionItems)
        {
            collectionChanged |= ApplyCollectionSnapshot(item, movie, hasActiveSource, now);
        }

        var hasChanges = metadataChanged || ratingChanged || collectionChanged;
        if (hasChanges)
        {
            movie.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await MetadataDetailRefreshCooldown.MarkMovieSucceededAsync(movieId, CancellationToken.None);
        ScanIdentificationDiagnostics.Write(
            $"event=movie-detail-tmdb-metadata-refresh-succeeded movieId={movieId} tmdbId={details.TmdbId} changed={FormatBool(hasChanges)} metadataChanged={FormatBool(metadataChanged)} ratingChanged={FormatBool(ratingChanged)} collectionChanged={FormatBool(collectionChanged)} cooldownHours=4");
        return MovieMetadataRefreshResult.Succeeded(movieId, details.TmdbId, hasChanges);
    }

    private static bool ApplyMovieMetadata(Movie movie, MetadataSearchCandidate details)
    {
        var changed = false;
        changed |= SetIfChanged(movie.Title, TruncateRequired(FirstNonEmpty(details.Title, movie.Title), MovieTitleMaxLength), value => movie.Title = value);
        changed |= SetIfChanged(movie.OriginalTitle, TruncateOrNull(details.OriginalTitle, MovieTitleMaxLength) ?? movie.OriginalTitle, value => movie.OriginalTitle = value);
        changed |= SetIfChanged(movie.ReleaseYear, details.ReleaseYear ?? movie.ReleaseYear, value => movie.ReleaseYear = value);
        changed |= SetIfChanged(movie.ReleaseDate, details.ReleaseDate ?? movie.ReleaseDate, value => movie.ReleaseDate = value);
        changed |= SetIfChanged(movie.Overview, TruncateOrNull(details.Overview, MovieOverviewMaxLength) ?? movie.Overview, value => movie.Overview = value);
        changed |= SetIfChanged(movie.PosterRemoteUrl, TruncateOrNull(details.PosterRemoteUrl, MoviePosterUrlMaxLength) ?? movie.PosterRemoteUrl, value => movie.PosterRemoteUrl = value);
        changed |= SetIfChanged(movie.Country, TruncateOrNull(details.Country, MovieCountryMaxLength) ?? movie.Country, value => movie.Country = value);
        changed |= SetIfChanged(movie.Language, TruncateOrNull(details.Language, MovieLanguageMaxLength) ?? movie.Language, value => movie.Language = value);
        changed |= SetIfChanged(movie.DirectorText, TruncateOrNull(details.DirectorText, MoviePersonTextMaxLength) ?? movie.DirectorText, value => movie.DirectorText = value);
        changed |= SetIfChanged(movie.WriterText, TruncateOrNull(details.WriterText, MoviePersonTextMaxLength) ?? movie.WriterText, value => movie.WriterText = value);
        changed |= SetIfChanged(movie.ActorsText, TruncateOrNull(details.ActorsText, MovieActorsTextMaxLength) ?? movie.ActorsText, value => movie.ActorsText = value);
        changed |= SetIfChanged(
            movie.ProductionCompanyText,
            TruncateOrNull(details.ProductionCompanyText, MoviePersonTextMaxLength) ?? movie.ProductionCompanyText,
            value => movie.ProductionCompanyText = value);
        changed |= SetIfChanged(movie.RuntimeMinutes, details.RuntimeMinutes is > 0 ? details.RuntimeMinutes : movie.RuntimeMinutes, value => movie.RuntimeMinutes = value);
        changed |= SetIfChanged(movie.ImdbId, TruncateOrNull(details.ImdbId, MovieImdbIdMaxLength) ?? movie.ImdbId, value => movie.ImdbId = value);
        changed |= SetIfChanged(movie.GenresText, TruncateOrNull(details.GenresText, MovieGenresTextMaxLength) ?? movie.GenresText, value => movie.GenresText = value);
        return changed;
    }

    private static bool ApplyCollectionSnapshot(
        UserMovieCollectionItem item,
        Movie movie,
        bool hasActiveSource,
        DateTime now)
    {
        var tmdbRating = movie.RatingSources
            .FirstOrDefault(x => string.Equals(x.SourceName, "TMDB", StringComparison.OrdinalIgnoreCase));
        var omdbRating = movie.RatingSources
            .FirstOrDefault(x => string.Equals(x.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase));

        var changed = false;
        changed |= SetIfChanged(item.MovieId, movie.Id, value => item.MovieId = value);
        changed |= SetIfChanged(item.TmdbId, movie.TmdbId, value => item.TmdbId = value);
        changed |= SetIfChanged(item.Title, TruncateRequired(movie.Title, CollectionTitleMaxLength), value => item.Title = value);
        changed |= SetIfChanged(item.OriginalTitle, Truncate(movie.OriginalTitle, CollectionTitleMaxLength), value => item.OriginalTitle = value);
        changed |= SetIfChanged(item.ReleaseYear, movie.ReleaseYear, value => item.ReleaseYear = value);
        changed |= SetIfChanged(item.ReleaseDate, movie.ReleaseDate, value => item.ReleaseDate = value);
        changed |= SetIfChanged(item.PosterRemoteUrl, Truncate(movie.PosterRemoteUrl, CollectionPosterUrlMaxLength), value => item.PosterRemoteUrl = value);
        changed |= SetIfChanged(item.Overview, Truncate(movie.Overview, CollectionOverviewMaxLength), value => item.Overview = value);
        changed |= SetIfChanged(item.GenresText, Truncate(movie.GenresText, CollectionGenresTextMaxLength), value => item.GenresText = value);
        changed |= SetIfChanged(item.Country, Truncate(movie.Country, CollectionCountryMaxLength), value => item.Country = value);
        changed |= SetIfChanged(item.Language, Truncate(movie.Language, CollectionLanguageMaxLength), value => item.Language = value);
        changed |= SetIfChanged(item.RuntimeMinutes, movie.RuntimeMinutes, value => item.RuntimeMinutes = value);
        changed |= SetIfChanged(item.ImdbId, Truncate(movie.ImdbId, CollectionImdbIdMaxLength), value => item.ImdbId = value);
        changed |= SetIfChanged(item.TmdbRating, tmdbRating?.ScoreValue, value => item.TmdbRating = value);
        changed |= SetIfChanged(item.TmdbVoteCount, tmdbRating?.VoteCount, value => item.TmdbVoteCount = value);
        changed |= SetIfChanged(item.OmdbScoreValue, omdbRating?.ScoreValue, value => item.OmdbScoreValue = value);
        changed |= SetIfChanged(item.OmdbScoreScale, omdbRating?.ScoreScale, value => item.OmdbScoreScale = value);
        changed |= SetIfChanged(item.OmdbVoteCount, omdbRating?.VoteCount, value => item.OmdbVoteCount = value);
        changed |= SetIfChanged(item.OmdbSourceUrl, Truncate(omdbRating?.SourceUrl, CollectionPosterUrlMaxLength), value => item.OmdbSourceUrl = value);
        changed |= SetIfChanged(item.OmdbLastUpdatedAt, omdbRating?.LastUpdatedAt, value => item.OmdbLastUpdatedAt = value);
        changed |= SetIfChanged(item.IsInLibrary, hasActiveSource, value => item.IsInLibrary = value);
        if (changed)
        {
            item.UpdatedAt = now;
        }

        return changed;
    }

    private static bool UpsertRating(
        Movie movie,
        string sourceName,
        double? scoreValue,
        double scoreScale,
        int? voteCount,
        string? sourceUrl,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || scoreValue is not > 0 || scoreScale <= 0)
        {
            return false;
        }

        var nextSourceUrl = TruncateOrNull(sourceUrl, CollectionPosterUrlMaxLength);
        var rating = movie.RatingSources.FirstOrDefault(
            x => string.Equals(x.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        if (rating is null)
        {
            rating = new RatingSource
            {
                SourceName = sourceName,
                CreatedAt = now
            };
            movie.RatingSources.Add(rating);
        }
        else if (rating.ScoreValue.Equals(scoreValue.Value)
                 && rating.ScoreScale.Equals(scoreScale)
                 && rating.VoteCount == voteCount
                 && string.Equals(rating.SourceUrl, nextSourceUrl, StringComparison.Ordinal))
        {
            return false;
        }

        rating.ScoreValue = scoreValue.Value;
        rating.ScoreScale = scoreScale;
        rating.VoteCount = voteCount;
        rating.SourceUrl = nextSourceUrl;
        rating.LastUpdatedAt = now;
        return true;
    }

    private static bool SetIfChanged<T>(T currentValue, T nextValue, Action<T> apply)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return false;
        }

        apply(nextValue);
        return true;
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TruncateOrNull(string? value, int maxLength)
    {
        var truncated = Truncate(value, maxLength);
        return string.IsNullOrWhiteSpace(truncated) ? null : truncated;
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
