using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.App.ViewModels.Collections;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class MovieDetailViewModel : PageViewModelBase
{
    private const string ExternalAiAnalyzingText = "AI 正在分析影片";
    private const string ExternalAiMissingText = "-";
    private const string CorrectionTargetMovieText = "修正为电影";
    private const string CorrectionTargetTvEpisodeText = "修正为电视剧集";
    private const string CorrectionTargetUnknownSeasonText = "加入已有未识别季";
    private static readonly TimeSpan CorrectionApplyTimeout = TimeSpan.FromSeconds(45);

    private readonly INavigationStateService _navigationStateService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly IMovieDetailQueryService _movieDetailQueryService;
    private readonly IMovieMetadataRefreshService _movieMetadataRefreshService;
    private readonly IDiscoveryMovieStatusResolver _discoveryMovieStatusResolver;
    private readonly ITmdbService _tmdbService;
    private readonly IMovieIdentificationService _movieIdentificationService;
    private readonly ISingleSourceCorrectionService _singleSourceCorrectionService;
    private readonly IMovieManagementService _movieManagementService;
    private readonly IAiClassificationService _aiClassificationService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IUserCollectionService _userCollectionService;
    private readonly IMediaProbeService _mediaProbeService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly HashSet<int> _lazyProbeCheckedMediaFileIds = [];
    private readonly HashSet<int> _probingMediaFileIds = [];
    private readonly ConcurrentDictionary<int, byte> _backgroundClassifyingMovieIds = new();
    private readonly ConcurrentDictionary<int, byte> _backgroundRefreshingMovieMetadataIds = new();
    private readonly ConcurrentDictionary<string, byte> _backgroundClassifyingExternalKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _isProbeCompletionRefreshQueued;
    private AiRecommendationItem? _externalRecommendation;
    private int? _movieId;
    private int? _tmdbId;
    private string _title = "未选择影片";
    private string _originalTitle = "-";
    private string _releaseYearText = "-";
    private string _releaseDateText = "-";
    private string _overview = "请先从资源库中选择一部影片。";
    private string _posterRemoteUrl = string.Empty;
    private string _posterDisplayUrl = string.Empty;
    private string _country = "-";
    private string _language = "-";
    private string _directorText = "-";
    private string _writerText = "-";
    private string _actorsText = "-";
    private string _productionCompanyText = "-";
    private string _runtimeText = "-";
    private string _genresText = "未提供";
    private string _aiTagsText = "尚未分类";
    private string _emotionTagsText = "尚未分类";
    private string _sceneTagsText = "尚未分类";
    private string _identificationStatusText = "未加载";
    private string _confidenceText = "-";
    private string _tmdbIdText = "-";
    private string _imdbIdText = "-";
    private string _defaultSourceDisplay = "尚未设置";
    private string _statusMessage = "请从资源库选择影片查看详情。";
    private string _availabilityText = "未加载";
    private string _playButtonText = "播放默认源";
    private string _favoriteButtonText = "喜爱";
    private string _watchedButtonText = "标记已看";
    private string _wantToWatchButtonText = "想看";
    private string _notInterestedButtonText = "不想看";
    private string _manualSearchQuery = string.Empty;
    private string _manualSearchYear = string.Empty;
    private string _tvCorrectionQuery = string.Empty;
    private string _correctionSeasonNumber = "1";
    private string _correctionEpisodeNumber = "1";
    private string _unknownSeasonSearchQuery = string.Empty;
    private string _unknownSeasonEpisodeNumber = "1";
    private string _selectedCorrectionTarget = CorrectionTargetMovieText;
    private string _movieCorrectionStatusMessage = string.Empty;
    private string _tvEpisodeCorrectionStatusMessage = string.Empty;
    private string _unknownSeasonCorrectionStatusMessage = string.Empty;
    private string _correctionSourceDisplay = "请选择一个播放源。";
    private string _correctionPreviewText = string.Empty;
    private string _correctionSourceFileName = string.Empty;
    private string _correctionSourcePath = string.Empty;
    private MovieSourceItem? _selectedCorrectionSource;
    private int? _selectedTvCorrectionSeriesTmdbId;
    private string _selectedTvCorrectionSeriesName = string.Empty;
    private int? _selectedTvCorrectionSeasonNumber;
    private UnknownTvSeasonCorrectionTargetItem? _selectedUnknownSeasonTarget;
    private int? _correctionMediaFileId;
    private int _selectedDetailTabIndex;
    private IdentificationStatus _identificationStatus;
    private bool _hasMovie;
    private bool _isLibraryMovie;
    private bool _isNoSourceDetail;
    private bool _canPlay;
    private bool _isOpeningPlayer;
    private bool _isTogglingWatched;
    private bool _isTogglingWantToWatch;
    private bool _isTogglingNotInterested;
    private bool _isTogglingFavorite;
    private bool _isFavorite;
    private bool _isWatched;
    private bool _isWantToWatch;
    private bool _isNotInterested;
    private bool _isVisibleInLibrary;
    private bool _isAddingToLibrary;
    private bool _autoVisibleInLibraryFromCurrentDetailState;
    private bool _hasCorrectionPreview;
    private bool _isCorrectionBusy;
    private bool _isRestoringCorrectionStatus;
    private bool _isUnknownSeasonPickerDialogOpen;
    private bool _isDetailLoading;
    private CancellationTokenSource? _correctionAiCancellation;
    private LibraryVisibilityState _libraryVisibilityState = LibraryVisibilityState.Auto;

    public MovieDetailViewModel(
        INavigationStateService navigationStateService,
        IPlayerWindowService playerWindowService,
        IMovieDetailQueryService movieDetailQueryService,
        IMovieMetadataRefreshService movieMetadataRefreshService,
        IDiscoveryMovieStatusResolver discoveryMovieStatusResolver,
        ITmdbService tmdbService,
        IMovieIdentificationService movieIdentificationService,
        ISingleSourceCorrectionService singleSourceCorrectionService,
        IMovieManagementService movieManagementService,
        IAiClassificationService aiClassificationService,
        IDataRefreshService dataRefreshService,
        IUserCollectionService userCollectionService,
        IMediaProbeService mediaProbeService,
        IConfirmationDialogService confirmationDialogService)
        : base("详情", "查看影片信息、播放源、评分、识别修正和观看记录。")
    {
        _navigationStateService = navigationStateService;
        _playerWindowService = playerWindowService;
        _movieDetailQueryService = movieDetailQueryService;
        _movieMetadataRefreshService = movieMetadataRefreshService;
        _discoveryMovieStatusResolver = discoveryMovieStatusResolver;
        _tmdbService = tmdbService;
        _movieIdentificationService = movieIdentificationService;
        _singleSourceCorrectionService = singleSourceCorrectionService;
        _movieManagementService = movieManagementService;
        _aiClassificationService = aiClassificationService;
        _dataRefreshService = dataRefreshService;
        _userCollectionService = userCollectionService;
        _mediaProbeService = mediaProbeService;
        _confirmationDialogService = confirmationDialogService;

        SearchCandidatesCommand = new AsyncRelayCommand(SearchCandidatesAsync, CanSearchCandidates);
        ApplyManualMatchCommand = new AsyncRelayCommand(ApplyMovieCandidateCorrectionAsync, CanApplyMovieCandidateCorrection);
        BeginSourceCorrectionCommand = new RelayCommand(BeginSourceCorrection, CanBeginSourceCorrection);
        SelectTvEpisodeCorrectionTargetCommand = new RelayCommand(SelectTvEpisodeCorrectionTarget, CanSelectTvEpisodeCorrectionTarget);
        ApplyTvEpisodeCorrectionTargetCommand = new AsyncRelayCommand(ApplyTvEpisodeCorrectionTargetAsync, CanSelectTvEpisodeCorrectionTarget);
        PreviewTvEpisodeCorrectionCommand = new AsyncRelayCommand(ApplySelectedTvEpisodeCorrectionAsync, CanApplySelectedTvEpisodeCorrection);
        SearchUnknownSeasonTargetsCommand = new AsyncRelayCommand(SearchUnknownSeasonTargetsAsync, CanSearchUnknownSeasonTargets);
        OpenUnknownSeasonPickerCommand = new AsyncRelayCommand(OpenUnknownSeasonPickerAsync, CanOpenUnknownSeasonPicker);
        CloseUnknownSeasonPickerCommand = new RelayCommand(CloseUnknownSeasonPicker, () => IsUnknownSeasonPickerDialogOpen);
        SelectUnknownSeasonTargetCommand = new RelayCommand(SelectUnknownSeasonTarget, CanSelectUnknownSeasonTarget);
        ApplyUnknownSeasonCorrectionCommand = new AsyncRelayCommand(ApplyUnknownSeasonCorrectionAsync, CanApplyUnknownSeasonCorrection);
        CancelCorrectionCommand = new RelayCommand(CancelCorrection, () => IsCorrectionPanelVisible && !IsCorrectionBusy);
        CloseCorrectionCommand = new RelayCommand(CloseCorrection, () => IsCorrectionPanelVisible);
        OpenCorrectionDialogCommand = new RelayCommand(OpenDefaultSourceCorrection, () => CanUseIdentificationCorrection);
        SetDefaultSourceCommand = new AsyncRelayCommand(SetDefaultSourceAsync);
        ResetSourceRecognitionCommand = new AsyncRelayCommand(ResetSourceRecognitionAsync, CanResetSourceRecognition);
        ManualProbeSourceCommand = new RelayCommand(parameter => _ = ManualProbeSourceAsync(parameter), CanManualProbeSource);
        OpenPlayerCommand = new AsyncRelayCommand(OpenPlayerAsync, _ => CanOpenPlayer);
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, disableWhileExecuting: false);
        TogglePreferenceCommand = new AsyncRelayCommand(TogglePreferenceAsync, () => CanTogglePreference, disableWhileExecuting: false);
        ToggleWatchedCommand = new AsyncRelayCommand(ToggleWatchedAsync, () => CanToggleWatched, disableWhileExecuting: false);
        ToggleWantToWatchCommand = new AsyncRelayCommand(ToggleWantToWatchAsync, () => CanToggleWantToWatch, disableWhileExecuting: false);
        ToggleNotInterestedCommand = new AsyncRelayCommand(ToggleNotInterestedAsync, () => CanToggleNotInterested, disableWhileExecuting: false);
        AddToLibraryCommand = new AsyncRelayCommand(AddToLibraryAsync, () => CanAddToLibrary);
        AiSuggestSearchCommand = new AsyncRelayCommand(AiSuggestSearchAsync, CanAiSuggestSearch);
        ClearManualSearchQueryCommand = new RelayCommand(() => ManualSearchQuery = string.Empty);
        ClearManualSearchYearCommand = new RelayCommand(() => ManualSearchYear = string.Empty);
        ClearTvCorrectionQueryCommand = new RelayCommand(() => TvCorrectionQuery = string.Empty);
        ClearCorrectionSeasonNumberCommand = new RelayCommand(() => CorrectionSeasonNumber = string.Empty);
        ClearCorrectionEpisodeNumberCommand = new RelayCommand(() => CorrectionEpisodeNumber = string.Empty);
        ClearUnknownSeasonEpisodeNumberCommand = new RelayCommand(() => UnknownSeasonEpisodeNumber = string.Empty);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
        NavigateBackCommand = new RelayCommand(_navigationStateService.RequestDetailBackToLibrary);

        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;
        _mediaProbeService.ProbeStatusChanged += OnProbeStatusChanged;
    }

    public ObservableCollection<MovieRatingItem> Ratings { get; } = [];

    public ObservableCollection<MovieSourceItem> Sources { get; } = [];

    public ObservableCollection<MetadataSearchCandidate> SearchCandidates { get; } = [];

    public ObservableCollection<TmdbTvSeriesSearchItem> TvSearchCandidates { get; } = [];

    public ObservableCollection<TmdbTvSeriesCorrectionSeriesGroup> TvSeriesCandidateGroups { get; } = [];

    public BulkObservableCollection<UnknownTvSeasonCorrectionTargetItem> UnknownSeasonTargets { get; } = [];

    public BulkObservableCollection<UnknownTvSeasonCorrectionSeriesGroup> UnknownSeasonSeriesGroups { get; } = [];

    public RelayCommand NavigateBackCommand { get; }

    public IReadOnlyList<string> CorrectionTargetOptions { get; } =
    [
        CorrectionTargetMovieText,
        CorrectionTargetTvEpisodeText,
        CorrectionTargetUnknownSeasonText
    ];

    public AsyncRelayCommand SearchCandidatesCommand { get; }

    public AsyncRelayCommand ApplyManualMatchCommand { get; }

    public RelayCommand BeginSourceCorrectionCommand { get; }

    public RelayCommand SelectTvEpisodeCorrectionTargetCommand { get; }

    public AsyncRelayCommand ApplyTvEpisodeCorrectionTargetCommand { get; }

    public AsyncRelayCommand PreviewTvEpisodeCorrectionCommand { get; }

    public AsyncRelayCommand SearchUnknownSeasonTargetsCommand { get; }

    public AsyncRelayCommand OpenUnknownSeasonPickerCommand { get; }

    public RelayCommand CloseUnknownSeasonPickerCommand { get; }

    public RelayCommand SelectUnknownSeasonTargetCommand { get; }

    public AsyncRelayCommand ApplyUnknownSeasonCorrectionCommand { get; }

    public RelayCommand CancelCorrectionCommand { get; }

    public RelayCommand CloseCorrectionCommand { get; }

    public RelayCommand OpenCorrectionDialogCommand { get; }

    public AsyncRelayCommand SetDefaultSourceCommand { get; }

    public AsyncRelayCommand ResetSourceRecognitionCommand { get; }

    public RelayCommand ManualProbeSourceCommand { get; }

    public AsyncRelayCommand OpenPlayerCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand TogglePreferenceCommand { get; }

    public AsyncRelayCommand ToggleWatchedCommand { get; }

    public AsyncRelayCommand ToggleWantToWatchCommand { get; }

    public AsyncRelayCommand ToggleNotInterestedCommand { get; }

    public AsyncRelayCommand AddToLibraryCommand { get; }

    public AsyncRelayCommand AiSuggestSearchCommand { get; }

    public RelayCommand ClearManualSearchQueryCommand { get; }

    public RelayCommand ClearManualSearchYearCommand { get; }

    public RelayCommand ClearTvCorrectionQueryCommand { get; }

    public RelayCommand ClearCorrectionSeasonNumberCommand { get; }

    public RelayCommand ClearCorrectionEpisodeNumberCommand { get; }

    public RelayCommand ClearUnknownSeasonEpisodeNumberCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string TitleText { get => _title; private set => SetProperty(ref _title, value); }

    public string OriginalTitle { get => _originalTitle; private set => SetProperty(ref _originalTitle, value); }

    public string ReleaseYearText { get => _releaseYearText; private set => SetProperty(ref _releaseYearText, value); }

    public string ReleaseDateText { get => _releaseDateText; private set => SetProperty(ref _releaseDateText, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string PosterRemoteUrl { get => _posterRemoteUrl; private set => SetProperty(ref _posterRemoteUrl, value); }

    public string PosterDisplayUrl { get => _posterDisplayUrl; private set => SetProperty(ref _posterDisplayUrl, value); }

    public string Country { get => _country; private set => SetProperty(ref _country, value); }

    public string Language { get => _language; private set => SetProperty(ref _language, value); }

    public string DirectorText { get => _directorText; private set => SetProperty(ref _directorText, value); }

    public string WriterText { get => _writerText; private set => SetProperty(ref _writerText, value); }

    public string ActorsText { get => _actorsText; private set => SetProperty(ref _actorsText, value); }

    public string ProductionCompanyText { get => _productionCompanyText; private set => SetProperty(ref _productionCompanyText, value); }

    public string RuntimeText { get => _runtimeText; private set => SetProperty(ref _runtimeText, value); }

    public string GenresText
    {
        get => _genresText;
        private set
        {
            if (SetProperty(ref _genresText, value))
            {
                OnPropertyChanged(nameof(GenreTags));
            }
        }
    }

    public string AiTagsText { get => _aiTagsText; private set => SetProperty(ref _aiTagsText, value); }

    public string EmotionTagsText
    {
        get => _emotionTagsText;
        private set
        {
            if (SetProperty(ref _emotionTagsText, value))
            {
                OnPropertyChanged(nameof(EmotionTags));
            }
        }
    }

    public string SceneTagsText
    {
        get => _sceneTagsText;
        private set
        {
            if (SetProperty(ref _sceneTagsText, value))
            {
                OnPropertyChanged(nameof(SceneTags));
            }
        }
    }

    public IReadOnlyList<string> GenreTags => SplitDisplayTags(GenresText);

    public IReadOnlyList<string> EmotionTags => SplitDisplayTags(EmotionTagsText);

    public IReadOnlyList<string> SceneTags => SplitDisplayTags(SceneTagsText);

    public string IdentificationStatusText { get => _identificationStatusText; private set => SetProperty(ref _identificationStatusText, value); }

    public string ConfidenceText { get => _confidenceText; private set => SetProperty(ref _confidenceText, value); }

    public string TmdbIdText { get => _tmdbIdText; private set => SetProperty(ref _tmdbIdText, value); }

    public string ImdbIdText { get => _imdbIdText; private set => SetProperty(ref _imdbIdText, value); }

    public string DefaultSourceDisplay { get => _defaultSourceDisplay; private set => SetProperty(ref _defaultSourceDisplay, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                StoreCorrectionStatusForSelectedTarget(value);
            }
        }
    }

    public string AvailabilityText { get => _availabilityText; private set => SetProperty(ref _availabilityText, value); }

    public string PlayButtonText { get => _playButtonText; private set => SetProperty(ref _playButtonText, value); }

    public string FavoriteButtonText { get => _favoriteButtonText; private set => SetProperty(ref _favoriteButtonText, value); }

    public string WatchedButtonText { get => _watchedButtonText; private set => SetProperty(ref _watchedButtonText, value); }

    public string WantToWatchButtonText { get => _wantToWatchButtonText; private set => SetProperty(ref _wantToWatchButtonText, value); }

    public string NotInterestedButtonText { get => _notInterestedButtonText; private set => SetProperty(ref _notInterestedButtonText, value); }

    public string WatchedButtonIcon => IsWatched ? "x-circle" : "check-circle";

    public string PreferenceButtonText => IsWatched ? FavoriteButtonText : WantToWatchButtonText;

    public string PreferenceButtonIcon => IsWatched
        ? IsFavorite ? "heart-fill" : "heart"
        : IsWantToWatch ? "star-fill" : "star";

    public string NotInterestedButtonIcon => IsNotInterested ? "arrow-counter-clockwise" : "prohibit";

    public string ManualSearchQuery { get => _manualSearchQuery; set => SetProperty(ref _manualSearchQuery, value); }

    public string ManualSearchYear { get => _manualSearchYear; set => SetProperty(ref _manualSearchYear, value); }

    public string TvCorrectionQuery { get => _tvCorrectionQuery; set => SetProperty(ref _tvCorrectionQuery, value); }

    public string CorrectionSeasonNumber
    {
        get => _correctionSeasonNumber;
        set
        {
            if (SetProperty(ref _correctionSeasonNumber, value))
            {
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrectionEpisodeNumber
    {
        get => _correctionEpisodeNumber;
        set
        {
            if (SetProperty(ref _correctionEpisodeNumber, value))
            {
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string UnknownSeasonSearchQuery { get => _unknownSeasonSearchQuery; set => SetProperty(ref _unknownSeasonSearchQuery, value); }

    public string UnknownSeasonEpisodeNumber
    {
        get => _unknownSeasonEpisodeNumber;
        set
        {
            if (SetProperty(ref _unknownSeasonEpisodeNumber, value))
            {
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedCorrectionTarget
    {
        get => _selectedCorrectionTarget;
        set
        {
            if (SetProperty(ref _selectedCorrectionTarget, value))
            {
                ClearCorrectionPreview();
                OnPropertyChanged(nameof(IsCorrectionTargetMovie));
                OnPropertyChanged(nameof(IsCorrectionTargetTvEpisode));
                OnPropertyChanged(nameof(IsCorrectionTargetUnknownSeason));
                ClearSelectedTvCorrectionTarget();
                RestoreCorrectionStatusForSelectedTarget();
                OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(HasTvSearchCandidates));
                SearchCandidatesCommand.RaiseCanExecuteChanged();
                ApplyManualMatchCommand.RaiseCanExecuteChanged();
                SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
                ApplyTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
                OpenUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
                SelectUnknownSeasonTargetCommand.RaiseCanExecuteChanged();
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
                if (IsCorrectionTargetUnknownSeason && UnknownSeasonSeriesGroups.Count == 0)
                {
                    _ = SearchUnknownSeasonTargetsAsync();
                }
            }
        }
    }

    public string CorrectionSourceDisplay { get => _correctionSourceDisplay; private set => SetProperty(ref _correctionSourceDisplay, value); }

    public string CorrectionSourceFileName { get => _correctionSourceFileName; private set => SetProperty(ref _correctionSourceFileName, value); }

    public string CorrectionSourcePath { get => _correctionSourcePath; private set => SetProperty(ref _correctionSourcePath, value); }

    public MovieSourceItem? SelectedCorrectionSource
    {
        get => _selectedCorrectionSource;
        set
        {
            if (SetProperty(ref _selectedCorrectionSource, value)
                && value is not null
                && IsCorrectionPanelVisible
                && _correctionMediaFileId != value.MediaFileId)
            {
                BeginSourceCorrection(value);
            }
        }
    }

    public string CorrectionPreviewText { get => _correctionPreviewText; private set => SetProperty(ref _correctionPreviewText, value); }

    public UnknownTvSeasonCorrectionTargetItem? SelectedUnknownSeasonTarget
    {
        get => _selectedUnknownSeasonTarget;
        private set
        {
            if (SetProperty(ref _selectedUnknownSeasonTarget, value))
            {
                OnPropertyChanged(nameof(HasSelectedUnknownSeasonTarget));
                OnPropertyChanged(nameof(SelectedUnknownSeasonTargetDisplay));
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedUnknownSeasonTarget => SelectedUnknownSeasonTarget is not null;

    public string SelectedUnknownSeasonTargetDisplay => SelectedUnknownSeasonTarget is null
        ? "尚未选择未识别季。"
        : $"{SelectedUnknownSeasonTarget.DisplayTitle} · {SelectedUnknownSeasonTarget.DisplaySubtitle}";

    public bool IsUnknownSeasonPickerDialogOpen
    {
        get => _isUnknownSeasonPickerDialogOpen;
        private set
        {
            if (SetProperty(ref _isUnknownSeasonPickerDialogOpen, value))
            {
                CloseUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
                OpenUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
                SelectUnknownSeasonTargetCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCorrectionPanelVisible => _correctionMediaFileId.HasValue;

    public bool IsCorrectionTargetMovie => SelectedCorrectionTarget == CorrectionTargetMovieText;

    public bool IsCorrectionTargetTvEpisode => SelectedCorrectionTarget == CorrectionTargetTvEpisodeText;

    public bool IsCorrectionTargetUnknownSeason => SelectedCorrectionTarget == CorrectionTargetUnknownSeasonText;

    public int SelectedDetailTabIndex { get => _selectedDetailTabIndex; set => SetProperty(ref _selectedDetailTabIndex, value); }

    public bool HasCorrectionPreview
    {
        get => _hasCorrectionPreview;
        private set
        {
            SetProperty(ref _hasCorrectionPreview, value);
        }
    }

    public bool IsCorrectionBusy
    {
        get => _isCorrectionBusy;
        private set
        {
            if (SetProperty(ref _isCorrectionBusy, value))
            {
                SearchCandidatesCommand.RaiseCanExecuteChanged();
                ApplyManualMatchCommand.RaiseCanExecuteChanged();
                SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
                ApplyTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
                OpenUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
                SelectUnknownSeasonTargetCommand.RaiseCanExecuteChanged();
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
                CancelCorrectionCommand.RaiseCanExecuteChanged();
                CloseCorrectionCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsCorrectionInteractionEnabled));
            }
        }
    }

    public bool IsCorrectionInteractionEnabled => !IsCorrectionBusy;

    public bool IsDetailLoading
    {
        get => _isDetailLoading;
        private set => SetProperty(ref _isDetailLoading, value);
    }

    public bool HasMovie
    {
        get => _hasMovie;
        private set
        {
            if (SetProperty(ref _hasMovie, value))
            {
                OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(CanUseIdentificationCorrection));
                OnPropertyChanged(nameof(ShowLibrarySections));
                OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
                OnPropertyChanged(nameof(IsUnidentifiedMovie));
                OnPropertyChanged(nameof(HasNoSources));
                OnPropertyChanged(nameof(ShowExternalWantToWatchAction));
                OnPropertyChanged(nameof(ShowPreferenceAction));
                OnPropertyChanged(nameof(ShowNotInterestedAction));
                OnPropertyChanged(nameof(ShowWatchedAction));
                OnPropertyChanged(nameof(ShowAddToLibraryAction));
                RefreshWantToWatchCommandState();
                RefreshPreferenceCommandState();
                RefreshNotInterestedCommandState();
                RefreshWatchedCommandState();
                RefreshAddToLibraryCommandState();
                RefreshResetSourceRecognitionCommandState();
                OpenCorrectionDialogCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLibraryMovie
    {
        get => _isLibraryMovie;
        private set
        {
            if (SetProperty(ref _isLibraryMovie, value))
            {
                OnPropertyChanged(nameof(CanUseIdentificationCorrection));
                OnPropertyChanged(nameof(ShowLibrarySections));
                OnPropertyChanged(nameof(ShowCollectionActions));
                OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
                OnPropertyChanged(nameof(ShowExternalWantToWatchAction));
                OnPropertyChanged(nameof(ShowPreferenceAction));
                OnPropertyChanged(nameof(ShowNotInterestedAction));
                OnPropertyChanged(nameof(ShowWatchedAction));
                OnPropertyChanged(nameof(ShowAddToLibraryAction));
                RefreshWantToWatchCommandState();
                RefreshPreferenceCommandState();
                RefreshNotInterestedCommandState();
                RefreshWatchedCommandState();
                RefreshAddToLibraryCommandState();
                RefreshResetSourceRecognitionCommandState();
                OpenCorrectionDialogCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanPlay
    {
        get => _canPlay;
        private set
        {
            if (SetProperty(ref _canPlay, value))
            {
                RefreshOpenPlayerCommandState();
            }
        }
    }

    public bool CanOpenPlayer => CanPlay && !_isOpeningPlayer && !_playerWindowService.IsPlayerOpen;

    public bool CanResetSourcesToUnidentified => HasMovie
                                                 && IsLibraryMovie
                                                 && _identificationStatus != IdentificationStatus.Failed;

    public bool IsNoSourceDetail
    {
        get => _isNoSourceDetail;
        private set => SetProperty(ref _isNoSourceDetail, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                FavoriteButtonText = value ? "取消喜爱" : "喜爱";
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                RefreshPreferenceCommandState();
            }
        }
    }

    public bool IsWatched
    {
        get => _isWatched;
        private set
        {
            if (SetProperty(ref _isWatched, value))
            {
                WatchedButtonText = value ? "标记未看" : "标记已看";
                OnPropertyChanged(nameof(WatchedButtonIcon));
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                RefreshWantToWatchCommandState();
                RefreshPreferenceCommandState();
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
                WantToWatchButtonText = value ? "取消想看" : "想看";
                OnPropertyChanged(nameof(PreferenceButtonText));
                OnPropertyChanged(nameof(PreferenceButtonIcon));
                RefreshPreferenceCommandState();
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
                NotInterestedButtonText = value ? "取消不想看" : "不想看";
                OnPropertyChanged(nameof(NotInterestedButtonIcon));
            }
        }
    }

    public bool HasSearchCandidates => SearchCandidates.Count > 0;

    public bool HasTvSearchCandidates => TvSeriesCandidateGroups.Count > 0;

    public bool HasRatings => Ratings.Count > 0;

    public bool HasSources => Sources.Count > 0;

    public bool HasNoSources => HasMovie && Sources.Count == 0;

    public bool HasSelectedTvCorrectionTarget => _selectedTvCorrectionSeriesTmdbId.HasValue;

    public string SelectedTvCorrectionTargetDisplay
    {
        get
        {
            if (!_selectedTvCorrectionSeriesTmdbId.HasValue)
            {
                return "尚未选择目标电视剧。";
            }

            return _selectedTvCorrectionSeasonNumber.HasValue
                ? $"{_selectedTvCorrectionSeriesName} / S{_selectedTvCorrectionSeasonNumber.Value:00}"
                : _selectedTvCorrectionSeriesName;
        }
    }

    public bool HasUnknownSeasonTargets => UnknownSeasonSeriesGroups.Count > 0;

    public bool CanUseIdentificationCorrection => HasMovie && IsLibraryMovie && Sources.Count > 0;

    public bool ShowLibrarySections => HasMovie;

    public bool ShowCollectionActions => IsLibraryMovie;

    public bool ShowExternalWantToWatchAction => HasMovie && !IsLibraryMovie;

    public bool ShowPreferenceAction => HasMovie;

    public bool ShowNotInterestedAction => HasMovie;

    public bool ShowWatchedAction => HasMovie && (IsLibraryMovie || _externalRecommendation is not null);

    public bool ShowAddToLibraryAction => HasMovie;

    public bool CanAddToLibrary => ShowAddToLibraryAction && !_isAddingToLibrary;

    public string AddToLibraryButtonText => IsVisibleInLibrary ? "移出媒体库" : "加入媒体库";

    public string AddToLibraryButtonIcon => IsVisibleInLibrary ? "minus-square" : "plus-square";

    public bool IsVisibleInLibrary
    {
        get => _isVisibleInLibrary;
        private set
        {
            if (SetProperty(ref _isVisibleInLibrary, value))
            {
                RefreshAddToLibraryButtonState();
            }
        }
    }

    private LibraryVisibilityState CurrentLibraryVisibilityState
    {
        get => _libraryVisibilityState;
        set
        {
            if (SetProperty(ref _libraryVisibilityState, value))
            {
                RefreshAddToLibraryButtonState();
            }
        }
    }

    public bool CanToggleWatched => ShowWatchedAction;

    public bool CanToggleWantToWatch => (ShowExternalWantToWatchAction
                                         && _externalRecommendation is not null
                                         || IsLibraryMovie
                                         && _movieId.HasValue)
                                        && !IsWatched;

    public bool CanTogglePreference => ShowPreferenceAction
                                       && (IsWatched
                                           ? CanToggleFavoriteForCurrentMovie
                                           : CanToggleWantToWatch);

    private int? CurrentFavoriteMovieId =>
        IsLibraryMovie
            ? _movieId
            : _externalRecommendation?.MovieId is > 0
                ? _externalRecommendation.MovieId
                : null;

    private bool CanToggleFavoriteForCurrentMovie =>
        CurrentFavoriteMovieId.HasValue
        || (!IsLibraryMovie && _externalRecommendation is not null);

    public bool CanToggleNotInterested => ShowNotInterestedAction;

    public bool ShowRatingsAndTagsTab => ShowLibrarySections
                                         && (_externalRecommendation is not null
                                             || _identificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed);

    public bool IsUnidentifiedMovie => HasMovie && _identificationStatus == IdentificationStatus.Failed;

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        if (_navigationStateService.SelectedExternalRecommendation is { } externalRecommendation)
        {
            await LoadExternalRecommendationAsync(externalRecommendation, cancellationToken);
            return;
        }

        var selectedMovieId = _navigationStateService.SelectedMovieId;
        if (!selectedMovieId.HasValue)
        {
            ClearMovieState("请先从资源库中选择一部影片。");
            return;
        }

        await LoadMovieAsync(selectedMovieId.Value, cancellationToken);
    }

    public override void Deactivate()
    {
        _probingMediaFileIds.Clear();
        _lazyProbeCheckedMediaFileIds.Clear();
        _isProbeCompletionRefreshQueued = false;
        ManualProbeSourceCommand.RaiseCanExecuteChanged();
    }

    public void PrepareForActivation()
    {
        var selectedExternalRecommendation = _navigationStateService.SelectedExternalRecommendation;
        if (selectedExternalRecommendation is not null)
        {
            if (!ReferenceEquals(_externalRecommendation, selectedExternalRecommendation))
            {
                BeginDetailLoading("正在加载影片详情...");
            }

            return;
        }

        var selectedMovieId = _navigationStateService.SelectedMovieId;
        if (selectedMovieId.HasValue
            && (!_movieId.HasValue
                || _movieId.Value != selectedMovieId.Value
                || _externalRecommendation is not null))
        {
            BeginDetailLoading("正在加载影片详情...");
        }
    }

    private async Task LoadMovieAsync(int movieId, CancellationToken cancellationToken)
    {
        if (_movieId.HasValue && _movieId.Value != movieId)
        {
            BeginDetailLoading("正在加载影片详情...");
            await Task.Yield();
        }

        try
        {
            var detail = await _movieDetailQueryService.GetMovieDetailAsync(movieId, cancellationToken);
            if (detail is null)
            {
                ClearMovieState("未找到对应影片详情，可能已被删除。");
                return;
            }

            var isNewMovie = _movieId != detail.MovieId;
            var wasExternalDetail = _externalRecommendation is not null;
            _movieId = detail.MovieId;
            _tmdbId = detail.TmdbId;
            _externalRecommendation = null;
            HasMovie = true;
            IsLibraryMovie = true;
            IsFavorite = detail.IsFavorite;
            IsWatched = detail.IsWatched;
            IsWantToWatch = detail.IsWantToWatch;
            IsNotInterested = detail.IsNotInterested;
            IsVisibleInLibrary = detail.IsVisibleInLibrary;
            CurrentLibraryVisibilityState = detail.LibraryVisibilityState;
            if (isNewMovie || wasExternalDetail)
            {
                ResetDetailAutoLibraryVisibilityTracking();
            }
            RefreshWantToWatchCommandState();
            RefreshNotInterestedCommandState();
            RefreshWatchedCommandState();
            RefreshAddToLibraryCommandState();
            TitleText = detail.Title;
            OriginalTitle = string.IsNullOrWhiteSpace(detail.OriginalTitle) ? "-" : detail.OriginalTitle;
            ReleaseYearText = detail.ReleaseYear?.ToString() ?? "-";
            ReleaseDateText = detail.ReleaseDate.HasValue
                ? detail.ReleaseDate.Value.ToString("yyyy-MM-dd")
                : detail.ReleaseYear?.ToString() ?? "-";
            Overview = string.IsNullOrWhiteSpace(detail.Overview) ? "暂无简介" : detail.Overview;
            PosterRemoteUrl = detail.PosterRemoteUrl;
            PosterDisplayUrl = detail.PosterDisplayUrl;
            Country = MovieMetadataDisplayText.LocalizeCountries(detail.Country);
            Language = MovieMetadataDisplayText.LocalizeLanguages(detail.Language);
            DirectorText = string.IsNullOrWhiteSpace(detail.DirectorText) ? "-" : detail.DirectorText;
            WriterText = string.IsNullOrWhiteSpace(detail.WriterText) ? "-" : detail.WriterText;
            ActorsText = string.IsNullOrWhiteSpace(detail.ActorsText) ? "-" : detail.ActorsText;
            ProductionCompanyText = string.IsNullOrWhiteSpace(detail.ProductionCompanyText) ? "-" : detail.ProductionCompanyText;
            RuntimeText = FormatRuntimeMinutes(detail.RuntimeMinutes);
            GenresText = string.IsNullOrWhiteSpace(detail.GenresText)
                ? detail.IdentificationStatus == IdentificationStatus.Failed ? "-" : "未提供"
                : detail.GenresText;
            AiTagsText = string.IsNullOrWhiteSpace(detail.AiTagsText) ? "尚未分类" : detail.AiTagsText;
            EmotionTagsText = string.IsNullOrWhiteSpace(detail.EmotionTagsText) ? "尚未分类" : detail.EmotionTagsText;
            SceneTagsText = string.IsNullOrWhiteSpace(detail.SceneTagsText) ? "尚未分类" : detail.SceneTagsText;
            IdentificationStatusText = GetIdentificationStatusText(detail.IdentificationStatus);
            _identificationStatus = detail.IdentificationStatus;
            RefreshResetSourceRecognitionCommandState();
            if (_identificationStatus is not (IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed))
            {
                GenresText = "-";
                AiTagsText = "-";
                EmotionTagsText = "-";
                SceneTagsText = "-";
            }

            OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
            OnPropertyChanged(nameof(IsUnidentifiedMovie));
            ConfidenceText = detail.IdentifiedConfidence.HasValue ? $"{detail.IdentifiedConfidence:P0}" : "-";
            TmdbIdText = detail.TmdbId?.ToString() ?? "-";
            ImdbIdText = string.IsNullOrWhiteSpace(detail.ImdbId) ? "-" : detail.ImdbId;

            Ratings.Clear();
            foreach (var rating in NormalizeDetailRatings(detail.Ratings))
            {
                Ratings.Add(rating);
            }
            NotifyRatingStateChanged();

            Sources.Clear();
            foreach (var source in detail.Sources)
            {
                Sources.Add(source);
            }
            NotifySourceStateChanged();
            SyncProbeBusyStateFromSources();
            RefreshResetSourceRecognitionCommandState();

            var hasSources = Sources.Count > 0;
            var isUnidentifiedPlaceholder = detail.IdentificationStatus == IdentificationStatus.Failed
                                            && !detail.TmdbId.HasValue;
            IsNoSourceDetail = !hasSources && !isUnidentifiedPlaceholder;
            OnPropertyChanged(nameof(CanUseIdentificationCorrection));
            AvailabilityText = isUnidentifiedPlaceholder
                ? "未识别 / 待修正"
                : hasSources
                    ? "有播放源"
                    : "暂无播放源";
            CanPlay = hasSources;
            PlayButtonText = CanPlay ? "播放默认源" : "暂无可播放源";
            if (detail.TmdbId.HasValue && NeedsMovieCreditsHydration(detail))
            {
                _ = HydrateMovieCreditsForDisplayAsync(detail.MovieId, detail.TmdbId.Value, cancellationToken);
            }

            if (!hasSources)
            {
                SelectedDetailTabIndex = 0;
            }

            if (isNewMovie)
            {
                SearchCandidates.Clear();
                TvSearchCandidates.Clear();
                TvSeriesCandidateGroups.Clear();
                ClearUnknownSeasonTargets();
                SelectedUnknownSeasonTarget = null;
                IsUnknownSeasonPickerDialogOpen = false;
                _correctionMediaFileId = null;
                _selectedCorrectionSource = null;
                OnPropertyChanged(nameof(SelectedCorrectionSource));
                OnPropertyChanged(nameof(IsCorrectionPanelVisible));
                SelectedDetailTabIndex = 0;
                SelectedCorrectionTarget = CorrectionTargetMovieText;
                CorrectionSourceDisplay = "请选择一个播放源。";
                CorrectionSourceFileName = string.Empty;
                CorrectionSourcePath = string.Empty;
                ClearCorrectionPreview();
                OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(HasTvSearchCandidates));
                ManualSearchQuery = detail.Title;
                ManualSearchYear = detail.ReleaseYear?.ToString() ?? string.Empty;
                TvCorrectionQuery = detail.Title;
                UnknownSeasonSearchQuery = detail.Title;
            }
            else if (_correctionMediaFileId.HasValue && Sources.All(source => source.MediaFileId != _correctionMediaFileId.Value))
            {
                _correctionMediaFileId = null;
                _selectedCorrectionSource = null;
                OnPropertyChanged(nameof(SelectedCorrectionSource));
                CorrectionSourceFileName = string.Empty;
                CorrectionSourcePath = string.Empty;
                OnPropertyChanged(nameof(IsCorrectionPanelVisible));
                ClearCorrectionPreview();
                SearchCandidates.Clear();
                TvSearchCandidates.Clear();
                TvSeriesCandidateGroups.Clear();
                ClearUnknownSeasonTargets();
                SelectedUnknownSeasonTarget = null;
                IsUnknownSeasonPickerDialogOpen = false;
                OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(HasTvSearchCandidates));
                CancelCorrectionCommand.RaiseCanExecuteChanged();
                ApplyManualMatchCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }

            var defaultSource = detail.Sources.FirstOrDefault(source => source.IsDefault);
            DefaultSourceDisplay = defaultSource is null
                ? (IsNoSourceDetail ? "当前电影没有可用播放源" : "尚未设置默认播放源")
                : $"{defaultSource.SourceTypeText} · {defaultSource.FileName} ({defaultSource.Extension})";

            StatusMessage = IsNoSourceDetail
                ? "当前电影没有可用播放源，可通过扫描、修正或添加播放源补充。"
                : detail.IdentificationStatus switch
            {
                IdentificationStatus.NeedsReview => "该影片识别置信度较低，建议在识别修正中人工确认。",
                IdentificationStatus.Failed => "该影片尚未识别成功，可使用人工修正或 AI 辅助识别。",
                _ => "详情已加载。"
            };
            IsDetailLoading = false;
            ScheduleDetailLazyProbe(detail.MovieId, detail.Sources, cancellationToken);
            if (detail.TmdbId.HasValue)
            {
                StartMovieMetadataRefresh(detail.MovieId, detail.TmdbId.Value);
            }

            if (NeedsAutoClassification(detail))
            {
                StartMovieAutoClassification(detail.MovieId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ClearMovieState($"加载影片详情失败：{DescribeException(exception)}");
        }
    }

    private void StartMovieMetadataRefresh(int movieId, int tmdbId)
    {
        if (movieId <= 0 || tmdbId <= 0 || !_backgroundRefreshingMovieMetadataIds.TryAdd(movieId, 0))
        {
            return;
        }

        _ = RefreshMovieMetadataInBackgroundAsync(movieId, tmdbId);
    }

    private async Task RefreshMovieMetadataInBackgroundAsync(
        int movieId,
        int tmdbId)
    {
        try
        {
            var refreshResult = await _movieMetadataRefreshService.RefreshMovieMetadataAsync(
                movieId,
                forceRefresh: true,
                CancellationToken.None);
            if (!refreshResult.Success || !refreshResult.HasChanges)
            {
                return;
            }

            _dataRefreshService.NotifyMetadataChanged();
            if (_navigationStateService.SelectedMovieId != movieId || _movieId != movieId || _tmdbId != tmdbId)
            {
                return;
            }

            await LoadMovieAsync(movieId, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-tmdb-metadata-refresh-cancelled movieId={movieId} tmdbId={tmdbId}");
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-tmdb-metadata-refresh-failed movieId={movieId} tmdbId={tmdbId} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
        finally
        {
            _backgroundRefreshingMovieMetadataIds.TryRemove(movieId, out _);
        }
    }

    private void ScheduleDetailLazyProbe(
        int movieId,
        IReadOnlyCollection<MovieSourceItem> sources,
        CancellationToken cancellationToken)
    {
        var mediaFileIds = sources
            .Select(source => source.MediaFileId)
            .Where(mediaFileId => _lazyProbeCheckedMediaFileIds.Add(mediaFileId))
            .ToArray();
        if (mediaFileIds.Length == 0)
        {
            return;
        }

        _ = RunDetailLazyProbeAsync(movieId, mediaFileIds, cancellationToken);
    }

    private async Task RunDetailLazyProbeAsync(
        int movieId,
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var requestedMediaFileIds = mediaFileIds
            .Where(mediaFileId => mediaFileId > 0)
            .Distinct()
            .ToArray();
        SetProbeBusyState(requestedMediaFileIds, isBusy: true);
        try
        {
            var result = await _mediaProbeService.EnqueueDetailSourcesAsync(
                requestedMediaFileIds,
                "movie",
                movieId,
                cancellationToken: cancellationToken);
            var activeProbeMediaFileIds = result.ProbeMediaFileIds.ToHashSet();
            SetProbeBusyState(
                requestedMediaFileIds.Where(mediaFileId => !activeProbeMediaFileIds.Contains(mediaFileId)),
                isBusy: false);
            if (result.QueuedCount <= 0)
            {
                return;
            }

            await MarkMovieDetailTaskStartedAsync(movieId, "lazy-probe");
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-refresh contentKind=movie queuedCount={result.QueuedCount} refreshStrategy=probe-status-changed-event");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetProbeBusyState(requestedMediaFileIds, isBusy: false);
            ScanIdentificationDiagnostics.Write(
                "event=media-probe-detail-lazy-refresh contentKind=movie skippedReason=page-cancelled");
        }
        catch (Exception exception)
        {
            SetProbeBusyState(requestedMediaFileIds, isBusy: false);
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-refresh contentKind=movie skippedReason=refresh-error error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
    }

    private bool CanManualProbeSource(object? parameter)
    {
        return parameter is MovieSourceItem source
               && IsLibraryMovie
               && _movieId.HasValue
               && Sources.Any(item => item.MediaFileId == source.MediaFileId)
               && source.MediaProbeStatus != MediaProbeStatus.Pending
               && !_probingMediaFileIds.Contains(source.MediaFileId);
    }

    private async Task ManualProbeSourceAsync(object? parameter)
    {
        if (parameter is not MovieSourceItem source)
        {
            StatusMessage = "请先选择要探测的播放源。";
            return;
        }

        if (!Sources.Any(item => item.MediaFileId == source.MediaFileId))
        {
            StatusMessage = "该播放源不属于当前影片。";
            return;
        }

        if (!_probingMediaFileIds.Add(source.MediaFileId))
        {
            return;
        }

        ManualProbeSourceCommand.RaiseCanExecuteChanged();
        StatusMessage = "正在手动探测该播放源。";
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-manual-started contentKind=movie mediaFileId={source.MediaFileId}");
        try
        {
            await _mediaProbeService.ProbeMediaFileAsync(source.MediaFileId, force: true);
            if (IsLibraryMovie && _movieId is { } movieId && _navigationStateService.SelectedMovieId == movieId)
            {
                await LoadMovieAsync(movieId, CancellationToken.None);
            }

            StatusMessage = "手动探测已完成，播放源信息已刷新。";
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-manual-completed contentKind=movie mediaFileId={source.MediaFileId}");
        }
        catch (Exception exception)
        {
            StatusMessage = $"手动探测失败：{DescribeException(exception)}";
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-manual-failed contentKind=movie mediaFileId={source.MediaFileId} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
        finally
        {
            _probingMediaFileIds.Remove(source.MediaFileId);
            ManualProbeSourceCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnProbeStatusChanged(object? sender, MediaProbeStatusChangedEventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => OnProbeStatusChanged(sender, e)));
            return;
        }

        if (!IsLibraryMovie
            || _movieId is null
            || _navigationStateService.SelectedMovieId != _movieId
            || Sources.All(source => source.MediaFileId != e.MediaFileId))
        {
            return;
        }

        UpdateProbeBusyState(e);
        QueueProbeCompletionRefresh();
    }

    private void UpdateProbeBusyState(MediaProbeStatusChangedEventArgs e)
    {
        SetProbeBusyState([e.MediaFileId], e.Status == MediaProbeStatus.Pending);
    }

    private void SyncProbeBusyStateFromSources()
    {
        var pendingMediaFileIds = Sources
            .Where(source => source.MediaProbeStatus == MediaProbeStatus.Pending)
            .Select(source => source.MediaFileId)
            .ToHashSet();
        var changed = false;

        foreach (var mediaFileId in _probingMediaFileIds.ToArray())
        {
            if (!pendingMediaFileIds.Contains(mediaFileId))
            {
                changed |= _probingMediaFileIds.Remove(mediaFileId);
            }
        }

        foreach (var mediaFileId in pendingMediaFileIds)
        {
            changed |= _probingMediaFileIds.Add(mediaFileId);
        }

        if (changed)
        {
            ManualProbeSourceCommand.RaiseCanExecuteChanged();
        }
    }

    private void SetProbeBusyState(IEnumerable<int> mediaFileIds, bool isBusy)
    {
        var changed = false;
        foreach (var mediaFileId in mediaFileIds)
        {
            if (mediaFileId <= 0)
            {
                continue;
            }

            changed |= isBusy
                ? _probingMediaFileIds.Add(mediaFileId)
                : _probingMediaFileIds.Remove(mediaFileId);
        }

        if (changed)
        {
            ManualProbeSourceCommand.RaiseCanExecuteChanged();
        }
    }

    private void QueueProbeCompletionRefresh()
    {
        if (_isProbeCompletionRefreshQueued)
        {
            return;
        }

        _isProbeCompletionRefreshQueued = true;
        _ = RefreshAfterProbeStatusChangedAsync();
    }

    private async Task RefreshAfterProbeStatusChangedAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600));
            if (!IsLibraryMovie || _movieId is not { } movieId || _navigationStateService.SelectedMovieId != movieId)
            {
                ScanIdentificationDiagnostics.Write(
                    "event=media-probe-detail-lazy-refresh contentKind=movie skippedReason=page-changed");
                return;
            }

            await LoadMovieAsync(movieId, CancellationToken.None);
            ScanIdentificationDiagnostics.Write(
                "event=media-probe-detail-lazy-refresh contentKind=movie status=completed refreshStrategy=probe-status-changed-event");
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-refresh contentKind=movie skippedReason=refresh-error error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
        finally
        {
            _isProbeCompletionRefreshQueued = false;
        }
    }

    private async Task LoadExternalRecommendationAsync(AiRecommendationItem recommendation, CancellationToken cancellationToken)
    {
        var hasCachedClassification = ApplyCachedExternalTags(recommendation);
        var shouldAutoClassify = !hasCachedClassification && NeedsExternalAutoClassification(recommendation);
        var shouldResetLibraryVisibilityTracking = !ReferenceEquals(_externalRecommendation, recommendation) || IsLibraryMovie;

        _movieId = null;
        _tmdbId = recommendation.TmdbId;
        _externalRecommendation = recommendation;
        HasMovie = true;
        IsLibraryMovie = false;
        IsNoSourceDetail = true;
        IsVisibleInLibrary = recommendation.IsVisibleInLibrary;
        CurrentLibraryVisibilityState = recommendation.LibraryVisibilityState;
        if (shouldResetLibraryVisibilityTracking)
        {
            ResetDetailAutoLibraryVisibilityTracking();
        }
        SelectedDetailTabIndex = 0;
        _correctionMediaFileId = null;
        _selectedCorrectionSource = null;
        OnPropertyChanged(nameof(SelectedCorrectionSource));
        CorrectionSourceFileName = string.Empty;
        CorrectionSourcePath = string.Empty;
        ClearCorrectionPreview();
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        _identificationStatus = IdentificationStatus.Pending;
        OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
        OnPropertyChanged(nameof(IsUnidentifiedMovie));
        IsFavorite = false;
        IsWatched = recommendation.IsWatched;
        IsWantToWatch = recommendation.IsWantToWatch;
        IsNotInterested = recommendation.IsNotInterested;
        CanPlay = false;
        AvailabilityText = "暂无播放源";
        PlayButtonText = "暂无可播放源";
        TitleText = recommendation.Title;
        OriginalTitle = string.IsNullOrWhiteSpace(recommendation.OriginalTitle) ? "-" : recommendation.OriginalTitle;
        ReleaseYearText = recommendation.ReleaseYear?.ToString() ?? "-";
        ReleaseDateText = recommendation.ReleaseDate.HasValue
            ? recommendation.ReleaseDate.Value.ToString("yyyy-MM-dd")
            : recommendation.ReleaseYear?.ToString() ?? "-";
        Overview = string.IsNullOrWhiteSpace(recommendation.Overview) ? recommendation.Reason : recommendation.Overview;
        PosterRemoteUrl = recommendation.PosterRemoteUrl;
        PosterDisplayUrl = recommendation.PosterRemoteUrl;
        Country = MovieMetadataDisplayText.LocalizeCountries(recommendation.Country);
        Language = MovieMetadataDisplayText.LocalizeLanguages(recommendation.Language);
        DirectorText = string.IsNullOrWhiteSpace(recommendation.DirectorText) ? "-" : recommendation.DirectorText;
        WriterText = "-";
        ActorsText = string.IsNullOrWhiteSpace(recommendation.ActorsText) ? "-" : recommendation.ActorsText;
        ProductionCompanyText = "-";
        RuntimeText = FormatRuntimeMinutes(recommendation.RuntimeMinutes);
        if (shouldAutoClassify)
        {
            ShowExternalAiAnalyzingState(recommendation);
        }
        else
        {
            ApplyExternalTagDisplay(recommendation, ExternalAiMissingText);
        }
        IdentificationStatusText = "无播放源";
        ConfidenceText = "-";
        TmdbIdText = recommendation.TmdbId?.ToString() ?? "-";
        ImdbIdText = string.IsNullOrWhiteSpace(recommendation.ImdbId) ? "-" : recommendation.ImdbId;
        DefaultSourceDisplay = "当前电影没有可用播放源";
        StatusMessage = shouldAutoClassify
            ? "当前电影没有可用播放源，AI 正在分析影片标签。"
            : IsVisibleInLibrary
                ? "当前电影没有可用播放源。"
                : "当前电影没有可用播放源，可加入媒体库后保留详情记录。";
        ManualSearchQuery = recommendation.Title;
        ManualSearchYear = recommendation.ReleaseYear?.ToString() ?? string.Empty;

        var recommendationRatings = new List<MovieRatingItem>();
        if (recommendation.TmdbRating.HasValue)
        {
            recommendationRatings.Add(new MovieRatingItem
            {
                SourceName = "TMDB",
                ScoreValue = recommendation.TmdbRating.Value,
                ScoreScale = 10d,
                VoteCount = recommendation.TmdbVoteCount,
                SourceUrl = recommendation.TmdbId.HasValue ? $"https://www.themoviedb.org/movie/{recommendation.TmdbId.Value}" : string.Empty,
                LastUpdatedAt = DateTime.UtcNow
            });
        }

        if (recommendation.OmdbRating is not null)
        {
            recommendationRatings.Add(ToDisplayRating(recommendation.OmdbRating));
        }

        Ratings.Clear();
        foreach (var rating in NormalizeDetailRatings(recommendationRatings))
        {
            Ratings.Add(rating);
        }
        NotifyRatingStateChanged();
        if (recommendation.TmdbId.HasValue)
        {
            _ = HydrateMovieCreditsForDisplayAsync(null, recommendation.TmdbId.Value, cancellationToken);
        }

        Sources.Clear();
        NotifySourceStateChanged();
        SearchCandidates.Clear();
        OnPropertyChanged(nameof(HasSearchCandidates));
        await RefreshExternalCollectionStateAsync(cancellationToken);
        RefreshWantToWatchCommandState();
        RefreshNotInterestedCommandState();
        RefreshWatchedCommandState();
        IsDetailLoading = false;
        if (shouldAutoClassify)
        {
            StartExternalAutoClassification(recommendation);
        }
    }

    private void StartMovieAutoClassification(int movieId)
    {
        if (!_backgroundClassifyingMovieIds.TryAdd(movieId, 0))
        {
            return;
        }

        _ = MarkMovieDetailTaskStartedAsync(movieId, "ai-classification");
        _ = ClassifyMovieInBackgroundAsync(movieId);
    }

    private async Task ClassifyMovieInBackgroundAsync(int movieId)
    {
        try
        {
            await _aiClassificationService.ClassifyMovieAsync(movieId, CancellationToken.None);
            var classified = await _movieDetailQueryService.GetMovieDetailAsync(movieId, CancellationToken.None);
            await DispatchToUiAsync(
                () =>
                {
                    if (_externalRecommendation is null && _movieId == movieId && classified is not null)
                    {
                        ApplyClassifiedLocalTagsToCurrentDetail(classified);
                        StatusMessage = "详情已加载，AI 分类已自动更新。";
                    }

                    _dataRefreshService.NotifyMetadataChanged();
                });
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-ai-classification-background-complete contentKind=movie movieId={movieId} status=completed");
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-ai-classification-background-failed contentKind=movie movieId={movieId} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
        finally
        {
            _backgroundClassifyingMovieIds.TryRemove(movieId, out _);
        }
    }

    private void StartExternalAutoClassification(AiRecommendationItem recommendation)
    {
        var key = BuildExternalClassificationKey(recommendation);
        if (!_backgroundClassifyingExternalKeys.TryAdd(key, 0))
        {
            return;
        }

        _ = MarkExternalDetailTaskStartedAsync(recommendation, "external-ai-classification");
        _ = ClassifyExternalRecommendationInBackgroundAsync(recommendation, key);
    }

    private async Task MarkMovieDetailTaskStartedAsync(int movieId, string taskKind)
    {
        try
        {
            if (await _movieManagementService.TouchMovieUpdatedAtAsync(movieId, CancellationToken.None))
            {
                _dataRefreshService.NotifyMetadataChanged();
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-detail-task-start-touch contentKind=movie movieId={movieId} taskKind={ScanIdentificationDiagnostics.FormatValue(taskKind)}");
            }
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-task-start-touch-failed contentKind=movie movieId={movieId} taskKind={ScanIdentificationDiagnostics.FormatValue(taskKind)} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
    }

    private async Task MarkExternalDetailTaskStartedAsync(AiRecommendationItem recommendation, string taskKind)
    {
        try
        {
            if (await _userCollectionService.TouchCollectionItemUpdatedAtAsync(recommendation, CancellationToken.None))
            {
                _dataRefreshService.NotifyMetadataChanged();
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-detail-task-start-touch contentKind=external tmdbId={FormatNullable(recommendation.TmdbId)} taskKind={ScanIdentificationDiagnostics.FormatValue(taskKind)}");
            }
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-task-start-touch-failed contentKind=external tmdbId={FormatNullable(recommendation.TmdbId)} taskKind={ScanIdentificationDiagnostics.FormatValue(taskKind)} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
    }

    private async Task ClassifyExternalRecommendationInBackgroundAsync(
        AiRecommendationItem recommendation,
        string classificationKey)
    {
        try
        {
            await DispatchToUiAsync(
                () =>
                {
                    if (IsCurrentExternalRecommendation(classificationKey))
                    {
                        ShowExternalAiAnalyzingState(recommendation);
                        StatusMessage = "正在为无播放源候选生成 AI 标签。";
                    }
                });

            var tags = await _aiClassificationService.ClassifyExternalMovieAsync(recommendation, CancellationToken.None);
            ApplyExternalClassificationResult(recommendation, tags);
            CacheExternalTags(recommendation);
            await DispatchToUiAsync(
                () =>
                {
                    if (IsCurrentExternalRecommendation(classificationKey))
                    {
                        ApplyExternalTagDisplay(recommendation, ExternalAiMissingText);
                        StatusMessage = "无播放源候选 AI 标签已自动生成。";
                    }

                    _dataRefreshService.NotifyMetadataChanged();
                });
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-ai-classification-background-complete contentKind=external tmdbId={FormatNullable(recommendation.TmdbId)} status=completed");
        }
        catch (Exception exception)
        {
            await DispatchToUiAsync(
                () =>
                {
                    if (IsCurrentExternalRecommendation(classificationKey))
                    {
                        ApplyExternalTagDisplay(recommendation, ExternalAiMissingText);
                        StatusMessage = $"无播放源候选 AI 标签生成失败：{DescribeException(exception)}";
                    }
                });
            ScanIdentificationDiagnostics.Write(
                $"event=movie-detail-ai-classification-background-failed contentKind=external tmdbId={FormatNullable(recommendation.TmdbId)} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
        finally
        {
            _backgroundClassifyingExternalKeys.TryRemove(classificationKey, out _);
        }
    }

    private async Task OpenPlayerAsync(object? parameter)
    {
        if (!IsLibraryMovie)
        {
            StatusMessage = "当前电影没有可用播放源，无法播放。";
            return;
        }

        if (!HasMovie || _movieId is null)
        {
            StatusMessage = "请先选择影片。";
            return;
        }

        if (!CanPlay)
        {
            StatusMessage = "当前电影没有可用播放源，无法播放。";
            return;
        }

        if (!CanOpenPlayer)
        {
            return;
        }

        SetOpeningPlayer(true);
        try
        {
            var mediaFileId = parameter is MovieSourceItem source ? source.MediaFileId : (int?)null;
            await _playerWindowService.OpenAsync(_movieId.Value, mediaFileId);
        }
        catch (Exception exception)
        {
            StatusMessage = $"播放器打开失败：{DescribeException(exception)}";
        }
        finally
        {
            SetOpeningPlayer(false);
        }
    }

    private void SetOpeningPlayer(bool value)
    {
        if (_isOpeningPlayer == value)
        {
            return;
        }

        _isOpeningPlayer = value;
        RefreshOpenPlayerCommandState();
    }

    private void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        RefreshOpenPlayerCommandState();
        _ = RefreshCurrentMovieAfterPlayerClosedAsync();
    }

    private async Task RefreshCurrentMovieAfterPlayerClosedAsync()
    {
        if (!IsLibraryMovie || _movieId is not { } movieId)
        {
            return;
        }

        if (_navigationStateService.SelectedMovieId != movieId)
        {
            return;
        }

        try
        {
            await LoadMovieAsync(movieId, CancellationToken.None);
        }
        catch
        {
            // Playback-close refresh is best-effort; the page can still be refreshed manually.
        }
    }

    private void RefreshOpenPlayerCommandState()
    {
        OnPropertyChanged(nameof(CanOpenPlayer));
        OpenPlayerCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshResetSourceRecognitionCommandState()
    {
        OnPropertyChanged(nameof(CanResetSourcesToUnidentified));
        ResetSourceRecognitionCommand?.RaiseCanExecuteChanged();
    }

    private void NotifyRatingStateChanged()
    {
        OnPropertyChanged(nameof(HasRatings));
    }

    private void NotifySourceStateChanged()
    {
        OnPropertyChanged(nameof(HasSources));
        OnPropertyChanged(nameof(HasNoSources));
        OnPropertyChanged(nameof(CanUseIdentificationCorrection));
        OpenCorrectionDialogCommand?.RaiseCanExecuteChanged();
        AiSuggestSearchCommand?.RaiseCanExecuteChanged();
        SearchUnknownSeasonTargetsCommand?.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshWantToWatchCommandState()
    {
        OnPropertyChanged(nameof(CanToggleWantToWatch));
        ToggleWantToWatchCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshPreferenceCommandState()
    {
        OnPropertyChanged(nameof(CanTogglePreference));
        TogglePreferenceCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshNotInterestedCommandState()
    {
        OnPropertyChanged(nameof(CanToggleNotInterested));
        ToggleNotInterestedCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshWatchedCommandState()
    {
        OnPropertyChanged(nameof(CanToggleWatched));
        ToggleWatchedCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshAddToLibraryCommandState()
    {
        OnPropertyChanged(nameof(CanAddToLibrary));
        AddToLibraryCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshAddToLibraryButtonState()
    {
        OnPropertyChanged(nameof(ShowAddToLibraryAction));
        OnPropertyChanged(nameof(AddToLibraryButtonText));
        OnPropertyChanged(nameof(AddToLibraryButtonIcon));
        RefreshAddToLibraryCommandState();
    }

    private void NotifyRecommendationChangedIfCurrentMovieAffectsAiRecommendation()
    {
        var hasReliableLibraryIdentity = IsLibraryMovie
                                         && _tmdbId is > 0
                                         && _identificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed;
        var hasReliableExternalIdentity = !IsLibraryMovie
                                          && _externalRecommendation?.TmdbId is > 0;
        if (hasReliableLibraryIdentity || hasReliableExternalIdentity)
        {
            _dataRefreshService.NotifyRecommendationChanged();
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (IsStatePersistencePending)
        {
            return;
        }

        var favoriteMovieId = CurrentFavoriteMovieId;
        if (!favoriteMovieId.HasValue && _externalRecommendation is null)
        {
            StatusMessage = "当前无播放源影片暂不支持在此详情页标记喜爱。";
            return;
        }

        var previousFavorite = IsFavorite;
        var previousNotInterested = IsNotInterested;
        var targetFavorite = !IsFavorite;
        if (targetFavorite && !IsWatched)
        {
            StatusMessage = "只有已看影片可以标记喜爱。";
            return;
        }

        SetTogglingFavorite(true);
        try
        {
            IsFavorite = targetFavorite;
            if (IsFavorite)
            {
                IsNotInterested = false;
            }

            await Dispatcher.Yield(DispatcherPriority.Background);
            if (favoriteMovieId.HasValue)
            {
                await PersistStateInBackgroundAsync(
                    () => _movieManagementService.SetFavoriteAsync(favoriteMovieId.Value, targetFavorite));
            }
            else
            {
                _externalRecommendation!.IsFavorite = targetFavorite;
                _externalRecommendation.IsWatched = IsWatched;
                _externalRecommendation.IsNotInterested = IsNotInterested;
                await PersistStateInBackgroundAsync(
                    () => _userCollectionService.SetFavoriteAsync(
                        _externalRecommendation,
                        targetFavorite,
                        changeSource: "MovieDetail"));
            }

            if (_externalRecommendation is not null)
            {
                await RefreshExternalCollectionStateAsync(CancellationToken.None);
            }
            StatusMessage = IsFavorite ? "已标记为喜爱。" : "已取消喜爱。";
            QueueStateRefresh(collectionChanged: true, recommendationChanged: true);
        }
        catch (Exception exception)
        {
            IsFavorite = previousFavorite;
            IsNotInterested = previousNotInterested;
            if (_externalRecommendation is not null)
            {
                _externalRecommendation.IsFavorite = previousFavorite;
                _externalRecommendation.IsNotInterested = previousNotInterested;
            }
            StatusMessage = $"喜爱状态更新失败：{DescribeException(exception)}";
        }
        finally
        {
            SetTogglingFavorite(false);
        }
    }

    private async Task TogglePreferenceAsync()
    {
        if (IsWatched)
        {
            await ToggleFavoriteAsync();
            return;
        }

        await ToggleWantToWatchAsync();
    }

    private async Task AddToLibraryAsync()
    {
        if (!HasMovie)
        {
            return;
        }

        _isAddingToLibrary = true;
        RefreshAddToLibraryCommandState();
        try
        {
            if (IsLibraryMovie && _movieId.HasValue)
            {
                if (IsVisibleInLibrary)
                {
                    await _movieManagementService.RemoveFromLibraryAsync(_movieId.Value);
                }
                else if (_libraryVisibilityState == LibraryVisibilityState.Hidden)
                {
                    await _movieManagementService.RestoreToLibraryAsync(_movieId.Value);
                }
                else
                {
                    await _movieManagementService.AddToLibraryAsync(_movieId.Value);
                }
                _dataRefreshService.NotifyLibraryChanged();
                _dataRefreshService.NotifyCollectionChanged();
                await LoadMovieAsync(_movieId.Value, CancellationToken.None);
                ResetDetailAutoLibraryVisibilityTracking();
                StatusMessage = IsVisibleInLibrary ? "已加入媒体库。" : "已移出媒体库。";
                return;
            }

            if (_externalRecommendation is null)
            {
                StatusMessage = "缺少外部影片信息，无法加入媒体库。";
                return;
            }

            if (IsVisibleInLibrary)
            {
                await _userCollectionService.HideFromLibraryAsync(_externalRecommendation, changeSource: "MovieDetail");
                IsVisibleInLibrary = false;
                CurrentLibraryVisibilityState = LibraryVisibilityState.Hidden;
            }
            else if (_libraryVisibilityState == LibraryVisibilityState.Hidden)
            {
                await _userCollectionService.RestoreToLibraryAsync(_externalRecommendation, changeSource: "MovieDetail");
                IsVisibleInLibrary = true;
            }
            else
            {
                await _userCollectionService.AddToLibraryAsync(_externalRecommendation, changeSource: "MovieDetail");
                IsVisibleInLibrary = true;
            }
            CurrentLibraryVisibilityState = IsVisibleInLibrary
                && _libraryVisibilityState == LibraryVisibilityState.Hidden
                                     && (IsWatched || IsWantToWatch || IsNotInterested)
                ? LibraryVisibilityState.Auto
                : IsVisibleInLibrary ? LibraryVisibilityState.Visible : LibraryVisibilityState.Hidden;
            _externalRecommendation.IsVisibleInLibrary = IsVisibleInLibrary;
            _externalRecommendation.LibraryVisibilityState = CurrentLibraryVisibilityState;
            ResetDetailAutoLibraryVisibilityTracking();
            AvailabilityText = "暂无播放源";
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            StatusMessage = IsVisibleInLibrary ? "已加入媒体库。" : "已移出媒体库。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"加入媒体库失败：{DescribeException(exception)}";
        }
        finally
        {
            _isAddingToLibrary = false;
            RefreshAddToLibraryCommandState();
        }
    }

    private async Task ToggleWatchedAsync()
    {
        if (IsStatePersistencePending)
        {
            return;
        }

        if (IsLibraryMovie)
        {
            await ToggleLibraryWatchedAsync();
            return;
        }

        await ToggleExternalWatchedAsync();
    }

    private async Task ToggleLibraryWatchedAsync()
    {
        if (_movieId is null)
        {
            StatusMessage = "请先选择影片。";
            return;
        }

        var previousWatched = IsWatched;
        var previousFavorite = IsFavorite;
        var previousWantToWatch = IsWantToWatch;
        var targetWatched = !previousWatched;
        var wasVisibleBeforeToggle = IsVisibleInLibrary;
        SetTogglingWatched(true);
        try
        {
            IsWatched = targetWatched;
            if (targetWatched)
            {
                IsWantToWatch = false;
            }
            else
            {
                IsFavorite = false;
            }

            await Dispatcher.Yield(DispatcherPriority.Background);
            await PersistStateInBackgroundAsync(
                () => _movieManagementService.SetWatchedAsync(_movieId.Value, targetWatched));
            await RestoreLibraryVisibilityIfNeededAfterStateRemovalAsync(
                wasVisibleBeforeToggle,
                targetWatched,
                CancellationToken.None);
            TrackDetailStateLibraryVisibility(wasVisibleBeforeToggle, targetWatched);
            StatusMessage = IsWatched ? "已标记为已看。" : "已标记为未看。";
            QueueStateRefresh(metadataChanged: true, collectionChanged: true, recommendationChanged: true);
        }
        catch (Exception exception)
        {
            IsWatched = previousWatched;
            IsFavorite = previousFavorite;
            IsWantToWatch = previousWantToWatch;
            StatusMessage = $"观看状态更新失败：{DescribeException(exception)}";
        }
        finally
        {
            SetTogglingWatched(false);
        }
    }

    private async Task ToggleExternalWatchedAsync()
    {
        if (_externalRecommendation is null)
        {
            StatusMessage = "当前无播放源影片缺少可保存的候选记录，无法切换观看状态。";
            return;
        }

        var previousWatched = IsWatched;
        var previousFavorite = IsFavorite;
        var previousWantToWatch = IsWantToWatch;
        var targetWatched = !previousWatched;
        var wasVisibleBeforeToggle = IsVisibleInLibrary;
        SetTogglingWatched(true);
        try
        {
            IsWatched = targetWatched;
            _externalRecommendation.IsWatched = targetWatched;
            if (targetWatched)
            {
                IsWantToWatch = false;
                _externalRecommendation.IsWantToWatch = false;
            }
            else
            {
                IsFavorite = false;
                _externalRecommendation.IsFavorite = false;
            }

            await Dispatcher.Yield(DispatcherPriority.Background);
            await PersistStateInBackgroundAsync(
                () => _userCollectionService.SetWatchedAsync(_externalRecommendation, targetWatched));
            await RestoreLibraryVisibilityIfNeededAfterStateRemovalAsync(
                wasVisibleBeforeToggle,
                targetWatched,
                CancellationToken.None);
            await RefreshExternalCollectionStateAsync(CancellationToken.None);
            TrackDetailStateLibraryVisibility(wasVisibleBeforeToggle, targetWatched);
            StatusMessage = targetWatched ? "已标记为已看。" : "已标记为未看。";
            QueueStateRefresh(collectionChanged: true, recommendationChanged: true);
        }
        catch (Exception exception)
        {
            IsWatched = previousWatched;
            IsFavorite = previousFavorite;
            IsWantToWatch = previousWantToWatch;
            _externalRecommendation.IsWatched = previousWatched;
            _externalRecommendation.IsFavorite = previousFavorite;
            _externalRecommendation.IsWantToWatch = previousWantToWatch;
            StatusMessage = $"观看状态更新失败：{DescribeException(exception)}";
        }
        finally
        {
            SetTogglingWatched(false);
        }
    }

    private async Task ToggleWantToWatchAsync()
    {
        if (IsStatePersistencePending)
        {
            return;
        }

        if (IsWatched)
        {
            StatusMessage = "已看影片请使用喜爱状态。";
            return;
        }

        if ((IsLibraryMovie && !_movieId.HasValue)
            || (!IsLibraryMovie && _externalRecommendation is null))
        {
            StatusMessage = "当前影片缺少稳定记录，无法更新想看状态。";
            return;
        }

        var previousState = IsWantToWatch;
        var previousNotInterested = IsNotInterested;
        var targetWantToWatch = !previousState;
        var wasVisibleBeforeToggle = IsVisibleInLibrary;
        SetTogglingWantToWatch(true);
        try
        {
            IsWantToWatch = targetWantToWatch;
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (IsLibraryMovie)
            {
                await PersistStateInBackgroundAsync(
                    () => _userCollectionService.SetWantToWatchAsync(_movieId!.Value, IsWantToWatch));
            }
            else
            {
                _externalRecommendation!.IsWantToWatch = IsWantToWatch;
                if (IsWantToWatch)
                {
                    await PersistStateInBackgroundAsync(
                        () => _userCollectionService.AddWantToWatchAsync(_externalRecommendation));
                }
                else
                {
                    await PersistStateInBackgroundAsync(
                        () => _userCollectionService.RemoveWantToWatchAsync(_externalRecommendation));
                }
            }

            if (IsWantToWatch)
            {
                IsNotInterested = false;
                if (_externalRecommendation is not null)
                {
                    _externalRecommendation.IsNotInterested = false;
                }
            }

            await RestoreLibraryVisibilityIfNeededAfterStateRemovalAsync(
                wasVisibleBeforeToggle,
                targetWantToWatch,
                CancellationToken.None);
            if (_externalRecommendation is not null)
            {
                await RefreshExternalCollectionStateAsync(CancellationToken.None);
            }
            TrackDetailStateLibraryVisibility(wasVisibleBeforeToggle, targetWantToWatch);
            StatusMessage = IsWantToWatch ? "已加入想看。" : "已取消想看。";
            QueueStateRefresh(collectionChanged: true, recommendationChanged: true);
        }
        catch (Exception exception)
        {
            IsWantToWatch = previousState;
            IsNotInterested = previousNotInterested;
            if (_externalRecommendation is not null)
            {
                _externalRecommendation.IsWantToWatch = previousState;
                _externalRecommendation.IsNotInterested = previousNotInterested;
            }
            StatusMessage = $"想看状态更新失败：{DescribeException(exception)}";
        }
        finally
        {
            SetTogglingWantToWatch(false);
        }
    }

    private async Task ToggleNotInterestedAsync()
    {
        if (IsStatePersistencePending)
        {
            return;
        }

        if (!HasMovie)
        {
            StatusMessage = "请先选择影片。";
            return;
        }

        if ((IsLibraryMovie && !_movieId.HasValue)
            || (!IsLibraryMovie && _externalRecommendation is null))
        {
            StatusMessage = "当前影片缺少稳定记录，无法更新不想看状态。";
            return;
        }

        var previousNotInterested = IsNotInterested;
        var previousWantToWatch = IsWantToWatch;
        var previousFavorite = IsFavorite;
        var targetNotInterested = !previousNotInterested;
        var wasVisibleBeforeToggle = IsVisibleInLibrary;
        SetTogglingNotInterested(true);
        try
        {
            IsNotInterested = targetNotInterested;
            if (targetNotInterested)
            {
                IsWantToWatch = false;
                IsFavorite = false;
                if (_externalRecommendation is not null)
                {
                    _externalRecommendation.IsWantToWatch = false;
                }
            }

            await Dispatcher.Yield(DispatcherPriority.Background);
            if (IsLibraryMovie)
            {
                await PersistStateInBackgroundAsync(
                    () => _userCollectionService.SetNotInterestedAsync(_movieId!.Value, targetNotInterested));
            }
            else
            {
                var recommendation = _externalRecommendation!;
                await PersistStateInBackgroundAsync(
                    () => _userCollectionService.SetNotInterestedAsync(recommendation, targetNotInterested));
                recommendation.IsNotInterested = targetNotInterested;
            }

            await RestoreLibraryVisibilityIfNeededAfterStateRemovalAsync(
                wasVisibleBeforeToggle,
                targetNotInterested,
                CancellationToken.None);
            if (_externalRecommendation is not null)
            {
                await RefreshExternalCollectionStateAsync(CancellationToken.None);
            }
            TrackDetailStateLibraryVisibility(wasVisibleBeforeToggle, targetNotInterested);
            StatusMessage = targetNotInterested ? "已标记为不想看。" : "已取消不想看。";
            QueueStateRefresh(collectionChanged: true, forceRecommendationChanged: true);
        }
        catch (Exception exception)
        {
            IsNotInterested = previousNotInterested;
            IsWantToWatch = previousWantToWatch;
            IsFavorite = previousFavorite;
            if (_externalRecommendation is not null)
            {
                _externalRecommendation.IsNotInterested = previousNotInterested;
                _externalRecommendation.IsWantToWatch = previousWantToWatch;
            }

            StatusMessage = $"不想看状态更新失败：{DescribeException(exception)}";
        }
        finally
        {
            SetTogglingNotInterested(false);
        }
    }

    private async Task RefreshExternalCollectionStateAsync(CancellationToken cancellationToken)
    {
        if (_externalRecommendation is null)
        {
            IsWantToWatch = false;
            IsWatched = false;
            IsNotInterested = false;
            IsVisibleInLibrary = false;
            CurrentLibraryVisibilityState = LibraryVisibilityState.Auto;
            return;
        }

        try
        {
            if (_externalRecommendation.TmdbId is int tmdbId && tmdbId > 0)
            {
                var statuses = await _discoveryMovieStatusResolver.ResolveAsync(new[] { tmdbId }, cancellationToken);
                if (statuses.TryGetValue(tmdbId, out var status))
                {
                    ApplyExternalMovieStatus(status);
                    return;
                }
            }

            var collectionItems = await _userCollectionService.GetCollectionItemsAsync(cancellationToken);
            var isNotInterested = await _userCollectionService.IsNotInterestedAsync(_externalRecommendation, cancellationToken);
            var collectionItem = collectionItems.FirstOrDefault(x => IsSameRecommendation(x, _externalRecommendation));
            var isWatched = collectionItem is null ? _externalRecommendation.IsWatched : collectionItem.IsWatched;
            IsFavorite = collectionItem?.IsLiked == true;
            var isWantToWatch = collectionItem is null
                ? _externalRecommendation.IsWantToWatch && !isWatched && !isNotInterested
                : collectionItem.IsWantToWatch && !isWatched;
            var hasCurrentState = isWatched || isWantToWatch || isNotInterested;
            ApplyExternalCollectionState(
                isWatched,
                isWantToWatch,
                isNotInterested,
                ResolveIsVisibleInLibrary(
                    _externalRecommendation.IsInLibrary,
                    _externalRecommendation.LibraryVisibilityState,
                    hasCurrentState),
                _externalRecommendation.LibraryVisibilityState,
                _externalRecommendation.IsInLibrary);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            var hasCurrentState = _externalRecommendation.IsWatched
                                  || _externalRecommendation.IsWantToWatch
                                  || _externalRecommendation.IsNotInterested;
            ApplyExternalCollectionState(
                _externalRecommendation.IsWatched,
                _externalRecommendation.IsWantToWatch && !_externalRecommendation.IsWatched,
                _externalRecommendation.IsNotInterested,
                ResolveIsVisibleInLibrary(
                    _externalRecommendation.IsInLibrary,
                    _externalRecommendation.LibraryVisibilityState,
                    hasCurrentState),
                _externalRecommendation.LibraryVisibilityState,
                _externalRecommendation.IsInLibrary);
        }
    }

    private void ApplyExternalMovieStatus(DiscoveryMovieStatus status)
    {
        if (status.MovieId is > 0)
        {
            _externalRecommendation!.MovieId = status.MovieId.Value;
        }

        IsFavorite = status.IsFavorite;
        ApplyExternalCollectionState(
            status.IsWatched,
            status.IsWantToWatch && !status.IsWatched,
            status.IsNotInterested,
            status.IsVisibleInLibrary,
            status.LibraryVisibilityState,
            status.IsInLibrary);
    }

    private void ApplyExternalCollectionState(
        bool isWatched,
        bool isWantToWatch,
        bool isNotInterested,
        bool isVisibleInLibrary,
        LibraryVisibilityState libraryVisibilityState,
        bool isInLibrary)
    {
        IsWatched = isWatched;
        IsWantToWatch = isWantToWatch && !isWatched && !isNotInterested;
        IsNotInterested = isNotInterested;
        IsVisibleInLibrary = isVisibleInLibrary;
        CurrentLibraryVisibilityState = libraryVisibilityState;
        _externalRecommendation!.IsWatched = IsWatched;
        _externalRecommendation.IsFavorite = IsFavorite;
        _externalRecommendation.IsWantToWatch = IsWantToWatch;
        _externalRecommendation.IsNotInterested = IsNotInterested;
        _externalRecommendation.IsVisibleInLibrary = IsVisibleInLibrary;
        _externalRecommendation.LibraryVisibilityState = CurrentLibraryVisibilityState;
        _externalRecommendation.IsInLibrary = isInLibrary;
    }

    private static bool ResolveIsVisibleInLibrary(
        bool hasActiveSource,
        LibraryVisibilityState visibilityState,
        bool hasCurrentState)
    {
        return visibilityState switch
        {
            LibraryVisibilityState.Hidden => false,
            LibraryVisibilityState.Visible => true,
            _ => hasActiveSource || hasCurrentState
        };
    }

    private void ResetDetailAutoLibraryVisibilityTracking()
    {
        _autoVisibleInLibraryFromCurrentDetailState = false;
    }

    private void TrackDetailStateLibraryVisibility(bool wasVisibleBeforeToggle, bool targetState)
    {
        if (targetState && !wasVisibleBeforeToggle && IsVisibleInLibrary)
        {
            _autoVisibleInLibraryFromCurrentDetailState = true;
            return;
        }

        if (!targetState && _autoVisibleInLibraryFromCurrentDetailState && !IsVisibleInLibrary)
        {
            _autoVisibleInLibraryFromCurrentDetailState = false;
        }
    }

    private async Task RestoreLibraryVisibilityIfNeededAfterStateRemovalAsync(
        bool wasVisibleBeforeToggle,
        bool targetState,
        CancellationToken cancellationToken)
    {
        if (targetState || !wasVisibleBeforeToggle || _autoVisibleInLibraryFromCurrentDetailState)
        {
            return;
        }

        if (IsLibraryMovie && _movieId is { } movieId)
        {
            await _movieManagementService.RestoreToLibraryAsync(movieId, cancellationToken);
            return;
        }

        if (_externalRecommendation is not null)
        {
            await _userCollectionService.RestoreToLibraryAsync(
                _externalRecommendation,
                cancellationToken,
                "MovieDetailStateCancelPreserveLibrary");
        }
    }

    private void SetTogglingWantToWatch(bool value)
    {
        if (_isTogglingWantToWatch == value)
        {
            return;
        }

        _isTogglingWantToWatch = value;
    }

    private bool IsStatePersistencePending =>
        _isTogglingFavorite
        || _isTogglingWatched
        || _isTogglingWantToWatch
        || _isTogglingNotInterested;

    private void SetTogglingFavorite(bool value)
    {
        if (_isTogglingFavorite == value)
        {
            return;
        }

        _isTogglingFavorite = value;
    }

    private void SetTogglingNotInterested(bool value)
    {
        if (_isTogglingNotInterested == value)
        {
            return;
        }

        _isTogglingNotInterested = value;
    }

    private void SetTogglingWatched(bool value)
    {
        if (_isTogglingWatched == value)
        {
            return;
        }

        _isTogglingWatched = value;
    }

    private void QueueStateRefresh(
        bool metadataChanged = false,
        bool collectionChanged = false,
        bool recommendationChanged = false,
        bool forceRecommendationChanged = false)
    {
        void Notify()
        {
            if (metadataChanged)
            {
                _dataRefreshService.NotifyMetadataChanged();
            }

            if (collectionChanged)
            {
                _dataRefreshService.NotifyCollectionChanged();
            }

            if (forceRecommendationChanged)
            {
                _dataRefreshService.NotifyRecommendationChanged();
            }
            else if (recommendationChanged)
            {
                NotifyRecommendationChangedIfCurrentMovieAffectsAiRecommendation();
            }
        }

        _ = Task.Run(Notify);
    }

    private static Task PersistStateInBackgroundAsync(Func<Task> persistenceAction)
    {
        return Task.Run(persistenceAction);
    }

    private async Task AiSuggestSearchAsync()
    {
        if (!CanAiSuggestSearch())
        {
            StatusMessage = "请先在播放源列表中选择要修正的单个播放源。";
            return;
        }

        var targetKind = IsCorrectionTargetTvEpisode ? "tv-episode" : "movie";
        var mediaFileId = _correctionMediaFileId;
        CancelCorrectionAiRequest();
        var cancellation = new CancellationTokenSource();
        _correctionAiCancellation = cancellation;
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-started page=movie targetKind={targetKind} mediaFileId={mediaFileId}");

        try
        {
            IsCorrectionBusy = true;
            StatusMessage = IsCorrectionTargetTvEpisode
                ? "正在请求 AI 辅助识别电视剧集，请稍候。"
                : "正在请求 AI 辅助识别电影，请稍候。";
            await Task.Yield();
            if (IsCorrectionTargetTvEpisode)
            {
                await AiSuggestTvSearchAsync(cancellation.Token);
            }
            else
            {
                await AiSuggestMovieSearchAsync(cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_correctionAiCancellation, cancellation) && IsCorrectionPanelVisible)
            {
                StatusMessage = "AI 辅助搜索已取消。";
            }
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-failed page=movie targetKind={targetKind} mediaFileId={mediaFileId} reason=cancelled");
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(_correctionAiCancellation, cancellation))
            {
                StatusMessage = $"AI 辅助搜索失败：{DescribeException(exception)}";
            }
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-failed page=movie targetKind={targetKind} mediaFileId={mediaFileId} reason=exception");
        }
        finally
        {
            var isCurrentRequest = ReferenceEquals(_correctionAiCancellation, cancellation);
            if (isCurrentRequest)
            {
                _correctionAiCancellation = null;
            }

            cancellation.Dispose();
            if (isCurrentRequest)
            {
                IsCorrectionBusy = false;
            }
        }
    }

    private async Task AiSuggestMovieSearchAsync(CancellationToken cancellationToken)
    {
        var releaseYear = int.TryParse(ManualSearchYear, out var parsedYear) ? parsedYear : (int?)null;
        var suggestionResult = await _aiClassificationService.SuggestMovieCorrectionSearchQueryAsync(
            TitleText,
            _correctionSourceFileName,
            releaseYear,
            Overview,
            _correctionSourcePath,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (suggestionResult.Status != AiSearchSuggestionStatus.Success)
        {
            StatusMessage = string.IsNullOrWhiteSpace(suggestionResult.Message)
                ? "AI 未返回可用电影搜索词，请手动输入后搜索。"
                : $"AI 未返回可用电影搜索词：{suggestionResult.Message}";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-skipped page=movie targetKind=movie mediaFileId={_correctionMediaFileId} reason={FormatAiSuggestionStatus(suggestionResult.Status)} message={ScanIdentificationDiagnostics.FormatValue(suggestionResult.Message, 180)}");
            return;
        }

        var suggestion = suggestionResult.Suggestion;

        ManualSearchQuery = suggestion.Query;
        ManualSearchYear = suggestion.ReleaseYear?.ToString() ?? string.Empty;
        StatusMessage = FormatAiSearchSuggestionStatus("电影", suggestion);
        await SearchCandidatesCoreAsync(cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-succeeded page=movie targetKind=movie mediaFileId={_correctionMediaFileId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={SearchCandidates.Count}");
    }

    private async Task AiSuggestTvSearchAsync(CancellationToken cancellationToken)
    {
        var suggestionResult = await _aiClassificationService.SuggestTvEpisodeCorrectionSearchQueryAsync(
            TitleText,
            _correctionSourceFileName,
            seriesTitle: TvCorrectionQuery,
            seasonNumber: TryParsePositiveOrZero(CorrectionSeasonNumber),
            episodeNumber: TryParsePositive(CorrectionEpisodeNumber),
            overview: Overview,
            sourcePath: _correctionSourcePath,
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (suggestionResult.Status != AiSearchSuggestionStatus.Success)
        {
            StatusMessage = string.IsNullOrWhiteSpace(suggestionResult.Message)
                ? "AI 未返回可用电视剧搜索词，请手动输入后搜索。"
                : $"AI 未返回可用电视剧搜索词：{suggestionResult.Message}";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-skipped page=movie targetKind=tv-episode mediaFileId={_correctionMediaFileId} reason={FormatAiSuggestionStatus(suggestionResult.Status)} message={ScanIdentificationDiagnostics.FormatValue(suggestionResult.Message, 180)}");
            return;
        }

        var suggestion = suggestionResult.Suggestion;

        var hasAiSeasonNumber = suggestionResult.Status == AiSearchSuggestionStatus.Success
                                && suggestion.SeasonNumber.HasValue
                                && suggestion.SeasonNumber.Value >= 0;
        var hasAiEpisodeNumber = suggestionResult.Status == AiSearchSuggestionStatus.Success
                                 && suggestion.EpisodeNumber.HasValue
                                 && suggestion.EpisodeNumber.Value > 0;

        TvCorrectionQuery = suggestion.Query;
        if (hasAiSeasonNumber)
        {
            CorrectionSeasonNumber = suggestion.SeasonNumber.GetValueOrDefault().ToString();
        }
        else
        {
            CorrectionSeasonNumber = string.Empty;
        }

        if (hasAiEpisodeNumber)
        {
            CorrectionEpisodeNumber = suggestion.EpisodeNumber.GetValueOrDefault().ToString();
        }
        else
        {
            CorrectionEpisodeNumber = string.Empty;
        }

        StatusMessage = FormatAiTvSearchSuggestionStatus(suggestion, hasAiSeasonNumber, hasAiEpisodeNumber);
        await SearchCandidatesCoreAsync(cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-succeeded page=movie targetKind=tv-episode mediaFileId={_correctionMediaFileId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={TvSeriesCandidateGroups.Count}");
    }

    private bool CanAiSuggestSearch()
    {
        return !IsCorrectionBusy
               && CanUseIdentificationCorrection
               && IsCorrectionPanelVisible
               && !IsCorrectionTargetUnknownSeason;
    }

    private bool CanBeginSourceCorrection(object? parameter)
    {
        return CanUseIdentificationCorrection
               && parameter is MovieSourceItem source
               && Sources.Any(item => item.MediaFileId == source.MediaFileId);
    }

    private void OpenDefaultSourceCorrection()
    {
        var source = Sources.FirstOrDefault(item => item.IsDefault) ?? Sources.FirstOrDefault();
        if (source is null)
        {
            StatusMessage = "当前电影没有可修正的播放源。";
            return;
        }

        BeginSourceCorrection(source);
    }

    private void BeginSourceCorrection(object? parameter)
    {
        if (parameter is not MovieSourceItem source || !CanBeginSourceCorrection(parameter))
        {
            return;
        }

        if (IsCorrectionPanelVisible)
        {
            SwitchCorrectionSource(source);
            return;
        }

        _correctionMediaFileId = null;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        _selectedCorrectionSource = source;
        OnPropertyChanged(nameof(SelectedCorrectionSource));
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = $"{source.SourceTypeText} · {source.FileName}";
        CorrectionSourceFileName = source.FileName;
        CorrectionSourcePath = source.FilePath;
        ManualSearchQuery = string.IsNullOrWhiteSpace(ManualSearchQuery) ? TitleText : ManualSearchQuery;
        TvCorrectionQuery = string.IsNullOrWhiteSpace(TvCorrectionQuery) ? TitleText : TvCorrectionQuery;
        UnknownSeasonSearchQuery = string.IsNullOrWhiteSpace(UnknownSeasonSearchQuery) ? TitleText : UnknownSeasonSearchQuery;
        UnknownSeasonEpisodeNumber = "1";
        CorrectionSeasonNumber = "1";
        CorrectionEpisodeNumber = "1";
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        TvSeriesCandidateGroups.Clear();
        ClearUnknownSeasonTargets();
        SelectedUnknownSeasonTarget = null;
        IsUnknownSeasonPickerDialogOpen = false;
        ClearSelectedTvCorrectionTarget();
        _correctionMediaFileId = source.MediaFileId;
        SelectedDetailTabIndex = 3;
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
        CloseCorrectionCommand.RaiseCanExecuteChanged();
        SearchCandidatesCommand.RaiseCanExecuteChanged();
        ApplyManualMatchCommand.RaiseCanExecuteChanged();
        SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        ApplyTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = string.Empty;
    }

    private void SwitchCorrectionSource(MovieSourceItem source)
    {
        _selectedCorrectionSource = source;
        OnPropertyChanged(nameof(SelectedCorrectionSource));
        _correctionMediaFileId = source.MediaFileId;
        CorrectionSourceDisplay = $"{source.SourceTypeText} · {source.FileName}";
        CorrectionSourceFileName = source.FileName;
        CorrectionSourcePath = source.FilePath;
        ClearCorrectionPreview();
        ApplyManualMatchCommand.RaiseCanExecuteChanged();
        ApplyTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
    }

    private void CancelCorrection()
    {
        if (CancelCorrectionAiRequest())
        {
            IsCorrectionBusy = false;
        }
        _correctionMediaFileId = null;
        _selectedCorrectionSource = null;
        OnPropertyChanged(nameof(SelectedCorrectionSource));
        CorrectionSourceFileName = string.Empty;
        CorrectionSourcePath = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        TvSeriesCandidateGroups.Clear();
        ClearUnknownSeasonTargets();
        SelectedUnknownSeasonTarget = null;
        IsUnknownSeasonPickerDialogOpen = false;
        ClearSelectedTvCorrectionTarget();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
        CloseCorrectionCommand.RaiseCanExecuteChanged();
        SearchCandidatesCommand.RaiseCanExecuteChanged();
        ApplyManualMatchCommand.RaiseCanExecuteChanged();
        SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        ApplyTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = "已取消本次修正，未修改任何数据。";
    }

    private void CloseCorrection()
    {
        if (_correctionAiCancellation is not null)
        {
            CancelCorrection();
            return;
        }

        if (!IsCorrectionBusy)
        {
            CancelCorrection();
        }
    }

    private bool CancelCorrectionAiRequest()
    {
        var cancellation = _correctionAiCancellation;
        _correctionAiCancellation = null;
        cancellation?.Cancel();
        return cancellation is not null;
    }

    private bool CanSearchCandidates()
    {
        return !IsCorrectionBusy
               && CanUseIdentificationCorrection
               && IsCorrectionPanelVisible
               && !IsCorrectionTargetUnknownSeason;
    }

    private async Task SearchCandidatesAsync()
    {
        if (IsCorrectionBusy)
        {
            return;
        }

        CancelCorrectionAiRequest();
        var cancellation = new CancellationTokenSource();
        _correctionAiCancellation = cancellation;
        try
        {
            IsCorrectionBusy = true;
            await SearchCandidatesCoreAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_correctionAiCancellation, cancellation) && IsCorrectionPanelVisible)
            {
                StatusMessage = "TMDB 搜索已取消。";
            }
        }
        finally
        {
            var isCurrentRequest = ReferenceEquals(_correctionAiCancellation, cancellation);
            if (isCurrentRequest)
            {
                _correctionAiCancellation = null;
            }

            cancellation.Dispose();
            if (isCurrentRequest)
            {
                IsCorrectionBusy = false;
            }
        }
    }

    private async Task SearchCandidatesCoreAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUseIdentificationCorrection)
        {
            StatusMessage = "只有已入库影片才能执行识别修正。";
            return;
        }

        if (!IsCorrectionPanelVisible)
        {
            StatusMessage = "请先在播放源列表中选择要修正的单个播放源。";
            return;
        }

        if (IsCorrectionTargetTvEpisode)
        {
            await SearchTvCandidatesAsync(cancellationToken);
            return;
        }

        if (IsCorrectionTargetUnknownSeason)
        {
            StatusMessage = "请使用“加入已有未识别季”区域选择目标季。";
            return;
        }

        var query = string.IsNullOrWhiteSpace(ManualSearchQuery) ? TitleText : ManualSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchCandidates.Clear();
            OnPropertyChanged(nameof(HasSearchCandidates));
            StatusMessage = "请输入要搜索的片名。";
            return;
        }

        try
        {
            StatusMessage = "正在搜索 TMDB 电影，请稍候。";
            await Task.Yield();
            var releaseYear = int.TryParse(ManualSearchYear, out var parsedYear) ? parsedYear : (int?)null;
            var candidates = await _movieIdentificationService.SearchCandidatesAsync(query, releaseYear, cancellationToken);
            var hydratedCandidates = await HydrateMovieCorrectionCandidatesAsync(candidates, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            SearchCandidates.Clear();
            foreach (var candidate in hydratedCandidates)
            {
                candidate.IsCurrentMatchedMovie = _tmdbId.HasValue && candidate.TmdbId == _tmdbId.Value;
                SearchCandidates.Add(candidate);
            }

            OnPropertyChanged(nameof(HasSearchCandidates));
            StatusMessage = SearchCandidates.Count == 0
                ? "没有找到符合条件的 TMDB 结果。"
                : $"已找到 {SearchCandidates.Count} 个候选结果。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SearchCandidates.Clear();
            OnPropertyChanged(nameof(HasSearchCandidates));
            StatusMessage = DescribeTmdbSearchFailure(exception);
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-tmdb-search-failed page=movie targetKind=movie reason=search-failed");
        }
    }

    private async Task SearchTvCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(TvCorrectionQuery) ? TitleText : TvCorrectionQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            TvSeriesCandidateGroups.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = "请输入要搜索的电视剧名。";
            return;
        }

        try
        {
            StatusMessage = "正在搜索 TMDB 电视剧，请稍候。";
            await Task.Yield();
            var page = await _tmdbService.SearchTvSeriesAsync(query, 1, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            ClearSelectedTvCorrectionTarget();
            var candidates = page.Results.Take(12).ToList();
            var details = await HydrateTvCorrectionCandidatesAsync(candidates, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                TvSearchCandidates.Add(candidate);
                TvSeriesCandidateGroups.Add(new TmdbTvSeriesCorrectionSeriesGroup(candidate, details[index]));
            }

            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = TvSeriesCandidateGroups.Count == 0
                ? "没有找到符合条件的 TMDB 电视剧结果。"
                : $"已找到 {TvSeriesCandidateGroups.Count} 个电视剧候选；可展开选择季，或直接修正到剧并使用输入的季号/集号。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = DescribeTmdbSearchFailure(exception);
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-tmdb-search-failed page=movie targetKind=tv-episode reason=search-failed");
        }
    }

    private async Task<IReadOnlyList<MetadataSearchCandidate>> HydrateMovieCorrectionCandidatesAsync(
        IReadOnlyList<MetadataSearchCandidate> candidates,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = candidates
            .Select(candidate => HydrateMovieCorrectionCandidateAsync(candidate, gate, cancellationToken))
            .ToArray();
        return await Task.WhenAll(tasks);
    }

    private async Task<MetadataSearchCandidate> HydrateMovieCorrectionCandidateAsync(
        MetadataSearchCandidate candidate,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var details = await _tmdbService.GetMovieDetailsAsync(candidate.TmdbId, cancellationToken);
            if (details is null)
            {
                return LocalizeMovieCorrectionCandidate(candidate);
            }

            details.Confidence = candidate.Confidence;
            return LocalizeMovieCorrectionCandidate(details);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-movie-detail-fallback page=movie tmdbId={candidate.TmdbId} reason=detail-load-failed");
            return LocalizeMovieCorrectionCandidate(candidate);
        }
        finally
        {
            gate.Release();
        }
    }

    private static MetadataSearchCandidate LocalizeMovieCorrectionCandidate(MetadataSearchCandidate candidate)
    {
        candidate.Country = MovieMetadataDisplayText.LocalizeCountries(candidate.Country);
        candidate.Language = MovieMetadataDisplayText.LocalizeLanguages(candidate.Language);
        return candidate;
    }

    private async Task<IReadOnlyList<TmdbTvSeriesDetailResult?>> HydrateTvCorrectionCandidatesAsync(
        IReadOnlyList<TmdbTvSeriesSearchItem> candidates,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(4);
        var tasks = candidates
            .Select(candidate => HydrateTvCorrectionCandidateAsync(candidate, gate, cancellationToken))
            .ToArray();
        return await Task.WhenAll(tasks);
    }

    private async Task<TmdbTvSeriesDetailResult?> HydrateTvCorrectionCandidateAsync(
        TmdbTvSeriesSearchItem candidate,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await _tmdbService.GetTvSeriesDetailsAsync(candidate.TmdbId, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-tv-detail-fallback page=movie tmdbSeriesId={candidate.TmdbId} reason=detail-load-failed");
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool CanSearchUnknownSeasonTargets()
    {
        return !IsCorrectionBusy
               && CanUseIdentificationCorrection
               && IsCorrectionPanelVisible
               && IsCorrectionTargetUnknownSeason;
    }

    private bool CanOpenUnknownSeasonPicker()
    {
        return CanSearchUnknownSeasonTargets() && !IsUnknownSeasonPickerDialogOpen;
    }

    private async Task OpenUnknownSeasonPickerAsync()
    {
        if (!CanOpenUnknownSeasonPicker())
        {
            StatusMessage = "请先选择播放源，并把目标类型切换为“加入已有未识别季”。";
            return;
        }

        await SearchUnknownSeasonTargetsAsync();
        IsUnknownSeasonPickerDialogOpen = true;
    }

    private void CloseUnknownSeasonPicker()
    {
        IsUnknownSeasonPickerDialogOpen = false;
    }

    private bool CanSelectUnknownSeasonTarget(object? parameter)
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetUnknownSeason
               && parameter is UnknownTvSeasonCorrectionTargetItem;
    }

    private void SelectUnknownSeasonTarget(object? parameter)
    {
        if (parameter is not UnknownTvSeasonCorrectionTargetItem target)
        {
            return;
        }

        SelectedUnknownSeasonTarget = target;
        if (IsUnknownSeasonPickerDialogOpen)
        {
            IsUnknownSeasonPickerDialogOpen = false;
        }
        StatusMessage = $"已选择未识别季：{target.DisplayTitle}。请输入集号后加入。";
    }

    private async Task SearchUnknownSeasonTargetsAsync()
    {
        if (!CanSearchUnknownSeasonTargets())
        {
            StatusMessage = "请先选择播放源，并把目标类型切换为“加入已有未识别季”。";
            return;
        }

        CancelCorrectionAiRequest();
        var cancellation = new CancellationTokenSource();
        _correctionAiCancellation = cancellation;
        try
        {
            IsCorrectionBusy = true;
            StatusMessage = "正在加载已有未识别季，请稍候。";
            await Task.Yield();
            var targets = await _singleSourceCorrectionService.SearchUnknownSeasonTargetsAsync(
                null,
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            SetUnknownSeasonTargets(targets);
            StatusMessage = UnknownSeasonTargets.Count == 0
                ? "没有可选择的已有未识别季。"
                : $"已找到 {UnknownSeasonTargets.Count} 个可加入的未识别季。";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_correctionAiCancellation, cancellation) && IsCorrectionPanelVisible)
            {
                StatusMessage = "加载未识别季已取消。";
            }
        }
        catch (Exception exception)
        {
            ClearUnknownSeasonTargets();
            StatusMessage = $"加载未识别季失败：{DescribeException(exception)}";
        }
        finally
        {
            var isCurrentRequest = ReferenceEquals(_correctionAiCancellation, cancellation);
            if (isCurrentRequest)
            {
                _correctionAiCancellation = null;
            }

            cancellation.Dispose();
            if (isCurrentRequest)
            {
                IsCorrectionBusy = false;
            }
        }
    }

    private bool CanApplyMovieCandidateCorrection(object? parameter)
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetMovie
               && parameter is MetadataSearchCandidate { IsCurrentMatchedMovie: false };
    }

    private async Task ApplyMovieCandidateCorrectionAsync(object? parameter)
    {
        if (_correctionMediaFileId is null || parameter is not MetadataSearchCandidate candidate)
        {
            StatusMessage = "请先选择播放源和电影候选。";
            return;
        }

        try
        {
            IsCorrectionBusy = true;
            StatusMessage = $"正在修正为电影：{candidate.Title}。";
            await Task.Yield();
            using var timeout = new CancellationTokenSource(CorrectionApplyTimeout);
            var mediaFileId = _correctionMediaFileId.Value;
            var result = await Task.Run(
                () => _singleSourceCorrectionService.ApplyMovieCorrectionAsync(
                    mediaFileId,
                    candidate.TmdbId,
                    timeout.Token),
                timeout.Token);
            _dataRefreshService.NotifyMetadataChanged();
            if (result.TargetMovieId.HasValue)
            {
                _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, result.TargetMovieId.Value);
                await LoadMovieAsync(result.TargetMovieId.Value, CancellationToken.None);
            }

            StatusMessage = $"已修正为电影：{candidate.Title}";
            ClearCorrectionAfterApply();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "应用修正超时，事务已回滚；请稍后重试或先确认 TMDB 网络状态。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"应用修正失败，事务已回滚：{DescribeException(exception)}";
        }
        finally
        {
            IsCorrectionBusy = false;
        }
    }

    private bool CanSelectTvEpisodeCorrectionTarget(object? parameter)
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetTvEpisode
               && (parameter is TmdbTvSeriesCorrectionSeriesGroup
                   || parameter is TmdbTvSeasonCorrectionSeasonItem
                   || parameter is TmdbTvSeriesSearchItem);
    }

    private void SelectTvEpisodeCorrectionTarget(object? parameter)
    {
        var (seriesTmdbId, seriesName, selectedSeasonNumber) = ResolveTvEpisodeCorrectionTarget(parameter);
        if (seriesTmdbId <= 0)
        {
            StatusMessage = "请选择目标电视剧或季。";
            return;
        }

        _selectedTvCorrectionSeriesTmdbId = seriesTmdbId;
        _selectedTvCorrectionSeriesName = string.IsNullOrWhiteSpace(seriesName) ? $"TV {seriesTmdbId}" : seriesName;
        TvCorrectionQuery = _selectedTvCorrectionSeriesName;
        _selectedTvCorrectionSeasonNumber = selectedSeasonNumber;
        if (selectedSeasonNumber.HasValue)
        {
            CorrectionSeasonNumber = selectedSeasonNumber.Value.ToString();
        }

        OnPropertyChanged(nameof(HasSelectedTvCorrectionTarget));
        OnPropertyChanged(nameof(SelectedTvCorrectionTargetDisplay));
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        StatusMessage = selectedSeasonNumber.HasValue
            ? $"已选择目标季：{_selectedTvCorrectionSeriesName} S{selectedSeasonNumber.Value:00}。请确认集号后点击确认修正。"
            : $"已选择目标剧：{_selectedTvCorrectionSeriesName}。请确认季号和集号后点击确认修正。";
    }

    private bool CanApplySelectedTvEpisodeCorrection()
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetTvEpisode
               && _correctionMediaFileId.HasValue
               && _selectedTvCorrectionSeriesTmdbId.HasValue;
    }

    private async Task ApplyTvEpisodeCorrectionTargetAsync(object? parameter)
    {
        SelectTvEpisodeCorrectionTarget(parameter);
        await ApplySelectedTvEpisodeCorrectionAsync();
    }

    private async Task ApplySelectedTvEpisodeCorrectionAsync()
    {
        if (_correctionMediaFileId is null || _selectedTvCorrectionSeriesTmdbId is null)
        {
            StatusMessage = "请先选择播放源和目标电视剧。";
            return;
        }

        if (!int.TryParse(CorrectionSeasonNumber, out var seasonNumber) || seasonNumber < 0)
        {
            StatusMessage = "季号必须是 0 或正整数。";
            return;
        }

        if (!int.TryParse(CorrectionEpisodeNumber, out var episodeNumber) || episodeNumber <= 0)
        {
            StatusMessage = "集号必须是正整数。";
            return;
        }

        var seriesTmdbId = _selectedTvCorrectionSeriesTmdbId.Value;
        var seriesName = string.IsNullOrWhiteSpace(_selectedTvCorrectionSeriesName)
            ? $"TV {seriesTmdbId}"
            : _selectedTvCorrectionSeriesName;

        try
        {
            IsCorrectionBusy = true;
            StatusMessage = $"正在修正为电视剧集：{seriesName} S{seasonNumber:00}E{episodeNumber:00}。";
            await Task.Yield();
            using var timeout = new CancellationTokenSource(CorrectionApplyTimeout);
            var mediaFileId = _correctionMediaFileId.Value;
            var result = await Task.Run(
                () => _singleSourceCorrectionService.ApplyTvEpisodeCorrectionAsync(
                    mediaFileId,
                    seriesTmdbId,
                    seasonNumber,
                    episodeNumber,
                    timeout.Token),
                timeout.Token);
            _dataRefreshService.NotifyMetadataChanged();
            if (result.TargetEpisodeId.HasValue)
            {
                _navigationStateService.RequestEpisodeDetail(result.TargetEpisodeId.Value);
            }

            StatusMessage = $"已修正为电视剧集：{seriesName} S{seasonNumber:00}E{episodeNumber:00}";
            ClearCorrectionAfterApply();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "应用修正超时，事务已回滚；请稍后重试或先确认 TMDB 网络状态。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"应用修正失败，事务已回滚：{DescribeException(exception)}";
        }
        finally
        {
            IsCorrectionBusy = false;
        }
    }

    private static (int SeriesTmdbId, string SeriesName, int? SeasonNumber) ResolveTvEpisodeCorrectionTarget(object? parameter)
    {
        return parameter switch
        {
            TmdbTvSeasonCorrectionSeasonItem season => (season.TmdbSeriesId, season.SeriesTitle, season.SeasonNumber),
            TmdbTvSeriesCorrectionSeriesGroup series => (series.TmdbSeriesId, series.SeriesTitle, null),
            TmdbTvSeriesSearchItem item => (item.TmdbId, item.Name, null),
            _ => (0, string.Empty, null)
        };
    }

    private bool CanApplyUnknownSeasonCorrection(object? parameter)
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetUnknownSeason
               && (parameter is UnknownTvSeasonCorrectionTargetItem || SelectedUnknownSeasonTarget is not null)
               && int.TryParse(UnknownSeasonEpisodeNumber, out var episodeNumber)
               && episodeNumber > 0;
    }

    private async Task ApplyUnknownSeasonCorrectionAsync(object? parameter)
    {
        var target = parameter as UnknownTvSeasonCorrectionTargetItem ?? SelectedUnknownSeasonTarget;
        if (_correctionMediaFileId is null || target is null)
        {
            StatusMessage = "请先选择播放源和未识别季。";
            return;
        }

        if (!int.TryParse(UnknownSeasonEpisodeNumber, out var episodeNumber) || episodeNumber <= 0)
        {
            StatusMessage = "集号必须是正整数。";
            return;
        }

        try
        {
            IsCorrectionBusy = true;
            StatusMessage = $"正在加入未识别季：{target.DisplayTitle} E{episodeNumber:00}。";
            await Task.Yield();
            using var timeout = new CancellationTokenSource(CorrectionApplyTimeout);
            var mediaFileId = _correctionMediaFileId.Value;
            var result = await Task.Run(
                () => _singleSourceCorrectionService.ApplyUnknownSeasonEpisodeCorrectionAsync(
                    mediaFileId,
                    target.SeasonId,
                    episodeNumber,
                    timeout.Token),
                timeout.Token);
            _dataRefreshService.NotifyMetadataChanged();
            if (result.TargetEpisodeId.HasValue)
            {
                _navigationStateService.RequestEpisodeDetail(result.TargetEpisodeId.Value);
            }

            StatusMessage = result.CreatedEpisode
                ? $"已创建并加入未识别季 E{episodeNumber:00}。"
                : $"已加入未识别季 E{episodeNumber:00}，并设为默认播放源。";
            ClearCorrectionAfterApply();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "应用修正超时，事务已回滚；请稍后重试。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"应用修正失败，事务已回滚：{DescribeException(exception)}";
        }
        finally
        {
            IsCorrectionBusy = false;
        }
    }

    private void SetCorrectionPreview(SingleSourceCorrectionPreview preview)
    {
        CorrectionPreviewText = preview.PreviewText;
        HasCorrectionPreview = preview.IsValid;
    }

    private void ClearCorrectionPreview()
    {
        CorrectionPreviewText = string.Empty;
        HasCorrectionPreview = false;
    }

    private void ClearUnknownSeasonTargets()
    {
        UnknownSeasonTargets.Clear();
        UnknownSeasonSeriesGroups.Clear();
        OnPropertyChanged(nameof(HasUnknownSeasonTargets));
    }

    private void SetUnknownSeasonTargets(IEnumerable<UnknownTvSeasonCorrectionTargetItem> targets)
    {
        var orderedTargets = targets
            .OrderBy(x => x.SeriesTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SeasonNumber)
            .ThenBy(x => x.SeasonTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SeasonId)
            .ToList();

        UnknownSeasonTargets.ReplaceAll(orderedTargets);
        UnknownSeasonSeriesGroups.ReplaceAll(UnknownTvSeasonCorrectionSeriesGroup.FromTargets(orderedTargets));

        OnPropertyChanged(nameof(HasUnknownSeasonTargets));
    }

    private void ClearCandidatesForInactiveCorrectionTarget()
    {
        if (IsCorrectionTargetMovie)
        {
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            ClearUnknownSeasonTargets();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            return;
        }

        if (IsCorrectionTargetTvEpisode)
        {
            SearchCandidates.Clear();
            ClearUnknownSeasonTargets();
            OnPropertyChanged(nameof(HasSearchCandidates));
            return;
        }

        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        TvSeriesCandidateGroups.Clear();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
    }

    private void ClearSelectedTvCorrectionTarget()
    {
        _selectedTvCorrectionSeriesTmdbId = null;
        _selectedTvCorrectionSeriesName = string.Empty;
        _selectedTvCorrectionSeasonNumber = null;
        OnPropertyChanged(nameof(HasSelectedTvCorrectionTarget));
        OnPropertyChanged(nameof(SelectedTvCorrectionTargetDisplay));
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
    }

    private void StoreCorrectionStatusForSelectedTarget(string statusMessage)
    {
        if (_isRestoringCorrectionStatus || !IsCorrectionPanelVisible)
        {
            return;
        }

        if (IsCorrectionTargetUnknownSeason)
        {
            _unknownSeasonCorrectionStatusMessage = statusMessage;
        }
        else if (IsCorrectionTargetTvEpisode)
        {
            _tvEpisodeCorrectionStatusMessage = statusMessage;
        }
        else
        {
            _movieCorrectionStatusMessage = statusMessage;
        }
    }

    private void RestoreCorrectionStatusForSelectedTarget()
    {
        if (!IsCorrectionPanelVisible)
        {
            return;
        }

        var statusMessage = IsCorrectionTargetUnknownSeason
            ? _unknownSeasonCorrectionStatusMessage
            : IsCorrectionTargetTvEpisode
                ? _tvEpisodeCorrectionStatusMessage
                : _movieCorrectionStatusMessage;

        _isRestoringCorrectionStatus = true;
        try
        {
            StatusMessage = statusMessage;
        }
        finally
        {
            _isRestoringCorrectionStatus = false;
        }
    }

    private static int? TryParsePositive(string value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static int? TryParsePositiveOrZero(string value)
    {
        return int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : null;
    }

    private static string FormatAiSearchSuggestionStatus(string targetName, AiSearchSuggestion suggestion)
    {
        return suggestion.ReleaseYear.HasValue
            ? $"AI 建议{targetName}搜索：{suggestion.Query}（{suggestion.ReleaseYear}）"
            : $"AI 建议{targetName}搜索：{suggestion.Query}";
    }

    private static string FormatAiTvSearchSuggestionStatus(
        AiSearchSuggestion suggestion,
        bool hasSeasonNumber,
        bool hasEpisodeNumber)
    {
        var episodeText = hasSeasonNumber && hasEpisodeNumber
            ? $" S{suggestion.SeasonNumber.GetValueOrDefault():00}E{suggestion.EpisodeNumber.GetValueOrDefault():00}"
            : string.Empty;
        var missingText = hasSeasonNumber && hasEpisodeNumber
            ? string.Empty
            : "；AI 未返回完整季号/集号，请手动输入或展开剧候选选择季";
        return $"AI 建议电视剧搜索：{suggestion.Query}{episodeText}{missingText}";
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

    private void ClearCorrectionAfterApply()
    {
        _correctionMediaFileId = null;
        _selectedCorrectionSource = null;
        OnPropertyChanged(nameof(SelectedCorrectionSource));
        CorrectionSourceFileName = string.Empty;
        CorrectionSourcePath = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        TvSeriesCandidateGroups.Clear();
        ClearUnknownSeasonTargets();
        SelectedUnknownSeasonTarget = null;
        IsUnknownSeasonPickerDialogOpen = false;
        ClearSelectedTvCorrectionTarget();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
        SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        ApplyTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
    }

    private async Task SetDefaultSourceAsync(object? parameter)
    {
        if (_movieId is null || parameter is not MovieSourceItem source)
        {
            return;
        }

        try
        {
            await _movieManagementService.SetDefaultMediaFileAsync(_movieId.Value, source.MediaFileId);
            StatusMessage = $"默认播放源已切换为：{source.SourceTypeText} · {source.FileName}";
            await LoadMovieAsync(_movieId.Value, CancellationToken.None);
        }
        catch (Exception exception)
        {
            StatusMessage = $"设置默认播放源失败：{DescribeException(exception)}";
        }
    }

    private async Task ResetSourceRecognitionAsync(object? parameter)
    {
        if (_movieId is null || parameter is not MovieSourceItem source)
        {
            return;
        }

        if (!CanResetSourcesToUnidentified)
        {
            StatusMessage = "该播放源已在未识别承接中，无需从当前电影拆分。";
            return;
        }

        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "确认拆分此播放源？",
            $"拆分后，该播放源将不再归属于当前电影，可能需要在未识别项目中继续修正。该操作不会删除本地文件或 WebDAV 文件。\n\n播放源：{source.FileName}",
            "拆分来源",
            "取消",
            ConfirmationDialogVariant.Warning);
        if (!confirmed)
        {
            StatusMessage = "已取消拆分播放源。";
            return;
        }

        try
        {
            var result = await _movieManagementService.ResetMediaFileToUnidentifiedAsync(_movieId.Value, source.MediaFileId);
            _dataRefreshService.NotifyMetadataChanged();
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, result.DetailMovieId);
            await LoadMovieAsync(result.DetailMovieId, CancellationToken.None);
            StatusMessage = $"已将播放源“{source.FileName}”从当前电影拆分。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"拆分播放源失败：{DescribeException(exception)}";
        }
    }

    private void ClearMovieState(string statusMessage)
    {
        IsDetailLoading = false;
        _movieId = null;
        _externalRecommendation = null;
        HasMovie = false;
        IsLibraryMovie = false;
        IsNoSourceDetail = false;
        _tmdbId = null;
        CanPlay = false;
        IsFavorite = false;
        IsWatched = false;
        IsWantToWatch = false;
        IsNotInterested = false;
        IsVisibleInLibrary = false;
        CurrentLibraryVisibilityState = LibraryVisibilityState.Auto;
        RefreshWantToWatchCommandState();
        RefreshNotInterestedCommandState();
        RefreshWatchedCommandState();
        RefreshAddToLibraryCommandState();
        AvailabilityText = "未加载";
        PlayButtonText = "播放默认源";
        TitleText = "未选择影片";
        OriginalTitle = "-";
        ReleaseYearText = "-";
        ReleaseDateText = "-";
        Overview = "请先从资源库中选择一部影片。";
        PosterRemoteUrl = string.Empty;
        PosterDisplayUrl = string.Empty;
        Country = "-";
        Language = "-";
        DirectorText = "-";
        WriterText = "-";
        ActorsText = "-";
        ProductionCompanyText = "-";
        RuntimeText = "-";
        GenresText = "未提供";
        AiTagsText = "尚未分类";
        EmotionTagsText = "尚未分类";
        SceneTagsText = "尚未分类";
        IdentificationStatusText = "未加载";
        ConfidenceText = "-";
        TmdbIdText = "-";
        ImdbIdText = "-";
        DefaultSourceDisplay = "尚未设置";
        StatusMessage = statusMessage;
        _identificationStatus = IdentificationStatus.Pending;
        RefreshResetSourceRecognitionCommandState();
        OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
        OnPropertyChanged(nameof(IsUnidentifiedMovie));
        ManualSearchQuery = string.Empty;
        ManualSearchYear = string.Empty;
        Ratings.Clear();
        Sources.Clear();
        NotifyRatingStateChanged();
        NotifySourceStateChanged();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        TvSeriesCandidateGroups.Clear();
        ClearUnknownSeasonTargets();
        SelectedUnknownSeasonTarget = null;
        IsUnknownSeasonPickerDialogOpen = false;
        _correctionMediaFileId = null;
        _selectedCorrectionSource = null;
        OnPropertyChanged(nameof(SelectedCorrectionSource));
        CorrectionSourceFileName = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
    }

    private void BeginDetailLoading(string statusMessage)
    {
        IsDetailLoading = true;
        ClearMovieState(statusMessage);
        IsDetailLoading = true;
    }

    private bool CanResetSourceRecognition(object? parameter)
    {
        return parameter is MovieSourceItem source
               && CanResetSourcesToUnidentified
               && Sources.Any(item => item.MediaFileId == source.MediaFileId);
    }

    private static string GetIdentificationStatusText(IdentificationStatus status)
    {
        return status switch
        {
            IdentificationStatus.Matched => "自动匹配",
            IdentificationStatus.NeedsReview => "待人工确认",
            IdentificationStatus.ManualConfirmed => "人工确认",
            IdentificationStatus.Failed => "识别失败",
            _ => "待识别"
        };
    }

    private static bool NeedsAutoClassification(MovieDetailModel detail)
    {
        return detail.IdentificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed
               && (string.IsNullOrWhiteSpace(detail.AiTagsText)
                   || string.IsNullOrWhiteSpace(detail.EmotionTagsText)
                   || string.IsNullOrWhiteSpace(detail.SceneTagsText));
    }

    private void ShowExternalAiAnalyzingState(AiRecommendationItem recommendation)
    {
        GenresText = string.IsNullOrWhiteSpace(recommendation.Tags)
            ? ExternalAiAnalyzingText
            : recommendation.Tags;
        AiTagsText = string.IsNullOrWhiteSpace(recommendation.Tags)
            ? ExternalAiAnalyzingText
            : recommendation.Tags;
        EmotionTagsText = string.IsNullOrWhiteSpace(recommendation.EmotionTagsText)
            ? ExternalAiAnalyzingText
            : recommendation.EmotionTagsText;
        SceneTagsText = string.IsNullOrWhiteSpace(recommendation.SceneTagsText)
            ? ExternalAiAnalyzingText
            : recommendation.SceneTagsText;
    }

    private void ApplyExternalTagDisplay(AiRecommendationItem recommendation, string missingText)
    {
        AiTagsText = string.IsNullOrWhiteSpace(recommendation.Tags) ? missingText : recommendation.Tags;
        GenresText = AiTagsText;
        EmotionTagsText = string.IsNullOrWhiteSpace(recommendation.EmotionTagsText)
            ? missingText
            : recommendation.EmotionTagsText;
        SceneTagsText = string.IsNullOrWhiteSpace(recommendation.SceneTagsText)
            ? missingText
            : recommendation.SceneTagsText;
    }

    private static bool ApplyCachedExternalTags(AiRecommendationItem recommendation)
    {
        if (!ExternalMovieTagCache.TryGet(
                recommendation.TmdbId,
                recommendation.ImdbId,
                recommendation.Title,
                recommendation.ReleaseYear,
                out var cachedTags))
        {
            return false;
        }

        recommendation.Tags = string.IsNullOrWhiteSpace(cachedTags.AiTagsText) ? recommendation.Tags : cachedTags.AiTagsText;
        recommendation.EmotionTagsText = string.IsNullOrWhiteSpace(cachedTags.EmotionTagsText)
            ? recommendation.EmotionTagsText
            : cachedTags.EmotionTagsText;
        recommendation.SceneTagsText = string.IsNullOrWhiteSpace(cachedTags.SceneTagsText)
            ? recommendation.SceneTagsText
            : cachedTags.SceneTagsText;
        return !NeedsExternalAutoClassification(recommendation);
    }

    private static void CacheExternalTags(AiRecommendationItem recommendation)
    {
        ExternalMovieTagCache.Set(recommendation);
    }

    private static bool NeedsExternalAutoClassification(AiRecommendationItem recommendation)
    {
        return string.IsNullOrWhiteSpace(recommendation.Tags)
               || string.IsNullOrWhiteSpace(recommendation.EmotionTagsText)
               || string.IsNullOrWhiteSpace(recommendation.SceneTagsText);
    }

    private void ApplyClassifiedLocalTagsToCurrentDetail(MovieDetailModel classified)
    {
        AiTagsText = string.IsNullOrWhiteSpace(classified.AiTagsText)
            ? ExternalAiMissingText
            : classified.AiTagsText;
        EmotionTagsText = string.IsNullOrWhiteSpace(classified.EmotionTagsText)
            ? ExternalAiMissingText
            : classified.EmotionTagsText;
        SceneTagsText = string.IsNullOrWhiteSpace(classified.SceneTagsText)
            ? ExternalAiMissingText
            : classified.SceneTagsText;
    }

    private static void ApplyExternalClassificationResult(AiRecommendationItem recommendation, AiMovieTags tags)
    {
        recommendation.Tags = string.IsNullOrWhiteSpace(tags.AiTagsText) ? string.Empty : tags.AiTagsText;
        recommendation.EmotionTagsText = string.IsNullOrWhiteSpace(tags.EmotionTagsText) ? string.Empty : tags.EmotionTagsText;
        recommendation.SceneTagsText = string.IsNullOrWhiteSpace(tags.SceneTagsText) ? string.Empty : tags.SceneTagsText;
    }

    private bool IsCurrentExternalRecommendation(string classificationKey)
    {
        return !IsLibraryMovie
               && _externalRecommendation is not null
               && string.Equals(
                   BuildExternalClassificationKey(_externalRecommendation),
                   classificationKey,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExternalClassificationKey(AiRecommendationItem recommendation)
    {
        if (recommendation.TmdbId is > 0)
        {
            return $"tmdb:{recommendation.TmdbId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(recommendation.ImdbId))
        {
            return $"imdb:{recommendation.ImdbId.Trim().ToLowerInvariant()}";
        }

        return $"title:{recommendation.Title.Trim().ToLowerInvariant()}:{recommendation.ReleaseYear?.ToString() ?? string.Empty}";
    }

    private static string FormatNullable(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "(none)";
    }

    private static Task DispatchToUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }

    private async Task HydrateMovieCreditsForDisplayAsync(int? expectedMovieId, int tmdbId, CancellationToken cancellationToken)
    {
        try
        {
            var details = await _tmdbService.GetMovieDetailsAsync(tmdbId, cancellationToken);
            if (details is null)
            {
                return;
            }

            if (expectedMovieId.HasValue)
            {
                if (_movieId != expectedMovieId.Value)
                {
                    return;
                }
            }
            else if (_movieId.HasValue
                     || _externalRecommendation?.TmdbId != tmdbId)
            {
                return;
            }

            ApplyMovieCreditsForDisplay(details);
            if (_movieId is null
                && _externalRecommendation?.TmdbId == tmdbId
                && _externalRecommendation.ApplyMetadataDetails(details))
            {
                _dataRefreshService.NotifyRecommendationChanged();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // 详情页主创补齐是展示增强，失败时保留已有影片详情，不打断页面使用。
        }
    }

    private void ApplyMovieCreditsForDisplay(MetadataSearchCandidate details)
    {
        if (!string.IsNullOrWhiteSpace(details.DirectorText))
        {
            DirectorText = details.DirectorText;
        }

        if (!string.IsNullOrWhiteSpace(details.WriterText))
        {
            WriterText = details.WriterText;
        }

        if (!string.IsNullOrWhiteSpace(details.ActorsText))
        {
            ActorsText = details.ActorsText;
        }

        if (!string.IsNullOrWhiteSpace(details.ProductionCompanyText))
        {
            ProductionCompanyText = details.ProductionCompanyText;
        }

        if (!string.IsNullOrWhiteSpace(details.Country) && Country == "-")
        {
            Country = details.Country;
        }

        if (!string.IsNullOrWhiteSpace(details.Language) && Language == "-")
        {
            Language = details.Language;
        }
    }

    private static bool NeedsMovieCreditsHydration(MovieDetailModel detail)
    {
        return string.IsNullOrWhiteSpace(detail.DirectorText)
               || string.IsNullOrWhiteSpace(detail.WriterText)
               || string.IsNullOrWhiteSpace(detail.ActorsText)
               || string.IsNullOrWhiteSpace(detail.ProductionCompanyText);
    }

    private static string FormatRuntimeMinutes(int? runtimeMinutes)
    {
        return runtimeMinutes.HasValue && runtimeMinutes.Value > 0
            ? TimeSpan.FromMinutes(runtimeMinutes.Value).ToString(@"hh\:mm\:ss")
            : "-";
    }

    private static IReadOnlyList<string> SplitDisplayTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', '，', '/', '|', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<MovieRatingItem> NormalizeDetailRatings(IEnumerable<MovieRatingItem> ratings)
    {
        var displayRatings = ratings
            .Select(ToDisplayRating)
            .Where(rating => IsDetailRatingSource(rating.SourceName))
            .GroupBy(rating => rating.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(rating => rating.LastUpdatedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        return new[] { "TMDB", "IMDb" }
            .Select(sourceName => displayRatings.TryGetValue(sourceName, out var rating)
                ? rating
                : new MovieRatingItem { SourceName = sourceName })
            .ToList();
    }

    private static MovieRatingItem ToDisplayRating(MovieRatingItem rating)
    {
        return new MovieRatingItem
        {
            SourceName = GetDisplayRatingSourceName(rating.SourceName),
            ScoreValue = rating.ScoreValue,
            ScoreScale = rating.ScoreScale,
            VoteCount = rating.VoteCount,
            SourceUrl = rating.SourceUrl,
            LastUpdatedAt = rating.LastUpdatedAt
        };
    }

    private static string GetDisplayRatingSourceName(string sourceName)
    {
        return string.Equals(sourceName, "OMDb", StringComparison.OrdinalIgnoreCase)
            ? "IMDb"
            : sourceName;
    }

    private static bool IsDetailRatingSource(string sourceName)
    {
        return string.Equals(sourceName, "TMDB", StringComparison.OrdinalIgnoreCase)
               || string.Equals(sourceName, "IMDb", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeTmdbSearchFailure(Exception exception)
    {
        if (exception is TaskCanceledException || exception.InnerException is TaskCanceledException)
        {
            return "搜索 TMDB 超时，请稍后重试。";
        }

        if (exception is HttpRequestException httpRequestException)
        {
            return httpRequestException.StatusCode.HasValue
                ? $"TMDB API 请求失败：{(int)httpRequestException.StatusCode.Value} {httpRequestException.StatusCode.Value}"
                : $"TMDB 网络请求失败：{exception.Message}";
        }

        return $"搜索 TMDB 失败：{exception.Message}";
    }

    private static bool IsSameRecommendation(CollectionMovieItem collectionItem, AiRecommendationItem recommendation)
    {
        return (recommendation.MovieId > 0 && collectionItem.MovieId == recommendation.MovieId)
               || (recommendation.TmdbId.HasValue && collectionItem.TmdbId == recommendation.TmdbId)
               || (!string.IsNullOrWhiteSpace(recommendation.ImdbId)
                   && string.Equals(
                       recommendation.ImdbId.Trim(),
                       collectionItem.ImdbId?.Trim(),
                       StringComparison.OrdinalIgnoreCase))
               || (collectionItem.ReleaseYear == recommendation.ReleaseYear
                   && string.Equals(
                       NormalizeTitle(collectionItem.Title),
                       NormalizeTitle(recommendation.Title),
                       StringComparison.Ordinal));
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var chars = title.Trim().ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch >= 0x4e00 && ch <= 0x9fff);
        return string.Concat(chars);
    }
}
