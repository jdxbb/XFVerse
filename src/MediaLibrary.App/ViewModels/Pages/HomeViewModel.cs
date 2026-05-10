using System.Collections.ObjectModel;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class HomeViewModel : PageViewModelBase
{
    private const string RecommendationStatusEmpty = "Empty";
    private const string RecommendationStatusError = "Error";
    private const string RecommendationStatusMissingSeed = "MissingSeed";
    private const string HomeMissingRecommendationSeedMessage = "先标记几部影片后，AI 会为你生成推荐。";
    private const string HomeEmptyRecommendationMessage = "当前筛选条件下暂无可推荐影片。";
    private const string HomeNotRequestedRecommendationMessage = "AI 推荐尚未生成，进入 AI 推荐页生成推荐。";
    private readonly IHomeDashboardQueryService _dashboardQueryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IPlayerWindowService _playerWindowService;
    private string _statusMessage = "正在加载首页概览。";
    private int _movieCount;
    private int _sourceCount;
    private int _watchedCount;
    private int _favoriteCount;
    private string _lastScanStatus = "暂无扫描记录";
    private string _recommendationsStatusMessage = string.Empty;
    private bool _isRefreshing;
    private bool _pendingRefresh;
    private bool _hasLoadedDashboard;
    private bool _isContinuingPlayback;

    public HomeViewModel(
        IHomeDashboardQueryService dashboardQueryService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService,
        IPlayerWindowService playerWindowService)
        : base("首页", "片库概览、最近播放、推荐内容、统计分布与扫描状态。")
    {
        _dashboardQueryService = dashboardQueryService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _playerWindowService = playerWindowService;
        _dataRefreshService.DataChanged += OnDataChanged;
        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;

        OpenMovieCommand = new RelayCommand(OpenMovie);
        ContinuePlaybackCommand = new AsyncRelayCommand(ContinuePlaybackAsync, CanContinuePlayback);
        OpenLibraryCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.Library));
        OpenFavoritesCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.Favorites));
        OpenRecommendationsCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.Recommendations));
        OpenScanTasksCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.ScanTasks));
    }

    public ObservableCollection<HomeMovieItem> RecentlyAdded { get; } = [];

    public ObservableCollection<HomeMovieItem> RecentlyPlayed { get; } = [];

    public ObservableCollection<HomeMovieItem> Favorites { get; } = [];

    public ObservableCollection<CollectionMovieItem> FavoriteCollectionItems { get; } = [];

    public ObservableCollection<CollectionMovieItem> WantToWatchItems { get; } = [];

    public ObservableCollection<AiRecommendationItem> Recommendations { get; } = [];

    public ObservableCollection<ChartSliceItem> GenreDistribution { get; } = [];

    public ObservableCollection<ChartSliceItem> YearDistribution { get; } = [];

    public ObservableCollection<ChartSliceItem> WatchedDistribution { get; } = [];

    public ObservableCollection<ChartSliceItem> RatingDistribution { get; } = [];

    public RelayCommand OpenMovieCommand { get; }

    public AsyncRelayCommand ContinuePlaybackCommand { get; }

    public RelayCommand OpenLibraryCommand { get; }

    public RelayCommand OpenFavoritesCommand { get; }

    public RelayCommand OpenRecommendationsCommand { get; }

    public RelayCommand OpenScanTasksCommand { get; }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int MovieCount { get => _movieCount; private set => SetProperty(ref _movieCount, value); }

    public int SourceCount { get => _sourceCount; private set => SetProperty(ref _sourceCount, value); }

    public int WatchedCount { get => _watchedCount; private set => SetProperty(ref _watchedCount, value); }

    public int FavoriteCount { get => _favoriteCount; private set => SetProperty(ref _favoriteCount, value); }

    public string LastScanStatus { get => _lastScanStatus; private set => SetProperty(ref _lastScanStatus, value); }

    public string RecommendationsStatusMessage
    {
        get => _recommendationsStatusMessage;
        private set => SetProperty(ref _recommendationsStatusMessage, value);
    }

    public override bool IsRefreshing => _isRefreshing;

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        if (IsRefreshing)
        {
            _pendingRefresh = true;
            return;
        }

        SetIsRefreshing(true);
        try
        {
            do
            {
                _pendingRefresh = false;
                StatusMessage = "正在刷新...";

                await LoadDashboardModulesAsync(cancellationToken);
            }
            while (_pendingRefresh && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            SetIsRefreshing(false);
        }
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        var reason = e.Reason;
        _ = Application.Current.Dispatcher.InvokeAsync(() => _ = SafeRefreshByReasonAsync(reason));
    }

    private async Task SafeRefreshByReasonAsync(AppDataChangeReason reason)
    {
        try
        {
            await RefreshByReasonAsync(reason);
        }
        catch (Exception exception)
        {
            StatusMessage = $"首页刷新失败：{exception.Message}";
        }
    }

    private void SetIsRefreshing(bool value)
    {
        SetProperty(ref _isRefreshing, value, nameof(IsRefreshing));
    }

    private async Task RefreshByReasonAsync(AppDataChangeReason reason)
    {
        if (!_hasLoadedDashboard)
        {
            await ActivateAsync();
            return;
        }

        try
        {
            switch (reason)
            {
                case AppDataChangeReason.PlaybackHistoryChanged:
                    await RefreshRecentPlaybackAsync();
                    break;
                case AppDataChangeReason.CollectionChanged:
                    await RefreshCollectionAsync();
                    break;
                case AppDataChangeReason.RecommendationChanged:
                    await RefreshRecommendationsAsync();
                    break;
                case AppDataChangeReason.ScanChanged:
                    await RefreshScanOverviewAsync();
                    break;
                case AppDataChangeReason.MetadataChanged:
                case AppDataChangeReason.LibraryChanged:
                    await RefreshLibraryOverviewAsync();
                    break;
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"首页局部刷新失败：{exception.Message}";
        }
    }

    private async Task LoadDashboardModulesAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        await TryRefreshAsync("片库概览", () => RefreshLibraryOverviewAsync(cancellationToken), failures);
        await TryRefreshAsync("最近播放", () => RefreshRecentPlaybackAsync(cancellationToken), failures);
        await TryRefreshAsync("收藏夹", () => RefreshCollectionAsync(cancellationToken), failures);
        await TryRefreshAsync("AI 推荐", () => RefreshRecommendationsAsync(cancellationToken), failures);

        _hasLoadedDashboard = true;
        StatusMessage = failures.Count == 0
            ? MovieCount == 0
                ? "当前片库为空，请先到扫描任务页执行扫描。"
                : $"已加载 {MovieCount} 部影片、{SourceCount} 个播放源。"
            : $"首页部分模块刷新失败：{string.Join("、", failures)}";
    }

    private async Task TryRefreshAsync(string moduleName, Func<Task> refresh, ICollection<string> failures)
    {
        try
        {
            await refresh();
        }
        catch
        {
            failures.Add(moduleName);
        }
    }

    private async Task RefreshLibraryOverviewAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await _dashboardQueryService.GetLibraryOverviewAsync(cancellationToken);
        MovieCount = dashboard.MovieCount;
        SourceCount = dashboard.SourceCount;
        WatchedCount = dashboard.WatchedCount;
        FavoriteCount = dashboard.FavoriteCount;
        LastScanStatus = dashboard.LastScanStatus;
        Replace(RecentlyAdded, dashboard.RecentlyAdded);
        Replace(Favorites, dashboard.Favorites);
        Replace(GenreDistribution, dashboard.GenreDistribution);
        Replace(YearDistribution, dashboard.YearDistribution);
        Replace(WatchedDistribution, dashboard.WatchedDistribution);
        Replace(RatingDistribution, dashboard.RatingDistribution);
    }

    private async Task RefreshScanOverviewAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await _dashboardQueryService.GetScanOverviewAsync(cancellationToken);
        MovieCount = dashboard.MovieCount;
        SourceCount = dashboard.SourceCount;
        WatchedCount = dashboard.WatchedCount;
        FavoriteCount = dashboard.FavoriteCount;
        LastScanStatus = dashboard.LastScanStatus;
        Replace(RecentlyAdded, dashboard.RecentlyAdded);
    }

    private async Task RefreshRecentPlaybackAsync(CancellationToken cancellationToken = default)
    {
        Replace(RecentlyPlayed, await _dashboardQueryService.GetRecentlyPlayedAsync(cancellationToken));
        ApplyActivePlaybackState();
        ContinuePlaybackCommand.RaiseCanExecuteChanged();
    }

    private async Task RefreshCollectionAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await _dashboardQueryService.GetCollectionPreviewAsync(cancellationToken);
        Replace(FavoriteCollectionItems, dashboard.FavoriteCollectionItems);
        Replace(WantToWatchItems, dashboard.WantToWatchItems);
    }

    private async Task RefreshRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _dashboardQueryService.GetRecommendationsPreviewStateAsync(cancellationToken);
            var hasVisibleRecommendations = Recommendations.Count > 0;
            var shouldPreserveVisibleRecommendations = state.Items.Count == 0
                                                       && hasVisibleRecommendations
                                                       && state.IsPending
                                                       && state.IsUpdating;
            if (state.Items.Count > 0)
            {
                Replace(Recommendations, state.Items);
            }
            else if (!shouldPreserveVisibleRecommendations)
            {
                Replace(Recommendations, state.Items);
            }

            RecommendationsStatusMessage = shouldPreserveVisibleRecommendations
                ? "正在后台更新推荐..."
                : BuildRecommendationsStatusMessage(state);
        }
        catch
        {
            throw;
        }
    }

    private void OpenMovie(object? parameter)
    {
        switch (parameter)
        {
            case HomeMovieItem movie:
                _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movie.MovieId);
                break;
            case CollectionMovieItem collectionMovie:
                OpenCollectionMovie(collectionMovie);
                break;
            case AiRecommendationItem recommendation:
                if (recommendation.IsInLibrary && recommendation.MovieId > 0)
                {
                    _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, recommendation.MovieId);
                }
                else
                {
                    _navigationStateService.RequestExternalMovieDetail(recommendation);
                }

                break;
        }
    }

    private async Task ContinuePlaybackAsync(object? parameter)
    {
        if (parameter is not HomeMovieItem movie
            || !movie.CanOpenContinuePlayback
            || _isContinuingPlayback
            || _playerWindowService.IsPlayerOpen)
        {
            return;
        }

        var keepDisabledUntilWindowClosed = false;
        try
        {
            _isContinuingPlayback = true;
            movie.IsOpeningPlayback = true;
            ContinuePlaybackCommand.RaiseCanExecuteChanged();
            await _playerWindowService.OpenAsync(movie.MovieId, movie.MediaFileId);
            keepDisabledUntilWindowClosed = _playerWindowService.IsPlayerOpen
                                             && IsSamePlayback(
                                                 movie,
                                                 _playerWindowService.ActiveMovieId,
                                                 _playerWindowService.ActiveMediaFileId);
        }
        catch (Exception exception)
        {
            StatusMessage = $"播放器打开失败：{exception.Message}";
        }
        finally
        {
            _isContinuingPlayback = false;
            if (!keepDisabledUntilWindowClosed)
            {
                movie.IsOpeningPlayback = false;
            }

            ContinuePlaybackCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanContinuePlayback(object? parameter)
    {
        return !_isContinuingPlayback
               && !_playerWindowService.IsPlayerOpen
               && parameter is HomeMovieItem movie
               && movie.CanOpenContinuePlayback;
    }

    private void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(
            () =>
            {
                _isContinuingPlayback = false;
                foreach (var movie in RecentlyPlayed)
                {
                    movie.IsOpeningPlayback = false;
                }

                ContinuePlaybackCommand.RaiseCanExecuteChanged();
            });
    }

    private void ApplyActivePlaybackState()
    {
        if (!_playerWindowService.IsPlayerOpen)
        {
            foreach (var movie in RecentlyPlayed)
            {
                movie.IsOpeningPlayback = false;
            }

            return;
        }

        foreach (var movie in RecentlyPlayed)
        {
            movie.IsOpeningPlayback = IsSamePlayback(
                movie,
                _playerWindowService.ActiveMovieId,
                _playerWindowService.ActiveMediaFileId);
        }
    }

    private static bool IsSamePlayback(HomeMovieItem movie, int? activeMovieId, int? activeMediaFileId)
    {
        return activeMovieId == movie.MovieId
               && (!activeMediaFileId.HasValue || movie.MediaFileId == activeMediaFileId);
    }

    private static string BuildRecommendationsStatusMessage(AiRecommendationPreviewState state)
    {
        if (string.Equals(state.Status, RecommendationStatusError, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(state.Message)
                ? "AI 推荐生成失败，请稍后重试。"
                : state.Message;
        }

        if (state.Items.Count > 0)
        {
            return state.IsUpdating ? "正在后台更新推荐..." : string.Empty;
        }

        if (string.Equals(state.Status, RecommendationStatusEmpty, StringComparison.OrdinalIgnoreCase))
        {
            return HomeEmptyRecommendationMessage;
        }

        if (string.Equals(state.Status, RecommendationStatusMissingSeed, StringComparison.OrdinalIgnoreCase)
            || !state.CanRequest)
        {
            return string.IsNullOrWhiteSpace(state.Message)
                ? HomeMissingRecommendationSeedMessage
                : state.Message;
        }

        if (state.IsPending && state.IsUpdating)
        {
            return "正在后台更新推荐...";
        }

        if (state.IsPending)
        {
            return string.IsNullOrWhiteSpace(state.Message)
                ? "正在等待 AI 分析并推荐影片"
                : state.Message;
        }

        if (!state.HasRequested)
        {
            return string.IsNullOrWhiteSpace(state.Message)
                ? HomeNotRequestedRecommendationMessage
                : state.Message;
        }

        return "当前默认组合暂无推荐结果。";
    }

    private void OpenCollectionMovie(CollectionMovieItem movie)
    {
        if (movie.IsInLibrary && movie.MovieId.HasValue)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movie.MovieId.Value);
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(
            new AiRecommendationItem
            {
                MovieId = 0,
                TmdbId = movie.TmdbId,
                Title = movie.Title,
                OriginalTitle = movie.OriginalTitle,
                ReleaseYear = movie.ReleaseYear,
                PosterRemoteUrl = movie.PosterRemoteUrl,
                Overview = movie.Overview,
                Country = movie.Country,
                Language = movie.Language,
                RuntimeMinutes = movie.RuntimeMinutes,
                ImdbId = movie.ImdbId,
                TmdbRating = movie.TmdbRating,
                TmdbVoteCount = movie.TmdbVoteCount,
                OmdbRating = movie.OmdbScoreValue.HasValue
                    ? new MovieRatingItem
                    {
                        SourceName = "OMDb",
                        ScoreValue = movie.OmdbScoreValue.Value,
                        ScoreScale = movie.OmdbScoreScale ?? 10d,
                        VoteCount = movie.OmdbVoteCount,
                        SourceUrl = movie.OmdbSourceUrl,
                        LastUpdatedAt = movie.OmdbLastUpdatedAt ?? DateTime.UtcNow
                    }
                    : null,
                Tags = string.IsNullOrWhiteSpace(movie.AiTagsText) ? movie.GenresText : movie.AiTagsText,
                EmotionTagsText = movie.EmotionTagsText,
                SceneTagsText = movie.SceneTagsText,
                IsInLibrary = false,
                IsWatched = movie.IsWatched,
                IsWantToWatch = movie.IsWantToWatch,
                ScopeText = "收藏夹",
                AvailabilityText = "未入库",
                WatchStateText = movie.WatchStateText
            });
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
