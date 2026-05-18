using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMovieManagementService
{
    Task SetDefaultMediaFileAsync(
        int movieId,
        int mediaFileId,
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(
        int movieId,
        bool isFavorite,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual");

    Task SetWatchedAsync(
        int movieId,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual");

    Task RemoveFromLibraryAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task AddToLibraryAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task RestoreToLibraryAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task RemoveGroupedPlaceholderRangeFromLibraryAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task RestoreGroupedPlaceholderRangeToLibraryAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task SetGroupedPlaceholderRangeWatchedAsync(
        IReadOnlyCollection<int> mediaFileIds,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual");

    Task DeleteMovieRecordAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task DeleteGroupedPlaceholderRangeRecordAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task<ResetSourceResult> ResetMediaFileToUnidentifiedAsync(
        int movieId,
        int mediaFileId,
        CancellationToken cancellationToken = default);
}
