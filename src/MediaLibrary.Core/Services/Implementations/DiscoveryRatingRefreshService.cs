using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class DiscoveryRatingRefreshService : IDiscoveryRatingRefreshService
{
    public async Task<bool> RefreshMovieRatingsAsync(
        int? movieId,
        int tmdbId,
        double? tmdbRating,
        int? tmdbVoteCount,
        MovieRatingItem? omdbRating,
        CancellationToken cancellationToken = default)
    {
        if (movieId is not > 0 && tmdbId <= 0)
        {
            return false;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movieQuery = dbContext.Movies.Include(x => x.RatingSources).AsQueryable();
        var movie = movieId.HasValue
            ? await movieQuery.FirstOrDefaultAsync(x => x.Id == movieId.Value, cancellationToken)
            : await movieQuery.FirstOrDefaultAsync(x => x.TmdbId == tmdbId, cancellationToken);
        if (movie is null && tmdbId > 0)
        {
            movie = await movieQuery.FirstOrDefaultAsync(x => x.TmdbId == tmdbId, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (movie is not null)
        {
            UpsertMovieRating(
                movie,
                "TMDB",
                tmdbRating,
                10d,
                tmdbVoteCount,
                tmdbId > 0 ? $"https://www.themoviedb.org/movie/{tmdbId}" : string.Empty,
                now);
            UpsertMovieRating(
                movie,
                "OMDb",
                omdbRating?.ScoreValue,
                omdbRating?.ScoreScale ?? 10d,
                omdbRating?.VoteCount,
                omdbRating?.SourceUrl ?? string.Empty,
                now);
        }

        if (tmdbId > 0)
        {
            var collectionItems = await dbContext.UserMovieCollectionItems
                .Where(x => x.TmdbId == tmdbId || (movieId.HasValue && x.MovieId == movieId.Value))
                .ToListAsync(cancellationToken);
            foreach (var item in collectionItems)
            {
                if (tmdbRating is > 0)
                {
                    item.TmdbRating = tmdbRating;
                    item.TmdbVoteCount = tmdbVoteCount;
                }

                if (omdbRating is { ScoreValue: > 0, ScoreScale: > 0 })
                {
                    item.OmdbScoreValue = omdbRating.ScoreValue;
                    item.OmdbScoreScale = omdbRating.ScoreScale;
                    item.OmdbVoteCount = omdbRating.VoteCount;
                    item.OmdbSourceUrl = omdbRating.SourceUrl ?? string.Empty;
                    item.OmdbLastUpdatedAt = omdbRating.LastUpdatedAt ?? now;
                }

            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> RefreshTvSeriesRatingsAsync(
        int? tvSeriesId,
        int tmdbSeriesId,
        double? tmdbRating,
        int? tmdbVoteCount,
        MovieRatingItem? omdbRating,
        CancellationToken cancellationToken = default)
    {
        if (tvSeriesId is not > 0 && tmdbSeriesId <= 0)
        {
            return false;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var seriesQuery = dbContext.TvSeries.Include(x => x.RatingSources).AsQueryable();
        var series = tvSeriesId.HasValue
            ? await seriesQuery.FirstOrDefaultAsync(x => x.Id == tvSeriesId.Value, cancellationToken)
            : await seriesQuery.FirstOrDefaultAsync(x => x.TmdbSeriesId == tmdbSeriesId, cancellationToken);
        if (series is null && tmdbSeriesId > 0)
        {
            series = await seriesQuery.FirstOrDefaultAsync(x => x.TmdbSeriesId == tmdbSeriesId, cancellationToken);
        }
        if (series is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        UpsertTvSeriesRating(
            series,
            "TMDB",
            tmdbRating,
            10d,
            tmdbVoteCount,
            tmdbSeriesId > 0 ? $"https://www.themoviedb.org/tv/{tmdbSeriesId}" : string.Empty,
            now);
        UpsertTvSeriesRating(
            series,
            "OMDb",
            omdbRating?.ScoreValue,
            omdbRating?.ScoreScale ?? 10d,
            omdbRating?.VoteCount,
            omdbRating?.SourceUrl ?? string.Empty,
            now);
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    private static void UpsertMovieRating(
        Movie movie,
        string sourceName,
        double? scoreValue,
        double scoreScale,
        int? voteCount,
        string sourceUrl,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || scoreValue is not > 0 || scoreScale <= 0)
        {
            return;
        }

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

        rating.ScoreValue = scoreValue.Value;
        rating.ScoreScale = scoreScale;
        rating.VoteCount = voteCount;
        rating.SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl;
        rating.LastUpdatedAt = now;
    }

    private static void UpsertTvSeriesRating(
        TvSeries series,
        string sourceName,
        double? scoreValue,
        double scoreScale,
        int? voteCount,
        string sourceUrl,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || scoreValue is not > 0 || scoreScale <= 0)
        {
            return;
        }

        var rating = series.RatingSources.FirstOrDefault(
            x => string.Equals(x.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        if (rating is null)
        {
            rating = new TvSeriesRatingSource
            {
                SourceName = sourceName,
                CreatedAt = now
            };
            series.RatingSources.Add(rating);
        }

        rating.ScoreValue = scoreValue.Value;
        rating.ScoreScale = scoreScale;
        rating.VoteCount = voteCount;
        rating.SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl;
        rating.LastUpdatedAt = now;
    }
}
