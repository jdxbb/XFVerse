using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITvSeasonCollectionService
{
    Task<IReadOnlyList<CollectionMovieItem>> GetCollectionItemsAsync(CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(int tvSeasonId, bool isFavorite, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetWantToWatchAsync(int tvSeasonId, bool isWantToWatch, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetNotInterestedAsync(int tvSeasonId, bool isNotInterested, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetWatchedAsync(int tvSeasonId, bool isWatched, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetEpisodeWatchedAsync(int tvEpisodeId, bool isWatched, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task RemoveFromLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default);

    Task DeleteSeasonRecordAsync(int tvSeasonId, CancellationToken cancellationToken = default);
}
