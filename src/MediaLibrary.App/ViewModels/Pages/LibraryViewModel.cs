using System.Diagnostics;
using System.Collections.ObjectModel;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
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
    private readonly IUserCollectionService _userCollectionService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IManualUnknownSeasonAggregationService _manualUnknownSeasonAggregationService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly List<LibraryMovieListItem> _allMovies = [];
    private readonly HashSet<string> _selectedItemKeys = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _batchIdentifyCancellationTokenSource;
    private bool _suppressLibraryRefreshFromBatchNotification;
    private string _searchText = string.Empty;
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
        IUserCollectionService userCollectionService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IManualUnknownSeasonAggregationService manualUnknownSeasonAggregationService,
        IConfirmationDialogService confirmationDialogService)
        : base("媒体库", "浏览真实影片数据，支持搜索、排序、筛选和批量操作。")
    {
        _libraryQueryService = libraryQueryService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _movieManagementService = movieManagementService;
        _movieIdentificationService = movieIdentificationService;
        _userCollectionService = userCollectionService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _manualUnknownSeasonAggregationService = manualUnknownSeasonAggregationService;
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
        SwitchToPosterViewCommand = new RelayCommand(() => IsPosterView = true);
        SwitchToListViewCommand = new RelayCommand(() => IsPosterView = false);
        SelectLibraryScopeCommand = new RelayCommand(SelectLibraryScope);
        SelectSourceFilterCommand = new RelayCommand(SelectSourceFilter);
        SelectContentTypeCommand = new RelayCommand(SelectContentType);
        ClearTagFilterCommand = new RelayCommand(ClearTagFilter);
        SelectCollectionStatusCommand = new RelayCommand(SelectCollectionStatus);
        ShowLayoutSwitchPlaceholderCommand = new RelayCommand(ShowLayoutSwitchPlaceholder);
        OpenMovieCommand = new RelayCommand(OpenMovie);
        OpenOrToggleSelectionCommand = new RelayCommand(OpenOrToggleSelection);
        ToggleItemSelectionCommand = new RelayCommand(ToggleItemSelection);
        ToggleBatchSelectionModeCommand = new RelayCommand(ToggleBatchSelectionMode, () => !IsBatchOperationRunning);
        SelectVisibleItemsCommand = new RelayCommand(SelectVisibleItems, () => CanSelectVisibleItems);
        ClearBatchSelectionCommand = new RelayCommand(ClearBatchSelection, () => CanClearBatchSelection);
        BatchMarkWatchedCommand = new AsyncRelayCommand(() => BatchSetWatchedAsync(true), () => CanBatchMarkWatched);
        BatchMarkUnwatchedCommand = new AsyncRelayCommand(() => BatchSetWatchedAsync(false), () => CanBatchMarkUnwatched);
        BatchAutoIdentifyCommand = new AsyncRelayCommand(BatchAutoIdentifyAsync, () => CanBatchAutoIdentify);
        CancelBatchOperationCommand = new RelayCommand(CancelBatchOperation, () => CanCancelBatchOperation);
        BatchRemoveFromLibraryCommand = new AsyncRelayCommand(BatchRemoveFromLibraryAsync, () => CanBatchRemoveFromLibrary);
        BatchDeleteMovieRecordsCommand = new AsyncRelayCommand(BatchDeleteMovieRecordsAsync, () => CanBatchDeleteMovieRecords);
        OpenManualAggregationCommand = new AsyncRelayCommand(OpenManualAggregationAsync, () => CanBatchManualAggregate);
        ApplyManualAggregationCommand = new AsyncRelayCommand(ApplyManualAggregationAsync, () => CanApplyManualAggregation);
        CancelManualAggregationCommand = new RelayCommand(CancelManualAggregation, () => IsManualAggregationDialogOpen && !IsManualAggregationBusy);
        OpenRemovedLibraryCommand = new AsyncRelayCommand(OpenRemovedLibraryAsync, () => !IsRemovedLibraryLoading);
        CloseRemovedLibraryCommand = new RelayCommand(CloseRemovedLibrary);
        RestoreRemovedLibraryItemCommand = new AsyncRelayCommand(RestoreRemovedLibraryItemAsync, _ => !IsRemovedLibraryLoading);
        DeleteRemovedLibraryItemCommand = new AsyncRelayCommand(DeleteRemovedLibraryItemAsync, _ => !IsRemovedLibraryLoading);
        RestoreRemovedLibraryGroupCommand = new AsyncRelayCommand(RestoreRemovedLibraryGroupAsync, parameter => !IsRemovedLibraryLoading && parameter is RemovedLibraryGroupViewModel { IsTvGroup: true });
        DeleteRemovedLibraryGroupCommand = new AsyncRelayCommand(DeleteRemovedLibraryGroupAsync, parameter => !IsRemovedLibraryLoading && parameter is RemovedLibraryGroupViewModel { IsTvGroup: true });
        OpenRemovedLibraryDetailCommand = new RelayCommand(OpenMovie);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
        ApplySearchCommand = new RelayCommand(ApplyFilters);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        RefreshTagOptions();
    }

    public ObservableCollection<LibraryMovieItemViewModel> Movies { get; } = [];

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

    public RelayCommand ShowLayoutSwitchPlaceholderCommand { get; }

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

    public RelayCommand ClearFiltersCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
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

    private int? ManualAggregationSeasonNumber => int.TryParse(ManualAggregationSeasonNumberText, out var value) && value > 0
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

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var movies = await _libraryQueryService.GetLibraryItemsAsync(IsBatchSelectionMode, cancellationToken);
            _allMovies.Clear();
            _allMovies.AddRange(movies);
            RefreshTagOptions();
            RefreshDecadeOptions();
            ApplyFilters();

            if (_allMovies.Count == 0)
            {
                StatusMessage = "当前还没有可展示的影片数据。请先到扫描任务页执行扫描。";
            }
        }
        catch (Exception exception)
        {
            Movies.Clear();
            OnPropertyChanged(nameof(HasMovies));
            StatusMessage = $"加载媒体库失败：{exception.Message}";
        }
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
            await ActivateAsync();
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
            await ActivateAsync();
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
            "取消");
        if (!confirmed)
        {
            return;
        }

        try
        {
            if (IsGroupedPlaceholder(item.Movie))
            {
                await _movieManagementService.DeleteGroupedPlaceholderRangeRecordAsync(item.Movie.GroupedRangeMediaFileIds);
            }
            else if ((item.IsSeason || item.Movie.IsOther) && item.SeasonId > 0)
            {
                await _tvSeasonCollectionService.DeleteSeasonRecordAsync(item.SeasonId);
            }
            else if ((item.IsMovie || item.Movie.IsOther) && item.MovieId > 0)
            {
                await _movieManagementService.DeleteMovieRecordAsync(item.MovieId);
            }
            else if (item.IsMovie)
            {
                await _userCollectionService.DeleteCollectionRecordAsync(BuildRecommendationItem(item.Movie));
            }
            else
            {
                RemovedLibraryStatusMessage = "电视剧总览没有独立删除记录，请处理具体 Season。";
                return;
            }

            RemovedLibraryStatusMessage = $"已删除记录：{item.Title}";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await LoadRemovedLibraryItemsAsync();
            await ActivateAsync();
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
            "取消");
        if (!confirmed)
        {
            return;
        }

        try
        {
            foreach (var item in items)
            {
                if (IsGroupedPlaceholder(item.Movie))
                {
                    await _movieManagementService.DeleteGroupedPlaceholderRangeRecordAsync(item.Movie.GroupedRangeMediaFileIds);
                }
                else if ((item.IsSeason || item.Movie.IsOther) && item.SeasonId > 0)
                {
                    await _tvSeasonCollectionService.DeleteSeasonRecordAsync(item.SeasonId);
                }
                else if ((item.IsMovie || item.Movie.IsOther) && item.MovieId > 0)
                {
                    await _movieManagementService.DeleteMovieRecordAsync(item.MovieId);
                }
                else if (item.IsMovie)
                {
                    await _userCollectionService.DeleteCollectionRecordAsync(BuildRecommendationItem(item.Movie));
                }
            }

            RemovedLibraryStatusMessage = $"已删除记录：{group.Title}（{items.Length} 项）";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await LoadRemovedLibraryItemsAsync();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            RemovedLibraryStatusMessage = $"删除记录失败：{exception.Message}";
        }
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (e.LibraryChanged || e.Reason == AppDataChangeReason.CollectionChanged)
        {
            if (_suppressLibraryRefreshFromBatchNotification)
            {
                return;
            }

            _ = ActivateAsync();
        }
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
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

    private void ShowLayoutSwitchPlaceholder()
    {
        BatchResultSummary = "布局切换将在后续阶段接入。";
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
        var query = _allMovies.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
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
        ReconcileSelectionWithVisibleItems(filtered);

        Movies.Clear();
        foreach (var movie in filtered)
        {
            var selectionKey = BuildSelectionKey(movie);
            Movies.Add(
                new LibraryMovieItemViewModel(
                    movie,
                    selectionKey,
                    IsBatchSelectionMode,
                    _selectedItemKeys.Contains(selectionKey)));
        }

        OnPropertyChanged(nameof(HasMovies));
        RefreshBatchCommandState();
        StatusMessage = _allMovies.Count == 0
            ? "当前还没有可展示的影片数据。请先到扫描任务页执行扫描。"
            : BuildResultStatusMessage(filtered.Count);
    }

    private string BuildResultStatusMessage(int filteredCount)
    {
        var message = $"找到 {filteredCount} 部影片";
        if (SelectedSourceFilter != SourceFilterAll)
        {
            message += $"；来源筛选「{SelectedSourceFilter}」";
        }

        if (IsBatchSelectionMode)
        {
            message += $"；已选择 {SelectedCount} 部";
        }

        return message;
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
            _ = ActivateAsync();
            return;
        }

        IsBatchSelectionMode = true;
        BatchResultSummary = string.Empty;
        ClearSelection();
        _ = ActivateAsync();
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

            await ActivateAsync();
            BatchResultSummary = BuildResultSummary(operationName, successCount, errors);
            NotifyAfterBatchStatusChange();
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
            "取消");

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

            await ActivateAsync();
            BatchResultSummary = BuildRemoveFromLibraryResultSummary(successCount, hiddenCount, errors);
            NotifyAfterBatchRemoveFromLibrary();
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
            "取消");

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
                    if (IsGroupedPlaceholder(item.Movie))
                    {
                        await _movieManagementService.DeleteGroupedPlaceholderRangeRecordAsync(item.Movie.GroupedRangeMediaFileIds);
                    }
                    else if ((item.Movie.IsSeason || item.Movie.IsOther) && item.Movie.SeasonId > 0)
                    {
                        await _tvSeasonCollectionService.DeleteSeasonRecordAsync(item.Movie.SeasonId);
                    }
                    else if ((item.Movie.IsMovie || item.Movie.IsOther) && item.MovieId > 0)
                    {
                        await _movieManagementService.DeleteMovieRecordAsync(item.MovieId);
                    }
                    else if (item.Movie.IsMovie)
                    {
                        await _userCollectionService.DeleteCollectionRecordAsync(BuildRecommendationItem(item.Movie));
                    }
                    else
                    {
                        errors.Add(new BatchItemError(item.SelectionKey, item.Title, "电视剧总览不参与批量操作。"));
                        continue;
                    }

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

            await ActivateAsync();
            BatchResultSummary = BuildResultSummary("删除软件记录", successCount, errors);
            NotifyAfterBatchMovieRecordDelete();
            WriteLibraryBatchEvent(
                $"event=library-delete-movie-records-complete success={successCount} failed={errors.Count}");
        }
        finally
        {
            IsBatchOperationRunning = false;
        }
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

        var request = new ManualUnknownSeasonAggregationApplyRequest
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
            await ActivateAsync();
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
            await ActivateAsync();
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
            return "季号必须是正整数。";
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
            StatusMessage = BuildResultStatusMessage(Movies.Count);
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

        if (IsGroupedPlaceholder(movie))
        {
            StatusMessage = "该项目是未识别剧集候选，后续可通过批量修正或人工聚合为季处理。";
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
