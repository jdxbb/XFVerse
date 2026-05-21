using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Net.Http;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class MovieDetailViewModel : PageViewModelBase
{
    private const string ExternalAiAnalyzingText = "AI 正在分析影片";
    private const string ExternalAiMissingText = "尚未分类";
    private const string CorrectionTargetMovieText = "修正为电影";
    private const string CorrectionTargetTvEpisodeText = "修正为电视剧集";
    private static readonly TimeSpan CorrectionApplyTimeout = TimeSpan.FromSeconds(45);

    private static readonly ConcurrentDictionary<int, AiMovieTags> ExternalAiTagCache = new();

    private readonly INavigationStateService _navigationStateService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly IMovieDetailQueryService _movieDetailQueryService;
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
    private bool _isProbeCompletionRefreshQueued;
    private AiRecommendationItem? _externalRecommendation;
    private int? _movieId;
    private int? _tmdbId;
    private string _title = "未选择影片";
    private string _originalTitle = "-";
    private string _releaseYearText = "-";
    private string _overview = "请先从资源库中选择一部影片。";
    private string _posterRemoteUrl = string.Empty;
    private string _country = "-";
    private string _language = "-";
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
    private string _wantToWatchButtonText = "+ 想看";
    private string _notInterestedButtonText = "不想看";
    private string _manualSearchQuery = string.Empty;
    private string _manualSearchYear = string.Empty;
    private string _tvCorrectionQuery = string.Empty;
    private string _correctionSeasonNumber = "1";
    private string _correctionEpisodeNumber = "1";
    private string _selectedCorrectionTarget = CorrectionTargetMovieText;
    private string _correctionSourceDisplay = "请选择一个播放源。";
    private string _correctionPreviewText = string.Empty;
    private string _correctionSourceFileName = string.Empty;
    private int? _correctionMediaFileId;
    private int _selectedDetailTabIndex;
    private IdentificationStatus _identificationStatus;
    private bool _hasMovie;
    private bool _isLibraryMovie;
    private bool _canPlay;
    private bool _isOpeningPlayer;
    private bool _isTogglingWatched;
    private bool _isTogglingWantToWatch;
    private bool _isTogglingNotInterested;
    private bool _isFavorite;
    private bool _isWatched;
    private bool _isWantToWatch;
    private bool _isNotInterested;
    private bool _isVisibleInLibrary;
    private bool _isAddingToLibrary;
    private bool _hasCorrectionPreview;
    private bool _isCorrectionBusy;
    private LibraryVisibilityState _libraryVisibilityState = LibraryVisibilityState.Auto;

    public MovieDetailViewModel(
        INavigationStateService navigationStateService,
        IPlayerWindowService playerWindowService,
        IMovieDetailQueryService movieDetailQueryService,
        ITmdbService tmdbService,
        IMovieIdentificationService movieIdentificationService,
        ISingleSourceCorrectionService singleSourceCorrectionService,
        IMovieManagementService movieManagementService,
        IAiClassificationService aiClassificationService,
        IDataRefreshService dataRefreshService,
        IUserCollectionService userCollectionService,
        IMediaProbeService mediaProbeService,
        IConfirmationDialogService confirmationDialogService)
        : base("详情", "查看影片信息、播放源、字幕、评分、识别修正和观看记录。")
    {
        _navigationStateService = navigationStateService;
        _playerWindowService = playerWindowService;
        _movieDetailQueryService = movieDetailQueryService;
        _tmdbService = tmdbService;
        _movieIdentificationService = movieIdentificationService;
        _singleSourceCorrectionService = singleSourceCorrectionService;
        _movieManagementService = movieManagementService;
        _aiClassificationService = aiClassificationService;
        _dataRefreshService = dataRefreshService;
        _userCollectionService = userCollectionService;
        _mediaProbeService = mediaProbeService;
        _confirmationDialogService = confirmationDialogService;

        SearchCandidatesCommand = new AsyncRelayCommand(SearchCandidatesAsync);
        ApplyManualMatchCommand = new AsyncRelayCommand(ApplyMovieCandidateCorrectionAsync, CanApplyMovieCandidateCorrection);
        BeginSourceCorrectionCommand = new RelayCommand(BeginSourceCorrection, CanBeginSourceCorrection);
        PreviewTvEpisodeCorrectionCommand = new AsyncRelayCommand(ApplyTvEpisodeCandidateCorrectionAsync, CanApplyTvEpisodeCandidateCorrection);
        CancelCorrectionCommand = new RelayCommand(CancelCorrection, () => IsCorrectionPanelVisible);
        SetDefaultSourceCommand = new AsyncRelayCommand(SetDefaultSourceAsync);
        ResetSourceRecognitionCommand = new AsyncRelayCommand(ResetSourceRecognitionAsync, CanResetSourceRecognition);
        ManualProbeSourceCommand = new RelayCommand(parameter => _ = ManualProbeSourceAsync(parameter), CanManualProbeSource);
        OpenPlayerCommand = new AsyncRelayCommand(OpenPlayerAsync, _ => CanOpenPlayer);
        ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync);
        ToggleWatchedCommand = new AsyncRelayCommand(ToggleWatchedAsync, () => CanToggleWatched);
        ToggleWantToWatchCommand = new AsyncRelayCommand(ToggleWantToWatchAsync, () => CanToggleWantToWatch);
        ToggleNotInterestedCommand = new AsyncRelayCommand(ToggleNotInterestedAsync, () => CanToggleNotInterested);
        AddToLibraryCommand = new AsyncRelayCommand(AddToLibraryAsync, () => CanAddToLibrary);
        AiSuggestSearchCommand = new AsyncRelayCommand(AiSuggestSearchAsync, CanAiSuggestSearch);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());

        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;
        _mediaProbeService.ProbeStatusChanged += OnProbeStatusChanged;
    }

    public ObservableCollection<MovieRatingItem> Ratings { get; } = [];

    public ObservableCollection<MovieSourceItem> Sources { get; } = [];

    public ObservableCollection<MetadataSearchCandidate> SearchCandidates { get; } = [];

    public ObservableCollection<TmdbTvSeriesSearchItem> TvSearchCandidates { get; } = [];

    public IReadOnlyList<string> CorrectionTargetOptions { get; } =
    [
        CorrectionTargetMovieText,
        CorrectionTargetTvEpisodeText
    ];

    public AsyncRelayCommand SearchCandidatesCommand { get; }

    public AsyncRelayCommand ApplyManualMatchCommand { get; }

    public RelayCommand BeginSourceCorrectionCommand { get; }

    public AsyncRelayCommand PreviewTvEpisodeCorrectionCommand { get; }

    public RelayCommand CancelCorrectionCommand { get; }

    public AsyncRelayCommand SetDefaultSourceCommand { get; }

    public AsyncRelayCommand ResetSourceRecognitionCommand { get; }

    public RelayCommand ManualProbeSourceCommand { get; }

    public AsyncRelayCommand OpenPlayerCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand ToggleWatchedCommand { get; }

    public AsyncRelayCommand ToggleWantToWatchCommand { get; }

    public AsyncRelayCommand ToggleNotInterestedCommand { get; }

    public AsyncRelayCommand AddToLibraryCommand { get; }

    public AsyncRelayCommand AiSuggestSearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string TitleText { get => _title; private set => SetProperty(ref _title, value); }

    public string OriginalTitle { get => _originalTitle; private set => SetProperty(ref _originalTitle, value); }

    public string ReleaseYearText { get => _releaseYearText; private set => SetProperty(ref _releaseYearText, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string PosterRemoteUrl { get => _posterRemoteUrl; private set => SetProperty(ref _posterRemoteUrl, value); }

    public string Country { get => _country; private set => SetProperty(ref _country, value); }

    public string Language { get => _language; private set => SetProperty(ref _language, value); }

    public string RuntimeText { get => _runtimeText; private set => SetProperty(ref _runtimeText, value); }

    public string GenresText { get => _genresText; private set => SetProperty(ref _genresText, value); }

    public string AiTagsText { get => _aiTagsText; private set => SetProperty(ref _aiTagsText, value); }

    public string EmotionTagsText { get => _emotionTagsText; private set => SetProperty(ref _emotionTagsText, value); }

    public string SceneTagsText { get => _sceneTagsText; private set => SetProperty(ref _sceneTagsText, value); }

    public string IdentificationStatusText { get => _identificationStatusText; private set => SetProperty(ref _identificationStatusText, value); }

    public string ConfidenceText { get => _confidenceText; private set => SetProperty(ref _confidenceText, value); }

    public string TmdbIdText { get => _tmdbIdText; private set => SetProperty(ref _tmdbIdText, value); }

    public string ImdbIdText { get => _imdbIdText; private set => SetProperty(ref _imdbIdText, value); }

    public string DefaultSourceDisplay { get => _defaultSourceDisplay; private set => SetProperty(ref _defaultSourceDisplay, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public string AvailabilityText { get => _availabilityText; private set => SetProperty(ref _availabilityText, value); }

    public string PlayButtonText { get => _playButtonText; private set => SetProperty(ref _playButtonText, value); }

    public string FavoriteButtonText { get => _favoriteButtonText; private set => SetProperty(ref _favoriteButtonText, value); }

    public string WatchedButtonText { get => _watchedButtonText; private set => SetProperty(ref _watchedButtonText, value); }

    public string WantToWatchButtonText { get => _wantToWatchButtonText; private set => SetProperty(ref _wantToWatchButtonText, value); }

    public string NotInterestedButtonText { get => _notInterestedButtonText; private set => SetProperty(ref _notInterestedButtonText, value); }

    public string ManualSearchQuery { get => _manualSearchQuery; set => SetProperty(ref _manualSearchQuery, value); }

    public string ManualSearchYear { get => _manualSearchYear; set => SetProperty(ref _manualSearchYear, value); }

    public string TvCorrectionQuery { get => _tvCorrectionQuery; set => SetProperty(ref _tvCorrectionQuery, value); }

    public string CorrectionSeasonNumber { get => _correctionSeasonNumber; set => SetProperty(ref _correctionSeasonNumber, value); }

    public string CorrectionEpisodeNumber { get => _correctionEpisodeNumber; set => SetProperty(ref _correctionEpisodeNumber, value); }

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
                ClearCandidatesForInactiveCorrectionTarget();
                ApplyManualMatchCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrectionSourceDisplay { get => _correctionSourceDisplay; private set => SetProperty(ref _correctionSourceDisplay, value); }

    public string CorrectionPreviewText { get => _correctionPreviewText; private set => SetProperty(ref _correctionPreviewText, value); }

    public bool IsCorrectionPanelVisible => _correctionMediaFileId.HasValue;

    public bool IsCorrectionTargetMovie => SelectedCorrectionTarget == CorrectionTargetMovieText;

    public bool IsCorrectionTargetTvEpisode => SelectedCorrectionTarget == CorrectionTargetTvEpisodeText;

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
                ApplyManualMatchCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }
        }
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
                OnPropertyChanged(nameof(ShowExternalInfoSection));
                OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
                OnPropertyChanged(nameof(ShowExternalWantToWatchAction));
                OnPropertyChanged(nameof(ShowNotInterestedAction));
                OnPropertyChanged(nameof(ShowWatchedAction));
                OnPropertyChanged(nameof(ShowAddToLibraryAction));
                RefreshWantToWatchCommandState();
                RefreshNotInterestedCommandState();
                RefreshWatchedCommandState();
                RefreshAddToLibraryCommandState();
                RefreshResetSourceRecognitionCommandState();
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
                OnPropertyChanged(nameof(ShowExternalInfoSection));
                OnPropertyChanged(nameof(ShowCollectionActions));
                OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
                OnPropertyChanged(nameof(ShowExternalWantToWatchAction));
                OnPropertyChanged(nameof(ShowNotInterestedAction));
                OnPropertyChanged(nameof(ShowWatchedAction));
                OnPropertyChanged(nameof(ShowAddToLibraryAction));
                RefreshWantToWatchCommandState();
                RefreshNotInterestedCommandState();
                RefreshWatchedCommandState();
                RefreshAddToLibraryCommandState();
                RefreshResetSourceRecognitionCommandState();
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

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                FavoriteButtonText = value ? "取消喜爱" : "喜爱";
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
                RefreshWantToWatchCommandState();
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
                WantToWatchButtonText = value ? "取消想看" : "+ 想看";
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
            }
        }
    }

    public bool HasSearchCandidates => SearchCandidates.Count > 0;

    public bool HasTvSearchCandidates => TvSearchCandidates.Count > 0;

    public bool CanUseIdentificationCorrection => HasMovie && IsLibraryMovie;

    public bool ShowLibrarySections => HasMovie && IsLibraryMovie;

    public bool ShowExternalInfoSection => HasMovie && !IsLibraryMovie;

    public bool ShowCollectionActions => IsLibraryMovie;

    public bool ShowExternalWantToWatchAction => HasMovie && !IsLibraryMovie;

    public bool ShowNotInterestedAction => HasMovie;

    public bool ShowWatchedAction => HasMovie && (IsLibraryMovie || _externalRecommendation is not null);

    public bool ShowAddToLibraryAction => HasMovie && !IsVisibleInLibrary;

    public bool CanAddToLibrary => ShowAddToLibraryAction && !_isAddingToLibrary;

    public string AddToLibraryButtonText => _libraryVisibilityState == LibraryVisibilityState.Hidden
        ? "恢复到媒体库"
        : "加入媒体库";

    public bool IsVisibleInLibrary
    {
        get => _isVisibleInLibrary;
        private set
        {
            if (SetProperty(ref _isVisibleInLibrary, value))
            {
                OnPropertyChanged(nameof(ShowAddToLibraryAction));
                RefreshAddToLibraryCommandState();
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
                OnPropertyChanged(nameof(AddToLibraryButtonText));
            }
        }
    }

    public bool CanToggleWatched => ShowWatchedAction && !_isTogglingWatched;

    public bool CanToggleWantToWatch => ShowExternalWantToWatchAction
                                        && !_isTogglingWantToWatch
                                        && !IsWatched
                                        && _externalRecommendation is not null;

    public bool CanToggleNotInterested => ShowNotInterestedAction && !_isTogglingNotInterested;

    public bool ShowRatingsAndTagsTab => ShowLibrarySections
                                         && _identificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed;

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

    private async Task LoadMovieAsync(int movieId, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _movieDetailQueryService.GetMovieDetailAsync(movieId, cancellationToken);
            if (detail is null)
            {
                ClearMovieState("未找到对应影片详情，可能已被删除。");
                return;
            }

            var isNewMovie = _movieId != detail.MovieId;
            _movieId = detail.MovieId;
            _tmdbId = detail.TmdbId;
            _externalRecommendation = null;
            HasMovie = true;
            IsLibraryMovie = true;
            IsFavorite = detail.IsFavorite;
            IsWatched = detail.IsWatched;
            IsWantToWatch = false;
            IsNotInterested = detail.IsNotInterested;
            IsVisibleInLibrary = detail.IsVisibleInLibrary;
            CurrentLibraryVisibilityState = detail.LibraryVisibilityState;
            RefreshWantToWatchCommandState();
            RefreshNotInterestedCommandState();
            RefreshWatchedCommandState();
            RefreshAddToLibraryCommandState();
            AvailabilityText = "已入库 / 可播放";
            TitleText = detail.Title;
            OriginalTitle = string.IsNullOrWhiteSpace(detail.OriginalTitle) ? "-" : detail.OriginalTitle;
            ReleaseYearText = detail.ReleaseYear?.ToString() ?? "-";
            Overview = string.IsNullOrWhiteSpace(detail.Overview) ? "暂无简介。" : detail.Overview;
            PosterRemoteUrl = detail.PosterRemoteUrl;
            Country = string.IsNullOrWhiteSpace(detail.Country) ? "-" : detail.Country;
            Language = string.IsNullOrWhiteSpace(detail.Language) ? "-" : detail.Language;
            RuntimeText = detail.RuntimeMinutes.HasValue ? $"{detail.RuntimeMinutes.Value} 分钟" : "-";
            GenresText = string.IsNullOrWhiteSpace(detail.GenresText) ? "未提供" : detail.GenresText;
            AiTagsText = string.IsNullOrWhiteSpace(detail.AiTagsText) ? "尚未分类" : detail.AiTagsText;
            EmotionTagsText = string.IsNullOrWhiteSpace(detail.EmotionTagsText) ? "尚未分类" : detail.EmotionTagsText;
            SceneTagsText = string.IsNullOrWhiteSpace(detail.SceneTagsText) ? "尚未分类" : detail.SceneTagsText;
            IdentificationStatusText = GetIdentificationStatusText(detail.IdentificationStatus);
            _identificationStatus = detail.IdentificationStatus;
            RefreshResetSourceRecognitionCommandState();
            if (_identificationStatus is not (IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed))
            {
                AiTagsText = string.Empty;
                EmotionTagsText = string.Empty;
                SceneTagsText = string.Empty;
            }

            OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
            ConfidenceText = detail.IdentifiedConfidence.HasValue ? $"{detail.IdentifiedConfidence:P0}" : "-";
            TmdbIdText = detail.TmdbId?.ToString() ?? "-";
            ImdbIdText = string.IsNullOrWhiteSpace(detail.ImdbId) ? "-" : detail.ImdbId;

            Ratings.Clear();
            foreach (var rating in detail.Ratings)
            {
                Ratings.Add(ToDisplayRating(rating));
            }

            Sources.Clear();
            foreach (var source in detail.Sources)
            {
                Sources.Add(source);
            }
            RefreshResetSourceRecognitionCommandState();

            CanPlay = Sources.Count > 0;
            PlayButtonText = CanPlay ? "播放默认源" : "暂无可播放源";

            if (isNewMovie)
            {
                SearchCandidates.Clear();
                TvSearchCandidates.Clear();
                _correctionMediaFileId = null;
                OnPropertyChanged(nameof(IsCorrectionPanelVisible));
                SelectedDetailTabIndex = 0;
                SelectedCorrectionTarget = CorrectionTargetMovieText;
                CorrectionSourceDisplay = "请选择一个播放源。";
                _correctionSourceFileName = string.Empty;
                ClearCorrectionPreview();
                OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(HasTvSearchCandidates));
                ManualSearchQuery = detail.Title;
                ManualSearchYear = detail.ReleaseYear?.ToString() ?? string.Empty;
                TvCorrectionQuery = detail.Title;
            }
            else if (_correctionMediaFileId.HasValue && Sources.All(source => source.MediaFileId != _correctionMediaFileId.Value))
            {
                _correctionMediaFileId = null;
                _correctionSourceFileName = string.Empty;
                OnPropertyChanged(nameof(IsCorrectionPanelVisible));
                ClearCorrectionPreview();
                SearchCandidates.Clear();
                TvSearchCandidates.Clear();
                OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(HasTvSearchCandidates));
                CancelCorrectionCommand.RaiseCanExecuteChanged();
                ApplyManualMatchCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }

            var defaultSource = detail.Sources.FirstOrDefault(source => source.IsDefault);
            DefaultSourceDisplay = defaultSource is null
                ? "尚未设置默认播放源"
                : $"{defaultSource.SourceTypeText} · {defaultSource.FileName} ({defaultSource.Extension})";

            StatusMessage = detail.IdentificationStatus switch
            {
                IdentificationStatus.NeedsReview => "该影片识别置信度较低，建议在识别修正中人工确认。",
                IdentificationStatus.Failed => "该影片尚未识别成功，可使用人工修正或 AI 辅助识别。",
                _ => "详情已加载。"
            };
            ScheduleDetailLazyProbe(detail.MovieId, detail.Sources);

            if (NeedsAutoClassification(detail))
            {
                await _aiClassificationService.ClassifyMovieAsync(detail.MovieId, cancellationToken);
                var classified = await _movieDetailQueryService.GetMovieDetailAsync(movieId, cancellationToken);
                if (classified is not null)
                {
                    AiTagsText = string.IsNullOrWhiteSpace(classified.AiTagsText) ? AiTagsText : classified.AiTagsText;
                    EmotionTagsText = string.IsNullOrWhiteSpace(classified.EmotionTagsText) ? EmotionTagsText : classified.EmotionTagsText;
                    SceneTagsText = string.IsNullOrWhiteSpace(classified.SceneTagsText) ? SceneTagsText : classified.SceneTagsText;
                    StatusMessage = "详情已加载，AI 分类已自动更新。";
                }
            }
        }
        catch (Exception exception)
        {
            ClearMovieState($"加载影片详情失败：{DescribeException(exception)}");
        }
    }

    private void ScheduleDetailLazyProbe(int movieId, IReadOnlyCollection<MovieSourceItem> sources)
    {
        var mediaFileIds = sources
            .Select(source => source.MediaFileId)
            .Where(mediaFileId => _lazyProbeCheckedMediaFileIds.Add(mediaFileId))
            .ToArray();
        if (mediaFileIds.Length == 0)
        {
            return;
        }

        _ = RunDetailLazyProbeAsync(movieId, mediaFileIds);
    }

    private async Task RunDetailLazyProbeAsync(int movieId, IReadOnlyCollection<int> mediaFileIds)
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
                cancellationToken: CancellationToken.None);
            var activeProbeMediaFileIds = result.ProbeMediaFileIds.ToHashSet();
            SetProbeBusyState(
                requestedMediaFileIds.Where(mediaFileId => !activeProbeMediaFileIds.Contains(mediaFileId)),
                isBusy: false);
            if (result.QueuedCount <= 0)
            {
                return;
            }

            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-refresh contentKind=movie queuedCount={result.QueuedCount} refreshStrategy=probe-status-changed-event");
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
        ApplyCachedExternalTags(recommendation);
        var shouldAutoClassify = NeedsExternalAutoClassification(recommendation);

        _movieId = null;
        _tmdbId = recommendation.TmdbId;
        _externalRecommendation = recommendation;
        HasMovie = true;
        IsLibraryMovie = false;
        IsVisibleInLibrary = false;
        CurrentLibraryVisibilityState = LibraryVisibilityState.Auto;
        _identificationStatus = IdentificationStatus.Pending;
        OnPropertyChanged(nameof(ShowRatingsAndTagsTab));
        IsFavorite = false;
        IsWatched = recommendation.IsWatched;
        IsWantToWatch = recommendation.IsWantToWatch;
        IsNotInterested = recommendation.IsNotInterested;
        CanPlay = false;
        AvailabilityText = "未入库 / 无法播放";
        PlayButtonText = "该影片未入库，无法播放";
        TitleText = recommendation.Title;
        OriginalTitle = string.IsNullOrWhiteSpace(recommendation.OriginalTitle) ? "-" : recommendation.OriginalTitle;
        ReleaseYearText = recommendation.ReleaseYear?.ToString() ?? "-";
        Overview = string.IsNullOrWhiteSpace(recommendation.Overview) ? recommendation.Reason : recommendation.Overview;
        PosterRemoteUrl = recommendation.PosterRemoteUrl;
        Country = string.IsNullOrWhiteSpace(recommendation.Country) ? "-" : recommendation.Country;
        Language = string.IsNullOrWhiteSpace(recommendation.Language) ? "-" : recommendation.Language;
        RuntimeText = recommendation.RuntimeMinutes.HasValue ? $"{recommendation.RuntimeMinutes.Value} 分钟" : "-";
        if (shouldAutoClassify)
        {
            ShowExternalAiAnalyzingState(recommendation);
        }
        else
        {
            ApplyExternalTagDisplay(recommendation, ExternalAiMissingText);
        }
        IdentificationStatusText = "库外推荐";
        ConfidenceText = "-";
        TmdbIdText = recommendation.TmdbId?.ToString() ?? "-";
        ImdbIdText = string.IsNullOrWhiteSpace(recommendation.ImdbId) ? "-" : recommendation.ImdbId;
        DefaultSourceDisplay = "影片未入库，暂无播放源。";
        StatusMessage = shouldAutoClassify
            ? "当前页面展示的是未入库影片详情，AI 正在分析影片。"
            : "当前页面展示的是未入库影片详情，仅展示评分、标签与基础信息，无法播放。";
        ManualSearchQuery = recommendation.Title;
        ManualSearchYear = recommendation.ReleaseYear?.ToString() ?? string.Empty;

        Ratings.Clear();
        if (recommendation.TmdbRating.HasValue)
        {
            Ratings.Add(new MovieRatingItem
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
            Ratings.Add(ToDisplayRating(recommendation.OmdbRating));
        }

        Sources.Clear();
        SearchCandidates.Clear();
        OnPropertyChanged(nameof(HasSearchCandidates));
        await RefreshExternalWantToWatchStateAsync(cancellationToken);
        RefreshWantToWatchCommandState();
        RefreshNotInterestedCommandState();
        RefreshWatchedCommandState();
        if (shouldAutoClassify)
        {
            await ClassifyExternalRecommendationAsync(recommendation, cancellationToken);
        }
    }

    private async Task ClassifyExternalRecommendationAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken)
    {
        ShowExternalAiAnalyzingState(recommendation);
        StatusMessage = "当前页面展示的是未入库影片详情，正在生成 AI 标签。";
        try
        {
            var tags = await _aiClassificationService.ClassifyExternalMovieAsync(recommendation, cancellationToken);
            recommendation.Tags = string.IsNullOrWhiteSpace(tags.AiTagsText) ? recommendation.Tags : tags.AiTagsText;
            recommendation.EmotionTagsText = string.IsNullOrWhiteSpace(tags.EmotionTagsText) ? recommendation.EmotionTagsText : tags.EmotionTagsText;
            recommendation.SceneTagsText = string.IsNullOrWhiteSpace(tags.SceneTagsText) ? recommendation.SceneTagsText : tags.SceneTagsText;
            CacheExternalTags(recommendation);
            ApplyExternalTagDisplay(recommendation, ExternalAiMissingText);
            StatusMessage = "当前页面展示的是未入库影片详情，AI 标签已自动生成。";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            ApplyExternalTagDisplay(recommendation, ExternalAiMissingText);
            StatusMessage = $"未入库影片 AI 标签生成失败：{DescribeException(exception)}";
        }
    }

    private async Task OpenPlayerAsync(object? parameter)
    {
        if (!IsLibraryMovie)
        {
            StatusMessage = "该影片未入库，无法播放。";
            return;
        }

        if (!HasMovie || _movieId is null)
        {
            StatusMessage = "请先选择影片。";
            return;
        }

        if (!CanPlay)
        {
            StatusMessage = "当前影片没有可播放源。";
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

    private void RefreshWantToWatchCommandState()
    {
        OnPropertyChanged(nameof(CanToggleWantToWatch));
        ToggleWantToWatchCommand?.RaiseCanExecuteChanged();
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
        if (!IsLibraryMovie || _movieId is null)
        {
            StatusMessage = "库外影片不能在此详情页标记喜爱。";
            return;
        }

        var targetFavorite = !IsFavorite;
        if (targetFavorite && !IsWatched)
        {
            StatusMessage = "只有已看影片可以标记喜爱。";
            return;
        }

        await _movieManagementService.SetFavoriteAsync(_movieId.Value, targetFavorite);
        IsFavorite = targetFavorite;
        if (IsFavorite)
        {
            IsNotInterested = false;
        }

        StatusMessage = IsFavorite ? "已标记为喜爱。" : "已取消喜爱。";
        _dataRefreshService.NotifyCollectionChanged();
        NotifyRecommendationChangedIfCurrentMovieAffectsAiRecommendation();
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
                if (_libraryVisibilityState == LibraryVisibilityState.Hidden)
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
                StatusMessage = "已恢复到媒体库。";
                return;
            }

            if (_externalRecommendation is null)
            {
                StatusMessage = "缺少外部影片信息，无法加入媒体库。";
                return;
            }

            if (_libraryVisibilityState == LibraryVisibilityState.Hidden)
            {
                await _userCollectionService.RestoreToLibraryAsync(_externalRecommendation, changeSource: "MovieDetail");
            }
            else
            {
                await _userCollectionService.AddToLibraryAsync(_externalRecommendation, changeSource: "MovieDetail");
            }
            IsVisibleInLibrary = true;
            CurrentLibraryVisibilityState = _libraryVisibilityState == LibraryVisibilityState.Hidden
                                     && (IsWatched || IsWantToWatch || IsNotInterested)
                ? LibraryVisibilityState.Auto
                : LibraryVisibilityState.Visible;
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            StatusMessage = "已加入媒体库。";
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
        var targetWatched = !previousWatched;
        SetTogglingWatched(true);
        try
        {
            IsWatched = targetWatched;
            if (!targetWatched)
            {
                IsFavorite = false;
            }

            await _movieManagementService.SetWatchedAsync(_movieId.Value, targetWatched);
            StatusMessage = IsWatched ? "已标记为已看。" : "已标记为未看。";
            _dataRefreshService.NotifyMetadataChanged();
            _dataRefreshService.NotifyCollectionChanged();
            NotifyRecommendationChangedIfCurrentMovieAffectsAiRecommendation();
        }
        catch (Exception exception)
        {
            IsWatched = previousWatched;
            IsFavorite = previousFavorite;
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
            StatusMessage = "只有未入库影片可以在此切换库外观看状态。";
            return;
        }

        var previousWatched = IsWatched;
        var previousWantToWatch = IsWantToWatch;
        var targetWatched = !previousWatched;
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

            await _userCollectionService.SetWatchedAsync(_externalRecommendation, targetWatched);
            StatusMessage = targetWatched ? "已标记为已看。" : "已标记为未看。";
            _dataRefreshService.NotifyCollectionChanged();
            NotifyRecommendationChangedIfCurrentMovieAffectsAiRecommendation();
        }
        catch (Exception exception)
        {
            IsWatched = previousWatched;
            IsWantToWatch = previousWantToWatch;
            _externalRecommendation.IsWatched = previousWatched;
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
        if (_externalRecommendation is null || IsLibraryMovie)
        {
            StatusMessage = "只有未入库影片可以在此切换想看状态。";
            return;
        }

        var previousState = IsWantToWatch;
        var previousNotInterested = IsNotInterested;
        SetTogglingWantToWatch(true);
        try
        {
            IsWantToWatch = !previousState;
            _externalRecommendation.IsWantToWatch = IsWantToWatch;

            if (IsWantToWatch)
            {
                await _userCollectionService.AddWantToWatchAsync(_externalRecommendation);
                IsNotInterested = false;
                _externalRecommendation.IsNotInterested = false;
                StatusMessage = "已加入想看。";
            }
            else
            {
                await _userCollectionService.RemoveWantToWatchAsync(_externalRecommendation);
                StatusMessage = "已取消想看。";
            }

            _dataRefreshService.NotifyCollectionChanged();
            NotifyRecommendationChangedIfCurrentMovieAffectsAiRecommendation();
        }
        catch (Exception exception)
        {
            IsWantToWatch = previousState;
            IsNotInterested = previousNotInterested;
            _externalRecommendation.IsWantToWatch = previousState;
            _externalRecommendation.IsNotInterested = previousNotInterested;
            StatusMessage = $"想看状态更新失败：{DescribeException(exception)}";
        }
        finally
        {
            SetTogglingWantToWatch(false);
        }
    }

    private async Task ToggleNotInterestedAsync()
    {
        if (!HasMovie)
        {
            StatusMessage = "请先选择影片。";
            return;
        }

        var previousNotInterested = IsNotInterested;
        var previousWantToWatch = IsWantToWatch;
        var previousFavorite = IsFavorite;
        var targetNotInterested = !previousNotInterested;
        SetTogglingNotInterested(true);
        try
        {
            if (IsLibraryMovie)
            {
                if (_movieId is null)
                {
                    StatusMessage = "请先选择影片。";
                    return;
                }

                await _userCollectionService.SetNotInterestedAsync(_movieId.Value, targetNotInterested);
            }
            else
            {
                if (_externalRecommendation is null)
                {
                    StatusMessage = "只有未入库影片可以在此切换不想看状态。";
                    return;
                }

                await _userCollectionService.SetNotInterestedAsync(_externalRecommendation, targetNotInterested);
                _externalRecommendation.IsNotInterested = targetNotInterested;
            }

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

            StatusMessage = targetNotInterested ? "已标记为不想看。" : "已取消不想看。";
            _dataRefreshService.NotifyCollectionChanged();
            _dataRefreshService.NotifyRecommendationChanged();
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

    private async Task RefreshExternalWantToWatchStateAsync(CancellationToken cancellationToken)
    {
        if (_externalRecommendation is null)
        {
            IsWantToWatch = false;
            IsWatched = false;
            IsNotInterested = false;
            return;
        }

        var collectionItems = await _userCollectionService.GetCollectionItemsAsync(cancellationToken);
        var isNotInterested = await _userCollectionService.IsNotInterestedAsync(_externalRecommendation, cancellationToken);
        var collectionItem = collectionItems.FirstOrDefault(x => IsSameRecommendation(x, _externalRecommendation));
        var isWatched = collectionItem is null ? _externalRecommendation.IsWatched : collectionItem.IsWatched;
        var isWantToWatch = collectionItem is null
            ? _externalRecommendation.IsWantToWatch && !isWatched && !isNotInterested
            : collectionItem.IsWantToWatch && !isWatched;
        IsWatched = isWatched;
        IsWantToWatch = isWantToWatch;
        IsNotInterested = isNotInterested;
        _externalRecommendation.IsWatched = isWatched;
        _externalRecommendation.IsWantToWatch = IsWantToWatch;
        _externalRecommendation.IsNotInterested = IsNotInterested;
    }

    private void SetTogglingWantToWatch(bool value)
    {
        if (_isTogglingWantToWatch == value)
        {
            return;
        }

        _isTogglingWantToWatch = value;
        RefreshWantToWatchCommandState();
    }

    private void SetTogglingNotInterested(bool value)
    {
        if (_isTogglingNotInterested == value)
        {
            return;
        }

        _isTogglingNotInterested = value;
        RefreshNotInterestedCommandState();
    }

    private void SetTogglingWatched(bool value)
    {
        if (_isTogglingWatched == value)
        {
            return;
        }

        _isTogglingWatched = value;
        RefreshWatchedCommandState();
    }

    private async Task AiSuggestSearchAsync()
    {
        if (!CanAiSuggestSearch())
        {
            StatusMessage = "请先在播放源列表中选择要修正的单个播放源。";
            return;
        }

        var targetKind = IsCorrectionTargetTvEpisode ? "tv-episode" : "movie";
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-started page=movie targetKind={targetKind} mediaFileId={_correctionMediaFileId}");

        try
        {
            IsCorrectionBusy = true;
            if (IsCorrectionTargetTvEpisode)
            {
                await AiSuggestTvSearchAsync();
            }
            else
            {
                await AiSuggestMovieSearchAsync();
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "AI 辅助搜索已取消。";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-failed page=movie targetKind={targetKind} mediaFileId={_correctionMediaFileId} reason=cancelled");
        }
        catch (Exception exception)
        {
            StatusMessage = $"AI 辅助搜索失败：{DescribeException(exception)}";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-failed page=movie targetKind={targetKind} mediaFileId={_correctionMediaFileId} reason=exception");
        }
        finally
        {
            IsCorrectionBusy = false;
        }
    }

    private async Task AiSuggestMovieSearchAsync()
    {
        var releaseYear = int.TryParse(ManualSearchYear, out var parsedYear) ? parsedYear : (int?)null;
        var suggestionResult = await _aiClassificationService.SuggestMovieCorrectionSearchQueryAsync(
            TitleText,
            _correctionSourceFileName,
            releaseYear,
            Overview);
        var suggestion = suggestionResult.Status == AiSearchSuggestionStatus.Success
            ? suggestionResult.Suggestion
            : suggestionResult.FallbackSuggestion;

        ManualSearchQuery = suggestion.Query;
        ManualSearchYear = suggestion.ReleaseYear?.ToString() ?? string.Empty;
        StatusMessage = suggestionResult.Status == AiSearchSuggestionStatus.Success
            ? FormatAiSearchSuggestionStatus("电影", suggestion)
            : $"AI 未返回电影搜索词，已使用本地建议：{suggestion.Query}";
        await SearchCandidatesAsync();
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-succeeded page=movie targetKind=movie mediaFileId={_correctionMediaFileId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={SearchCandidates.Count}");
    }

    private async Task AiSuggestTvSearchAsync()
    {
        var suggestionResult = await _aiClassificationService.SuggestTvEpisodeCorrectionSearchQueryAsync(
            TitleText,
            _correctionSourceFileName,
            seriesTitle: TvCorrectionQuery,
            seasonNumber: TryParsePositiveOrZero(CorrectionSeasonNumber),
            episodeNumber: TryParsePositive(CorrectionEpisodeNumber),
            overview: Overview);
        var suggestion = suggestionResult.Status == AiSearchSuggestionStatus.Success
            ? suggestionResult.Suggestion
            : suggestionResult.FallbackSuggestion;

        TvCorrectionQuery = suggestion.Query;
        if (suggestion.SeasonNumber.HasValue && suggestion.SeasonNumber.Value >= 0)
        {
            CorrectionSeasonNumber = suggestion.SeasonNumber.Value.ToString();
        }

        if (suggestion.EpisodeNumber.HasValue && suggestion.EpisodeNumber.Value > 0)
        {
            CorrectionEpisodeNumber = suggestion.EpisodeNumber.Value.ToString();
        }

        StatusMessage = suggestionResult.Status == AiSearchSuggestionStatus.Success
            ? FormatAiTvSearchSuggestionStatus(suggestion)
            : $"AI 未返回电视剧搜索词，已使用本地建议：{suggestion.Query}";
        await SearchCandidatesAsync();
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-succeeded page=movie targetKind=tv-episode mediaFileId={_correctionMediaFileId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={TvSearchCandidates.Count}");
    }

    private bool CanAiSuggestSearch()
    {
        return !IsCorrectionBusy && CanUseIdentificationCorrection && IsCorrectionPanelVisible;
    }

    private bool CanBeginSourceCorrection(object? parameter)
    {
        return CanUseIdentificationCorrection
               && parameter is MovieSourceItem source
               && Sources.Any(item => item.MediaFileId == source.MediaFileId);
    }

    private void BeginSourceCorrection(object? parameter)
    {
        if (parameter is not MovieSourceItem source || !CanBeginSourceCorrection(parameter))
        {
            return;
        }

        _correctionMediaFileId = null;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = $"{source.SourceTypeText} · {source.FileName}";
        _correctionSourceFileName = source.FileName;
        ManualSearchQuery = string.IsNullOrWhiteSpace(ManualSearchQuery) ? TitleText : ManualSearchQuery;
        TvCorrectionQuery = string.IsNullOrWhiteSpace(TvCorrectionQuery) ? TitleText : TvCorrectionQuery;
        CorrectionSeasonNumber = "1";
        CorrectionEpisodeNumber = "1";
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        _correctionMediaFileId = source.MediaFileId;
        SelectedDetailTabIndex = 3;
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
        ApplyManualMatchCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = $"已选择播放源“{source.FileName}”，请搜索目标；点击候选后会直接修正。";
    }

    private void CancelCorrection()
    {
        _correctionMediaFileId = null;
        _correctionSourceFileName = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
        ApplyManualMatchCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = "已取消本次修正，未修改任何数据。";
    }

    private async Task SearchCandidatesAsync()
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
            await SearchTvCandidatesAsync();
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
            var releaseYear = int.TryParse(ManualSearchYear, out var parsedYear) ? parsedYear : (int?)null;
            var candidates = await _movieIdentificationService.SearchCandidatesAsync(query, releaseYear);

            SearchCandidates.Clear();
            foreach (var candidate in candidates)
            {
                SearchCandidates.Add(candidate);
            }

            OnPropertyChanged(nameof(HasSearchCandidates));
            StatusMessage = SearchCandidates.Count == 0
                ? "没有找到符合条件的 TMDB 结果。"
                : $"已找到 {SearchCandidates.Count} 个候选结果。";
        }
        catch (Exception exception)
        {
            SearchCandidates.Clear();
            OnPropertyChanged(nameof(HasSearchCandidates));
            StatusMessage = DescribeTmdbSearchFailure(exception);
        }
    }

    private async Task SearchTvCandidatesAsync()
    {
        var query = string.IsNullOrWhiteSpace(TvCorrectionQuery) ? TitleText : TvCorrectionQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            TvSearchCandidates.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = "请输入要搜索的电视剧名。";
            return;
        }

        try
        {
            var page = await _tmdbService.SearchTvSeriesAsync(query, 1);
            TvSearchCandidates.Clear();
            foreach (var candidate in page.Results.Take(12))
            {
                TvSearchCandidates.Add(candidate);
            }

            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = TvSearchCandidates.Count == 0
                ? "没有找到符合条件的 TMDB 电视剧结果。"
                : $"已找到 {TvSearchCandidates.Count} 个电视剧候选结果。";
        }
        catch (Exception exception)
        {
            TvSearchCandidates.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = DescribeTmdbSearchFailure(exception);
        }
    }

    private bool CanApplyMovieCandidateCorrection(object? parameter)
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetMovie
               && parameter is MetadataSearchCandidate;
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

    private bool CanApplyTvEpisodeCandidateCorrection(object? parameter)
    {
        return !IsCorrectionBusy
               && IsCorrectionPanelVisible
               && IsCorrectionTargetTvEpisode
               && parameter is TmdbTvSeriesSearchItem;
    }

    private async Task ApplyTvEpisodeCandidateCorrectionAsync(object? parameter)
    {
        if (_correctionMediaFileId is null || parameter is not TmdbTvSeriesSearchItem candidate)
        {
            StatusMessage = "请先选择播放源和电视剧候选。";
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

        try
        {
            IsCorrectionBusy = true;
            StatusMessage = $"正在修正为电视剧集：{candidate.Name} S{seasonNumber:00}E{episodeNumber:00}。";
            await Task.Yield();
            using var timeout = new CancellationTokenSource(CorrectionApplyTimeout);
            var mediaFileId = _correctionMediaFileId.Value;
            var result = await Task.Run(
                () => _singleSourceCorrectionService.ApplyTvEpisodeCorrectionAsync(
                    mediaFileId,
                    candidate.TmdbId,
                    seasonNumber,
                    episodeNumber,
                    timeout.Token),
                timeout.Token);
            _dataRefreshService.NotifyMetadataChanged();
            if (result.TargetEpisodeId.HasValue)
            {
                _navigationStateService.RequestEpisodeDetail(result.TargetEpisodeId.Value);
            }

            StatusMessage = $"已修正为电视剧集：{candidate.Name} S{seasonNumber:00}E{episodeNumber:00}";
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

    private void ClearCandidatesForInactiveCorrectionTarget()
    {
        if (IsCorrectionTargetMovie)
        {
            TvSearchCandidates.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            return;
        }

        SearchCandidates.Clear();
        OnPropertyChanged(nameof(HasSearchCandidates));
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

    private static string FormatAiTvSearchSuggestionStatus(AiSearchSuggestion suggestion)
    {
        var episodeText = suggestion.SeasonNumber.HasValue && suggestion.EpisodeNumber.HasValue
            ? $" S{suggestion.SeasonNumber.Value:00}E{suggestion.EpisodeNumber.Value:00}"
            : string.Empty;
        return $"AI 建议电视剧搜索：{suggestion.Query}{episodeText}";
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
        _correctionSourceFileName = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
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
        _movieId = null;
        _externalRecommendation = null;
        HasMovie = false;
        IsLibraryMovie = false;
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
        Overview = "请先从资源库中选择一部影片。";
        PosterRemoteUrl = string.Empty;
        Country = "-";
        Language = "-";
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
        ManualSearchQuery = string.Empty;
        ManualSearchYear = string.Empty;
        Ratings.Clear();
        Sources.Clear();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
        _correctionMediaFileId = null;
        _correctionSourceFileName = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetMovieText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
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

    private static void ApplyCachedExternalTags(AiRecommendationItem recommendation)
    {
        if (recommendation.TmdbId is not > 0
            || !ExternalAiTagCache.TryGetValue(recommendation.TmdbId.Value, out var cachedTags))
        {
            return;
        }

        recommendation.Tags = string.IsNullOrWhiteSpace(cachedTags.AiTagsText) ? recommendation.Tags : cachedTags.AiTagsText;
        recommendation.EmotionTagsText = string.IsNullOrWhiteSpace(cachedTags.EmotionTagsText)
            ? recommendation.EmotionTagsText
            : cachedTags.EmotionTagsText;
        recommendation.SceneTagsText = string.IsNullOrWhiteSpace(cachedTags.SceneTagsText)
            ? recommendation.SceneTagsText
            : cachedTags.SceneTagsText;
    }

    private static void CacheExternalTags(AiRecommendationItem recommendation)
    {
        if (recommendation.TmdbId is not > 0 || NeedsExternalAutoClassification(recommendation))
        {
            return;
        }

        ExternalAiTagCache[recommendation.TmdbId.Value] = new AiMovieTags
        {
            AiTagsText = recommendation.Tags,
            EmotionTagsText = recommendation.EmotionTagsText,
            SceneTagsText = recommendation.SceneTagsText
        };
    }

    private static bool NeedsExternalAutoClassification(AiRecommendationItem recommendation)
    {
        return string.IsNullOrWhiteSpace(recommendation.Tags)
               || string.IsNullOrWhiteSpace(recommendation.EmotionTagsText)
               || string.IsNullOrWhiteSpace(recommendation.SceneTagsText);
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
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
