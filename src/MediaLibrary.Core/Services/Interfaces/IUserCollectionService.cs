using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IUserCollectionService
{
    Task<IReadOnlyList<CollectionMovieItem>> GetCollectionItemsAsync(CancellationToken cancellationToken = default);

    Task AddWantToWatchAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task RemoveWantToWatchAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetWantToWatchAsync(int movieId, bool isWantToWatch, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetWatchedAsync(AiRecommendationItem recommendation, bool isWatched, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetFavoriteAsync(AiRecommendationItem recommendation, bool isFavorite, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetNotInterestedAsync(AiRecommendationItem recommendation, bool isNotInterested, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task SetNotInterestedAsync(int movieId, bool isNotInterested, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task<bool> TouchCollectionItemUpdatedAtAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default);

    Task HideFromLibraryAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task AddToLibraryAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task RestoreToLibraryAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default, string changeSource = "Manual");

    Task<bool> IsNotInterestedAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotInterestedMovieKey>> GetNotInterestedKeysAsync(CancellationToken cancellationToken = default);

    Task RemoveCollectionRecordAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default);

    Task DeleteCollectionRecordAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken = default);
}
