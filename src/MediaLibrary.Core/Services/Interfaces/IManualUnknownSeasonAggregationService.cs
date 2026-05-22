using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IManualUnknownSeasonAggregationService
{
    Task<ManualUnknownSeasonAggregationPrepareResult> PrepareAsync(
        IReadOnlyCollection<ManualUnknownSeasonAggregationSelection> selections,
        CancellationToken cancellationToken = default);

    Task<ManualUnknownSeasonAggregationApplyResult> ApplyAsync(
        ManualUnknownSeasonAggregationApplyRequest request,
        CancellationToken cancellationToken = default);
}
