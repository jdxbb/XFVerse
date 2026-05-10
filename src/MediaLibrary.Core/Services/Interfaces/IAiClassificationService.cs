using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IAiClassificationService
{
    Task ClassifyMovieAsync(int movieId, CancellationToken cancellationToken = default);

    Task<AiSearchSuggestion> SuggestSearchQueryAsync(int movieId, CancellationToken cancellationToken = default);

    Task<AiSearchSuggestionResult> SuggestSearchQueryWithStatusAsync(int movieId, CancellationToken cancellationToken = default);
}
