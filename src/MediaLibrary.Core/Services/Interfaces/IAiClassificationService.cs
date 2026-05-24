using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IAiClassificationService
{
    Task ClassifyMovieAsync(int movieId, CancellationToken cancellationToken = default);

    Task ClassifyMoviesAsync(
        IReadOnlyCollection<int> movieIds,
        string sourceKind,
        CancellationToken cancellationToken = default);

    Task<AiMovieTags> ClassifyExternalMovieAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default);

    Task<AiSearchSuggestion> SuggestSearchQueryAsync(int movieId, CancellationToken cancellationToken = default);

    Task<AiSearchSuggestionResult> SuggestSearchQueryWithStatusAsync(int movieId, CancellationToken cancellationToken = default);

    Task<AiSearchSuggestionResult> SuggestMovieCorrectionSearchQueryAsync(
        string currentTitle,
        string? sourceFileName,
        int? releaseYear = null,
        string? overview = null,
        string? sourcePath = null,
        CancellationToken cancellationToken = default);

    Task<AiSearchSuggestionResult> SuggestTvEpisodeCorrectionSearchQueryAsync(
        string currentTitle,
        string? sourceFileName,
        string? seriesTitle = null,
        int? seasonNumber = null,
        int? episodeNumber = null,
        string? overview = null,
        string? sourcePath = null,
        CancellationToken cancellationToken = default);

    Task<AiSearchSuggestionResult> SuggestTvSeasonCorrectionSearchQueryAsync(
        string currentTitle,
        IReadOnlyCollection<string> sourceFileNames,
        string? seriesTitle = null,
        int? seasonNumber = null,
        string? overview = null,
        CancellationToken cancellationToken = default);
}
