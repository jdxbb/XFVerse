using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOnlineSubtitleBindingQueryService
{
    Task<IReadOnlyList<OnlineSubtitleBindingListItem>> GetActiveBindingsAsync(
        int? movieId,
        int? episodeId,
        CancellationToken cancellationToken = default);
}
