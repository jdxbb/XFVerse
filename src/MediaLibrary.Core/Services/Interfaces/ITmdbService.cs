using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITmdbService
{
    Task<IReadOnlyList<MetadataSearchCandidate>> SearchMoviesAsync(
        string query,
        int? releaseYear,
        CancellationToken cancellationToken = default);

    Task<MetadataSearchCandidate?> GetMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}
