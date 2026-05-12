using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Implementations;

public sealed class NavigationStateService : INavigationStateService
{
    public int? SelectedMovieId { get; private set; }

    public AiRecommendationItem? SelectedExternalRecommendation { get; private set; }

    public DateTime? SelectedWatchHistoryDate { get; private set; }

    public event EventHandler<NavigationRequest>? NavigationRequested;

    public void RequestNavigation(NavigationPageKey pageKey, int? movieId = null, DateTime? targetDate = null)
    {
        SelectedMovieId = movieId;
        SelectedExternalRecommendation = null;
        SelectedWatchHistoryDate = pageKey == NavigationPageKey.WatchHistory ? targetDate?.Date : null;
        NavigationRequested?.Invoke(this, new NavigationRequest(pageKey, movieId, targetDate: SelectedWatchHistoryDate));
    }

    public void RequestExternalMovieDetail(AiRecommendationItem recommendation)
    {
        SelectedMovieId = null;
        SelectedExternalRecommendation = recommendation;
        SelectedWatchHistoryDate = null;
        NavigationRequested?.Invoke(this, new NavigationRequest(NavigationPageKey.MovieDetail, externalRecommendation: recommendation));
    }

    public DateTime? ConsumeWatchHistoryTargetDate()
    {
        var targetDate = SelectedWatchHistoryDate;
        SelectedWatchHistoryDate = null;
        return targetDate;
    }
}
