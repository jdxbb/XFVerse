using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Models.Library;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.App.ViewModels.Collections;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class LibraryViewModel : PageViewModelBase
{
    private const string FilterAll = "全部";
    private const string WatchedFilterWatched = "已看";
    private const string WatchedFilterUnwatched = "未看";
    private const string WatchedFilterNotInterested = "不想看";
    private const string LibraryScopeAll = "全部";
    private const string SourceFilterAll = "全部来源";
    private const string SourceFilterLocal = "本地";
    private const string SourceFilterWebDav = "网盘";
    private const string CollectionStatusFavorite = "喜爱";
    private const string CollectionStatusWantToWatch = "想看";
    private const string CollectionStatusNotInterested = "不想看";
    private const string TagCategoryType = "类型标签";
    private const string TagCategoryEmotion = "情绪标签";
    private const string TagCategoryScene = "场景标签";
    private const string StatusMatched = "自动匹配";
    private const string StatusNeedsReview = "待人工确认";
    private const string StatusManualConfirmed = "手动确认";
    private const string StatusFailed = "识别失败";
    private const string StatusPending = "未识别";
    private const string DecadeAll = "全部年代";
    private const string LibraryScopeWithSource = "有播放源";
    private const string LibraryScopeWithoutSource = "无播放源";
    private const string RefreshReasonActivate = "activate";
    private const string RefreshReasonManual = "manual-refresh";
    private const string RefreshReasonBatchModeChanged = "batch-mode-changed";
    private const string RefreshReasonRemovedLibraryChanged = "operation-removed-library-changed";
    private const string RefreshReasonBatchStatusChanged = "operation-batch-status-changed";
    private const string RefreshReasonBatchRemoveFromLibrary = "operation-batch-remove-from-library";
    private const string RefreshReasonBatchDeleteRecords = "operation-batch-delete-records";
    private const string RefreshReasonManualAggregation = "operation-manual-aggregation";
    private const string RefreshReasonBatchAiExitBatchMode = "operation-batch-ai-exit-batch-mode";
    private const string RefreshReasonBatchAiResult = "operation-batch-ai-result";
    private const string LibraryLayoutPoster = "poster";
    private const string LibraryLayoutList = "list";
    private static readonly TimeSpan RefreshDebounceDelay = TimeSpan.FromMilliseconds(200);
    private static readonly string[] TypeTagLabels =
    [
        "动作", "冒险", "动画", "喜剧", "犯罪", "纪录片", "剧情", "家庭", "奇幻", "历史",
        "恐怖", "音乐", "悬疑", "爱情", "科幻", "电视电影", "惊悚", "战争", "西部", "传记",
        "运动", "歌舞", "灾难", "武侠", "古装"
    ];

    private static readonly string[] EmotionTagLabels =
    [
        "治愈", "温暖", "感动", "轻松", "欢乐", "浪漫", "热血", "紧张", "悬疑", "压抑",
        "沉重", "震撼", "孤独", "荒诞", "黑色幽默", "催泪", "励志", "思考向", "爽感", "惊悚",
        "梦幻", "怀旧", "燃", "克制", "讽刺", "黑暗", "温柔"
    ];

    private static readonly string[] SceneTagLabels =
    [
        "独自观看", "情侣", "朋友", "亲子", "家人", "深夜", "放松", "下饭", "周末", "聚会",
        "高专注", "背景播放", "二刷", "影院感", "通勤", "短时观看", "长片沉浸", "节日", "雨天", "睡前"
    ];

    private readonly ILibraryQueryService _libraryQueryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IMovieManagementService _movieManagementService;
    private readonly IMovieIdentificationService _movieIdentificationService;
    private readonly IBatchAiCorrectionService _batchAiCorrectionService;
    private readonly IUserCollectionService _userCollectionService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IManualUnknownSeasonAggregationService _manualUnknownSeasonAggregationService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly ILibraryPreferencesService _libraryPreferencesService;
    private readonly List<LibraryMovieListItem> _allMovies = [];
    private readonly HashSet<string> _selectedItemKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _refreshGate = new();
    private readonly HashSet<string> _queuedRefreshReasons = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _refreshDebounceCts;
    private Task? _refreshLoopTask;
    private CancellationTokenSource? _batchIdentifyCancellationTokenSource;
    private bool _isActive;
    private bool _isRefreshRunning;
    private bool _pendingRefresh;
    private bool _queuedRefreshWasDebounced;
    private bool _dirtyWhileInactive;
    private bool _suppressLibraryRefreshFromBatchNotification;
    private long _refreshSequence;
    private string _searchText = string.Empty;
    private string _submittedSearchText = string.Empty;
    private string _genreFilterText = string.Empty;
    private string _selectedSortOption = "最近更新";
    private string _selectedSortDirection = "降序";
    private string _selectedStatusFilter = FilterAll;
    private string _selectedWatchedFilter = FilterAll;
    private string _selectedLibraryScope = LibraryScopeAll;
    private string _selectedSourceFilter = SourceFilterAll;
    private string _selectedCollectionStatusFilter = FilterAll;
    private string _selectedContentTypeFilter = "全部";
    private string _selectedDecadeFilter = DecadeAll;
    private bool _isUpdatingTagSelection;
    private bool _isPosterView = true;
    private bool _isApplyingLayoutPreference;
    private bool _hasUserChangedLayoutThisSession;
    private bool _isBatchSelectionMode;
    private bool _isBatchOperationRunning;
    private bool _isRemovedLibraryPanelOpen;
    private bool _isRemovedLibraryLoading;
    private bool _isManualAggregationDialogOpen;
    private bool _isManualAggregationBusy;
    private string _statusMessage = "媒体库会展示已识别的真实影片数据。";
    private string _batchResultSummary = string.Empty;
    private string _manualAggregationSeriesTitle = string.Empty;
    private string _manualAggregationSeasonTitle = string.Empty;
    private string _manualAggregationSeasonNumberText = "1";
    private string _manualAggregationStatusMessage = "请选择多个未识别播放源后聚合为新的未识别季。";
    private string _removedLibraryStatusMessage = "已移出媒体库项目会保留状态和播放源。";

    public LibraryViewModel(
        ILibraryQueryService libraryQueryService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService,
        IMovieManagementService movieManagementService,
        IMovieIdentificationService movieIdentificationService,
        IBatchAiCorrectionService batchAiCorrectionService,
        IUserCollectionService userCollectionService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IManualUnknownSeasonAggregationService manualUnknownSeasonAggregationService,
        ILibraryPreferencesService libraryPreferencesService,
        IConfirmationDialogService confirmationDialogService)
        : base("媒体库", "查看你的影片资源")
    {
        _libraryQueryService = libraryQueryService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _movieManagementService = movieManagementService;
        _movieIdentificationService = movieIdentificationService;
        _batchAiCorrectionService = batchAiCorrectionService;
        _userCollectionService = userCollectionService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _manualUnknownSeasonAggregationService = manualUnknownSeasonAggregationService;
        _libraryPreferencesService = libraryPreferencesService;
        _confirmationDialogService = confirmationDialogService;
        _dataRefreshService.DataChanged += OnDataChanged;

        SortOptions = ["最近更新", "标题", "年份", "评分"];
        SortDirectionOptions = ["降序", "升序"];
        StatusOptions = [FilterAll, StatusMatched, StatusNeedsReview, StatusManualConfirmed, StatusFailed, StatusPending];

        WatchedFilterOptions = [FilterAll, WatchedFilterWatched, WatchedFilterUnwatched, WatchedFilterNotInterested];
        LibraryScopeOptions = [LibraryScopeAll, LibraryScopeWithSource, LibraryScopeWithoutSource];
        SourceFilterOptions = [SourceFilterAll, SourceFilterLocal, SourceFilterWebDav];
        CollectionStatusOptions = [FilterAll, CollectionStatusFavorite, CollectionStatusWantToWatch, CollectionStatusNotInterested];
        ContentTypeOptions = ["全部", "电影", "电视剧", "其他"];
        SwitchToPosterViewCommand = new RelayCommand(() => SetLibraryLayout(isPosterView: true));
        SwitchToListViewCommand = new RelayCommand(() => SetLibraryLayout(isPosterView: false));
        SelectLibraryScopeCommand = new RelayCommand(SelectLibraryScope);
        SelectSourceFilterCommand = new RelayCommand(SelectSourceFilter);
        SelectContentTypeCommand = new RelayCommand(SelectContentType);
        ClearTagFilterCommand = new RelayCommand(ClearTagFilter);
        SelectCollectionStatusCommand = new RelayCommand(SelectCollectionStatus);
        OpenMovieCommand = new RelayCommand(OpenMovie);
        OpenOrToggleSelectionCommand = new RelayCommand(OpenOrToggleSelection);
        ToggleItemSelectionCommand = new RelayCommand(ToggleItemSelection);
        ToggleBatchSelectionModeCommand = new RelayCommand(ToggleBatchSelectionMode, () => !IsBatchOperationRunning);
        SelectVisibleItemsCommand = new RelayCommand(SelectVisibleItems, () => CanSelectVisibleItems);
        ClearBatchSelectionCommand = new RelayCommand(ClearBatchSelection, () => CanClearBatchSelection);
        BatchMarkWatchedCommand = new AsyncRelayCommand(() => BatchSetWatchedAsync(true), () => CanBatchMarkWatched);
        BatchMarkUnwatchedCommand = new AsyncRelayCommand(() => BatchSetWatchedAsync(false), () => CanBatchMarkUnwatched);
        BatchAutoIdentifyCommand = new AsyncRelayCommand(BatchAutoIdentifyCrossTypeAsync, () => CanBatchAutoIdentify);
        CancelBatchOperationCommand = new RelayCommand(CancelBatchOperation, () => CanCancelBatchOperation);
        BatchRemoveFromLibraryCommand = new AsyncRelayCommand(BatchRemoveFromLibraryAsync, () => CanBatchRemoveFromLibrary);
        BatchDeleteMovieRecordsCommand = new AsyncRelayCommand(BatchDeleteMovieRecordsAsync, () => CanBatchDeleteMovieRecords);
        OpenManualAggregationCommand = new AsyncRelayCommand(OpenManualAggregationAsync, () => CanBatchManualAggregate);
        ApplyManualAggregationCommand = new AsyncRelayCommand(ApplyManualAggregationAsync, () => CanApplyManualAggregation);
        ApplyManualAggregationAndIdentifyCommand = new AsyncRelayCommand(ApplyManualAggregationAndIdentifyAsync, () => CanApplyManualAggregation);
        CancelManualAggregationCommand = new RelayCommand(CancelManualAggregation, () => IsManualAggregationDialogOpen && !IsManualAggregationBusy);
        OpenRemovedLibraryCommand = new AsyncRelayCommand(OpenRemovedLibraryAsync, () => !IsRemovedLibraryLoading);
        CloseRemovedLibraryCommand = new RelayCommand(CloseRemovedLibrary);
        RestoreRemovedLibraryItemCommand = new AsyncRelayCommand(RestoreRemovedLibraryItemAsync, _ => !IsRemovedLibraryLoading);
        DeleteRemovedLibraryItemCommand = new AsyncRelayCommand(DeleteRemovedLibraryItemAsync, _ => !IsRemovedLibraryLoading);
        RestoreRemovedLibraryGroupCommand = new AsyncRelayCommand(RestoreRemovedLibraryGroupAsync, parameter => !IsRemovedLibraryLoading && parameter is RemovedLibraryGroupViewModel { IsTvGroup: true });
        DeleteRemovedLibraryGroupCommand = new AsyncRelayCommand(DeleteRemovedLibraryGroupAsync, parameter => !IsRemovedLibraryLoading && parameter is RemovedLibraryGroupViewModel { IsTvGroup: true });
        OpenRemovedLibraryDetailCommand = new RelayCommand(OpenMovie);
        RefreshCommand = new AsyncRelayCommand(() => RequestLibraryRefreshAsync(RefreshReasonManual, debounce: false, allowWhenInactive: true));
        ApplySearchCommand = new RelayCommand(SubmitSearch);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        RefreshTagOptions();
        _ = LoadLibraryPreferencesAsync();
    }

    public BulkObservableCollection<LibraryMovieItemViewModel> Movies { get; } = [];

    public ObservableCollection<LibraryMovieItemViewModel> RemovedLibraryItems { get; } = [];

    public ObservableCollection<RemovedLibraryGroupViewModel> RemovedLibraryGroups { get; } = [];

    public ObservableCollection<ManualUnknownSeasonAggregationSourceRowViewModel> ManualAggregationSources { get; } = [];

    public ObservableCollection<TagFilterOption> TypeTagOptions { get; } = [];

    public ObservableCollection<TagFilterOption> EmotionTagOptions { get; } = [];

    public ObservableCollection<TagFilterOption> SceneTagOptions { get; } = [];

    public ObservableCollection<string> DecadeFilterOptions { get; } = [];

    public IReadOnlyList<string> SortOptions { get; }

    public IReadOnlyList<string> SortDirectionOptions { get; }

    public IReadOnlyList<string> StatusOptions { get; }

    public IReadOnlyList<string> WatchedFilterOptions { get; }

    public IReadOnlyList<string> LibraryScopeOptions { get; }

    public IReadOnlyList<string> SourceFilterOptions { get; }

    public IReadOnlyList<string> CollectionStatusOptions { get; }

    public IReadOnlyList<string> ContentTypeOptions { get; }

    public RelayCommand SwitchToPosterViewCommand { get; }

    public RelayCommand SwitchToListViewCommand { get; }

    public RelayCommand SelectLibraryScopeCommand { get; }

    public RelayCommand SelectSourceFilterCommand { get; }

    public RelayCommand SelectContentTypeCommand { get; }

    public RelayCommand ClearTagFilterCommand { get; }

    public RelayCommand SelectCollectionStatusCommand { get; }

    public RelayCommand OpenMovieCommand { get; }

    public RelayCommand OpenOrToggleSelectionCommand { get; }

    public RelayCommand ToggleItemSelectionCommand { get; }

    public RelayCommand ToggleBatchSelectionModeCommand { get; }

    public RelayCommand SelectVisibleItemsCommand { get; }

    public RelayCommand ClearBatchSelectionCommand { get; }

    public AsyncRelayCommand BatchMarkWatchedCommand { get; }

    public AsyncRelayCommand BatchMarkUnwatchedCommand { get; }

    public AsyncRelayCommand BatchAutoIdentifyCommand { get; }

    public RelayCommand CancelBatchOperationCommand { get; }

    public AsyncRelayCommand BatchRemoveFromLibraryCommand { get; }

    public AsyncRelayCommand BatchDeleteMovieRecordsCommand { get; }

    public AsyncRelayCommand OpenManualAggregationCommand { get; }

    public AsyncRelayCommand ApplyManualAggregationCommand { get; }

    public AsyncRelayCommand ApplyManualAggregationAndIdentifyCommand { get; }

    public RelayCommand CancelManualAggregationCommand { get; }

    public AsyncRelayCommand OpenRemovedLibraryCommand { get; }

    public RelayCommand CloseRemovedLibraryCommand { get; }

    public AsyncRelayCommand RestoreRemovedLibraryItemCommand { get; }

    public AsyncRelayCommand DeleteRemovedLibraryItemCommand { get; }

    public AsyncRelayCommand RestoreRemovedLibraryGroupCommand { get; }

    public AsyncRelayCommand DeleteRemovedLibraryGroupCommand { get; }

    public RelayCommand OpenRemovedLibraryDetailCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public RelayCommand ApplySearchCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public RelayCommand ClearFiltersCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
        }
    }

    public string GenreFilterText
    {
        get => _genreFilterText;
        set
        {
            if (SetProperty(ref _genreFilterText, value))
            {
                ApplyFilters();
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
                ApplyFilters();
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
                ApplyFilters();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedWatchedFilter
    {
        get => _selectedWatchedFilter;
        set
        {
            if (SetProperty(ref _selectedWatchedFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedLibraryScope
    {
        get => _selectedLibraryScope;
        set
        {
            if (SetProperty(ref _selectedLibraryScope, value))
            {
                OnPropertyChanged(nameof(LibraryScopeMenuHeader));
                ApplyFilters();
            }
        }
    }

    public string SelectedSourceFilter
    {
        get => _selectedSourceFilter;
        set
        {
            if (SetProperty(ref _selectedSourceFilter, value))
            {
                OnPropertyChanged(nameof(LibraryScopeMenuHeader));
                ApplyFilters();
            }
        }
    }

    public string SelectedCollectionStatusFilter
    {
        get => _selectedCollectionStatusFilter;
        set
        {
            if (SetProperty(ref _selectedCollectionStatusFilter, value))
            {
                OnPropertyChanged(nameof(CollectionStatusMenuHeader));
                ApplyFilters();
            }
        }
    }

    public string SelectedContentTypeFilter
    {
        get => _selectedContentTypeFilter;
        set
        {
            if (!ContentTypeOptions.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _selectedContentTypeFilter, value))
            {
                ApplyFilters();
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
                ApplyFilters();
            }
        }
    }

    public string LibraryScopeMenuHeader => SelectedSourceFilter == SourceFilterAll
        ? $"播放源状态：{SelectedLibraryScope}"
        : $"播放源状态：{SelectedLibraryScope} / {SelectedSourceFilter}";

    public string TagFilterMenuHeader
    {
        get
        {
            var selectedTags = Enumerable.Empty<string>()
                .Concat(TypeTagOptions.Where(option => option.IsSelected).Select(option => $"类型 {option.Label}"))
                .Concat(EmotionTagOptions.Where(option => option.IsSelected).Select(option => $"情绪 {option.Label}"))
                .Concat(SceneTagOptions.Where(option => option.IsSelected).Select(option => $"场景 {option.Label}"))
                .ToList();
            return selectedTags.Count == 0 ? "标签：全部" : $"标签：{string.Join(" / ", selectedTags)}";
        }
    }

    public string CollectionStatusMenuHeader => $"收藏状态：{SelectedCollectionStatusFilter}";

    public bool IsPosterView
    {
        get => _isPosterView;
        set
        {
            if (SetProperty(ref _isPosterView, value))
            {
                OnPropertyChanged(nameof(IsListView));
                if (!_isApplyingLayoutPreference)
                {
                    _ = SaveLibraryPreferencesAsync();
                }
            }
        }
    }

    public bool IsListView => !IsPosterView;

    public bool IsBatchSelectionMode
    {
        get => _isBatchSelectionMode;
        private set
        {
            if (SetProperty(ref _isBatchSelectionMode, value))
            {
                foreach (var movie in Movies)
                {
                    movie.IsBatchSelectionMode = value;
                }

                OnPropertyChanged(nameof(BatchSelectionButtonText));
                RefreshBatchCommandState();
            }
        }
    }

    public bool IsBatchOperationRunning
    {
        get => _isBatchOperationRunning;
        private set
        {
            if (SetProperty(ref _isBatchOperationRunning, value))
            {
                RefreshBatchCommandState();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string BatchResultSummary
    {
        get => _batchResultSummary;
        private set
        {
            if (SetProperty(ref _batchResultSummary, value))
            {
                OnPropertyChanged(nameof(HasBatchResultSummary));
            }
        }
    }

    public bool HasBatchResultSummary => !string.IsNullOrWhiteSpace(BatchResultSummary);

    public bool HasMovies => Movies.Count > 0;

    public string EmptyStateTitle => _allMovies.Count == 0
        ? "媒体库暂无内容"
        : "没有匹配结果";

    public string EmptyStateMessage
    {
        get
        {
            if (_allMovies.Count == 0)
            {
                return "请先到扫描任务页执行扫描，或检查媒体源配置。";
            }

            if (SelectedLibraryScope == LibraryScopeWithoutSource)
            {
                return "没有找到无播放源项目。";
            }

            return "没有找到符合当前条件的媒体。";
        }
    }

    public int SelectedCount => _selectedItemKeys.Count;

    public bool HasSelection => SelectedCount > 0;

    public string BatchSelectionButtonText => IsBatchSelectionMode ? "完成" : "批量选择";

    public bool CanSelectVisibleItems => IsBatchSelectionMode && Movies.Count > 0 && !IsBatchOperationRunning;

    public bool CanClearBatchSelection => IsBatchSelectionMode && HasSelection && !IsBatchOperationRunning;

    public bool CanBatchMarkWatched => IsBatchSelectionMode && HasSelection && !IsBatchOperationRunning;

    public bool CanBatchMarkUnwatched => IsBatchSelectionMode && HasSelection && !IsBatchOperationRunning;

    public bool CanBatchAutoIdentify => IsBatchSelectionMode && HasSelection && !IsBatchOperationRunning;

    public bool CanBatchRemoveFromLibrary => IsBatchSelectionMode && HasSelection && !IsBatchOperationRunning;

    public bool CanBatchDeleteMovieRecords => IsBatchSelectionMode && HasSelection && !IsBatchOperationRunning;

    public bool CanBatchManualAggregate => IsBatchSelectionMode
                                           && HasSelection
                                           && !IsBatchOperationRunning
                                           && !IsManualAggregationBusy
                                           && GetSelectedVisibleItems().Count > 0
                                           && GetSelectedVisibleItems().All(CanUseForManualUnknownSeasonAggregation);

    public bool IsManualAggregationDialogOpen
    {
        get => _isManualAggregationDialogOpen;
        private set
        {
            if (SetProperty(ref _isManualAggregationDialogOpen, value))
            {
                OnPropertyChanged(nameof(CanApplyManualAggregation));
                ApplyManualAggregationCommand.RaiseCanExecuteChanged();
                ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
                CancelManualAggregationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsManualAggregationBusy
    {
        get => _isManualAggregationBusy;
        private set
        {
            if (SetProperty(ref _isManualAggregationBusy, value))
            {
                OnPropertyChanged(nameof(CanBatchManualAggregate));
                OnPropertyChanged(nameof(CanApplyManualAggregation));
                OpenManualAggregationCommand.RaiseCanExecuteChanged();
                ApplyManualAggregationCommand.RaiseCanExecuteChanged();
                ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
                CancelManualAggregationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ManualAggregationSeriesTitle
    {
        get => _manualAggregationSeriesTitle;
        set
        {
            if (SetProperty(ref _manualAggregationSeriesTitle, value))
            {
                OnPropertyChanged(nameof(CanApplyManualAggregation));
                ApplyManualAggregationCommand.RaiseCanExecuteChanged();
                ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ManualAggregationSeasonTitle
    {
        get => _manualAggregationSeasonTitle;
        set
        {
            if (SetProperty(ref _manualAggregationSeasonTitle, value))
            {
                OnPropertyChanged(nameof(CanApplyManualAggregation));
                OnPropertyChanged(nameof(ManualAggregationSeasonDisplayText));
                ApplyManualAggregationCommand.RaiseCanExecuteChanged();
                ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ManualAggregationSeasonNumberText
    {
        get => _manualAggregationSeasonNumberText;
        set
        {
            if (SetProperty(ref _manualAggregationSeasonNumberText, value))
            {
                OnPropertyChanged(nameof(CanApplyManualAggregation));
                OnPropertyChanged(nameof(ManualAggregationSeasonDisplayText));
                ApplyManualAggregationCommand.RaiseCanExecuteChanged();
                ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ManualAggregationSeasonDisplayText
    {
        get
        {
            var seasonNumber = ManualAggregationSeasonNumber;
            var seasonText = seasonNumber.HasValue ? $"S{seasonNumber.Value:00}" : "S?";
            return string.IsNullOrWhiteSpace(ManualAggregationSeasonTitle)
                ? seasonText
                : $"{seasonText} {ManualAggregationSeasonTitle.Trim()}";
        }
    }

    private int? ManualAggregationSeasonNumber => int.TryParse(ManualAggregationSeasonNumberText, out var value) && value >= 0
        ? value
        : null;

    public string ManualAggregationStatusMessage
    {
        get => _manualAggregationStatusMessage;
        private set => SetProperty(ref _manualAggregationStatusMessage, value);
    }

    public bool HasManualAggregationSources => ManualAggregationSources.Count > 0;

    public bool CanApplyManualAggregation => IsManualAggregationDialogOpen
                                             && !IsManualAggregationBusy
                                             && ManualAggregationSources.Count > 0;

    public bool CanCancelBatchOperation => IsBatchOperationRunning
                                           && _batchIdentifyCancellationTokenSource is not null
                                           && !_batchIdentifyCancellationTokenSource.IsCancellationRequested;

    public bool IsRemovedLibraryPanelOpen
    {
        get => _isRemovedLibraryPanelOpen;
        private set => SetProperty(ref _isRemovedLibraryPanelOpen, value);
    }

    public bool IsRemovedLibraryLoading
    {
        get => _isRemovedLibraryLoading;
        private set
        {
            if (SetProperty(ref _isRemovedLibraryLoading, value))
            {
                OpenRemovedLibraryCommand.RaiseCanExecuteChanged();
                RestoreRemovedLibraryItemCommand.RaiseCanExecuteChanged();
                DeleteRemovedLibraryItemCommand.RaiseCanExecuteChanged();
                RestoreRemovedLibraryGroupCommand.RaiseCanExecuteChanged();
                DeleteRemovedLibraryGroupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RemovedLibraryStatusMessage
    {
        get => _removedLibraryStatusMessage;
        private set => SetProperty(ref _removedLibraryStatusMessage, value);
    }

    public bool HasRemovedLibraryItems => RemovedLibraryGroups.Count > 0;

    public override Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        lock (_refreshGate)
        {
            _isActive = true;
        }

        return RequestLibraryRefreshAsync(RefreshReasonActivate, debounce: false, allowWhenInactive: true, cancellationToken);
    }

    public override void Deactivate()
    {
        lock (_refreshGate)
        {
            _isActive = false;
        }
    }

    private Task RefreshLibraryAfterOperationAsync(string reason, CancellationToken cancellationToken = default)
    {
        return RequestLibraryRefreshAsync(reason, debounce: false, allowWhenInactive: true, cancellationToken);
    }

    private async Task ExecuteLibraryRefreshCoreAsync(RefreshRequestSnapshot refreshRequest, CancellationToken cancellationToken = default)
    {
        var refreshId = Interlocked.Increment(ref _refreshSequence);
        var totalStopwatch = Stopwatch.StartNew();
        var queryElapsedMs = 0L;
        var tagDecadeElapsedMs = 0L;
        var filterMetrics = LibraryFilterApplyMetrics.Empty;
        WriteLibraryRefreshEvent(
            "library-refresh-started",
            BuildRefreshLogFields(refreshId, refreshRequest, queryElapsedMs, tagDecadeElapsedMs, filterMetrics, totalElapsedMs: 0));

        try
        {
            var queryStopwatch = Stopwatch.StartNew();
            var movies = await _libraryQueryService.GetLibraryItemsAsync(IsBatchSelectionMode, cancellationToken);
            queryStopwatch.Stop();
            queryElapsedMs = queryStopwatch.ElapsedMilliseconds;
            _allMovies.Clear();
            _allMovies.AddRange(movies);
            ApplyExternalTagCache(_allMovies);

            var tagDecadeStopwatch = Stopwatch.StartNew();
            RefreshTagOptions();
            RefreshDecadeOptions();
            tagDecadeStopwatch.Stop();
            tagDecadeElapsedMs = tagDecadeStopwatch.ElapsedMilliseconds;

            filterMetrics = ApplyFiltersWithMetrics();

            if (_allMovies.Count == 0)
            {
                StatusMessage = "当前还没有可展示的影片数据。请先到扫描任务页执行扫描。";
            }
            totalStopwatch.Stop();
            WriteLibraryRefreshEvent(
                "library-refresh-completed",
                BuildRefreshLogFields(
                    refreshId,
                    refreshRequest,
                    queryElapsedMs,
                    tagDecadeElapsedMs,
                    filterMetrics,
                    totalStopwatch.ElapsedMilliseconds));
            ScheduleRenderReadyDiagnostics(refreshId, refreshRequest, queryElapsedMs, tagDecadeElapsedMs, filterMetrics, totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            totalStopwatch.Stop();
            Movies.ReplaceAll([]);
            OnPropertyChanged(nameof(HasMovies));
            WriteLibraryRefreshEvent(
                "library-refresh-failed",
                BuildRefreshLogFields(
                    refreshId,
                    refreshRequest,
                    queryElapsedMs,
                    tagDecadeElapsedMs,
                    filterMetrics,
                    totalStopwatch.ElapsedMilliseconds,
                    extraFields: $"errorType={exception.GetType().Name}"));
            StatusMessage = $"加载媒体库失败：{exception.Message}";
        }
    }

    private Task RequestLibraryRefreshAsync(
        string reason,
        bool debounce,
        bool allowWhenInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        Task? runningRefreshTask = null;
        CancellationTokenSource? debounceToCancel = null;
        CancellationTokenSource? debounceToSchedule = null;
        var shouldStartRefresh = false;
        var skippedInactive = false;
        var pendingMarked = false;
        var coalescedByDebounce = false;
        var activeSnapshot = false;
        string mergedReasons;

        lock (_refreshGate)
        {
            activeSnapshot = _isActive;
            _queuedRefreshReasons.Add(reason);
            mergedReasons = FormatQueuedRefreshReasonsNoLock();

            if (!activeSnapshot && !allowWhenInactive)
            {
                _dirtyWhileInactive = true;
                skippedInactive = true;
            }
            else if (IsRefreshLoopActiveNoLock())
            {
                _pendingRefresh = true;
                pendingMarked = true;
                runningRefreshTask = _refreshLoopTask ?? Task.CompletedTask;
            }
            else if (debounce)
            {
                debounceToCancel = _refreshDebounceCts;
                debounceToSchedule = new CancellationTokenSource();
                _refreshDebounceCts = debounceToSchedule;
                _queuedRefreshWasDebounced = true;
                coalescedByDebounce = debounceToCancel is not null || _queuedRefreshReasons.Count > 1;
            }
            else
            {
                debounceToCancel = _refreshDebounceCts;
                _refreshDebounceCts = null;
                _queuedRefreshWasDebounced |= debounceToCancel is not null;
                shouldStartRefresh = true;
            }
        }

        WriteLibraryRefreshEvent(
            "library-refresh-requested",
            BuildRefreshRequestLogFields(reason, mergedReasons, activeSnapshot, debounce, coalescedByDebounce: false));

        if (skippedInactive)
        {
            WriteLibraryRefreshEvent(
                "library-refresh-skipped-inactive",
                BuildRefreshRequestLogFields(reason, mergedReasons, activeSnapshot, debounce, coalescedByDebounce: false));
            return Task.CompletedTask;
        }

        if (pendingMarked)
        {
            WriteLibraryRefreshEvent(
                "library-refresh-pending-marked",
                BuildRefreshRequestLogFields(reason, mergedReasons, activeSnapshot, debounce, coalescedByDebounce: false));
            return runningRefreshTask ?? Task.CompletedTask;
        }

        debounceToCancel?.Cancel();

        if (debounceToSchedule is not null)
        {
            WriteLibraryRefreshEvent(
                "library-refresh-debounced",
                BuildRefreshRequestLogFields(reason, mergedReasons, activeSnapshot, debounce, coalescedByDebounce));
            _ = RunDebouncedLibraryRefreshAsync(debounceToSchedule);
            return Task.CompletedTask;
        }

        return shouldStartRefresh
            ? EnsureLibraryRefreshLoopAsync(cancellationToken)
            : Task.CompletedTask;
    }

    private async Task RunDebouncedLibraryRefreshAsync(CancellationTokenSource debounceCts)
    {
        try
        {
            await Task.Delay(RefreshDebounceDelay, debounceCts.Token);
            if (debounceCts.Token.IsCancellationRequested)
            {
                return;
            }

            await EnsureLibraryRefreshLoopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_refreshGate)
            {
                if (ReferenceEquals(_refreshDebounceCts, debounceCts))
                {
                    _refreshDebounceCts = null;
                }
            }

            debounceCts.Dispose();
        }
    }

    private Task EnsureLibraryRefreshLoopAsync(CancellationToken cancellationToken)
    {
        lock (_refreshGate)
        {
            if (IsRefreshLoopActiveNoLock())
            {
                _pendingRefresh = true;
                return _refreshLoopTask ?? Task.CompletedTask;
            }

            _refreshLoopTask = RunLibraryRefreshLoopAsync(cancellationToken);
            return _refreshLoopTask;
        }
    }

    private async Task RunLibraryRefreshLoopAsync(CancellationToken initialCancellationToken)
    {
        await Task.Yield();

        var executePendingRefresh = false;
        var cancellationToken = initialCancellationToken;
        while (true)
        {
            RefreshRequestSnapshot refreshRequest;
            lock (_refreshGate)
            {
                _isRefreshRunning = true;
                _pendingRefresh = false;
                refreshRequest = DrainRefreshRequestNoLock(executePendingRefresh);
            }

            if (refreshRequest.PendingRefreshExecuted)
            {
                WriteLibraryRefreshEvent(
                    "library-refresh-pending-executed",
                    BuildRefreshRequestLogFields(
                        refreshRequest.PrimaryReason,
                        refreshRequest.MergedReasons,
                        refreshRequest.IsActive,
                        debounce: false,
                        coalescedByDebounce: refreshRequest.WasDebounced));
            }

            await ExecuteLibraryRefreshCoreAsync(refreshRequest, cancellationToken);
            cancellationToken = CancellationToken.None;

            lock (_refreshGate)
            {
                if (!_pendingRefresh)
                {
                    _isRefreshRunning = false;
                    _refreshLoopTask = null;
                    return;
                }

                executePendingRefresh = true;
            }
        }
    }

    private RefreshRequestSnapshot DrainRefreshRequestNoLock(bool pendingRefreshExecuted)
    {
        var reasons = _queuedRefreshReasons.Count == 0
            ? [pendingRefreshExecuted ? "pending-refresh" : RefreshReasonManual]
            : _queuedRefreshReasons.OrderBy(reason => reason, StringComparer.OrdinalIgnoreCase).ToArray();
        var request = new RefreshRequestSnapshot(
            reasons,
            _isActive,
            IsBatchSelectionMode,
            _queuedRefreshWasDebounced,
            _dirtyWhileInactive,
            pendingRefreshExecuted);

        _queuedRefreshReasons.Clear();
        _queuedRefreshWasDebounced = false;
        _dirtyWhileInactive = false;
        return request;
    }

    private bool IsRefreshLoopActiveNoLock()
    {
        return _isRefreshRunning || _refreshLoopTask is { IsCompleted: false };
    }

    private string FormatQueuedRefreshReasonsNoLock()
    {
        return _queuedRefreshReasons.Count == 0
            ? "none"
            : string.Join("+", _queuedRefreshReasons.OrderBy(reason => reason, StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildRefreshRequestLogFields(
        string reason,
        string mergedReasons,
        bool isActive,
        bool debounce,
        bool coalescedByDebounce)
    {
        return $"reason={reason} mergedReasons={mergedReasons} active={FormatBool(isActive)} debounce={FormatBool(debounce)} coalescedByDebounce={FormatBool(coalescedByDebounce)}";
    }

    private string BuildRefreshLogFields(
        long refreshId,
        RefreshRequestSnapshot refreshRequest,
        long queryElapsedMs,
        long tagDecadeElapsedMs,
        LibraryFilterApplyMetrics filterMetrics,
        long totalElapsedMs,
        string? extraFields = null)
    {
        var fields = $"refreshId={refreshId} reason={refreshRequest.PrimaryReason} mergedReasons={refreshRequest.MergedReasons} active={FormatBool(refreshRequest.IsActive)} batchMode={FormatBool(refreshRequest.IsBatchMode)} viewMode={filterMetrics.ViewMode} posterVirtualization={FormatBool(filterMetrics.PosterVirtualizationEnabled)} collectionApply={filterMetrics.CollectionApplyMode} queryMs={queryElapsedMs} tagDecadeMs={tagDecadeElapsedMs} filterSortMs={filterMetrics.FilterSortElapsedMs} uiApplyMs={filterMetrics.UiApplyElapsedMs} totalMs={totalElapsedMs} resultTotal={filterMetrics.ResultTotalCount} filtered={filterMetrics.FilteredCount} movie={filterMetrics.MovieCount} tv={filterMetrics.TvCount} other={filterMetrics.OtherCount} batchEligible={filterMetrics.BatchEligibleCount} selected={filterMetrics.SelectedCount} debounced={FormatBool(refreshRequest.WasDebounced)} coalesced={FormatBool(refreshRequest.IsCoalesced)} skippedInactive={FormatBool(refreshRequest.WasDirtyWhileInactive)} pendingExecuted={FormatBool(refreshRequest.PendingRefreshExecuted)}";
        return string.IsNullOrWhiteSpace(extraFields) ? fields : $"{fields} {extraFields}";
    }

    private void ScheduleRenderReadyDiagnostics(
        long refreshId,
        RefreshRequestSnapshot refreshRequest,
        long queryElapsedMs,
        long tagDecadeElapsedMs,
        LibraryFilterApplyMetrics filterMetrics,
        long totalElapsedMs)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        var scheduledAt = Stopwatch.GetTimestamp();
        _ = dispatcher.BeginInvoke(
            new Action(() =>
            {
                var renderReadyElapsedMs = (long)Math.Round(Stopwatch.GetElapsedTime(scheduledAt).TotalMilliseconds);
                WriteLibraryRefreshEvent(
                    "library-render-ready",
                    BuildRefreshLogFields(
                        refreshId,
                        refreshRequest,
                        queryElapsedMs,
                        tagDecadeElapsedMs,
                        filterMetrics,
                        totalElapsedMs,
                        extraFields: $"renderReadyMs={renderReadyElapsedMs}"));
            }),
            DispatcherPriority.ContextIdle);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private async Task OpenRemovedLibraryAsync()
    {
        IsRemovedLibraryPanelOpen = true;
        await LoadRemovedLibraryItemsAsync();
    }

    private async Task LoadRemovedLibraryItemsAsync(CancellationToken cancellationToken = default)
    {
        if (IsRemovedLibraryLoading)
        {
            return;
        }

        IsRemovedLibraryLoading = true;
        try
        {
            RemovedLibraryStatusMessage = "正在加载已移出媒体库项目。";
            var items = await _libraryQueryService.GetHiddenLibraryItemsAsync(cancellationToken);
            RemovedLibraryItems.Clear();
            foreach (var item in items)
            {
                RemovedLibraryItems.Add(new LibraryMovieItemViewModel(item, BuildSelectionKey(item), false, false));
            }

            RefreshRemovedLibraryGroups();
            OnPropertyChanged(nameof(HasRemovedLibraryItems));
            RemovedLibraryStatusMessage = items.Count == 0
                ? "当前没有已移出媒体库项目。"
                : $"已移出媒体库 {items.Count} 项。";
        }
        catch (Exception exception)
        {
            RemovedLibraryItems.Clear();
            RemovedLibraryGroups.Clear();
            OnPropertyChanged(nameof(HasRemovedLibraryItems));
            RemovedLibraryStatusMessage = $"加载已移出媒体库失败：{exception.Message}";
        }
        finally
        {
            IsRemovedLibraryLoading = false;
        }
    }

    private void CloseRemovedLibrary()
    {
        IsRemovedLibraryPanelOpen = false;
    }

    private void RefreshRemovedLibraryGroups()
    {
        RemovedLibraryGroups.Clear();
        foreach (var group in RemovedLibraryGroupViewModel.FromItems(RemovedLibraryItems))
        {
            RemovedLibraryGroups.Add(group);
        }
    }

    private async Task RestoreRemovedLibraryItemAsync(object? parameter)
    {
        if (parameter is not LibraryMovieItemViewModel item)
        {
            return;
        }

        try
        {
                    if (IsGroupedPlaceholder(item.Movie))
                    {
                        await _movieManagementService.RestoreGroupedPlaceholderRangeToLibraryAsync(item.Movie.GroupedRangeMediaFileIds);
                    }
                    else if ((item.IsSeason || item.Movie.IsOther) && item.SeasonId > 0)
                    {
                        await _tvSeasonCollectionService.RestoreSeasonToLibraryAsync(item.SeasonId);
                    }
                    else if ((item.IsMovie || item.Movie.IsOther) && item.MovieId > 0)
                    {
                        await _movieManagementService.RestoreToLibraryAsync(item.MovieId);
                    }
            else if (item.IsMovie)
            {
                await _userCollectionService.RestoreToLibraryAsync(BuildRecommendationItem(item.Movie), changeSource: "RemovedLibrary");
            }
            else
            {
                RemovedLibraryStatusMessage = "电视剧总览没有独立恢复状态，请恢复具体 Season。";
                return;
            }

            RemovedLibraryStatusMessage = $"已恢复到媒体库：{item.Title}";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await LoadRemovedLibraryItemsAsync();
            await RefreshLibraryAfterOperationAsync(RefreshReasonRemovedLibraryChanged);
        }
        catch (Exception exception)
        {
            RemovedLibraryStatusMessage = $"恢复到媒体库失败：{exception.Message}";
        }
    }

    private async Task RestoreRemovedLibraryGroupAsync(object? parameter)
    {
        if (parameter is not RemovedLibraryGroupViewModel { IsTvGroup: true } group)
        {
            return;
        }

        var items = group.Items.ToArray();
        if (items.Length == 0)
        {
            return;
        }

        try
        {
            foreach (var item in items)
            {
                if (IsGroupedPlaceholder(item.Movie))
                {
                    await _movieManagementService.RestoreGroupedPlaceholderRangeToLibraryAsync(item.Movie.GroupedRangeMediaFileIds);
                }
                else if ((item.IsSeason || item.Movie.IsOther) && item.SeasonId > 0)
                {
                    await _tvSeasonCollectionService.RestoreSeasonToLibraryAsync(item.SeasonId);
                }
                else if ((item.IsMovie || item.Movie.IsOther) && item.MovieId > 0)
                {
                    await _movieManagementService.RestoreToLibraryAsync(item.MovieId);
                }
                else if (item.IsMovie)
                {
                    await _userCollectionService.RestoreToLibraryAsync(BuildRecommendationItem(item.Movie), changeSource: "RemovedLibrary");
                }
            }

            RemovedLibraryStatusMessage = $"已恢复到媒体库：{group.Title}（{items.Length} 项）";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await LoadRemovedLibraryItemsAsync();
            await RefreshLibraryAfterOperationAsync(RefreshReasonRemovedLibraryChanged);
        }
        catch (Exception exception)
        {
            RemovedLibraryStatusMessage = $"恢复到媒体库失败：{exception.Message}";
        }
    }

    private async Task DeleteRemovedLibraryItemAsync(object? parameter)
    {
        if (parameter is not LibraryMovieItemViewModel item)
        {
            return;
        }

        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "确认删除记录？",
            "删除记录会清除软件内记录、metadata 和状态，但不会删除本地文件或 WebDAV 文件。",
            "删除记录",
            "取消",
            ConfirmationDialogVariant.Danger);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await DeleteLibraryItemRecordAsync(item);

            RemovedLibraryStatusMessage = $"已删除记录：{item.Title}";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await LoadRemovedLibraryItemsAsync();
            await RefreshLibraryAfterOperationAsync(RefreshReasonRemovedLibraryChanged);
        }
        catch (Exception exception)
        {
            RemovedLibraryStatusMessage = $"删除记录失败：{exception.Message}";
        }
    }

    private async Task DeleteRemovedLibraryGroupAsync(object? parameter)
    {
        if (parameter is not RemovedLibraryGroupViewModel { IsTvGroup: true } group)
        {
            return;
        }

        var items = group.Items.ToArray();
        if (items.Length == 0)
        {
            return;
        }

        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "确认删除记录？",
            $"将删除该剧下已移出媒体库的 {items.Length} 个 Season 记录。删除记录不会删除本地文件或 WebDAV 文件。",
            "删除记录",
            "取消",
            ConfirmationDialogVariant.Danger);
        if (!confirmed)
        {
            return;
        }

        try
        {
            foreach (var item in items)
            {
                await DeleteLibraryItemRecordAsync(item);
            }

            RemovedLibraryStatusMessage = $"已删除记录：{group.Title}（{items.Length} 项）";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await LoadRemovedLibraryItemsAsync();
            await RefreshLibraryAfterOperationAsync(RefreshReasonRemovedLibraryChanged);
        }
        catch (Exception exception)
        {
            RemovedLibraryStatusMessage = $"删除记录失败：{exception.Message}";
        }
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (!ShouldRefreshLibraryForDataChange(e))
        {
            return;
        }

        if (_suppressLibraryRefreshFromBatchNotification)
        {
            return;
        }

        var reason = FormatDataChangeRefreshReason(e);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _ = RequestLibraryRefreshAsync(reason, debounce: true);
            return;
        }

        _ = dispatcher.InvokeAsync(() => _ = RequestLibraryRefreshAsync(reason, debounce: true));
    }

    private static bool ShouldRefreshLibraryForDataChange(AppDataChangedEventArgs e)
    {
        return e.LibraryChanged || e.Reason == AppDataChangeReason.CollectionChanged;
    }

    private static string FormatDataChangeRefreshReason(AppDataChangedEventArgs e)
    {
        return e.Reason switch
        {
            AppDataChangeReason.CollectionChanged => "data-changed-collection",
            AppDataChangeReason.MetadataChanged => "data-changed-metadata",
            AppDataChangeReason.ScanChanged => "data-changed-scan",
            AppDataChangeReason.LibraryChanged => "data-changed-library",
            _ when e.LibraryChanged => "data-changed-library",
            _ => $"data-changed-{e.Reason.ToString().ToLowerInvariant()}"
        };
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SetSubmittedSearchText(string.Empty);
        GenreFilterText = string.Empty;
        SelectedDecadeFilter = DecadeAll;
        SelectedStatusFilter = FilterAll;
        SelectedWatchedFilter = FilterAll;
        SelectedLibraryScope = LibraryScopeAll;
        SelectedSourceFilter = SourceFilterAll;
        SelectedCollectionStatusFilter = FilterAll;
        SelectedContentTypeFilter = "全部";
        ClearTagFilter(applyFilters: false);
        SelectedSortOption = "最近更新";
        SelectedSortDirection = "降序";
        ApplyFilters();
    }

    private void SubmitSearch()
    {
        SetSubmittedSearchText(SearchText);
        ApplyFilters();
    }

    private void ClearSearch()
    {
        var hadSearch = !string.IsNullOrWhiteSpace(SearchText)
                        || !string.IsNullOrWhiteSpace(_submittedSearchText);
        SearchText = string.Empty;
        SetSubmittedSearchText(string.Empty);
        if (hadSearch)
        {
            ApplyFilters();
        }
    }

    private void SelectLibraryScope(object? parameter)
    {
        if (parameter is string scope && LibraryScopeOptions.Contains(scope))
        {
            SelectedLibraryScope = scope;
        }
    }

    private void SelectSourceFilter(object? parameter)
    {
        if (parameter is string source && SourceFilterOptions.Contains(source))
        {
            SelectedSourceFilter = source;
        }
    }

    private void SelectContentType(object? parameter)
    {
        if (parameter is string contentType && ContentTypeOptions.Contains(contentType))
        {
            SelectedContentTypeFilter = contentType;
        }
    }

    private void SelectCollectionStatus(object? parameter)
    {
        if (parameter is string status && CollectionStatusOptions.Contains(status))
        {
            SelectedCollectionStatusFilter = status;
        }
    }

    private void ClearTagFilter()
    {
        ClearTagFilter(applyFilters: true);
    }

    private void ClearTagFilter(bool applyFilters)
    {
        _isUpdatingTagSelection = true;
        try
        {
            foreach (var option in AllTagOptions())
            {
                option.IsSelected = false;
            }
        }
        finally
        {
            _isUpdatingTagSelection = false;
        }

        OnPropertyChanged(nameof(TagFilterMenuHeader));
        if (applyFilters)
        {
            ApplyFilters();
        }
    }

    private void SetLibraryLayout(bool isPosterView)
    {
        _hasUserChangedLayoutThisSession = true;
        IsPosterView = isPosterView;
    }

    private async Task LoadLibraryPreferencesAsync()
    {
        try
        {
            var preferences = await _libraryPreferencesService.LoadAsync();
            if (_hasUserChangedLayoutThisSession)
            {
                return;
            }

            _isApplyingLayoutPreference = true;
            try
            {
                IsPosterView = !string.Equals(
                    preferences.LayoutMode,
                    LibraryLayoutList,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                _isApplyingLayoutPreference = false;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteLibraryRefreshEvent(
                "library-preferences-restore-failed",
                $"errorType={exception.GetType().Name}");
        }
    }

    private async Task SaveLibraryPreferencesAsync()
    {
        try
        {
            await _libraryPreferencesService.SaveAsync(CreateLibraryPreferencesSnapshot());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteLibraryRefreshEvent(
                "library-preferences-save-failed",
                $"errorType={exception.GetType().Name}");
        }
    }

    private LibraryPreferencesModel CreateLibraryPreferencesSnapshot()
    {
        return new LibraryPreferencesModel
        {
            LayoutMode = IsPosterView ? LibraryLayoutPoster : LibraryLayoutList
        };
    }

    private void RefreshTagOptions()
    {
        if (TypeTagOptions.Count == 0)
        {
            ReplaceTagOptions(TypeTagOptions, BuildStaticTagOptions(TagCategoryType, TypeTagLabels));
        }

        if (EmotionTagOptions.Count == 0)
        {
            ReplaceTagOptions(EmotionTagOptions, BuildStaticTagOptions(TagCategoryEmotion, EmotionTagLabels));
        }

        if (SceneTagOptions.Count == 0)
        {
            ReplaceTagOptions(SceneTagOptions, BuildStaticTagOptions(TagCategoryScene, SceneTagLabels));
        }
    }

    private IEnumerable<TagFilterOption> AllTagOptions()
    {
        return TypeTagOptions.Concat(EmotionTagOptions).Concat(SceneTagOptions);
    }

    private IReadOnlyList<TagFilterOption> BuildStaticTagOptions(
        string category,
        IEnumerable<string> labels)
    {
        return labels
            .Select(label => new TagFilterOption(category, label, OnTagSelectionChanged))
            .ToList();
    }

    private void OnTagSelectionChanged(TagFilterOption option)
    {
        if (_isUpdatingTagSelection)
        {
            return;
        }

        OnPropertyChanged(nameof(TagFilterMenuHeader));
        ApplyFilters();
    }

    private void RefreshDecadeOptions()
    {
        var options = _allMovies
            .Where(item => item.ReleaseYear is > 0)
            .Select(item => item.ReleaseYear!.Value / 10 * 10)
            .Distinct()
            .OrderByDescending(decade => decade)
            .Select(decade => $"{decade}年代")
            .ToList();

        DecadeFilterOptions.Clear();
        DecadeFilterOptions.Add(DecadeAll);
        foreach (var option in options)
        {
            DecadeFilterOptions.Add(option);
        }

        if (!DecadeFilterOptions.Contains(SelectedDecadeFilter))
        {
            SelectedDecadeFilter = DecadeAll;
        }
    }

    private static void ReplaceTagOptions(
        ObservableCollection<TagFilterOption> target,
        IEnumerable<TagFilterOption> source)
    {
        target.Clear();
        foreach (var option in source)
        {
            target.Add(option);
        }
    }

    private void ApplyFilters()
    {
        _ = ApplyFiltersWithMetrics();
    }

    private LibraryFilterApplyMetrics ApplyFiltersWithMetrics()
    {
        var filterSortStopwatch = Stopwatch.StartNew();
        var query = _allMovies.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_submittedSearchText))
        {
            var keyword = _submittedSearchText;
            query = query.Where(
                item => item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || item.OriginalTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(GenreFilterText))
        {
            var genreKeyword = GenreFilterText.Trim();
            query = query.Where(item => item.GenresText.Contains(genreKeyword, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedContentTypeFilter switch
        {
            "电影" => query.Where(item => item.IsMovie),
            "电视剧" => query.Where(item => item.IsSeries || item.IsSeason),
            "其他" => query.Where(item => item.IsOther),
            _ => query
        };

        foreach (var tag in TypeTagOptions.Where(option => option.IsSelected).Select(option => option.Label))
        {
            query = query.Where(item => HasSelectedTag(item, TagCategoryType, tag));
        }

        foreach (var tag in EmotionTagOptions.Where(option => option.IsSelected).Select(option => option.Label))
        {
            query = query.Where(item => HasSelectedTag(item, TagCategoryEmotion, tag));
        }

        foreach (var tag in SceneTagOptions.Where(option => option.IsSelected).Select(option => option.Label))
        {
            query = query.Where(item => HasSelectedTag(item, TagCategoryScene, tag));
        }

        if (TryParseDecadeFilter(SelectedDecadeFilter, out var decadeStart))
        {
            query = query.Where(item => item.ReleaseYear >= decadeStart && item.ReleaseYear < decadeStart + 10);
        }

        query = SelectedStatusFilter switch
        {
            StatusMatched => query.Where(item => item.IdentificationStatus == IdentificationStatus.Matched),
            StatusNeedsReview => query.Where(item => item.IdentificationStatus == IdentificationStatus.NeedsReview),
            StatusManualConfirmed => query.Where(item => item.IdentificationStatus == IdentificationStatus.ManualConfirmed),
            StatusFailed => query.Where(item => item.IdentificationStatus == IdentificationStatus.Failed),
            StatusPending => query.Where(item => item.IdentificationStatus == IdentificationStatus.Pending),
            _ => query
        };

        query = SelectedWatchedFilter switch
        {
            WatchedFilterWatched => query.Where(item => item.IsWatched),
            WatchedFilterUnwatched => query.Where(item => !item.IsWatched),
            WatchedFilterNotInterested => query.Where(item => item.IsNotInterested),
            _ => query
        };

        query = SelectedCollectionStatusFilter switch
        {
            CollectionStatusFavorite => query.Where(item => item.IsFavorite),
            CollectionStatusWantToWatch => query.Where(item => item.IsWantToWatch),
            CollectionStatusNotInterested => query.Where(item => item.IsNotInterested),
            _ => query
        };

        query = SelectedLibraryScope switch
        {
            LibraryScopeWithSource => query.Where(item => item.IsVisibleInLibrary && item.HasActiveSource),
            LibraryScopeWithoutSource => query.Where(item => item.IsVisibleInLibrary && !item.HasActiveSource),
            _ => query.Where(item => item.IsVisibleInLibrary)
        };

        query = SelectedSourceFilter switch
        {
            SourceFilterLocal => query.Where(item => item.HasLocalSource),
            SourceFilterWebDav => query.Where(item => item.HasWebDavSource),
            _ => query
        };

        query = ApplySorting(query);

        var filtered = query.ToList();
        filterSortStopwatch.Stop();
        var uiApplyStopwatch = Stopwatch.StartNew();
        ReconcileSelectionWithVisibleItems(filtered);

        var viewModels = filtered
            .Select(movie =>
            {
                var selectionKey = BuildSelectionKey(movie);
                return new LibraryMovieItemViewModel(
                    movie,
                    selectionKey,
                    IsBatchSelectionMode,
                    _selectedItemKeys.Contains(selectionKey));
            })
            .ToList();
        Movies.ReplaceAll(viewModels);

        OnPropertyChanged(nameof(HasMovies));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
        RefreshBatchCommandState();
        StatusMessage = _allMovies.Count == 0
            ? "当前还没有可展示的影片数据。请先到扫描任务页执行扫描。"
            : BuildResultStatusMessage(filtered);
        uiApplyStopwatch.Stop();

        return new LibraryFilterApplyMetrics(
            _allMovies.Count,
            filtered.Count,
            filtered.Count(item => item.IsMovie),
            filtered.Count(item => item.IsSeries || item.IsSeason),
            filtered.Count(item => item.IsOther),
            filtered.Count(IsBatchOperationTarget),
            SelectedCount,
            filterSortStopwatch.ElapsedMilliseconds,
            uiApplyStopwatch.ElapsedMilliseconds,
            IsPosterView ? "poster" : "list",
            IsPosterView,
            "range-reset");
    }

    private string BuildResultStatusMessage(IReadOnlyCollection<LibraryMovieListItem> filteredItems)
    {
        var totalCount = filteredItems.Count;
        var watchedCount = filteredItems.Count(item => item.IsWatched);
        var unwatchedCount = filteredItems.Count(item => !item.IsWatched);
        var sourceCount = filteredItems.Count(item => item.HasActiveSource);
        var noSourceCount = filteredItems.Count(item => !item.HasActiveSource);
        var message = $"共找到 {totalCount} 项媒体 · 已看 {watchedCount} · 未看 {unwatchedCount} · 有播放源 {sourceCount} · 无播放源 {noSourceCount}";

        if (IsBatchSelectionMode)
        {
            message += $" · 已选 {SelectedCount} 项";
        }

        return message;
    }

    private void SetSubmittedSearchText(string value)
    {
        var normalized = NormalizeSearchText(value);
        if (string.Equals(_submittedSearchText, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _submittedSearchText = normalized;
    }

    private static string NormalizeSearchText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool HasSelectedTag(
        LibraryMovieListItem item,
        string category,
        string selectedTag)
    {
        var tags = category switch
        {
            TagCategoryType => GetTypeTags(item),
            TagCategoryEmotion => SplitTags(item.EmotionTagsText),
            TagCategoryScene => SplitTags(item.SceneTagsText),
            _ => Array.Empty<string>()
        };

        return tags.Any(tag => string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyExternalTagCache(IEnumerable<LibraryMovieListItem> items)
    {
        foreach (var item in items.Where(item => !item.HasActiveSource))
        {
            if (!ExternalMovieTagCache.TryGet(item.TmdbId, item.ImdbId, item.Title, item.ReleaseYear, out var tags))
            {
                continue;
            }

            item.AiTagsText = string.IsNullOrWhiteSpace(tags.AiTagsText) ? item.AiTagsText : tags.AiTagsText;
            item.EmotionTagsText = string.IsNullOrWhiteSpace(tags.EmotionTagsText) ? item.EmotionTagsText : tags.EmotionTagsText;
            item.SceneTagsText = string.IsNullOrWhiteSpace(tags.SceneTagsText) ? item.SceneTagsText : tags.SceneTagsText;
        }
    }

    private static bool TryParseDecadeFilter(string value, out int decadeStart)
    {
        decadeStart = 0;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, DecadeAll, StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = value.Replace("年代", string.Empty, StringComparison.Ordinal).Trim();
        return int.TryParse(normalized, out decadeStart);
    }

    private static IReadOnlyList<string> GetTypeTags(LibraryMovieListItem item)
    {
        return SplitTags(string.IsNullOrWhiteSpace(item.AiTagsText) ? item.GenresText : item.AiTagsText);
    }

    private static IReadOnlyList<string> SplitTags(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? []
            : text
                .Split(['、', '/', ',', '，', ';', '；', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList();
    }

    private IEnumerable<LibraryMovieListItem> ApplySorting(IEnumerable<LibraryMovieListItem> query)
    {
        if (IsBatchSelectionMode)
        {
            return ApplyBatchSelectionSorting(query);
        }

        var descending = string.Equals(SelectedSortDirection, "降序", StringComparison.Ordinal);

        return SelectedSortOption switch
        {
            "标题" => descending
                ? query.OrderByDescending(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            "年份" => descending
                ? query.OrderByDescending(item => item.ReleaseYear ?? 0).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(item => item.ReleaseYear ?? 0).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            "评分" => descending
                ? query.OrderByDescending(item => item.PrimaryRatingValue ?? -1d).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(item => item.PrimaryRatingValue ?? -1d).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => descending
                ? query.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    private IEnumerable<LibraryMovieListItem> ApplyBatchSelectionSorting(IEnumerable<LibraryMovieListItem> query)
    {
        var items = query.ToList();
        var seriesGroups = items
            .Where(IsSeasonLikeBatchItem)
            .GroupBy(GetBatchSelectionSeriesGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new BatchSelectionSeriesSortKey(
                    group.Max(item => item.UpdatedAt),
                    group.Select(GetBatchSelectionSeriesKey).FirstOrDefault(title => !string.IsNullOrWhiteSpace(title)) ?? string.Empty,
                    group.Where(item => item.ReleaseYear is > 0).Select(item => item.ReleaseYear!.Value).DefaultIfEmpty(0).Min()),
                StringComparer.OrdinalIgnoreCase);
        var descending = string.Equals(SelectedSortDirection, "降序", StringComparison.Ordinal);

        IOrderedEnumerable<LibraryMovieListItem> ordered = SelectedSortOption switch
        {
            "标题" => descending
                ? items.OrderByDescending(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase)
                : items.OrderBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase),
            "年份" => descending
                ? items.OrderByDescending(item => GetBatchSelectionGroupReleaseYear(item, seriesGroups))
                    .ThenBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase)
                : items.OrderBy(item => GetBatchSelectionGroupReleaseYear(item, seriesGroups))
                    .ThenBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase),
            "评分" => descending
                ? items.OrderByDescending(item => item.PrimaryRatingValue ?? -1d)
                    .ThenBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase)
                : items.OrderBy(item => item.PrimaryRatingValue ?? -1d)
                    .ThenBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase),
            _ => descending
                ? items.OrderByDescending(item => GetBatchSelectionGroupUpdatedAt(item, seriesGroups))
                    .ThenBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase)
                : items.OrderBy(item => GetBatchSelectionGroupUpdatedAt(item, seriesGroups))
                    .ThenBy(item => GetBatchSelectionGroupTitle(item, seriesGroups), StringComparer.CurrentCultureIgnoreCase)
        };

        return ordered
            .ThenBy(item => GetBatchSelectionGroupKey(item))
            .ThenBy(item => GetBatchSelectionSeasonNumber(item))
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(item => item.UpdatedAt);
    }

    private static string GetBatchSelectionSeriesKey(LibraryMovieListItem item)
    {
        if (IsSeasonLikeBatchItem(item))
        {
            return string.IsNullOrWhiteSpace(item.SeriesTitle)
                ? item.Title
                : item.SeriesTitle;
        }

        return item.Title;
    }

    private static string GetBatchSelectionSeriesGroupKey(LibraryMovieListItem item)
    {
        if (item.SeriesId > 0)
        {
            return $"series:{item.SeriesId}";
        }

        return $"title:{GetBatchSelectionSeriesKey(item)}";
    }

    private static string GetBatchSelectionGroupKey(LibraryMovieListItem item)
    {
        return IsSeasonLikeBatchItem(item)
            ? GetBatchSelectionSeriesGroupKey(item)
            : BuildSelectionKey(item);
    }

    private static DateTime GetBatchSelectionGroupUpdatedAt(
        LibraryMovieListItem item,
        IReadOnlyDictionary<string, BatchSelectionSeriesSortKey> seriesGroups)
    {
        if (!IsSeasonLikeBatchItem(item))
        {
            return item.UpdatedAt;
        }

        return seriesGroups.TryGetValue(GetBatchSelectionSeriesGroupKey(item), out var sortKey)
            ? sortKey.UpdatedAt
            : item.UpdatedAt;
    }

    private static string GetBatchSelectionGroupTitle(
        LibraryMovieListItem item,
        IReadOnlyDictionary<string, BatchSelectionSeriesSortKey> seriesGroups)
    {
        if (!IsSeasonLikeBatchItem(item))
        {
            return item.Title;
        }

        return seriesGroups.TryGetValue(GetBatchSelectionSeriesGroupKey(item), out var sortKey)
            ? sortKey.Title
            : GetBatchSelectionSeriesKey(item);
    }

    private static int GetBatchSelectionGroupReleaseYear(
        LibraryMovieListItem item,
        IReadOnlyDictionary<string, BatchSelectionSeriesSortKey> seriesGroups)
    {
        if (!IsSeasonLikeBatchItem(item))
        {
            return item.ReleaseYear ?? 0;
        }

        return seriesGroups.TryGetValue(GetBatchSelectionSeriesGroupKey(item), out var sortKey)
            ? sortKey.ReleaseYear
            : item.ReleaseYear ?? 0;
    }

    private static int GetBatchSelectionSeasonNumber(LibraryMovieListItem item)
    {
        return IsSeasonLikeBatchItem(item) && item.SeasonNumber >= 0
            ? item.SeasonNumber
            : int.MaxValue;
    }

    private static bool IsSeasonLikeBatchItem(LibraryMovieListItem item)
    {
        return item.SeasonId > 0 && (item.IsSeason || item.IsOther);
    }

    private static bool IsBatchOperationTarget(LibraryMovieListItem item)
    {
        return item.IsMovie || item.IsSeason || item.IsOther;
    }

    private sealed record BatchSelectionSeriesSortKey(DateTime UpdatedAt, string Title, int ReleaseYear);

    private sealed record RefreshRequestSnapshot(
        IReadOnlyList<string> Reasons,
        bool IsActive,
        bool IsBatchMode,
        bool WasDebounced,
        bool WasDirtyWhileInactive,
        bool PendingRefreshExecuted)
    {
        public string PrimaryReason => PendingRefreshExecuted ? "pending-refresh" : Reasons.FirstOrDefault() ?? RefreshReasonManual;

        public string MergedReasons => Reasons.Count == 0 ? "none" : string.Join("+", Reasons);

        public bool IsCoalesced => WasDebounced || PendingRefreshExecuted || Reasons.Count > 1;
    }

    private readonly record struct LibraryFilterApplyMetrics(
        int ResultTotalCount,
        int FilteredCount,
        int MovieCount,
        int TvCount,
        int OtherCount,
        int BatchEligibleCount,
        int SelectedCount,
        long FilterSortElapsedMs,
        long UiApplyElapsedMs,
        string ViewMode,
        bool PosterVirtualizationEnabled,
        string CollectionApplyMode)
    {
        public static LibraryFilterApplyMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, "unknown", false, "none");
    }

    private void ToggleBatchSelectionMode()
    {
        if (IsBatchOperationRunning)
        {
            return;
        }

        if (IsBatchSelectionMode)
        {
            ClearSelection();
            IsBatchSelectionMode = false;
            BatchResultSummary = string.Empty;
            _ = RequestLibraryRefreshAsync(RefreshReasonBatchModeChanged, debounce: false, allowWhenInactive: true);
            return;
        }

        IsBatchSelectionMode = true;
        BatchResultSummary = string.Empty;
        ClearSelection();
        _ = RequestLibraryRefreshAsync(RefreshReasonBatchModeChanged, debounce: false, allowWhenInactive: true);
    }

    private void OpenOrToggleSelection(object? parameter)
    {
        if (parameter is not LibraryMovieItemViewModel item)
        {
            return;
        }

        if (IsBatchSelectionMode)
        {
            ToggleSelection(item);
            return;
        }

        OpenMovie(item.Movie);
    }

    private void ToggleItemSelection(object? parameter)
    {
        if (parameter is LibraryMovieItemViewModel item)
        {
            ToggleSelection(item);
        }
    }

    private void ToggleSelection(LibraryMovieItemViewModel item)
    {
        if (!IsBatchSelectionMode || IsBatchOperationRunning)
        {
            return;
        }

        if (_selectedItemKeys.Remove(item.SelectionKey))
        {
            item.IsSelected = false;
        }
        else
        {
            _selectedItemKeys.Add(item.SelectionKey);
            item.IsSelected = true;
        }

        RefreshBatchCommandState();
    }

    private void SelectVisibleItems()
    {
        if (!IsBatchSelectionMode || IsBatchOperationRunning || Movies.Count == 0)
        {
            return;
        }

        foreach (var item in Movies)
        {
            _selectedItemKeys.Add(item.SelectionKey);
            item.IsSelected = true;
        }

        BatchResultSummary = $"\u5DF2\u9009\u4E2D\u5F53\u524D\u5217\u8868 {Movies.Count} \u9879\u3002";
        WriteLibraryBatchEvent($"event=library-select-visible-items count={Movies.Count}");
        RefreshBatchCommandState();
    }

    private void ClearBatchSelection()
    {
        if (!IsBatchSelectionMode || IsBatchOperationRunning || _selectedItemKeys.Count == 0)
        {
            return;
        }

        ClearSelection();
        BatchResultSummary = "\u5DF2\u6E05\u7A7A\u9009\u62E9\u3002";
    }

    private async Task BatchSetWatchedAsync(bool isWatched)
    {
        var selectedItems = GetSelectedVisibleItems();
        if (selectedItems.Count == 0)
        {
            ClearSelection();
            BatchResultSummary = "没有可处理的已选项目。";
            return;
        }

        var operationName = isWatched ? "批量标记已看" : "批量标记未看";
        var successCount = 0;
        var errors = new List<BatchItemError>();
        IsBatchOperationRunning = true;

        try
        {
            foreach (var item in selectedItems)
            {
                try
                {
                    if ((item.Movie.IsSeason || item.Movie.IsOther) && item.Movie.SeasonId > 0)
                    {
                        await _tvSeasonCollectionService.SetWatchedAsync(item.Movie.SeasonId, isWatched, changeSource: "Batch");
                    }
                    else if (item.Movie.IsMovie && item.IsInLibrary && item.MovieId > 0)
                    {
                        await _movieManagementService.SetWatchedAsync(item.MovieId, isWatched, changeSource: "Batch");
                    }
                    else if (item.Movie.IsMovie)
                    {
                        await _userCollectionService.SetWatchedAsync(BuildRecommendationItem(item.Movie), isWatched, changeSource: "Batch");
                    }
                    else
                    {
                        errors.Add(new BatchItemError(item.SelectionKey, item.Title, "电视剧总览不参与批量操作。"));
                        continue;
                    }

                    successCount++;
                }
                catch (Exception exception)
                {
                    errors.Add(new BatchItemError(item.SelectionKey, item.Title, DescribeException(exception)));
                }
            }

            if (errors.Count > 0)
            {
                SetSelectionToFailures(errors);
            }
            else if (successCount > 0)
            {
                ClearSelection();
                IsBatchSelectionMode = false;
            }
            else
            {
                SetSelectionToFailures(errors);
            }

            NotifyAfterBatchStatusChange();
            await RefreshLibraryAfterOperationAsync(RefreshReasonBatchStatusChanged);
            BatchResultSummary = BuildResultSummary(operationName, successCount, errors);
        }
        finally
        {
            IsBatchOperationRunning = false;
        }
    }

    private async Task BatchRemoveFromLibraryAsync()
    {
        var selectedItems = GetSelectedVisibleItems();
        if (selectedItems.Count == 0)
        {
            ClearSelection();
            BatchResultSummary = "没有可隐藏的已选项目。";
            return;
        }

        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "确认移出媒体库？",
            "移出后所选项目将从媒体库列表隐藏，但不会删除或禁用播放源、状态、metadata、播放历史、本地文件或 WebDAV 文件。",
            "移出",
            "取消",
            ConfirmationDialogVariant.Warning);

        if (!confirmed)
        {
            BatchResultSummary = "已取消移出媒体库。";
            return;
        }

        var successCount = 0;
        var hiddenCount = 0;
        var errors = new List<BatchItemError>();
        IsBatchOperationRunning = true;
        WriteLibraryBatchEvent($"event=library-remove-from-library-start count={selectedItems.Count}");

        try
        {
            foreach (var item in selectedItems)
            {
                try
                {
                    if (IsGroupedPlaceholder(item.Movie))
                    {
                        hiddenCount++;
                        await _movieManagementService.RemoveGroupedPlaceholderRangeFromLibraryAsync(item.Movie.GroupedRangeMediaFileIds);
                    }
                    else if ((item.Movie.IsSeason || item.Movie.IsOther) && item.Movie.SeasonId > 0)
                    {
                        hiddenCount++;
                        await _tvSeasonCollectionService.RemoveFromLibraryAsync(item.Movie.SeasonId);
                    }
                    else if ((item.Movie.IsMovie || item.Movie.IsOther) && item.MovieId > 0)
                    {
                        hiddenCount++;
                        await _movieManagementService.RemoveFromLibraryAsync(item.MovieId);
                    }
                    else if (item.Movie.IsMovie)
                    {
                        hiddenCount++;
                        await _userCollectionService.HideFromLibraryAsync(BuildRecommendationItem(item.Movie), changeSource: "Batch");
                    }
                    else
                    {
                        errors.Add(new BatchItemError(item.SelectionKey, item.Title, "电视剧总览不参与批量操作。"));
                        continue;
                    }

                    successCount++;
                }
                catch (Exception exception)
                {
                    errors.Add(new BatchItemError(item.SelectionKey, item.Title, DescribeException(exception)));
                }
            }

            if (errors.Count > 0)
            {
                SetSelectionToFailures(errors);
            }
            else if (successCount > 0)
            {
                ClearSelection();
                IsBatchSelectionMode = false;
            }
            else
            {
                SetSelectionToFailures(errors);
            }

            NotifyAfterBatchRemoveFromLibrary();
            await RefreshLibraryAfterOperationAsync(RefreshReasonBatchRemoveFromLibrary);
            BatchResultSummary = BuildRemoveFromLibraryResultSummary(successCount, hiddenCount, errors);
            WriteLibraryBatchEvent(
                $"event=library-remove-from-library-complete success={successCount} hidden={hiddenCount} failed={errors.Count}");
        }
        finally
        {
            IsBatchOperationRunning = false;
        }
    }

    private async Task BatchDeleteMovieRecordsAsync()
    {
        var selectedItems = GetSelectedVisibleItems();
        if (selectedItems.Count == 0)
        {
            ClearSelection();
            BatchResultSummary = "没有可删除记录的已选项目。";
            return;
        }

        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "确认删除记录？",
            "删除后将移除所选电影或电视剧季在软件中的信息、播放历史、收藏状态和播放源记录，但不会删除本地文件或 WebDAV 文件。后续扫描可能重新发现。",
            "删除记录",
            "取消",
            ConfirmationDialogVariant.Danger);

        if (!confirmed)
        {
            BatchResultSummary = "已取消删除记录。";
            return;
        }

        var successCount = 0;
        var errors = new List<BatchItemError>();
        IsBatchOperationRunning = true;
        WriteLibraryBatchEvent($"event=library-delete-movie-records-start count={selectedItems.Count}");

        try
        {
            foreach (var item in selectedItems)
            {
                try
                {
                    await DeleteLibraryItemRecordAsync(item);

                    successCount++;
                    WriteLibraryBatchEvent(
                        $"event=library-delete-movie-record item={FormatSelectionKeyForLog(item.SelectionKey)} movieId={item.MovieId} result=success");
                }
                catch (Exception exception)
                {
                    var message = DescribeException(exception);
                    errors.Add(new BatchItemError(item.SelectionKey, item.Title, message));
                    WriteLibraryBatchEvent(
                        $"event=library-delete-movie-record item={FormatSelectionKeyForLog(item.SelectionKey)} movieId={item.MovieId} result=failed reason=\"{AiPerfDiagnostics.SanitizeMessage(message)}\"");
                }
            }

            if (errors.Count > 0)
            {
                SetSelectionToFailures(errors);
            }
            else if (successCount > 0)
            {
                ClearSelection();
                IsBatchSelectionMode = false;
            }
            else
            {
                SetSelectionToFailures(errors);
            }

            NotifyAfterBatchMovieRecordDelete();
            await RefreshLibraryAfterOperationAsync(RefreshReasonBatchDeleteRecords);
            BatchResultSummary = BuildResultSummary("删除软件记录", successCount, errors);
            WriteLibraryBatchEvent(
                $"event=library-delete-movie-records-complete success={successCount} failed={errors.Count}");
        }
        finally
        {
            IsBatchOperationRunning = false;
        }
    }

    private async Task DeleteLibraryItemRecordAsync(LibraryMovieItemViewModel item)
    {
        var movie = item.Movie;
        if (IsGroupedPlaceholder(movie))
        {
            await _movieManagementService.DeleteGroupedPlaceholderRangeRecordAsync(movie.GroupedRangeMediaFileIds);
            return;
        }

        if (movie.SeasonId > 0 && (movie.IsSeason || movie.IsOther))
        {
            await _tvSeasonCollectionService.DeleteSeasonRecordAsync(movie.SeasonId);
            return;
        }

        if (movie.MovieId > 0 && !movie.IsSeries && movie.SeasonId <= 0)
        {
            await _movieManagementService.DeleteMovieRecordAsync(movie.MovieId);
            return;
        }

        if (movie.IsMovie)
        {
            await _userCollectionService.DeleteCollectionRecordAsync(BuildRecommendationItem(movie));
            return;
        }

        throw new InvalidOperationException("电视剧总览没有独立删除记录，请处理具体 Season。");
    }

    private async Task OpenManualAggregationAsync()
    {
        var selectedItems = GetSelectedVisibleItems();
        if (selectedItems.Count == 0)
        {
            BatchResultSummary = "没有可聚合的已选项目。";
            return;
        }

        if (selectedItems.Any(item => !CanUseForManualUnknownSeasonAggregation(item)))
        {
            BatchResultSummary = "人工聚合为季只支持未识别且有播放源的项目；混入已识别或无播放源项目时不可用。";
            RefreshBatchCommandState();
            return;
        }

        IsManualAggregationBusy = true;
        ManualAggregationStatusMessage = "正在展开已选未识别播放源...";
        WriteLibraryBatchEvent($"event=manual-unknown-season-aggregation-open count={selectedItems.Count}");

        try
        {
            var selections = selectedItems
                .Select(BuildManualAggregationSelection)
                .ToArray();
            var result = await _manualUnknownSeasonAggregationService.PrepareAsync(selections);
            ManualAggregationStatusMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "已展开已选播放源。"
                : result.Message;

            if (!result.IsValid)
            {
                BatchResultSummary = ManualAggregationStatusMessage;
                WriteLibraryBatchEvent(
                    $"event=manual-unknown-season-aggregation-open-skipped reason=\"{AiPerfDiagnostics.SanitizeMessage(ManualAggregationStatusMessage)}\"");
                return;
            }

            ManualAggregationSeriesTitle = result.SuggestedSeriesTitle;
            ManualAggregationSeasonTitle = result.SuggestedSeasonTitle;
            ManualAggregationSeasonNumberText = "1";
            ManualAggregationSources.Clear();
            foreach (var source in result.Sources.OrderBy(item => item.SortIndex).ThenBy(item => item.MediaFileId))
            {
                ManualAggregationSources.Add(new ManualUnknownSeasonAggregationSourceRowViewModel(source));
            }

            RefreshManualAggregationState();
            IsManualAggregationDialogOpen = true;
            BatchResultSummary = $"已展开 {ManualAggregationSources.Count} 个未识别播放源，请确认集号后聚合。";
        }
        catch (Exception exception)
        {
            var message = DescribeException(exception);
            ManualAggregationStatusMessage = $"打开人工聚合失败：{message}";
            BatchResultSummary = ManualAggregationStatusMessage;
            WriteLibraryBatchEvent(
                $"event=manual-unknown-season-aggregation-open-failed reason=\"{AiPerfDiagnostics.SanitizeMessage(message)}\"");
        }
        finally
        {
            IsManualAggregationBusy = false;
        }
    }

    private async Task ApplyManualAggregationAsync()
    {
        if (!IsManualAggregationDialogOpen || IsManualAggregationBusy)
        {
            return;
        }

        var validationMessage = ValidateManualAggregationInput();
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            ManualAggregationStatusMessage = validationMessage;
            return;
        }

        var request = BuildManualAggregationApplyRequest();

        IsManualAggregationBusy = true;
        IsBatchOperationRunning = true;
        ManualAggregationStatusMessage = "正在聚合为未识别季...";
        WriteLibraryBatchEvent(
            $"event=manual-season-aggregate-apply-started sourceCount={request.Sources.Count} seasonNumber={request.SeasonNumber}");

        try
        {
            var result = await _manualUnknownSeasonAggregationService.ApplyAsync(request);
            var message = $"人工聚合完成：移动 {result.SourceCount} 个播放源，创建 {result.CreatedEpisodeCount} 集，重复集号追加 {result.AdditionalSourceCount} 个播放源。";
            ManualAggregationStatusMessage = message;
            BatchResultSummary = message;
            IsManualAggregationDialogOpen = false;
            ManualAggregationSources.Clear();
            ManualAggregationSeasonNumberText = "1";
            RefreshManualAggregationState();
            ClearSelection();
            IsBatchSelectionMode = false;
            NotifyAfterManualAggregation();
            await RefreshLibraryAfterOperationAsync(RefreshReasonManualAggregation);
            WriteLibraryBatchEvent(
                $"event=manual-season-aggregate-apply-succeeded seasonId={result.SeasonId} sourceCount={result.SourceCount} createdEpisodeCount={result.CreatedEpisodeCount} additionalSourceCount={result.AdditionalSourceCount} seasonNumber={request.SeasonNumber}");
        }
        catch (Exception exception)
        {
            var message = DescribeException(exception);
            ManualAggregationStatusMessage = message.Contains("已存在同名剧集", StringComparison.Ordinal)
                ? message
                : $"聚合失败，事务已回滚：{message}";
            BatchResultSummary = ManualAggregationStatusMessage;
            WriteLibraryBatchEvent(
                $"event=manual-season-aggregate-apply-failed seasonNumber={request.SeasonNumber} failureReason=\"{AiPerfDiagnostics.SanitizeMessage(message)}\"");
        }
        finally
        {
            IsBatchOperationRunning = false;
            IsManualAggregationBusy = false;
        }
    }

    private async Task ApplyManualAggregationAndIdentifyAsync()
    {
        if (!IsManualAggregationDialogOpen || IsManualAggregationBusy)
        {
            return;
        }

        var validationMessage = ValidateManualAggregationInput();
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            ManualAggregationStatusMessage = validationMessage;
            return;
        }

        var request = BuildManualAggregationApplyRequest();
        IsManualAggregationBusy = true;
        IsBatchOperationRunning = true;
        ManualAggregationStatusMessage = "正在聚合为未识别季...";
        ScanIdentificationDiagnostics.Write(
            $"event=manual-aggregate-identify-started sourceCount={request.Sources.Count} seasonNumber={request.SeasonNumber}");

        ManualUnknownSeasonAggregationApplyResult? aggregateResult = null;
        try
        {
            aggregateResult = await _manualUnknownSeasonAggregationService.ApplyAsync(request);
            ScanIdentificationDiagnostics.Write(
                $"event=manual-aggregate-identify-aggregate-succeeded unknownSeasonId={aggregateResult.SeasonId} unknownSeriesId={aggregateResult.SeriesId} sourceCount={aggregateResult.SourceCount} createdEpisodeCount={aggregateResult.CreatedEpisodeCount} additionalSourceCount={aggregateResult.AdditionalSourceCount}");
        }
        catch (Exception exception)
        {
            var message = DescribeException(exception);
            ManualAggregationStatusMessage = message.Contains("已存在同名剧集", StringComparison.Ordinal)
                ? message
                : $"聚合失败，事务已回滚：{message}";
            BatchResultSummary = ManualAggregationStatusMessage;
            ScanIdentificationDiagnostics.Write(
                $"event=manual-aggregate-identify-failed stage=\"aggregate\" sourceCount={request.Sources.Count} seasonNumber={request.SeasonNumber} failureReason={ScanIdentificationDiagnostics.FormatValue(message, 260)}");
            IsBatchOperationRunning = false;
            IsManualAggregationBusy = false;
            return;
        }

        var aggregateSummary = $"聚合完成：移动 {aggregateResult.SourceCount} 个播放源，创建 {aggregateResult.CreatedEpisodeCount} 集，重复集号追加 {aggregateResult.AdditionalSourceCount} 个播放源。";
        ManualAggregationStatusMessage = "聚合完成，正在 AI 识别新聚合季...";
        ScanIdentificationDiagnostics.Write(
            $"event=manual-aggregate-identify-season-ai-started unknownSeasonId={aggregateResult.SeasonId} sourceCount={aggregateResult.SourceCount}");

        var identifySummary = "已完成聚合，但 AI 未能安全识别，可后续手动修正。";
        var identifySucceeded = false;
        try
        {
            var selection = new BatchAiCorrectionSelectionItem
            {
                SelectionKey = $"season:{aggregateResult.SeasonId}",
                Title = string.IsNullOrWhiteSpace(ManualAggregationSeasonTitle)
                    ? ManualAggregationSeriesTitle.Trim()
                    : $"{ManualAggregationSeriesTitle.Trim()} {ManualAggregationSeasonTitle.Trim()}",
                SeriesTitle = ManualAggregationSeriesTitle.Trim(),
                ItemKind = LibraryMediaItemKind.Season,
                SeasonId = aggregateResult.SeasonId,
                IsInLibrary = true,
                HasActiveSource = aggregateResult.SourceCount > 0
            };
            var progress = new Progress<BatchAiCorrectionProgress>(progressValue =>
            {
                ManualAggregationStatusMessage =
                    $"正在 AI 识别聚合季 {Math.Min(progressValue.ProcessedCount, progressValue.TotalCount)} / {progressValue.TotalCount}：成功 {progressValue.SuccessCount}，跳过 {progressValue.SkippedCount}，失败 {progressValue.FailedCount}。";
            });

            var aiResult = await _batchAiCorrectionService.CorrectAsync([selection], progress);
            var unitResult = aiResult.UnitResults.FirstOrDefault(x => x.SeasonId == aggregateResult.SeasonId)
                             ?? aiResult.UnitResults.FirstOrDefault();
            ScanIdentificationDiagnostics.Write(
                $"event=manual-aggregate-identify-season-ai-result unknownSeasonId={aggregateResult.SeasonId} success={aiResult.SuccessCount} skipped={aiResult.SkippedCount} failed={aiResult.FailedCount} cancelled={aiResult.CancelledCount} unitStatus={ScanIdentificationDiagnostics.FormatValue(unitResult?.Status)} targetKind={ScanIdentificationDiagnostics.FormatValue(unitResult?.TargetKind)} message={ScanIdentificationDiagnostics.FormatValue(unitResult?.Message, 220)}");

            if (aiResult.SuccessCount > 0)
            {
                identifySucceeded = true;
                identifySummary = $"AI 识别成功，已将新聚合季修正到已识别季。{NormalizeAutoIdentifyMessage(unitResult?.Message ?? string.Empty, string.Empty)}";
                ScanIdentificationDiagnostics.Write(
                    $"event=manual-aggregate-identify-season-correction-succeeded unknownSeasonId={aggregateResult.SeasonId} sourceCount={aggregateResult.SourceCount} targetKind={ScanIdentificationDiagnostics.FormatValue(unitResult?.TargetKind)} message={ScanIdentificationDiagnostics.FormatValue(unitResult?.Message, 220)}");
            }
            else
            {
                var reason = NormalizeAutoIdentifyMessage(unitResult?.Message ?? string.Empty, "AI 未返回安全目标季。");
                identifySummary = $"已完成聚合，但 AI 未能安全识别，可后续手动修正。原因：{reason}";
                ScanIdentificationDiagnostics.Write(
                    $"event=manual-aggregate-identify-skipped unknownSeasonId={aggregateResult.SeasonId} skippedReason={ScanIdentificationDiagnostics.FormatValue(reason, 220)}");
            }
        }
        catch (Exception exception)
        {
            var message = DescribeException(exception);
            identifySummary = $"已完成聚合，但 AI 识别失败，可后续手动修正。原因：{message}";
            ScanIdentificationDiagnostics.Write(
                $"event=manual-aggregate-identify-failed stage=\"identify\" unknownSeasonId={aggregateResult.SeasonId} failureReason={ScanIdentificationDiagnostics.FormatValue(message, 260)}");
        }
        finally
        {
            var finalMessage = $"{aggregateSummary} {identifySummary}";
            ManualAggregationStatusMessage = finalMessage;
            BatchResultSummary = finalMessage;
            IsManualAggregationDialogOpen = false;
            ManualAggregationSources.Clear();
            ManualAggregationSeasonNumberText = "1";
            RefreshManualAggregationState();
            ClearSelection();
            IsBatchSelectionMode = false;
            NotifyAfterManualAggregation();
            ScanIdentificationDiagnostics.Write(
                $"event=manual-aggregate-identify-summary unknownSeasonId={aggregateResult.SeasonId} aggregateSucceeded=true identifySucceeded={identifySucceeded.ToString().ToLowerInvariant()} sourceCount={aggregateResult.SourceCount} createdEpisodeCount={aggregateResult.CreatedEpisodeCount} additionalSourceCount={aggregateResult.AdditionalSourceCount}");
            IsBatchOperationRunning = false;
            IsManualAggregationBusy = false;
            try
            {
                await RefreshLibraryAfterOperationAsync(RefreshReasonManualAggregation);
            }
            catch (Exception exception)
            {
                var refreshMessage = DescribeException(exception);
                BatchResultSummary = $"{finalMessage} 刷新媒体库失败：{refreshMessage}";
                ScanIdentificationDiagnostics.Write(
                    $"event=manual-aggregate-identify-refresh-failed unknownSeasonId={aggregateResult.SeasonId} failureReason={ScanIdentificationDiagnostics.FormatValue(refreshMessage, 260)}");
            }
        }
    }

    private ManualUnknownSeasonAggregationApplyRequest BuildManualAggregationApplyRequest()
    {
        return new ManualUnknownSeasonAggregationApplyRequest
        {
            SeriesTitle = ManualAggregationSeriesTitle.Trim(),
            SeasonTitle = ManualAggregationSeasonTitle.Trim(),
            SeasonNumber = ManualAggregationSeasonNumber.GetValueOrDefault(),
            Sources = ManualAggregationSources
                .Select(row => new ManualUnknownSeasonAggregationSourceAssignment
                {
                    MediaFileId = row.MediaFileId,
                    EpisodeNumber = row.ParsedEpisodeNumber.GetValueOrDefault()
                })
                .ToArray()
        };
    }

    private void CancelManualAggregation()
    {
        if (IsManualAggregationBusy)
        {
            return;
        }

        IsManualAggregationDialogOpen = false;
        ManualAggregationSources.Clear();
        ManualAggregationSeasonNumberText = "1";
        RefreshManualAggregationState();
        ManualAggregationStatusMessage = "已取消人工聚合。";
        BatchResultSummary = "已取消人工聚合。";
    }

    private async Task BatchAutoIdentifyCrossTypeAsync()
    {
        var selectedItems = GetSelectedVisibleItems();
        if (selectedItems.Count == 0)
        {
            ClearSelection();
            BatchResultSummary = "没有可识别的已选项目。";
            return;
        }

        var selections = selectedItems
            .Select(BuildBatchAiCorrectionSelection)
            .ToArray();
        _batchIdentifyCancellationTokenSource = new CancellationTokenSource();
        IsBatchOperationRunning = true;
        ClearSelection();
        IsBatchSelectionMode = false;
        BatchResultSummary = $"正在准备批量 AI 辅助识别：已选择 {selectedItems.Count} 项，正在切回普通视图。";

        var batchStopwatch = Stopwatch.StartNew();
        WriteBatch2Event($"event=batch2-ai-identify-batch-start count={selectedItems.Count} mode=cross-type");

        try
        {
            var cancellationToken = _batchIdentifyCancellationTokenSource.Token;
            var initialRefreshStopwatch = Stopwatch.StartNew();
            WriteBatch2Event("event=batch2-ai-identify-initial-refresh-start reason=exit-batch-selection");
            await RefreshLibraryAfterOperationAsync(RefreshReasonBatchAiExitBatchMode, cancellationToken);
            initialRefreshStopwatch.Stop();
            WriteBatch2Event($"event=batch2-ai-identify-initial-refresh-complete elapsedMs={initialRefreshStopwatch.ElapsedMilliseconds}");
            BatchResultSummary = $"正在批量 AI 辅助识别 0 / {selectedItems.Count}：正在创建处理单元。";

            var progress = new Progress<BatchAiCorrectionProgress>(UpdateBatchAutoIdentifyProgress);
            var result = await _batchAiCorrectionService.CorrectAsync(selections, progress, cancellationToken);
            var retainedItems = BuildRetainedBatchAiItems(result);

            var refreshStopwatch = Stopwatch.StartNew();
            WriteBatch2Event("event=batch2-ai-identify-refresh-start");
            await RefreshLibraryAfterOperationAsync(RefreshReasonBatchAiResult);
            refreshStopwatch.Stop();
            WriteBatch2Event($"event=batch2-ai-identify-refresh-complete elapsedMs={refreshStopwatch.ElapsedMilliseconds}");
            BatchResultSummary = BuildAutoIdentifyResultSummary(result, retainedItems);
            if (result.SuccessCount > 0)
            {
                NotifyAfterBatchIdentification();
            }

            batchStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-batch-complete elapsedMs={batchStopwatch.ElapsedMilliseconds} success={result.SuccessCount} skipped={result.SkippedCount} failed={result.FailedCount} cancelled={result.CancelledCount}");
        }
        catch (OperationCanceledException)
        {
            BatchResultSummary = "批量 AI 辅助识别已取消。";
            WriteBatch2Event("event=batch2-ai-identify-batch-cancelled");
        }
        catch (Exception exception)
        {
            var message = DescribeException(exception);
            BatchResultSummary = $"批量 AI 辅助识别失败：{message}";
            WriteBatch2Event(
                $"event=batch2-ai-identify-batch-failed failureReason=\"{AiPerfDiagnostics.SanitizeMessage(message)}\"");
        }
        finally
        {
            _batchIdentifyCancellationTokenSource.Dispose();
            _batchIdentifyCancellationTokenSource = null;
            IsBatchOperationRunning = false;
            RefreshBatchCommandState();
        }
    }

    private async Task BatchAutoIdentifyAsync()
    {
        var selectedItems = GetSelectedVisibleItems();
        if (selectedItems.Count == 0)
        {
            ClearSelection();
            BatchResultSummary = "没有可识别的已选影片。";
            return;
        }

        if (selectedItems.Any(item => !item.Movie.IsMovie))
        {
            BatchResultSummary = "AI 辅助识别仅支持电影；电视剧季修正入口已分离。";
            return;
        }

        var successCount = 0;
        var noResultCount = 0;
        var failedCount = 0;
        var cancelledCount = 0;
        var retainedItems = new List<BatchItemError>();
        _batchIdentifyCancellationTokenSource = new CancellationTokenSource();
        IsBatchOperationRunning = true;
        var batchStopwatch = Stopwatch.StartNew();
        WriteBatch2Event($"event=batch2-ai-identify-batch-start count={selectedItems.Count}");

        try
        {
            var cancellationToken = _batchIdentifyCancellationTokenSource.Token;
            for (var index = 0; index < selectedItems.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelledCount += AddCancelledItems(selectedItems, index, retainedItems);
                    break;
                }

                var item = selectedItems[index];
                var itemStopwatch = Stopwatch.StartNew();
                WriteBatch2Event(
                    $"event=batch2-ai-identify-item-start movieId={item.MovieId} index={index + 1} total={selectedItems.Count}");
                UpdateBatchAutoIdentifyProgress(index + 1, selectedItems.Count, successCount, noResultCount, failedCount, cancelledCount);

                if (!item.IsInLibrary || item.MovieId <= 0)
                {
                    failedCount++;
                    retainedItems.Add(new BatchItemError(item.SelectionKey, item.Title, "仅有播放源影片支持 AI 辅助识别。"));
                    UpdateBatchAutoIdentifyProgress(index + 1, selectedItems.Count, successCount, noResultCount, failedCount, cancelledCount);
                    itemStopwatch.Stop();
                    WriteBatch2Event(
                        $"event=batch2-ai-identify-item-complete movieId={item.MovieId} elapsedMs={itemStopwatch.ElapsedMilliseconds} status=failed");
                    continue;
                }

                var result = await _movieIdentificationService.AutoIdentifyWithFirstResultAsync(item.MovieId, cancellationToken);
                switch (result.Status)
                {
                    case AutoIdentifyStatus.Success:
                        successCount++;
                        break;
                    case AutoIdentifyStatus.NoResult:
                        noResultCount++;
                        retainedItems.Add(new BatchItemError(item.SelectionKey, item.Title, NormalizeAutoIdentifyMessage(result.Message, "无可用识别结果。")));
                        break;
                    case AutoIdentifyStatus.Cancelled:
                        cancelledCount += AddCancelledItems(selectedItems, index, retainedItems);
                        break;
                    default:
                        failedCount++;
                        retainedItems.Add(new BatchItemError(item.SelectionKey, item.Title, NormalizeAutoIdentifyMessage(result.Message, "AI 辅助识别失败。")));
                        break;
                }

                UpdateBatchAutoIdentifyProgress(index + 1, selectedItems.Count, successCount, noResultCount, failedCount, cancelledCount);
                itemStopwatch.Stop();
                WriteBatch2Event(
                    $"event=batch2-ai-identify-item-complete movieId={item.MovieId} elapsedMs={itemStopwatch.ElapsedMilliseconds} status={FormatAutoIdentifyStatus(result.Status)}");

                if (result.Status == AutoIdentifyStatus.Cancelled)
                {
                    break;
                }
            }

            var wasCancelled = cancelledCount > 0 || _batchIdentifyCancellationTokenSource.IsCancellationRequested;
            if (successCount > 0 && (!wasCancelled || retainedItems.Count == 0))
            {
                ClearSelection();
                IsBatchSelectionMode = false;
            }
            else
            {
                SetSelectionToFailures(retainedItems);
            }

            var refreshStopwatch = Stopwatch.StartNew();
            WriteBatch2Event("event=batch2-ai-identify-refresh-start");
            await RefreshLibraryAfterOperationAsync(RefreshReasonBatchAiResult);
            refreshStopwatch.Stop();
            WriteBatch2Event($"event=batch2-ai-identify-refresh-complete elapsedMs={refreshStopwatch.ElapsedMilliseconds}");
            BatchResultSummary = BuildAutoIdentifyResultSummary(successCount, noResultCount, failedCount, cancelledCount, retainedItems);
            if (successCount > 0)
            {
                NotifyAfterBatchIdentification();
            }

            batchStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-batch-complete elapsedMs={batchStopwatch.ElapsedMilliseconds} success={successCount} noResult={noResultCount} failed={failedCount} cancelled={cancelledCount}");
        }
        finally
        {
            _batchIdentifyCancellationTokenSource.Dispose();
            _batchIdentifyCancellationTokenSource = null;
            IsBatchOperationRunning = false;
            RefreshBatchCommandState();
        }
    }

    private void CancelBatchOperation()
    {
        if (_batchIdentifyCancellationTokenSource is null
            || _batchIdentifyCancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        _batchIdentifyCancellationTokenSource.Cancel();
        BatchResultSummary = "正在取消批量 AI 辅助识别，当前项结束后停止后续项。";
        RefreshBatchCommandState();
    }

    private IReadOnlyList<LibraryMovieItemViewModel> GetSelectedVisibleItems()
    {
        if (_selectedItemKeys.Count == 0)
        {
            return [];
        }

        return Movies
            .Where(item => item.IsSelected && _selectedItemKeys.Contains(item.SelectionKey))
            .ToList();
    }

    private static ManualUnknownSeasonAggregationSelection BuildManualAggregationSelection(LibraryMovieItemViewModel item)
    {
        return new ManualUnknownSeasonAggregationSelection
        {
            SelectionKey = item.SelectionKey,
            MovieId = item.Movie.MovieId,
            SeasonId = item.Movie.SeasonId,
            OrphanMediaFileId = item.Movie.OrphanMediaFileId,
            GroupedRangeMediaFileIds = item.Movie.GroupedRangeMediaFileIds
        };
    }

    private static BatchAiCorrectionSelectionItem BuildBatchAiCorrectionSelection(LibraryMovieItemViewModel item)
    {
        return new BatchAiCorrectionSelectionItem
        {
            SelectionKey = item.SelectionKey,
            Title = item.Title,
            SeriesTitle = item.SeriesTitle,
            ItemKind = item.Movie.ItemKind,
            MovieId = item.Movie.MovieId,
            SeriesId = item.Movie.SeriesId,
            SeasonId = item.Movie.SeasonId,
            OrphanMediaFileId = item.Movie.OrphanMediaFileId,
            GroupedRangeMediaFileIds = item.Movie.GroupedRangeMediaFileIds,
            IsInLibrary = item.IsInLibrary,
            HasActiveSource = item.HasActiveSource
        };
    }

    private static bool CanUseForManualUnknownSeasonAggregation(LibraryMovieItemViewModel item)
    {
        if (!item.HasActiveSource)
        {
            return false;
        }

        var movie = item.Movie;
        if (movie.IsOther && movie.OrphanMediaFileId > 0)
        {
            return true;
        }

        if (IsGroupedPlaceholder(movie))
        {
            return true;
        }

        if ((movie.IsSeason || movie.IsOther)
            && movie.SeasonId > 0
            && movie.TmdbId is null
            && movie.IdentificationStatus == IdentificationStatus.Failed)
        {
            return true;
        }

        return (movie.IsMovie || movie.IsOther)
               && movie.MovieId > 0
               && movie.TmdbId is null
               && movie.IdentificationStatus == IdentificationStatus.Failed;
    }

    private string? ValidateManualAggregationInput()
    {
        if (string.IsNullOrWhiteSpace(ManualAggregationSeriesTitle))
        {
            return "请输入剧名。";
        }

        if (string.IsNullOrWhiteSpace(ManualAggregationSeasonTitle))
        {
            return "请输入季名称。";
        }

        if (ManualAggregationSeasonNumber is null)
        {
            WriteLibraryBatchEvent("event=manual-season-aggregate-season-number-invalid");
            return "季号必须是 0 或正整数。";
        }

        if (ManualAggregationSources.Count == 0)
        {
            return "没有可聚合的播放源。";
        }

        var invalidRows = ManualAggregationSources
            .Where(row => row.ParsedEpisodeNumber is null)
            .Take(3)
            .Select(row => row.FileName)
            .ToArray();
        if (invalidRows.Length == 0)
        {
            return null;
        }

        return $"集号必须是正整数，请检查：{string.Join("；", invalidRows)}";
    }

    private void RefreshManualAggregationState()
    {
        OnPropertyChanged(nameof(HasManualAggregationSources));
        OnPropertyChanged(nameof(CanApplyManualAggregation));
        ApplyManualAggregationCommand.RaiseCanExecuteChanged();
        ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
        CancelManualAggregationCommand.RaiseCanExecuteChanged();
    }

    private void SetSelectionToFailures(IEnumerable<BatchItemError> errors)
    {
        _selectedItemKeys.Clear();
        foreach (var error in errors)
        {
            _selectedItemKeys.Add(error.SelectionKey);
        }

        foreach (var movie in Movies)
        {
            movie.IsSelected = _selectedItemKeys.Contains(movie.SelectionKey);
        }

        RefreshBatchCommandState();
    }

    private void ReconcileSelectionWithVisibleItems(IReadOnlyCollection<LibraryMovieListItem> visibleItems)
    {
        if (!IsBatchSelectionMode)
        {
            _selectedItemKeys.Clear();
            return;
        }

        var visibleKeys = visibleItems
            .Select(BuildSelectionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _selectedItemKeys.RemoveWhere(key => !visibleKeys.Contains(key));
    }

    private void ClearSelection()
    {
        _selectedItemKeys.Clear();
        foreach (var movie in Movies)
        {
            movie.IsSelected = false;
        }

        RefreshBatchCommandState();
    }

    private void RefreshBatchCommandState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanSelectVisibleItems));
        OnPropertyChanged(nameof(CanClearBatchSelection));
        OnPropertyChanged(nameof(CanBatchMarkWatched));
        OnPropertyChanged(nameof(CanBatchMarkUnwatched));
        OnPropertyChanged(nameof(CanBatchAutoIdentify));
        OnPropertyChanged(nameof(CanBatchRemoveFromLibrary));
        OnPropertyChanged(nameof(CanBatchDeleteMovieRecords));
        OnPropertyChanged(nameof(CanBatchManualAggregate));
        OnPropertyChanged(nameof(CanCancelBatchOperation));
        OnPropertyChanged(nameof(BatchSelectionButtonText));
        if (_allMovies.Count > 0)
        {
            StatusMessage = BuildResultStatusMessage(Movies.Select(item => item.Movie).ToArray());
        }

        ToggleBatchSelectionModeCommand.RaiseCanExecuteChanged();
        SelectVisibleItemsCommand.RaiseCanExecuteChanged();
        ClearBatchSelectionCommand.RaiseCanExecuteChanged();
        BatchMarkWatchedCommand.RaiseCanExecuteChanged();
        BatchMarkUnwatchedCommand.RaiseCanExecuteChanged();
        BatchAutoIdentifyCommand.RaiseCanExecuteChanged();
        CancelBatchOperationCommand.RaiseCanExecuteChanged();
        BatchRemoveFromLibraryCommand.RaiseCanExecuteChanged();
        BatchDeleteMovieRecordsCommand.RaiseCanExecuteChanged();
        OpenManualAggregationCommand.RaiseCanExecuteChanged();
        ApplyManualAggregationCommand.RaiseCanExecuteChanged();
        ApplyManualAggregationAndIdentifyCommand.RaiseCanExecuteChanged();
        CancelManualAggregationCommand.RaiseCanExecuteChanged();
    }

    private void NotifyAfterBatchStatusChange()
    {
        _dataRefreshService.NotifyMetadataChanged();
        _dataRefreshService.NotifyCollectionChanged();
        _dataRefreshService.NotifyRecommendationChanged();
    }

    private void NotifyAfterBatchRemoveFromLibrary()
    {
        _dataRefreshService.NotifyLibraryChanged();
        _dataRefreshService.NotifyCollectionChanged();
        _dataRefreshService.NotifyRecommendationChanged();
    }

    private void NotifyAfterBatchMovieRecordDelete()
    {
        _dataRefreshService.NotifyLibraryChanged();
        _dataRefreshService.NotifyPlaybackChanged();
        _dataRefreshService.NotifyMetadataChanged();
        _dataRefreshService.NotifyCollectionChanged();
        _dataRefreshService.NotifyRecommendationChanged();
    }

    private void NotifyAfterManualAggregation()
    {
        _dataRefreshService.NotifyLibraryChanged();
        _dataRefreshService.NotifyPlaybackChanged();
        _dataRefreshService.NotifyMetadataChanged();
        _dataRefreshService.NotifyCollectionChanged();
    }

    private void NotifyAfterBatchIdentification()
    {
        _suppressLibraryRefreshFromBatchNotification = true;
        try
        {
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyCollectionChanged();
        }
        finally
        {
            _suppressLibraryRefreshFromBatchNotification = false;
        }

        WriteBatch2Event("event=batch2-ai-identify-recommendation-refresh-deferred reason=avoid-immediate-ai-refresh");
    }

    private void OpenMovie(object? parameter)
    {
        _ = OpenMovieAsync(parameter);
    }

    private async Task OpenMovieAsync(object? parameter)
    {
        var movie = parameter switch
        {
            LibraryMovieItemViewModel item => item.Movie,
            LibraryMovieListItem listItem => listItem,
            _ => null
        };

        if (movie is null)
        {
            return;
        }

        if (movie.IsOther && movie.OrphanMediaFileId > 0)
        {
            await OpenOrphanUnknownMovieDetailAsync(movie.OrphanMediaFileId);
            return;
        }

        if (IsGroupedPlaceholder(movie) && movie.SeriesId <= 0 && movie.SeasonId <= 0)
        {
            StatusMessage = "该项目是未识别剧集候选，当前无法定位可打开的剧或季详情。";
            return;
        }

        if ((movie.IsSeries || (movie.IsOther && movie.SeriesId > 0 && movie.SeasonId == 0))
            && movie.SeriesId > 0)
        {
            _navigationStateService.RequestTvSeriesOverview(movie.SeriesId);
            return;
        }

        if ((movie.IsSeason || movie.IsOther) && movie.SeasonId > 0)
        {
            _navigationStateService.RequestTvSeasonDetail(movie.SeasonId);
            return;
        }

        if (movie.MovieId > 0)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movie.MovieId);
            return;
        }

        if (movie.IsOther)
        {
            StatusMessage = "该项目是未识别 / 待修正项目，当前没有可打开的详情。";
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(BuildRecommendationItem(movie));
    }

    private async Task OpenOrphanUnknownMovieDetailAsync(int mediaFileId)
    {
        try
        {
            StatusMessage = "正在打开未识别文件详情。";
            var movieId = await _movieManagementService.EnsureUnidentifiedMoviePlaceholderForMediaFileAsync(mediaFileId);
            _dataRefreshService.NotifyLibraryChanged();
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movieId);
        }
        catch (Exception exception)
        {
            StatusMessage = $"打开未识别文件详情失败：{DescribeException(exception)}";
        }
    }

    private static AiRecommendationItem BuildRecommendationItem(LibraryMovieListItem movie)
    {
        return new AiRecommendationItem
        {
            MovieId = movie.MovieId,
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
            IsInLibrary = movie.IsInLibrary,
            IsVisibleInLibrary = movie.IsVisibleInLibrary,
            LibraryVisibilityState = movie.LibraryVisibilityState,
            IsWatched = movie.IsWatched,
            IsWantToWatch = movie.IsWantToWatch,
            IsNotInterested = movie.IsNotInterested,
            ScopeText = "媒体库",
            AvailabilityText = movie.HasActiveSource ? "有播放源" : "暂无播放源",
            WatchStateText = movie.IsWatched ? "已看" : "未看"
        };
    }

    private static bool IsGroupedPlaceholder(LibraryMovieListItem item)
    {
        return item.IsOther
               && !string.IsNullOrWhiteSpace(item.GroupedRangeKey)
               && item.GroupedRangeMediaFileIds.Count > 0;
    }

    private static string BuildSelectionKey(LibraryMovieListItem item)
    {
        if (item.IsOther && !string.IsNullOrWhiteSpace(item.GroupedRangeKey))
        {
            return $"other:{item.GroupedRangeKey}";
        }

        if (item.SeasonId > 0)
        {
            return $"season:{item.SeasonId}";
        }

        if (item.SeriesId > 0)
        {
            return $"series:{item.SeriesId}";
        }

        if (item.MovieId > 0)
        {
            return $"movie:{item.MovieId}";
        }

        if (item.TmdbId.HasValue)
        {
            return $"tmdb:{item.TmdbId.Value}";
        }

        return $"title:{item.Title.Trim().ToLowerInvariant()}:{item.ReleaseYear?.ToString() ?? string.Empty}";
    }

    private static int AddCancelledItems(
        IReadOnlyList<LibraryMovieItemViewModel> items,
        int startIndex,
        ICollection<BatchItemError> retainedItems)
    {
        for (var index = startIndex; index < items.Count; index++)
        {
            var item = items[index];
            retainedItems.Add(new BatchItemError(item.SelectionKey, item.Title, "已取消，未执行。"));
        }

        return Math.Max(0, items.Count - startIndex);
    }

    private void UpdateBatchAutoIdentifyProgress(
        int currentIndex,
        int totalCount,
        int successCount,
        int noResultCount,
        int failedCount,
        int cancelledCount)
    {
        BatchResultSummary = $"正在 AI 辅助识别 {Math.Min(currentIndex, totalCount)} / {totalCount}：成功 {successCount}，无结果 {noResultCount}，失败 {failedCount}，已取消 {cancelledCount}。";
    }

    private void UpdateBatchAutoIdentifyProgress(BatchAiCorrectionProgress progress)
    {
        BatchResultSummary =
            $"正在批量 AI 辅助识别 {Math.Min(progress.ProcessedCount, progress.TotalCount)} / {progress.TotalCount}：成功 {progress.SuccessCount}，跳过 {progress.SkippedCount}，失败 {progress.FailedCount}，已取消 {progress.CancelledCount}。当前：{progress.CurrentTitle}";
    }

    private static string BuildResultSummary(
        string operationName,
        int successCount,
        IReadOnlyCollection<BatchItemError> errors)
    {
        return BuildResultSummary(operationName, successCount, Array.Empty<BatchItemError>(), errors);
    }

    private static string BuildRemoveFromLibraryResultSummary(
        int successCount,
        int hiddenCount,
        IReadOnlyCollection<BatchItemError> errors)
    {
        var summary = $"移出媒体库完成：处理 {successCount} 项，已从媒体库隐藏 {hiddenCount} 项，失败 {errors.Count} 项。";
        if (errors.Count == 0)
        {
            return summary;
        }

        var preview = string.Join(
            "；",
            errors
                .Take(3)
                .Select(error => $"{error.Title}：{error.Message}"));
        var suffix = errors.Count > 3 ? $"；另有 {errors.Count - 3} 项失败" : string.Empty;
        return $"{summary} 失败项：{preview}{suffix}";
    }

    private static string BuildResultSummary(
        string operationName,
        int successCount,
        IReadOnlyCollection<BatchItemError> skipped,
        IReadOnlyCollection<BatchItemError> errors)
    {
        var summary = $"{operationName}完成：成功 {successCount}，跳过 {skipped.Count}，失败 {errors.Count}。";
        if (skipped.Count == 0 && errors.Count == 0)
        {
            return summary;
        }

        var parts = new List<string>();
        if (skipped.Count > 0)
        {
            var skippedPreview = string.Join(
                "；",
                skipped
                    .Take(3)
                    .Select(item => $"{item.Title}：{item.Message}"));
            var skippedSuffix = skipped.Count > 3 ? $"；另有 {skipped.Count - 3} 项跳过" : string.Empty;
            parts.Add($"跳过项：{skippedPreview}{skippedSuffix}");
        }

        if (errors.Count == 0)
        {
            return $"{summary} {string.Join(" ", parts)}";
        }

        var preview = string.Join(
            "；",
            errors
                .Take(3)
                .Select(error => $"{error.Title}：{error.Message}"));
        var suffix = errors.Count > 3 ? $"；另有 {errors.Count - 3} 项失败" : string.Empty;
        parts.Add($"失败项：{preview}{suffix}");
        return $"{summary} {string.Join(" ", parts)}";
    }

    private static IReadOnlyList<BatchItemError> BuildRetainedBatchAiItems(BatchAiCorrectionRunResult result)
    {
        return result.UnitResults
            .Where(item => !string.Equals(item.Status, "success", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.SelectionKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var message = group.Count() == 1
                    ? first.Message
                    : $"{group.Count()} 个处理单元未成功，首个原因：{first.Message}";
                return new BatchItemError(first.SelectionKey, first.Title, message);
            })
            .ToList();
    }

    private static string BuildAutoIdentifyResultSummary(
        BatchAiCorrectionRunResult result,
        IReadOnlyCollection<BatchItemError> retainedItems)
    {
        var summary = $"AI 辅助识别完成：成功 {result.SuccessCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}，已取消 {result.CancelledCount}。";
        if (retainedItems.Count == 0)
        {
            return summary;
        }

        var preview = string.Join(
            "；",
            retainedItems
                .Take(3)
                .Select(item => $"{item.Title}：{item.Message}"));
        var suffix = retainedItems.Count > 3 ? $"；另有 {retainedItems.Count - 3} 项未成功" : string.Empty;
        return $"{summary} 未成功项：{preview}{suffix}";
    }

    private static string BuildAutoIdentifyResultSummary(
        int successCount,
        int noResultCount,
        int failedCount,
        int cancelledCount,
        IReadOnlyCollection<BatchItemError> retainedItems)
    {
        var summary = $"AI 辅助识别完成：成功 {successCount}，无结果 {noResultCount}，失败 {failedCount}，已取消 {cancelledCount}。无结果影片未重置原识别。";
        if (retainedItems.Count == 0)
        {
            return summary;
        }

        var preview = string.Join(
            "；",
            retainedItems
                .Take(3)
                .Select(item => $"{item.Title}：{item.Message}"));
        var suffix = retainedItems.Count > 3 ? $"；另有 {retainedItems.Count - 3} 项未成功" : string.Empty;
        return $"{summary} 未成功项：{preview}{suffix}";
    }

    private static string NormalizeAutoIdentifyMessage(string message, string fallback)
    {
        return string.IsNullOrWhiteSpace(message) ? fallback : message.Trim();
    }

    private static void WriteBatch2Event(string message)
    {
        AiPerfDiagnostics.WriteEvent(message);
    }

    private static void WriteLibraryBatchEvent(string message)
    {
        AiPerfDiagnostics.WriteEvent(message);
    }

    private static void WriteLibraryRefreshEvent(string eventName, string fields)
    {
        AiPerfDiagnostics.WriteEvent($"event={eventName} {fields}");
    }

    private static string FormatSelectionKeyForLog(string selectionKey)
    {
        if (selectionKey.StartsWith("movie:", StringComparison.OrdinalIgnoreCase)
            || selectionKey.StartsWith("tmdb:", StringComparison.OrdinalIgnoreCase))
        {
            return selectionKey;
        }

        return "external-title";
    }

    private static string FormatAutoIdentifyStatus(AutoIdentifyStatus status)
    {
        return status switch
        {
            AutoIdentifyStatus.Success => "success",
            AutoIdentifyStatus.NoResult => "no-result",
            AutoIdentifyStatus.Failed => "failed",
            AutoIdentifyStatus.Cancelled => "cancelled",
            _ => status.ToString().ToLowerInvariant()
        };
    }

    private static string DescribeException(Exception exception)
    {
        return exception.InnerException?.Message ?? exception.Message;
    }

    public sealed class TagFilterOption : ObservableObject
    {
        private readonly Action<TagFilterOption> _selectionChanged;
        private bool _isSelected;

        public TagFilterOption(string category, string label, Action<TagFilterOption> selectionChanged)
        {
            Category = category;
            Label = label;
            _selectionChanged = selectionChanged;
        }

        public string Category { get; }

        public string Label { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    _selectionChanged(this);
                }
            }
        }
    }

    private sealed record BatchItemError(string SelectionKey, string Title, string Message);
}
