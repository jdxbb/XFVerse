using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Interfaces;

public interface INavigationStateService
{
    int? SelectedMovieId { get; }

    AiRecommendationItem? SelectedExternalRecommendation { get; }

    DateTime? SelectedWatchHistoryDate { get; }

    event EventHandler<NavigationRequest>? NavigationRequested;

    void RequestNavigation(NavigationPageKey pageKey, int? movieId = null, DateTime? targetDate = null);

    void RequestExternalMovieDetail(AiRecommendationItem recommendation);

    DateTime? ConsumeWatchHistoryTargetDate();
}
