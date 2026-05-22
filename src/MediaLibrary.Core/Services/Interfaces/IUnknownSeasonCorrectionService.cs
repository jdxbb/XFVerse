using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IUnknownSeasonCorrectionService
{
    Task<UnknownSeasonCorrectionApplyResult> ApplySeasonToRecognizedSeasonAsync(
        int sourceSeasonId,
        int targetSeriesTmdbId,
        int targetSeasonNumber,
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings = null,
        CancellationToken cancellationToken = default);

    Task<UnknownSeasonCorrectionApplyResult> ApplyUnknownSeasonToRecognizedSeasonAsync(
        int sourceSeasonId,
        int targetSeriesTmdbId,
        int targetSeasonNumber,
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings = null,
        CancellationToken cancellationToken = default);

    Task<UnknownSeasonCorrectionApplyResult> ApplySeasonToUnknownSeasonAsync(
        int sourceSeasonId,
        int targetSeasonId,
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings = null,
        CancellationToken cancellationToken = default);
}
