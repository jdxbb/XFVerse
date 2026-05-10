using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMovieDetailQueryService
{
    Task<MovieDetailModel?> GetMovieDetailAsync(
        int movieId,
        CancellationToken cancellationToken = default);
}
