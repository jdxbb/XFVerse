using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class MovieDiscoveryViewModel : PageViewModelBase
{
    private const int SearchTabIndex = 0;
    private const int RankingTabIndex = 1;
    private const int AiRecommendationTabIndex = 2;
    private const string SearchTypeMovie = "按影片搜";
    private const string SearchTypePerson = "按人物搜";
    private const string RankingTypePopular = "热门榜";
    private const string RankingTypeTopRated = "高分榜";
    private const string RankingTypeTrending = "趋势榜";
    private const string TrendingTimeDay = "今日趋势";
    private const string TrendingTimeWeek = "本周趋势";
    private const string TrendingWindowDay = "day";
    private const string TrendingWindowWeek = "week";
    private const string FilterAll = "全部";
    private const string FilterOther = "其它";
    private const string SortRelevance = "相关度";
    private const string SortRating = "评分";
    private const string SortPopularity = "热度";
    private const string SortReleaseDate = "上映时间";
    private const string SortTitle = "片名";
    private const string DirectionDescending = "降序";
    private const string DirectionAscending = "升序";
    private const string DecadeAll = "全部";
    private const string DecadeEarlier = "更早";
    private const int OmdbResolveConcurrency = 2;
    private const int SearchTmdbPageSize = 20;
    private const int SearchDisplayPageSize = 30;
    private const int SearchFilteredInitialSourcePages = 10;
    private const int SearchFilteredSourcePagesPerDisplayPage = 5;
    private const int SearchFilteredMaxSourcePages = 30;
    private const int RankingTmdbPageSize = 20;
    private const int MaxRankingMovies = 200;
    private const int RankingFirstDisplayPageSize = 21;
    private const int RankingRegularDisplayPageSize = 20;

    private static readonly IReadOnlyDictionary<string, string[]> RegionCountryCodes = new Dictionary<string, string[]>
    {
        ["中国大陆"] = ["CN"],
        ["香港"] = ["HK"],
        ["台湾"] = ["TW"],
        ["美国"] = ["US"],
        ["日本"] = ["JP"],
        ["韩国"] = ["KR"],
        ["英国"] = ["GB"],
        ["法国"] = ["FR"],
        ["德国"] = ["DE"],
        ["印度"] = ["IN"],
        ["泰国"] = ["TH"]
    };

    private static readonly IReadOnlyDictionary<string, string[]> RegionLanguageFallbacks = new Dictionary<string, string[]>
    {
        ["中国大陆"] = ["zh"],
        ["香港"] = ["zh", "cn"],
        ["台湾"] = ["zh", "cn"],
        ["美国"] = ["en"],
        ["英国"] = ["en"],
        ["日本"] = ["ja"],
        ["韩国"] = ["ko"],
        ["法国"] = ["fr"],
        ["德国"] = ["de"],
        ["印度"] = ["hi", "ta", "te", "ml"],
        ["泰国"] = ["th"]
    };

    private static readonly IReadOnlyDictionary<string, string> LanguageCodes = new Dictionary<string, string>
    {
        ["中文"] = "zh",
        ["英语"] = "en",
        ["日语"] = "ja",
        ["韩语"] = "ko",
        ["法语"] = "fr",
        ["德语"] = "de",
        ["西班牙语"] = "es",
        ["印地语"] = "hi",
        ["泰语"] = "th"
    };

    private readonly RecommendationsViewModel _aiRecommendationViewModel;
    private readonly ITmdbService _tmdbService;
    private readonly IOmdbService _omdbService;
    private readonly IDiscoveryMovieStatusResolver _statusResolver;
    private readonly IUserCollectionService _userCollectionService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly List<DiscoveryMovieCardViewModel> _searchResultPool = [];
    private readonly HashSet<int> _searchTmdbIds = [];
    private readonly Dictionary<int, TmdbMovieDiscoveryPage> _searchSourcePageCache = [];
    private readonly List<DiscoveryMovieCardViewModel> _rankingMovies = [];
    private readonly HashSet<int> _rankingTmdbIds = [];
    private readonly Dictionary<int, TmdbMovieDiscoveryPage> _rankingSourcePageCache = [];

    private CancellationTokenSource? _searchCancellationTokenSource;
    private CancellationTokenSource? _rankingCancellationTokenSource;
    private int _selectedTabIndex = SearchTabIndex;
    private bool _hasActivatedRankings;
    private bool _hasActivatedAiRecommendations;
    private string _searchText = string.Empty;
    private string _selectedSearchType = SearchTypeMovie;
    private string _selectedGenreFilter = FilterAll;
    private string _selectedRegionFilter = FilterAll;
    private string _selectedWatchStatusFilter = FilterAll;
    private string _selectedSortOption = SortRelevance;
    private string _selectedSortDirection = DirectionDescending;
    private string _selectedDecadeFilter = DecadeAll;
    private string _selectedLanguageFilter = FilterAll;
    private string _searchStatusMessage = "请输入关键词搜索 TMDB 影片。";
    private string _searchSummaryText = string.Empty;
    private bool _isSearchLoading;
    private bool _suppressFilterApply;
    private int _searchPageIndex = 1;
    private int _searchTotalPages;
    private int _searchTotalResults;
    private int _searchSourceNextPage = 1;
    private int _searchSourceTotalPages;
    private int _searchRequestVersion;
    private int _nextSearchOrder;
    private bool _searchSourceExhausted;
    private bool _canGoToNextSearchPage;
    private string _selectedRankingType = RankingTypePopular;
    private string _selectedTrendingTime = TrendingTimeDay;
    private string _rankingStatusMessage = "打开榜单后将加载 TMDB 热门榜。";
    private string _rankingSummaryText = string.Empty;
    private bool _isRankingLoading;
    private int _rankingPageIndex = 1;
    private int _rankingTotalDisplayPages = 1;
    private int _rankingTotalResults;
    private int _rankingSourceNextPage = 1;
    private int _rankingSourceTotalPages;
    private int _rankingRequestVersion;
    private int _nextRankingRank;
    private bool _rankingSourceExhausted;
    private bool _canGoToNextRankingPage;
    private DiscoveryMovieCardViewModel? _topRankingMovie;

    public MovieDiscoveryViewModel(
        RecommendationsViewModel aiRecommendationViewModel,
        ITmdbService tmdbService,
        IOmdbService omdbService,
        IDiscoveryMovieStatusResolver statusResolver,
        IUserCollectionService userCollectionService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService)
        : base("影片发现", "搜索、榜单和 AI 推荐集中在这里。")
    {
        _aiRecommendationViewModel = aiRecommendationViewModel;
        _tmdbService = tmdbService;
        _omdbService = omdbService;
        _statusResolver = statusResolver;
        _userCollectionService = userCollectionService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;

        SearchTypeOptions = [SearchTypeMovie, SearchTypePerson];
        GenreFilterOptions = TmdbGenreMapper.GenreLabels;
        RegionFilterOptions = [FilterAll, "中国大陆", "香港", "台湾", "美国", "日本", "韩国", "英国", "法国", "德国", "印度", "泰国", FilterOther];
        WatchStatusFilterOptions = [FilterAll, "已入库", "未入库", "已看", "未看", "想看", "喜爱", "不想看"];
        SortOptions = [SortRelevance, SortRating, SortPopularity, SortReleaseDate, SortTitle];
        SortDirectionOptions = [DirectionDescending, DirectionAscending];
        DecadeFilterOptions = [DecadeAll, "2020s", "2010s", "2000s", "1990s", "1980s", "1970s", "1960s", DecadeEarlier];
        LanguageFilterOptions = [FilterAll, "中文", "英语", "日语", "韩语", "法语", "德语", "西班牙语", "印地语", "泰语", FilterOther];
        RankingTypeOptions = [RankingTypePopular, RankingTypeTopRated, RankingTypeTrending];
        TrendingTimeOptions = [TrendingTimeDay, TrendingTimeWeek];

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsSearchLoading);
        GoPreviousSearchPageCommand = new AsyncRelayCommand(GoPreviousSearchPageAsync, () => CanGoPreviousSearchPage);
        GoNextSearchPageCommand = new AsyncRelayCommand(GoNextSearchPageAsync, () => CanGoNextSearchPage);
        ClearSearchFiltersCommand = new RelayCommand(ClearSearchFilters);
        ShowLayoutSwitchPlaceholderCommand = new RelayCommand(() => SearchStatusMessage = "切布局将在后续视觉阶段接入。");
        OpenSearchMovieCommand = new RelayCommand(OpenSearchMovie);
        ToggleSearchWantToWatchCommand = new AsyncRelayCommand(ToggleSearchWantToWatchAsync);
        SelectRankingTypeCommand = new RelayCommand(SelectRankingType);
        SelectTrendingTimeCommand = new RelayCommand(SelectTrendingTime, _ => IsTrendingRanking);
        GoPreviousRankingPageCommand = new AsyncRelayCommand(GoPreviousRankingPageAsync, () => CanGoPreviousRankingPage);
        GoNextRankingPageCommand = new AsyncRelayCommand(GoNextRankingPageAsync, () => CanGoNextRankingPage);
        OpenRankingMovieCommand = new RelayCommand(OpenRankingMovie);
        ToggleRankingWantToWatchCommand = new AsyncRelayCommand(ToggleRankingWantToWatchAsync);
    }

    public RecommendationsViewModel AiRecommendationViewModel => _aiRecommendationViewModel;

    public ObservableCollection<DiscoveryMovieCardViewModel> SearchMovies { get; } = [];

    public ObservableCollection<DiscoveryRankingRowViewModel> RankingRows { get; } = [];

    public IReadOnlyList<string> SearchTypeOptions { get; }

    public IReadOnlyList<string> GenreFilterOptions { get; }

    public IReadOnlyList<string> RegionFilterOptions { get; }

    public IReadOnlyList<string> WatchStatusFilterOptions { get; }

    public IReadOnlyList<string> SortOptions { get; }

    public IReadOnlyList<string> SortDirectionOptions { get; }

    public IReadOnlyList<string> DecadeFilterOptions { get; }

    public IReadOnlyList<string> LanguageFilterOptions { get; }

    public IReadOnlyList<string> RankingTypeOptions { get; }

    public IReadOnlyList<string> TrendingTimeOptions { get; }

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand GoPreviousSearchPageCommand { get; }

    public AsyncRelayCommand GoNextSearchPageCommand { get; }

    public RelayCommand ClearSearchFiltersCommand { get; }

    public RelayCommand ShowLayoutSwitchPlaceholderCommand { get; }

    public RelayCommand OpenSearchMovieCommand { get; }

    public AsyncRelayCommand ToggleSearchWantToWatchCommand { get; }

    public RelayCommand SelectRankingTypeCommand { get; }

    public RelayCommand SelectTrendingTimeCommand { get; }

    public AsyncRelayCommand GoPreviousRankingPageCommand { get; }

    public AsyncRelayCommand GoNextRankingPageCommand { get; }

    public RelayCommand OpenRankingMovieCommand { get; }

    public AsyncRelayCommand ToggleRankingWantToWatchCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (!SetProperty(ref _selectedTabIndex, value))
            {
                return;
            }

            if (value == RankingTabIndex)
            {
                _ = EnsureRankingsActivatedAsync();
            }

            if (value == AiRecommendationTabIndex)
            {
                _ = EnsureAiRecommendationsActivatedAsync();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string SelectedSearchType
    {
        get => _selectedSearchType;
        set
        {
            if (SetProperty(ref _selectedSearchType, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedGenreFilter
    {
        get => _selectedGenreFilter;
        set
        {
            if (SetProperty(ref _selectedGenreFilter, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedRegionFilter
    {
        get => _selectedRegionFilter;
        set
        {
            if (SetProperty(ref _selectedRegionFilter, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedWatchStatusFilter
    {
        get => _selectedWatchStatusFilter;
        set
        {
            if (SetProperty(ref _selectedWatchStatusFilter, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedSortDirection
    {
        get => _selectedSortDirection;
        set
        {
            if (SetProperty(ref _selectedSortDirection, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedDecadeFilter
    {
        get => _selectedDecadeFilter;
        set
        {
            if (SetProperty(ref _selectedDecadeFilter, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedLanguageFilter
    {
        get => _selectedLanguageFilter;
        set
        {
            if (SetProperty(ref _selectedLanguageFilter, value))
            {
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SearchStatusMessage
    {
        get => _searchStatusMessage;
        private set
        {
            if (SetProperty(ref _searchStatusMessage, value))
            {
                OnPropertyChanged(nameof(SearchStatusOverlayText));
            }
        }
    }

    public string SearchSummaryText
    {
        get => _searchSummaryText;
        private set => SetProperty(ref _searchSummaryText, value);
    }

    public bool IsSearchLoading
    {
        get => _isSearchLoading;
        private set
        {
            if (SetProperty(ref _isSearchLoading, value))
            {
                RefreshSearchVisibility();
                RefreshSearchCommandState();
            }
        }
    }

    public int SearchPageIndex
    {
        get => _searchPageIndex;
        private set
        {
            if (SetProperty(ref _searchPageIndex, value))
            {
                OnPropertyChanged(nameof(SearchPageStatusText));
                OnPropertyChanged(nameof(CanGoPreviousSearchPage));
            }
        }
    }

    public int SearchTotalPages
    {
        get => _searchTotalPages;
        private set
        {
            if (SetProperty(ref _searchTotalPages, value))
            {
                OnPropertyChanged(nameof(SearchPageStatusText));
            }
        }
    }

    public bool HasSearchMovies => SearchMovies.Count > 0;

    public bool ShowSearchEmptyState => !HasSearchMovies && !IsSearchLoading;

    public bool ShowSearchStatusOverlay => IsSearchLoading || ShowSearchEmptyState;

    public string SearchStatusOverlayText => SearchStatusMessage;

    public bool CanGoPreviousSearchPage => !IsSearchLoading && SearchPageIndex > 1;

    public bool CanGoNextSearchPage => !IsSearchLoading && _canGoToNextSearchPage;

    public string SearchPageStatusText => SearchTotalPages <= 0
        ? "第 0 / 0 页"
        : $"第 {SearchPageIndex} / {SearchTotalPages} 页";

    public string SelectedRankingType
    {
        get => _selectedRankingType;
        private set
        {
            if (SetProperty(ref _selectedRankingType, value))
            {
                OnPropertyChanged(nameof(RankingTitle));
                OnPropertyChanged(nameof(RankingTimeText));
                OnPropertyChanged(nameof(IsTrendingRanking));
                OnPropertyChanged(nameof(IsRankingTimeSelectable));
                SelectTrendingTimeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedTrendingTime
    {
        get => _selectedTrendingTime;
        private set
        {
            if (SetProperty(ref _selectedTrendingTime, value))
            {
                OnPropertyChanged(nameof(RankingTimeText));
            }
        }
    }

    public string RankingTitle => SelectedRankingType;

    public string RankingTimeText => SelectedRankingType switch
    {
        RankingTypePopular => "当前热门",
        RankingTypeTopRated => "历史高分",
        _ => SelectedTrendingTime
    };

    public bool IsTrendingRanking => string.Equals(SelectedRankingType, RankingTypeTrending, StringComparison.Ordinal);

    public bool IsRankingTimeSelectable => IsTrendingRanking && !IsRankingLoading;

    public DiscoveryMovieCardViewModel? TopRankingMovie
    {
        get => _topRankingMovie;
        private set
        {
            if (SetProperty(ref _topRankingMovie, value))
            {
                OnPropertyChanged(nameof(ShowTopRankingMovie));
            }
        }
    }

    public bool ShowTopRankingMovie => TopRankingMovie is not null;

    public string RankingStatusMessage
    {
        get => _rankingStatusMessage;
        private set
        {
            if (SetProperty(ref _rankingStatusMessage, value))
            {
                OnPropertyChanged(nameof(RankingStatusOverlayText));
            }
        }
    }

    public string RankingSummaryText
    {
        get => _rankingSummaryText;
        private set => SetProperty(ref _rankingSummaryText, value);
    }

    public bool IsRankingLoading
    {
        get => _isRankingLoading;
        private set
        {
            if (SetProperty(ref _isRankingLoading, value))
            {
                RefreshRankingVisibility();
                RefreshRankingCommandState();
            }
        }
    }

    public int RankingPageIndex
    {
        get => _rankingPageIndex;
        private set
        {
            if (SetProperty(ref _rankingPageIndex, value))
            {
                OnPropertyChanged(nameof(RankingPageStatusText));
                OnPropertyChanged(nameof(CanGoPreviousRankingPage));
            }
        }
    }

    public int RankingTotalDisplayPages
    {
        get => _rankingTotalDisplayPages;
        private set
        {
            if (SetProperty(ref _rankingTotalDisplayPages, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(RankingPageStatusText));
            }
        }
    }

    public bool HasRankingMovies => TopRankingMovie is not null || RankingRows.Count > 0;

    public bool ShowRankingEmptyState => !HasRankingMovies && !IsRankingLoading;

    public bool ShowRankingStatusOverlay => IsRankingLoading || ShowRankingEmptyState;

    public string RankingStatusOverlayText => RankingStatusMessage;

    public bool CanGoPreviousRankingPage => !IsRankingLoading && RankingPageIndex > 1;

    public bool CanGoNextRankingPage => !IsRankingLoading && _canGoToNextRankingPage;

    public string RankingPageStatusText => $"第 {RankingPageIndex} / {RankingTotalDisplayPages} 页";

    public override Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        SelectedTabIndex = SearchTabIndex;
        return Task.CompletedTask;
    }

    public override void Deactivate()
    {
        _searchCancellationTokenSource?.Cancel();
        _rankingCancellationTokenSource?.Cancel();
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ResetSearchBuffers();
            SearchMovies.Clear();
            SearchPageIndex = 1;
            SearchTotalPages = 0;
            _canGoToNextSearchPage = false;
            SearchStatusMessage = "请输入关键词。";
            SearchSummaryText = string.Empty;
            RefreshSearchVisibility();
            RefreshSearchCommandState();
            return;
        }

        await ResetAndLoadSearchDisplayPageAsync(1);
    }

    private async Task GoPreviousSearchPageAsync()
    {
        if (!CanGoPreviousSearchPage)
        {
            return;
        }

        await LoadSearchDisplayPageAsync(SearchPageIndex - 1);
    }

    private async Task GoNextSearchPageAsync()
    {
        if (!CanGoNextSearchPage)
        {
            return;
        }

        await LoadSearchDisplayPageAsync(SearchPageIndex + 1);
    }

    private void ResetSearchFromFilterChange()
    {
        if (_suppressFilterApply)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            RebuildSearchDisplay();
            return;
        }

        _ = ResetAndLoadSearchDisplayPageAsync(1);
    }

    private async Task ResetAndLoadSearchDisplayPageAsync(int displayPage)
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();
        ResetSearchBuffers();
        SearchPageIndex = 1;
        SearchTotalPages = 0;
        SearchMovies.Clear();
        RefreshSearchVisibility();

        await LoadSearchDisplayPageCoreAsync(displayPage, _searchCancellationTokenSource.Token);
    }

    private async Task LoadSearchDisplayPageAsync(int displayPage)
    {
        if (displayPage < 1 || string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        _searchCancellationTokenSource ??= new CancellationTokenSource();
        await LoadSearchDisplayPageCoreAsync(displayPage, _searchCancellationTokenSource.Token);
    }

    private async Task LoadSearchDisplayPageCoreAsync(
        int displayPage,
        CancellationToken cancellationToken)
    {
        var requestVersion = ++_searchRequestVersion;
        IsSearchLoading = true;
        SearchStatusMessage = displayPage <= 1 ? "正在搜索..." : $"正在加载第 {displayPage} 页...";

        try
        {
            await EnsureSearchPoolForDisplayPageAsync(displayPage, requestVersion, cancellationToken);
            if (requestVersion != _searchRequestVersion)
            {
                return;
            }

            var filteredCount = BuildFilteredSearchMovies().Count;
            if (displayPage > 1
                && filteredCount <= (displayPage - 1) * SearchDisplayPageSize
                && !CanFetchNextSearchSourcePage(GetSearchSourcePageLimit(displayPage + 1)))
            {
                displayPage = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)SearchDisplayPageSize));
            }

            SearchPageIndex = displayPage;
            var visibleItems = RebuildSearchDisplay();
            if (SearchMovies.Count == 0)
            {
                SearchStatusMessage = _searchResultPool.Count == 0
                    ? "未找到相关影片。"
                    : "当前筛选条件下无结果。";
            }
            else
            {
                SearchStatusMessage = $"{SelectedSearchType}结果已加载。";
                _ = EnrichOmdbRatingsAsync(visibleItems, requestVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SearchStatusMessage = $"搜索失败：{DescribeException(exception)}";
            RebuildSearchDisplay();
        }
        finally
        {
            IsSearchLoading = false;
            RefreshSearchVisibility();
            RefreshSearchCommandState();
        }
    }

    private async Task EnsureSearchPoolForDisplayPageAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var sourcePageLimit = GetSearchSourcePageLimit(displayPage);
        while (requestVersion == _searchRequestVersion
               && !HasEnoughSearchResultsForDisplayPage(displayPage)
               && CanFetchNextSearchSourcePage(sourcePageLimit))
        {
            await FetchNextSearchSourcePageAsync(requestVersion, cancellationToken);
        }
    }

    private bool HasEnoughSearchResultsForDisplayPage(int displayPage)
    {
        var required = displayPage * SearchDisplayPageSize;
        if (!HasExpandedSearchCriteria())
        {
            return _searchResultPool.Count >= required || _searchSourceExhausted;
        }

        return BuildFilteredSearchMovies().Count >= required || _searchSourceExhausted;
    }

    private bool CanFetchNextSearchSourcePage(int sourcePageLimit)
    {
        if (_searchSourceExhausted)
        {
            return false;
        }

        if (_searchSourceTotalPages > 0 && _searchSourceNextPage > _searchSourceTotalPages)
        {
            return false;
        }

        return _searchSourceNextPage <= Math.Max(1, sourcePageLimit);
    }

    private int GetSearchSourcePageLimit(int displayPage)
    {
        if (!HasExpandedSearchCriteria())
        {
            return (int)Math.Ceiling(displayPage * SearchDisplayPageSize / (double)SearchTmdbPageSize);
        }

        var pageDrivenLimit = Math.Max(
            SearchFilteredInitialSourcePages,
            displayPage * SearchFilteredSourcePagesPerDisplayPage);
        return Math.Min(SearchFilteredMaxSourcePages, pageDrivenLimit);
    }

    private async Task FetchNextSearchSourcePageAsync(
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var page = _searchSourceNextPage;
        if (!_searchSourcePageCache.TryGetValue(page, out var response))
        {
            response = string.Equals(SelectedSearchType, SearchTypePerson, StringComparison.Ordinal)
                ? await _tmdbService.SearchDiscoveryMoviesByPersonAsync(SearchText, page, cancellationToken)
                : await _tmdbService.SearchDiscoveryMoviesAsync(
                    SearchText,
                    page,
                    region: GetSearchRegionParameter(),
                    cancellationToken: cancellationToken);
            _searchSourcePageCache[page] = response;
        }

        if (requestVersion != _searchRequestVersion)
        {
            return;
        }

        _searchSourceNextPage = page + 1;
        _searchSourceTotalPages = response.TotalPages;
        _searchTotalResults = response.TotalResults;
        if (response.TotalPages <= 0 || page >= response.TotalPages || response.Results.Count == 0)
        {
            _searchSourceExhausted = true;
        }

        var pageItems = response.Results
            .Where(item => item.TmdbId > 0 && _searchTmdbIds.Add(item.TmdbId))
            .Select(item => new DiscoveryMovieCardViewModel(item, ++_nextSearchOrder))
            .ToList();

        if (pageItems.Count == 0)
        {
            return;
        }

        var statuses = await _statusResolver.ResolveAsync(pageItems.Select(item => item.TmdbId), cancellationToken);
        foreach (var item in pageItems)
        {
            if (statuses.TryGetValue(item.TmdbId, out var status))
            {
                item.ApplyStatus(status);
            }
        }

        _searchResultPool.AddRange(pageItems);
    }

    private async Task EnrichOmdbRatingsAsync(
        IReadOnlyList<DiscoveryMovieCardViewModel> items,
        int requestVersion)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var limiter = new SemaphoreSlim(OmdbResolveConcurrency, OmdbResolveConcurrency);
        var tasks = items.Select(
            async item =>
            {
                await limiter.WaitAsync();
                try
                {
                    if (requestVersion != _searchRequestVersion || item.OmdbRating is not null)
                    {
                        return;
                    }

                    var imdbId = item.ImdbId;
                    if (string.IsNullOrWhiteSpace(imdbId))
                    {
                        MetadataSearchCandidate? details;
                        try
                        {
                            details = await _tmdbService.GetMovieDetailsAsync(item.TmdbId, CancellationToken.None);
                        }
                        catch
                        {
                            return;
                        }

                        if (details is null || requestVersion != _searchRequestVersion)
                        {
                            return;
                        }

                        await DispatchAsync(
                            () =>
                            {
                                item.SetDetailsSnapshot(details);
                                RebuildSearchDisplay();
                            });
                        imdbId = details.ImdbId;
                    }

                    if (string.IsNullOrWhiteSpace(imdbId))
                    {
                        return;
                    }

                    MovieRatingItem? omdbRating;
                    try
                    {
                        omdbRating = await _omdbService.GetRatingAsync(imdbId, CancellationToken.None);
                    }
                    catch
                    {
                        return;
                    }

                    if (omdbRating is null || requestVersion != _searchRequestVersion)
                    {
                        return;
                    }

                    await DispatchAsync(
                        () =>
                        {
                            item.SetOmdbRating(omdbRating);
                            RebuildSearchDisplay();
                        });
                }
                finally
                {
                    limiter.Release();
                }
            });

        await Task.WhenAll(tasks);
    }

    private IReadOnlyList<DiscoveryMovieCardViewModel> RebuildSearchDisplay()
    {
        var filtered = BuildFilteredSearchMovies();
        var pageItems = filtered
            .Skip((SearchPageIndex - 1) * SearchDisplayPageSize)
            .Take(SearchDisplayPageSize)
            .ToList();

        SearchMovies.Clear();
        foreach (var item in pageItems)
        {
            SearchMovies.Add(item);
        }

        UpdateSearchPagination(filtered.Count);
        SearchSummaryText = BuildSearchSummaryText(filtered.Count);
        RefreshSearchVisibility();
        RefreshSearchCommandState();
        return pageItems;
    }

    private List<DiscoveryMovieCardViewModel> BuildFilteredSearchMovies()
    {
        var query = _searchResultPool.AsEnumerable();

        if (!string.Equals(SelectedGenreFilter, FilterAll, StringComparison.Ordinal))
        {
            query = query.Where(item => SplitTags(item.DisplayTags).Contains(SelectedGenreFilter, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedRegionFilter, FilterAll, StringComparison.Ordinal))
        {
            query = query.Where(item => MatchesRegion(item, SelectedRegionFilter));
        }

        if (!string.Equals(SelectedLanguageFilter, FilterAll, StringComparison.Ordinal))
        {
            query = query.Where(item => MatchesLanguage(item, SelectedLanguageFilter));
        }

        if (!string.Equals(SelectedDecadeFilter, DecadeAll, StringComparison.Ordinal))
        {
            query = query.Where(item => MatchesDecade(item, SelectedDecadeFilter));
        }

        query = SelectedWatchStatusFilter switch
        {
            "已入库" => query.Where(item => item.IsInLibrary),
            "未入库" => query.Where(item => !item.IsInLibrary),
            "已看" => query.Where(item => item.IsWatched),
            "未看" => query.Where(item => !item.IsWatched),
            "想看" => query.Where(item => item.IsWantToWatch),
            "喜爱" => query.Where(item => item.IsFavorite),
            "不想看" => query.Where(item => item.IsNotInterested),
            _ => query
        };

        return ApplySearchSorting(query).ToList();
    }

    private IEnumerable<DiscoveryMovieCardViewModel> ApplySearchSorting(IEnumerable<DiscoveryMovieCardViewModel> query)
    {
        var descending = string.Equals(SelectedSortDirection, DirectionDescending, StringComparison.Ordinal);
        return SelectedSortOption switch
        {
            SortRating => descending
                ? query.OrderByDescending(item => item.RatingValue ?? -1d).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.RatingValue ?? -1d).ThenBy(item => item.SearchOrder),
            SortPopularity => descending
                ? query.OrderByDescending(item => item.Popularity ?? -1d).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.Popularity ?? -1d).ThenBy(item => item.SearchOrder),
            SortReleaseDate => descending
                ? query.OrderByDescending(item => item.ReleaseYear ?? 0).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.ReleaseYear ?? 0).ThenBy(item => item.SearchOrder),
            SortTitle => descending
                ? query.OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.SearchOrder),
            _ => descending
                ? query.OrderBy(item => item.SearchOrder)
                : query.OrderByDescending(item => item.SearchOrder)
        };
    }

    private void UpdateSearchPagination(int filteredCount)
    {
        var currentPageHasFullResult = filteredCount > SearchPageIndex * SearchDisplayPageSize;
        var canFetchMore = CanFetchNextSearchSourcePage(GetSearchSourcePageLimit(SearchPageIndex + 1));
        _canGoToNextSearchPage = currentPageHasFullResult || canFetchMore;

        var loadedPages = filteredCount == 0
            ? 0
            : (int)Math.Ceiling(filteredCount / (double)SearchDisplayPageSize);
        if (!HasExpandedSearchCriteria() && _searchTotalResults > 0)
        {
            SearchTotalPages = Math.Max(1, (int)Math.Ceiling(_searchTotalResults / (double)SearchDisplayPageSize));
        }
        else if (_canGoToNextSearchPage)
        {
            SearchTotalPages = Math.Max(SearchPageIndex + 1, loadedPages);
        }
        else
        {
            SearchTotalPages = loadedPages;
        }

        OnPropertyChanged(nameof(CanGoNextSearchPage));
        OnPropertyChanged(nameof(SearchPageStatusText));
    }

    private string BuildSearchSummaryText(int filteredCount)
    {
        if (_searchResultPool.Count == 0)
        {
            return string.Empty;
        }

        var scopeText = HasExpandedSearchCriteria()
            ? $"已扫描 {_searchResultPool.Count} / {FormatTotalResults(_searchTotalResults)}，当前筛选匹配 {filteredCount} 部"
            : $"已缓存 {_searchResultPool.Count} / {FormatTotalResults(_searchTotalResults)}";
        return $"{scopeText}，每页最多 {SearchDisplayPageSize} 部";
    }

    private bool HasExpandedSearchCriteria()
    {
        return !string.Equals(SelectedGenreFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedRegionFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedLanguageFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedDecadeFilter, DecadeAll, StringComparison.Ordinal)
               || !string.Equals(SelectedWatchStatusFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedSortOption, SortRelevance, StringComparison.Ordinal)
               || !string.Equals(SelectedSortDirection, DirectionDescending, StringComparison.Ordinal);
    }

    private void ClearSearchFilters()
    {
        _suppressFilterApply = true;
        SelectedGenreFilter = FilterAll;
        SelectedRegionFilter = FilterAll;
        SelectedWatchStatusFilter = FilterAll;
        SelectedSortOption = SortRelevance;
        SelectedSortDirection = DirectionDescending;
        SelectedDecadeFilter = DecadeAll;
        SelectedLanguageFilter = FilterAll;
        _suppressFilterApply = false;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            RebuildSearchDisplay();
            SearchStatusMessage = "筛选已清除。请输入关键词搜索 TMDB 影片。";
            return;
        }

        SearchStatusMessage = "筛选已清除。";
        _ = ResetAndLoadSearchDisplayPageAsync(1);
    }

    private void OpenSearchMovie(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        OpenDiscoveryMovie(item, message => SearchStatusMessage = message);
    }

    private async Task ToggleSearchWantToWatchAsync(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        await ToggleDiscoveryWantToWatchAsync(
            item,
            message => SearchStatusMessage = message,
            () => RebuildSearchDisplay());
    }

    private void OpenRankingMovie(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        OpenDiscoveryMovie(item, message => RankingStatusMessage = message);
    }

    private async Task ToggleRankingWantToWatchAsync(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        await ToggleDiscoveryWantToWatchAsync(
            item,
            message => RankingStatusMessage = message,
            () => RebuildRankingRows());
    }

    private void OpenDiscoveryMovie(
        DiscoveryMovieCardViewModel item,
        Action<string> setStatusMessage)
    {
        if (item.IsInLibrary && item.MovieId is > 0)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, item.MovieId.Value);
            return;
        }

        if (item.TmdbId <= 0)
        {
            setStatusMessage("缺少 TMDB ID，暂时无法打开未入库详情。");
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(DiscoveryExternalMovieAdapter.ToRecommendation(item));
    }

    private async Task ToggleDiscoveryWantToWatchAsync(
        DiscoveryMovieCardViewModel item,
        Action<string> setStatusMessage,
        Action refreshView)
    {
        if (item.IsWatched)
        {
            setStatusMessage("已看影片不需要加入想看。");
            return;
        }

        var previousState = item.IsWantToWatch;
        try
        {
            var recommendation = DiscoveryExternalMovieAdapter.ToRecommendation(item);
            if (previousState)
            {
                await _userCollectionService.RemoveWantToWatchAsync(recommendation, changeSource: "Discovery");
                item.ApplyWantToWatchState(false);
                setStatusMessage($"已取消想看：{item.Title}");
            }
            else
            {
                await _userCollectionService.AddWantToWatchAsync(recommendation, changeSource: "Discovery");
                setStatusMessage($"已加入想看：{item.Title}");
            }

            await RefreshMovieStatusAsync(item);
            _dataRefreshService.NotifyCollectionChanged();
            if (item.TmdbId > 0)
            {
                _dataRefreshService.NotifyRecommendationChanged();
            }

            refreshView();
        }
        catch (Exception exception)
        {
            item.ApplyWantToWatchState(previousState);
            setStatusMessage($"想看状态更新失败：{DescribeException(exception)}");
        }
    }

    private async Task RefreshMovieStatusAsync(DiscoveryMovieCardViewModel item)
    {
        if (item.TmdbId <= 0)
        {
            return;
        }

        var statuses = await _statusResolver.ResolveAsync([item.TmdbId], CancellationToken.None);
        if (statuses.TryGetValue(item.TmdbId, out var status))
        {
            item.ApplyStatus(status);
        }
        else
        {
            item.ApplyWantToWatchState(false);
        }
    }

    private async Task EnsureRankingsActivatedAsync()
    {
        if (_hasActivatedRankings)
        {
            return;
        }

        _hasActivatedRankings = true;
        try
        {
            await ResetAndLoadRankingsAsync();
        }
        catch
        {
            _hasActivatedRankings = false;
        }
    }

    private void SelectRankingType(object? parameter)
    {
        if (parameter is not string rankingType
            || string.IsNullOrWhiteSpace(rankingType)
            || string.Equals(rankingType, SelectedRankingType, StringComparison.Ordinal))
        {
            return;
        }

        SelectedRankingType = rankingType;
        _ = ResetAndLoadRankingsAsync();
    }

    private void SelectTrendingTime(object? parameter)
    {
        if (!IsTrendingRanking
            || parameter is not string trendingTime
            || string.IsNullOrWhiteSpace(trendingTime)
            || string.Equals(trendingTime, SelectedTrendingTime, StringComparison.Ordinal))
        {
            return;
        }

        SelectedTrendingTime = trendingTime;
        _ = ResetAndLoadRankingsAsync();
    }

    private async Task ResetAndLoadRankingsAsync()
    {
        _rankingCancellationTokenSource?.Cancel();
        _rankingCancellationTokenSource = new CancellationTokenSource();
        ResetRankingBuffers();
        RankingPageIndex = 1;
        RankingTotalDisplayPages = 1;
        TopRankingMovie = null;
        RankingRows.Clear();
        RankingSummaryText = string.Empty;
        RankingStatusMessage = $"正在加载{SelectedRankingType}...";
        RefreshRankingVisibility();

        await LoadRankingDisplayPageCoreAsync(1, _rankingCancellationTokenSource.Token);
    }

    private async Task GoPreviousRankingPageAsync()
    {
        if (!CanGoPreviousRankingPage)
        {
            return;
        }

        await LoadRankingDisplayPageAsync(RankingPageIndex - 1);
    }

    private async Task GoNextRankingPageAsync()
    {
        if (!CanGoNextRankingPage)
        {
            return;
        }

        var targetPage = RankingPageIndex + 1;
        WriteRankingDiagnostics(
            "next-click",
            $"currentDisplayPage={RankingPageIndex} targetDisplayPage={targetPage} totalDisplayPages={RankingTotalDisplayPages} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage}");
        await LoadRankingDisplayPageAsync(targetPage);
    }

    private async Task LoadRankingDisplayPageAsync(int displayPage)
    {
        if (displayPage < 1 || displayPage > RankingTotalDisplayPages)
        {
            WriteRankingDiagnostics(
                "display-load-rejected",
                $"displayPage={displayPage} totalDisplayPages={RankingTotalDisplayPages} bufferedItems={_rankingMovies.Count}");
            return;
        }

        _rankingCancellationTokenSource ??= new CancellationTokenSource();
        await LoadRankingDisplayPageCoreAsync(displayPage, _rankingCancellationTokenSource.Token);
    }

    private async Task LoadRankingDisplayPageCoreAsync(
        int displayPage,
        CancellationToken cancellationToken)
    {
        var requestVersion = ++_rankingRequestVersion;
        var loadStopwatch = Stopwatch.StartNew();
        var requiredItemCount = GetRankingRequiredItemCount(displayPage);
        IsRankingLoading = true;
        WriteRankingDiagnostics(
            "display-load-start",
            $"displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage} sourceTotalPages={_rankingSourceTotalPages} sourceExhausted={_rankingSourceExhausted}");
        RankingStatusMessage = displayPage <= 1 ? $"正在加载{SelectedRankingType}..." : $"正在加载第 {displayPage} 页...";

        try
        {
            await EnsureRankingItemsForDisplayPageAsync(displayPage, requestVersion, cancellationToken);
            if (requestVersion != _rankingRequestVersion)
            {
                WriteRankingDiagnostics(
                    "display-load-stale",
                    $"displayPage={displayPage} requestVersion={requestVersion} activeVersion={_rankingRequestVersion} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
                return;
            }

            RankingPageIndex = displayPage;
            UpdateRankingTotalPages();
            var visibleItems = RebuildRankingRows();
            WriteRankingDiagnostics(
                "display-load-complete",
                $"displayPage={displayPage} requestVersion={requestVersion} visibleItems={visibleItems.Count} bufferedItems={_rankingMovies.Count} totalDisplayPages={RankingTotalDisplayPages} canGoNext={CanGoNextRankingPage} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
            RankingStatusMessage = visibleItems.Count == 0
                ? "榜单暂无结果。"
                : $"{SelectedRankingType}第 {RankingPageIndex} 页已加载。";
            _ = EnrichRankingOmdbRatingsAsync(visibleItems, requestVersion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WriteRankingDiagnostics(
                "display-load-canceled",
                $"displayPage={displayPage} requestVersion={requestVersion} bufferedItems={_rankingMovies.Count} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            RankingStatusMessage = $"榜单加载失败：{DescribeException(exception)}";
            WriteRankingDiagnostics(
                "display-load-failed",
                $"displayPage={displayPage} requestVersion={requestVersion} bufferedItems={_rankingMovies.Count} elapsedMs={loadStopwatch.ElapsedMilliseconds} errorType={exception.GetType().Name}");
            RankingSummaryText = BuildRankingSummaryText(0);
            RebuildRankingRows();
        }
        finally
        {
            IsRankingLoading = false;
            RefreshRankingCommandState();
        }
    }

    private async Task EnsureRankingItemsForDisplayPageAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var requiredItemCount = GetRankingRequiredItemCount(displayPage);
        WriteRankingDiagnostics(
            "ensure-start",
            $"displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage} canFetch={CanFetchNextRankingSourcePage()}");
        while (requestVersion == _rankingRequestVersion
               && _rankingMovies.Count < requiredItemCount
               && CanFetchNextRankingSourcePage())
        {
            WriteRankingDiagnostics(
                "ensure-fetch-needed",
                $"displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage}");
            await FetchNextRankingSourcePageAsync(requestVersion, cancellationToken);
        }

        WriteRankingDiagnostics(
            "ensure-complete",
            $"displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage} canFetch={CanFetchNextRankingSourcePage()} sourceExhausted={_rankingSourceExhausted}");
    }

    private bool CanFetchNextRankingSourcePage()
    {
        if (_rankingSourceExhausted || _rankingMovies.Count >= MaxRankingMovies)
        {
            return false;
        }

        if (_rankingSourceTotalPages > 0 && _rankingSourceNextPage > _rankingSourceTotalPages)
        {
            return false;
        }

        return _rankingSourceNextPage <= (int)Math.Ceiling(MaxRankingMovies / (double)RankingTmdbPageSize);
    }

    private async Task FetchNextRankingSourcePageAsync(
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var page = _rankingSourceNextPage;
        var bufferedBefore = _rankingMovies.Count;
        var fetchStopwatch = Stopwatch.StartNew();
        var fromCache = _rankingSourcePageCache.TryGetValue(page, out var response);
        WriteRankingDiagnostics(
            "source-fetch-start",
            $"requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} bufferedItems={bufferedBefore} sourceTotalPages={_rankingSourceTotalPages}");

        try
        {
            if (!fromCache)
            {
                response = await LoadRankingPageFromTmdbAsync(page, cancellationToken);
                _rankingSourcePageCache[page] = response;
            }
        }
        catch (OperationCanceledException)
        {
            WriteRankingDiagnostics(
                "source-fetch-canceled",
                $"requestVersion={requestVersion} sourcePage={page} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            throw;
        }
        catch (Exception exception)
        {
            WriteRankingDiagnostics(
                "source-fetch-failed",
                $"requestVersion={requestVersion} sourcePage={page} elapsedMs={fetchStopwatch.ElapsedMilliseconds} errorType={exception.GetType().Name}");
            throw;
        }

        if (response is null)
        {
            WriteRankingDiagnostics(
                "source-fetch-missing-response",
                $"requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            return;
        }

        if (requestVersion != _rankingRequestVersion)
        {
            WriteRankingDiagnostics(
                "source-fetch-stale",
                $"requestVersion={requestVersion} activeVersion={_rankingRequestVersion} sourcePage={page} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            return;
        }

        _rankingSourceNextPage = page + 1;
        _rankingSourceTotalPages = response.TotalPages;
        _rankingTotalResults = response.TotalResults;
        if (response.TotalPages <= 0 || page >= response.TotalPages || response.Results.Count == 0)
        {
            _rankingSourceExhausted = true;
        }

        var pageItems = BuildRankingPageItems(response.Results);
        if (pageItems.Count == 0)
        {
            WriteRankingDiagnostics(
                "source-fetch-no-items",
                $"requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} tmdbResults={response.Results.Count} bufferedItems={_rankingMovies.Count} nextSourcePage={_rankingSourceNextPage} sourceExhausted={_rankingSourceExhausted} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            return;
        }

        var statusStopwatch = Stopwatch.StartNew();
        var statuses = await _statusResolver.ResolveAsync(pageItems.Select(item => item.TmdbId), cancellationToken);
        WriteRankingDiagnostics(
            "status-resolve-complete",
            $"requestVersion={requestVersion} sourcePage={page} itemCount={pageItems.Count} statusCount={statuses.Count} elapsedMs={statusStopwatch.ElapsedMilliseconds}");
        foreach (var item in pageItems)
        {
            if (statuses.TryGetValue(item.TmdbId, out var status))
            {
                item.ApplyStatus(status);
            }
        }

        _rankingMovies.AddRange(pageItems);
        UpdateRankingTotalPages();
        WriteRankingDiagnostics(
            "source-fetch-complete",
            $"requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} tmdbResults={response.Results.Count} addedItems={pageItems.Count} bufferedBefore={bufferedBefore} bufferedAfter={_rankingMovies.Count} nextSourcePage={_rankingSourceNextPage} sourceTotalPages={_rankingSourceTotalPages} totalResults={_rankingTotalResults} sourceExhausted={_rankingSourceExhausted} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
    }

    private Task<TmdbMovieDiscoveryPage> LoadRankingPageFromTmdbAsync(
        int page,
        CancellationToken cancellationToken)
    {
        return SelectedRankingType switch
        {
            RankingTypeTopRated => _tmdbService.GetTopRatedMoviesAsync(page, cancellationToken: cancellationToken),
            RankingTypeTrending => _tmdbService.GetTrendingMoviesAsync(GetSelectedTrendingWindow(), page, cancellationToken: cancellationToken),
            _ => _tmdbService.GetPopularMoviesAsync(page, cancellationToken: cancellationToken)
        };
    }

    private List<DiscoveryMovieCardViewModel> BuildRankingPageItems(IReadOnlyList<TmdbMovieDiscoveryItem> sourceItems)
    {
        var remainingSlots = MaxRankingMovies - _rankingMovies.Count;
        if (remainingSlots <= 0)
        {
            return [];
        }

        var pageItems = new List<DiscoveryMovieCardViewModel>();
        foreach (var sourceItem in sourceItems)
        {
            if (sourceItem.TmdbId <= 0
                || !_rankingTmdbIds.Add(sourceItem.TmdbId)
                || pageItems.Count >= remainingSlots)
            {
                continue;
            }

            pageItems.Add(new DiscoveryMovieCardViewModel(sourceItem, ++_nextRankingRank));
        }

        return pageItems;
    }

    private async Task EnrichRankingOmdbRatingsAsync(
        IReadOnlyList<DiscoveryMovieCardViewModel> items,
        int requestVersion)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var limiter = new SemaphoreSlim(OmdbResolveConcurrency, OmdbResolveConcurrency);
        var tasks = items.Select(
            async item =>
            {
                await limiter.WaitAsync();
                try
                {
                    if (requestVersion != _rankingRequestVersion || item.OmdbRating is not null)
                    {
                        return;
                    }

                    var imdbId = item.ImdbId;
                    if (string.IsNullOrWhiteSpace(imdbId))
                    {
                        MetadataSearchCandidate? details;
                        try
                        {
                            details = await _tmdbService.GetMovieDetailsAsync(item.TmdbId, CancellationToken.None);
                        }
                        catch
                        {
                            return;
                        }

                        if (details is null || requestVersion != _rankingRequestVersion)
                        {
                            return;
                        }

                        await DispatchAsync(() => item.SetDetailsSnapshot(details));
                        imdbId = details.ImdbId;
                    }

                    if (string.IsNullOrWhiteSpace(imdbId))
                    {
                        return;
                    }

                    MovieRatingItem? omdbRating;
                    try
                    {
                        omdbRating = await _omdbService.GetRatingAsync(imdbId, CancellationToken.None);
                    }
                    catch
                    {
                        return;
                    }

                    if (omdbRating is null || requestVersion != _rankingRequestVersion)
                    {
                        return;
                    }

                    await DispatchAsync(() => item.SetOmdbRating(omdbRating));
                }
                finally
                {
                    limiter.Release();
                }
            });

        await Task.WhenAll(tasks);
    }

    private IReadOnlyList<DiscoveryMovieCardViewModel> RebuildRankingRows()
    {
        var visibleItems = GetRankingDisplayItems(RankingPageIndex);
        TopRankingMovie = RankingPageIndex == 1 ? visibleItems.FirstOrDefault() : null;
        var rowItems = RankingPageIndex == 1
            ? visibleItems.Skip(1).ToList()
            : visibleItems;

        RankingRows.Clear();
        for (var index = 0; index < rowItems.Count; index += 2)
        {
            var left = rowItems[index];
            var right = index + 1 < rowItems.Count ? rowItems[index + 1] : null;
            RankingRows.Add(new DiscoveryRankingRowViewModel(left, right));
        }

        RankingSummaryText = BuildRankingSummaryText(visibleItems.Count);
        _canGoToNextRankingPage = RankingPageIndex < RankingTotalDisplayPages
                                  && GetRankingPageStartIndex(RankingPageIndex + 1) < Math.Min(MaxRankingMovies, GetRankingDisplayLimit());
        RefreshRankingVisibility();
        RefreshRankingCommandState();
        return visibleItems;
    }

    private List<DiscoveryMovieCardViewModel> GetRankingDisplayItems(int displayPage)
    {
        var startIndex = GetRankingPageStartIndex(displayPage);
        var pageSize = displayPage == 1 ? RankingFirstDisplayPageSize : RankingRegularDisplayPageSize;
        return _rankingMovies
            .Skip(startIndex)
            .Take(pageSize)
            .ToList();
    }

    private static int GetRankingPageStartIndex(int displayPage)
    {
        return displayPage <= 1
            ? 0
            : RankingFirstDisplayPageSize + (displayPage - 2) * RankingRegularDisplayPageSize;
    }

    private static int GetRankingRequiredItemCount(int displayPage)
    {
        if (displayPage <= 1)
        {
            return RankingFirstDisplayPageSize;
        }

        return Math.Min(
            MaxRankingMovies,
            RankingFirstDisplayPageSize + (displayPage - 1) * RankingRegularDisplayPageSize);
    }

    private void UpdateRankingTotalPages()
    {
        var displayLimit = GetRankingDisplayLimit();
        RankingTotalDisplayPages = displayLimit <= RankingFirstDisplayPageSize
            ? 1
            : 1 + (int)Math.Ceiling((displayLimit - RankingFirstDisplayPageSize) / (double)RankingRegularDisplayPageSize);
        _canGoToNextRankingPage = RankingPageIndex < RankingTotalDisplayPages;
        OnPropertyChanged(nameof(CanGoNextRankingPage));
    }

    private int GetRankingDisplayLimit()
    {
        if (_rankingTotalResults <= 0)
        {
            return MaxRankingMovies;
        }

        return Math.Min(MaxRankingMovies, _rankingTotalResults);
    }

    private string BuildRankingSummaryText(int visibleCount)
    {
        if (_rankingMovies.Count == 0)
        {
            return string.Empty;
        }

        var startRank = GetRankingPageStartIndex(RankingPageIndex) + 1;
        var endRank = Math.Min(startRank + Math.Max(0, visibleCount) - 1, GetRankingDisplayLimit());
        var totalText = _rankingTotalResults > 0 ? _rankingTotalResults.ToString() : "未知总数";
        return $"当前页显示第 {startRank}-{endRank} 名，已缓存 {_rankingMovies.Count} / {totalText}，最多展示前 {MaxRankingMovies} 名";
    }

    private string GetSelectedTrendingWindow()
    {
        return string.Equals(SelectedTrendingTime, TrendingTimeWeek, StringComparison.Ordinal)
            ? TrendingWindowWeek
            : TrendingWindowDay;
    }

    private void WriteRankingDiagnostics(string eventName, string details)
    {
        AiPerfDiagnostics.WriteEvent($"event=ranking-{eventName} rankingType={GetRankingDiagnosticsType()} {details}");
    }

    private string GetRankingDiagnosticsType()
    {
        if (string.Equals(SelectedRankingType, RankingTypeTopRated, StringComparison.Ordinal))
        {
            return "top-rated";
        }

        if (string.Equals(SelectedRankingType, RankingTypeTrending, StringComparison.Ordinal))
        {
            return $"trending-{GetSelectedTrendingWindow()}";
        }

        return "popular";
    }

    private async Task EnsureAiRecommendationsActivatedAsync()
    {
        if (_hasActivatedAiRecommendations)
        {
            return;
        }

        _hasActivatedAiRecommendations = true;
        try
        {
            await AiRecommendationViewModel.ActivateAsync();
        }
        catch
        {
            _hasActivatedAiRecommendations = false;
        }
    }

    private void ResetSearchBuffers()
    {
        _searchResultPool.Clear();
        _searchTmdbIds.Clear();
        _searchSourcePageCache.Clear();
        _searchSourceNextPage = 1;
        _searchSourceTotalPages = 0;
        _searchTotalResults = 0;
        _nextSearchOrder = 0;
        _searchSourceExhausted = false;
        _canGoToNextSearchPage = false;
    }

    private void ResetRankingBuffers()
    {
        _rankingMovies.Clear();
        _rankingTmdbIds.Clear();
        _rankingSourcePageCache.Clear();
        _rankingSourceNextPage = 1;
        _rankingSourceTotalPages = 0;
        _rankingTotalResults = 0;
        _nextRankingRank = 0;
        _rankingSourceExhausted = false;
        _canGoToNextRankingPage = false;
    }

    private void RefreshSearchVisibility()
    {
        OnPropertyChanged(nameof(HasSearchMovies));
        OnPropertyChanged(nameof(ShowSearchEmptyState));
        OnPropertyChanged(nameof(ShowSearchStatusOverlay));
        OnPropertyChanged(nameof(SearchStatusOverlayText));
    }

    private void RefreshSearchCommandState()
    {
        OnPropertyChanged(nameof(CanGoPreviousSearchPage));
        OnPropertyChanged(nameof(CanGoNextSearchPage));
        SearchCommand.RaiseCanExecuteChanged();
        GoPreviousSearchPageCommand.RaiseCanExecuteChanged();
        GoNextSearchPageCommand.RaiseCanExecuteChanged();
    }

    private void RefreshRankingVisibility()
    {
        OnPropertyChanged(nameof(HasRankingMovies));
        OnPropertyChanged(nameof(ShowRankingEmptyState));
        OnPropertyChanged(nameof(ShowRankingStatusOverlay));
        OnPropertyChanged(nameof(RankingStatusOverlayText));
        OnPropertyChanged(nameof(ShowTopRankingMovie));
    }

    private void RefreshRankingCommandState()
    {
        OnPropertyChanged(nameof(CanGoPreviousRankingPage));
        OnPropertyChanged(nameof(CanGoNextRankingPage));
        OnPropertyChanged(nameof(IsRankingTimeSelectable));
        SelectTrendingTimeCommand.RaiseCanExecuteChanged();
        GoPreviousRankingPageCommand.RaiseCanExecuteChanged();
        GoNextRankingPageCommand.RaiseCanExecuteChanged();
    }

    private string GetSearchRegionParameter()
    {
        return RegionCountryCodes.TryGetValue(SelectedRegionFilter, out var codes)
            ? codes.FirstOrDefault() ?? string.Empty
            : string.Empty;
    }

    private static bool MatchesRegion(DiscoveryMovieCardViewModel item, string selectedRegion)
    {
        var knownCodes = RegionCountryCodes.Values.SelectMany(x => x).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(selectedRegion, FilterOther, StringComparison.Ordinal))
        {
            if (item.OriginCountries.Count > 0)
            {
                return item.OriginCountries.All(code => !knownCodes.Contains(code));
            }

            var knownLanguages = RegionLanguageFallbacks.Values.SelectMany(x => x).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return !string.IsNullOrWhiteSpace(item.OriginalLanguage)
                   && !knownLanguages.Contains(item.OriginalLanguage);
        }

        if (!RegionCountryCodes.TryGetValue(selectedRegion, out var codes))
        {
            return true;
        }

        if (item.OriginCountries.Count > 0)
        {
            return item.OriginCountries.Any(code => codes.Contains(code, StringComparer.OrdinalIgnoreCase));
        }

        return RegionLanguageFallbacks.TryGetValue(selectedRegion, out var languageCodes)
               && languageCodes.Contains(item.OriginalLanguage, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesLanguage(DiscoveryMovieCardViewModel item, string selectedLanguage)
    {
        if (string.Equals(selectedLanguage, FilterOther, StringComparison.Ordinal))
        {
            var knownLanguageCodes = LanguageCodes.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return !string.IsNullOrWhiteSpace(item.OriginalLanguage)
                   && !knownLanguageCodes.Contains(item.OriginalLanguage);
        }

        return LanguageCodes.TryGetValue(selectedLanguage, out var code)
               && string.Equals(item.OriginalLanguage, code, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDecade(DiscoveryMovieCardViewModel item, string selectedDecade)
    {
        if (!item.ReleaseYear.HasValue)
        {
            return false;
        }

        if (string.Equals(selectedDecade, DecadeEarlier, StringComparison.Ordinal))
        {
            return item.ReleaseYear.Value < 1960;
        }

        if (!selectedDecade.EndsWith("s", StringComparison.Ordinal)
            || !int.TryParse(selectedDecade.TrimEnd('s'), out var decadeStart))
        {
            return true;
        }

        return item.ReleaseYear.Value >= decadeStart && item.ReleaseYear.Value < decadeStart + 10;
    }

    private static IReadOnlyList<string> SplitTags(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? []
            : text
                .Split(['、', '/', ',', '，', ';', '；', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList();
    }

    private static string FormatTotalResults(int totalResults)
    {
        return totalResults > 0 ? totalResults.ToString() : "未知总数";
    }

    private static string DescribeException(Exception exception)
    {
        return exception.Message;
    }

    private static Task DispatchAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }
}
