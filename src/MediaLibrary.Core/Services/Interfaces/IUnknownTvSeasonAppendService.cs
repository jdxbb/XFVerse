using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IUnknownTvSeasonAppendService
{
    Task<UnknownTvSeasonAppendResult> TryAppendAsync(
        IReadOnlyCollection<int> mediaFileIds,
        string sourceKind,
        CancellationToken cancellationToken = default);

    Task<UnknownTvSeasonAppendResult> TryAppendScanPathsAsync(
        IReadOnlyCollection<int> scanPathIds,
        string sourceKind,
        CancellationToken cancellationToken = default);
}
