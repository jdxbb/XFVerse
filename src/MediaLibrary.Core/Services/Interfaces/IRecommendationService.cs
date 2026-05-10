using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IRecommendationService
{
    Task<IReadOnlyList<AiRecommendationItem>> GetRecommendationsAsync(
        RecommendationQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<AiRecommendationPreviewState> GetRecommendationPreviewStateAsync(
        RecommendationQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<CandidatePoolRefillResult> RefillCandidatePoolIfLowAsync(
        RecommendationQueryOptions? options = null,
        string trigger = "",
        CancellationToken cancellationToken = default);

    Task SaveCandidatePoolRefillFailureAsync(
        RecommendationQueryOptions? options = null,
        string? errorMessage = null,
        string? expectedFingerprint = null,
        CancellationToken cancellationToken = default);
}
