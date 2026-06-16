using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Models.Discovery;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class MovieDiscoveryViewModel : PageViewModelBase
{
    private const int SearchTabIndex = 0;
    private const int RankingTabIndex = 1;
    private const int AiRecommendationTabIndex = 2;
    private const string DiscoveryMediaTypeMovie = "电影";
    private const string DiscoveryMediaTypeTv = "电视剧";
    private const string SearchTypeMovieTitle = "按电影名";
    private const string SearchTypeTvTitle = "按电视剧名";
    private const string SearchTypePerson = "按人物名";
    private const string RankingTypePopular = "热门榜";
    private const string RankingTypeTopRated = "高分榜";
    private const string RankingTypeTrending = "趋势榜";
    private const string TrendingTimeDay = "今日趋势";
    private const string TrendingTimeWeek = "本周趋势";
    private const string TrendingWindowDay = "day";
    private const string TrendingWindowWeek = "week";
    private const string FilterAll = "全部";
    private const string FilterOther = "其它";
    private const string PlaybackSourceWithSource = "有播放源";
    private const string PlaybackSourceWithoutSource = "无播放源";
    private const string LibraryStatusInLibrary = "已入库";
    private const string LibraryStatusNotInLibrary = "未入库";
    private const string WatchStatusWatched = "已看";
    private const string WatchStatusUnwatched = "未看";
    private const string CollectionStatusFavorite = "喜爱";
    private const string CollectionStatusWantToWatch = "想看";
    private const string CollectionStatusNotInterested = "不想看";
    private const string CollectionStatusOther = "其他";
    private const string SortRelevance = "相关度";
    private const string SortRating = "评分";
    private const string SortPopularity = "热度";
    private const string SortReleaseDate = "上映时间";
    private const string SortTitle = "片名";
    private const string SortFirstAirYear = "首播年份";
    private const string SortSeriesName = "剧名";
    private const string DirectionDescending = "降序";
    private const string DirectionAscending = "升序";
    private const string DecadeAll = "全部";
    private const string DecadeEarlier = "更早";
    private const string SearchLayoutPoster = "poster";
    private const string SearchLayoutList = "list";
    private const int OmdbResolveConcurrency = 2;
    private const int TvDetailResolveConcurrency = 3;
    private const int SearchTmdbPageSize = 20;
    private const int SearchDisplayPageSize = 30;
    private const int SearchFilteredMaxDisplayPages = 30;
    private const int SearchDiscoverMaxDisplayPages = 100;
    private const int SearchFilteredMaxSourcePages =
        (SearchFilteredMaxDisplayPages * SearchDisplayPageSize + SearchTmdbPageSize - 1) / SearchTmdbPageSize;
    private const int SearchDiscoverMaxSourcePages =
        (SearchDiscoverMaxDisplayPages * SearchDisplayPageSize + SearchTmdbPageSize - 1) / SearchTmdbPageSize;
    private const int RankingTmdbPageSize = 20;
    private const int MaxRankingMovies = 200;
    private const int RankingFirstDisplayPageSize = 21;
    private const int RankingRegularDisplayPageSize = 20;
    private const int PageLoadMaxRetryAttempts = 3;
    private const int PageLoadRetryDelayMilliseconds = 350;
    private const int PageLoadAttemptTimeoutSeconds = 5;
    private const int SearchFilteredPageLoadAttemptTimeoutSeconds = SearchFilteredMaxSourcePages * 3;
    private const int PageLoadFailureToastMilliseconds = 1500;
    private const string TransientPageMessageKindInfo = "Info";
    private const string TransientPageMessageKindWarning = "Warning";
    private const string TransientPageMessageKindError = "Error";

    private static readonly IReadOnlyList<string> MovieSearchTypeOptions =
    [
        SearchTypeMovieTitle,
        SearchTypePerson
    ];

    private static readonly IReadOnlyList<string> TvSearchTypeOptions =
    [
        SearchTypeTvTitle,
        SearchTypePerson
    ];

    private static readonly IReadOnlyList<string> RegionFilterLabels =
    [
        FilterAll,
        "中国大陆",
        "香港",
        "台湾",
        "美国",
        "日本",
        "韩国",
        "英国",
        "法国",
        "德国",
        "印度",
        "泰国",
        FilterOther
    ];

    private static readonly IReadOnlyList<string> LanguageFilterLabels =
    [
        FilterAll,
        "中文",
        "英语",
        "日语",
        "韩语",
        "法语",
        "德语",
        "西班牙语",
        "印地语",
        "泰语",
        FilterOther
    ];

    private static readonly IReadOnlyList<string> CollectionStatusFilterLabels =
    [
        FilterAll,
        CollectionStatusFavorite,
        CollectionStatusWantToWatch,
        CollectionStatusNotInterested,
        CollectionStatusOther
    ];

    private static readonly IReadOnlyList<string> DefaultCollectionStatusFilters =
    [
        CollectionStatusOther,
        CollectionStatusFavorite,
        CollectionStatusWantToWatch
    ];

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
    private readonly IDiscoveryTvSeriesStatusResolver _tvStatusResolver;
    private readonly IDiscoveryRatingRefreshService _ratingRefreshService;
    private readonly ITvMetadataHydrationService _tvMetadataHydrationService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IUserCollectionService _userCollectionService;
    private readonly IMovieDetailQueryService _movieDetailQueryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IDiscoveryPreferencesService _discoveryPreferencesService;
    private readonly List<DiscoveryMovieCardViewModel> _searchResultPool = [];
    private readonly HashSet<int> _searchTmdbIds = [];
    private readonly Dictionary<int, TmdbMovieDiscoveryPage> _searchSourcePageCache = [];
    private readonly List<DiscoveryTvSeriesCardViewModel> _tvSearchResultPool = [];
    private readonly HashSet<int> _tvSearchTmdbIds = [];
    private readonly Dictionary<int, TmdbTvSeriesSearchPage> _tvSearchSourcePageCache = [];
    private readonly List<DiscoveryMovieCardViewModel> _rankingMovies = [];
    private readonly HashSet<int> _rankingTmdbIds = [];
    private readonly Dictionary<int, TmdbMovieDiscoveryPage> _rankingSourcePageCache = [];
    private readonly List<DiscoveryTvSeriesCardViewModel> _rankingTvSeries = [];
    private readonly HashSet<int> _rankingTvSeriesTmdbIds = [];
    private readonly Dictionary<int, TmdbTvSeriesSearchPage> _rankingTvSeriesSourcePageCache = [];
    private readonly HashSet<string> _selectedGenreFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedRegionFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedLanguageFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedDecadeFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedCollectionStatusFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedTvGenreFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedTvRegionFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedTvLanguageFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedTvDecadeFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedTvCollectionStatusFilters = new(StringComparer.Ordinal);

    private CancellationTokenSource? _searchCancellationTokenSource;
    private CancellationTokenSource? _rankingCancellationTokenSource;
    private int _selectedTabIndex = SearchTabIndex;
    private bool _hasActivatedDiscoveryPage;
    private bool _hasActivatedRankings;
    private bool _hasActivatedAiRecommendations;
    private string _searchText = string.Empty;
    private string _selectedSearchMediaType = DiscoveryMediaTypeMovie;
    private string _selectedSearchType = SearchTypeMovieTitle;
    private bool _isSearchPosterLayout = true;
    private bool _isDiscoveryPreferencesLoaded;
    private bool _isActive;
    private bool _pendingLocalMovieCardRefresh;
    private int _localMovieCardRefreshVersion;
    private string _selectedGenreFilter = FilterAll;
    private string _selectedRegionFilter = FilterAll;
    private string _selectedWatchStatusFilter = FilterAll;
    private string _selectedPlaybackSourceFilter = FilterAll;
    private string _selectedLibraryStatusFilter = FilterAll;
    private string _selectedCollectionStatusFilter = FilterAll;
    private string _selectedSortOption = SortRelevance;
    private string _selectedSortDirection = DirectionDescending;
    private string _selectedDecadeFilter = DecadeAll;
    private string _selectedLanguageFilter = FilterAll;
    private string _searchStatusMessage = "输入电影名后搜索 TMDB 电影。";
    private string _searchSummaryText = string.Empty;
    private bool _isSearchLoading;
    private bool _searchLoadFailed;
    private bool _suppressFilterApply;
    private int _searchPageIndex = 1;
    private int _lastSearchDisplayPage = 1;
    private int _searchTotalPages;
    private int _searchTotalResults;
    private int _searchSourceNextPage = 1;
    private int _searchSourceTotalPages;
    private int _searchRequestVersion;
    private int _nextSearchOrder;
    private bool _searchSourceExhausted;
    private bool _canGoToNextSearchPage;
    private double _searchMoviePosterScrollOffset;
    private double _searchMovieListScrollOffset;
    private string _tvSearchStatusMessage = "输入电视剧名后搜索 TMDB 电视剧。";
    private string _tvSearchSummaryText = string.Empty;
    private bool _isTvSearchLoading;
    private bool _tvSearchLoadFailed;
    private bool _suppressTvFilterApply;
    private string _selectedTvGenreFilter = FilterAll;
    private string _selectedTvRegionFilter = FilterAll;
    private string _selectedTvWatchStatusFilter = FilterAll;
    private string _selectedTvPlaybackSourceFilter = FilterAll;
    private string _selectedTvLibraryStatusFilter = FilterAll;
    private string _selectedTvCollectionStatusFilter = FilterAll;
    private string _selectedTvSortOption = SortRelevance;
    private string _selectedTvSortDirection = DirectionDescending;
    private string _selectedTvDecadeFilter = DecadeAll;
    private string _selectedTvLanguageFilter = FilterAll;
    private int _tvSearchPageIndex = 1;
    private int _lastTvSearchDisplayPage = 1;
    private int _tvSearchTotalPages;
    private int _tvSearchTotalResults;
    private int _tvSearchSourceNextPage = 1;
    private int _tvSearchSourceTotalPages;
    private int _tvSearchRequestVersion;
    private int _nextTvSearchOrder;
    private bool _tvSearchSourceExhausted;
    private bool _canGoToNextTvSearchPage;
    private double _searchTvPosterScrollOffset;
    private double _searchTvListScrollOffset;
    private string _selectedRankingMediaType = DiscoveryMediaTypeMovie;
    private string _selectedRankingType = RankingTypePopular;
    private string _selectedTvRankingType = RankingTypePopular;
    private string _selectedTrendingTime = TrendingTimeDay;
    private string _selectedTvTrendingTime = TrendingTimeDay;
    private string _rankingStatusMessage = "打开榜单后将加载 TMDB 热门榜。";
    private string _rankingSummaryText = string.Empty;
    private bool _isRankingLoading;
    private bool _rankingLoadFailed;
    private bool _suppressRankingPresetApply;
    private int _rankingPageIndex = 1;
    private int _lastRankingDisplayPage = 1;
    private int _rankingTotalDisplayPages = 1;
    private int _rankingTotalResults;
    private int _rankingSourceNextPage = 1;
    private int _rankingSourceTotalPages;
    private int _rankingRequestVersion;
    private int _nextRankingRank;
    private bool _rankingSourceExhausted;
    private bool _canGoToNextRankingPage;
    private double _rankingMovieScrollOffset;
    private DiscoveryMovieCardViewModel? _topRankingMovie;
    private DiscoveryTvSeriesCardViewModel? _topRankingTvSeries;
    private string _tvRankingStatusMessage = "切换到电视剧榜单后将加载 TMDB TV 热门榜。";
    private string _tvRankingSummaryText = string.Empty;
    private bool _isTvRankingLoading;
    private bool _tvRankingLoadFailed;
    private int _tvRankingPageIndex = 1;
    private int _lastTvRankingDisplayPage = 1;
    private int _tvRankingTotalDisplayPages = 1;
    private int _tvRankingTotalResults;
    private int _tvRankingSourceNextPage = 1;
    private int _tvRankingSourceTotalPages;
    private int _tvRankingRequestVersion;
    private int _nextTvRankingRank;
    private bool _tvRankingSourceExhausted;
    private bool _canGoToNextTvRankingPage;
    private double _rankingTvScrollOffset;
    private int _tvSeriesOpenRequestVersion;
    private bool _isTvSeriesNavigating;
    private bool _openAiRecommendationsOnNextActivation;
    private string _transientPageMessageText = string.Empty;
    private string _transientPageMessageKind = TransientPageMessageKindInfo;
    private bool _isTransientPageMessageVisible;
    private int _transientPageMessageVersion;

    public MovieDiscoveryViewModel(
        RecommendationsViewModel aiRecommendationViewModel,
        ITmdbService tmdbService,
        IOmdbService omdbService,
        IDiscoveryMovieStatusResolver statusResolver,
        IDiscoveryTvSeriesStatusResolver tvStatusResolver,
        IDiscoveryRatingRefreshService ratingRefreshService,
        ITvMetadataHydrationService tvMetadataHydrationService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IUserCollectionService userCollectionService,
        IMovieDetailQueryService movieDetailQueryService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService,
        IConfirmationDialogService confirmationDialogService,
        IDiscoveryPreferencesService discoveryPreferencesService)
        : base("影片发现", "遇见更多适合你的影片")
    {
        _aiRecommendationViewModel = aiRecommendationViewModel;
        _tmdbService = tmdbService;
        _omdbService = omdbService;
        _statusResolver = statusResolver;
        _tvStatusResolver = tvStatusResolver;
        _ratingRefreshService = ratingRefreshService;
        _tvMetadataHydrationService = tvMetadataHydrationService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _userCollectionService = userCollectionService;
        _movieDetailQueryService = movieDetailQueryService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _confirmationDialogService = confirmationDialogService;
        _discoveryPreferencesService = discoveryPreferencesService;

        SearchMediaTypeOptions = [DiscoveryMediaTypeMovie, DiscoveryMediaTypeTv];
        GenreFilterOptions = TmdbGenreMapper.GenreLabels;
        RegionFilterOptions = RegionFilterLabels;
        WatchStatusFilterOptions = [FilterAll, WatchStatusWatched, WatchStatusUnwatched];
        PlaybackSourceFilterOptions = [FilterAll, PlaybackSourceWithSource, PlaybackSourceWithoutSource];
        LibraryStatusFilterOptions = [FilterAll, LibraryStatusInLibrary, LibraryStatusNotInLibrary];
        CollectionStatusFilterOptions = CollectionStatusFilterLabels;
        SortOptions = [SortRelevance, SortRating, SortPopularity, SortReleaseDate, SortTitle];
        SortDirectionOptions = [DirectionDescending, DirectionAscending];
        DecadeFilterOptions = [DecadeAll, "2020s", "2010s", "2000s", "1990s", "1980s", "1970s", "1960s", DecadeEarlier];
        LanguageFilterOptions = LanguageFilterLabels;
        TvGenreFilterOptions = TmdbTvGenreMapper.GenreLabels;
        TvWatchStatusFilterOptions = [FilterAll, WatchStatusWatched, WatchStatusUnwatched];
        TvSortOptions = [SortRelevance, SortRating, SortPopularity, SortFirstAirYear, SortSeriesName];
        RankingMediaTypeOptions = [DiscoveryMediaTypeMovie, DiscoveryMediaTypeTv];
        RankingTypeOptions = [RankingTypePopular, RankingTypeTopRated, RankingTypeTrending];
        TrendingTimeOptions = [TrendingTimeDay, TrendingTimeWeek];
        ReplaceDiscoveryFilterOptions(SearchGenreFilterMenuOptions, GenreFilterOptions);
        ReplaceDiscoveryFilterOptions(SearchRegionFilterMenuOptions, RegionFilterOptions);
        ReplaceDiscoveryFilterOptions(SearchLanguageFilterMenuOptions, LanguageFilterOptions);
        ReplaceDiscoveryFilterOptions(SearchDecadeFilterMenuOptions, DecadeFilterOptions);
        ReplaceDiscoveryFilterOptions(SearchCollectionStatusFilterMenuOptions, CollectionStatusFilterOptions);
        ReplaceDiscoveryFilterOptions(TvSearchGenreFilterMenuOptions, TvGenreFilterOptions);
        ReplaceDiscoveryFilterOptions(TvSearchRegionFilterMenuOptions, RegionFilterOptions);
        ReplaceDiscoveryFilterOptions(TvSearchLanguageFilterMenuOptions, LanguageFilterOptions);
        ReplaceDiscoveryFilterOptions(TvSearchDecadeFilterMenuOptions, DecadeFilterOptions);
        ReplaceDiscoveryFilterOptions(TvSearchCollectionStatusFilterMenuOptions, CollectionStatusFilterOptions);
        ResetCollectionStatusFiltersToDefault(_selectedCollectionStatusFilters);
        ResetCollectionStatusFiltersToDefault(_selectedTvCollectionStatusFilters);
        RefreshSearchGenreFilterState(applyFilters: false);
        RefreshSearchRegionFilterState(applyFilters: false);
        RefreshSearchLanguageFilterState(applyFilters: false);
        RefreshSearchDecadeFilterState(applyFilters: false);
        RefreshSearchCollectionStatusFilterState(applyFilters: false);
        RefreshTvSearchGenreFilterState(applyFilters: false);
        RefreshTvSearchRegionFilterState(applyFilters: false);
        RefreshTvSearchLanguageFilterState(applyFilters: false);
        RefreshTvSearchDecadeFilterState(applyFilters: false);
        RefreshTvSearchCollectionStatusFilterState(applyFilters: false);

        SelectDiscoveryTabCommand = new RelayCommand(SelectDiscoveryTab);
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsActiveSearchLoading);
        RetrySearchCommand = new AsyncRelayCommand(RetryActiveSearchAsync, () => CanRetryActiveSearch);
        GoPreviousSearchPageCommand = new AsyncRelayCommand(GoPreviousSearchPageAsync, () => CanGoPreviousActiveSearchPage);
        GoNextSearchPageCommand = new AsyncRelayCommand(GoNextSearchPageAsync, () => CanGoNextActiveSearchPage);
        ClearSearchFiltersCommand = new RelayCommand(ClearSearchFilters, () => CanClearSearchFilters);
        ClearSearchTextCommand = new RelayCommand(ClearSearchText, () => !string.IsNullOrEmpty(SearchText));
        SwitchSearchToPosterLayoutCommand = new RelayCommand(() => SetSearchLayout(isPosterLayout: true));
        SwitchSearchToListLayoutCommand = new RelayCommand(() => SetSearchLayout(isPosterLayout: false));
        ToggleSearchSortDirectionCommand = new RelayCommand(ToggleActiveSearchSortDirection);
        SelectSearchMediaTypeCommand = new RelayCommand(SelectSearchMediaType);
        SelectSearchTypeCommand = new RelayCommand(SelectSearchType);
        SelectSearchGenreFilterCommand = new RelayCommand(SelectSearchGenreFilter);
        SelectSearchRegionFilterCommand = new RelayCommand(value => SelectedRegionFilter = GetOptionValue(value, FilterAll));
        SelectSearchRegionMultiFilterCommand = new RelayCommand(SelectSearchRegionMultiFilter);
        SelectSearchWatchStatusFilterCommand = new RelayCommand(value => SelectedWatchStatusFilter = GetOptionValue(value, FilterAll));
        SelectSearchPlaybackSourceFilterCommand = new RelayCommand(value => SelectedPlaybackSourceFilter = GetOptionValue(value, FilterAll));
        SelectSearchLibraryStatusFilterCommand = new RelayCommand(value => SelectedLibraryStatusFilter = GetOptionValue(value, FilterAll));
        SelectSearchCollectionStatusFilterCommand = new RelayCommand(SelectSearchCollectionStatusFilter);
        SelectSearchSortOptionCommand = new RelayCommand(SelectActiveSearchSortOption);
        SelectSearchSortDirectionCommand = new RelayCommand(value => SelectedSortDirection = GetOptionValue(value, DirectionDescending));
        SelectSearchDecadeFilterCommand = new RelayCommand(SelectSearchDecadeFilter);
        SelectSearchLanguageFilterCommand = new RelayCommand(value => SelectedLanguageFilter = GetOptionValue(value, FilterAll));
        SelectSearchLanguageMultiFilterCommand = new RelayCommand(SelectSearchLanguageMultiFilter);
        SelectTvSearchGenreFilterCommand = new RelayCommand(SelectTvSearchGenreFilter);
        SelectTvSearchRegionFilterCommand = new RelayCommand(value => SelectedTvRegionFilter = GetOptionValue(value, FilterAll));
        SelectTvSearchRegionMultiFilterCommand = new RelayCommand(SelectTvSearchRegionMultiFilter);
        SelectTvSearchWatchStatusFilterCommand = new RelayCommand(value => SelectedTvWatchStatusFilter = GetOptionValue(value, FilterAll));
        SelectTvSearchPlaybackSourceFilterCommand = new RelayCommand(value => SelectedTvPlaybackSourceFilter = GetOptionValue(value, FilterAll));
        SelectTvSearchLibraryStatusFilterCommand = new RelayCommand(value => SelectedTvLibraryStatusFilter = GetOptionValue(value, FilterAll));
        SelectTvSearchCollectionStatusFilterCommand = new RelayCommand(SelectTvSearchCollectionStatusFilter);
        SelectTvSearchSortOptionCommand = new RelayCommand(value => SelectedTvSortOption = GetOptionValue(value, SortRelevance));
        SelectTvSearchSortDirectionCommand = new RelayCommand(value => SelectedTvSortDirection = GetOptionValue(value, DirectionDescending));
        SelectTvSearchDecadeFilterCommand = new RelayCommand(SelectTvSearchDecadeFilter);
        SelectTvSearchLanguageFilterCommand = new RelayCommand(value => SelectedTvLanguageFilter = GetOptionValue(value, FilterAll));
        SelectTvSearchLanguageMultiFilterCommand = new RelayCommand(SelectTvSearchLanguageMultiFilter);
        OpenSearchMovieCommand = new AsyncRelayCommand(OpenSearchMovieAsync);
        ToggleSearchWantToWatchCommand = new AsyncRelayCommand(ToggleSearchWantToWatchAsync);
        AddSearchMovieToLibraryCommand = new AsyncRelayCommand(AddSearchMovieToLibraryAsync);
        SelectRankingMediaTypeCommand = new RelayCommand(SelectRankingMediaType);
        SelectRankingTypeCommand = new RelayCommand(SelectRankingType);
        SelectTrendingTimeCommand = new RelayCommand(SelectTrendingTime, _ => IsActiveTrendingRanking);
        SelectRankingPresetCommand = new RelayCommand(SelectRankingPreset);
        RetryRankingCommand = new AsyncRelayCommand(RetryActiveRankingAsync, () => CanRetryActiveRanking);
        GoPreviousRankingPageCommand = new AsyncRelayCommand(GoPreviousRankingPageAsync, () => CanGoPreviousActiveRankingPage);
        GoNextRankingPageCommand = new AsyncRelayCommand(GoNextRankingPageAsync, () => CanGoNextActiveRankingPage);
        OpenRankingMovieCommand = new AsyncRelayCommand(OpenRankingMovieAsync);
        OpenRankingItemCommand = new AsyncRelayCommand(OpenRankingItemAsync);
        ToggleRankingWantToWatchCommand = new AsyncRelayCommand(ToggleRankingWantToWatchAsync);
        AddRankingMovieToLibraryCommand = new AsyncRelayCommand(AddRankingMovieToLibraryAsync);
        OpenTvSeriesCommand = new RelayCommand(OpenTvSeries, _ => !IsTvSeriesNavigating);
        AddTvSeriesToLibraryCommand = new AsyncRelayCommand(AddTvSeriesToLibraryAsync);
        _dataRefreshService.DataChanged += OnDataChanged;
    }

    public RecommendationsViewModel AiRecommendationViewModel => _aiRecommendationViewModel;

    public string TransientPageMessageText
    {
        get => _transientPageMessageText;
        private set => SetProperty(ref _transientPageMessageText, value);
    }

    public string TransientPageMessageKind
    {
        get => _transientPageMessageKind;
        private set => SetProperty(ref _transientPageMessageKind, value);
    }

    public bool IsTransientPageMessageVisible
    {
        get => _isTransientPageMessageVisible;
        private set => SetProperty(ref _isTransientPageMessageVisible, value);
    }

    public ObservableCollection<DiscoveryMovieCardViewModel> SearchMovies { get; } = [];

    public ObservableCollection<DiscoveryTvSeriesCardViewModel> SearchTvSeries { get; } = [];

    public ObservableCollection<DiscoveryRankingRowViewModel> RankingRows { get; } = [];

    public ObservableCollection<DiscoveryTvSeriesCardViewModel> RankingTvSeries { get; } = [];

    public ObservableCollection<DiscoveryTvRankingRowViewModel> RankingTvRows { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> SearchGenreFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> SearchRegionFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> SearchLanguageFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> SearchDecadeFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> SearchCollectionStatusFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> TvSearchGenreFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> TvSearchRegionFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> TvSearchLanguageFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> TvSearchDecadeFilterMenuOptions { get; } = [];

    public ObservableCollection<DiscoveryFilterOption> TvSearchCollectionStatusFilterMenuOptions { get; } = [];

    public IReadOnlyList<string> SearchMediaTypeOptions { get; }

    public IReadOnlyList<string> SearchTypeOptions => IsTvSearchSelected ? TvSearchTypeOptions : MovieSearchTypeOptions;

    public IReadOnlyList<string> GenreFilterOptions { get; }

    public IReadOnlyList<string> RegionFilterOptions { get; }

    public IReadOnlyList<string> WatchStatusFilterOptions { get; }

    public IReadOnlyList<string> PlaybackSourceFilterOptions { get; }

    public IReadOnlyList<string> LibraryStatusFilterOptions { get; }

    public IReadOnlyList<string> CollectionStatusFilterOptions { get; }

    public IReadOnlyList<string> SortOptions { get; }

    public IReadOnlyList<string> SortDirectionOptions { get; }

    public IReadOnlyList<string> DecadeFilterOptions { get; }

    public IReadOnlyList<string> LanguageFilterOptions { get; }

    public IReadOnlyList<string> TvGenreFilterOptions { get; }

    public IReadOnlyList<string> TvWatchStatusFilterOptions { get; }

    public IReadOnlyList<string> TvSortOptions { get; }

    public IReadOnlyList<string> RankingMediaTypeOptions { get; }

    public IReadOnlyList<string> RankingTypeOptions { get; }

    public IReadOnlyList<string> TrendingTimeOptions { get; }

    public event EventHandler? RequestCloseFilterMenu;

    public RelayCommand SelectDiscoveryTabCommand { get; }

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RetrySearchCommand { get; }

    public AsyncRelayCommand GoPreviousSearchPageCommand { get; }

    public AsyncRelayCommand GoNextSearchPageCommand { get; }

    public RelayCommand ClearSearchFiltersCommand { get; }

    public RelayCommand ClearSearchTextCommand { get; }

    public RelayCommand SwitchSearchToPosterLayoutCommand { get; }

    public RelayCommand SwitchSearchToListLayoutCommand { get; }

    public RelayCommand ToggleSearchSortDirectionCommand { get; }

    public RelayCommand SelectSearchMediaTypeCommand { get; }

    public RelayCommand SelectSearchTypeCommand { get; }

    public RelayCommand SelectSearchGenreFilterCommand { get; }

    public RelayCommand SelectSearchRegionFilterCommand { get; }

    public RelayCommand SelectSearchRegionMultiFilterCommand { get; }

    public RelayCommand SelectSearchWatchStatusFilterCommand { get; }

    public RelayCommand SelectSearchPlaybackSourceFilterCommand { get; }

    public RelayCommand SelectSearchLibraryStatusFilterCommand { get; }

    public RelayCommand SelectSearchCollectionStatusFilterCommand { get; }

    public RelayCommand SelectSearchSortOptionCommand { get; }

    public RelayCommand SelectSearchSortDirectionCommand { get; }

    public RelayCommand SelectSearchDecadeFilterCommand { get; }

    public RelayCommand SelectSearchLanguageFilterCommand { get; }

    public RelayCommand SelectSearchLanguageMultiFilterCommand { get; }

    public RelayCommand SelectTvSearchGenreFilterCommand { get; }

    public RelayCommand SelectTvSearchRegionFilterCommand { get; }

    public RelayCommand SelectTvSearchRegionMultiFilterCommand { get; }

    public RelayCommand SelectTvSearchWatchStatusFilterCommand { get; }

    public RelayCommand SelectTvSearchPlaybackSourceFilterCommand { get; }

    public RelayCommand SelectTvSearchLibraryStatusFilterCommand { get; }

    public RelayCommand SelectTvSearchCollectionStatusFilterCommand { get; }

    public RelayCommand SelectTvSearchSortOptionCommand { get; }

    public RelayCommand SelectTvSearchSortDirectionCommand { get; }

    public RelayCommand SelectTvSearchDecadeFilterCommand { get; }

    public RelayCommand SelectTvSearchLanguageFilterCommand { get; }

    public RelayCommand SelectTvSearchLanguageMultiFilterCommand { get; }

    public AsyncRelayCommand OpenSearchMovieCommand { get; }

    public AsyncRelayCommand ToggleSearchWantToWatchCommand { get; }

    public AsyncRelayCommand AddSearchMovieToLibraryCommand { get; }

    public RelayCommand SelectRankingMediaTypeCommand { get; }

    public RelayCommand SelectRankingTypeCommand { get; }

    public RelayCommand SelectTrendingTimeCommand { get; }

    public RelayCommand SelectRankingPresetCommand { get; }

    public AsyncRelayCommand RetryRankingCommand { get; }

    public AsyncRelayCommand GoPreviousRankingPageCommand { get; }

    public AsyncRelayCommand GoNextRankingPageCommand { get; }

    public AsyncRelayCommand OpenRankingMovieCommand { get; }

    public AsyncRelayCommand OpenRankingItemCommand { get; }

    public AsyncRelayCommand ToggleRankingWantToWatchCommand { get; }

    public RelayCommand OpenTvSeriesCommand { get; }

    public AsyncRelayCommand AddRankingMovieToLibraryCommand { get; }

    public AsyncRelayCommand AddTvSeriesToLibraryCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (!SetProperty(ref _selectedTabIndex, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSearchTabSelected));
            OnPropertyChanged(nameof(IsRankingTabSelected));
            OnPropertyChanged(nameof(IsAiRecommendationTabSelected));

            if (value != AiRecommendationTabIndex)
            {
                _aiRecommendationViewModel.ClosePreferenceDialog();
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

    public bool IsSearchTabSelected => SelectedTabIndex == SearchTabIndex;

    public bool IsRankingTabSelected => SelectedTabIndex == RankingTabIndex;

    public bool IsAiRecommendationTabSelected => SelectedTabIndex == AiRecommendationTabIndex;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(SearchInputToolTip));
                ClearSearchTextCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedSearchMediaType
    {
        get => _selectedSearchMediaType;
        set
        {
            if (SetProperty(ref _selectedSearchMediaType, value))
            {
                if (!IsSearchPersonSelected)
                {
                    _selectedSearchType = GetTitleSearchTypeForActiveMedia();
                    OnPropertyChanged(nameof(SelectedSearchType));
                }

                RefreshSearchModeProperties();
                RefreshActiveSearchPromptIfIdle();
            }
        }
    }

    public bool IsMovieSearchSelected => string.Equals(SelectedSearchMediaType, DiscoveryMediaTypeMovie, StringComparison.Ordinal);

    public bool IsTvSearchSelected => string.Equals(SelectedSearchMediaType, DiscoveryMediaTypeTv, StringComparison.Ordinal);

    public bool IsSearchPersonSelected => string.Equals(SelectedSearchType, SearchTypePerson, StringComparison.Ordinal);

    public string SearchPlaceholderText => IsSearchPersonSelected
        ? "输入需要搜索的导演/演员"
        : IsTvSearchSelected
            ? "输入需要搜索的电视剧名"
            : "输入需要搜索的电影";

    public string? SearchInputToolTip => string.IsNullOrWhiteSpace(SearchText) ? null : SearchPlaceholderText;

    public bool IsSearchPosterLayout
    {
        get => _isSearchPosterLayout;
        private set
        {
            if (SetProperty(ref _isSearchPosterLayout, value))
            {
                OnPropertyChanged(nameof(IsSearchListLayout));
                if (_isDiscoveryPreferencesLoaded)
                {
                    _ = SaveDiscoveryPreferencesAsync();
                }
            }
        }
    }

    public bool IsSearchListLayout => !IsSearchPosterLayout;

    public bool CanClearSearchFilters => IsTvSearchSelected
        ? HasExpandedTvSearchCriteria() || !string.Equals(SelectedSearchType, SearchTypeTvTitle, StringComparison.Ordinal)
        : HasExpandedSearchCriteria() || !string.Equals(SelectedSearchType, SearchTypeMovieTitle, StringComparison.Ordinal);

    public double SearchMoviePosterScrollOffset
    {
        get => _searchMoviePosterScrollOffset;
        set => SetProperty(ref _searchMoviePosterScrollOffset, Math.Max(0d, value));
    }

    public double SearchMovieListScrollOffset
    {
        get => _searchMovieListScrollOffset;
        set => SetProperty(ref _searchMovieListScrollOffset, Math.Max(0d, value));
    }

    public double SearchTvPosterScrollOffset
    {
        get => _searchTvPosterScrollOffset;
        set => SetProperty(ref _searchTvPosterScrollOffset, Math.Max(0d, value));
    }

    public double SearchTvListScrollOffset
    {
        get => _searchTvListScrollOffset;
        set => SetProperty(ref _searchTvListScrollOffset, Math.Max(0d, value));
    }

    public double RankingMovieScrollOffset
    {
        get => _rankingMovieScrollOffset;
        set => SetProperty(ref _rankingMovieScrollOffset, Math.Max(0d, value));
    }

    public double RankingTvScrollOffset
    {
        get => _rankingTvScrollOffset;
        set => SetProperty(ref _rankingTvScrollOffset, Math.Max(0d, value));
    }

    public string SearchMediaTypeButtonText => BuildFilterButtonText("影视", SelectedSearchMediaType);

    public string SearchTypeButtonText => BuildFilterButtonText(
        "搜索方式",
        IsSearchPersonSelected ? SearchTypePerson : GetTitleSearchTypeForActiveMedia());

    public string SearchGenreFilterButtonText => BuildFilterButtonText("类型", SelectedGenreFilter);

    public string SearchRegionFilterButtonText => BuildFilterButtonText("地区", SelectedRegionFilter);

    public string SearchWatchStatusFilterButtonText => BuildFilterButtonText("观看状态", SelectedWatchStatusFilter);

    public string SearchPlaybackSourceFilterButtonText => BuildFilterButtonText("播放源", SelectedPlaybackSourceFilter);

    public string SearchLibraryStatusFilterButtonText => BuildFilterButtonText("入库状态", SelectedLibraryStatusFilter);

    public string SearchCollectionStatusFilterButtonText => BuildFilterButtonText("收藏状态", SelectedCollectionStatusFilter);

    public string SearchSortDirectionButtonToolTip => BuildFilterButtonText("顺序", ActiveSearchSortDirection);

    public string SearchSortDirectionIconData => string.Equals(ActiveSearchSortDirection, DirectionAscending, StringComparison.Ordinal)
        ? "M 8 18 L 8 6 M 4 10 L 8 6 L 12 10 M 15 7 H 23 M 15 12 H 21 M 15 17 H 19"
        : "M 8 6 L 8 18 M 4 14 L 8 18 L 12 14 M 15 7 H 19 M 15 12 H 21 M 15 17 H 23";

    public IReadOnlyList<string> ActiveSearchSortOptions => IsTvSearchSelected ? TvSortOptions : SortOptions;

    public string ActiveSelectedSearchSortOption => IsTvSearchSelected ? SelectedTvSortOption : SelectedSortOption;

    public string SearchSortOptionButtonText => BuildFilterButtonText(
        "排序",
        IsTvSearchSelected ? SelectedTvSortOption : SelectedSortOption);

    public string SearchDecadeFilterButtonText => BuildFilterButtonText("年代", SelectedDecadeFilter);

    public string SearchLanguageFilterButtonText => BuildFilterButtonText("语言", SelectedLanguageFilter);

    public string TvSearchGenreFilterButtonText => BuildFilterButtonText("类型", SelectedTvGenreFilter);

    public string TvSearchRegionFilterButtonText => BuildFilterButtonText("地区", SelectedTvRegionFilter);

    public string TvSearchWatchStatusFilterButtonText => BuildFilterButtonText("观看状态", SelectedTvWatchStatusFilter);

    public string TvSearchPlaybackSourceFilterButtonText => BuildFilterButtonText("播放源", SelectedTvPlaybackSourceFilter);

    public string TvSearchLibraryStatusFilterButtonText => BuildFilterButtonText("入库状态", SelectedTvLibraryStatusFilter);

    public string TvSearchCollectionStatusFilterButtonText => BuildFilterButtonText("收藏状态", SelectedTvCollectionStatusFilter);

    public string TvSearchSortDirectionButtonText => BuildFilterButtonText("顺序", SelectedTvSortDirection);

    public string TvSearchSortOptionButtonText => BuildFilterButtonText("排序", SelectedTvSortOption);

    public string TvSearchDecadeFilterButtonText => BuildFilterButtonText("年代", SelectedTvDecadeFilter);

    public string TvSearchLanguageFilterButtonText => BuildFilterButtonText("语言", SelectedTvLanguageFilter);

    public string SelectedTvGenreFilter
    {
        get => _selectedTvGenreFilter;
        set
        {
            if (SetProperty(ref _selectedTvGenreFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchGenreFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvRegionFilter
    {
        get => _selectedTvRegionFilter;
        set
        {
            if (SetProperty(ref _selectedTvRegionFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchRegionFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvWatchStatusFilter
    {
        get => _selectedTvWatchStatusFilter;
        set
        {
            if (SetProperty(ref _selectedTvWatchStatusFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchWatchStatusFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvPlaybackSourceFilter
    {
        get => _selectedTvPlaybackSourceFilter;
        set
        {
            if (SetProperty(ref _selectedTvPlaybackSourceFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchPlaybackSourceFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvLibraryStatusFilter
    {
        get => _selectedTvLibraryStatusFilter;
        set
        {
            if (SetProperty(ref _selectedTvLibraryStatusFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchLibraryStatusFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvCollectionStatusFilter
    {
        get => _selectedTvCollectionStatusFilter;
        private set
        {
            if (SetProperty(ref _selectedTvCollectionStatusFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchCollectionStatusFilterButtonText));
            }
        }
    }

    public string SelectedTvSortOption
    {
        get => _selectedTvSortOption;
        set
        {
            if (SetProperty(ref _selectedTvSortOption, value))
            {
                OnPropertyChanged(nameof(TvSearchSortOptionButtonText));
                OnPropertyChanged(nameof(SearchSortOptionButtonText));
                OnPropertyChanged(nameof(ActiveSelectedSearchSortOption));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvSortDirection
    {
        get => _selectedTvSortDirection;
        set
        {
            if (SetProperty(ref _selectedTvSortDirection, value))
            {
                OnPropertyChanged(nameof(TvSearchSortDirectionButtonText));
                OnPropertyChanged(nameof(ActiveSearchSortDirection));
                OnPropertyChanged(nameof(SearchSortDirectionIconData));
                OnPropertyChanged(nameof(SearchSortDirectionButtonToolTip));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvDecadeFilter
    {
        get => _selectedTvDecadeFilter;
        set
        {
            if (SetProperty(ref _selectedTvDecadeFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchDecadeFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedTvLanguageFilter
    {
        get => _selectedTvLanguageFilter;
        set
        {
            if (SetProperty(ref _selectedTvLanguageFilter, value))
            {
                OnPropertyChanged(nameof(TvSearchLanguageFilterButtonText));
                ResetTvSearchFromFilterChange();
            }
        }
    }

    public string SelectedSearchType
    {
        get => _selectedSearchType;
        set
        {
            if (SetProperty(ref _selectedSearchType, value))
            {
                OnPropertyChanged(nameof(SearchTypeButtonText));
                RefreshSearchTypeProperties();
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    RefreshSearchFilterCommandState();
                    return;
                }

                if (IsTvSearchSelected)
                {
                    ResetTvSearchFromFilterChange();
                }
                else
                {
                    ResetSearchFromFilterChange();
                }
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
                OnPropertyChanged(nameof(SearchGenreFilterButtonText));
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
                OnPropertyChanged(nameof(SearchRegionFilterButtonText));
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
                OnPropertyChanged(nameof(SearchWatchStatusFilterButtonText));
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedPlaybackSourceFilter
    {
        get => _selectedPlaybackSourceFilter;
        set
        {
            if (SetProperty(ref _selectedPlaybackSourceFilter, value))
            {
                OnPropertyChanged(nameof(SearchPlaybackSourceFilterButtonText));
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedLibraryStatusFilter
    {
        get => _selectedLibraryStatusFilter;
        set
        {
            if (SetProperty(ref _selectedLibraryStatusFilter, value))
            {
                OnPropertyChanged(nameof(SearchLibraryStatusFilterButtonText));
                ResetSearchFromFilterChange();
            }
        }
    }

    public string SelectedCollectionStatusFilter
    {
        get => _selectedCollectionStatusFilter;
        private set
        {
            if (SetProperty(ref _selectedCollectionStatusFilter, value))
            {
                OnPropertyChanged(nameof(SearchCollectionStatusFilterButtonText));
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
                OnPropertyChanged(nameof(SearchSortOptionButtonText));
                OnPropertyChanged(nameof(ActiveSelectedSearchSortOption));
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
                OnPropertyChanged(nameof(ActiveSearchSortDirection));
                OnPropertyChanged(nameof(SearchSortDirectionIconData));
                OnPropertyChanged(nameof(SearchSortDirectionButtonToolTip));
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
                OnPropertyChanged(nameof(SearchDecadeFilterButtonText));
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
                OnPropertyChanged(nameof(SearchLanguageFilterButtonText));
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
                OnPropertyChanged(nameof(ActiveSearchStatusMessage));
                OnPropertyChanged(nameof(ActiveSearchStatusOverlayText));
            }
        }
    }

    public string SearchSummaryText
    {
        get => _searchSummaryText;
        private set
        {
            if (SetProperty(ref _searchSummaryText, value))
            {
                OnPropertyChanged(nameof(ActiveSearchSummaryText));
            }
        }
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
                OnPropertyChanged(nameof(IsActiveSearchLoading));
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
                OnPropertyChanged(nameof(ActiveSearchPageStatusText));
                OnPropertyChanged(nameof(CanGoPreviousSearchPage));
                OnPropertyChanged(nameof(CanGoPreviousActiveSearchPage));
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
                OnPropertyChanged(nameof(ActiveSearchPageStatusText));
            }
        }
    }

    public bool HasSearchMovies => SearchMovies.Count > 0;

    public bool ShowSearchEmptyState => !HasSearchMovies && !IsSearchLoading && !_searchLoadFailed;

    public bool ShowSearchStatusOverlay => IsSearchLoading || _searchLoadFailed || ShowSearchEmptyState;

    public string SearchStatusOverlayText => SearchStatusMessage;

    public bool CanGoPreviousSearchPage => !IsSearchLoading && SearchPageIndex > 1;

    public bool CanGoNextSearchPage => !IsSearchLoading && _canGoToNextSearchPage;

    public string SearchPageStatusText => SearchTotalPages <= 0
        ? "第 0 / 0 页"
        : $"第 {SearchPageIndex} / {SearchTotalPages} 页";

    public string TvSearchStatusMessage
    {
        get => _tvSearchStatusMessage;
        private set
        {
            if (SetProperty(ref _tvSearchStatusMessage, value))
            {
                OnPropertyChanged(nameof(ActiveSearchStatusMessage));
                OnPropertyChanged(nameof(ActiveSearchStatusOverlayText));
            }
        }
    }

    public string TvSearchSummaryText
    {
        get => _tvSearchSummaryText;
        private set
        {
            if (SetProperty(ref _tvSearchSummaryText, value))
            {
                OnPropertyChanged(nameof(ActiveSearchSummaryText));
            }
        }
    }

    public bool IsTvSearchLoading
    {
        get => _isTvSearchLoading;
        private set
        {
            if (SetProperty(ref _isTvSearchLoading, value))
            {
                RefreshTvSearchVisibility();
                RefreshSearchCommandState();
                OnPropertyChanged(nameof(IsActiveSearchLoading));
            }
        }
    }

    public bool IsTvSeriesNavigating
    {
        get => _isTvSeriesNavigating;
        private set
        {
            if (SetProperty(ref _isTvSeriesNavigating, value))
            {
                RefreshTvSearchVisibility();
                RefreshSearchCommandState();
                RefreshTvRankingVisibility();
                RefreshRankingCommandState();
                OnPropertyChanged(nameof(CanOpenTvSeries));
                OpenTvSeriesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanOpenTvSeries => !IsTvSeriesNavigating;

    public int TvSearchPageIndex
    {
        get => _tvSearchPageIndex;
        private set
        {
            if (SetProperty(ref _tvSearchPageIndex, value))
            {
                OnPropertyChanged(nameof(TvSearchPageStatusText));
                OnPropertyChanged(nameof(ActiveSearchPageStatusText));
                OnPropertyChanged(nameof(CanGoPreviousTvSearchPage));
                OnPropertyChanged(nameof(CanGoPreviousActiveSearchPage));
            }
        }
    }

    public int TvSearchTotalPages
    {
        get => _tvSearchTotalPages;
        private set
        {
            if (SetProperty(ref _tvSearchTotalPages, value))
            {
                OnPropertyChanged(nameof(TvSearchPageStatusText));
                OnPropertyChanged(nameof(ActiveSearchPageStatusText));
            }
        }
    }

    public bool HasSearchTvSeries => SearchTvSeries.Count > 0;

    public bool ShowTvSearchEmptyState => !HasSearchTvSeries && !IsTvSearchLoading && !_tvSearchLoadFailed;

    public bool ShowTvSearchStatusOverlay => IsTvSearchLoading || _tvSearchLoadFailed || ShowTvSearchEmptyState;

    public bool IsActiveSearchLoading => IsTvSearchSelected ? IsTvSearchLoading : IsSearchLoading;

    public string ActiveSearchStatusMessage => IsTvSearchSelected ? TvSearchStatusMessage : SearchStatusMessage;

    public string ActiveSearchSummaryText => IsTvSearchSelected ? TvSearchSummaryText : SearchSummaryText;

    public bool ShowActiveSearchStatusOverlay => IsTvSearchSelected ? ShowTvSearchStatusOverlay : ShowSearchStatusOverlay;

    public string ActiveSearchStatusOverlayText => ActiveSearchStatusMessage;

    public bool ShowActiveSearchRetry => IsTvSearchSelected ? _tvSearchLoadFailed : _searchLoadFailed;

    public bool CanRetryActiveSearch => ShowActiveSearchRetry && !IsActiveSearchLoading && !IsTvSeriesNavigating;

    public bool CanGoPreviousTvSearchPage => !IsTvSearchLoading && !IsTvSeriesNavigating && TvSearchPageIndex > 1;

    public bool CanGoNextTvSearchPage => !IsTvSearchLoading && !IsTvSeriesNavigating && _canGoToNextTvSearchPage;

    public bool CanGoPreviousActiveSearchPage => IsTvSearchSelected ? CanGoPreviousTvSearchPage : CanGoPreviousSearchPage;

    public bool CanGoNextActiveSearchPage => IsTvSearchSelected ? CanGoNextTvSearchPage : CanGoNextSearchPage;

    public string TvSearchPageStatusText => TvSearchTotalPages <= 0
        ? "第 0 / 0 页"
        : $"第 {TvSearchPageIndex} / {TvSearchTotalPages} 页";

    public string ActiveSearchPageStatusText => IsTvSearchSelected ? TvSearchPageStatusText : SearchPageStatusText;

    public string ActiveSearchSortDirection => IsTvSearchSelected ? SelectedTvSortDirection : SelectedSortDirection;

    public string SelectedRankingMediaType
    {
        get => _selectedRankingMediaType;
        set
        {
            if (SetProperty(ref _selectedRankingMediaType, value))
            {
                RefreshRankingModeProperties();
                if (!_suppressRankingPresetApply
                    && SelectedTabIndex == RankingTabIndex
                    && _hasActivatedRankings)
                {
                    _ = ResetAndLoadRankingsAsync();
                }
            }
        }
    }

    public bool IsMovieRankingSelected => string.Equals(SelectedRankingMediaType, DiscoveryMediaTypeMovie, StringComparison.Ordinal);

    public bool IsTvRankingSelected => string.Equals(SelectedRankingMediaType, DiscoveryMediaTypeTv, StringComparison.Ordinal);

    public DiscoveryTvSeriesCardViewModel? TopRankingTvSeries
    {
        get => _topRankingTvSeries;
        private set
        {
            if (SetProperty(ref _topRankingTvSeries, value))
            {
                OnPropertyChanged(nameof(ShowTopRankingTvSeries));
                OnPropertyChanged(nameof(HasRankingTvSeries));
            }
        }
    }

    public bool ShowTopRankingTvSeries => TopRankingTvSeries is not null;

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
                OnPropertyChanged(nameof(ActiveRankingTitle));
                OnPropertyChanged(nameof(ActiveRankingTimeText));
                OnPropertyChanged(nameof(IsActiveTrendingRanking));
                OnPropertyChanged(nameof(IsActiveRankingTimeSelectable));
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
                OnPropertyChanged(nameof(ActiveRankingTimeText));
            }
        }
    }

    public string SelectedTvRankingType
    {
        get => _selectedTvRankingType;
        private set
        {
            if (SetProperty(ref _selectedTvRankingType, value))
            {
                OnPropertyChanged(nameof(TvRankingTitle));
                OnPropertyChanged(nameof(TvRankingTimeText));
                OnPropertyChanged(nameof(IsTvTrendingRanking));
                OnPropertyChanged(nameof(IsTvRankingTimeSelectable));
                OnPropertyChanged(nameof(ActiveRankingTitle));
                OnPropertyChanged(nameof(ActiveRankingTimeText));
                OnPropertyChanged(nameof(IsActiveTrendingRanking));
                OnPropertyChanged(nameof(IsActiveRankingTimeSelectable));
                SelectTrendingTimeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedTvTrendingTime
    {
        get => _selectedTvTrendingTime;
        private set
        {
            if (SetProperty(ref _selectedTvTrendingTime, value))
            {
                OnPropertyChanged(nameof(TvRankingTimeText));
                OnPropertyChanged(nameof(ActiveRankingTimeText));
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

    public string TvRankingTitle => SelectedTvRankingType;

    public string TvRankingTimeText => SelectedTvRankingType switch
    {
        RankingTypePopular => "当前热门",
        RankingTypeTopRated => "历史高分",
        _ => SelectedTvTrendingTime
    };

    public bool IsTvTrendingRanking => string.Equals(SelectedTvRankingType, RankingTypeTrending, StringComparison.Ordinal);

    public bool IsTvRankingTimeSelectable => IsTvTrendingRanking && !IsTvRankingLoading && !IsTvSeriesNavigating;

    public string ActiveRankingTitle => IsTvRankingSelected ? TvRankingTitle : RankingTitle;

    public string ActiveRankingTimeText => IsTvRankingSelected ? TvRankingTimeText : RankingTimeText;

    public bool IsActiveTrendingRanking => IsTvRankingSelected ? IsTvTrendingRanking : IsTrendingRanking;

    public bool IsActiveRankingTimeSelectable => IsTvRankingSelected ? IsTvRankingTimeSelectable : IsRankingTimeSelectable;

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
                OnPropertyChanged(nameof(ActiveRankingStatusMessage));
                OnPropertyChanged(nameof(ActiveRankingStatusOverlayText));
            }
        }
    }

    public string RankingSummaryText
    {
        get => _rankingSummaryText;
        private set
        {
            if (SetProperty(ref _rankingSummaryText, value))
            {
                OnPropertyChanged(nameof(ActiveRankingSummaryText));
            }
        }
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
                OnPropertyChanged(nameof(IsActiveRankingLoading));
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
                OnPropertyChanged(nameof(ActiveRankingPageStatusText));
                OnPropertyChanged(nameof(CanGoPreviousRankingPage));
                OnPropertyChanged(nameof(CanGoPreviousActiveRankingPage));
                OnPropertyChanged(nameof(CanGoNextRankingPage));
                OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
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
                OnPropertyChanged(nameof(ActiveRankingPageStatusText));
                OnPropertyChanged(nameof(CanGoNextRankingPage));
                OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
            }
        }
    }

    public bool HasRankingMovies => TopRankingMovie is not null || RankingRows.Count > 0;

    public bool ShowRankingEmptyState => !HasRankingMovies && !IsRankingLoading && !_rankingLoadFailed;

    public bool ShowRankingStatusOverlay => IsRankingLoading || _rankingLoadFailed || ShowRankingEmptyState;

    public string RankingStatusOverlayText => RankingStatusMessage;

    public bool CanGoPreviousRankingPage => !IsRankingLoading && RankingPageIndex > 1;

    public bool CanGoNextRankingPage => !IsRankingLoading && _canGoToNextRankingPage;

    public string RankingPageStatusText => $"第 {RankingPageIndex} / {RankingTotalDisplayPages} 页";

    public string TvRankingStatusMessage
    {
        get => _tvRankingStatusMessage;
        private set
        {
            if (SetProperty(ref _tvRankingStatusMessage, value))
            {
                OnPropertyChanged(nameof(ActiveRankingStatusMessage));
                OnPropertyChanged(nameof(ActiveRankingStatusOverlayText));
            }
        }
    }

    public string TvRankingSummaryText
    {
        get => _tvRankingSummaryText;
        private set
        {
            if (SetProperty(ref _tvRankingSummaryText, value))
            {
                OnPropertyChanged(nameof(ActiveRankingSummaryText));
            }
        }
    }

    public bool IsTvRankingLoading
    {
        get => _isTvRankingLoading;
        private set
        {
            if (SetProperty(ref _isTvRankingLoading, value))
            {
                RefreshTvRankingVisibility();
                RefreshRankingCommandState();
                OnPropertyChanged(nameof(IsActiveRankingLoading));
            }
        }
    }

    public int TvRankingPageIndex
    {
        get => _tvRankingPageIndex;
        private set
        {
            if (SetProperty(ref _tvRankingPageIndex, value))
            {
                OnPropertyChanged(nameof(TvRankingPageStatusText));
                OnPropertyChanged(nameof(ActiveRankingPageStatusText));
                OnPropertyChanged(nameof(CanGoPreviousTvRankingPage));
                OnPropertyChanged(nameof(CanGoPreviousActiveRankingPage));
                OnPropertyChanged(nameof(CanGoNextTvRankingPage));
                OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
            }
        }
    }

    public int TvRankingTotalDisplayPages
    {
        get => _tvRankingTotalDisplayPages;
        private set
        {
            if (SetProperty(ref _tvRankingTotalDisplayPages, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(TvRankingPageStatusText));
                OnPropertyChanged(nameof(ActiveRankingPageStatusText));
                OnPropertyChanged(nameof(CanGoNextTvRankingPage));
                OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
            }
        }
    }

    public bool HasRankingTvSeries => TopRankingTvSeries is not null || RankingTvRows.Count > 0;

    public bool ShowTvRankingEmptyState => !HasRankingTvSeries && !IsTvRankingLoading && !_tvRankingLoadFailed;

    public bool ShowTvRankingStatusOverlay => IsTvRankingLoading || IsTvSeriesNavigating || _tvRankingLoadFailed || ShowTvRankingEmptyState;

    public bool IsActiveRankingLoading => IsTvRankingSelected ? IsTvRankingLoading : IsRankingLoading;

    public string ActiveRankingStatusMessage => IsTvRankingSelected ? TvRankingStatusMessage : RankingStatusMessage;

    public string ActiveRankingSummaryText => IsTvRankingSelected ? TvRankingSummaryText : RankingSummaryText;

    public bool ShowActiveRankingStatusOverlay => IsTvRankingSelected ? ShowTvRankingStatusOverlay : ShowRankingStatusOverlay;

    public string ActiveRankingStatusOverlayText => ActiveRankingStatusMessage;

    public bool ShowActiveRankingRetry => IsTvRankingSelected ? _tvRankingLoadFailed : _rankingLoadFailed;

    public bool CanRetryActiveRanking => ShowActiveRankingRetry && !IsActiveRankingLoading && !IsTvSeriesNavigating;

    public bool CanGoPreviousTvRankingPage => !IsTvRankingLoading && !IsTvSeriesNavigating && TvRankingPageIndex > 1;

    public bool CanGoNextTvRankingPage => !IsTvRankingLoading && !IsTvSeriesNavigating && _canGoToNextTvRankingPage;

    public bool CanGoPreviousActiveRankingPage => IsTvRankingSelected ? CanGoPreviousTvRankingPage : CanGoPreviousRankingPage;

    public bool CanGoNextActiveRankingPage => IsTvRankingSelected ? CanGoNextTvRankingPage : CanGoNextRankingPage;

    public string TvRankingPageStatusText => $"第 {TvRankingPageIndex} / {TvRankingTotalDisplayPages} 页";

    public string ActiveRankingPageStatusText => IsTvRankingSelected ? TvRankingPageStatusText : RankingPageStatusText;

    private bool CanUseActiveRankingPager => !IsActiveRankingLoading && !IsTvSeriesNavigating;

    public void OpenAiRecommendationsOnNextActivation()
    {
        _openAiRecommendationsOnNextActivation = true;
    }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        await EnsureDiscoveryPreferencesLoadedAsync(cancellationToken);

        if (_openAiRecommendationsOnNextActivation)
        {
            _openAiRecommendationsOnNextActivation = false;
            _hasActivatedDiscoveryPage = true;
            SelectedTabIndex = AiRecommendationTabIndex;
            _ = EnsureAiRecommendationsActivatedAsync();
            RequestCachedMovieCardRefresh();
            return;
        }

        if (!_hasActivatedDiscoveryPage)
        {
            _hasActivatedDiscoveryPage = true;
            SelectedTabIndex = SearchTabIndex;
            RequestCachedMovieCardRefresh();
            return;
        }

        if (SelectedTabIndex == RankingTabIndex)
        {
            _ = EnsureRankingsActivatedAsync();
        }
        else if (SelectedTabIndex == AiRecommendationTabIndex)
        {
            _ = EnsureAiRecommendationsActivatedAsync();
        }

        RequestCachedMovieCardRefresh();
    }

    public override void Deactivate()
    {
        _isActive = false;
        _searchCancellationTokenSource?.Cancel();
        _rankingCancellationTokenSource?.Cancel();
        _hasActivatedAiRecommendations = false;
        _aiRecommendationViewModel.Deactivate();
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (!ShouldRefreshMovieCardsForDataChange(e))
        {
            return;
        }

        RequestCachedMovieCardRefresh();
    }

    private static bool ShouldRefreshMovieCardsForDataChange(AppDataChangedEventArgs e)
    {
        return e.LibraryChanged
               || e.Reason is AppDataChangeReason.MetadataChanged
                   or AppDataChangeReason.CollectionChanged;
    }

    private void RefreshPendingLocalMovieCardsIfNeeded()
    {
        if (!_pendingLocalMovieCardRefresh)
        {
            return;
        }

        _ = RefreshCachedMovieCardsFromLocalDetailsAsync();
    }

    private void RequestCachedMovieCardRefresh()
    {
        _pendingLocalMovieCardRefresh = true;
        if (!_isActive)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RefreshPendingLocalMovieCardsIfNeeded();
            return;
        }

        _ = dispatcher.InvokeAsync(RefreshPendingLocalMovieCardsIfNeeded);
    }

    private void SetSearchLayout(bool isPosterLayout)
    {
        if (IsSearchPosterLayout == isPosterLayout)
        {
            return;
        }

        IsSearchPosterLayout = isPosterLayout;
        var message = IsSearchPosterLayout ? "已切换为海报布局。" : "已切换为列表布局。";
        if (IsTvSearchSelected)
        {
            TvSearchStatusMessage = message;
        }
        else
        {
            SearchStatusMessage = message;
        }
    }

    private void ClearSearchText()
    {
        SearchText = string.Empty;
    }

    private void ToggleActiveSearchSortDirection()
    {
        if (IsTvSearchSelected)
        {
            SelectedTvSortDirection = ToggleSortDirectionValue(SelectedTvSortDirection);
            return;
        }

        SelectedSortDirection = ToggleSortDirectionValue(SelectedSortDirection);
    }

    private void SelectActiveSearchSortOption(object? parameter)
    {
        if (IsTvSearchSelected)
        {
            SelectedTvSortOption = GetOptionValue(parameter, SortRelevance);
            return;
        }

        SelectedSortOption = GetOptionValue(parameter, SortRelevance);
    }

    private void SelectDiscoveryTab(object? parameter)
    {
        if (!int.TryParse(parameter?.ToString(), out var tabIndex)
            || tabIndex < SearchTabIndex
            || tabIndex > AiRecommendationTabIndex)
        {
            return;
        }

        SelectedTabIndex = tabIndex;
    }

    private void SelectSearchMediaType(object? parameter)
    {
        var mediaType = GetOptionValue(parameter, DiscoveryMediaTypeMovie);
        if (!SearchMediaTypeOptions.Contains(mediaType, StringComparer.Ordinal)
            || string.Equals(mediaType, SelectedSearchMediaType, StringComparison.Ordinal))
        {
            return;
        }

        SelectedSearchMediaType = mediaType;
    }

    private void SelectSearchType(object? parameter)
    {
        var searchType = GetOptionValue(parameter, GetTitleSearchTypeForActiveMedia());
        if (!SearchTypeOptions.Contains(searchType, StringComparer.Ordinal)
            || string.Equals(searchType, SelectedSearchType, StringComparison.Ordinal))
        {
            return;
        }

        SelectedSearchType = searchType;
    }

    private void SelectSearchGenreFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            GenreFilterOptions,
            _selectedGenreFilters,
            SearchGenreFilterMenuOptions,
            RefreshSearchGenreFilterState);
    }

    private void SelectSearchRegionMultiFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            RegionFilterOptions,
            _selectedRegionFilters,
            SearchRegionFilterMenuOptions,
            RefreshSearchRegionFilterState);
    }

    private void SelectSearchLanguageMultiFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            LanguageFilterOptions,
            _selectedLanguageFilters,
            SearchLanguageFilterMenuOptions,
            RefreshSearchLanguageFilterState);
    }

    private void SelectSearchDecadeFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, DecadeAll),
            DecadeAll,
            DecadeFilterOptions,
            _selectedDecadeFilters,
            SearchDecadeFilterMenuOptions,
            RefreshSearchDecadeFilterState);
    }

    private void SelectSearchCollectionStatusFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            CollectionStatusFilterOptions,
            _selectedCollectionStatusFilters,
            SearchCollectionStatusFilterMenuOptions,
            RefreshSearchCollectionStatusFilterState);
    }

    private void SelectTvSearchGenreFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            TvGenreFilterOptions,
            _selectedTvGenreFilters,
            TvSearchGenreFilterMenuOptions,
            RefreshTvSearchGenreFilterState);
    }

    private void SelectTvSearchRegionMultiFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            RegionFilterOptions,
            _selectedTvRegionFilters,
            TvSearchRegionFilterMenuOptions,
            RefreshTvSearchRegionFilterState);
    }

    private void SelectTvSearchLanguageMultiFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            LanguageFilterOptions,
            _selectedTvLanguageFilters,
            TvSearchLanguageFilterMenuOptions,
            RefreshTvSearchLanguageFilterState);
    }

    private void SelectTvSearchDecadeFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, DecadeAll),
            DecadeAll,
            DecadeFilterOptions,
            _selectedTvDecadeFilters,
            TvSearchDecadeFilterMenuOptions,
            RefreshTvSearchDecadeFilterState);
    }

    private void SelectTvSearchCollectionStatusFilter(object? parameter)
    {
        UpdateDiscoveryMultiSelectFilter(
            GetOptionValue(parameter, FilterAll),
            FilterAll,
            CollectionStatusFilterOptions,
            _selectedTvCollectionStatusFilters,
            TvSearchCollectionStatusFilterMenuOptions,
            RefreshTvSearchCollectionStatusFilterState);
    }

    private async Task SearchAsync()
    {
        if (IsTvSearchSelected)
        {
            await SearchTvSeriesAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText) && !CanUseMovieDiscoverSearch())
        {
            ResetMovieSearchToMissingKeyword();
            return;
        }

        await ResetAndLoadSearchDisplayPageAsync(1);
    }

    private void ResetMovieSearchToMissingKeyword()
    {
        ResetSearchBuffers();
        SearchMovies.Clear();
        ResetMovieSearchScrollOffsets();
        _searchLoadFailed = false;
        SearchPageIndex = 1;
        SearchTotalPages = 0;
        _canGoToNextSearchPage = false;
        SearchStatusMessage = BuildMissingSearchKeywordMessage();
        SearchSummaryText = string.Empty;
        RefreshSearchVisibility();
        RefreshSearchCommandState();
    }

    private async Task GoPreviousSearchPageAsync()
    {
        if (IsTvSearchSelected)
        {
            if (!CanGoPreviousTvSearchPage)
            {
                return;
            }

            await LoadTvSearchDisplayPageAsync(TvSearchPageIndex - 1);
            return;
        }

        if (!CanGoPreviousSearchPage)
        {
            return;
        }

        await LoadSearchDisplayPageAsync(SearchPageIndex - 1);
    }

    private async Task GoNextSearchPageAsync()
    {
        if (IsTvSearchSelected)
        {
            if (!CanGoNextTvSearchPage)
            {
                return;
            }

            await LoadTvSearchDisplayPageAsync(TvSearchPageIndex + 1);
            return;
        }

        if (!CanGoNextSearchPage)
        {
            return;
        }

        await LoadSearchDisplayPageAsync(SearchPageIndex + 1);
    }

    private async Task RetryActiveSearchAsync()
    {
        if (!CanRetryActiveSearch)
        {
            return;
        }

        if (IsTvSearchSelected)
        {
            await LoadTvSearchDisplayPageAsync(Math.Max(1, _lastTvSearchDisplayPage));
            return;
        }

        await LoadSearchDisplayPageAsync(Math.Max(1, _lastSearchDisplayPage));
    }

    private async Task SearchTvSeriesAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText) && !CanUseTvDiscoverSearch())
        {
            ResetTvSearchToMissingKeyword();
            return;
        }

        await ResetAndLoadTvSearchDisplayPageAsync(1);
    }

    private void ResetTvSearchToMissingKeyword()
    {
        ResetTvSearchBuffers();
        SearchTvSeries.Clear();
        ResetTvSearchScrollOffsets();
        _tvSearchLoadFailed = false;
        TvSearchPageIndex = 1;
        TvSearchTotalPages = 0;
        _canGoToNextTvSearchPage = false;
        TvSearchStatusMessage = BuildMissingSearchKeywordMessage();
        TvSearchSummaryText = string.Empty;
        RefreshTvSearchVisibility();
        RefreshSearchCommandState();
    }

    private async Task ResetAndLoadTvSearchDisplayPageAsync(int displayPage)
    {
        InvalidateTvSeriesOpenRequest();
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();
        ResetTvSearchBuffers();
        ResetTvSearchScrollOffsets();
        TvSearchPageIndex = 1;
        TvSearchTotalPages = 0;
        SearchTvSeries.Clear();
        TvSearchSummaryText = string.Empty;
        _tvSearchLoadFailed = false;
        RefreshTvSearchVisibility();

        await LoadTvSearchDisplayPageCoreAsync(displayPage, _searchCancellationTokenSource.Token);
    }

    private async Task LoadTvSearchDisplayPageAsync(int displayPage)
    {
        if (IsTvSeriesNavigating || displayPage < 1 || !CanLoadTvSearchSource())
        {
            return;
        }

        InvalidateTvSeriesOpenRequest();
        _searchCancellationTokenSource = EnsureActiveCancellationTokenSource(_searchCancellationTokenSource);
        await LoadTvSearchDisplayPageCoreAsync(displayPage, _searchCancellationTokenSource.Token);
    }

    private async Task LoadTvSearchDisplayPageCoreAsync(
        int displayPage,
        CancellationToken cancellationToken)
    {
        var rollbackState = CaptureTvSearchPageState();
        var requestVersion = ++_tvSearchRequestVersion;
        _lastTvSearchDisplayPage = displayPage;
        _tvSearchLoadFailed = false;
        IsTvSearchLoading = true;
        TvSearchStatusMessage = displayPage <= 1 ? BuildTvSearchLoadingMessage() : $"正在加载第 {displayPage} 页...";
        SearchTvSeries.Clear();
        TvSearchSummaryText = string.Empty;
        RefreshTvSearchVisibility();

        try
        {
            await EnsureTvSearchPoolForDisplayPageWithRetryAsync(displayPage, requestVersion, cancellationToken);
            if (requestVersion != _tvSearchRequestVersion)
            {
                return;
            }

            var filteredCount = BuildFilteredTvSearchSeries().Count;
            if (displayPage > 1
                && filteredCount <= (displayPage - 1) * SearchDisplayPageSize
                && !CanFetchNextTvSearchSourcePage(GetTvSearchSourcePageLimit(displayPage + 1)))
            {
                displayPage = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)SearchDisplayPageSize));
            }

            TvSearchPageIndex = displayPage;
            ResetTvSearchScrollOffsets();
            var visibleItems = RebuildTvSearchDisplay();
            if (SearchTvSeries.Count == 0)
            {
                TvSearchStatusMessage = _tvSearchResultPool.Count == 0
                    ? BuildTvSearchNoResultsMessage()
                    : "当前筛选条件下无电视剧结果。";
            }
            else
            {
                TvSearchStatusMessage = BuildTvSearchLoadedMessage();
                _ = EnrichTvSeriesDetailsAsync(visibleItems, requestVersion, isRankingRequest: false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (requestVersion == _tvSearchRequestVersion)
            {
                RestoreTvSearchPageState(rollbackState, restoreDisplay: true);
                TvSearchStatusMessage = BuildPageRollbackStatus("电视剧搜索加载已取消", rollbackState.PageIndex);
            }
        }
        catch (Exception exception)
        {
            RestoreTvSearchPageState(rollbackState, restoreDisplay: true);
            TvSearchStatusMessage = BuildPageRollbackStatus(
                $"第 {displayPage} 页加载失败：{DescribeException(exception)}",
                rollbackState.PageIndex);
            ShowPageLoadFailureToast(displayPage);
        }
        finally
        {
            IsTvSearchLoading = false;
            RefreshTvSearchVisibility();
            RefreshSearchCommandState();
        }
    }

    private async Task EnsureTvSearchPoolForDisplayPageAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var sourcePageLimit = GetTvSearchSourcePageLimit(displayPage);
        while (requestVersion == _tvSearchRequestVersion
               && !HasEnoughTvSearchResultsForDisplayPage(displayPage)
               && CanFetchNextTvSearchSourcePage(sourcePageLimit))
        {
            await FetchNextTvSearchSourcePageAsync(requestVersion, cancellationToken);
        }
    }

    private Task EnsureTvSearchPoolForDisplayPageWithRetryAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        return ExecutePageLoadWithRetryAsync(
            CaptureTvSearchPageState,
            state => RestoreTvSearchPageState(state, restoreDisplay: false),
            attemptToken => EnsureTvSearchPoolForDisplayPageAsync(displayPage, requestVersion, attemptToken),
            cancellationToken,
            RequiresCompleteTvSearchPool()
                ? SearchFilteredPageLoadAttemptTimeoutSeconds
                : PageLoadAttemptTimeoutSeconds);
    }

    private bool HasEnoughTvSearchResultsForDisplayPage(int displayPage)
    {
        var required = displayPage * SearchDisplayPageSize;
        if (!RequiresCompleteTvSearchPool())
        {
            return _tvSearchResultPool.Count >= required || _tvSearchSourceExhausted;
        }

        return !CanFetchNextTvSearchSourcePage(SearchFilteredMaxSourcePages);
    }

    private bool CanFetchNextTvSearchSourcePage(int sourcePageLimit)
    {
        if (_tvSearchSourceExhausted)
        {
            return false;
        }

        if (_tvSearchSourceTotalPages > 0 && _tvSearchSourceNextPage > _tvSearchSourceTotalPages)
        {
            return false;
        }

        return _tvSearchSourceNextPage <= Math.Max(1, sourcePageLimit);
    }

    private int GetTvSearchSourcePageLimit(int displayPage)
    {
        if (RequiresCompleteTvSearchPool())
        {
            return SearchFilteredMaxSourcePages;
        }

        var pageDrivenLimit = (int)Math.Ceiling(displayPage * SearchDisplayPageSize / (double)SearchTmdbPageSize);
        return CanUseTvDiscoverSearch()
            ? Math.Min(SearchDiscoverMaxSourcePages, pageDrivenLimit)
            : pageDrivenLimit;
    }

    private async Task FetchNextTvSearchSourcePageAsync(
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var page = _tvSearchSourceNextPage;
        if (!_tvSearchSourcePageCache.TryGetValue(page, out var response))
        {
            response = CanUseTvDiscoverSearch()
                ? await _tmdbService.DiscoverTvSeriesAsync(
                    page,
                    GetTvDiscoverSortBy(),
                    GetSelectedTvGenreIds(),
                    GetSelectedTvOriginCountryCodes(),
                    GetSelectedTvOriginalLanguage(),
                    GetSelectedTvFirstAirDateGte(),
                    GetSelectedTvFirstAirDateLte(),
                    cancellationToken: cancellationToken)
                : IsSearchPersonSelected
                ? await _tmdbService.SearchTvSeriesByPersonAsync(SearchText, page, cancellationToken: cancellationToken)
                : await _tmdbService.SearchTvSeriesAsync(SearchText, page, cancellationToken: cancellationToken);
            _tvSearchSourcePageCache[page] = response;
        }

        if (requestVersion != _tvSearchRequestVersion)
        {
            return;
        }

        _tvSearchSourceNextPage = page + 1;
        _tvSearchSourceTotalPages = response.TotalPages;
        _tvSearchTotalResults = response.TotalResults;
        if (response.TotalPages <= 0 || page >= response.TotalPages || response.Results.Count == 0)
        {
            _tvSearchSourceExhausted = true;
        }

        var pageItems = response.Results
            .Where(item => item.TmdbId > 0 && _tvSearchTmdbIds.Add(item.TmdbId))
            .Select(item => new DiscoveryTvSeriesCardViewModel(item, ++_nextTvSearchOrder))
            .ToList();

        if (pageItems.Count == 0)
        {
            return;
        }

        var statuses = await _tvStatusResolver.ResolveAsync(pageItems.Select(item => item.TmdbSeriesId), cancellationToken);
        foreach (var item in pageItems)
        {
            if (statuses.TryGetValue(item.TmdbSeriesId, out var status))
            {
                item.ApplyStatus(status);
            }
        }

        _tvSearchResultPool.AddRange(pageItems);
    }

    private IReadOnlyList<DiscoveryTvSeriesCardViewModel> RebuildTvSearchDisplay()
    {
        var filtered = BuildFilteredTvSearchSeries();
        var pageItems = filtered
            .Skip((TvSearchPageIndex - 1) * SearchDisplayPageSize)
            .Take(SearchDisplayPageSize)
            .ToList();

        SearchTvSeries.Clear();
        foreach (var item in pageItems)
        {
            SearchTvSeries.Add(item);
        }

        UpdateTvSearchPagination(filtered.Count);
        TvSearchSummaryText = BuildTvSearchSummaryText(filtered);
        RefreshTvSearchVisibility();
        RefreshSearchNonSubmitCommandState();
        return pageItems;
    }

    private void UpdateTvSearchPagination(int filteredCount)
    {
        var currentPageHasFullResult = filteredCount > TvSearchPageIndex * SearchDisplayPageSize;
        var canFetchMore = CanFetchNextTvSearchSourcePage(GetTvSearchSourcePageLimit(TvSearchPageIndex + 1));
        _canGoToNextTvSearchPage = currentPageHasFullResult || canFetchMore;

        var loadedPages = filteredCount == 0
            ? 0
            : (int)Math.Ceiling(filteredCount / (double)SearchDisplayPageSize);
        if (!RequiresCompleteTvSearchPool() && _tvSearchTotalResults > 0)
        {
            var availableResults = CanUseTvDiscoverSearch()
                ? Math.Min(_tvSearchTotalResults, SearchDiscoverMaxDisplayPages * SearchDisplayPageSize)
                : _tvSearchTotalResults;
            TvSearchTotalPages = Math.Max(1, (int)Math.Ceiling(availableResults / (double)SearchDisplayPageSize));
        }
        else if (_canGoToNextTvSearchPage)
        {
            TvSearchTotalPages = Math.Max(TvSearchPageIndex + 1, loadedPages);
        }
        else
        {
            TvSearchTotalPages = loadedPages;
        }

        OnPropertyChanged(nameof(CanGoNextTvSearchPage));
        OnPropertyChanged(nameof(CanGoNextActiveSearchPage));
        OnPropertyChanged(nameof(TvSearchPageStatusText));
        OnPropertyChanged(nameof(ActiveSearchPageStatusText));
    }

    private string BuildTvSearchSummaryText(IReadOnlyCollection<DiscoveryTvSeriesCardViewModel> filteredItems)
    {
        if (_tvSearchResultPool.Count == 0 && filteredItems.Count == 0)
        {
            return string.Empty;
        }

        var inLibraryCount = filteredItems.Count(item => item.IsVisibleInLibrary);
        return BuildSearchResultSummaryText(filteredItems.Count, inLibraryCount);
    }

    private List<DiscoveryTvSeriesCardViewModel> BuildFilteredTvSearchSeries()
    {
        var query = _tvSearchResultPool.AsEnumerable();

        if (_selectedTvGenreFilters.Count > 0)
        {
            query = query.Where(
                item => _selectedTvGenreFilters.Any(
                    filter => SplitTags(item.GenresText).Contains(filter, StringComparer.OrdinalIgnoreCase)));
        }

        if (_selectedTvRegionFilters.Count > 0)
        {
            query = query.Where(item => _selectedTvRegionFilters.Any(filter => MatchesRegion(item, filter)));
        }

        if (_selectedTvLanguageFilters.Count > 0)
        {
            query = query.Where(item => _selectedTvLanguageFilters.Any(filter => MatchesLanguage(item, filter)));
        }

        if (_selectedTvDecadeFilters.Count > 0)
        {
            query = query.Where(item => _selectedTvDecadeFilters.Any(filter => MatchesDecade(item, filter)));
        }

        query = SelectedTvPlaybackSourceFilter switch
        {
            PlaybackSourceWithSource => query.Where(item => item.IsInLibrary),
            PlaybackSourceWithoutSource => query.Where(item => !item.IsInLibrary),
            _ => query
        };

        query = SelectedTvLibraryStatusFilter switch
        {
            LibraryStatusInLibrary => query.Where(item => item.IsVisibleInLibrary),
            LibraryStatusNotInLibrary => query.Where(item => !item.IsVisibleInLibrary),
            _ => query
        };

        query = SelectedTvWatchStatusFilter switch
        {
            WatchStatusWatched => query.Where(item => item.IsWatched),
            WatchStatusUnwatched => query.Where(item => !item.IsWatched),
            _ => query
        };

        if (_selectedTvCollectionStatusFilters.Count > 0)
        {
            query = query.Where(
                item => _selectedTvCollectionStatusFilters.Any(filter => MatchesTvCollectionStatusFilter(item, filter)));
        }

        return ApplyTvSearchSorting(query).ToList();
    }

    private IEnumerable<DiscoveryTvSeriesCardViewModel> ApplyTvSearchSorting(IEnumerable<DiscoveryTvSeriesCardViewModel> query)
    {
        var descending = string.Equals(SelectedTvSortDirection, DirectionDescending, StringComparison.Ordinal);
        return SelectedTvSortOption switch
        {
            SortRating => descending
                ? query.OrderByDescending(item => item.RatingValue ?? -1d).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.RatingValue ?? -1d).ThenBy(item => item.SearchOrder),
            SortPopularity => descending
                ? query.OrderByDescending(item => item.Popularity ?? -1d).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.Popularity ?? -1d).ThenBy(item => item.SearchOrder),
            SortFirstAirYear => descending
                ? query.OrderByDescending(item => item.FirstAirYear ?? 0).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.FirstAirYear ?? 0).ThenBy(item => item.SearchOrder),
            SortSeriesName => descending
                ? query.OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.SearchOrder)
                : query.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.SearchOrder),
            _ => descending
                ? query.OrderBy(item => item.SearchOrder)
                : query.OrderByDescending(item => item.SearchOrder)
        };
    }

    private bool HasExpandedTvSearchCriteria()
    {
        return _selectedTvGenreFilters.Count > 0
               || _selectedTvRegionFilters.Count > 0
               || _selectedTvLanguageFilters.Count > 0
               || _selectedTvDecadeFilters.Count > 0
               || !string.Equals(SelectedTvWatchStatusFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedTvPlaybackSourceFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedTvLibraryStatusFilter, FilterAll, StringComparison.Ordinal)
               || !IsDefaultCollectionStatusFilter(_selectedTvCollectionStatusFilters)
               || !string.Equals(SelectedTvSortOption, SortRelevance, StringComparison.Ordinal)
               || !string.Equals(SelectedTvSortDirection, DirectionDescending, StringComparison.Ordinal);
    }

    private bool RequiresCompleteTvSearchPool()
    {
        return HasExpandedTvSearchCriteria() && !CanUseTvDiscoverSearch();
    }

    private bool CanLoadMovieSearchSource()
    {
        return !string.IsNullOrWhiteSpace(SearchText) || CanUseMovieDiscoverSearch();
    }

    private bool CanLoadTvSearchSource()
    {
        return !string.IsNullOrWhiteSpace(SearchText) || CanUseTvDiscoverSearch();
    }

    private bool CanUseMovieDiscoverSearch()
    {
        return string.IsNullOrWhiteSpace(SearchText)
               && !string.Equals(SelectedSortOption, SortRelevance, StringComparison.Ordinal);
    }

    private bool CanUseTvDiscoverSearch()
    {
        return string.IsNullOrWhiteSpace(SearchText)
               && !string.Equals(SelectedTvSortOption, SortRelevance, StringComparison.Ordinal);
    }

    private string GetMovieDiscoverSortBy()
    {
        var suffix = string.Equals(SelectedSortDirection, DirectionAscending, StringComparison.Ordinal)
            ? "asc"
            : "desc";

        return SelectedSortOption switch
        {
            SortRating => $"vote_average.{suffix}",
            SortPopularity => $"popularity.{suffix}",
            SortReleaseDate => $"primary_release_date.{suffix}",
            SortTitle => $"original_title.{suffix}",
            _ => $"popularity.{suffix}"
        };
    }

    private string GetTvDiscoverSortBy()
    {
        var suffix = string.Equals(SelectedTvSortDirection, DirectionAscending, StringComparison.Ordinal)
            ? "asc"
            : "desc";

        return SelectedTvSortOption switch
        {
            SortRating => $"vote_average.{suffix}",
            SortPopularity => $"popularity.{suffix}",
            SortFirstAirYear => $"first_air_date.{suffix}",
            SortSeriesName => $"name.{suffix}",
            _ => $"popularity.{suffix}"
        };
    }

    private IReadOnlyCollection<int> GetSelectedMovieGenreIds()
    {
        return _selectedGenreFilters
            .Select(label => TmdbGenreMapper.TryGetGenreId(label, out var genreId) ? genreId : 0)
            .Where(genreId => genreId > 0)
            .Distinct()
            .ToList();
    }

    private IReadOnlyCollection<int> GetSelectedTvGenreIds()
    {
        return _selectedTvGenreFilters
            .Select(label => TmdbTvGenreMapper.TryGetGenreId(label, out var genreId) ? genreId : 0)
            .Where(genreId => genreId > 0)
            .Distinct()
            .ToList();
    }

    private IReadOnlyCollection<string> GetSelectedMovieOriginCountryCodes()
    {
        return GetSelectedOriginCountryCodes(_selectedRegionFilters, SelectedRegionFilter);
    }

    private IReadOnlyCollection<string> GetSelectedTvOriginCountryCodes()
    {
        return GetSelectedOriginCountryCodes(_selectedTvRegionFilters, SelectedTvRegionFilter);
    }

    private static IReadOnlyCollection<string> GetSelectedOriginCountryCodes(
        IReadOnlyCollection<string> selectedRegionFilters,
        string selectedRegionFilter)
    {
        var filters = selectedRegionFilters.Count > 0
            ? selectedRegionFilters
            : string.Equals(selectedRegionFilter, FilterAll, StringComparison.Ordinal)
                ? []
                : [selectedRegionFilter];

        return filters
            .Where(filter => !string.Equals(filter, FilterOther, StringComparison.Ordinal))
            .SelectMany(filter => RegionCountryCodes.TryGetValue(filter, out var codes) ? codes : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetSelectedMovieOriginalLanguage()
    {
        return GetSelectedOriginalLanguage(_selectedLanguageFilters, SelectedLanguageFilter);
    }

    private string GetSelectedTvOriginalLanguage()
    {
        return GetSelectedOriginalLanguage(_selectedTvLanguageFilters, SelectedTvLanguageFilter);
    }

    private static string GetSelectedOriginalLanguage(
        IReadOnlyCollection<string> selectedLanguageFilters,
        string selectedLanguageFilter)
    {
        var filters = selectedLanguageFilters.Count > 0
            ? selectedLanguageFilters
            : string.Equals(selectedLanguageFilter, FilterAll, StringComparison.Ordinal)
                ? []
                : [selectedLanguageFilter];
        return filters.Count == 1
               && !filters.Contains(FilterOther, StringComparer.Ordinal)
               && LanguageCodes.TryGetValue(filters.First(), out var code)
            ? code
            : string.Empty;
    }

    private string GetSelectedMoviePrimaryReleaseDateGte()
    {
        return GetSelectedDateRange(_selectedDecadeFilters, SelectedDecadeFilter).Gte;
    }

    private string GetSelectedMoviePrimaryReleaseDateLte()
    {
        return GetSelectedDateRange(_selectedDecadeFilters, SelectedDecadeFilter).Lte;
    }

    private string GetSelectedTvFirstAirDateGte()
    {
        return GetSelectedDateRange(_selectedTvDecadeFilters, SelectedTvDecadeFilter).Gte;
    }

    private string GetSelectedTvFirstAirDateLte()
    {
        return GetSelectedDateRange(_selectedTvDecadeFilters, SelectedTvDecadeFilter).Lte;
    }

    private static (string Gte, string Lte) GetSelectedDateRange(
        IReadOnlyCollection<string> selectedDecadeFilters,
        string selectedDecadeFilter)
    {
        var filters = selectedDecadeFilters.Count > 0
            ? selectedDecadeFilters
            : string.Equals(selectedDecadeFilter, DecadeAll, StringComparison.Ordinal)
                ? []
                : [selectedDecadeFilter];
        if (filters.Count != 1)
        {
            return (string.Empty, string.Empty);
        }

        var decade = filters.First();
        if (string.Equals(decade, DecadeEarlier, StringComparison.Ordinal))
        {
            return (string.Empty, "1959-12-31");
        }

        if (!decade.EndsWith("s", StringComparison.Ordinal)
            || !int.TryParse(decade.TrimEnd('s'), out var decadeStart))
        {
            return (string.Empty, string.Empty);
        }

        return ($"{decadeStart:0000}-01-01", $"{decadeStart + 9:0000}-12-31");
    }

    private void ResetTvSearchFromFilterChange()
    {
        if (_suppressTvFilterApply)
        {
            return;
        }

        RefreshSearchFilterCommandState();
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            if (CanUseTvDiscoverSearch())
            {
                _ = ResetAndLoadTvSearchDisplayPageAsync(1);
                return;
            }

            ResetTvSearchToMissingKeyword();
            return;
        }

        _ = ResetAndLoadTvSearchDisplayPageAsync(1);
    }

    private void ResetSearchFromFilterChange()
    {
        if (_suppressFilterApply)
        {
            return;
        }

        RefreshSearchFilterCommandState();
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            if (CanUseMovieDiscoverSearch())
            {
                _ = ResetAndLoadSearchDisplayPageAsync(1);
                return;
            }

            ResetMovieSearchToMissingKeyword();
            return;
        }

        _ = ResetAndLoadSearchDisplayPageAsync(1);
    }

    private async Task ResetAndLoadSearchDisplayPageAsync(int displayPage)
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();
        ResetSearchBuffers();
        ResetMovieSearchScrollOffsets();
        SearchPageIndex = 1;
        SearchTotalPages = 0;
        SearchMovies.Clear();
        SearchSummaryText = string.Empty;
        _searchLoadFailed = false;
        RefreshSearchVisibility();

        await LoadSearchDisplayPageCoreAsync(displayPage, _searchCancellationTokenSource.Token);
    }

    private async Task LoadSearchDisplayPageAsync(int displayPage)
    {
        if (displayPage < 1 || !CanLoadMovieSearchSource())
        {
            return;
        }

        _searchCancellationTokenSource = EnsureActiveCancellationTokenSource(_searchCancellationTokenSource);
        await LoadSearchDisplayPageCoreAsync(displayPage, _searchCancellationTokenSource.Token);
    }

    private async Task LoadSearchDisplayPageCoreAsync(
        int displayPage,
        CancellationToken cancellationToken)
    {
        var rollbackState = CaptureMovieSearchPageState();
        var requestVersion = ++_searchRequestVersion;
        _lastSearchDisplayPage = displayPage;
        _searchLoadFailed = false;
        IsSearchLoading = true;
        SearchStatusMessage = displayPage <= 1 ? BuildMovieSearchLoadingMessage() : $"正在加载第 {displayPage} 页...";
        SearchMovies.Clear();
        SearchSummaryText = string.Empty;
        RefreshSearchVisibility();

        try
        {
            await EnsureSearchPoolForDisplayPageWithRetryAsync(displayPage, requestVersion, cancellationToken);
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
            ResetMovieSearchScrollOffsets();
            var visibleItems = RebuildSearchDisplay();
            if (SearchMovies.Count == 0)
            {
                SearchStatusMessage = _searchResultPool.Count == 0
                    ? BuildMovieSearchNoResultsMessage()
                    : "当前筛选条件下无电影结果。";
            }
            else
            {
                SearchStatusMessage = BuildMovieSearchLoadedMessage();
                _ = EnrichOmdbRatingsAsync(visibleItems, requestVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (requestVersion == _searchRequestVersion)
            {
                RestoreMovieSearchPageState(rollbackState, restoreDisplay: true);
                SearchStatusMessage = BuildPageRollbackStatus("搜索加载已取消", rollbackState.PageIndex);
            }
        }
        catch (Exception exception)
        {
            RestoreMovieSearchPageState(rollbackState, restoreDisplay: true);
            SearchStatusMessage = BuildPageRollbackStatus(
                $"第 {displayPage} 页加载失败：{DescribeException(exception)}",
                rollbackState.PageIndex);
            ShowPageLoadFailureToast(displayPage);
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

    private Task EnsureSearchPoolForDisplayPageWithRetryAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        return ExecutePageLoadWithRetryAsync(
            CaptureMovieSearchPageState,
            state => RestoreMovieSearchPageState(state, restoreDisplay: false),
            attemptToken => EnsureSearchPoolForDisplayPageAsync(displayPage, requestVersion, attemptToken),
            cancellationToken,
            RequiresCompleteSearchPool()
                ? SearchFilteredPageLoadAttemptTimeoutSeconds
                : PageLoadAttemptTimeoutSeconds);
    }

    private bool HasEnoughSearchResultsForDisplayPage(int displayPage)
    {
        var required = displayPage * SearchDisplayPageSize;
        if (!RequiresCompleteSearchPool())
        {
            return _searchResultPool.Count >= required || _searchSourceExhausted;
        }

        return !CanFetchNextSearchSourcePage(SearchFilteredMaxSourcePages);
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
        if (RequiresCompleteSearchPool())
        {
            return SearchFilteredMaxSourcePages;
        }

        var pageDrivenLimit = (int)Math.Ceiling(displayPage * SearchDisplayPageSize / (double)SearchTmdbPageSize);
        return CanUseMovieDiscoverSearch()
            ? Math.Min(SearchDiscoverMaxSourcePages, pageDrivenLimit)
            : pageDrivenLimit;
    }

    private async Task FetchNextSearchSourcePageAsync(
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var page = _searchSourceNextPage;
        if (!_searchSourcePageCache.TryGetValue(page, out var response))
        {
            response = CanUseMovieDiscoverSearch()
                ? await _tmdbService.DiscoverMoviesAsync(
                    page,
                    GetMovieDiscoverSortBy(),
                    GetSelectedMovieGenreIds(),
                    GetSelectedMovieOriginCountryCodes(),
                    GetSelectedMovieOriginalLanguage(),
                    GetSelectedMoviePrimaryReleaseDateGte(),
                    GetSelectedMoviePrimaryReleaseDateLte(),
                    cancellationToken: cancellationToken)
                : string.Equals(SelectedSearchType, SearchTypePerson, StringComparison.Ordinal)
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

        ApplyExternalTagCacheSnapshots(pageItems);
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

        var hasDisplayChanges = 0;
        var hasPersistedRatingChanges = 0;
        using var limiter = new SemaphoreSlim(OmdbResolveConcurrency, OmdbResolveConcurrency);
        var tasks = items.Select(
            async item =>
            {
                await limiter.WaitAsync();
                try
                {
                    if (requestVersion != _searchRequestVersion || !item.CanEnrichExternalOmdbRating)
                    {
                        return;
                    }

                    var imdbId = item.ImdbId;
                    if (string.IsNullOrWhiteSpace(imdbId) || item.NeedsDetailsSnapshot)
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
                                hasDisplayChanges = 1;
                            });
                        imdbId = string.IsNullOrWhiteSpace(imdbId) ? details.ImdbId : imdbId;
                    }

                    if (string.IsNullOrWhiteSpace(imdbId))
                    {
                        if (await TryRefreshMovieRatingsAsync(item, null))
                        {
                            hasPersistedRatingChanges = 1;
                        }

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
                        if (omdbRating is null && requestVersion == _searchRequestVersion
                            && await TryRefreshMovieRatingsAsync(item, null))
                        {
                            hasPersistedRatingChanges = 1;
                        }

                        return;
                    }

                    await DispatchAsync(
                        () =>
                        {
                            item.SetOmdbRating(omdbRating);
                            hasDisplayChanges = 1;
                        });
                    if (await TryRefreshMovieRatingsAsync(item, omdbRating))
                    {
                        hasPersistedRatingChanges = 1;
                    }
                }
                finally
                {
                    limiter.Release();
                }
            });

        await Task.WhenAll(tasks);
        if (hasDisplayChanges != 0 && requestVersion == _searchRequestVersion)
        {
            await DispatchAsync(
                () =>
                {
                    if (requestVersion == _searchRequestVersion)
                    {
                        RebuildSearchDisplay();
                    }
                });
        }

        if (hasPersistedRatingChanges != 0 && requestVersion == _searchRequestVersion)
        {
            await DispatchAsync(
                () =>
                {
                    _dataRefreshService.NotifyCollectionChanged();
                    _dataRefreshService.NotifyLibraryChanged();
                });
        }
    }

    private async Task<bool> TryRefreshMovieRatingsAsync(
        DiscoveryMovieCardViewModel item,
        MovieRatingItem? omdbRating)
    {
        try
        {
            return await _ratingRefreshService.RefreshMovieRatingsAsync(
                item.MovieId,
                item.TmdbId,
                item.TmdbRating,
                item.TmdbVoteCount,
                omdbRating,
                CancellationToken.None);
        }
        catch
        {
            return false;
        }
    }

    private async Task EnrichTvSeriesDetailsAsync(
        IReadOnlyList<DiscoveryTvSeriesCardViewModel> items,
        int requestVersion,
        bool isRankingRequest)
    {
        if (items.Count == 0)
        {
            return;
        }

        var hasDisplayChanges = 0;
        var hasPersistedRatingChanges = 0;
        using var limiter = new SemaphoreSlim(TvDetailResolveConcurrency, TvDetailResolveConcurrency);
        var tasks = items.Select(
            async item =>
            {
                await limiter.WaitAsync();
                try
                {
                    if (item.HasLoadedSeasonCount || !IsCurrentTvDetailRequest(requestVersion, isRankingRequest))
                    {
                        return;
                    }

                    TmdbTvSeriesDetailResult? details;
                    try
                    {
                        details = await _tmdbService.GetTvSeriesDetailsAsync(item.TmdbSeriesId, cancellationToken: CancellationToken.None);
                    }
                    catch
                    {
                        if (IsCurrentTvDetailRequest(requestVersion, isRankingRequest))
                        {
                            await DispatchAsync(item.MarkSeasonCountUnavailable);
                        }

                        return;
                    }

                    if (!IsCurrentTvDetailRequest(requestVersion, isRankingRequest))
                    {
                        return;
                    }

                    await DispatchAsync(
                        () =>
                        {
                            if (details is null)
                            {
                                item.MarkSeasonCountUnavailable();
                            }
                            else
                            {
                                item.ApplyDetails(details);
                                hasDisplayChanges = 1;
                            }
                        });

                    if (details is null)
                    {
                        return;
                    }

                    MovieRatingItem? omdbRating = null;
                    try
                    {
                        var externalIds = await _tmdbService.GetTvSeriesExternalIdsAsync(item.TmdbSeriesId, CancellationToken.None);
                        if (!string.IsNullOrWhiteSpace(externalIds?.ImdbId))
                        {
                            omdbRating = await _omdbService.GetSeriesRatingAsync(externalIds.ImdbId, CancellationToken.None);
                        }
                    }
                    catch
                    {
                        omdbRating = null;
                    }

                    if (!IsCurrentTvDetailRequest(requestVersion, isRankingRequest))
                    {
                        return;
                    }

                    if (omdbRating is not null)
                    {
                        await DispatchAsync(
                            () =>
                            {
                                item.SetOmdbRating(omdbRating);
                                hasDisplayChanges = 1;
                            });
                    }

                    if (await TryRefreshTvSeriesRatingsAsync(item, omdbRating))
                    {
                        hasPersistedRatingChanges = 1;
                    }
                }
                finally
                {
                    limiter.Release();
                }
            });

        await Task.WhenAll(tasks);
        if (hasDisplayChanges != 0 && !isRankingRequest && requestVersion == _tvSearchRequestVersion)
        {
            await DispatchAsync(() => RebuildTvSearchDisplay());
        }

        if (hasPersistedRatingChanges != 0 && IsCurrentTvDetailRequest(requestVersion, isRankingRequest))
        {
            await DispatchAsync(
                () =>
                {
                    _dataRefreshService.NotifyCollectionChanged();
                    _dataRefreshService.NotifyLibraryChanged();
                });
        }
    }

    private async Task<bool> TryRefreshTvSeriesRatingsAsync(
        DiscoveryTvSeriesCardViewModel item,
        MovieRatingItem? omdbRating)
    {
        try
        {
            return await _ratingRefreshService.RefreshTvSeriesRatingsAsync(
                item.TvSeriesId,
                item.TmdbSeriesId,
                item.TmdbRating,
                item.TmdbVoteCount,
                omdbRating,
                CancellationToken.None);
        }
        catch
        {
            return false;
        }
    }

    private bool IsCurrentTvDetailRequest(int requestVersion, bool isRankingRequest)
    {
        return isRankingRequest
            ? requestVersion == _tvRankingRequestVersion
            : requestVersion == _tvSearchRequestVersion;
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
        SearchSummaryText = BuildSearchSummaryText(filtered);
        RefreshSearchVisibility();
        RefreshSearchNonSubmitCommandState();
        return pageItems;
    }

    private List<DiscoveryMovieCardViewModel> BuildFilteredSearchMovies()
    {
        var query = _searchResultPool.AsEnumerable();

        if (_selectedGenreFilters.Count > 0)
        {
            query = query.Where(
                item => _selectedGenreFilters.Any(
                    filter => SplitTags(item.DisplayTags).Contains(filter, StringComparer.OrdinalIgnoreCase)));
        }

        if (_selectedRegionFilters.Count > 0)
        {
            query = query.Where(item => _selectedRegionFilters.Any(filter => MatchesRegion(item, filter)));
        }

        if (_selectedLanguageFilters.Count > 0)
        {
            query = query.Where(item => _selectedLanguageFilters.Any(filter => MatchesLanguage(item, filter)));
        }

        if (_selectedDecadeFilters.Count > 0)
        {
            query = query.Where(item => _selectedDecadeFilters.Any(filter => MatchesDecade(item, filter)));
        }

        query = SelectedPlaybackSourceFilter switch
        {
            PlaybackSourceWithSource => query.Where(item => item.IsInLibrary),
            PlaybackSourceWithoutSource => query.Where(item => !item.IsInLibrary),
            _ => query
        };

        query = SelectedLibraryStatusFilter switch
        {
            LibraryStatusInLibrary => query.Where(item => item.IsVisibleInLibrary),
            LibraryStatusNotInLibrary => query.Where(item => !item.IsVisibleInLibrary),
            _ => query
        };

        query = SelectedWatchStatusFilter switch
        {
            WatchStatusWatched => query.Where(item => item.IsWatched),
            WatchStatusUnwatched => query.Where(item => !item.IsWatched),
            _ => query
        };

        if (_selectedCollectionStatusFilters.Count > 0)
        {
            query = query.Where(
                item => _selectedCollectionStatusFilters.Any(filter => MatchesMovieCollectionStatusFilter(item, filter)));
        }

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
        if (!RequiresCompleteSearchPool() && _searchTotalResults > 0)
        {
            var availableResults = CanUseMovieDiscoverSearch()
                ? Math.Min(_searchTotalResults, SearchDiscoverMaxDisplayPages * SearchDisplayPageSize)
                : _searchTotalResults;
            SearchTotalPages = Math.Max(1, (int)Math.Ceiling(availableResults / (double)SearchDisplayPageSize));
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
        OnPropertyChanged(nameof(CanGoNextActiveSearchPage));
        OnPropertyChanged(nameof(SearchPageStatusText));
        OnPropertyChanged(nameof(ActiveSearchPageStatusText));
    }

    private string BuildSearchSummaryText(IReadOnlyCollection<DiscoveryMovieCardViewModel> filteredItems)
    {
        if (_searchResultPool.Count == 0 && filteredItems.Count == 0)
        {
            return string.Empty;
        }

        var inLibraryCount = filteredItems.Count(item => item.IsVisibleInLibrary);
        return BuildSearchResultSummaryText(filteredItems.Count, inLibraryCount);
    }

    private static string BuildSearchResultSummaryText(int totalCount, int inLibraryCount)
    {
        var notInLibraryCount = Math.Max(0, totalCount - inLibraryCount);
        return $"已加载 {totalCount} 项媒体 · 已入库 {inLibraryCount} 项 · 未入库 {notInLibraryCount} 项";
    }

    private bool HasExpandedSearchCriteria()
    {
        return _selectedGenreFilters.Count > 0
               || _selectedRegionFilters.Count > 0
               || _selectedLanguageFilters.Count > 0
               || _selectedDecadeFilters.Count > 0
               || !string.Equals(SelectedWatchStatusFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedPlaybackSourceFilter, FilterAll, StringComparison.Ordinal)
               || !string.Equals(SelectedLibraryStatusFilter, FilterAll, StringComparison.Ordinal)
               || !IsDefaultCollectionStatusFilter(_selectedCollectionStatusFilters)
               || !string.Equals(SelectedSortOption, SortRelevance, StringComparison.Ordinal)
               || !string.Equals(SelectedSortDirection, DirectionDescending, StringComparison.Ordinal);
    }

    private bool RequiresCompleteSearchPool()
    {
        return HasExpandedSearchCriteria() && !CanUseMovieDiscoverSearch();
    }

    private void ClearSearchFilters()
    {
        if (IsTvSearchSelected)
        {
            ClearTvSearchFilters();
            return;
        }

        var searchTypeChanged = ResetActiveSearchTypeToTitle();
        _suppressFilterApply = true;
        _selectedGenreFilters.Clear();
        RefreshSearchGenreFilterState(applyFilters: false);
        _selectedRegionFilters.Clear();
        RefreshSearchRegionFilterState(applyFilters: false);
        SelectedWatchStatusFilter = FilterAll;
        SelectedPlaybackSourceFilter = FilterAll;
        SelectedLibraryStatusFilter = FilterAll;
        ResetCollectionStatusFiltersToDefault(_selectedCollectionStatusFilters);
        RefreshSearchCollectionStatusFilterState(applyFilters: false);
        SelectedSortOption = SortRelevance;
        SelectedSortDirection = DirectionDescending;
        _selectedDecadeFilters.Clear();
        RefreshSearchDecadeFilterState(applyFilters: false);
        _selectedLanguageFilters.Clear();
        RefreshSearchLanguageFilterState(applyFilters: false);
        _suppressFilterApply = false;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ResetMovieSearchToMissingKeyword();
            return;
        }

        if (searchTypeChanged)
        {
            _ = ResetAndLoadSearchDisplayPageAsync(1);
            return;
        }

        SearchPageIndex = 1;
        ResetMovieSearchScrollOffsets();
        RebuildSearchDisplay();
        SearchStatusMessage = "筛选已清除。";
    }

    private void ClearTvSearchFilters()
    {
        var searchTypeChanged = ResetActiveSearchTypeToTitle();
        _suppressTvFilterApply = true;
        _selectedTvGenreFilters.Clear();
        RefreshTvSearchGenreFilterState(applyFilters: false);
        _selectedTvRegionFilters.Clear();
        RefreshTvSearchRegionFilterState(applyFilters: false);
        SelectedTvWatchStatusFilter = FilterAll;
        SelectedTvPlaybackSourceFilter = FilterAll;
        SelectedTvLibraryStatusFilter = FilterAll;
        ResetCollectionStatusFiltersToDefault(_selectedTvCollectionStatusFilters);
        RefreshTvSearchCollectionStatusFilterState(applyFilters: false);
        SelectedTvSortOption = SortRelevance;
        SelectedTvSortDirection = DirectionDescending;
        _selectedTvDecadeFilters.Clear();
        RefreshTvSearchDecadeFilterState(applyFilters: false);
        _selectedTvLanguageFilters.Clear();
        RefreshTvSearchLanguageFilterState(applyFilters: false);
        _suppressTvFilterApply = false;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ResetTvSearchToMissingKeyword();
            return;
        }

        if (searchTypeChanged)
        {
            _ = ResetAndLoadTvSearchDisplayPageAsync(1);
            return;
        }

        TvSearchPageIndex = 1;
        ResetTvSearchScrollOffsets();
        RebuildTvSearchDisplay();
        TvSearchStatusMessage = "筛选已清除。";
    }

    private bool ResetActiveSearchTypeToTitle()
    {
        var titleSearchType = GetTitleSearchTypeForActiveMedia();
        if (string.Equals(_selectedSearchType, titleSearchType, StringComparison.Ordinal))
        {
            return false;
        }

        _selectedSearchType = titleSearchType;
        OnPropertyChanged(nameof(SelectedSearchType));
        OnPropertyChanged(nameof(SearchTypeButtonText));
        RefreshSearchTypeProperties();
        RefreshSearchFilterCommandState();
        return true;
    }

    private async Task OpenSearchMovieAsync(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        await OpenDiscoveryMovieAsync(item, message => SearchStatusMessage = message);
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

    private async Task AddSearchMovieToLibraryAsync(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        await AddDiscoveryMovieToLibraryAsync(
            item,
            message => SearchStatusMessage = message,
            () => RebuildSearchDisplay());
    }

    private async Task OpenRankingMovieAsync(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        await OpenDiscoveryMovieAsync(item, message => RankingStatusMessage = message);
    }

    private async Task OpenRankingItemAsync(object? parameter)
    {
        switch (parameter)
        {
            case DiscoveryMovieCardViewModel movie:
                await OpenRankingMovieAsync(movie);
                break;
            case DiscoveryTvSeriesCardViewModel tvSeries when !IsTvSeriesNavigating:
                await OpenTvSeriesAsync(tvSeries);
                break;
        }
    }

    private void OpenTvSeries(object? parameter)
    {
        if (IsTvSeriesNavigating)
        {
            return;
        }

        if (parameter is not DiscoveryTvSeriesCardViewModel item)
        {
            return;
        }

        _ = OpenTvSeriesAsync(item);
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

    private async Task AddRankingMovieToLibraryAsync(object? parameter)
    {
        if (parameter is not DiscoveryMovieCardViewModel item)
        {
            return;
        }

        await AddDiscoveryMovieToLibraryAsync(
            item,
            message => RankingStatusMessage = message,
            () => RebuildRankingRows());
    }

    private async Task AddTvSeriesToLibraryAsync(object? parameter)
    {
        if (parameter is not DiscoveryTvSeriesCardViewModel item)
        {
            return;
        }

        var requestVersion = ++_tvSeriesOpenRequestVersion;
        try
        {
            SetTvOpenStatusMessage(requestVersion, "正在写入 TV metadata 并加入媒体库。");
            var tvSeriesId = item.TvSeriesId;
            if (!tvSeriesId.HasValue && item.TmdbSeriesId > 0)
            {
                var hydrateResult = await Task.Run(() => _tvMetadataHydrationService.HydrateSeriesAsync(item.TmdbSeriesId));
                if (!IsCurrentTvSeriesOpenRequest(requestVersion))
                {
                    return;
                }

                if (!hydrateResult.TvSeriesId.HasValue)
                {
                    SetTvOpenStatusMessage(requestVersion, hydrateResult.HasErrors ? hydrateResult.BuildStatusMessage() : "TV metadata 写入失败，无法加入媒体库。");
                    return;
                }

                tvSeriesId = hydrateResult.TvSeriesId.Value;
            }

            if (!tvSeriesId.HasValue)
            {
                SetTvOpenStatusMessage(requestVersion, "缺少 TV Series ID，无法加入媒体库。");
                return;
            }

            await _tvMetadataHydrationService.EnsureHydratedBySeriesIdAsync(tvSeriesId.Value);
            if (item.HasHiddenSeason)
            {
                await _tvSeasonCollectionService.RestoreSeriesToLibraryAsync(tvSeriesId.Value);
            }
            else
            {
                await _tvSeasonCollectionService.AddSeriesToLibraryAsync(tvSeriesId.Value);
            }
            var statuses = await _tvStatusResolver.ResolveAsync([item.TmdbSeriesId]);
            if (statuses.TryGetValue(item.TmdbSeriesId, out var status))
            {
                item.ApplyStatus(status);
            }

            _dataRefreshService.NotifyCollectionChanged();
            _dataRefreshService.NotifyLibraryChanged();
            SetTvOpenStatusMessage(requestVersion, $"已加入媒体库：{item.Title}");
        }
        catch (Exception exception)
        {
            SetTvOpenStatusMessage(requestVersion, $"加入媒体库失败：{exception.Message}");
        }
    }

    private async Task AddDiscoveryMovieToLibraryAsync(
        DiscoveryMovieCardViewModel item,
        Action<string> setStatusMessage,
        Action rebuild)
    {
        try
        {
            if (item.LibraryVisibilityState == LibraryVisibilityState.Hidden)
            {
                await _userCollectionService.RestoreToLibraryAsync(
                    DiscoveryExternalMovieAdapter.ToRecommendation(item),
                    changeSource: "Discovery");
            }
            else
            {
                await _userCollectionService.AddToLibraryAsync(
                    DiscoveryExternalMovieAdapter.ToRecommendation(item),
                    changeSource: "Discovery");
            }
            var statuses = await _statusResolver.ResolveAsync([item.TmdbId]);
            if (statuses.TryGetValue(item.TmdbId, out var status))
            {
                item.ApplyStatus(status);
            }

            _dataRefreshService.NotifyCollectionChanged();
            _dataRefreshService.NotifyLibraryChanged();
            rebuild();
            setStatusMessage($"已加入媒体库：{item.Title}");
        }
        catch (Exception exception)
        {
            setStatusMessage($"加入媒体库失败：{exception.Message}");
        }
    }

    private async Task OpenDiscoveryMovieAsync(
        DiscoveryMovieCardViewModel item,
        Action<string> setStatusMessage)
    {
        await RefreshDiscoveryMovieStatusBeforeOpenAsync(item, setStatusMessage);

        if (item.MovieId is > 0)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, item.MovieId.Value);
            return;
        }

        if (item.TmdbId <= 0)
        {
            setStatusMessage("缺少 TMDB ID，暂时无法打开无播放源详情。");
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(DiscoveryExternalMovieAdapter.ToRecommendation(item));
    }

    private async Task RefreshDiscoveryMovieStatusBeforeOpenAsync(
        DiscoveryMovieCardViewModel item,
        Action<string> setStatusMessage)
    {
        if (item.TmdbId <= 0)
        {
            return;
        }

        try
        {
            var statuses = await _statusResolver.ResolveAsync([item.TmdbId], CancellationToken.None);
            if (statuses.TryGetValue(item.TmdbId, out var status))
            {
                item.ApplyStatus(status);
            }
        }
        catch (Exception exception)
        {
            setStatusMessage($"刷新影片收藏状态失败，已使用当前卡片状态打开：{DescribeException(exception)}");
        }
    }

    private async Task OpenTvSeriesAsync(DiscoveryTvSeriesCardViewModel item)
    {
        var requestVersion = ++_tvSeriesOpenRequestVersion;
        IsTvSeriesNavigating = true;
        try
        {
            if (item.IsInLibrary && item.TvSeriesId is > 0)
            {
                var tvSeriesId = item.TvSeriesId.Value;
                SetTvOpenStatusMessage(requestVersion, "正在读取 TV metadata。");
                var result = await Task.Run(
                    () => _tvMetadataHydrationService.EnsureSeriesSummaryBySeriesIdAsync(tvSeriesId));
                if (!IsCurrentTvSeriesOpenRequest(requestVersion))
                {
                    return;
                }

                SetTvOpenStatusMessage(
                    requestVersion,
                    result.HasErrors ? result.BuildStatusMessage() : "TV metadata 已准备好，正在打开详情。");
                if (result.TvSeriesId.HasValue)
                {
                    _navigationStateService.RequestTvSeriesOverview(result.TvSeriesId.Value);
                    return;
                }

                if (item.TmdbSeriesId > 0)
                {
                    SetTvOpenStatusMessage(requestVersion, "本地 TV metadata 已删除，正在重新写入。");
                    result = await Task.Run(() => _tvMetadataHydrationService.EnsureSeriesSummaryAsync(item.TmdbSeriesId, force: true));
                    if (!IsCurrentTvSeriesOpenRequest(requestVersion))
                    {
                        return;
                    }

                    if (result.TvSeriesId.HasValue)
                    {
                        SetTvOpenStatusMessage(
                            requestVersion,
                            result.HasErrors ? result.BuildStatusMessage() : "TV metadata 已准备好，正在打开详情。");
                        _navigationStateService.RequestTvSeriesOverview(result.TvSeriesId.Value);
                        return;
                    }
                }

                SetTvOpenStatusMessage(
                    requestVersion,
                    result.HasErrors
                        ? $"TV metadata 写入失败：{string.Join("；", result.Errors)}"
                        : "TV metadata 写入失败。");
                return;
            }

            if (item.TmdbSeriesId > 0)
            {
                SetTvOpenStatusMessage(requestVersion, "正在读取并写入 TV metadata。");
                var result = await Task.Run(() => _tvMetadataHydrationService.EnsureSeriesSummaryAsync(item.TmdbSeriesId));
                if (!IsCurrentTvSeriesOpenRequest(requestVersion))
                {
                    return;
                }

                if (result.TvSeriesId.HasValue)
                {
                    SetTvOpenStatusMessage(
                        requestVersion,
                        result.HasErrors ? result.BuildStatusMessage() : "TV metadata 已准备好，正在打开详情。");

                    _navigationStateService.RequestTvSeriesOverview(result.TvSeriesId.Value);
                    return;
                }

                SetTvOpenStatusMessage(
                    requestVersion,
                    result.HasErrors
                        ? $"TV metadata 写入失败：{string.Join("；", result.Errors)}"
                        : "TV metadata 写入失败。");
                return;
            }

            var message = $"《{item.Title}》暂无播放源，TV metadata 暂不可用。";
            SetTvOpenStatusMessage(requestVersion, message);
            await _confirmationDialogService.ConfirmAsync(
                "电视剧 metadata 不可用",
                message,
                "知道了",
                "关闭");
        }
        catch (Exception exception)
        {
            SetTvOpenStatusMessage(requestVersion, $"TV metadata 读取失败：{DescribeException(exception)}");
        }
        finally
        {
            if (IsCurrentTvSeriesOpenRequest(requestVersion))
            {
                IsTvSeriesNavigating = false;
            }
        }
    }

    private void SetTvOpenStatusMessage(string message)
    {
        if (IsTvSearchSelected)
        {
            TvSearchStatusMessage = message;
        }

        if (IsTvRankingSelected)
        {
            TvRankingStatusMessage = message;
        }
    }

    private void SetTvOpenStatusMessage(int requestVersion, string message)
    {
        if (IsCurrentTvSeriesOpenRequest(requestVersion))
        {
            SetTvOpenStatusMessage(message);
        }
    }

    private bool IsCurrentTvSeriesOpenRequest(int requestVersion)
    {
        return requestVersion == _tvSeriesOpenRequestVersion;
    }

    private void InvalidateTvSeriesOpenRequest()
    {
        _tvSeriesOpenRequestVersion++;
    }

    private async Task ToggleDiscoveryWantToWatchAsync(
        DiscoveryMovieCardViewModel item,
        Action<string> setStatusMessage,
        Action refreshView)
    {
        if (item.IsWatched)
        {
            const string watchedWantBlockedMessage = "该电影已经看过，无法标记想看。";
            setStatusMessage(watchedWantBlockedMessage);
            ShowTransientPageMessage(watchedWantBlockedMessage, TransientPageMessageKindWarning);
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
            item.ApplyMissingStatus();
        }
    }

    private async Task RefreshCachedMovieCardsFromLocalDetailsAsync()
    {
        _pendingLocalMovieCardRefresh = false;
        var refreshVersion = ++_localMovieCardRefreshVersion;
        var items = GetCachedMovieCards();
        var tvItems = GetCachedTvSeriesCards();
        if (items.Count == 0 && tvItems.Count == 0)
        {
            return;
        }

        if (items.Count > 0)
        {
            IReadOnlyDictionary<int, DiscoveryMovieStatus> statuses;
            try
            {
                statuses = await _statusResolver.ResolveAsync(
                    items.Select(item => item.TmdbId).Where(tmdbId => tmdbId > 0).Distinct(),
                    CancellationToken.None);
            }
            catch
            {
                _pendingLocalMovieCardRefresh = true;
                return;
            }

            if (refreshVersion != _localMovieCardRefreshVersion)
            {
                return;
            }

            await DispatchAsync(
                () =>
                {
                    foreach (var item in items)
                    {
                        if (statuses.TryGetValue(item.TmdbId, out var status))
                        {
                            item.ApplyStatus(status);
                        }
                        else
                        {
                            item.ApplyMissingStatus();
                        }
                    }

                    ApplyExternalTagCacheSnapshots(items);
                });

            var itemsByMovieId = items
                .Where(item => item.MovieId is > 0)
                .GroupBy(item => item.MovieId!.Value)
                .ToArray();
            foreach (var group in itemsByMovieId)
            {
                MovieDetailModel? detail;
                try
                {
                    detail = await _movieDetailQueryService.GetMovieDetailAsync(group.Key, CancellationToken.None);
                }
                catch
                {
                    continue;
                }

                if (detail is null || refreshVersion != _localMovieCardRefreshVersion)
                {
                    continue;
                }

                await DispatchAsync(
                    () =>
                    {
                        foreach (var item in group)
                        {
                            item.ApplyLocalTagSnapshot(detail);
                        }
                    });
            }
        }

        if (tvItems.Count > 0)
        {
            IReadOnlyDictionary<int, DiscoveryTvSeriesStatus> statuses;
            try
            {
                statuses = await _tvStatusResolver.ResolveAsync(
                    tvItems.Select(item => item.TmdbSeriesId).Where(tmdbId => tmdbId > 0).Distinct(),
                    CancellationToken.None);
            }
            catch
            {
                _pendingLocalMovieCardRefresh = true;
                return;
            }

            if (refreshVersion != _localMovieCardRefreshVersion)
            {
                return;
            }

            await DispatchAsync(
                () =>
                {
                    foreach (var item in tvItems)
                    {
                        if (statuses.TryGetValue(item.TmdbSeriesId, out var status))
                        {
                            item.ApplyStatus(status);
                        }
                    }
                });
        }
    }

    private IReadOnlyList<DiscoveryMovieCardViewModel> GetCachedMovieCards()
    {
        return _searchResultPool
            .Concat(_rankingMovies)
            .Where(item => item.TmdbId > 0)
            .ToList();
    }

    private IReadOnlyList<DiscoveryTvSeriesCardViewModel> GetCachedTvSeriesCards()
    {
        return _tvSearchResultPool
            .Concat(_rankingTvSeries)
            .Where(item => item.TmdbSeriesId > 0)
            .ToList();
    }

    private static void ApplyExternalTagCacheSnapshots(IEnumerable<DiscoveryMovieCardViewModel> items)
    {
        foreach (var item in items)
        {
            ApplyExternalTagCacheSnapshot(item);
        }
    }

    private static void ApplyExternalTagCacheSnapshot(DiscoveryMovieCardViewModel item)
    {
        if (item.HasLocalMovie)
        {
            return;
        }

        if (ExternalMovieTagCache.TryGet(item.TmdbId, item.ImdbId, item.Title, item.ReleaseYear, out var tags))
        {
            item.ApplyExternalTagSnapshot(tags);
        }
    }

    private async Task EnsureRankingsActivatedAsync()
    {
        if (_hasActivatedRankings && !ShouldLoadActiveRankingsOnActivation())
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

    private bool ShouldLoadActiveRankingsOnActivation()
    {
        if (IsTvRankingSelected)
        {
            return !IsTvSeriesNavigating && !IsTvRankingLoading && !HasRankingTvSeries;
        }

        return !IsRankingLoading && !HasRankingMovies;
    }

    private void SelectRankingMediaType(object? parameter)
    {
        if (IsActiveRankingLoading)
        {
            return;
        }

        var mediaType = GetOptionValue(parameter, DiscoveryMediaTypeMovie);
        if (!RankingMediaTypeOptions.Contains(mediaType, StringComparer.Ordinal)
            || string.Equals(mediaType, SelectedRankingMediaType, StringComparison.Ordinal))
        {
            return;
        }

        SelectedRankingMediaType = mediaType;
    }

    private void SelectRankingType(object? parameter)
    {
        if (IsActiveRankingLoading)
        {
            return;
        }

        if (parameter is not string rankingType
            || string.IsNullOrWhiteSpace(rankingType)
            || string.Equals(rankingType, IsTvRankingSelected ? SelectedTvRankingType : SelectedRankingType, StringComparison.Ordinal))
        {
            return;
        }

        if (IsTvRankingSelected)
        {
            SelectedTvRankingType = rankingType;
        }
        else
        {
            SelectedRankingType = rankingType;
        }

        _ = ResetAndLoadRankingsAsync();
    }

    private void SelectTrendingTime(object? parameter)
    {
        if (IsActiveRankingLoading)
        {
            return;
        }

        if (parameter is not string trendingTime
            || string.IsNullOrWhiteSpace(trendingTime)
            || !IsActiveTrendingRanking)
        {
            return;
        }

        if (IsTvRankingSelected)
        {
            if (string.Equals(trendingTime, SelectedTvTrendingTime, StringComparison.Ordinal))
            {
                return;
            }

            SelectedTvTrendingTime = trendingTime;
        }
        else
        {
            if (string.Equals(trendingTime, SelectedTrendingTime, StringComparison.Ordinal))
            {
                return;
            }

            SelectedTrendingTime = trendingTime;
        }

        _ = ResetAndLoadRankingsAsync();
    }

    private void SelectRankingPreset(object? parameter)
    {
        if (IsActiveRankingLoading || parameter is not string preset || string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        var parts = preset.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return;
        }

        var mediaType = parts[0];
        var rankingType = parts[1];
        var trendingTime = parts.Length >= 3 ? parts[2] : string.Empty;
        if (!RankingMediaTypeOptions.Contains(mediaType, StringComparer.Ordinal)
            || !RankingTypeOptions.Contains(rankingType, StringComparer.Ordinal)
            || (string.Equals(rankingType, RankingTypeTrending, StringComparison.Ordinal)
                && !TrendingTimeOptions.Contains(trendingTime, StringComparer.Ordinal)))
        {
            return;
        }

        var changed = false;
        _suppressRankingPresetApply = true;
        try
        {
            if (!string.Equals(SelectedRankingMediaType, mediaType, StringComparison.Ordinal))
            {
                SelectedRankingMediaType = mediaType;
                changed = true;
            }

            if (string.Equals(mediaType, DiscoveryMediaTypeTv, StringComparison.Ordinal))
            {
                if (!string.Equals(SelectedTvRankingType, rankingType, StringComparison.Ordinal))
                {
                    SelectedTvRankingType = rankingType;
                    changed = true;
                }

                if (string.Equals(rankingType, RankingTypeTrending, StringComparison.Ordinal)
                    && !string.Equals(SelectedTvTrendingTime, trendingTime, StringComparison.Ordinal))
                {
                    SelectedTvTrendingTime = trendingTime;
                    changed = true;
                }
            }
            else
            {
                if (!string.Equals(SelectedRankingType, rankingType, StringComparison.Ordinal))
                {
                    SelectedRankingType = rankingType;
                    changed = true;
                }

                if (string.Equals(rankingType, RankingTypeTrending, StringComparison.Ordinal)
                    && !string.Equals(SelectedTrendingTime, trendingTime, StringComparison.Ordinal))
                {
                    SelectedTrendingTime = trendingTime;
                    changed = true;
                }
            }
        }
        finally
        {
            _suppressRankingPresetApply = false;
        }

        if (changed && SelectedTabIndex == RankingTabIndex && _hasActivatedRankings)
        {
            _ = ResetAndLoadRankingsAsync();
        }
    }

    private async Task ResetAndLoadRankingsAsync()
    {
        if (IsTvRankingSelected)
        {
            await ResetAndLoadTvRankingsAsync();
            return;
        }

        _rankingCancellationTokenSource?.Cancel();
        _rankingCancellationTokenSource = new CancellationTokenSource();
        IsRankingLoading = true;
        ResetRankingBuffers();
        ResetRankingMovieScrollOffset();
        RankingPageIndex = 1;
        RankingTotalDisplayPages = 1;
        TopRankingMovie = null;
        RankingRows.Clear();
        RankingSummaryText = string.Empty;
        _rankingLoadFailed = false;
        RankingStatusMessage = $"正在加载{SelectedRankingType}...";
        RefreshRankingVisibility();

        await LoadRankingDisplayPageCoreAsync(1, _rankingCancellationTokenSource.Token);
    }

    private async Task GoPreviousRankingPageAsync()
    {
        if (IsTvRankingSelected)
        {
            if (!CanGoPreviousTvRankingPage)
            {
                return;
            }

            await LoadTvRankingDisplayPageAsync(TvRankingPageIndex - 1);
            return;
        }

        if (!CanGoPreviousRankingPage)
        {
            return;
        }

        await LoadRankingDisplayPageAsync(RankingPageIndex - 1);
    }

    private async Task GoNextRankingPageAsync()
    {
        WriteRankingDiagnostics(
            "next-command-enter",
            $"mediaType={GetActiveRankingDiagnosticsMediaType()} moviePage={RankingPageIndex} movieTotalPages={RankingTotalDisplayPages} movieCanNext={CanGoNextRankingPage} tvPage={TvRankingPageIndex} tvTotalPages={TvRankingTotalDisplayPages} tvCanNext={CanGoNextTvRankingPage} activeCanNext={CanGoNextActiveRankingPage} movieLoading={IsRankingLoading} tvLoading={IsTvRankingLoading} tvNavigating={IsTvSeriesNavigating}");

        if (IsTvRankingSelected)
        {
            if (!CanGoNextTvRankingPage)
            {
                WriteRankingDiagnostics(
                    "next-command-rejected",
                    $"mediaType=tv reason=can-next-false page={TvRankingPageIndex} totalPages={TvRankingTotalDisplayPages} canNext={CanGoNextTvRankingPage} bufferedItems={_rankingTvSeries.Count} sourceNextPage={_tvRankingSourceNextPage} sourceTotalPages={_tvRankingSourceTotalPages} sourceExhausted={_tvRankingSourceExhausted} isLoading={IsTvRankingLoading} isNavigating={IsTvSeriesNavigating}");
                return;
            }

            var tvTargetPage = TvRankingPageIndex + 1;
            WriteRankingDiagnostics(
                "next-click",
                $"mediaType=tv currentDisplayPage={TvRankingPageIndex} targetDisplayPage={tvTargetPage} totalDisplayPages={TvRankingTotalDisplayPages} bufferedItems={_rankingTvSeries.Count} sourceNextPage={_tvRankingSourceNextPage}");
            await LoadTvRankingDisplayPageAsync(tvTargetPage);
            return;
        }

        if (!CanGoNextRankingPage)
        {
            WriteRankingDiagnostics(
                "next-command-rejected",
                $"mediaType=movie reason=can-next-false page={RankingPageIndex} totalPages={RankingTotalDisplayPages} canNext={CanGoNextRankingPage} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage} sourceTotalPages={_rankingSourceTotalPages} sourceExhausted={_rankingSourceExhausted} isLoading={IsRankingLoading}");
            return;
        }

        var targetPage = RankingPageIndex + 1;
        WriteRankingDiagnostics(
            "next-click",
            $"currentDisplayPage={RankingPageIndex} targetDisplayPage={targetPage} totalDisplayPages={RankingTotalDisplayPages} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage}");
        await LoadRankingDisplayPageAsync(targetPage);
    }

    private async Task RetryActiveRankingAsync()
    {
        if (!CanRetryActiveRanking)
        {
            return;
        }

        if (IsTvRankingSelected)
        {
            await LoadTvRankingDisplayPageAsync(Math.Max(1, _lastTvRankingDisplayPage));
            return;
        }

        await LoadRankingDisplayPageAsync(Math.Max(1, _lastRankingDisplayPage));
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

        _rankingCancellationTokenSource = EnsureActiveCancellationTokenSource(_rankingCancellationTokenSource);
        await LoadRankingDisplayPageCoreAsync(displayPage, _rankingCancellationTokenSource.Token);
    }

    private async Task LoadRankingDisplayPageCoreAsync(
        int displayPage,
        CancellationToken cancellationToken)
    {
        var rollbackState = CaptureMovieRankingPageState();
        var requestVersion = ++_rankingRequestVersion;
        _lastRankingDisplayPage = displayPage;
        _rankingLoadFailed = false;
        var loadStopwatch = Stopwatch.StartNew();
        var requiredItemCount = GetRankingRequiredItemCount(displayPage);
        IsRankingLoading = true;
        WriteRankingDiagnostics(
            "display-load-start",
            $"displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingMovies.Count} sourceNextPage={_rankingSourceNextPage} sourceTotalPages={_rankingSourceTotalPages} sourceExhausted={_rankingSourceExhausted}");
        RankingStatusMessage = displayPage <= 1 ? $"正在加载{SelectedRankingType}..." : $"正在加载第 {displayPage} 页...";

        try
        {
            await EnsureRankingItemsForDisplayPageWithRetryAsync(displayPage, requestVersion, cancellationToken);
            if (requestVersion != _rankingRequestVersion)
            {
                WriteRankingDiagnostics(
                    "display-load-stale",
                    $"displayPage={displayPage} requestVersion={requestVersion} activeVersion={_rankingRequestVersion} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
                return;
            }

            RankingPageIndex = displayPage;
            ResetRankingMovieScrollOffset();
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
            if (requestVersion == _rankingRequestVersion)
            {
                RestoreMovieRankingPageState(rollbackState, restoreDisplay: true);
                RankingStatusMessage = BuildPageRollbackStatus("榜单加载已取消", rollbackState.PageIndex);
            }
        }
        catch (Exception exception)
        {
            WriteRankingDiagnostics(
                "display-load-failed",
                $"displayPage={displayPage} requestVersion={requestVersion} bufferedItems={_rankingMovies.Count} elapsedMs={loadStopwatch.ElapsedMilliseconds} errorType={exception.GetType().Name}");
            RestoreMovieRankingPageState(rollbackState, restoreDisplay: true);
            RankingStatusMessage = BuildPageRollbackStatus(
                $"第 {displayPage} 页加载失败：{DescribeException(exception)}",
                rollbackState.PageIndex);
            ShowPageLoadFailureToast(displayPage);
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

    private Task EnsureRankingItemsForDisplayPageWithRetryAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        return ExecutePageLoadWithRetryAsync(
            CaptureMovieRankingPageState,
            state => RestoreMovieRankingPageState(state, restoreDisplay: false),
            attemptToken => EnsureRankingItemsForDisplayPageAsync(displayPage, requestVersion, attemptToken),
            cancellationToken);
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

    private async Task ResetAndLoadTvRankingsAsync()
    {
        if (IsTvSeriesNavigating)
        {
            return;
        }

        InvalidateTvSeriesOpenRequest();
        _rankingCancellationTokenSource?.Cancel();
        _rankingCancellationTokenSource = new CancellationTokenSource();
        IsTvRankingLoading = true;
        ResetTvRankingBuffers();
        ResetRankingTvScrollOffset();
        TvRankingPageIndex = 1;
        TvRankingTotalDisplayPages = 1;
        TopRankingTvSeries = null;
        RankingTvSeries.Clear();
        RankingTvRows.Clear();
        TvRankingSummaryText = string.Empty;
        _tvRankingLoadFailed = false;
        TvRankingStatusMessage = $"正在加载{SelectedTvRankingType}...";
        RefreshTvRankingVisibility();

        await LoadTvRankingDisplayPageCoreAsync(1, _rankingCancellationTokenSource.Token);
    }

    private async Task LoadTvRankingDisplayPageAsync(int displayPage)
    {
        if (IsTvSeriesNavigating || IsTvRankingLoading || displayPage < 1 || displayPage > TvRankingTotalDisplayPages)
        {
            WriteRankingDiagnostics(
                "display-load-rejected",
                $"mediaType=tv displayPage={displayPage} totalDisplayPages={TvRankingTotalDisplayPages} bufferedItems={_rankingTvSeries.Count} isLoading={IsTvRankingLoading} isNavigating={IsTvSeriesNavigating}");
            return;
        }

        InvalidateTvSeriesOpenRequest();
        _rankingCancellationTokenSource = EnsureActiveCancellationTokenSource(_rankingCancellationTokenSource);
        await LoadTvRankingDisplayPageCoreAsync(displayPage, _rankingCancellationTokenSource.Token);
    }

    private async Task LoadTvRankingDisplayPageCoreAsync(
        int displayPage,
        CancellationToken cancellationToken)
    {
        var rollbackState = CaptureTvRankingPageState();
        var requestVersion = ++_tvRankingRequestVersion;
        _lastTvRankingDisplayPage = displayPage;
        _tvRankingLoadFailed = false;
        var loadStopwatch = Stopwatch.StartNew();
        var requiredItemCount = GetTvRankingRequiredItemCount(displayPage);
        IsTvRankingLoading = true;
        WriteRankingDiagnostics(
            "display-load-start",
            $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingTvSeries.Count} sourceNextPage={_tvRankingSourceNextPage} sourceTotalPages={_tvRankingSourceTotalPages} sourceExhausted={_tvRankingSourceExhausted}");
        TvRankingStatusMessage = displayPage <= 1 ? $"正在加载{SelectedTvRankingType}..." : $"正在加载第 {displayPage} 页...";

        try
        {
            await EnsureTvRankingItemsForDisplayPageWithRetryAsync(displayPage, requestVersion, cancellationToken);
            if (requestVersion != _tvRankingRequestVersion)
            {
                WriteRankingDiagnostics(
                    "display-load-stale",
                    $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} activeVersion={_tvRankingRequestVersion} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
                return;
            }

            TvRankingPageIndex = displayPage;
            ResetRankingTvScrollOffset();
            UpdateTvRankingTotalPages();
            var visibleItems = RebuildTvRankingDisplay();
            WriteRankingDiagnostics(
                "display-load-complete",
                $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} visibleItems={visibleItems.Count} bufferedItems={_rankingTvSeries.Count} totalDisplayPages={TvRankingTotalDisplayPages} canGoNext={CanGoNextTvRankingPage} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
            TvRankingStatusMessage = visibleItems.Count == 0
                ? "电视剧榜单暂无结果。"
                : $"{SelectedTvRankingType}第 {TvRankingPageIndex} 页已加载。";
            _ = EnrichTvSeriesDetailsAsync(visibleItems, requestVersion, isRankingRequest: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WriteRankingDiagnostics(
                "display-load-canceled",
                $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} bufferedItems={_rankingTvSeries.Count} elapsedMs={loadStopwatch.ElapsedMilliseconds}");
            if (requestVersion == _tvRankingRequestVersion)
            {
                RestoreTvRankingPageState(rollbackState, restoreDisplay: true);
                TvRankingStatusMessage = BuildPageRollbackStatus("电视剧榜单加载已取消", rollbackState.PageIndex);
            }
        }
        catch (Exception exception)
        {
            WriteRankingDiagnostics(
                "display-load-failed",
                $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} bufferedItems={_rankingTvSeries.Count} elapsedMs={loadStopwatch.ElapsedMilliseconds} errorType={exception.GetType().Name}");
            RestoreTvRankingPageState(rollbackState, restoreDisplay: true);
            TvRankingStatusMessage = BuildPageRollbackStatus(
                $"第 {displayPage} 页加载失败：{DescribeException(exception)}",
                rollbackState.PageIndex);
            ShowPageLoadFailureToast(displayPage);
        }
        finally
        {
            IsTvRankingLoading = false;
            RefreshRankingCommandState();
        }
    }

    private async Task EnsureTvRankingItemsForDisplayPageAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var requiredItemCount = GetTvRankingRequiredItemCount(displayPage);
        WriteRankingDiagnostics(
            "ensure-start",
            $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingTvSeries.Count} sourceNextPage={_tvRankingSourceNextPage} canFetch={CanFetchNextTvRankingSourcePage()}");
        while (requestVersion == _tvRankingRequestVersion
               && _rankingTvSeries.Count < requiredItemCount
               && CanFetchNextTvRankingSourcePage())
        {
            WriteRankingDiagnostics(
                "ensure-fetch-needed",
                $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingTvSeries.Count} sourceNextPage={_tvRankingSourceNextPage}");
            await FetchNextTvRankingSourcePageAsync(requestVersion, cancellationToken);
        }

        WriteRankingDiagnostics(
            "ensure-complete",
            $"mediaType=tv displayPage={displayPage} requestVersion={requestVersion} requiredItems={requiredItemCount} bufferedItems={_rankingTvSeries.Count} sourceNextPage={_tvRankingSourceNextPage} canFetch={CanFetchNextTvRankingSourcePage()} sourceExhausted={_tvRankingSourceExhausted}");
    }

    private Task EnsureTvRankingItemsForDisplayPageWithRetryAsync(
        int displayPage,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        return ExecutePageLoadWithRetryAsync(
            CaptureTvRankingPageState,
            state => RestoreTvRankingPageState(state, restoreDisplay: false),
            attemptToken => EnsureTvRankingItemsForDisplayPageAsync(displayPage, requestVersion, attemptToken),
            cancellationToken);
    }

    private bool CanFetchNextTvRankingSourcePage()
    {
        if (_tvRankingSourceExhausted || _rankingTvSeries.Count >= MaxRankingMovies)
        {
            return false;
        }

        if (_tvRankingSourceTotalPages > 0 && _tvRankingSourceNextPage > _tvRankingSourceTotalPages)
        {
            return false;
        }

        return _tvRankingSourceNextPage <= (int)Math.Ceiling(MaxRankingMovies / (double)RankingTmdbPageSize);
    }

    private async Task FetchNextTvRankingSourcePageAsync(
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var page = _tvRankingSourceNextPage;
        var bufferedBefore = _rankingTvSeries.Count;
        var fetchStopwatch = Stopwatch.StartNew();
        var fromCache = _rankingTvSeriesSourcePageCache.TryGetValue(page, out var response);
        WriteRankingDiagnostics(
            "source-fetch-start",
            $"mediaType=tv requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} bufferedItems={bufferedBefore} sourceTotalPages={_tvRankingSourceTotalPages}");

        try
        {
            if (!fromCache)
            {
                response = await LoadTvRankingPageFromTmdbAsync(page, cancellationToken);
                _rankingTvSeriesSourcePageCache[page] = response;
            }
        }
        catch (OperationCanceledException)
        {
            WriteRankingDiagnostics(
                "source-fetch-canceled",
                $"mediaType=tv requestVersion={requestVersion} sourcePage={page} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            throw;
        }
        catch (Exception exception)
        {
            WriteRankingDiagnostics(
                "source-fetch-failed",
                $"mediaType=tv requestVersion={requestVersion} sourcePage={page} elapsedMs={fetchStopwatch.ElapsedMilliseconds} errorType={exception.GetType().Name}");
            throw;
        }

        if (response is null)
        {
            WriteRankingDiagnostics(
                "source-fetch-missing-response",
                $"mediaType=tv requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            return;
        }

        if (requestVersion != _tvRankingRequestVersion)
        {
            WriteRankingDiagnostics(
                "source-fetch-stale",
                $"mediaType=tv requestVersion={requestVersion} activeVersion={_tvRankingRequestVersion} sourcePage={page} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            return;
        }

        _tvRankingSourceNextPage = page + 1;
        _tvRankingSourceTotalPages = response.TotalPages;
        _tvRankingTotalResults = response.TotalResults;
        if (response.TotalPages <= 0 || page >= response.TotalPages || response.Results.Count == 0)
        {
            _tvRankingSourceExhausted = true;
        }

        var pageItems = BuildTvRankingPageItems(response.Results);
        if (pageItems.Count == 0)
        {
            WriteRankingDiagnostics(
                "source-fetch-no-items",
                $"mediaType=tv requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} tmdbResults={response.Results.Count} bufferedItems={_rankingTvSeries.Count} nextSourcePage={_tvRankingSourceNextPage} sourceExhausted={_tvRankingSourceExhausted} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
            return;
        }

        var statusStopwatch = Stopwatch.StartNew();
        var statuses = await _tvStatusResolver.ResolveAsync(pageItems.Select(item => item.TmdbSeriesId), cancellationToken);
        WriteRankingDiagnostics(
            "status-resolve-complete",
            $"mediaType=tv requestVersion={requestVersion} sourcePage={page} itemCount={pageItems.Count} statusCount={statuses.Count} elapsedMs={statusStopwatch.ElapsedMilliseconds}");
        foreach (var item in pageItems)
        {
            if (statuses.TryGetValue(item.TmdbSeriesId, out var status))
            {
                item.ApplyStatus(status);
            }
        }

        _rankingTvSeries.AddRange(pageItems);
        UpdateTvRankingTotalPages();
        WriteRankingDiagnostics(
            "source-fetch-complete",
            $"mediaType=tv requestVersion={requestVersion} sourcePage={page} fromCache={fromCache} addedItems={pageItems.Count} bufferedBefore={bufferedBefore} bufferedAfter={_rankingTvSeries.Count} nextSourcePage={_tvRankingSourceNextPage} sourceTotalPages={_tvRankingSourceTotalPages} sourceExhausted={_tvRankingSourceExhausted} elapsedMs={fetchStopwatch.ElapsedMilliseconds}");
    }

    private Task<TmdbTvSeriesSearchPage> LoadTvRankingPageFromTmdbAsync(
        int page,
        CancellationToken cancellationToken)
    {
        return SelectedTvRankingType switch
        {
            RankingTypeTopRated => _tmdbService.GetTopRatedTvSeriesAsync(page, cancellationToken: cancellationToken),
            RankingTypeTrending => _tmdbService.GetTrendingTvSeriesAsync(GetSelectedTvTrendingWindow(), page, cancellationToken: cancellationToken),
            _ => _tmdbService.GetPopularTvSeriesAsync(page, cancellationToken: cancellationToken)
        };
    }

    private List<DiscoveryTvSeriesCardViewModel> BuildTvRankingPageItems(IReadOnlyList<TmdbTvSeriesSearchItem> sourceItems)
    {
        var remainingSlots = MaxRankingMovies - _rankingTvSeries.Count;
        if (remainingSlots <= 0)
        {
            return [];
        }

        var pageItems = new List<DiscoveryTvSeriesCardViewModel>();
        foreach (var sourceItem in sourceItems)
        {
            if (sourceItem.TmdbId <= 0
                || !_rankingTvSeriesTmdbIds.Add(sourceItem.TmdbId)
                || pageItems.Count >= remainingSlots)
            {
                continue;
            }

            pageItems.Add(new DiscoveryTvSeriesCardViewModel(sourceItem, ++_nextTvRankingRank, showRank: true));
        }

        return pageItems;
    }

    private IReadOnlyList<DiscoveryTvSeriesCardViewModel> RebuildTvRankingDisplay()
    {
        var visibleItems = GetTvRankingDisplayItems(TvRankingPageIndex);
        TopRankingTvSeries = TvRankingPageIndex == 1 ? visibleItems.FirstOrDefault() : null;
        var rowItems = TvRankingPageIndex == 1
            ? visibleItems.Skip(1).ToList()
            : visibleItems;

        RankingTvSeries.Clear();
        foreach (var item in visibleItems)
        {
            RankingTvSeries.Add(item);
        }

        RankingTvRows.Clear();
        for (var index = 0; index < rowItems.Count; index += 2)
        {
            var left = rowItems[index];
            var right = index + 1 < rowItems.Count ? rowItems[index + 1] : null;
            RankingTvRows.Add(new DiscoveryTvRankingRowViewModel(left, right));
        }

        TvRankingSummaryText = BuildTvRankingSummaryText(visibleItems.Count);
        _canGoToNextTvRankingPage = TvRankingPageIndex < TvRankingTotalDisplayPages
                                    && GetTvRankingPageStartIndex(TvRankingPageIndex + 1) < Math.Min(MaxRankingMovies, GetTvRankingDisplayLimit());
        RefreshTvRankingVisibility();
        RefreshRankingCommandState();
        return visibleItems;
    }

    private List<DiscoveryTvSeriesCardViewModel> GetTvRankingDisplayItems(int displayPage)
    {
        var startIndex = GetTvRankingPageStartIndex(displayPage);
        var pageSize = displayPage == 1 ? RankingFirstDisplayPageSize : RankingRegularDisplayPageSize;
        return _rankingTvSeries
            .Skip(startIndex)
            .Take(pageSize)
            .ToList();
    }

    private static int GetTvRankingPageStartIndex(int displayPage)
    {
        return displayPage <= 1
            ? 0
            : RankingFirstDisplayPageSize + (displayPage - 2) * RankingRegularDisplayPageSize;
    }

    private static int GetTvRankingRequiredItemCount(int displayPage)
    {
        if (displayPage <= 1)
        {
            return RankingFirstDisplayPageSize;
        }

        return Math.Min(
            MaxRankingMovies,
            RankingFirstDisplayPageSize + (displayPage - 1) * RankingRegularDisplayPageSize);
    }

    private void UpdateTvRankingTotalPages()
    {
        var displayLimit = GetTvRankingDisplayLimit();
        TvRankingTotalDisplayPages = displayLimit <= RankingFirstDisplayPageSize
            ? 1
            : 1 + (int)Math.Ceiling((displayLimit - RankingFirstDisplayPageSize) / (double)RankingRegularDisplayPageSize);
        _canGoToNextTvRankingPage = TvRankingPageIndex < TvRankingTotalDisplayPages;
        OnPropertyChanged(nameof(CanGoNextTvRankingPage));
        OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
    }

    private int GetTvRankingDisplayLimit()
    {
        if (_tvRankingTotalResults <= 0)
        {
            return MaxRankingMovies;
        }

        return Math.Min(MaxRankingMovies, _tvRankingTotalResults);
    }

    private string BuildTvRankingSummaryText(int visibleCount)
    {
        if (_rankingTvSeries.Count == 0)
        {
            return string.Empty;
        }

        var startRank = GetTvRankingPageStartIndex(TvRankingPageIndex) + 1;
        var endRank = Math.Min(startRank + Math.Max(0, visibleCount) - 1, GetTvRankingDisplayLimit());
        var totalText = _tvRankingTotalResults > 0 ? _tvRankingTotalResults.ToString() : "未知总数";
        return $"当前页显示第 {startRank}-{endRank} 名，已缓存 {_rankingTvSeries.Count} / {totalText}，最多展示前 {MaxRankingMovies} 名";
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

        ApplyExternalTagCacheSnapshots(pageItems);
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

        var hasPersistedRatingChanges = 0;
        using var limiter = new SemaphoreSlim(OmdbResolveConcurrency, OmdbResolveConcurrency);
        var tasks = items.Select(
            async item =>
            {
                await limiter.WaitAsync();
                try
                {
                    if (requestVersion != _rankingRequestVersion || !item.CanEnrichExternalOmdbRating)
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
                        if (await TryRefreshMovieRatingsAsync(item, null))
                        {
                            hasPersistedRatingChanges = 1;
                        }

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
                        if (omdbRating is null && requestVersion == _rankingRequestVersion
                            && await TryRefreshMovieRatingsAsync(item, null))
                        {
                            hasPersistedRatingChanges = 1;
                        }

                        return;
                    }

                    await DispatchAsync(() => item.SetOmdbRating(omdbRating));
                    if (await TryRefreshMovieRatingsAsync(item, omdbRating))
                    {
                        hasPersistedRatingChanges = 1;
                    }
                }
                finally
                {
                    limiter.Release();
                }
            });

        await Task.WhenAll(tasks);
        if (hasPersistedRatingChanges != 0 && requestVersion == _rankingRequestVersion)
        {
            await DispatchAsync(
                () =>
                {
                    _dataRefreshService.NotifyCollectionChanged();
                    _dataRefreshService.NotifyLibraryChanged();
                });
        }
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
        OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
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

    private string GetSelectedTvTrendingWindow()
    {
        return string.Equals(SelectedTvTrendingTime, TrendingTimeWeek, StringComparison.Ordinal)
            ? TrendingWindowWeek
            : TrendingWindowDay;
    }

    private void WriteRankingDiagnostics(string eventName, string details)
    {
        AiPerfDiagnostics.WriteEvent($"event=ranking-{eventName} mediaType={GetActiveRankingDiagnosticsMediaType()} rankingType={GetRankingDiagnosticsType()} {details}");
    }

    private string GetActiveRankingDiagnosticsMediaType()
    {
        return IsTvRankingSelected ? "tv" : "movie";
    }

    private string GetRankingDiagnosticsType()
    {
        var rankingType = IsTvRankingSelected ? SelectedTvRankingType : SelectedRankingType;
        if (string.Equals(rankingType, RankingTypeTopRated, StringComparison.Ordinal))
        {
            return "top-rated";
        }

        if (string.Equals(rankingType, RankingTypeTrending, StringComparison.Ordinal))
        {
            var window = IsTvRankingSelected ? GetSelectedTvTrendingWindow() : GetSelectedTrendingWindow();
            return $"trending-{window}";
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

    private async Task EnsureDiscoveryPreferencesLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isDiscoveryPreferencesLoaded)
        {
            return;
        }

        var preferences = await _discoveryPreferencesService.LoadAsync(cancellationToken);
        IsSearchPosterLayout = !string.Equals(
            preferences.SearchLayoutMode,
            SearchLayoutList,
            StringComparison.OrdinalIgnoreCase);
        _isDiscoveryPreferencesLoaded = true;
    }

    private async Task SaveDiscoveryPreferencesAsync()
    {
        try
        {
            await _discoveryPreferencesService.SaveAsync(CreateDiscoveryPreferencesSnapshot());
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AiPerfDiagnostics.WriteEvent(
                $"discovery-preferences-save-failed errorType={exception.GetType().Name}");
        }
    }

    private DiscoveryPreferencesModel CreateDiscoveryPreferencesSnapshot()
    {
        return new DiscoveryPreferencesModel
        {
            SearchLayoutMode = IsSearchPosterLayout ? SearchLayoutPoster : SearchLayoutList
        };
    }

    private async Task ExecutePageLoadWithRetryAsync<TState>(
        Func<TState> captureState,
        Action<TState> restoreState,
        Func<CancellationToken, Task> loadAsync,
        CancellationToken cancellationToken,
        int attemptTimeoutSeconds = PageLoadAttemptTimeoutSeconds)
    {
        for (var attempt = 1; attempt <= PageLoadMaxRetryAttempts; attempt++)
        {
            var attemptState = captureState();
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(TimeSpan.FromSeconds(attemptTimeoutSeconds));
            try
            {
                await loadAsync(attemptCts.Token);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                restoreState(attemptState);
                if (attempt >= PageLoadMaxRetryAttempts)
                {
                    throw;
                }

                await Task.Delay(PageLoadRetryDelayMilliseconds, cancellationToken);
            }
        }
    }

    private MovieSearchPageState CaptureMovieSearchPageState()
    {
        return new MovieSearchPageState(
            _searchResultPool.ToList(),
            new HashSet<int>(_searchTmdbIds),
            _searchSourceNextPage,
            _searchSourceTotalPages,
            _searchTotalResults,
            _nextSearchOrder,
            _searchSourceExhausted,
            _canGoToNextSearchPage,
            SearchPageIndex,
            SearchTotalPages,
            SearchStatusMessage,
            SearchSummaryText,
            _searchLoadFailed,
            SearchMovies.ToList(),
            SearchMoviePosterScrollOffset,
            SearchMovieListScrollOffset);
    }

    private void RestoreMovieSearchPageState(MovieSearchPageState state, bool restoreDisplay)
    {
        _searchResultPool.Clear();
        _searchResultPool.AddRange(state.ResultPool);
        _searchTmdbIds.Clear();
        _searchTmdbIds.UnionWith(state.TmdbIds);
        _searchSourceNextPage = state.SourceNextPage;
        _searchSourceTotalPages = state.SourceTotalPages;
        _searchTotalResults = state.TotalResults;
        _nextSearchOrder = state.NextOrder;
        _searchSourceExhausted = state.SourceExhausted;
        _canGoToNextSearchPage = state.CanGoNext;
        _searchLoadFailed = state.LoadFailed;

        if (!restoreDisplay)
        {
            return;
        }

        ReplaceObservableItems(SearchMovies, state.VisibleItems);
        SearchPageIndex = state.PageIndex;
        SearchTotalPages = state.TotalPages;
        SearchStatusMessage = state.StatusMessage;
        SearchSummaryText = state.SummaryText;
        SearchMoviePosterScrollOffset = state.PosterScrollOffset;
        SearchMovieListScrollOffset = state.ListScrollOffset;
        RefreshSearchVisibility();
        RefreshSearchCommandState();
    }

    private TvSearchPageState CaptureTvSearchPageState()
    {
        return new TvSearchPageState(
            _tvSearchResultPool.ToList(),
            new HashSet<int>(_tvSearchTmdbIds),
            _tvSearchSourceNextPage,
            _tvSearchSourceTotalPages,
            _tvSearchTotalResults,
            _nextTvSearchOrder,
            _tvSearchSourceExhausted,
            _canGoToNextTvSearchPage,
            TvSearchPageIndex,
            TvSearchTotalPages,
            TvSearchStatusMessage,
            TvSearchSummaryText,
            _tvSearchLoadFailed,
            SearchTvSeries.ToList(),
            SearchTvPosterScrollOffset,
            SearchTvListScrollOffset);
    }

    private void RestoreTvSearchPageState(TvSearchPageState state, bool restoreDisplay)
    {
        _tvSearchResultPool.Clear();
        _tvSearchResultPool.AddRange(state.ResultPool);
        _tvSearchTmdbIds.Clear();
        _tvSearchTmdbIds.UnionWith(state.TmdbIds);
        _tvSearchSourceNextPage = state.SourceNextPage;
        _tvSearchSourceTotalPages = state.SourceTotalPages;
        _tvSearchTotalResults = state.TotalResults;
        _nextTvSearchOrder = state.NextOrder;
        _tvSearchSourceExhausted = state.SourceExhausted;
        _canGoToNextTvSearchPage = state.CanGoNext;
        _tvSearchLoadFailed = state.LoadFailed;

        if (!restoreDisplay)
        {
            return;
        }

        ReplaceObservableItems(SearchTvSeries, state.VisibleItems);
        TvSearchPageIndex = state.PageIndex;
        TvSearchTotalPages = state.TotalPages;
        TvSearchStatusMessage = state.StatusMessage;
        TvSearchSummaryText = state.SummaryText;
        SearchTvPosterScrollOffset = state.PosterScrollOffset;
        SearchTvListScrollOffset = state.ListScrollOffset;
        RefreshTvSearchVisibility();
        RefreshSearchCommandState();
    }

    private MovieRankingPageState CaptureMovieRankingPageState()
    {
        return new MovieRankingPageState(
            _rankingMovies.ToList(),
            new HashSet<int>(_rankingTmdbIds),
            _rankingSourceNextPage,
            _rankingSourceTotalPages,
            _rankingTotalResults,
            _nextRankingRank,
            _rankingSourceExhausted,
            _canGoToNextRankingPage,
            RankingPageIndex,
            RankingTotalDisplayPages,
            RankingStatusMessage,
            RankingSummaryText,
            _rankingLoadFailed,
            RankingMovieScrollOffset);
    }

    private void RestoreMovieRankingPageState(MovieRankingPageState state, bool restoreDisplay)
    {
        _rankingMovies.Clear();
        _rankingMovies.AddRange(state.Items);
        _rankingTmdbIds.Clear();
        _rankingTmdbIds.UnionWith(state.TmdbIds);
        _rankingSourceNextPage = state.SourceNextPage;
        _rankingSourceTotalPages = state.SourceTotalPages;
        _rankingTotalResults = state.TotalResults;
        _nextRankingRank = state.NextRank;
        _rankingSourceExhausted = state.SourceExhausted;
        _canGoToNextRankingPage = state.CanGoNext;
        _rankingLoadFailed = state.LoadFailed;

        if (!restoreDisplay)
        {
            return;
        }

        RankingPageIndex = state.PageIndex;
        RankingTotalDisplayPages = state.TotalPages;
        RankingStatusMessage = state.StatusMessage;
        RankingSummaryText = state.SummaryText;
        RankingMovieScrollOffset = state.ScrollOffset;
        RebuildRankingRows();
        RefreshRankingVisibility();
        RefreshRankingCommandState();
    }

    private TvRankingPageState CaptureTvRankingPageState()
    {
        return new TvRankingPageState(
            _rankingTvSeries.ToList(),
            new HashSet<int>(_rankingTvSeriesTmdbIds),
            _tvRankingSourceNextPage,
            _tvRankingSourceTotalPages,
            _tvRankingTotalResults,
            _nextTvRankingRank,
            _tvRankingSourceExhausted,
            _canGoToNextTvRankingPage,
            TvRankingPageIndex,
            TvRankingTotalDisplayPages,
            TvRankingStatusMessage,
            TvRankingSummaryText,
            _tvRankingLoadFailed,
            RankingTvScrollOffset);
    }

    private void RestoreTvRankingPageState(TvRankingPageState state, bool restoreDisplay)
    {
        _rankingTvSeries.Clear();
        _rankingTvSeries.AddRange(state.Items);
        _rankingTvSeriesTmdbIds.Clear();
        _rankingTvSeriesTmdbIds.UnionWith(state.TmdbIds);
        _tvRankingSourceNextPage = state.SourceNextPage;
        _tvRankingSourceTotalPages = state.SourceTotalPages;
        _tvRankingTotalResults = state.TotalResults;
        _nextTvRankingRank = state.NextRank;
        _tvRankingSourceExhausted = state.SourceExhausted;
        _canGoToNextTvRankingPage = state.CanGoNext;
        _tvRankingLoadFailed = state.LoadFailed;

        if (!restoreDisplay)
        {
            return;
        }

        TvRankingPageIndex = state.PageIndex;
        TvRankingTotalDisplayPages = state.TotalPages;
        TvRankingStatusMessage = state.StatusMessage;
        TvRankingSummaryText = state.SummaryText;
        RankingTvScrollOffset = state.ScrollOffset;
        RebuildTvRankingDisplay();
        RefreshTvRankingVisibility();
        RefreshRankingCommandState();
    }

    private static void ReplaceObservableItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static CancellationTokenSource EnsureActiveCancellationTokenSource(CancellationTokenSource? source)
    {
        if (source is not null)
        {
            try
            {
                if (!source.IsCancellationRequested)
                {
                    return source;
                }
            }
            catch (ObjectDisposedException)
            {
            }

            source.Dispose();
        }

        return new CancellationTokenSource();
    }

    private static string BuildPageRollbackStatus(string reason, int pageIndex)
    {
        reason = NormalizePageRollbackReason(reason);
        return pageIndex > 1
            ? $"{reason}，已回退到第 {pageIndex} 页。"
            : $"{reason}，已回退到当前页。";
    }

    private static string NormalizePageRollbackReason(string reason)
    {
        if (reason.Contains("加载失败", StringComparison.Ordinal)
            && !reason.Contains("请稍后重试", StringComparison.Ordinal))
        {
            return reason.Replace("加载失败", "翻页加载失败，请稍后重试", StringComparison.Ordinal);
        }

        return reason;
    }

    private void ShowPageLoadFailureToast(int displayPage)
    {
        ShowTransientPageMessage($"第 {displayPage} 页加载失败，请稍后重试。", TransientPageMessageKindError);
    }

    private void ShowTransientPageMessage(string message, string kind = TransientPageMessageKindInfo)
    {
        var version = ++_transientPageMessageVersion;
        TransientPageMessageKind = kind;
        TransientPageMessageText = message;
        IsTransientPageMessageVisible = true;
        _ = HideTransientPageMessageAfterDelayAsync(version);
    }

    private async Task HideTransientPageMessageAfterDelayAsync(int version)
    {
        await Task.Delay(PageLoadFailureToastMilliseconds);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            HideTransientPageMessage(version);
            return;
        }

        await dispatcher.InvokeAsync(() => HideTransientPageMessage(version));
    }

    private void HideTransientPageMessage(int version)
    {
        if (version == _transientPageMessageVersion)
        {
            IsTransientPageMessageVisible = false;
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

    private void ResetMovieSearchScrollOffsets()
    {
        SearchMoviePosterScrollOffset = 0;
        SearchMovieListScrollOffset = 0;
    }

    private void ResetTvSearchBuffers()
    {
        _tvSearchResultPool.Clear();
        _tvSearchTmdbIds.Clear();
        _tvSearchSourcePageCache.Clear();
        _tvSearchSourceNextPage = 1;
        _tvSearchSourceTotalPages = 0;
        _tvSearchTotalResults = 0;
        _nextTvSearchOrder = 0;
        _tvSearchSourceExhausted = false;
        _canGoToNextTvSearchPage = false;
    }

    private void ResetTvSearchScrollOffsets()
    {
        SearchTvPosterScrollOffset = 0;
        SearchTvListScrollOffset = 0;
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

    private void ResetRankingMovieScrollOffset()
    {
        RankingMovieScrollOffset = 0;
    }

    private void ResetTvRankingBuffers()
    {
        _rankingTvSeries.Clear();
        _rankingTvSeriesTmdbIds.Clear();
        _rankingTvSeriesSourcePageCache.Clear();
        _tvRankingSourceNextPage = 1;
        _tvRankingSourceTotalPages = 0;
        _tvRankingTotalResults = 0;
        _nextTvRankingRank = 0;
        _tvRankingSourceExhausted = false;
        _canGoToNextTvRankingPage = false;
    }

    private void ResetRankingTvScrollOffset()
    {
        RankingTvScrollOffset = 0;
    }

    private void RefreshSearchVisibility()
    {
        OnPropertyChanged(nameof(HasSearchMovies));
        OnPropertyChanged(nameof(ShowSearchEmptyState));
        OnPropertyChanged(nameof(ShowSearchStatusOverlay));
        OnPropertyChanged(nameof(SearchStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveSearchStatusOverlay));
        OnPropertyChanged(nameof(ActiveSearchStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveSearchRetry));
        OnPropertyChanged(nameof(CanRetryActiveSearch));
        RetrySearchCommand.RaiseCanExecuteChanged();
    }

    private void RefreshTvSearchVisibility()
    {
        OnPropertyChanged(nameof(HasSearchTvSeries));
        OnPropertyChanged(nameof(ShowTvSearchEmptyState));
        OnPropertyChanged(nameof(ShowTvSearchStatusOverlay));
        OnPropertyChanged(nameof(ShowActiveSearchStatusOverlay));
        OnPropertyChanged(nameof(ActiveSearchStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveSearchRetry));
        OnPropertyChanged(nameof(CanRetryActiveSearch));
        RetrySearchCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSearchCommandState()
    {
        RefreshSearchNonSubmitCommandState();
        SearchCommand.RaiseCanExecuteChanged();
        RetrySearchCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSearchNonSubmitCommandState()
    {
        RefreshSearchPageCommandState();
        RefreshSearchFilterCommandState();
    }

    private void RefreshSearchPageCommandState()
    {
        OnPropertyChanged(nameof(CanGoPreviousSearchPage));
        OnPropertyChanged(nameof(CanGoNextSearchPage));
        OnPropertyChanged(nameof(CanGoPreviousTvSearchPage));
        OnPropertyChanged(nameof(CanGoNextTvSearchPage));
        OnPropertyChanged(nameof(CanGoPreviousActiveSearchPage));
        OnPropertyChanged(nameof(CanGoNextActiveSearchPage));
        GoPreviousSearchPageCommand.RaiseCanExecuteChanged();
        GoNextSearchPageCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSearchFilterCommandState()
    {
        OnPropertyChanged(nameof(CanClearSearchFilters));
        ClearSearchFiltersCommand.RaiseCanExecuteChanged();
    }

    private void RefreshRankingVisibility()
    {
        OnPropertyChanged(nameof(HasRankingMovies));
        OnPropertyChanged(nameof(ShowRankingEmptyState));
        OnPropertyChanged(nameof(ShowRankingStatusOverlay));
        OnPropertyChanged(nameof(RankingStatusOverlayText));
        OnPropertyChanged(nameof(ShowTopRankingMovie));
        OnPropertyChanged(nameof(ShowActiveRankingStatusOverlay));
        OnPropertyChanged(nameof(ActiveRankingStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveRankingRetry));
        OnPropertyChanged(nameof(CanRetryActiveRanking));
        RetryRankingCommand.RaiseCanExecuteChanged();
    }

    private void RefreshTvRankingVisibility()
    {
        OnPropertyChanged(nameof(HasRankingTvSeries));
        OnPropertyChanged(nameof(ShowTvRankingEmptyState));
        OnPropertyChanged(nameof(ShowTvRankingStatusOverlay));
        OnPropertyChanged(nameof(ShowTopRankingTvSeries));
        OnPropertyChanged(nameof(ShowActiveRankingStatusOverlay));
        OnPropertyChanged(nameof(ActiveRankingStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveRankingRetry));
        OnPropertyChanged(nameof(CanRetryActiveRanking));
        RetryRankingCommand.RaiseCanExecuteChanged();
    }

    private void RefreshRankingCommandState()
    {
        OnPropertyChanged(nameof(CanGoPreviousRankingPage));
        OnPropertyChanged(nameof(CanGoNextRankingPage));
        OnPropertyChanged(nameof(CanGoPreviousTvRankingPage));
        OnPropertyChanged(nameof(CanGoNextTvRankingPage));
        OnPropertyChanged(nameof(CanGoPreviousActiveRankingPage));
        OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
        OnPropertyChanged(nameof(IsRankingTimeSelectable));
        OnPropertyChanged(nameof(IsTvRankingTimeSelectable));
        OnPropertyChanged(nameof(IsActiveRankingTimeSelectable));
        SelectTrendingTimeCommand.RaiseCanExecuteChanged();
        GoPreviousRankingPageCommand.RaiseCanExecuteChanged();
        GoNextRankingPageCommand.RaiseCanExecuteChanged();
        RetryRankingCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSearchModeProperties()
    {
        OnPropertyChanged(nameof(IsMovieSearchSelected));
        OnPropertyChanged(nameof(IsTvSearchSelected));
        OnPropertyChanged(nameof(SearchMediaTypeButtonText));
        OnPropertyChanged(nameof(SearchTypeOptions));
        OnPropertyChanged(nameof(SearchTypeButtonText));
        OnPropertyChanged(nameof(ActiveSearchSortOptions));
        OnPropertyChanged(nameof(ActiveSelectedSearchSortOption));
        OnPropertyChanged(nameof(SearchSortOptionButtonText));
        OnPropertyChanged(nameof(ActiveSearchSortDirection));
        OnPropertyChanged(nameof(SearchSortDirectionIconData));
        OnPropertyChanged(nameof(SearchSortDirectionButtonToolTip));
        RefreshSearchTypeProperties();
        OnPropertyChanged(nameof(IsActiveSearchLoading));
        OnPropertyChanged(nameof(ActiveSearchStatusMessage));
        OnPropertyChanged(nameof(ActiveSearchSummaryText));
        OnPropertyChanged(nameof(ShowActiveSearchStatusOverlay));
        OnPropertyChanged(nameof(ActiveSearchStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveSearchRetry));
        OnPropertyChanged(nameof(CanRetryActiveSearch));
        OnPropertyChanged(nameof(CanGoPreviousActiveSearchPage));
        OnPropertyChanged(nameof(CanGoNextActiveSearchPage));
        OnPropertyChanged(nameof(ActiveSearchPageStatusText));
        RefreshSearchCommandState();
    }

    private void RefreshSearchTypeProperties()
    {
        OnPropertyChanged(nameof(IsSearchPersonSelected));
        OnPropertyChanged(nameof(SearchTypeButtonText));
        OnPropertyChanged(nameof(SearchPlaceholderText));
        OnPropertyChanged(nameof(SearchInputToolTip));
        RefreshActiveSearchPromptIfIdle();
    }

    private void RefreshActiveSearchPromptIfIdle()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        if (IsTvSearchSelected)
        {
            if (!HasSearchTvSeries && !IsTvSearchLoading)
            {
                TvSearchStatusMessage = BuildSearchPromptMessage();
            }
        }
        else if (!HasSearchMovies && !IsSearchLoading)
        {
            SearchStatusMessage = BuildSearchPromptMessage();
        }
    }

    private void RefreshRankingModeProperties()
    {
        OnPropertyChanged(nameof(IsMovieRankingSelected));
        OnPropertyChanged(nameof(IsTvRankingSelected));
        OnPropertyChanged(nameof(ActiveRankingTitle));
        OnPropertyChanged(nameof(ActiveRankingTimeText));
        OnPropertyChanged(nameof(IsActiveTrendingRanking));
        OnPropertyChanged(nameof(IsActiveRankingTimeSelectable));
        OnPropertyChanged(nameof(IsActiveRankingLoading));
        OnPropertyChanged(nameof(ActiveRankingStatusMessage));
        OnPropertyChanged(nameof(ActiveRankingSummaryText));
        OnPropertyChanged(nameof(ShowActiveRankingStatusOverlay));
        OnPropertyChanged(nameof(ActiveRankingStatusOverlayText));
        OnPropertyChanged(nameof(ShowActiveRankingRetry));
        OnPropertyChanged(nameof(CanRetryActiveRanking));
        OnPropertyChanged(nameof(CanGoPreviousActiveRankingPage));
        OnPropertyChanged(nameof(CanGoNextActiveRankingPage));
        OnPropertyChanged(nameof(ActiveRankingPageStatusText));
        RefreshRankingCommandState();
    }

    private string GetSearchRegionParameter()
    {
        var selectedRegion = _selectedRegionFilters.Count == 1
            ? _selectedRegionFilters.First()
            : SelectedRegionFilter;
        return RegionCountryCodes.TryGetValue(selectedRegion, out var codes)
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

    private static bool MatchesRegion(DiscoveryTvSeriesCardViewModel item, string selectedRegion)
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

    private static bool MatchesLanguage(DiscoveryTvSeriesCardViewModel item, string selectedLanguage)
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

    private static bool MatchesDecade(DiscoveryTvSeriesCardViewModel item, string selectedDecade)
    {
        if (!item.FirstAirYear.HasValue)
        {
            return false;
        }

        if (string.Equals(selectedDecade, DecadeEarlier, StringComparison.Ordinal))
        {
            return item.FirstAirYear.Value < 1960;
        }

        if (!selectedDecade.EndsWith("s", StringComparison.Ordinal)
            || !int.TryParse(selectedDecade.TrimEnd('s'), out var decadeStart))
        {
            return true;
        }

        return item.FirstAirYear.Value >= decadeStart && item.FirstAirYear.Value < decadeStart + 10;
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

    private static string GetOptionValue(object? parameter, string fallback)
    {
        return parameter is string value && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static string BuildFilterButtonText(string label, string selectedValue)
    {
        var value = string.IsNullOrWhiteSpace(selectedValue) ? FilterAll : selectedValue;
        return $"{label}：{value}";
    }

    private static string FormatMultiSelectFilterForButton(
        IReadOnlySet<string> selectedOptions,
        IReadOnlyList<string> optionOrder,
        string allOption)
    {
        if (selectedOptions.Count == 0)
        {
            return allOption;
        }

        var ordered = optionOrder
            .Where(option => !string.Equals(option, allOption, StringComparison.Ordinal)
                             && selectedOptions.Contains(option))
            .ToList();
        return ordered.Count switch
        {
            0 => allOption,
            <= 2 => string.Join(" / ", ordered),
            _ => $"{ordered.Count}项"
        };
    }

    private static void ResetCollectionStatusFiltersToDefault(HashSet<string> selectedOptions)
    {
        selectedOptions.Clear();
        foreach (var status in DefaultCollectionStatusFilters)
        {
            selectedOptions.Add(status);
        }
    }

    private static bool IsDefaultCollectionStatusFilter(IReadOnlySet<string> selectedOptions)
    {
        return selectedOptions.Count == DefaultCollectionStatusFilters.Count
               && DefaultCollectionStatusFilters.All(selectedOptions.Contains);
    }

    private void UpdateDiscoveryMultiSelectFilter(
        string option,
        string allOption,
        IReadOnlyList<string> allOptions,
        HashSet<string> selectedOptions,
        ObservableCollection<DiscoveryFilterOption> menuOptions,
        Action<bool> refreshState,
        bool resetWhenAllNonAllSelected = true)
    {
        var closeMenu = false;
        if (string.Equals(option, allOption, StringComparison.Ordinal))
        {
            selectedOptions.Clear();
            closeMenu = true;
        }
        else if (allOptions.Contains(option, StringComparer.Ordinal))
        {
            if (!selectedOptions.Remove(option))
            {
                selectedOptions.Add(option);
            }
        }

        var nonAllCount = allOptions.Count(value => !string.Equals(value, allOption, StringComparison.Ordinal));
        if (resetWhenAllNonAllSelected && selectedOptions.Count >= nonAllCount)
        {
            selectedOptions.Clear();
            closeMenu = true;
        }

        RefreshDiscoveryFilterOptionSelections(menuOptions, selectedOptions, allOption);
        refreshState(true);
        if (closeMenu)
        {
            RequestCloseFilterMenu?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshSearchGenreFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedGenreFilter,
            FormatMultiSelectFilterForButton(_selectedGenreFilters, GenreFilterOptions, FilterAll),
            nameof(SelectedGenreFilter));
        OnPropertyChanged(nameof(SearchGenreFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(SearchGenreFilterMenuOptions, _selectedGenreFilters, FilterAll);
        if (applyFilters)
        {
            ResetSearchFromFilterChange();
        }
    }

    private void RefreshSearchRegionFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedRegionFilter,
            FormatMultiSelectFilterForButton(_selectedRegionFilters, RegionFilterOptions, FilterAll),
            nameof(SelectedRegionFilter));
        OnPropertyChanged(nameof(SearchRegionFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(SearchRegionFilterMenuOptions, _selectedRegionFilters, FilterAll);
        if (applyFilters)
        {
            ResetSearchFromFilterChange();
        }
    }

    private void RefreshSearchLanguageFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedLanguageFilter,
            FormatMultiSelectFilterForButton(_selectedLanguageFilters, LanguageFilterOptions, FilterAll),
            nameof(SelectedLanguageFilter));
        OnPropertyChanged(nameof(SearchLanguageFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(SearchLanguageFilterMenuOptions, _selectedLanguageFilters, FilterAll);
        if (applyFilters)
        {
            ResetSearchFromFilterChange();
        }
    }

    private void RefreshSearchDecadeFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedDecadeFilter,
            FormatMultiSelectFilterForButton(_selectedDecadeFilters, DecadeFilterOptions, DecadeAll),
            nameof(SelectedDecadeFilter));
        OnPropertyChanged(nameof(SearchDecadeFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(SearchDecadeFilterMenuOptions, _selectedDecadeFilters, DecadeAll);
        if (applyFilters)
        {
            ResetSearchFromFilterChange();
        }
    }

    private void RefreshSearchCollectionStatusFilterState(bool applyFilters)
    {
        SelectedCollectionStatusFilter = FormatMultiSelectFilterForButton(
            _selectedCollectionStatusFilters,
            CollectionStatusFilterOptions,
            FilterAll);
        RefreshDiscoveryFilterOptionSelections(
            SearchCollectionStatusFilterMenuOptions,
            _selectedCollectionStatusFilters,
            FilterAll);
        if (applyFilters)
        {
            ResetSearchFromFilterChange();
        }
    }

    private void RefreshTvSearchGenreFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedTvGenreFilter,
            FormatMultiSelectFilterForButton(_selectedTvGenreFilters, TvGenreFilterOptions, FilterAll),
            nameof(SelectedTvGenreFilter));
        OnPropertyChanged(nameof(TvSearchGenreFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(TvSearchGenreFilterMenuOptions, _selectedTvGenreFilters, FilterAll);
        if (applyFilters)
        {
            ResetTvSearchFromFilterChange();
        }
    }

    private void RefreshTvSearchRegionFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedTvRegionFilter,
            FormatMultiSelectFilterForButton(_selectedTvRegionFilters, RegionFilterOptions, FilterAll),
            nameof(SelectedTvRegionFilter));
        OnPropertyChanged(nameof(TvSearchRegionFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(TvSearchRegionFilterMenuOptions, _selectedTvRegionFilters, FilterAll);
        if (applyFilters)
        {
            ResetTvSearchFromFilterChange();
        }
    }

    private void RefreshTvSearchLanguageFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedTvLanguageFilter,
            FormatMultiSelectFilterForButton(_selectedTvLanguageFilters, LanguageFilterOptions, FilterAll),
            nameof(SelectedTvLanguageFilter));
        OnPropertyChanged(nameof(TvSearchLanguageFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(TvSearchLanguageFilterMenuOptions, _selectedTvLanguageFilters, FilterAll);
        if (applyFilters)
        {
            ResetTvSearchFromFilterChange();
        }
    }

    private void RefreshTvSearchDecadeFilterState(bool applyFilters)
    {
        SetProperty(
            ref _selectedTvDecadeFilter,
            FormatMultiSelectFilterForButton(_selectedTvDecadeFilters, DecadeFilterOptions, DecadeAll),
            nameof(SelectedTvDecadeFilter));
        OnPropertyChanged(nameof(TvSearchDecadeFilterButtonText));
        RefreshDiscoveryFilterOptionSelections(TvSearchDecadeFilterMenuOptions, _selectedTvDecadeFilters, DecadeAll);
        if (applyFilters)
        {
            ResetTvSearchFromFilterChange();
        }
    }

    private void RefreshTvSearchCollectionStatusFilterState(bool applyFilters)
    {
        SelectedTvCollectionStatusFilter = FormatMultiSelectFilterForButton(
            _selectedTvCollectionStatusFilters,
            CollectionStatusFilterOptions,
            FilterAll);
        RefreshDiscoveryFilterOptionSelections(
            TvSearchCollectionStatusFilterMenuOptions,
            _selectedTvCollectionStatusFilters,
            FilterAll);
        if (applyFilters)
        {
            ResetTvSearchFromFilterChange();
        }
    }

    private static void ReplaceDiscoveryFilterOptions(
        ObservableCollection<DiscoveryFilterOption> target,
        IReadOnlyList<string> source)
    {
        target.Clear();
        foreach (var label in source)
        {
            target.Add(new DiscoveryFilterOption(label));
        }
    }

    private static void RefreshDiscoveryFilterOptionSelections(
        ObservableCollection<DiscoveryFilterOption> options,
        IReadOnlySet<string> selectedOptions,
        string allOption)
    {
        foreach (var option in options)
        {
            option.RefreshSelection(string.Equals(option.Label, allOption, StringComparison.Ordinal)
                ? selectedOptions.Count == 0
                : selectedOptions.Contains(option.Label));
        }
    }

    private static string ToggleSortDirectionValue(string value)
    {
        return string.Equals(value, DirectionDescending, StringComparison.Ordinal)
            ? DirectionAscending
            : DirectionDescending;
    }

    private string GetTitleSearchTypeForActiveMedia()
    {
        return IsTvSearchSelected ? SearchTypeTvTitle : SearchTypeMovieTitle;
    }

    private static bool MatchesMovieCollectionStatusFilter(DiscoveryMovieCardViewModel item, string filter)
    {
        return filter switch
        {
            CollectionStatusFavorite => item.IsFavorite,
            CollectionStatusWantToWatch => item.IsWantToWatch,
            CollectionStatusNotInterested => item.IsNotInterested,
            CollectionStatusOther => !item.IsFavorite && !item.IsWantToWatch && !item.IsNotInterested,
            _ => true
        };
    }

    private static bool MatchesTvCollectionStatusFilter(DiscoveryTvSeriesCardViewModel item, string filter)
    {
        return filter switch
        {
            CollectionStatusFavorite => item.HasFavoriteSeason,
            CollectionStatusWantToWatch => item.HasWantToWatchSeason,
            CollectionStatusNotInterested => item.HasNotInterestedSeason,
            CollectionStatusOther => !item.HasFavoriteSeason && !item.HasWantToWatchSeason && !item.HasNotInterestedSeason,
            _ => true
        };
    }

    private string BuildSearchPromptMessage()
    {
        if (IsSearchPersonSelected)
        {
            return IsTvSearchSelected
                ? "输入导演/演员后搜索 TMDB 电视剧。"
                : "输入导演/演员后搜索 TMDB 电影。";
        }

        return IsTvSearchSelected
            ? "输入电视剧名后搜索 TMDB 电视剧。"
            : "输入电影名后搜索 TMDB 电影。";
    }

    private string BuildMissingSearchKeywordMessage()
    {
        if (IsSearchPersonSelected)
        {
            return "请输入需要搜索的导演/演员。";
        }

        return IsTvSearchSelected
            ? "请输入需要搜索的电视剧名。"
            : "请输入需要搜索的电影。";
    }

    private string BuildMovieSearchLoadingMessage()
    {
        if (CanUseMovieDiscoverSearch())
        {
            return $"正在按{SelectedSortOption}发现电影...";
        }

        return IsSearchPersonSelected
            ? "正在按导演/演员搜索电影..."
            : "正在搜索电影...";
    }

    private string BuildTvSearchLoadingMessage()
    {
        if (CanUseTvDiscoverSearch())
        {
            return $"正在按{SelectedTvSortOption}发现电视剧...";
        }

        return IsSearchPersonSelected
            ? "正在按导演/演员搜索电视剧..."
            : "正在搜索电视剧...";
    }

    private string BuildMovieSearchNoResultsMessage()
    {
        if (CanUseMovieDiscoverSearch())
        {
            return "未找到符合当前筛选的电影。";
        }

        return IsSearchPersonSelected
            ? "未找到该人物相关电影。"
            : "未找到相关电影。";
    }

    private string BuildTvSearchNoResultsMessage()
    {
        if (CanUseTvDiscoverSearch())
        {
            return "未找到符合当前筛选的电视剧。";
        }

        return IsSearchPersonSelected
            ? "未找到该人物相关电视剧。"
            : "未找到相关电视剧。";
    }

    private string BuildMovieSearchLoadedMessage()
    {
        if (CanUseMovieDiscoverSearch())
        {
            return "电影发现结果已加载。";
        }

        return IsSearchPersonSelected
            ? "电影人物搜索结果已加载。"
            : "电影搜索结果已加载。";
    }

    private string BuildTvSearchLoadedMessage()
    {
        if (CanUseTvDiscoverSearch())
        {
            return "电视剧发现结果已加载。";
        }

        return IsSearchPersonSelected
            ? "电视剧人物搜索结果已加载。"
            : "电视剧搜索结果已加载。";
    }

    private static string FormatTotalResults(int totalResults)
    {
        return totalResults > 0 ? totalResults.ToString() : "未知总数";
    }

    private static string DescribeException(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return "请求超时";
        }

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

    private sealed record MovieSearchPageState(
        List<DiscoveryMovieCardViewModel> ResultPool,
        HashSet<int> TmdbIds,
        int SourceNextPage,
        int SourceTotalPages,
        int TotalResults,
        int NextOrder,
        bool SourceExhausted,
        bool CanGoNext,
        int PageIndex,
        int TotalPages,
        string StatusMessage,
        string SummaryText,
        bool LoadFailed,
        List<DiscoveryMovieCardViewModel> VisibleItems,
        double PosterScrollOffset,
        double ListScrollOffset);

    private sealed record TvSearchPageState(
        List<DiscoveryTvSeriesCardViewModel> ResultPool,
        HashSet<int> TmdbIds,
        int SourceNextPage,
        int SourceTotalPages,
        int TotalResults,
        int NextOrder,
        bool SourceExhausted,
        bool CanGoNext,
        int PageIndex,
        int TotalPages,
        string StatusMessage,
        string SummaryText,
        bool LoadFailed,
        List<DiscoveryTvSeriesCardViewModel> VisibleItems,
        double PosterScrollOffset,
        double ListScrollOffset);

    private sealed record MovieRankingPageState(
        List<DiscoveryMovieCardViewModel> Items,
        HashSet<int> TmdbIds,
        int SourceNextPage,
        int SourceTotalPages,
        int TotalResults,
        int NextRank,
        bool SourceExhausted,
        bool CanGoNext,
        int PageIndex,
        int TotalPages,
        string StatusMessage,
        string SummaryText,
        bool LoadFailed,
        double ScrollOffset);

    private sealed record TvRankingPageState(
        List<DiscoveryTvSeriesCardViewModel> Items,
        HashSet<int> TmdbIds,
        int SourceNextPage,
        int SourceTotalPages,
        int TotalResults,
        int NextRank,
        bool SourceExhausted,
        bool CanGoNext,
        int PageIndex,
        int TotalPages,
        string StatusMessage,
        string SummaryText,
        bool LoadFailed,
        double ScrollOffset);

    public sealed class DiscoveryFilterOption : ObservableObject
    {
        private bool _isSelected;

        public DiscoveryFilterOption(string label)
        {
            Label = label;
        }

        public string Label { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public void RefreshSelection(bool isSelected)
        {
            if (!SetProperty(ref _isSelected, isSelected))
            {
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
}
