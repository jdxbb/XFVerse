using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Interfaces;

public interface INavigationStateService
{
    int? SelectedMovieId { get; }

    int? SelectedTvSeriesId { get; }

    int? SelectedTvSeasonId { get; }

    int? SelectedTvEpisodeId { get; }

    AiRecommendationItem? SelectedExternalRecommendation { get; }

    DateTime? SelectedWatchHistoryDate { get; }

    event EventHandler<NavigationRequest>? NavigationRequested;

    void RequestNavigation(NavigationPageKey pageKey, int? movieId = null, DateTime? targetDate = null);

    void RequestTvSeriesOverview(int tvSeriesId);

    void RequestTvSeasonDetail(int tvSeasonId, int? tvEpisodeId = null);

    void RequestEpisodeDetail(int tvEpisodeId);

    void RequestExternalMovieDetail(AiRecommendationItem recommendation);

    DateTime? ConsumeWatchHistoryTargetDate();
}
