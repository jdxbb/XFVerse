using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IRescanReattachService
{
    Task<RescanReattachResult> TryReattachAsync(
        IReadOnlyCollection<int> mediaFileIds,
        string sourceKind,
        CancellationToken cancellationToken = default);
}
