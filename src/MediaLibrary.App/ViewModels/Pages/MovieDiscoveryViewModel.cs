using System.Collections.ObjectModel;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
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
    private const int MaxRankingMovies = 200;
    private const int SearchDisplayPageSize = 30;
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
    private readonly List<DiscoveryMovieCardViewModel> _loadedSearchMovies = [];
    private readonly List<DiscoveryMovieCardViewModel> _loadedRankingMovies = [];
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
    private int _searchDisplayPage = 1;
    private int _searchTotalPages;
    private int _searchTotalResults;
    private int _searchRequestVersion;
    private bool _canGoToNextSearchPage;
    private string _selectedRankingType = RankingTypePopular;
    private string _selectedTrendingTime = TrendingTimeDay;
    private string _rankingStatusMessage = "打开榜单后将加载 TMDB 热门榜。";
    private string _rankingSummaryText = string.Empty;
    private bool _isRankingLoading;
    private int _rankingDisplayPage = 1;
    private int _rankingTotalPages;
    private int _rankingTotalResults;
    private int _rankingRequestVersion;
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
        RegionFilterOptions = [FilterAll, "中国大陆", "香港", "台湾", "美国", "日本", "韩国", "英国", "法国", "德国", "印度", "泰国", "其它"];
        WatchStatusFilterOptions = [FilterAll, "已入库", "未入库", "已看", "未看", "想看", "喜爱", "不想看"];
        SortOptions = [SortRelevance, SortRating, SortPopularity, SortReleaseDate, SortTitle];
        SortDirectionOptions = [DirectionDescending, DirectionAscending];
        DecadeFilterOptions = [DecadeAll, "2020s", "2010s", "2000s", "1990s", "1980s", "1970s", "1960s", DecadeEarlier];
        LanguageFilterOptions = [FilterAll, "中文", "英语", "日语", "韩语", "法语", "德语", "西班牙语", "印地语", "泰语", "其它"];
        RankingTypeOptions = [RankingTypePopular, RankingTypeTopRated, RankingTypeTrending];
        TrendingTimeOptions = [TrendingTimeDay, TrendingTimeWeek];

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsSearchLoading);
        LoadMoreSearchCommand = new AsyncRelayCommand(LoadMoreSearchAsync, () => CanLoadMoreSearch);
        ClearSearchFiltersCommand = new RelayCommand(ClearSearchFilters);
        ShowLayoutSwitchPlaceholderCommand = new RelayCommand(() => SearchStatusMessage = "切布局将在后续视觉阶段接入。");
        OpenSearchMovieCommand = new RelayCommand(OpenSearchMovie);
        ToggleSearchWantToWatchCommand = new AsyncRelayCommand(ToggleSearchWantToWatchAsync);
        SelectRankingTypeCommand = new RelayCommand(SelectRankingType);
        SelectTrendingTimeCommand = new RelayCommand(SelectTrendingTime, _ => IsTrendingRanking);
        LoadMoreRankingsCommand = new AsyncRelayCommand(LoadMoreRankingsAsync, () => CanLoadMoreRankings);
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

    public AsyncRelayCommand LoadMoreSearchCommand { get; }

    public RelayCommand ClearSearchFiltersCommand { get; }

    public RelayCommand ShowLayoutSwitchPlaceholderCommand { get; }

    public RelayCommand OpenSearchMovieCommand { get; }

    public AsyncRelayCommand ToggleSearchWantToWatchCommand { get; }

    public RelayCommand SelectRankingTypeCommand { get; }

    public RelayCommand SelectTrendingTimeCommand { get; }

    public AsyncRelayCommand LoadMoreRankingsCommand { get; }

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
        set => SetProperty(ref _selectedSearchType, value);
    }

    public string SelectedGenreFilter
    {
        get => _selectedGenreFilter;
        set
        {
            if (SetProperty(ref _selectedGenreFilter, value))
            {
                ApplySearchFilters();
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
                ApplySearchFilters();
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
                ApplySearchFilters();
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
                ApplySearchFilters();
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
                ApplySearchFilters();
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
                ApplySearchFilters();
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
                ApplySearchFilters();
            }
        }
    }

    public string SearchStatusMessage
    {
        get => _searchStatusMessage;
        private set => SetProperty(ref _searchStatusMessage, value);
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
                RefreshSearchCommandState();
            }
        }
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set
        {
            if (SetProperty(ref _isLoadingMore, value))
            {
                OnPropertyChanged(nameof(LoadMoreSearchButtonText));
                RefreshSearchCommandState();
            }
        }
    }

    public bool HasSearchMovies => SearchMovies.Count > 0;

    public bool ShowSearchEmptyState => !HasSearchMovies && !IsSearchLoading;

    public bool CanLoadMoreSearch => !IsSearchLoading
                                     && !IsLoadingMore
                                     && _searchPage > 0
                                     && _searchPage < _searchTotalPages;

    public string LoadMoreSearchButtonText => IsLoadingMore ? "加载中..." : "加载更多";

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

    public bool IsRankingTimeSelectable => IsTrendingRanking && !IsRankingLoading && !IsRankingLoadingMore;

    public DiscoveryMovieCardViewModel? TopRankingMovie
    {
        get => _topRankingMovie;
        private set => SetProperty(ref _topRankingMovie, value);
    }

    public string RankingStatusMessage
    {
        get => _rankingStatusMessage;
        private set => SetProperty(ref _rankingStatusMessage, value);
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
                RefreshRankingCommandState();
            }
        }
    }

    public bool IsRankingLoadingMore
    {
        get => _isRankingLoadingMore;
        private set
        {
            if (SetProperty(ref _isRankingLoadingMore, value))
            {
                RefreshRankingCommandState();
            }
        }
    }

    public bool HasRankingMovies => TopRankingMovie is not null;

    public bool ShowRankingEmptyState => !HasRankingMovies && !IsRankingLoading;

    public bool CanLoadMoreRankings => !IsRankingLoading
                                       && !IsRankingLoadingMore
                                       && _rankingPage > 0
                                       && _rankingPage < _rankingTotalPages
                                       && _loadedRankingMovies.Count < MaxRankingMovies;

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
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _searchCancellationTokenSource.Token;
        var requestVersion = ++_searchRequestVersion;

        _loadedSearchMovies.Clear();
        _nextSearchOrder = 0;
        _searchPage = 0;
        _searchTotalPages = 0;
        _searchTotalResults = 0;
        SearchMovies.Clear();
        RefreshSearchVisibility();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SearchStatusMessage = "请输入关键词。";
            SearchSummaryText = string.Empty;
            RefreshSearchCommandState();
            return;
        }

        await LoadSearchPageAsync(1, append: false, requestVersion, cancellationToken);
    }

    private async Task LoadMoreSearchAsync()
    {
        if (!CanLoadMoreSearch)
        {
            return;
        }

        var cancellationToken = _searchCancellationTokenSource?.Token ?? CancellationToken.None;
        await LoadSearchPageAsync(_searchPage + 1, append: true, _searchRequestVersion, cancellationToken);
    }

    private async Task LoadSearchPageAsync(
        int page,
        bool append,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        if (append)
        {
            IsLoadingMore = true;
        }
        else
        {
            IsSearchLoading = true;
        }

        try
        {
            var response = string.Equals(SelectedSearchType, SearchTypePerson, StringComparison.Ordinal)
                ? await _tmdbService.SearchDiscoveryMoviesByPersonAsync(SearchText, page, cancellationToken)
                : await _tmdbService.SearchDiscoveryMoviesAsync(SearchText, page, cancellationToken: cancellationToken);
            if (requestVersion != _searchRequestVersion)
            {
                return;
            }

            _searchPage = response.Page;
            _searchTotalPages = response.TotalPages;
            _searchTotalResults = response.TotalResults;

            var existingTmdbIds = _loadedSearchMovies.Select(x => x.TmdbId).ToHashSet();
            var pageItems = response.Results
                .Where(item => item.TmdbId > 0 && existingTmdbIds.Add(item.TmdbId))
                .Select(item => new DiscoveryMovieCardViewModel(item, ++_nextSearchOrder))
                .ToList();

            var statuses = await _statusResolver.ResolveAsync(pageItems.Select(item => item.TmdbId), cancellationToken);
            foreach (var item in pageItems)
            {
                if (statuses.TryGetValue(item.TmdbId, out var status))
                {
                    item.ApplyStatus(status);
                }
            }

            _loadedSearchMovies.AddRange(pageItems);
            ApplySearchFilters();

            if (_loadedSearchMovies.Count == 0)
            {
                SearchStatusMessage = string.IsNullOrWhiteSpace(response.ResultMessage)
                    ? "未找到相关影片。"
                    : response.ResultMessage;
            }
            else
            {
                var sourceMessage = string.IsNullOrWhiteSpace(response.ResultMessage) ? string.Empty : $" {response.ResultMessage}";
                SearchStatusMessage = $"已加载 {_loadedSearchMovies.Count} 部影片。{sourceMessage}".Trim();
                _ = EnrichOmdbRatingsAsync(pageItems, requestVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SearchStatusMessage = $"搜索失败：{DescribeException(exception)}";
            ApplySearchFilters();
        }
        finally
        {
            if (append)
            {
                IsLoadingMore = false;
            }
            else
            {
                IsSearchLoading = false;
            }

            RefreshSearchCommandState();
        }
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
                                ApplySearchFilters();
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
                            ApplySearchFilters();
                        });
                }
                finally
                {
                    limiter.Release();
                }
            });

        await Task.WhenAll(tasks);
    }

    private void ApplySearchFilters()
    {
        if (_suppressFilterApply)
        {
            return;
        }

        var query = _loadedSearchMovies.AsEnumerable();

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

        query = ApplySearchSorting(query);
        var filtered = query.ToList();

        SearchMovies.Clear();
        foreach (var item in filtered)
        {
            SearchMovies.Add(item);
        }

        SearchSummaryText = _loadedSearchMovies.Count == 0
            ? string.Empty
            : $"已加载 {_loadedSearchMovies.Count} / {FormatTotalResults(_searchTotalResults)}，当前筛选显示 {filtered.Count} 部";
        if (_loadedSearchMovies.Count > 0 && filtered.Count == 0)
        {
            SearchStatusMessage = "当前筛选条件下无结果。";
        }

        RefreshSearchVisibility();
        RefreshSearchCommandState();
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
                ? query.OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => descending
                ? query.OrderBy(item => item.SearchOrder)
                : query.OrderByDescending(item => item.SearchOrder)
        };
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

        ApplySearchFilters();
        SearchStatusMessage = _loadedSearchMovies.Count == 0
            ? "筛选已清除。请输入关键词搜索 TMDB 影片。"
            : "筛选已清除。";
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
            ApplySearchFilters);
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
            RebuildRankingRows);
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

    public void RequestLoadMoreRankingsFromScroll()
    {
        if (LoadMoreRankingsCommand.CanExecute(null))
        {
            LoadMoreRankingsCommand.Execute(null);
        }
    }

    private async Task ResetAndLoadRankingsAsync()
    {
        _rankingCancellationTokenSource?.Cancel();
        _rankingCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _rankingCancellationTokenSource.Token;
        var requestVersion = ++_rankingRequestVersion;

        _loadedRankingMovies.Clear();
        _rankingPage = 0;
        _rankingTotalPages = 0;
        _rankingTotalResults = 0;
        _nextRankingRank = 0;
        TopRankingMovie = null;
        RankingRows.Clear();
        RankingSummaryText = string.Empty;
        RankingStatusMessage = $"正在加载{SelectedRankingType}...";
        RefreshRankingVisibility();

        await LoadRankingPageAsync(1, append: false, requestVersion, cancellationToken);
    }

    private async Task LoadMoreRankingsAsync()
    {
        if (!CanLoadMoreRankings)
        {
            return;
        }

        var cancellationToken = _rankingCancellationTokenSource?.Token ?? CancellationToken.None;
        await LoadRankingPageAsync(_rankingPage + 1, append: true, _rankingRequestVersion, cancellationToken);
    }

    private async Task LoadRankingPageAsync(
        int page,
        bool append,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        if (append)
        {
            IsRankingLoadingMore = true;
        }
        else
        {
            IsRankingLoading = true;
        }

        try
        {
            var response = await LoadRankingPageFromTmdbAsync(page, cancellationToken);
            if (requestVersion != _rankingRequestVersion)
            {
                return;
            }

            _rankingPage = response.Page;
            _rankingTotalPages = response.TotalPages;
            _rankingTotalResults = response.TotalResults;

            var pageItems = BuildRankingPageItems(response.Results);
            var statuses = await _statusResolver.ResolveAsync(pageItems.Select(item => item.TmdbId), cancellationToken);
            foreach (var item in pageItems)
            {
                if (statuses.TryGetValue(item.TmdbId, out var status))
                {
                    item.ApplyStatus(status);
                }
            }

            _loadedRankingMovies.AddRange(pageItems);
            RebuildRankingRows();

            if (_loadedRankingMovies.Count == 0)
            {
                RankingStatusMessage = "榜单暂无结果。";
            }
            else if (_loadedRankingMovies.Count >= MaxRankingMovies)
            {
                RankingStatusMessage = "已显示前 200 名。";
            }
            else
            {
                RankingStatusMessage = append
                    ? $"已加载到第 {_loadedRankingMovies.Count} 名。"
                    : $"{SelectedRankingType}已加载。";
            }

            RankingSummaryText = BuildRankingSummaryText();
            _ = EnrichRankingOmdbRatingsAsync(pageItems, requestVersion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RankingStatusMessage = $"榜单加载失败：{DescribeException(exception)}";
            RankingSummaryText = BuildRankingSummaryText();
            RebuildRankingRows();
        }
        finally
        {
            if (append)
            {
                IsRankingLoadingMore = false;
            }
            else
            {
                IsRankingLoading = false;
            }

            RefreshRankingCommandState();
        }
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
        var remainingSlots = MaxRankingMovies - _loadedRankingMovies.Count;
        if (remainingSlots <= 0)
        {
            return [];
        }

        var existingTmdbIds = _loadedRankingMovies.Select(item => item.TmdbId).ToHashSet();
        var pageItems = new List<DiscoveryMovieCardViewModel>();
        foreach (var sourceItem in sourceItems)
        {
            if (sourceItem.TmdbId <= 0
                || !existingTmdbIds.Add(sourceItem.TmdbId)
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

    private void RebuildRankingRows()
    {
        TopRankingMovie = _loadedRankingMovies.FirstOrDefault();
        RankingRows.Clear();
        for (var index = 1; index < _loadedRankingMovies.Count; index += 2)
        {
            var left = _loadedRankingMovies[index];
            var right = index + 1 < _loadedRankingMovies.Count ? _loadedRankingMovies[index + 1] : null;
            RankingRows.Add(new DiscoveryRankingRowViewModel(left, right));
        }

        RefreshRankingVisibility();
        RefreshRankingCommandState();
    }

    private string BuildRankingSummaryText()
    {
        if (_loadedRankingMovies.Count == 0)
        {
            return string.Empty;
        }

        var totalText = _rankingTotalResults > 0 ? _rankingTotalResults.ToString() : "未知总数";
        var capText = _loadedRankingMovies.Count >= MaxRankingMovies ? "，已达 200 名上限" : string.Empty;
        return $"已加载 {_loadedRankingMovies.Count} / {totalText}{capText}";
    }

    private string GetSelectedTrendingWindow()
    {
        return string.Equals(SelectedTrendingTime, TrendingTimeWeek, StringComparison.Ordinal)
            ? TrendingWindowWeek
            : TrendingWindowDay;
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

    private void RefreshSearchVisibility()
    {
        OnPropertyChanged(nameof(HasSearchMovies));
        OnPropertyChanged(nameof(ShowSearchEmptyState));
        OnPropertyChanged(nameof(CanLoadMoreSearch));
    }

    private void RefreshSearchCommandState()
    {
        OnPropertyChanged(nameof(CanLoadMoreSearch));
        SearchCommand.RaiseCanExecuteChanged();
        LoadMoreSearchCommand.RaiseCanExecuteChanged();
    }

    private void RefreshRankingVisibility()
    {
        OnPropertyChanged(nameof(HasRankingMovies));
        OnPropertyChanged(nameof(ShowRankingEmptyState));
        OnPropertyChanged(nameof(CanLoadMoreRankings));
    }

    private void RefreshRankingCommandState()
    {
        OnPropertyChanged(nameof(CanLoadMoreRankings));
        OnPropertyChanged(nameof(IsRankingTimeSelectable));
        SelectTrendingTimeCommand.RaiseCanExecuteChanged();
        LoadMoreRankingsCommand.RaiseCanExecuteChanged();
    }

    private static bool MatchesRegion(DiscoveryMovieCardViewModel item, string selectedRegion)
    {
        var knownCodes = RegionCountryCodes.Values.SelectMany(x => x).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(selectedRegion, "其它", StringComparison.Ordinal))
        {
            return item.OriginCountries.Count > 0
                   && item.OriginCountries.All(code => !knownCodes.Contains(code));
        }

        return RegionCountryCodes.TryGetValue(selectedRegion, out var codes)
               && item.OriginCountries.Any(code => codes.Contains(code, StringComparer.OrdinalIgnoreCase));
    }

    private static bool MatchesLanguage(DiscoveryMovieCardViewModel item, string selectedLanguage)
    {
        if (string.Equals(selectedLanguage, "其它", StringComparison.Ordinal))
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
