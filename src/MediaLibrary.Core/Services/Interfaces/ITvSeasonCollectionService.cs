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

    Task SetEpisodeDefaultMediaFileAsync(int tvEpisodeId, int mediaFileId, CancellationToken cancellationToken = default);

    Task ResetEpisodeSourceToUnidentifiedAsync(int tvEpisodeId, int mediaFileId, CancellationToken cancellationToken = default);

    Task RemoveFromLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default);

    Task RemoveSeriesFromLibraryAsync(int tvSeriesId, CancellationToken cancellationToken = default);

    Task AddSeasonToLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default);

    Task AddSeriesToLibraryAsync(int tvSeriesId, CancellationToken cancellationToken = default);

    Task RestoreSeasonToLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default);

    Task RestoreSeriesToLibraryAsync(int tvSeriesId, CancellationToken cancellationToken = default);

    Task DeleteSeasonRecordAsync(int tvSeasonId, CancellationToken cancellationToken = default);
}
