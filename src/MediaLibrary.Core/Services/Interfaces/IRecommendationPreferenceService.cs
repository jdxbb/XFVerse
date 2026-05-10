using MediaLibrary.Core.Models.Settings;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IRecommendationPreferenceService
{
    Task<RecommendationPreferenceModel> GetAsync(CancellationToken cancellationToken = default);

    Task<RecommendationPreferenceModel> SaveAsync(
        RecommendationPreferenceModel preference,
        CancellationToken cancellationToken = default);

    Task<RecommendationPreferenceModel> ClearAsync(CancellationToken cancellationToken = default);

    string NormalizeText(string? text);

    string BuildFingerprintPart(RecommendationPreferenceModel preference);
}
