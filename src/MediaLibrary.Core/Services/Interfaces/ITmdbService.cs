using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITmdbService
{
    Task<IReadOnlyList<MetadataSearchCandidate>> SearchMoviesAsync(
        string query,
        int? releaseYear,
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDiscoveryPage> SearchDiscoveryMoviesAsync(
        string query,
        int page,
        int? releaseYear = null,
        string region = "",
        CancellationToken cancellationToken = default);

    Task<TmdbPersonSearchPage> SearchPeopleAsync(
        string query,
        int page,
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDiscoveryPage> GetPersonMovieCreditsAsync(
        int personId,
        int page,
        string personName = "",
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDiscoveryPage> SearchDiscoveryMoviesByPersonAsync(
        string query,
        int page,
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDiscoveryPage> GetPopularMoviesAsync(
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDiscoveryPage> GetTopRatedMoviesAsync(
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbMovieDiscoveryPage> GetTrendingMoviesAsync(
        string timeWindow,
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<MetadataSearchCandidate?> GetMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}
