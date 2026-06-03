using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Implementations;

public sealed class NavigationStateService : INavigationStateService
{
    private readonly Stack<NavigationRequest> _detailBackStack = [];
    private readonly Dictionary<int, double> _seriesSeasonListScrollOffsets = [];
    private readonly Dictionary<int, double> _seasonEpisodeListScrollOffsets = [];
    private NavigationRequest? _currentRequest;

    public int? SelectedMovieId { get; private set; }

    public int? SelectedTvSeriesId { get; private set; }

    public int? SelectedTvSeasonId { get; private set; }

    public int? SelectedTvEpisodeId { get; private set; }

    public AiRecommendationItem? SelectedExternalRecommendation { get; private set; }

    public DateTime? SelectedWatchHistoryDate { get; private set; }

    public bool IsDetailNavigationBlocked { get; private set; }

    public event EventHandler<NavigationRequest>? NavigationRequested;

    public void NotifyPageActivated(NavigationRequest request)
    {
        _currentRequest = request;
        if (!IsDetailPage(request.PageKey))
        {
            _detailBackStack.Clear();
        }
    }

    public void RequestNavigation(NavigationPageKey pageKey, int? movieId = null, DateTime? targetDate = null)
    {
        var request = new NavigationRequest(
            pageKey,
            movieId,
            targetDate: pageKey == NavigationPageKey.WatchHistory ? targetDate?.Date : null);

        Request(request);
    }

    public void RequestTvSeriesOverview(int tvSeriesId)
    {
        Request(new NavigationRequest(NavigationPageKey.SeriesOverview, tvSeriesId: tvSeriesId));
    }

    public void RequestTvSeasonDetail(int tvSeasonId, int? tvEpisodeId = null)
    {
        Request(
            new NavigationRequest(
                NavigationPageKey.TvSeasonDetail,
                tvSeasonId: tvSeasonId,
                tvEpisodeId: tvEpisodeId));
    }

    public void RequestEpisodeDetail(int tvEpisodeId)
    {
        Request(new NavigationRequest(NavigationPageKey.EpisodeDetail, tvEpisodeId: tvEpisodeId));
    }

    public void RequestExternalMovieDetail(AiRecommendationItem recommendation)
    {
        Request(new NavigationRequest(NavigationPageKey.MovieDetail, externalRecommendation: recommendation));
    }

    public void SetDetailNavigationBlocked(bool isBlocked)
    {
        IsDetailNavigationBlocked = isBlocked;
    }

    public void RequestDetailBackToLibrary()
    {
        RequestDetailBack(new NavigationRequest(NavigationPageKey.Library));
    }

    public void RequestDetailBackToSeries(int tvSeriesId)
    {
        RequestDetailBack(new NavigationRequest(NavigationPageKey.SeriesOverview, tvSeriesId: tvSeriesId));
    }

    public void RequestDetailBackToSeason(int tvSeasonId, int? tvEpisodeId = null)
    {
        RequestDetailBack(
            new NavigationRequest(
                NavigationPageKey.TvSeasonDetail,
                tvSeasonId: tvSeasonId,
                tvEpisodeId: tvEpisodeId));
    }

    public DateTime? ConsumeWatchHistoryTargetDate()
    {
        var targetDate = SelectedWatchHistoryDate;
        SelectedWatchHistoryDate = null;
        return targetDate;
    }

    public double GetSeriesSeasonListScrollOffset(int tvSeriesId)
    {
        return _seriesSeasonListScrollOffsets.GetValueOrDefault(tvSeriesId);
    }

    public void SetSeriesSeasonListScrollOffset(int tvSeriesId, double offset)
    {
        SetScrollOffset(_seriesSeasonListScrollOffsets, tvSeriesId, offset);
    }

    public double GetSeasonEpisodeListScrollOffset(int tvSeasonId)
    {
        return _seasonEpisodeListScrollOffsets.GetValueOrDefault(tvSeasonId);
    }

    public void SetSeasonEpisodeListScrollOffset(int tvSeasonId, double offset)
    {
        SetScrollOffset(_seasonEpisodeListScrollOffsets, tvSeasonId, offset);
    }

    private void RequestDetailBack(NavigationRequest fallbackRequest)
    {
        var request = _detailBackStack.Count > 0
            ? _detailBackStack.Pop()
            : fallbackRequest;

        Request(request, captureDetailOrigin: false);
    }

    private void Request(NavigationRequest request, bool captureDetailOrigin = true)
    {
        if (IsDetailNavigationBlocked && IsDetailPage(request.PageKey))
        {
            return;
        }

        if (captureDetailOrigin)
        {
            CaptureDetailOrigin(request);
        }

        ApplyRequestSelection(request);
        NavigationRequested?.Invoke(this, request);
    }

    private void CaptureDetailOrigin(NavigationRequest targetRequest)
    {
        if (!IsDetailPage(targetRequest.PageKey) ||
            _currentRequest is null ||
            AreSameRequest(_currentRequest, targetRequest))
        {
            return;
        }

        _detailBackStack.Push(_currentRequest);
    }

    private void ApplyRequestSelection(NavigationRequest request)
    {
        SelectedMovieId = request.MovieId;
        SelectedTvSeriesId = request.TvSeriesId;
        SelectedTvSeasonId = request.TvSeasonId;
        SelectedTvEpisodeId = request.TvEpisodeId;
        SelectedExternalRecommendation = request.ExternalRecommendation;
        SelectedWatchHistoryDate = request.PageKey == NavigationPageKey.WatchHistory
            ? request.TargetDate?.Date
            : null;
    }

    private static bool IsDetailPage(NavigationPageKey pageKey)
    {
        return pageKey is NavigationPageKey.MovieDetail
            or NavigationPageKey.SeriesOverview
            or NavigationPageKey.TvSeasonDetail
            or NavigationPageKey.EpisodeDetail;
    }

    private static void SetScrollOffset(Dictionary<int, double> offsets, int key, double offset)
    {
        if (offset <= 0)
        {
            offsets.Remove(key);
            return;
        }

        offsets[key] = offset;
    }

    private static bool AreSameRequest(NavigationRequest left, NavigationRequest right)
    {
        return left.PageKey == right.PageKey
            && left.MovieId == right.MovieId
            && left.TvSeriesId == right.TvSeriesId
            && left.TvSeasonId == right.TvSeasonId
            && left.TvEpisodeId == right.TvEpisodeId
            && left.TargetDate?.Date == right.TargetDate?.Date
            && AreSameExternalRecommendation(left.ExternalRecommendation, right.ExternalRecommendation);
    }

    private static bool AreSameExternalRecommendation(AiRecommendationItem? left, AiRecommendationItem? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return ReferenceEquals(left, right)
            || left.MovieId == right.MovieId
            && left.TmdbId == right.TmdbId
            && string.Equals(left.Title, right.Title, StringComparison.Ordinal);
    }
}
