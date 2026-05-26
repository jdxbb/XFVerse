using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOnlineSubtitleBindingService : IOnlineSubtitleBindingQueryService
{
    Task<OnlineSubtitleBindingListItem> UpsertBindingAsync(
        OnlineSubtitleBindingUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> MarkUsedAsync(
        int bindingId,
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(
        int bindingId,
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken = default);

    Task<int> SoftDeleteForMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);
}
