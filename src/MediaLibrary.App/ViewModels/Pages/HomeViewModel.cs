using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class HomeViewModel : PageViewModelBase
{
    private const string DefaultUserDisplayName = "James";
    private const string HomeWelcomeSubtitle = "享受你的私人 AI 影音库";
    private const int ExpandedAiStatusLineLength = 20;
    private const int CollapsedAiStatusLineLength = 25;

    private readonly IHomeDashboardQueryService _dashboardQueryService;
    private readonly IWatchStatisticsService _watchStatisticsService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly MovieDiscoveryViewModel _movieDiscoveryViewModel;
    private string _statusMessage = "正在加载首页概览。";
    private int _movieCount;
    private int _sourceCount;
    private int _watchedCount;
    private int _favoriteCount;
    private string _lastScanStatus = "暂无扫描记录";
    private bool _isRefreshing;
    private bool _pendingRefresh;
    private bool _hasLoadedDashboard;
    private bool _isContinuingPlayback;
    private bool _isActive;
    private bool _refreshPendingOnActivate;
    private int _recommendationRefreshVersion;

    public HomeViewModel(
        IHomeDashboardQueryService dashboardQueryService,
        IWatchStatisticsService watchStatisticsService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService,
        IPlayerWindowService playerWindowService,
        MovieDiscoveryViewModel movieDiscoveryViewModel,
        RecommendationsViewModel aiRecommendationViewModel)
        : base("首页", HomeWelcomeSubtitle)
    {
        _dashboardQueryService = dashboardQueryService;
        _watchStatisticsService = watchStatisticsService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _playerWindowService = playerWindowService;
        _movieDiscoveryViewModel = movieDiscoveryViewModel;
        AiRecommendationViewModel = aiRecommendationViewModel;
        AiRecommendationViewModel.Recommendations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(AiRecommendationPreview));
        AiRecommendationViewModel.PropertyChanged += OnAiRecommendationViewModelPropertyChanged;
        _dataRefreshService.DataChanged += OnDataChanged;
        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;

        OpenMovieCommand = new RelayCommand(OpenMovie);
        ContinuePlaybackCommand = new AsyncRelayCommand(ContinuePlaybackAsync, CanContinuePlayback);
        OpenLibraryCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.Library));
        OpenFavoritesCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.Favorites));
        OpenScanTasksCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.ScanTasks));
        NavigateToWatchHistoryCommand = new RelayCommand(() => _navigationStateService.RequestNavigation(NavigationPageKey.WatchHistory));
        NavigateToMovieDiscoveryCommand = new RelayCommand(NavigateToMovieDiscoveryAiTab);
    }

    public ObservableCollection<HomeStatusMetricItem> LibraryStatusOverview { get; } = [];

    public ObservableCollection<HomeMovieItem> RecentlyAdded { get; } = [];

    public ObservableCollection<HomeMovieItem> RecentlyPlayed { get; } = [];

    public IEnumerable<HomeMovieItem> RecentlyPlayedPreview => RecentlyPlayed.Take(3);

    public IEnumerable<HomeMovieItem> RecentlyAddedPreview => RecentlyAdded.Take(5);

    public IEnumerable<HomeMovieItem> RecentlyAddedCollapsedPreview => RecentlyAdded.Take(6);

    public IEnumerable<AiRecommendationItem> AiRecommendationPreview => AiRecommendationViewModel.Recommendations.Take(3);

    public string AiRecommendationStatusText => AiRecommendationViewModel.StatusMessage;

    public string AiRecommendationStatusExpandedLine1 => SplitTwoLineStatus(AiRecommendationStatusText, ExpandedAiStatusLineLength).Line1;

    public string AiRecommendationStatusExpandedLine2 => SplitTwoLineStatus(AiRecommendationStatusText, ExpandedAiStatusLineLength).Line2;

    public string AiRecommendationStatusCollapsedLine1 => SplitTwoLineStatus(AiRecommendationStatusText, CollapsedAiStatusLineLength).Line1;

    public string AiRecommendationStatusCollapsedLine2 => SplitTwoLineStatus(AiRecommendationStatusText, CollapsedAiStatusLineLength).Line2;

    public ObservableCollection<HomeMovieItem> Favorites { get; } = [];

    public ObservableCollection<CollectionMovieItem> FavoriteCollectionItems { get; } = [];

    public ObservableCollection<CollectionMovieItem> WantToWatchItems { get; } = [];

    public ObservableCollection<ChartSliceItem> GenreDistribution { get; } = [];

    public ObservableCollection<ChartSliceItem> YearDistribution { get; } = [];

    public ObservableCollection<ChartSliceItem> WatchedDistribution { get; } = [];

    public ObservableCollection<ChartSliceItem> RatingDistribution { get; } = [];

    public RelayCommand OpenMovieCommand { get; }

    public AsyncRelayCommand ContinuePlaybackCommand { get; }

    public RelayCommand OpenLibraryCommand { get; }

    public RelayCommand OpenFavoritesCommand { get; }

    public RelayCommand OpenScanTasksCommand { get; }

    public RelayCommand NavigateToWatchHistoryCommand { get; }

    public RelayCommand NavigateToMovieDiscoveryCommand { get; }

    public RecommendationsViewModel AiRecommendationViewModel { get; }

    public string UserDisplayName => DefaultUserDisplayName;

    public string WelcomeTitle => $"欢迎回来，{UserDisplayName} 👋";

    public string WelcomeSubtitle => HomeWelcomeSubtitle;

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int MovieCount { get => _movieCount; private set => SetProperty(ref _movieCount, value); }

    public int SourceCount { get => _sourceCount; private set => SetProperty(ref _sourceCount, value); }

    public int WatchedCount { get => _watchedCount; private set => SetProperty(ref _watchedCount, value); }

    public int FavoriteCount { get => _favoriteCount; private set => SetProperty(ref _favoriteCount, value); }

    public string LastScanStatus { get => _lastScanStatus; private set => SetProperty(ref _lastScanStatus, value); }

    public override bool IsRefreshing => _isRefreshing;

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        var forceRefresh = _refreshPendingOnActivate;
        _refreshPendingOnActivate = false;

        if (IsRefreshing)
        {
            _pendingRefresh = true;
            return;
        }

        if (_hasLoadedDashboard && !forceRefresh)
        {
            ApplyActivePlaybackState();
            ContinuePlaybackCommand.RaiseCanExecuteChanged();
            StartRecommendationsRefresh(CancellationToken.None);
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

    public override void Deactivate()
    {
        _isActive = false;
        AiRecommendationViewModel.Deactivate();
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (!_isActive)
        {
            _refreshPendingOnActivate = true;
            return;
        }

        var reason = e.Reason;
        _ = Application.Current.Dispatcher.InvokeAsync(() => _ = SafeRefreshByReasonAsync(reason));
    }

    private void OnAiRecommendationViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || string.Equals(e.PropertyName, nameof(RecommendationsViewModel.StatusMessage), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(AiRecommendationStatusText));
            OnPropertyChanged(nameof(AiRecommendationStatusExpandedLine1));
            OnPropertyChanged(nameof(AiRecommendationStatusExpandedLine2));
            OnPropertyChanged(nameof(AiRecommendationStatusCollapsedLine1));
            OnPropertyChanged(nameof(AiRecommendationStatusCollapsedLine2));
        }
    }

    private async Task SafeRefreshByReasonAsync(AppDataChangeReason reason)
    {
        if (!_isActive)
        {
            _refreshPendingOnActivate = true;
            return;
        }

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
        if (SetProperty(ref _isRefreshing, value, nameof(IsRefreshing)))
        {
            OnPropertyChanged(nameof(IsHomeInitialLoading));
        }
    }

    public bool IsHomeInitialLoading => !_hasLoadedDashboard;

    private async Task RefreshByReasonAsync(AppDataChangeReason reason)
    {
        if (!_isActive)
        {
            _refreshPendingOnActivate = true;
            return;
        }

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
                    await RefreshLibraryOverviewAsync();
                    NotifyAiRecommendationBindings();
                    break;
                case AppDataChangeReason.RecommendationChanged:
                    NotifyAiRecommendationBindings();
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

        SetHasLoadedDashboard(true);
        StatusMessage = failures.Count == 0
            ? MovieCount == 0
                ? "当前片库为空，请先到扫描任务页执行扫描。"
                : $"已加载 {MovieCount} 部影片、{SourceCount} 个播放源。"
            : $"首页部分模块刷新失败：{string.Join("、", failures)}";
        StartRecommendationsRefresh(cancellationToken);
    }

    private void StartRecommendationsRefresh(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var refreshVersion = unchecked(_recommendationRefreshVersion + 1);
        _recommendationRefreshVersion = refreshVersion;
        _ = RefreshRecommendationsInBackgroundAsync(refreshVersion);
    }

    private async Task RefreshRecommendationsInBackgroundAsync(int refreshVersion)
    {
        try
        {
            await RefreshRecommendationsAsync();
        }
        catch (Exception exception)
        {
            if (refreshVersion == _recommendationRefreshVersion)
            {
                StatusMessage = $"AI 推荐刷新失败：{AiFailureMessageFormatter.Build(exception)}";
            }
        }
    }

    private void SetHasLoadedDashboard(bool value)
    {
        if (_hasLoadedDashboard == value)
        {
            return;
        }

        _hasLoadedDashboard = value;
        OnPropertyChanged(nameof(IsHomeInitialLoading));
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
        var dashboard = await RunQueryOffUiThreadAsync(_dashboardQueryService.GetLibraryOverviewAsync, cancellationToken);
        MovieCount = dashboard.MovieCount;
        SourceCount = dashboard.SourceCount;
        WatchedCount = dashboard.WatchedCount;
        FavoriteCount = dashboard.FavoriteCount;
        LastScanStatus = dashboard.LastScanStatus;
        Replace(RecentlyAdded, dashboard.RecentlyAdded);
        OnPropertyChanged(nameof(RecentlyAddedPreview));
        OnPropertyChanged(nameof(RecentlyAddedCollapsedPreview));
        Replace(Favorites, dashboard.Favorites);
        Replace(GenreDistribution, dashboard.GenreDistribution);
        Replace(YearDistribution, dashboard.YearDistribution);
        Replace(WatchedDistribution, dashboard.WatchedDistribution);
        Replace(RatingDistribution, dashboard.RatingDistribution);
        await RefreshLibraryStatusOverviewAsync(cancellationToken);
    }

    private async Task RefreshScanOverviewAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await RunQueryOffUiThreadAsync(_dashboardQueryService.GetScanOverviewAsync, cancellationToken);
        MovieCount = dashboard.MovieCount;
        SourceCount = dashboard.SourceCount;
        WatchedCount = dashboard.WatchedCount;
        FavoriteCount = dashboard.FavoriteCount;
        LastScanStatus = dashboard.LastScanStatus;
        Replace(RecentlyAdded, dashboard.RecentlyAdded);
        OnPropertyChanged(nameof(RecentlyAddedPreview));
        OnPropertyChanged(nameof(RecentlyAddedCollapsedPreview));
        await RefreshLibraryStatusOverviewAsync(cancellationToken);
    }

    private async Task RefreshRecentPlaybackAsync(CancellationToken cancellationToken = default)
    {
        Replace(
            RecentlyPlayed,
            await RunQueryOffUiThreadAsync(_dashboardQueryService.GetRecentlyPlayedAsync, cancellationToken));
        ApplyActivePlaybackState();
        OnPropertyChanged(nameof(RecentlyPlayedPreview));
        ContinuePlaybackCommand.RaiseCanExecuteChanged();
    }

    private async Task RefreshCollectionAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await RunQueryOffUiThreadAsync(_dashboardQueryService.GetCollectionPreviewAsync, cancellationToken);
        Replace(FavoriteCollectionItems, dashboard.FavoriteCollectionItems);
        Replace(WantToWatchItems, dashboard.WantToWatchItems);
    }

    private async Task RefreshRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        await AiRecommendationViewModel.ActivateAsync(cancellationToken);
        NotifyAiRecommendationBindings();
    }

    private async Task RefreshLibraryStatusOverviewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await RunQueryOffUiThreadAsync(
                token => _watchStatisticsService.GetStatisticsAsync(
                    WatchStatisticsTimeRange.All,
                    calendarMonth: null,
                    forceRefresh: false,
                    token),
                cancellationToken);
            var monthSnapshot = await RunQueryOffUiThreadAsync(
                token => _watchStatisticsService.GetStatisticsAsync(
                    WatchStatisticsTimeRange.Month,
                    calendarMonth: null,
                    forceRefresh: false,
                    token),
                cancellationToken);
            ReplaceLibraryStatusOverview(
                snapshot.WatchedCount,
                snapshot.FavoriteCount,
                snapshot.WantToWatchCount,
                snapshot.NotInterestedCount,
                monthSnapshot.WatchedDeltaFromLastWeek,
                monthSnapshot.FavoriteDeltaFromLastWeek,
                monthSnapshot.WantToWatchDeltaFromLastWeek,
                monthSnapshot.NotInterestedDeltaFromLastWeek);
        }
        catch
        {
            ReplaceLibraryStatusOverview(WatchedCount, FavoriteCount, 0, 0, null, null, null, null);
        }
    }

    private void ReplaceLibraryStatusOverview(
        int watchedCount,
        int favoriteCount,
        int wantToWatchCount,
        int notInterestedCount,
        int? watchedDelta,
        int? favoriteDelta,
        int? wantToWatchDelta,
        int? notInterestedDelta)
    {
        Replace(
            LibraryStatusOverview,
            [
                BuildStatusMetric("已看", watchedCount, watchedDelta, "check"),
                BuildStatusMetric("喜爱", favoriteCount, favoriteDelta, "heart"),
                BuildStatusMetric("想看", wantToWatchCount, wantToWatchDelta, "star"),
                BuildStatusMetric("不想看", notInterestedCount, notInterestedDelta, "prohibit")
            ]);
    }

    private static HomeStatusMetricItem BuildStatusMetric(string title, int value, int? delta, string iconGlyph)
    {
        return new HomeStatusMetricItem(
            title,
            value.ToString(),
            "部",
            FormatMonthlyTrend(delta),
            FormatMonthlyTrendArrow(delta),
            iconGlyph);
    }

    private static string FormatMonthlyTrend(int? delta)
    {
        if (!delta.HasValue)
        {
            return "暂无上月记录";
        }

        if (delta.Value == 0)
        {
            return "较上月无变化";
        }

        var sign = delta.Value > 0 ? "+" : string.Empty;
        return $"较上月 {sign}{delta.Value}";
    }

    private static string FormatMonthlyTrendArrow(int? delta)
    {
        return delta switch
        {
            > 0 => "↑",
            < 0 => "↓",
            0 => "→",
            _ => string.Empty
        };
    }

    private static (string Line1, string Line2) SplitTwoLineStatus(string? value, int maxLineLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (text.Length <= maxLineLength)
        {
            return (text, string.Empty);
        }

        var line1 = TakeStatusLine(text, maxLineLength);
        var remaining = text[line1.Length..].TrimStart(' ', '，', '、', '/', '|');
        if (remaining.Length <= maxLineLength)
        {
            return (line1, remaining);
        }

        var line2 = TakeStatusLine(remaining, Math.Max(1, maxLineLength - 1)).TrimEnd(' ', '，', '、', '/', '|') + "…";
        return (line1, line2);
    }

    private static string TakeStatusLine(string text, int maxLineLength)
    {
        if (text.Length <= maxLineLength)
        {
            return text;
        }

        var boundary = text.LastIndexOfAny([' ', '，', '、', '/', '|'], Math.Min(text.Length - 1, maxLineLength));
        if (boundary >= Math.Max(4, maxLineLength / 2))
        {
            return text[..boundary].TrimEnd(' ', '，', '、', '/', '|');
        }

        return text[..maxLineLength].TrimEnd(' ', '，', '、', '/', '|');
    }

    private void NotifyAiRecommendationBindings()
    {
        OnPropertyChanged(nameof(AiRecommendationPreview));
        OnPropertyChanged(nameof(AiRecommendationStatusText));
        OnPropertyChanged(nameof(AiRecommendationStatusExpandedLine1));
        OnPropertyChanged(nameof(AiRecommendationStatusExpandedLine2));
        OnPropertyChanged(nameof(AiRecommendationStatusCollapsedLine1));
        OnPropertyChanged(nameof(AiRecommendationStatusCollapsedLine2));
    }

    private static Task<T> RunQueryOffUiThreadAsync<T>(
        Func<CancellationToken, Task<T>> queryAsync,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => queryAsync(cancellationToken), cancellationToken);
    }

    private void NavigateToMovieDiscoveryAiTab()
    {
        _movieDiscoveryViewModel.OpenAiRecommendationsOnNextActivation();
        _navigationStateService.RequestNavigation(NavigationPageKey.MovieDiscovery);
    }

    private void OpenMovie(object? parameter)
    {
        switch (parameter)
        {
            case HomeMovieItem movie:
                if (movie.TvSeasonId.HasValue)
                {
                    _navigationStateService.RequestTvSeasonDetail(movie.TvSeasonId.Value, movie.EpisodeId);
                }
                else
                {
                    _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movie.MovieId);
                }
                break;
            case CollectionMovieItem collectionMovie:
                OpenCollectionMovie(collectionMovie);
                break;
            case AiRecommendationItem recommendation:
                if (recommendation.MovieId > 0)
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
            if (movie.EpisodeId.HasValue)
            {
                await _playerWindowService.OpenEpisodeAsync(movie.EpisodeId.Value, movie.MediaFileId);
            }
            else
            {
                await _playerWindowService.OpenAsync(movie.MovieId, movie.MediaFileId);
            }

            keepDisabledUntilWindowClosed = _playerWindowService.IsPlayerOpen
                                             && IsSamePlayback(
                                                 movie,
                                                 _playerWindowService.ActiveMovieId,
                                                 _playerWindowService.ActiveEpisodeId,
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
                _playerWindowService.ActiveEpisodeId,
                _playerWindowService.ActiveMediaFileId);
        }
    }

    private static bool IsSamePlayback(HomeMovieItem movie, int? activeMovieId, int? activeEpisodeId, int? activeMediaFileId)
    {
        if (movie.EpisodeId.HasValue)
        {
            return activeEpisodeId == movie.EpisodeId.Value
                   && (!activeMediaFileId.HasValue || movie.MediaFileId == activeMediaFileId);
        }

        return activeMovieId == movie.MovieId
               && (!activeMediaFileId.HasValue || movie.MediaFileId == activeMediaFileId);
    }

    private void OpenCollectionMovie(CollectionMovieItem movie)
    {
        if (movie.MovieId.HasValue)
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
                ReleaseDate = movie.ReleaseDate,
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
                AvailabilityText = "暂无播放源",
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

public sealed record HomeStatusMetricItem(
    string Title,
    string ValueText,
    string UnitText,
    string TrendText,
    string TrendArrowText,
    string IconGlyph);
