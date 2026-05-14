using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Implementations;

public sealed class NavigationStateService : INavigationStateService
{
    public int? SelectedMovieId { get; private set; }

    public int? SelectedTvSeriesId { get; private set; }

    public int? SelectedTvSeasonId { get; private set; }

    public int? SelectedTvEpisodeId { get; private set; }

    public AiRecommendationItem? SelectedExternalRecommendation { get; private set; }

    public DateTime? SelectedWatchHistoryDate { get; private set; }

    public event EventHandler<NavigationRequest>? NavigationRequested;

    public void RequestNavigation(NavigationPageKey pageKey, int? movieId = null, DateTime? targetDate = null)
    {
        SelectedMovieId = movieId;
        SelectedTvSeriesId = null;
        SelectedTvSeasonId = null;
        SelectedTvEpisodeId = null;
        SelectedExternalRecommendation = null;
        SelectedWatchHistoryDate = pageKey == NavigationPageKey.WatchHistory ? targetDate?.Date : null;
        NavigationRequested?.Invoke(this, new NavigationRequest(pageKey, movieId, targetDate: SelectedWatchHistoryDate));
    }

    public void RequestTvSeriesOverview(int tvSeriesId)
    {
        SelectedMovieId = null;
        SelectedTvSeriesId = tvSeriesId;
        SelectedTvSeasonId = null;
        SelectedTvEpisodeId = null;
        SelectedExternalRecommendation = null;
        SelectedWatchHistoryDate = null;
        NavigationRequested?.Invoke(this, new NavigationRequest(NavigationPageKey.SeriesOverview, tvSeriesId: tvSeriesId));
    }

    public void RequestTvSeasonDetail(int tvSeasonId, int? tvEpisodeId = null)
    {
        SelectedMovieId = null;
        SelectedTvSeriesId = null;
        SelectedTvSeasonId = tvSeasonId;
        SelectedTvEpisodeId = tvEpisodeId;
        SelectedExternalRecommendation = null;
        SelectedWatchHistoryDate = null;
        NavigationRequested?.Invoke(
            this,
            new NavigationRequest(
                NavigationPageKey.TvSeasonDetail,
                tvSeasonId: tvSeasonId,
                tvEpisodeId: tvEpisodeId));
    }

    public void RequestExternalMovieDetail(AiRecommendationItem recommendation)
    {
        SelectedMovieId = null;
        SelectedTvSeriesId = null;
        SelectedTvSeasonId = null;
        SelectedTvEpisodeId = null;
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
