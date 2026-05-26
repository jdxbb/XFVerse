using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOnlineSubtitleBindingQueryService
{
    Task<IReadOnlyList<OnlineSubtitleBindingListItem>> GetActiveBindingsAsync(
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken = default);
}
