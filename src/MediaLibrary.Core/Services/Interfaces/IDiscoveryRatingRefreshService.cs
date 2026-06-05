using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IDiscoveryRatingRefreshService
{
    Task<bool> RefreshMovieRatingsAsync(
        int? movieId,
        int tmdbId,
        double? tmdbRating,
        int? tmdbVoteCount,
        MovieRatingItem? omdbRating,
        CancellationToken cancellationToken = default);

    Task<bool> RefreshTvSeriesRatingsAsync(
        int? tvSeriesId,
        int tmdbSeriesId,
        double? tmdbRating,
        int? tmdbVoteCount,
        MovieRatingItem? omdbRating,
        CancellationToken cancellationToken = default);
}
