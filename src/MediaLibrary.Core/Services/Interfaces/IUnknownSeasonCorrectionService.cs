using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IUnknownSeasonCorrectionService
{
    Task<UnknownSeasonCorrectionApplyResult> ApplyUnknownSeasonToRecognizedSeasonAsync(
        int sourceSeasonId,
        int targetSeriesTmdbId,
        int targetSeasonNumber,
        CancellationToken cancellationToken = default);
}
