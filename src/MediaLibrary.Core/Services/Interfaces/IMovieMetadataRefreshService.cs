using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMovieMetadataRefreshService
{
    Task<MovieMetadataRefreshResult> RefreshMovieMetadataAsync(
        int movieId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
