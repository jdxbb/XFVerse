using System.Collections.ObjectModel;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.App.ViewModels.Collections;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class TvSeasonDetailViewModel : PageViewModelBase
{
    private const string SeasonCorrectionTargetRecognized = "recognized";
    private const string SeasonCorrectionTargetUnknown = "unknown";
    private const string SeasonCorrectionTargetRecognizedText = "修正为季";
    private const string SeasonCorrectionTargetUnknownText = "加入到已有未识别季";
    private const string TmdbRatingLoadingText = "TMDB 季评分加载中...";
    private const string ImdbRatingLoadingText = "IMDb 剧集评分加载中...";
    private const string RatingUnavailableText = "暂无评分";
    private const string SeasonRatingUnavailableText = "暂无季评分";
    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
    private readonly ITvMetadataHydrationService _metadataHydrationService;
    private readonly ITmdbService _tmdbService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IUnknownSeasonCorrectionService _unknownSeasonCorrectionService;
    private readonly ISingleSourceCorrectionService _singleSourceCorrectionService;
    private readonly IAiClassificationService _aiClassificationService;
    private int? _seasonId;
    private int? _seriesId;
    private string _seriesName = "-";
    private string _seriesOriginalName = "-";
    private string _seasonTitleText = "未选择电视剧季";
    private string _name = "未选择电视剧季";
    private string _overview = "请先选择一个电视剧季。";
    private string _posterDisplayUrl = string.Empty;
    private string _seasonNumberText = "-";
    private string _airDateText = "-";
    private string _genreDisplay = "未提供";
    private string _ratingDisplay = RatingUnavailableText;
    private string _sourceSummary = "暂无播放源";
    private string _progressText = "已看 0 / 0";
    private string _inLibraryText = "暂无播放源";
    private string _episodeCountText = "-";
    private string _totalEpisodeCountText = "-";
    private string _watchedCountText = "-";
    private int _totalEpisodeCount;
    private string _countryText = "-";
    private string _languageText = "-";
    private string _directorText = "-";
    private string _writerText = "-";
    private string _actorsText = "-";
    private string _networksText = "未提供";
    private string _productionCompaniesText = "未提供";
    private string _identificationStatusText = "未加载";
    private string _unidentifiedSummary = string.Empty;
    private string _statusMessage = "请先选择一个电视剧季。";
    private bool _hasSeason;
    private bool _isUnidentified;
    private bool _isFavorite;
    private bool _isWantToWatch;
    private bool _isNotInterested;
    private bool _isSeasonWatched;
    private bool _isSeasonUnwatched;
    private bool _isVisibleInLibrary;
    private bool _isEpisodeMetadataLoading;
    private bool _isOpeningEpisodePlayer;
    private bool _isDetailLoading;
    private LibraryVisibilityState _libraryVisibilityState = LibraryVisibilityState.Auto;
    private string _tmdbRatingDisplay = SeasonRatingUnavailableText;
    private string _imdbRatingDisplay = string.Empty;
    private MovieRatingItem _seasonTmdbRating = new() { SourceName = "TMDB" };
    private double _episodeListScrollOffset;
    private bool? _restoreEpisodeListScrollOffsetOnNextActivation;
    private bool _isSeasonCorrectionPanelOpen;
    private bool _isSeasonCorrectionBusy;
    private string _seasonCorrectionSearchQuery = string.Empty;
    private string _seasonCorrectionSeasonNumber = "1";
    private string _seasonCorrectionStatusMessage = string.Empty;
    private string _recognizedSeasonCorrectionStatusMessage = string.Empty;
    private string _unknownSeasonCorrectionStatusMessage = string.Empty;
    private string _seasonCorrectionConfirmationText = string.Empty;
    private string _selectedSeasonCorrectionTargetKind = SeasonCorrectionTargetRecognized;
    private bool _isRecognizedSeasonPickerDialogOpen;
    private bool _isUnknownSeasonPickerDialogOpen;
    private bool _isSeasonEpisodeMappingDialogOpen;
    private bool _isRestoringSeasonCorrectionStatus;
    private CancellationTokenSource? _seasonCorrectionAiCancellation;
    private Dictionary<int, SeasonEpisodeMappingSnapshot>? _seasonEpisodeMappingSnapshot;
    private TmdbTvSeriesCorrectionSeriesGroup? _selectedRecognizedSeriesTarget;
    private TmdbTvSeasonCorrectionSeasonItem? _selectedRecognizedSeasonTarget;
    private UnknownTvSeasonCorrectionTargetItem? _selectedUnknownSeasonTarget;

    public TvSeasonDetailViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService,
        ITvMetadataHydrationService metadataHydrationService,
        ITmdbService tmdbService,
        IPlayerWindowService playerWindowService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IDataRefreshService dataRefreshService,
        IUnknownSeasonCorrectionService unknownSeasonCorrectionService,
        ISingleSourceCorrectionService singleSourceCorrectionService,
        IAiClassificationService aiClassificationService)
        : base("电视剧季", "查看电视剧季详情、聚合进度和集列表。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        _metadataHydrationService = metadataHydrationService;
        _tmdbService = tmdbService;
        _playerWindowService = playerWindowService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _dataRefreshService = dataRefreshService;
        _unknownSeasonCorrectionService = unknownSeasonCorrectionService;
        _singleSourceCorrectionService = singleSourceCorrectionService;
        _aiClassificationService = aiClassificationService;
        NavigateBackToSeriesCommand = new RelayCommand(NavigateBackToSeries, () => _seriesId.HasValue);
        NavigateBackCommand = new RelayCommand(NavigateBackFromDetail);
        OpenEpisodeDetailCommand = new RelayCommand(OpenEpisodeDetail);
        PlayEpisodeCommand = new AsyncRelayCommand(PlayEpisodeAsync, CanPlayEpisode);
        ToggleFavoriteCommand = new AsyncRelayCommand(() => ToggleFavoriteAsync(), () => HasSeason && (IsFavorite || IsSeasonWatched));
        ToggleWantToWatchCommand = new AsyncRelayCommand(() => ToggleWantToWatchAsync(), () => HasSeason && (IsWantToWatch || IsSeasonUnwatched));
        TogglePreferenceCommand = new AsyncRelayCommand(TogglePreferenceAsync, () => HasSeason);
        ToggleNotInterestedCommand = new AsyncRelayCommand(() => ToggleNotInterestedAsync(), () => HasSeason);
        MarkSeasonWatchedCommand = new AsyncRelayCommand(() => SetSeasonWatchedAsync(true), () => HasSeason);
        MarkSeasonUnwatchedCommand = new AsyncRelayCommand(() => SetSeasonWatchedAsync(false), () => HasSeason);
        AddSeasonToLibraryCommand = new AsyncRelayCommand(AddSeasonToLibraryAsync, () => CanAddSeasonToLibrary);
        MarkEpisodeWatchedCommand = new AsyncRelayCommand(parameter => SetEpisodeWatchedAsync(parameter, true), disableWhileExecuting: false);
        MarkEpisodeUnwatchedCommand = new AsyncRelayCommand(parameter => SetEpisodeWatchedAsync(parameter, false), disableWhileExecuting: false);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
        OpenSeasonCorrectionCommand = new RelayCommand(OpenSeasonCorrection, () => CanCorrectSeasonToRecognized);
        CancelSeasonCorrectionCommand = new RelayCommand(CancelSeasonCorrection, () => IsSeasonCorrectionPanelOpen && !IsSeasonCorrectionBusy);
        CloseSeasonCorrectionCommand = new RelayCommand(CloseSeasonCorrection, () => IsSeasonCorrectionPanelOpen && !IsSeasonEpisodeMappingDialogOpen);
        OpenSeasonEpisodeMappingCommand = new RelayCommand(OpenSeasonEpisodeMapping, () => IsSeasonCorrectionPanelOpen && HasSeasonCorrectionMappings && !IsSeasonCorrectionBusy);
        CloseSeasonEpisodeMappingCommand = new RelayCommand(CloseSeasonEpisodeMapping, () => IsSeasonEpisodeMappingDialogOpen);
        ConfirmSeasonEpisodeMappingCommand = new RelayCommand(ConfirmSeasonEpisodeMapping, HasValidSeasonCorrectionMappings);
        ClearSeasonCorrectionSearchQueryCommand = new RelayCommand(() => SeasonCorrectionSearchQuery = string.Empty);
        ClearSeasonCorrectionSeasonNumberCommand = new RelayCommand(() => SeasonCorrectionSeasonNumber = string.Empty);
        SearchSeasonCorrectionCandidatesCommand = new AsyncRelayCommand(SearchSeasonCorrectionCandidatesAsync, () => IsSeasonCorrectionPanelOpen && IsSeasonCorrectionTargetRecognized && !IsSeasonCorrectionBusy);
        AiSuggestSeasonCorrectionCommand = new AsyncRelayCommand(AiSuggestSeasonCorrectionAsync, () => IsSeasonCorrectionPanelOpen && IsSeasonCorrectionTargetRecognized && !IsSeasonCorrectionBusy);
        CloseRecognizedSeasonPickerCommand = new RelayCommand(CloseRecognizedSeasonPicker, () => IsRecognizedSeasonPickerDialogOpen && !IsSeasonCorrectionBusy);
        SelectRecognizedSeasonTargetCommand = new RelayCommand(SelectRecognizedSeasonTarget, CanSelectRecognizedSeasonTarget);
        SelectSeasonCorrectionRecognizedTargetKindCommand = new RelayCommand(
            () => SetSeasonCorrectionTargetKind(SeasonCorrectionTargetRecognized),
            () => IsSeasonCorrectionPanelOpen && !IsSeasonCorrectionBusy);
        SelectSeasonCorrectionUnknownTargetKindCommand = new RelayCommand(
            () => SetSeasonCorrectionTargetKind(SeasonCorrectionTargetUnknown),
            () => IsSeasonCorrectionPanelOpen && !IsSeasonCorrectionBusy);
        OpenUnknownSeasonCorrectionPickerCommand = new AsyncRelayCommand(
            OpenUnknownSeasonCorrectionPickerAsync,
            () => IsSeasonCorrectionPanelOpen && IsSeasonCorrectionTargetUnknown && !IsSeasonCorrectionBusy);
        CloseUnknownSeasonPickerCommand = new RelayCommand(CloseUnknownSeasonPicker, () => IsUnknownSeasonPickerDialogOpen && !IsSeasonCorrectionBusy);
        SelectUnknownSeasonTargetCommand = new RelayCommand(SelectUnknownSeasonTarget, CanSelectUnknownSeasonTarget);
        ApplySeasonCorrectionCommand = new AsyncRelayCommand(ApplySeasonCorrectionV2Async, () => CanApplySeasonCorrection);
        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;
    }

    public event EventHandler<TvSeasonTargetEpisodeLocatedEventArgs>? TargetEpisodeLocated;

    public ObservableCollection<TvSeasonEpisodeListItem> Episodes { get; } = [];

    public ObservableCollection<SeasonCorrectionSourceMappingRowViewModel> SeasonCorrectionMappings { get; } = [];

    public ObservableCollection<TmdbTvSeriesCorrectionSeriesGroup> RecognizedSeasonSeriesGroups { get; } = [];

    public BulkObservableCollection<UnknownTvSeasonCorrectionSeriesGroup> UnknownSeasonSeriesGroups { get; } = [];

    public RelayCommand NavigateBackToSeriesCommand { get; }

    public RelayCommand NavigateBackCommand { get; }

    public RelayCommand OpenEpisodeDetailCommand { get; }

    public AsyncRelayCommand PlayEpisodeCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand ToggleWantToWatchCommand { get; }

    public AsyncRelayCommand TogglePreferenceCommand { get; }

    public AsyncRelayCommand ToggleNotInterestedCommand { get; }

    public AsyncRelayCommand MarkSeasonWatchedCommand { get; }

    public AsyncRelayCommand MarkSeasonUnwatchedCommand { get; }

    public AsyncRelayCommand AddSeasonToLibraryCommand { get; }

    public AsyncRelayCommand MarkEpisodeWatchedCommand { get; }

    public AsyncRelayCommand MarkEpisodeUnwatchedCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public double EpisodeListScrollOffset
    {
        get => _episodeListScrollOffset;
        set
        {
            _episodeListScrollOffset = value;
            if (_seasonId.HasValue)
            {
                _navigationStateService.SetSeasonEpisodeListScrollOffset(_seasonId.Value, value);
            }
        }
    }

    public RelayCommand OpenSeasonCorrectionCommand { get; }

    public RelayCommand CancelSeasonCorrectionCommand { get; }

    public RelayCommand CloseSeasonCorrectionCommand { get; }

    public RelayCommand OpenSeasonEpisodeMappingCommand { get; }

    public RelayCommand CloseSeasonEpisodeMappingCommand { get; }

    public RelayCommand ConfirmSeasonEpisodeMappingCommand { get; }

    public RelayCommand ClearSeasonCorrectionSearchQueryCommand { get; }

    public RelayCommand ClearSeasonCorrectionSeasonNumberCommand { get; }

    public AsyncRelayCommand SearchSeasonCorrectionCandidatesCommand { get; }

    public AsyncRelayCommand AiSuggestSeasonCorrectionCommand { get; }

    public RelayCommand CloseRecognizedSeasonPickerCommand { get; }

    public RelayCommand SelectRecognizedSeasonTargetCommand { get; }

    public RelayCommand SelectSeasonCorrectionRecognizedTargetKindCommand { get; }

    public RelayCommand SelectSeasonCorrectionUnknownTargetKindCommand { get; }

    public AsyncRelayCommand OpenUnknownSeasonCorrectionPickerCommand { get; }

    public RelayCommand CloseUnknownSeasonPickerCommand { get; }

    public RelayCommand SelectUnknownSeasonTargetCommand { get; }

    public AsyncRelayCommand ApplySeasonCorrectionCommand { get; }

    public IReadOnlyList<string> SeasonCorrectionTargetOptions { get; } =
    [
        SeasonCorrectionTargetRecognizedText,
        SeasonCorrectionTargetUnknownText
    ];

    public string SeriesName
    {
        get => _seriesName;
        private set
        {
            if (SetProperty(ref _seriesName, value))
            {
                OnPropertyChanged(nameof(SeriesAndOriginalNameText));
            }
        }
    }

    public string SeriesOriginalName
    {
        get => _seriesOriginalName;
        private set
        {
            if (SetProperty(ref _seriesOriginalName, value))
            {
                OnPropertyChanged(nameof(SeriesAndOriginalNameText));
            }
        }
    }

    public string SeriesAndOriginalNameText => string.IsNullOrWhiteSpace(SeriesOriginalName) || SeriesOriginalName == "-"
        ? SeriesName
        : $"{SeriesName} | {SeriesOriginalName}";

    public string SeasonTitleText { get => _seasonTitleText; private set => SetProperty(ref _seasonTitleText, value); }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string PosterDisplayUrl { get => _posterDisplayUrl; private set => SetProperty(ref _posterDisplayUrl, value); }

    public string SeasonNumberText { get => _seasonNumberText; private set => SetProperty(ref _seasonNumberText, value); }

    public string AirDateText { get => _airDateText; private set => SetProperty(ref _airDateText, value); }

    public string GenreDisplay { get => _genreDisplay; private set => SetProperty(ref _genreDisplay, value); }

    public string RatingDisplay { get => _ratingDisplay; private set => SetProperty(ref _ratingDisplay, value); }

    public string SourceSummary { get => _sourceSummary; private set => SetProperty(ref _sourceSummary, value); }

    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    public string InLibraryText { get => _inLibraryText; private set => SetProperty(ref _inLibraryText, value); }

    public string EpisodeCountText { get => _episodeCountText; private set => SetProperty(ref _episodeCountText, value); }

    public string TotalEpisodeCountText { get => _totalEpisodeCountText; private set => SetProperty(ref _totalEpisodeCountText, value); }

    public string WatchedCountText { get => _watchedCountText; private set => SetProperty(ref _watchedCountText, value); }

    public string CountryText { get => _countryText; private set => SetProperty(ref _countryText, value); }

    public string LanguageText { get => _languageText; private set => SetProperty(ref _languageText, value); }

    public string NetworksText { get => _networksText; private set => SetProperty(ref _networksText, value); }

    public string ProductionCompaniesText { get => _productionCompaniesText; private set => SetProperty(ref _productionCompaniesText, value); }

    public string DirectorText { get => _directorText; private set => SetProperty(ref _directorText, value); }

    public string WriterText { get => _writerText; private set => SetProperty(ref _writerText, value); }

    public string ActorsText { get => _actorsText; private set => SetProperty(ref _actorsText, value); }

    public MovieRatingItem SeasonTmdbRating { get => _seasonTmdbRating; private set => SetProperty(ref _seasonTmdbRating, value); }

    public string IdentificationStatusText { get => _identificationStatusText; private set => SetProperty(ref _identificationStatusText, value); }

    public string UnidentifiedSummary { get => _unidentifiedSummary; private set => SetProperty(ref _unidentifiedSummary, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool HasSeason
    {
        get => _hasSeason;
        private set
        {
            if (SetProperty(ref _hasSeason, value))
            {
                OnPropertyChanged(nameof(HasNoSeason));
                OnPropertyChanged(nameof(HasEpisodes));
                OnPropertyChanged(nameof(HasNoEpisodes));
                OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
                RefreshSeasonLibraryButtonState();
                RaiseSeasonStateCommandCanExecuteChanged();
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool HasNoSeason => !HasSeason;

    public bool HasEpisodes => HasSeason && Episodes.Count > 0;

    public bool HasNoEpisodes => HasSeason && Episodes.Count == 0;

    public bool IsDetailLoading
    {
        get => _isDetailLoading;
        private set => SetProperty(ref _isDetailLoading, value);
    }

    public bool IsEpisodeMetadataLoading
    {
        get => _isEpisodeMetadataLoading;
        private set
        {
            if (SetProperty(ref _isEpisodeMetadataLoading, value))
            {
                OnPropertyChanged(nameof(EpisodeEmptyText));
            }
        }
    }

    public string EpisodeEmptyText => IsEpisodeMetadataLoading ? "正在加载本季集信息..." : "暂无已解析集。";

    public bool IsOpeningEpisodePlayer
    {
        get => _isOpeningEpisodePlayer;
        private set
        {
            if (SetProperty(ref _isOpeningEpisodePlayer, value))
            {
                RefreshEpisodePlayCommandState();
            }
        }
    }

    public bool CanOpenEpisodePlayer => !IsOpeningEpisodePlayer && !_playerWindowService.IsPlayerOpen;

    public string EpisodePlayButtonBusyText => IsOpeningEpisodePlayer || _playerWindowService.IsPlayerOpen
        ? "播放器打开中"
        : string.Empty;

    public bool IsUnidentified
    {
        get => _isUnidentified;
        private set
        {
            if (SetProperty(ref _isUnidentified, value))
            {
                OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
                OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
                OnPropertyChanged(nameof(IsSeasonCorrectionInteractionEnabled));
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool CanCorrectSeasonToRecognized => HasSeason
                                                && HasSeasonCorrectionMappings
                                                && !IsSeasonCorrectionBusy;

    public bool CanShowRecognizedSeasonCorrectionEntry => CanCorrectSeasonToRecognized && !IsUnidentified;

    public bool IsSeasonCorrectionPanelOpen
    {
        get => _isSeasonCorrectionPanelOpen;
        private set
        {
            if (SetProperty(ref _isSeasonCorrectionPanelOpen, value))
            {
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool IsSeasonCorrectionBusy
    {
        get => _isSeasonCorrectionBusy;
        private set
        {
            if (SetProperty(ref _isSeasonCorrectionBusy, value))
            {
                OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
                OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
                OnPropertyChanged(nameof(IsSeasonCorrectionInteractionEnabled));
                OnPropertyChanged(nameof(IsSeasonCorrectionBaseInteractionEnabled));
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public string SeasonCorrectionSearchQuery
    {
        get => _seasonCorrectionSearchQuery;
        set => SetProperty(ref _seasonCorrectionSearchQuery, value);
    }

    public string SeasonCorrectionSeasonNumber
    {
        get => _seasonCorrectionSeasonNumber;
        set
        {
            if (SetProperty(ref _seasonCorrectionSeasonNumber, value))
            {
                UpdateSeasonCorrectionConfirmation();
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public string SeasonCorrectionStatusMessage
    {
        get => _seasonCorrectionStatusMessage;
        private set
        {
            if (SetProperty(ref _seasonCorrectionStatusMessage, value))
            {
                StoreSeasonCorrectionStatusForSelectedTarget(value);
            }
        }
    }

    public string SeasonCorrectionConfirmationText
    {
        get => _seasonCorrectionConfirmationText;
        private set => SetProperty(ref _seasonCorrectionConfirmationText, value);
    }

    public bool IsRecognizedSeasonPickerDialogOpen
    {
        get => _isRecognizedSeasonPickerDialogOpen;
        private set
        {
            if (SetProperty(ref _isRecognizedSeasonPickerDialogOpen, value))
            {
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool HasRecognizedSeasonTargets => RecognizedSeasonSeriesGroups.Count > 0;

    public bool IsUnknownSeasonPickerDialogOpen
    {
        get => _isUnknownSeasonPickerDialogOpen;
        private set
        {
            if (SetProperty(ref _isUnknownSeasonPickerDialogOpen, value))
            {
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool HasUnknownSeasonTargets => UnknownSeasonSeriesGroups.Count > 0;

    public bool IsSeasonCorrectionTargetRecognized => _selectedSeasonCorrectionTargetKind == SeasonCorrectionTargetRecognized;

    public bool IsSeasonCorrectionTargetUnknown => _selectedSeasonCorrectionTargetKind == SeasonCorrectionTargetUnknown;

    public string SelectedSeasonCorrectionTarget
    {
        get => IsSeasonCorrectionTargetUnknown ? SeasonCorrectionTargetUnknownText : SeasonCorrectionTargetRecognizedText;
        set => SetSeasonCorrectionTargetKind(
            string.Equals(value, SeasonCorrectionTargetUnknownText, StringComparison.Ordinal)
                ? SeasonCorrectionTargetUnknown
                : SeasonCorrectionTargetRecognized);
    }

    public bool IsSeasonCorrectionInteractionEnabled => !IsSeasonCorrectionBusy;

    public bool IsSeasonCorrectionBaseInteractionEnabled =>
        IsSeasonCorrectionInteractionEnabled && !IsSeasonEpisodeMappingDialogOpen;

    public bool IsSeasonEpisodeMappingDialogOpen
    {
        get => _isSeasonEpisodeMappingDialogOpen;
        private set
        {
            if (SetProperty(ref _isSeasonEpisodeMappingDialogOpen, value))
            {
                OnPropertyChanged(nameof(IsSeasonCorrectionBaseInteractionEnabled));
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public string SeasonCorrectionRecognizedTargetButtonText => IsSeasonCorrectionTargetRecognized ? "已选择：修正到已识别季" : "修正到已识别季";

    public string SeasonCorrectionUnknownTargetButtonText => IsSeasonCorrectionTargetUnknown ? "已选择：加入已有未识别季" : "加入已有未识别季";

    public bool HasSelectedSeasonCorrectionTarget =>
        IsSeasonCorrectionTargetRecognized
            ? _selectedRecognizedSeriesTarget is not null && TryGetSeasonCorrectionSeasonNumber(out _)
            : _selectedUnknownSeasonTarget is not null;

    public bool HasSeasonCorrectionMappings => SeasonCorrectionMappings.Count > 0;

    public string SelectedRecognizedSeasonTargetDisplay => _selectedRecognizedSeriesTarget is null
        ? "尚未选择目标已识别剧。"
        : _selectedRecognizedSeasonTarget is null
            ? _selectedRecognizedSeriesTarget.DisplayTitle
            : $"{_selectedRecognizedSeriesTarget.DisplayTitle} / {_selectedRecognizedSeasonTarget.DisplayTitle}";

    public string SelectedSeasonCorrectionTargetDisplay
    {
        get
        {
            if (IsSeasonCorrectionTargetRecognized)
            {
                return _selectedRecognizedSeriesTarget is null
                    ? "尚未选择目标已识别剧或季。"
                    : _selectedRecognizedSeasonTarget is null
                        ? _selectedRecognizedSeriesTarget.DisplayTitle
                        : $"{_selectedRecognizedSeriesTarget.DisplayTitle} / {_selectedRecognizedSeasonTarget.DisplayTitle}";
            }

            return _selectedUnknownSeasonTarget is null
                ? "尚未选择目标未识别季。"
                : _selectedUnknownSeasonTarget.DisplayTitle;
        }
    }

    public bool CanApplySeasonCorrection => IsSeasonCorrectionPanelOpen
                                            && !IsSeasonCorrectionBusy
                                            && _seasonId.HasValue
                                            && HasSelectedSeasonCorrectionTarget
                                            && HasValidSeasonCorrectionMappings();

    public bool HasInvalidSeasonCorrectionMappings => SeasonCorrectionMappings.Count > 0
                                                      && SeasonCorrectionMappings.Any(x => !x.ParsedTargetEpisodeNumber.HasValue);

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteButtonText));
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public bool IsWantToWatch
    {
        get => _isWantToWatch;
        private set
        {
            if (SetProperty(ref _isWantToWatch, value))
            {
                OnPropertyChanged(nameof(WantToWatchButtonText));
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                RaiseSeasonStateCommandCanExecuteChanged();
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                OnPropertyChanged(nameof(WatchedButtonIcon));
            }
        }
    }

    public bool IsNotInterested
    {
        get => _isNotInterested;
        private set
        {
            if (SetProperty(ref _isNotInterested, value))
            {
                OnPropertyChanged(nameof(NotInterestedButtonText));
            }
        }
    }

    public bool IsSeasonWatched
    {
        get => _isSeasonWatched;
        private set
        {
            if (SetProperty(ref _isSeasonWatched, value))
            {
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                OnPropertyChanged(nameof(WatchedButtonIcon));
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public bool IsSeasonUnwatched
    {
        get => _isSeasonUnwatched;
        private set
        {
            if (SetProperty(ref _isSeasonUnwatched, value))
            {
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public string FavoriteButtonText => IsFavorite ? "取消喜爱" : "喜爱";

    public string WantToWatchButtonText => IsWantToWatch ? "取消想看" : "想看";

    public string PreferenceButtonText => IsSeasonWatched ? FavoriteButtonText : WantToWatchButtonText;

    public string WatchedButtonIcon => IsSeasonWatched ? "\uE711" : "\uE8FB";

    public string PreferenceButtonIcon => IsSeasonWatched
        ? IsFavorite ? "\uEB52" : "\uEB51"
        : IsWantToWatch ? "\uE735" : "\uE734";

    public string NotInterestedButtonIcon => "\uE814";

    public string NotInterestedButtonText => IsNotInterested ? "取消不想看" : "不想看";

    public bool IsVisibleInLibrary
    {
        get => _isVisibleInLibrary;
        private set
        {
            if (SetProperty(ref _isVisibleInLibrary, value))
            {
                RefreshSeasonLibraryButtonState();
            }
        }
    }

    public bool CanAddSeasonToLibrary => HasSeason;

    public string AddSeasonToLibraryButtonText => IsVisibleInLibrary ? "移出媒体库" : "加入媒体库";

    public string AddSeasonToLibraryButtonIcon => IsVisibleInLibrary ? "\uE738" : "\uE710";

    private LibraryVisibilityState CurrentLibraryVisibilityState
    {
        get => _libraryVisibilityState;
        set
        {
            if (SetProperty(ref _libraryVisibilityState, value))
            {
                RefreshSeasonLibraryButtonState();
            }
        }
    }

    public void PrepareForActivation()
    {
        var selectedSeasonId = _navigationStateService.SelectedTvSeasonId;
        if (!selectedSeasonId.HasValue)
        {
            _restoreEpisodeListScrollOffsetOnNextActivation = false;
            return;
        }

        var shouldRestoreEpisodeListOffset =
            _navigationStateService.ConsumeSeasonEpisodeListScrollRestoreRequest(selectedSeasonId.Value);
        _restoreEpisodeListScrollOffsetOnNextActivation = shouldRestoreEpisodeListOffset;
        if (!shouldRestoreEpisodeListOffset)
        {
            ResetEpisodeListScrollOffset(selectedSeasonId.Value);
        }

        if (!_seasonId.HasValue || _seasonId.Value != selectedSeasonId.Value)
        {
            BeginDetailLoading("正在加载电视剧季详情...");
        }
    }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        var selectedSeasonId = _navigationStateService.SelectedTvSeasonId;
        if (!selectedSeasonId.HasValue)
        {
            Clear("请先选择一个电视剧季。");
            return;
        }

        try
        {
            if (_seasonId.HasValue && _seasonId.Value != selectedSeasonId.Value)
            {
                BeginDetailLoading("正在加载电视剧季详情...");
                PosterDisplayUrl = string.Empty;
                await Task.Yield();
            }

            var shouldRestoreEpisodeListOffset =
                _restoreEpisodeListScrollOffsetOnNextActivation ?? true;
            _restoreEpisodeListScrollOffsetOnNextActivation = null;
            if (!shouldRestoreEpisodeListOffset)
            {
                ResetEpisodeListScrollOffset(selectedSeasonId.Value);
            }

            var model = await _tvDetailQueryService.GetSeasonDetailAsync(selectedSeasonId.Value, cancellationToken);
            if (model is null)
            {
                Clear("未找到对应电视剧季，可能已被移出。");
                return;
            }

            _seasonId = model.SeasonId;
            _episodeListScrollOffset = _navigationStateService.GetSeasonEpisodeListScrollOffset(model.SeasonId);
            OnPropertyChanged(nameof(EpisodeListScrollOffset));
            _seriesId = model.SeriesId;
            HasSeason = true;
            SeriesName = model.SeriesName;
            SeriesOriginalName = string.IsNullOrWhiteSpace(model.SeriesOriginalName) ? "-" : model.SeriesOriginalName;
            Name = model.IsUnidentified ? "未识别电视剧季" : model.Name;
            SeasonTitleText = model.IsUnidentified ? "未识别电视剧季" : model.SeasonTitleText;
            Overview = string.IsNullOrWhiteSpace(model.Overview) ? "暂无简介" : model.Overview;
            PosterDisplayUrl = model.PosterDisplayUrl;
            SeasonNumberText = model.SeasonNumberText;
            AirDateText = model.AirDateText;
            GenreDisplay = string.IsNullOrWhiteSpace(model.GenreDisplay) ? "未提供" : model.GenreDisplay;
            SetRatingDisplayParts(TmdbRatingLoadingText, ImdbRatingLoadingText);
            SourceSummary = model.SourceSummary;
            ProgressText = model.ProgressText;
            InLibraryText = model.InLibraryText;
            EpisodeCountText = model.EpisodeCountText;
            TotalEpisodeCountText = $"{model.TotalEpisodeCount} 集";
            _totalEpisodeCount = model.TotalEpisodeCount;
            WatchedCountText = model.WatchedCountText;
            CountryText = MovieMetadataDisplayText.LocalizeCountries(model.SeriesCountry);
            LanguageText = MovieMetadataDisplayText.LocalizeLanguages(model.SeriesLanguage);
            DirectorText = string.IsNullOrWhiteSpace(model.SeriesDirectorText) ? "-" : model.SeriesDirectorText;
            WriterText = string.IsNullOrWhiteSpace(model.SeriesWriterText) ? "-" : model.SeriesWriterText;
            ActorsText = string.IsNullOrWhiteSpace(model.SeriesActorsText) ? "-" : model.SeriesActorsText;
            NetworksText = string.IsNullOrWhiteSpace(model.SeriesNetworksText) ? "未提供" : model.SeriesNetworksText;
            ProductionCompaniesText = string.IsNullOrWhiteSpace(model.SeriesProductionCompaniesText) ? "未提供" : model.SeriesProductionCompaniesText;
            IdentificationStatusText = model.IdentificationStatusText;
            IsUnidentified = model.IsUnidentified;
            UnidentifiedSummary = model.UnidentifiedSummary;
            IsFavorite = model.IsFavorite;
            IsWantToWatch = model.IsWantToWatch;
            IsNotInterested = model.IsNotInterested;
            IsSeasonWatched = model.IsSeasonWatched;
            IsSeasonUnwatched = model.IsSeasonUnwatched;
            IsVisibleInLibrary = model.IsVisibleInLibrary;
            CurrentLibraryVisibilityState = model.LibraryVisibilityState;
            var selectedEpisodeId = _navigationStateService.SelectedTvEpisodeId;
            Episodes.Clear();
            foreach (var episode in model.Episodes)
            {
                episode.IsTargetEpisode = selectedEpisodeId.HasValue && episode.EpisodeId == selectedEpisodeId.Value;
                Episodes.Add(episode);
            }

            RefreshSeasonCorrectionMappings(model.CorrectionSources);
            OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
            OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
            if (!CanCorrectSeasonToRecognized)
            {
                ClearSeasonCorrectionState();
            }
            else if (string.IsNullOrWhiteSpace(SeasonCorrectionSeasonNumber))
            {
                SeasonCorrectionSeasonNumber = model.SeasonNumber >= 0 ? model.SeasonNumber.ToString() : "1";
            }

            var shouldEnsureEpisodeMetadata = ShouldEnsureEpisodeMetadata(model);
            IsEpisodeMetadataLoading = shouldEnsureEpisodeMetadata;
            NavigateBackToSeriesCommand.RaiseCanExecuteChanged();
            RaiseSeasonStateCommandCanExecuteChanged();
            RaiseSeasonCorrectionCommandStates();
            RefreshEpisodePlayCommandState();
            OnPropertyChanged(nameof(HasEpisodes));
            OnPropertyChanged(nameof(HasNoEpisodes));
            OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
            OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
            if (selectedEpisodeId.HasValue)
            {
                var targetEpisode = Episodes.FirstOrDefault(episode => episode.EpisodeId == selectedEpisodeId.Value);
                if (targetEpisode is not null)
                {
                    StatusMessage = $"已定位到 {targetEpisode.EpisodeNumberText}。";
                    TargetEpisodeLocated?.Invoke(this, new TvSeasonTargetEpisodeLocatedEventArgs(selectedEpisodeId.Value));
                    ScanIdentificationDiagnostics.Write(
                        $"event=watch-history-target-highlighted targetKind=episode seasonId={model.SeasonId} episodeId={selectedEpisodeId.Value}");
                }
                else
                {
                    StatusMessage = "历史记录关联的目标集已不存在或不在当前季。";
                    ScanIdentificationDiagnostics.Write(
                        $"event=watch-history-target-missing targetKind=episode seasonId={model.SeasonId} episodeId={selectedEpisodeId.Value}");
                }
            }
            else
            {
                StatusMessage = Episodes.Count == 0
                    ? EpisodeEmptyText
                    : $"已加载 {Episodes.Count} 集。";
            }

            _ = LoadRatingDisplayAsync(model.SeasonId, cancellationToken);
            if (model.TmdbSeriesId.HasValue)
            {
                _ = EnsureSeriesMetadataAndRefreshAsync(model.SeriesId, model.SeasonId, cancellationToken);
            }

            if (shouldEnsureEpisodeMetadata)
            {
                _ = EnsureSeasonEpisodesAndRefreshAsync(model.SeasonId, cancellationToken);
            }

            IsDetailLoading = false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Clear($"加载电视剧季详情失败：{DescribeException(exception)}");
        }
    }

    private static bool ShouldEnsureEpisodeMetadata(TvSeasonDetailModel model)
    {
        return model.TmdbSeriesId is > 0
               && (model.Episodes.Count == 0
                   || (model.TotalEpisodeCount > 0 && model.Episodes.Count < model.TotalEpisodeCount));
    }

    private void RefreshSeasonLibraryButtonState()
    {
        OnPropertyChanged(nameof(CanAddSeasonToLibrary));
        OnPropertyChanged(nameof(AddSeasonToLibraryButtonText));
        OnPropertyChanged(nameof(AddSeasonToLibraryButtonIcon));
        AddSeasonToLibraryCommand.RaiseCanExecuteChanged();
    }

    private void ResetEpisodeListScrollOffset(int seasonId)
    {
        _navigationStateService.SetSeasonEpisodeListScrollOffset(seasonId, 0);
        if (_episodeListScrollOffset <= 0)
        {
            return;
        }

        _episodeListScrollOffset = 0;
        OnPropertyChanged(nameof(EpisodeListScrollOffset));
    }

    private async Task EnsureSeasonEpisodesAndRefreshAsync(int seasonId, CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = "正在补齐本季集信息。";
            var result = await Task.Run(
                () => _metadataHydrationService.EnsureSeasonEpisodesAsync(
                    seasonId,
                    cancellationToken: cancellationToken),
                cancellationToken);

            if (_navigationStateService.SelectedTvSeasonId != seasonId)
            {
                return;
            }

            var model = await _tvDetailQueryService.GetSeasonDetailAsync(seasonId, cancellationToken);
            if (model is null)
            {
                IsEpisodeMetadataLoading = false;
                return;
            }

            Episodes.Clear();
            foreach (var episode in model.Episodes)
            {
                Episodes.Add(episode);
            }

            RefreshSeasonCorrectionMappings(model.CorrectionSources);
            ProgressText = model.ProgressText;
            InLibraryText = model.InLibraryText;
            EpisodeCountText = model.EpisodeCountText;
            TotalEpisodeCountText = $"{model.TotalEpisodeCount} 集";
            WatchedCountText = model.WatchedCountText;
            IsSeasonWatched = model.IsSeasonWatched;
            IsSeasonUnwatched = model.IsSeasonUnwatched;
            IsEpisodeMetadataLoading = false;
            OnPropertyChanged(nameof(HasEpisodes));
            OnPropertyChanged(nameof(HasNoEpisodes));
            OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
            OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
            RaiseSeasonStateCommandCanExecuteChanged();
            RaiseSeasonCorrectionCommandStates();
            RefreshEpisodePlayCommandState();
            StatusMessage = result.HasErrors
                ? result.BuildStatusMessage()
                : result.Skipped
                    ? $"已加载 {Episodes.Count} 集。"
                    : $"已补齐 {Episodes.Count} 集。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            IsEpisodeMetadataLoading = false;
            if (_navigationStateService.SelectedTvSeasonId == seasonId)
            {
                StatusMessage = $"本季集信息补齐失败：{DescribeException(exception)}";
            }
        }
    }

    private async Task EnsureSeriesMetadataAndRefreshAsync(
        int seriesId,
        int seasonId,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(
                () => _metadataHydrationService.EnsureHydratedBySeriesIdAsync(
                    seriesId,
                    cancellationToken: cancellationToken),
                cancellationToken);

            if (_navigationStateService.SelectedTvSeasonId != seasonId)
            {
                return;
            }

            var model = await _tvDetailQueryService.GetSeasonDetailAsync(seasonId, cancellationToken);
            if (model is null)
            {
                return;
            }

            CountryText = MovieMetadataDisplayText.LocalizeCountries(model.SeriesCountry);
            LanguageText = MovieMetadataDisplayText.LocalizeLanguages(model.SeriesLanguage);
            DirectorText = string.IsNullOrWhiteSpace(model.SeriesDirectorText) ? "-" : model.SeriesDirectorText;
            WriterText = string.IsNullOrWhiteSpace(model.SeriesWriterText) ? "-" : model.SeriesWriterText;
            ActorsText = string.IsNullOrWhiteSpace(model.SeriesActorsText) ? "-" : model.SeriesActorsText;
            NetworksText = string.IsNullOrWhiteSpace(model.SeriesNetworksText) ? "未提供" : model.SeriesNetworksText;
            ProductionCompaniesText = string.IsNullOrWhiteSpace(model.SeriesProductionCompaniesText)
                ? "未提供"
                : model.SeriesProductionCompaniesText;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }

    private async Task LoadRatingDisplayAsync(int seasonId, CancellationToken cancellationToken)
    {
        try
        {
            var tmdbRatingDisplay = await _tvDetailQueryService.GetSeasonTmdbRatingDisplayAsync(seasonId, cancellationToken);
            SeasonTmdbRating = await _tvDetailQueryService.GetSeasonTmdbRatingAsync(seasonId, cancellationToken);
            if (_seasonId == seasonId)
            {
                SetRatingDisplayParts(
                    string.IsNullOrWhiteSpace(tmdbRatingDisplay) ? SeasonRatingUnavailableText : tmdbRatingDisplay,
                    ImdbRatingLoadingText);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (_seasonId == seasonId)
            {
                SeasonTmdbRating = new MovieRatingItem { SourceName = "TMDB" };
                SetRatingDisplayParts(SeasonRatingUnavailableText, ImdbRatingLoadingText);
            }
        }

        try
        {
            var imdbRatingDisplay = await _tvDetailQueryService.GetSeasonImdbSeriesRatingDisplayAsync(seasonId, cancellationToken);
            if (_seasonId == seasonId)
            {
                SetRatingDisplayParts(_tmdbRatingDisplay, imdbRatingDisplay);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            if (_seasonId == seasonId)
            {
                SetRatingDisplayParts(_tmdbRatingDisplay, string.Empty);
            }
        }
    }

    private void SetRatingDisplayParts(string tmdbRatingDisplay, string imdbRatingDisplay)
    {
        _tmdbRatingDisplay = tmdbRatingDisplay;
        _imdbRatingDisplay = imdbRatingDisplay;
        var parts = new[] { _tmdbRatingDisplay, _imdbRatingDisplay }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        RatingDisplay = parts.Length == 0 ? RatingUnavailableText : string.Join(" · ", parts);
    }

    private void NavigateBackToSeries()
    {
        NavigateBackFromDetail();
    }

    private void NavigateBackFromDetail()
    {
        if (_seriesId.HasValue)
        {
            _navigationStateService.RequestDetailBackToSeries(_seriesId.Value);
            return;
        }

        _navigationStateService.RequestDetailBackToLibrary();
    }

    private void OpenEpisodeDetail(object? parameter)
    {
        if (parameter is not TvSeasonEpisodeListItem episode)
        {
            StatusMessage = "请先选择要查看的集。";
            return;
        }

        _navigationStateService.RequestEpisodeDetail(episode.EpisodeId);
    }

    private void OpenSeasonCorrection()
    {
        if (!CanCorrectSeasonToRecognized)
        {
            SeasonCorrectionStatusMessage = "当前季没有可修正的未识别播放源。";
            return;
        }

        IsSeasonCorrectionPanelOpen = true;
        SetSeasonCorrectionTargetKind(SeasonCorrectionTargetRecognized);
        SeasonCorrectionSearchQuery = SeriesName;
        SeasonCorrectionSeasonNumber = TryParseDisplaySeasonNumber(SeasonNumberText, out var seasonNumber)
            ? seasonNumber.ToString()
            : "1";
        foreach (var mapping in SeasonCorrectionMappings)
        {
            mapping.TargetEpisodeNumberText = mapping.OriginalEpisodeNumber.ToString();
        }

        SeasonCorrectionStatusMessage = "搜索并从弹窗中选择目标已识别季。";
        UpdateSeasonCorrectionConfirmation();
    }

    private void RefreshSeasonCorrectionMappings(IEnumerable<TvSeasonCorrectionSourceItem> sources)
    {
        SeasonCorrectionMappings.Clear();
        foreach (var source in sources.OrderBy(x => x.EpisodeNumber).ThenBy(x => x.MediaFileId))
        {
            SeasonCorrectionMappings.Add(new SeasonCorrectionSourceMappingRowViewModel(source, OnSeasonCorrectionMappingChanged));
        }

        OnSeasonCorrectionMappingChanged();
        OnPropertyChanged(nameof(HasSeasonCorrectionMappings));
    }

    private void OnSeasonCorrectionMappingChanged()
    {
        OnPropertyChanged(nameof(HasInvalidSeasonCorrectionMappings));
        OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
        OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
        UpdateSeasonCorrectionConfirmation();
        RaiseSeasonCorrectionCommandStates();
    }

    private void CancelSeasonCorrection()
    {
        if (CancelSeasonCorrectionAiRequest())
        {
            IsSeasonCorrectionBusy = false;
        }
        ClearSeasonCorrectionState();
    }

    private void CloseSeasonCorrection()
    {
        CancelSeasonCorrection();
    }

    private void OpenSeasonEpisodeMapping()
    {
        if (HasSeasonCorrectionMappings && !IsSeasonCorrectionBusy)
        {
            _seasonEpisodeMappingSnapshot = SeasonCorrectionMappings.ToDictionary(
                mapping => mapping.OriginalEpisodeNumber,
                mapping => new SeasonEpisodeMappingSnapshot(mapping.MediaFileId, mapping.TargetEpisodeNumberText));
            IsSeasonEpisodeMappingDialogOpen = true;
        }
    }

    private void CloseSeasonEpisodeMapping()
    {
        if (_seasonEpisodeMappingSnapshot is not null)
        {
            foreach (var mapping in SeasonCorrectionMappings)
            {
                if (_seasonEpisodeMappingSnapshot.TryGetValue(mapping.OriginalEpisodeNumber, out var snapshot))
                {
                    mapping.RestoreSelectedSource(snapshot.MediaFileId);
                    mapping.TargetEpisodeNumberText = snapshot.TargetEpisodeNumberText;
                }
            }
        }

        _seasonEpisodeMappingSnapshot = null;
        IsSeasonEpisodeMappingDialogOpen = false;
    }

    private void ConfirmSeasonEpisodeMapping()
    {
        if (!HasValidSeasonCorrectionMappings())
        {
            SeasonCorrectionStatusMessage = "目标集号必须是正整数。";
            return;
        }

        _seasonEpisodeMappingSnapshot = null;
        IsSeasonEpisodeMappingDialogOpen = false;
        UpdateSeasonCorrectionConfirmation();
    }

    private bool CancelSeasonCorrectionAiRequest()
    {
        var cancellation = _seasonCorrectionAiCancellation;
        _seasonCorrectionAiCancellation = null;
        cancellation?.Cancel();
        return cancellation is not null;
    }

    private void StoreSeasonCorrectionStatusForSelectedTarget(string statusMessage)
    {
        if (_isRestoringSeasonCorrectionStatus || !IsSeasonCorrectionPanelOpen)
        {
            return;
        }

        if (IsSeasonCorrectionTargetUnknown)
        {
            _unknownSeasonCorrectionStatusMessage = statusMessage;
        }
        else
        {
            _recognizedSeasonCorrectionStatusMessage = statusMessage;
        }
    }

    private void RestoreSeasonCorrectionStatusForSelectedTarget()
    {
        if (!IsSeasonCorrectionPanelOpen)
        {
            return;
        }

        var statusMessage = IsSeasonCorrectionTargetUnknown
            ? _unknownSeasonCorrectionStatusMessage
            : _recognizedSeasonCorrectionStatusMessage;

        _isRestoringSeasonCorrectionStatus = true;
        try
        {
            SeasonCorrectionStatusMessage = statusMessage;
        }
        finally
        {
            _isRestoringSeasonCorrectionStatus = false;
        }
    }

    private void SetSeasonCorrectionTargetKind(string targetKind)
    {
        var normalizedTargetKind = string.Equals(targetKind, SeasonCorrectionTargetUnknown, StringComparison.Ordinal)
            ? SeasonCorrectionTargetUnknown
            : SeasonCorrectionTargetRecognized;
        if (_selectedSeasonCorrectionTargetKind == normalizedTargetKind)
        {
            return;
        }

        _selectedSeasonCorrectionTargetKind = normalizedTargetKind;
        _selectedRecognizedSeriesTarget = null;
        _selectedRecognizedSeasonTarget = null;
        _selectedUnknownSeasonTarget = null;
        IsRecognizedSeasonPickerDialogOpen = false;
        IsUnknownSeasonPickerDialogOpen = false;
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetRecognized));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetUnknown));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SeasonCorrectionRecognizedTargetButtonText));
        OnPropertyChanged(nameof(SeasonCorrectionUnknownTargetButtonText));
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetRecognized));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetUnknown));
        OnPropertyChanged(nameof(SeasonCorrectionRecognizedTargetButtonText));
        OnPropertyChanged(nameof(SeasonCorrectionUnknownTargetButtonText));
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        RestoreSeasonCorrectionStatusForSelectedTarget();
        if (string.IsNullOrWhiteSpace(SeasonCorrectionStatusMessage))
        {
            SeasonCorrectionStatusMessage = IsSeasonCorrectionTargetRecognized
                ? "请搜索 TMDB 电视剧并选择目标季。"
                : "请选择已有未识别季作为目标。";
        }
        UpdateSeasonCorrectionConfirmation();
        RaiseSeasonCorrectionCommandStates();
        if (IsSeasonCorrectionTargetUnknown && UnknownSeasonSeriesGroups.Count == 0)
        {
            _ = OpenUnknownSeasonCorrectionPickerAsync();
        }
    }

    private async Task AiSuggestSeasonCorrectionAsync()
    {
        if (!IsSeasonCorrectionTargetRecognized)
        {
            SeasonCorrectionStatusMessage = "请先切换到修正到已识别季。";
            return;
        }

        CancelSeasonCorrectionAiRequest();
        var cancellation = new CancellationTokenSource();
        _seasonCorrectionAiCancellation = cancellation;
        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = "AI 正在建议目标电视剧和季号...";
            var suggestionResult = await _aiClassificationService.SuggestTvSeasonCorrectionSearchQueryAsync(
                Name,
                BuildSeasonCorrectionAiSamples(),
                seriesTitle: SeriesName,
                seasonNumber: TryGetSeasonCorrectionSeasonNumber(out var currentSeasonNumber) ? currentSeasonNumber : null,
                overview: Overview,
                cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (suggestionResult.Status != AiSearchSuggestionStatus.Success)
            {
                SeasonCorrectionStatusMessage = string.IsNullOrWhiteSpace(suggestionResult.Message)
                    ? "AI 未返回可用电视剧搜索词，请手动输入后搜索。"
                    : $"AI 未返回可用电视剧搜索词：{suggestionResult.Message}";
                ScanIdentificationDiagnostics.Write(
                    $"event=season-correction-ai-assist-skipped sourceSeasonId={_seasonId} status={FormatAiSuggestionStatus(suggestionResult.Status)} message={ScanIdentificationDiagnostics.FormatValue(suggestionResult.Message, 180)}");
                return;
            }

            var suggestion = suggestionResult.Suggestion;

            SeasonCorrectionSearchQuery = suggestion.Query;
            var hasAiSeasonNumber = suggestion.SeasonNumber.HasValue
                                    && suggestion.SeasonNumber.Value >= 0;
            SeasonCorrectionSeasonNumber = hasAiSeasonNumber
                ? suggestion.SeasonNumber!.Value.ToString()
                : string.Empty;
            SeasonCorrectionStatusMessage = hasAiSeasonNumber
                ? $"AI 建议电视剧搜索：{suggestion.Query} S{suggestion.SeasonNumber!.Value:00}"
                : $"AI 建议电视剧搜索：{suggestion.Query}；请手动输入季号或展开剧候选选择季。";
            await SearchSeasonCorrectionCandidatesCoreAsync(cancellation.Token);
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-ai-assist-succeeded sourceSeasonId={_seasonId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={RecognizedSeasonSeriesGroups.Count} hasSeasonNumber={hasAiSeasonNumber.ToString().ToLowerInvariant()} message={ScanIdentificationDiagnostics.FormatValue(suggestionResult.Message, 180)}");
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_seasonCorrectionAiCancellation, cancellation) && IsSeasonCorrectionPanelOpen)
            {
                SeasonCorrectionStatusMessage = "AI 辅助搜索已取消。";
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(_seasonCorrectionAiCancellation, cancellation))
            {
                SeasonCorrectionStatusMessage = $"AI 辅助季修正失败：{DescribeException(exception)}";
            }
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-ai-assist-failed sourceSeasonId={_seasonId} failureReason={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 260)}");
        }
        finally
        {
            var isCurrentRequest = ReferenceEquals(_seasonCorrectionAiCancellation, cancellation);
            if (isCurrentRequest)
            {
                _seasonCorrectionAiCancellation = null;
            }

            cancellation.Dispose();
            if (isCurrentRequest)
            {
                IsSeasonCorrectionBusy = false;
            }
        }
    }

    private IReadOnlyCollection<string> BuildSeasonCorrectionAiSamples()
    {
        var rows = SeasonCorrectionMappings
            .OrderBy(x => x.OriginalEpisodeNumber)
            .ThenBy(x => x.MediaFileId)
            .ToList();
        if (rows.Count <= 9)
        {
            return rows.Select(x => x.FileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }

        var indexes = new SortedSet<int>
        {
            0,
            1,
            rows.Count / 4,
            rows.Count / 2,
            rows.Count * 3 / 4,
            rows.Count - 2,
            rows.Count - 1
        };
        return indexes
            .Where(x => x >= 0 && x < rows.Count)
            .Select(x => rows[x].FileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private async Task SearchSeasonCorrectionCandidatesAsync()
    {
        if (IsSeasonCorrectionBusy)
        {
            return;
        }

        CancelSeasonCorrectionAiRequest();
        var cancellation = new CancellationTokenSource();
        _seasonCorrectionAiCancellation = cancellation;
        try
        {
            await SearchSeasonCorrectionCandidatesCoreAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_seasonCorrectionAiCancellation, cancellation) && IsSeasonCorrectionPanelOpen)
            {
                SeasonCorrectionStatusMessage = "TMDB 搜索已取消。";
            }
        }
        finally
        {
            var isCurrentRequest = ReferenceEquals(_seasonCorrectionAiCancellation, cancellation);
            if (isCurrentRequest)
            {
                _seasonCorrectionAiCancellation = null;
            }

            cancellation.Dispose();
            if (isCurrentRequest)
            {
                IsSeasonCorrectionBusy = false;
            }
        }
    }

    private async Task SearchSeasonCorrectionCandidatesCoreAsync(CancellationToken cancellationToken)
    {
        if (!IsSeasonCorrectionTargetRecognized)
        {
            SeasonCorrectionStatusMessage = "请先切换到修正到已识别季。";
            return;
        }

        var query = string.IsNullOrWhiteSpace(SeasonCorrectionSearchQuery)
            ? SeriesName
            : SeasonCorrectionSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            RecognizedSeasonSeriesGroups.Clear();
            OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
            SeasonCorrectionStatusMessage = "请输入要搜索的电视剧名。";
            return;
        }

        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = "正在搜索 TMDB 电视剧...";
            RecognizedSeasonSeriesGroups.Clear();
            OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
            _selectedRecognizedSeriesTarget = null;
            _selectedRecognizedSeasonTarget = null;
            _selectedUnknownSeasonTarget = null;
            OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
            OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
            UpdateSeasonCorrectionConfirmation();
            await Task.Yield();

            var page = await _tmdbService.SearchTvSeriesAsync(query, 1, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var candidate in page.Results.Take(12))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var details = await _tmdbService.GetTvSeriesDetailsAsync(candidate.TmdbId, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                RecognizedSeasonSeriesGroups.Add(new TmdbTvSeriesCorrectionSeriesGroup(candidate, details));
            }

            OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
            IsRecognizedSeasonPickerDialogOpen = true;
            SeasonCorrectionStatusMessage = RecognizedSeasonSeriesGroups.Count == 0
                ? "没有找到可选择的 TMDB 电视剧。"
                : $"已找到 {RecognizedSeasonSeriesGroups.Count} 个电视剧候选；可展开选择季，或直接选择剧并使用输入的季号。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RecognizedSeasonSeriesGroups.Clear();
            OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
            SeasonCorrectionStatusMessage = $"搜索 TMDB 电视剧失败：{DescribeException(exception)}";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsSeasonCorrectionBusy = false;
            }
        }
    }

    private bool CanSelectRecognizedSeasonTarget(object? parameter)
    {
        return IsSeasonCorrectionPanelOpen
               && IsSeasonCorrectionTargetRecognized
               && !IsSeasonCorrectionBusy
               && (parameter is TmdbTvSeriesCorrectionSeriesGroup
                   || parameter is TmdbTvSeasonCorrectionSeasonItem);
    }

    private void SelectRecognizedSeasonTarget(object? parameter)
    {
        switch (parameter)
        {
            case TmdbTvSeasonCorrectionSeasonItem season:
                _selectedRecognizedSeriesTarget = season.Series;
                _selectedRecognizedSeasonTarget = season;
                SeasonCorrectionSearchQuery = season.SeriesTitle;
                SeasonCorrectionSeasonNumber = season.SeasonNumber.ToString();
                SeasonCorrectionStatusMessage = $"已选择目标季：{season.SeriesTitle} S{season.SeasonNumber:00}。";
                break;
            case TmdbTvSeriesCorrectionSeriesGroup series:
                _selectedRecognizedSeriesTarget = series;
                _selectedRecognizedSeasonTarget = null;
                SeasonCorrectionSearchQuery = series.SeriesTitle;
                SeasonCorrectionStatusMessage = $"已选择目标剧：{series.DisplayTitle}。请确认季号。";
                break;
            default:
                SeasonCorrectionStatusMessage = "请选择目标已识别剧或季。";
                return;
        }

        _selectedUnknownSeasonTarget = null;
        _selectedSeasonCorrectionTargetKind = SeasonCorrectionTargetRecognized;
        IsRecognizedSeasonPickerDialogOpen = false;
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetRecognized));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetUnknown));
        OnPropertyChanged(nameof(SeasonCorrectionRecognizedTargetButtonText));
        OnPropertyChanged(nameof(SeasonCorrectionUnknownTargetButtonText));
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        UpdateSeasonCorrectionConfirmation();
        LogSeasonCorrectionPreview();
    }

    private void CloseRecognizedSeasonPicker()
    {
        IsRecognizedSeasonPickerDialogOpen = false;
    }

    private async Task OpenUnknownSeasonCorrectionPickerAsync()
    {
        if (!IsSeasonCorrectionTargetUnknown)
        {
            SeasonCorrectionStatusMessage = "请先切换到加入已有未识别季。";
            return;
        }

        CancelSeasonCorrectionAiRequest();
        var cancellation = new CancellationTokenSource();
        _seasonCorrectionAiCancellation = cancellation;
        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = "正在加载已有未识别季...";
            UnknownSeasonSeriesGroups.Clear();
            OnPropertyChanged(nameof(HasUnknownSeasonTargets));
            _selectedUnknownSeasonTarget = null;
            _selectedRecognizedSeriesTarget = null;
            _selectedRecognizedSeasonTarget = null;
            OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
            OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
            UpdateSeasonCorrectionConfirmation();
            await Task.Yield();

            var targets = await _singleSourceCorrectionService.SearchUnknownSeasonTargetsAsync(null, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var filteredTargets = targets
                .Where(x => !_seasonId.HasValue || x.SeasonId != _seasonId.Value)
                .ToList();
            UnknownSeasonSeriesGroups.ReplaceAll(UnknownTvSeasonCorrectionSeriesGroup.FromTargets(filteredTargets));

            OnPropertyChanged(nameof(HasUnknownSeasonTargets));
            IsUnknownSeasonPickerDialogOpen = true;
            SeasonCorrectionStatusMessage = UnknownSeasonSeriesGroups.Count == 0
                ? "没有可选择的已有未识别季。"
                : $"已加载 {UnknownSeasonSeriesGroups.Count} 个未识别剧分组。";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_seasonCorrectionAiCancellation, cancellation) && IsSeasonCorrectionPanelOpen)
            {
                SeasonCorrectionStatusMessage = "加载未识别季已取消。";
            }
        }
        catch (Exception exception)
        {
            UnknownSeasonSeriesGroups.Clear();
            OnPropertyChanged(nameof(HasUnknownSeasonTargets));
            SeasonCorrectionStatusMessage = $"加载已有未识别季失败：{DescribeException(exception)}";
        }
        finally
        {
            var isCurrentRequest = ReferenceEquals(_seasonCorrectionAiCancellation, cancellation);
            if (isCurrentRequest)
            {
                _seasonCorrectionAiCancellation = null;
            }

            cancellation.Dispose();
            if (isCurrentRequest)
            {
                IsSeasonCorrectionBusy = false;
            }
        }
    }

    private void CloseUnknownSeasonPicker()
    {
        IsUnknownSeasonPickerDialogOpen = false;
    }

    private bool CanSelectUnknownSeasonTarget(object? parameter)
    {
        return IsSeasonCorrectionPanelOpen
               && IsSeasonCorrectionTargetUnknown
               && !IsSeasonCorrectionBusy
               && parameter is UnknownTvSeasonCorrectionTargetItem target
               && (!_seasonId.HasValue || target.SeasonId != _seasonId.Value);
    }

    private void SelectUnknownSeasonTarget(object? parameter)
    {
        if (parameter is not UnknownTvSeasonCorrectionTargetItem target)
        {
            SeasonCorrectionStatusMessage = "请选择目标未识别季。";
            return;
        }

        if (_seasonId.HasValue && target.SeasonId == _seasonId.Value)
        {
            SeasonCorrectionStatusMessage = "不能选择当前源季作为目标季。";
            return;
        }

        _selectedUnknownSeasonTarget = target;
        _selectedRecognizedSeriesTarget = null;
        _selectedRecognizedSeasonTarget = null;
        _selectedSeasonCorrectionTargetKind = SeasonCorrectionTargetUnknown;
        IsUnknownSeasonPickerDialogOpen = false;
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetRecognized));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetUnknown));
        OnPropertyChanged(nameof(SeasonCorrectionRecognizedTargetButtonText));
        OnPropertyChanged(nameof(SeasonCorrectionUnknownTargetButtonText));
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        SeasonCorrectionStatusMessage = $"已选择未识别季：{target.DisplayTitle}。";
        UpdateSeasonCorrectionConfirmation();
        LogSeasonCorrectionPreview();
    }

    private async Task ApplySeasonCorrectionV2Async()
    {
        if (!_seasonId.HasValue)
        {
            SeasonCorrectionStatusMessage = "当前源季尚未加载。";
            return;
        }

        if (IsSeasonCorrectionTargetRecognized && _selectedRecognizedSeriesTarget is null)
        {
            SeasonCorrectionStatusMessage = "请选择目标已识别电视剧或季。";
            return;
        }

        if (IsSeasonCorrectionTargetUnknown && _selectedUnknownSeasonTarget is null)
        {
            SeasonCorrectionStatusMessage = "请选择目标未识别季。";
            return;
        }

        if (!TryBuildSeasonCorrectionMappings(out var mappings, out var mappingError))
        {
            SeasonCorrectionStatusMessage = mappingError;
            return;
        }

        var sourceSeasonId = _seasonId.Value;
        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = IsSeasonCorrectionTargetRecognized
                ? "正在修正到已识别季..."
                : "正在加入已有未识别季...";
            UnknownSeasonCorrectionApplyResult result;
            if (IsSeasonCorrectionTargetRecognized)
            {
                if (!TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
                {
                    SeasonCorrectionStatusMessage = "季号必须是 0 或正整数。";
                    return;
                }

                result = await _unknownSeasonCorrectionService.ApplySeasonToRecognizedSeasonAsync(
                    sourceSeasonId,
                    _selectedRecognizedSeriesTarget!.TmdbSeriesId,
                    seasonNumber,
                    mappings);
            }
            else
            {
                result = await _unknownSeasonCorrectionService.ApplySeasonToUnknownSeasonAsync(
                    sourceSeasonId,
                    _selectedUnknownSeasonTarget!.SeasonId,
                    mappings);
            }

            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _navigationStateService.RequestTvSeasonDetail(result.TargetSeasonId);
            ClearSeasonCorrectionState();
            await ActivateAsync();
            StatusMessage = $"季级修正完成，移动 {result.MovedSourceCount} 个播放源，新增 {result.CreatedEpisodeCount} 集。";
        }
        catch (Exception exception)
        {
            SeasonCorrectionStatusMessage = $"季级修正失败：{DescribeException(exception)}";
        }
        finally
        {
            IsSeasonCorrectionBusy = false;
        }
    }

    private async Task ApplySeasonCorrectionAsync()
    {
        if (!_seasonId.HasValue || _selectedRecognizedSeriesTarget is null)
        {
            SeasonCorrectionStatusMessage = "请选择目标已识别季。";
            return;
        }

        if (!TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
        {
            SeasonCorrectionStatusMessage = "季号必须是 0 或正整数。";
            return;
        }

        if (!TryBuildSeasonCorrectionMappings(out var mappings, out var mappingError))
        {
            SeasonCorrectionStatusMessage = mappingError;
            return;
        }

        var sourceSeasonId = _seasonId.Value;
        var targetSeriesTmdbId = _selectedRecognizedSeriesTarget.TmdbSeriesId;
        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = "正在修正未识别季...";
            var result = await _unknownSeasonCorrectionService.ApplyUnknownSeasonToRecognizedSeasonAsync(
                sourceSeasonId,
                targetSeriesTmdbId,
                seasonNumber,
                mappings);

            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _navigationStateService.RequestTvSeasonDetail(result.TargetSeasonId);
            ClearSeasonCorrectionState();
            await ActivateAsync();
            StatusMessage = $"已修正为已识别季，移动 {result.MovedSourceCount} 个播放源，新增 {result.CreatedEpisodeCount} 集。";
        }
        catch (Exception exception)
        {
            SeasonCorrectionStatusMessage = $"修正未识别季失败：{DescribeException(exception)}";
        }
        finally
        {
            IsSeasonCorrectionBusy = false;
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetFavoriteAsync(_seasonId.Value, !IsFavorite, changeSource: "Manual");
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新喜爱状态失败：{DescribeException(exception)}";
        }
    }

    private Task TogglePreferenceAsync()
    {
        return IsSeasonWatched ? ToggleFavoriteAsync() : ToggleWantToWatchAsync();
    }

    private async Task ToggleWantToWatchAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetWantToWatchAsync(_seasonId.Value, !IsWantToWatch, changeSource: "Manual");
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新想看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task ToggleNotInterestedAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetNotInterestedAsync(_seasonId.Value, !IsNotInterested, changeSource: "Manual");
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新不想看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task SetSeasonWatchedAsync(bool isWatched)
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            foreach (var episode in Episodes)
            {
                episode.IsWatched = isWatched;
            }

            RefreshEpisodeAggregateState();
            await Task.Yield();
            await Task.Run(
                () => _tvSeasonCollectionService.SetWatchedAsync(_seasonId.Value, isWatched, changeSource: "Manual"));
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            StatusMessage = isWatched ? "已标记整季为已看。" : "已标记整季为未看。";
        }
        catch (Exception exception)
        {
            await ActivateAsync();
            StatusMessage = $"更新整季观看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task AddSeasonToLibraryAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            if (IsVisibleInLibrary)
            {
                await _tvSeasonCollectionService.RemoveFromLibraryAsync(_seasonId.Value);
            }
            else if (_libraryVisibilityState == LibraryVisibilityState.Hidden)
            {
                await _tvSeasonCollectionService.RestoreSeasonToLibraryAsync(_seasonId.Value);
            }
            else
            {
                await _tvSeasonCollectionService.AddSeasonToLibraryAsync(_seasonId.Value);
            }
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = IsVisibleInLibrary ? "已加入媒体库。" : "已移出媒体库。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"加入媒体库失败：{DescribeException(exception)}";
        }
    }

    private async Task SetEpisodeWatchedAsync(object? parameter, bool isWatched)
    {
        if (parameter is not TvSeasonEpisodeListItem episode)
        {
            StatusMessage = "请先选择要标记的集。";
            return;
        }

        try
        {
            episode.IsWatched = isWatched;
            RefreshEpisodeAggregateState();
            await Task.Yield();
            await Task.Run(
                () => _tvSeasonCollectionService.SetEpisodeWatchedAsync(episode.EpisodeId, isWatched, changeSource: "Manual"));
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            StatusMessage = $"{episode.EpisodeNumberText} 已标记为{(isWatched ? "已看" : "未看")}。";
        }
        catch (Exception exception)
        {
            await ActivateAsync();
            StatusMessage = $"更新集观看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task PlayEpisodeAsync(object? parameter)
    {
        if (parameter is not TvSeasonEpisodeListItem episode)
        {
            StatusMessage = "请先选择要播放的集。";
            return;
        }

        if (!episode.HasPlayableSource)
        {
            StatusMessage = $"{episode.EpisodeNumberText} 暂无可播放源。";
            return;
        }

        try
        {
            if (!CanOpenEpisodePlayer)
            {
                StatusMessage = "播放器已打开或正在打开，请关闭播放器后再播放其他集。";
                return;
            }

            IsOpeningEpisodePlayer = true;
            StatusMessage = $"{episode.EpisodeNumberText} 正在打开播放器。";
            await _playerWindowService.OpenEpisodeAsync(episode.EpisodeId);
            StatusMessage = $"{episode.EpisodeNumberText} 已打开播放器。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"打开剧集播放失败：{DescribeException(exception)}";
        }
        finally
        {
            if (!_playerWindowService.IsPlayerOpen)
            {
                IsOpeningEpisodePlayer = false;
            }
        }
    }

    private void RefreshEpisodeAggregateState()
    {
        var watchedCount = Episodes.Count(episode => episode.IsWatched);
        WatchedCountText = $"已看 {watchedCount} / {_totalEpisodeCount} 集";
        ProgressText = $"已看 {watchedCount} / {_totalEpisodeCount}";
        IsSeasonWatched = _totalEpisodeCount > 0
            ? Episodes.Count >= _totalEpisodeCount && watchedCount >= _totalEpisodeCount
            : Episodes.Count > 0 && watchedCount >= Episodes.Count;
        IsSeasonUnwatched = !IsSeasonWatched;
        if (!IsSeasonWatched)
        {
            IsFavorite = false;
        }

        if (IsSeasonWatched)
        {
            IsWantToWatch = false;
        }
    }

    private bool CanPlayEpisode(object? parameter)
    {
        return parameter is TvSeasonEpisodeListItem episode
               && episode.HasPlayableSource
               && CanOpenEpisodePlayer;
    }

    private void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        IsOpeningEpisodePlayer = false;
        _ = RefreshSeasonAfterPlayerClosedAsync();
    }

    private async Task RefreshSeasonAfterPlayerClosedAsync()
    {
        if (!_seasonId.HasValue || _navigationStateService.SelectedTvSeasonId != _seasonId.Value)
        {
            return;
        }

        try
        {
            await ActivateAsync();
        }
        catch
        {
            // Playback-close refresh is best-effort; the page can still be refreshed manually.
        }
    }

    private void ClearSeasonCorrectionState()
    {
        IsSeasonCorrectionPanelOpen = false;
        IsSeasonCorrectionBusy = false;
        IsSeasonEpisodeMappingDialogOpen = false;
        _seasonEpisodeMappingSnapshot = null;
        IsRecognizedSeasonPickerDialogOpen = false;
        IsUnknownSeasonPickerDialogOpen = false;
        RecognizedSeasonSeriesGroups.Clear();
        UnknownSeasonSeriesGroups.Clear();
        _selectedRecognizedSeriesTarget = null;
        _selectedRecognizedSeasonTarget = null;
        _selectedUnknownSeasonTarget = null;
        _selectedSeasonCorrectionTargetKind = SeasonCorrectionTargetRecognized;
        SeasonCorrectionSearchQuery = string.Empty;
        SeasonCorrectionConfirmationText = string.Empty;
        SeasonCorrectionStatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
        OnPropertyChanged(nameof(HasUnknownSeasonTargets));
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetRecognized));
        OnPropertyChanged(nameof(IsSeasonCorrectionTargetUnknown));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SeasonCorrectionRecognizedTargetButtonText));
        OnPropertyChanged(nameof(SeasonCorrectionUnknownTargetButtonText));
        RaiseSeasonCorrectionCommandStates();
    }

    private void UpdateSeasonCorrectionConfirmation()
    {
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        OnPropertyChanged(nameof(SelectedSeasonCorrectionTargetDisplay));
        if (IsSeasonCorrectionTargetUnknown)
        {
            if (_selectedUnknownSeasonTarget is null)
            {
                SeasonCorrectionConfirmationText = string.Empty;
                return;
            }

            if (!TryBuildSeasonCorrectionMappings(out var unknownMappings, out var unknownMappingError))
            {
                SeasonCorrectionConfirmationText = unknownMappingError;
                return;
            }

            var unknownTargetEpisodeCount = unknownMappings.Select(x => x.TargetEpisodeNumber).Distinct().Count();
            var unknownRemappedCount = unknownMappings.Count(x => x.OriginalEpisodeNumber != x.TargetEpisodeNumber);
            SeasonCorrectionConfirmationText =
                $"Move {unknownMappings.Count} sources to {_selectedUnknownSeasonTarget.DisplayTitle}; target episodes {unknownTargetEpisodeCount}; remapped {unknownRemappedCount}. No real files are deleted and no empty episodes are created.";
            return;
        }

        if (_selectedRecognizedSeriesTarget is null)
        {
            SeasonCorrectionConfirmationText = string.Empty;
            return;
        }

        if (!TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
        {
            SeasonCorrectionConfirmationText = "季号必须是 0 或正整数。";
            return;
        }

        if (!TryBuildSeasonCorrectionMappings(out var mappings, out var mappingError))
        {
            SeasonCorrectionConfirmationText = mappingError;
            return;
        }

        var targetEpisodeCount = mappings.Select(x => x.TargetEpisodeNumber).Distinct().Count();
        var remappedCount = mappings.Count(x => x.OriginalEpisodeNumber != x.TargetEpisodeNumber);
        SeasonCorrectionConfirmationText =
            $"将把当前未识别季的 {mappings.Count} 个播放源修正到 {_selectedRecognizedSeriesTarget.DisplayTitle} S{seasonNumber:D2}，目标集 {targetEpisodeCount} 个，改集号 {remappedCount} 个。不删除真实文件，不补空集。";
    }

    private void LogSeasonCorrectionPreview()
    {
        if (IsSeasonCorrectionTargetUnknown)
        {
            if (!_seasonId.HasValue || _selectedUnknownSeasonTarget is null)
            {
                return;
            }

            if (!TryBuildSeasonCorrectionMappings(out var unknownMappings, out _))
            {
                return;
            }

            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-preview-created sourceSeasonId={_seasonId.Value} targetKind=unknown targetSeasonId={_selectedUnknownSeasonTarget.SeasonId} movedSourceCount={unknownMappings.Count} mappingSummary={ScanIdentificationDiagnostics.FormatValue(BuildSeasonCorrectionMappingSummary(unknownMappings), 220)}");
            return;
        }

        if (!_seasonId.HasValue || _selectedRecognizedSeriesTarget is null || !TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
        {
            return;
        }

        if (!TryBuildSeasonCorrectionMappings(out var mappings, out _))
        {
            return;
        }

        ScanIdentificationDiagnostics.Write(
            $"event=season-correction-preview-created sourceSeasonId={_seasonId.Value} targetSeriesTmdbId={_selectedRecognizedSeriesTarget.TmdbSeriesId} targetSeasonNumber={seasonNumber} movedSourceCount={mappings.Count} mappingSummary={ScanIdentificationDiagnostics.FormatValue(BuildSeasonCorrectionMappingSummary(mappings), 220)}");
    }

    private bool TryGetSeasonCorrectionSeasonNumber(out int seasonNumber)
    {
        return int.TryParse(SeasonCorrectionSeasonNumber?.Trim(), out seasonNumber)
               && seasonNumber >= 0;
    }

    private bool HasValidSeasonCorrectionMappings()
    {
        return SeasonCorrectionMappings.Count > 0
               && SeasonCorrectionMappings.All(x => x.MediaFileId > 0)
               && SeasonCorrectionMappings.All(x => x.ParsedTargetEpisodeNumber.HasValue);
    }

    private bool TryBuildSeasonCorrectionMappings(
        out IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping> mappings,
        out string errorMessage)
    {
        var rows = SeasonCorrectionMappings.ToList();
        var result = new List<UnknownSeasonCorrectionEpisodeMapping>(rows.Count);
        if (rows.Count == 0)
        {
            mappings = [];
            errorMessage = "当前未识别季没有可迁移的播放源。";
            return false;
        }

        foreach (var row in rows)
        {
            if (row.MediaFileId <= 0)
            {
                mappings = [];
                errorMessage = $"请选择播放源：{row.OriginalEpisodeNumberText}";
                return false;
            }

            if (row.ParsedTargetEpisodeNumber is not { } targetEpisodeNumber)
            {
                mappings = [];
                errorMessage = $"目标集号必须是正整数：{row.OriginalEpisodeNumberText} · {row.FileName}";
                return false;
            }

            result.Add(
                new UnknownSeasonCorrectionEpisodeMapping
                {
                    MediaFileId = row.MediaFileId,
                    OriginalEpisodeNumber = row.OriginalEpisodeNumber,
                    TargetEpisodeNumber = targetEpisodeNumber
                });
        }

        mappings = result;
        errorMessage = string.Empty;
        return true;
    }

    private static string BuildSeasonCorrectionMappingSummary(
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping> mappings)
    {
        if (mappings.Count == 0)
        {
            return "none";
        }

        var groups = mappings
            .GroupBy(x => new { x.OriginalEpisodeNumber, x.TargetEpisodeNumber })
            .OrderBy(x => x.Key.OriginalEpisodeNumber)
            .ThenBy(x => x.Key.TargetEpisodeNumber)
            .Take(20)
            .Select(x => $"E{x.Key.OriginalEpisodeNumber}->E{x.Key.TargetEpisodeNumber}x{x.Count()}");
        var totalGroupCount = mappings
            .GroupBy(x => new { x.OriginalEpisodeNumber, x.TargetEpisodeNumber })
            .Count();
        return string.Join(",", groups) + (totalGroupCount > 20 ? ",..." : string.Empty);
    }

    private static bool TryParseDisplaySeasonNumber(string value, out int seasonNumber)
    {
        seasonNumber = 1;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("特别", StringComparison.CurrentCultureIgnoreCase)
            || value.Contains("Special", StringComparison.CurrentCultureIgnoreCase))
        {
            seasonNumber = 0;
            return true;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out seasonNumber) && seasonNumber >= 0;
    }

    private void Clear(string statusMessage)
    {
        IsDetailLoading = false;
        _seasonId = null;
        _seriesId = null;
        _episodeListScrollOffset = 0;
        OnPropertyChanged(nameof(EpisodeListScrollOffset));
        HasSeason = false;
        SeriesName = "-";
        SeriesOriginalName = "-";
        SeasonTitleText = "未选择电视剧季";
        Name = "未选择电视剧季";
        Overview = "请先选择一个电视剧季。";
        PosterDisplayUrl = string.Empty;
        SeasonNumberText = "-";
        AirDateText = "-";
        GenreDisplay = "未提供";
        _tmdbRatingDisplay = SeasonRatingUnavailableText;
        _imdbRatingDisplay = string.Empty;
        RatingDisplay = RatingUnavailableText;
        SourceSummary = "暂无播放源";
        ProgressText = "已看 0 / 0";
        InLibraryText = "暂无播放源";
        EpisodeCountText = "-";
        TotalEpisodeCountText = "-";
        WatchedCountText = "-";
        _totalEpisodeCount = 0;
        CountryText = "-";
        LanguageText = "-";
        DirectorText = "-";
        WriterText = "-";
        ActorsText = "-";
        NetworksText = "未提供";
        ProductionCompaniesText = "未提供";
        SeasonTmdbRating = new MovieRatingItem { SourceName = "TMDB" };
        IdentificationStatusText = "未加载";
        IsUnidentified = false;
        UnidentifiedSummary = string.Empty;
        IsFavorite = false;
        IsWantToWatch = false;
        IsNotInterested = false;
        IsSeasonWatched = false;
        IsSeasonUnwatched = true;
        IsVisibleInLibrary = false;
        IsEpisodeMetadataLoading = false;
        IsOpeningEpisodePlayer = false;
        CurrentLibraryVisibilityState = LibraryVisibilityState.Auto;
        StatusMessage = statusMessage;
        Episodes.Clear();
        SeasonCorrectionMappings.Clear();
        OnPropertyChanged(nameof(HasSeasonCorrectionMappings));
        OnPropertyChanged(nameof(HasInvalidSeasonCorrectionMappings));
        ClearSeasonCorrectionState();
        NavigateBackToSeriesCommand.RaiseCanExecuteChanged();
        RaiseSeasonStateCommandCanExecuteChanged();
        RaiseSeasonCorrectionCommandStates();
        RefreshEpisodePlayCommandState();
        OnPropertyChanged(nameof(HasEpisodes));
        OnPropertyChanged(nameof(HasNoEpisodes));
        OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
        OnPropertyChanged(nameof(CanShowRecognizedSeasonCorrectionEntry));
    }

    private void BeginDetailLoading(string statusMessage)
    {
        IsDetailLoading = true;
        Clear(statusMessage);
        IsDetailLoading = true;
    }

    private void RaiseSeasonStateCommandCanExecuteChanged()
    {
        ToggleFavoriteCommand.RaiseCanExecuteChanged();
        ToggleWantToWatchCommand.RaiseCanExecuteChanged();
        TogglePreferenceCommand.RaiseCanExecuteChanged();
        ToggleNotInterestedCommand.RaiseCanExecuteChanged();
        MarkSeasonWatchedCommand.RaiseCanExecuteChanged();
        MarkSeasonUnwatchedCommand.RaiseCanExecuteChanged();
        AddSeasonToLibraryCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSeasonCorrectionCommandStates()
    {
        OpenSeasonCorrectionCommand.RaiseCanExecuteChanged();
        CancelSeasonCorrectionCommand.RaiseCanExecuteChanged();
        CloseSeasonCorrectionCommand.RaiseCanExecuteChanged();
        OpenSeasonEpisodeMappingCommand.RaiseCanExecuteChanged();
        CloseSeasonEpisodeMappingCommand.RaiseCanExecuteChanged();
        ConfirmSeasonEpisodeMappingCommand.RaiseCanExecuteChanged();
        SearchSeasonCorrectionCandidatesCommand.RaiseCanExecuteChanged();
        AiSuggestSeasonCorrectionCommand.RaiseCanExecuteChanged();
        CloseRecognizedSeasonPickerCommand.RaiseCanExecuteChanged();
        SelectRecognizedSeasonTargetCommand.RaiseCanExecuteChanged();
        SelectSeasonCorrectionRecognizedTargetKindCommand.RaiseCanExecuteChanged();
        SelectSeasonCorrectionUnknownTargetKindCommand.RaiseCanExecuteChanged();
        OpenUnknownSeasonCorrectionPickerCommand.RaiseCanExecuteChanged();
        CloseUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
        SelectUnknownSeasonTargetCommand.RaiseCanExecuteChanged();
        ApplySeasonCorrectionCommand.RaiseCanExecuteChanged();
    }

    private void RefreshEpisodePlayCommandState()
    {
        OnPropertyChanged(nameof(CanOpenEpisodePlayer));
        OnPropertyChanged(nameof(EpisodePlayButtonBusyText));
        PlayEpisodeCommand.RaiseCanExecuteChanged();
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }

    private static string FormatAiSuggestionStatus(AiSearchSuggestionStatus status)
    {
        return status switch
        {
            AiSearchSuggestionStatus.Success => "success",
            AiSearchSuggestionStatus.NoResult => "no-result",
            AiSearchSuggestionStatus.Failed => "failed",
            _ => "unknown"
        };
    }

    private sealed record SeasonEpisodeMappingSnapshot(int MediaFileId, string TargetEpisodeNumberText);

    public sealed class TvSeasonTargetEpisodeLocatedEventArgs : EventArgs
    {
        public TvSeasonTargetEpisodeLocatedEventArgs(int episodeId)
        {
            EpisodeId = episodeId;
        }

        public int EpisodeId { get; }
    }
}
