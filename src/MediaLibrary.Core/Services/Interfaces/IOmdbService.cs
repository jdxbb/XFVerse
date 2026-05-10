using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOmdbService
{
    Task<MovieRatingItem?> GetRatingAsync(
        string imdbId,
        CancellationToken cancellationToken = default);
}
