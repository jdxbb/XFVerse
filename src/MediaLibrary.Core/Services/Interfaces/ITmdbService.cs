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

    Task<TmdbMovieDiscoveryPage> DiscoverMoviesAsync(
        int page,
        string sortBy,
        IReadOnlyCollection<int>? genreIds = null,
        IReadOnlyCollection<string>? originCountryCodes = null,
        string originalLanguage = "",
        string primaryReleaseDateGte = "",
        string primaryReleaseDateLte = "",
        string language = "zh-CN",
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

    Task<TmdbTvSeriesSearchPage> SearchTvSeriesAsync(
        string query,
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesSearchPage> GetPersonTvCreditsAsync(
        int personId,
        int page,
        string personName = "",
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesSearchPage> SearchTvSeriesByPersonAsync(
        string query,
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesSearchPage> DiscoverTvSeriesAsync(
        int page,
        string sortBy,
        IReadOnlyCollection<int>? genreIds = null,
        IReadOnlyCollection<string>? originCountryCodes = null,
        string originalLanguage = "",
        string firstAirDateGte = "",
        string firstAirDateLte = "",
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesDetailResult?> GetTvSeriesDetailsAsync(
        int seriesId,
        string language = "zh-CN",
        CancellationToken cancellationToken = default,
        bool forceRefresh = false);

    Task<TmdbTvSeasonDetailResult?> GetTvSeasonDetailsAsync(
        int seriesId,
        int seasonNumber,
        string language = "zh-CN",
        CancellationToken cancellationToken = default,
        bool forceRefresh = false);

    Task<TmdbTvSeriesExternalIdsResult?> GetTvSeriesExternalIdsAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesSearchPage> GetPopularTvSeriesAsync(
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesSearchPage> GetTopRatedTvSeriesAsync(
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<TmdbTvSeriesSearchPage> GetTrendingTvSeriesAsync(
        string timeWindow,
        int page,
        string language = "zh-CN",
        CancellationToken cancellationToken = default);

    Task<MetadataSearchCandidate?> GetMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false);
}
