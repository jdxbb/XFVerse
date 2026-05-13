using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOmdbService
{
    Task<MovieRatingItem?> GetRatingAsync(
        string imdbId,
        CancellationToken cancellationToken = default);

    Task<MovieRatingItem?> GetSeriesRatingAsync(
        string imdbId,
        CancellationToken cancellationToken = default);

    Task<OmdbSeasonRatingAuditResult> GetSeasonRatingAuditAsync(
        string imdbId,
        int seasonNumber,
        CancellationToken cancellationToken = default);
}
