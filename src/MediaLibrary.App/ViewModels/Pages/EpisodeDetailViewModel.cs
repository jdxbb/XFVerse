using System.Collections.ObjectModel;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class EpisodeDetailViewModel : PageViewModelBase
{
    private const string CorrectionTargetMovieText = "修正为电影";
    private const string CorrectionTargetTvEpisodeText = "修正为电视剧集";
    private const string CorrectionTargetUnknownSeasonText = "加入已有未识别季";

    private static readonly TimeSpan CorrectionApplyTimeout = TimeSpan.FromSeconds(45);

    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IMovieIdentificationService _movieIdentificationService;
    private readonly ITmdbService _tmdbService;
    private readonly ISingleSourceCorrectionService _singleSourceCorrectionService;
    private readonly IAiClassificationService _aiClassificationService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly IMediaProbeService _mediaProbeService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly HashSet<int> _lazyProbeCheckedMediaFileIds = [];
    private readonly HashSet<int> _probingMediaFileIds = [];
    private bool _isProbeCompletionRefreshQueued;
    private int? _episodeId;
    private int? _seasonId;
    private int? _defaultMediaFileId;
    private string _seriesName = "-";
    private string _seasonName = "-";
    private string _seasonNumberText = "-";
    private string _episodeNumberText = "-";
    private string _titleText = "未选择剧集";
    private string _overview = "请先选择一个剧集。";
    private string _airDateText = "-";
    private string _runtimeText = "-";
    private string _watchedText = "-";
    private string _progressText = "暂无进度";
    private string _sourceCountText = "暂无播放源";
    private string _sourceSummary = "暂无播放源";
    private string _lastPlayedText = "-";
    private string _identificationStatusText = "未加载";
    private string _statusMessage = "请先选择一个剧集。";
    private string _manualSearchQuery = string.Empty;
    private string _manualSearchYear = string.Empty;
    private string _tvCorrectionQuery = string.Empty;
    private string _correctionSeasonNumber = "1";
    private string _correctionEpisodeNumber = "1";
    private string _unknownSeasonSearchQuery = string.Empty;
    private string _unknownSeasonEpisodeNumber = "1";
    private string _selectedCorrectionTarget = CorrectionTargetTvEpisodeText;
    private string _correctionSourceDisplay = "请选择一个播放源。";
    private string _correctionPreviewText = string.Empty;
    private string _correctionSourceFileName = string.Empty;
    private string _correctionSourcePath = string.Empty;
    private int? _selectedTvCorrectionSeriesTmdbId;
    private string _selectedTvCorrectionSeriesName = string.Empty;
    private int? _selectedTvCorrectionSeasonNumber;
    private UnknownTvSeasonCorrectionTargetItem? _selectedUnknownSeasonTarget;
    private int? _correctionMediaFileId;
    private int _selectedDetailTabIndex;
    private bool _hasEpisode;
    private bool _isUnidentified;
    private bool _hasSources;
    private bool _isOpeningPlayer;
    private bool _isWatched;
    private bool _isUpdatingWatched;
    private bool _hasCorrectionPreview;
    private bool _isCorrectionBusy;
    private bool _isUnknownSeasonPickerDialogOpen;

    public EpisodeDetailViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IMovieIdentificationService movieIdentificationService,
        ITmdbService tmdbService,
        ISingleSourceCorrectionService singleSourceCorrectionService,
        IAiClassificationService aiClassificationService,
        IPlayerWindowService playerWindowService,
        IMediaProbeService mediaProbeService,
        IConfirmationDialogService confirmationDialogService,
        IDataRefreshService dataRefreshService)
        : base("剧集详情", "查看单集基础信息、识别状态、进度和播放源。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _movieIdentificationService = movieIdentificationService;
        _tmdbService = tmdbService;
        _singleSourceCorrectionService = singleSourceCorrectionService;
        _aiClassificationService = aiClassificationService;
        _playerWindowService = playerWindowService;
        _mediaProbeService = mediaProbeService;
        _confirmationDialogService = confirmationDialogService;
        _dataRefreshService = dataRefreshService;
        NavigateBackToSeasonCommand = new RelayCommand(NavigateBackToSeason, () => _seasonId.HasValue);
        OpenPlayerCommand = new AsyncRelayCommand(OpenPlayerAsync, _ => CanOpenPlayer);
        PlaySourceCommand = new AsyncRelayCommand(PlaySourceAsync, _ => CanOpenPlayer);
        ManualProbeSourceCommand = new RelayCommand(parameter => _ = ManualProbeSourceAsync(parameter), CanManualProbeSource);
        SetDefaultSourceCommand = new AsyncRelayCommand(SetDefaultSourceAsync, CanSetDefaultSource);
        ResetSourceRecognitionCommand = new AsyncRelayCommand(ResetSourceRecognitionAsync, CanResetSourceRecognition);
        ToggleWatchedCommand = new AsyncRelayCommand(ToggleWatchedAsync, () => CanToggleWatched);
        CorrectionPlaceholderCommand = new RelayCommand(BeginDefaultSourceCorrection, () => HasEpisode && HasSources);
        BeginSourceCorrectionCommand = new RelayCommand(BeginSourceCorrection, CanBeginSourceCorrection);
        SearchCandidatesCommand = new AsyncRelayCommand(SearchCandidatesAsync);
        AiSuggestSearchCommand = new AsyncRelayCommand(AiSuggestSearchAsync, CanAiSuggestSearch);
        PreviewMovieCorrectionCommand = new AsyncRelayCommand(ApplyMovieCandidateCorrectionAsync, CanApplyMovieCandidateCorrection);
        SelectTvEpisodeCorrectionTargetCommand = new RelayCommand(SelectTvEpisodeCorrectionTarget, CanSelectTvEpisodeCorrectionTarget);
        PreviewTvEpisodeCorrectionCommand = new AsyncRelayCommand(ApplySelectedTvEpisodeCorrectionAsync, CanApplySelectedTvEpisodeCorrection);
        SearchUnknownSeasonTargetsCommand = new AsyncRelayCommand(SearchUnknownSeasonTargetsAsync, CanSearchUnknownSeasonTargets);
        OpenUnknownSeasonPickerCommand = new AsyncRelayCommand(OpenUnknownSeasonPickerAsync, CanOpenUnknownSeasonPicker);
        CloseUnknownSeasonPickerCommand = new RelayCommand(CloseUnknownSeasonPicker, () => IsUnknownSeasonPickerDialogOpen);
        SelectUnknownSeasonTargetCommand = new RelayCommand(SelectUnknownSeasonTarget, CanSelectUnknownSeasonTarget);
        ApplyUnknownSeasonCorrectionCommand = new AsyncRelayCommand(ApplyUnknownSeasonCorrectionAsync, CanApplyUnknownSeasonCorrection);
        CancelCorrectionCommand = new RelayCommand(CancelCorrection, () => IsCorrectionPanelVisible);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;
        _mediaProbeService.ProbeStatusChanged += OnProbeStatusChanged;
    }

    public ObservableCollection<TvEpisodeSourceItem> Sources { get; } = [];

    public ObservableCollection<MetadataSearchCandidate> SearchCandidates { get; } = [];

    public ObservableCollection<TmdbTvSeriesSearchItem> TvSearchCandidates { get; } = [];

    public ObservableCollection<TmdbTvSeriesCorrectionSeriesGroup> TvSeriesCandidateGroups { get; } = [];

    public ObservableCollection<UnknownTvSeasonCorrectionTargetItem> UnknownSeasonTargets { get; } = [];

    public ObservableCollection<UnknownTvSeasonCorrectionSeriesGroup> UnknownSeasonSeriesGroups { get; } = [];

    public IReadOnlyList<string> CorrectionTargetOptions { get; } =
    [
        CorrectionTargetTvEpisodeText,
        CorrectionTargetMovieText,
        CorrectionTargetUnknownSeasonText
    ];

    public RelayCommand NavigateBackToSeasonCommand { get; }

    public AsyncRelayCommand OpenPlayerCommand { get; }

    public AsyncRelayCommand PlaySourceCommand { get; }

    public RelayCommand ManualProbeSourceCommand { get; }

    public AsyncRelayCommand SetDefaultSourceCommand { get; }

    public AsyncRelayCommand ResetSourceRecognitionCommand { get; }

    public AsyncRelayCommand ToggleWatchedCommand { get; }

    public RelayCommand CorrectionPlaceholderCommand { get; }

    public RelayCommand BeginSourceCorrectionCommand { get; }

    public AsyncRelayCommand SearchCandidatesCommand { get; }

    public AsyncRelayCommand AiSuggestSearchCommand { get; }

    public AsyncRelayCommand PreviewMovieCorrectionCommand { get; }

    public RelayCommand SelectTvEpisodeCorrectionTargetCommand { get; }

    public AsyncRelayCommand PreviewTvEpisodeCorrectionCommand { get; }

    public AsyncRelayCommand SearchUnknownSeasonTargetsCommand { get; }

    public AsyncRelayCommand OpenUnknownSeasonPickerCommand { get; }

    public RelayCommand CloseUnknownSeasonPickerCommand { get; }

    public RelayCommand SelectUnknownSeasonTargetCommand { get; }

    public AsyncRelayCommand ApplyUnknownSeasonCorrectionCommand { get; }

    public RelayCommand CancelCorrectionCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string SeriesName { get => _seriesName; private set => SetProperty(ref _seriesName, value); }

    public string SeasonName { get => _seasonName; private set => SetProperty(ref _seasonName, value); }

    public string SeasonNumberText { get => _seasonNumberText; private set => SetProperty(ref _seasonNumberText, value); }

    public string EpisodeNumberText { get => _episodeNumberText; private set => SetProperty(ref _episodeNumberText, value); }

    public string TitleText { get => _titleText; private set => SetProperty(ref _titleText, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string AirDateText { get => _airDateText; private set => SetProperty(ref _airDateText, value); }

    public string RuntimeText { get => _runtimeText; private set => SetProperty(ref _runtimeText, value); }

    public string WatchedText { get => _watchedText; private set => SetProperty(ref _watchedText, value); }

    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    public string SourceCountText { get => _sourceCountText; private set => SetProperty(ref _sourceCountText, value); }

    public string SourceSummary { get => _sourceSummary; private set => SetProperty(ref _sourceSummary, value); }

    public string LastPlayedText { get => _lastPlayedText; private set => SetProperty(ref _lastPlayedText, value); }

    public string IdentificationStatusText
    {
        get => _identificationStatusText;
        private set => SetProperty(ref _identificationStatusText, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

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
                ClearCandidatesForInactiveCorrectionTarget();
                ClearSelectedTvCorrectionTarget();
                PreviewMovieCorrectionCommand.RaiseCanExecuteChanged();
                SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
                OpenUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
                SelectUnknownSeasonTargetCommand.RaiseCanExecuteChanged();
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CorrectionSourceDisplay { get => _correctionSourceDisplay; private set => SetProperty(ref _correctionSourceDisplay, value); }

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

    public bool IsCorrectionPanelVisible => _correctionMediaFileId.HasValue && HasSources;

    public bool CanUseIdentificationCorrection => HasEpisode && HasSources;

    public bool IsCorrectionTargetMovie => SelectedCorrectionTarget == CorrectionTargetMovieText;

    public bool IsCorrectionTargetTvEpisode => SelectedCorrectionTarget == CorrectionTargetTvEpisodeText;

    public bool IsCorrectionTargetUnknownSeason => SelectedCorrectionTarget == CorrectionTargetUnknownSeasonText;

    public int SelectedDetailTabIndex { get => _selectedDetailTabIndex; set => SetProperty(ref _selectedDetailTabIndex, value); }

    public bool HasSearchCandidates => SearchCandidates.Count > 0;

    public bool HasTvSearchCandidates => TvSeriesCandidateGroups.Count > 0;

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
                PreviewMovieCorrectionCommand.RaiseCanExecuteChanged();
                SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
                OpenUnknownSeasonPickerCommand.RaiseCanExecuteChanged();
                SelectUnknownSeasonTargetCommand.RaiseCanExecuteChanged();
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasEpisode
    {
        get => _hasEpisode;
        private set
        {
            if (SetProperty(ref _hasEpisode, value))
            {
                OnPropertyChanged(nameof(HasNoEpisode));
                OnPropertyChanged(nameof(CanResetSourcesToUnidentified));
                OnPropertyChanged(nameof(CanUseIdentificationCorrection));
                CorrectionPlaceholderCommand.RaiseCanExecuteChanged();
                SetDefaultSourceCommand.RaiseCanExecuteChanged();
                ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
                RefreshWatchedCommandState();
                RefreshPlayerCommandState();
            }
        }
    }

    public bool HasNoEpisode => !HasEpisode;

    public bool IsUnidentified
    {
        get => _isUnidentified;
        private set
        {
            if (SetProperty(ref _isUnidentified, value))
            {
                OnPropertyChanged(nameof(CanResetSourcesToUnidentified));
                ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSources
    {
        get => _hasSources;
        private set
        {
            if (SetProperty(ref _hasSources, value))
            {
                OnPropertyChanged(nameof(HasNoSources));
                OnPropertyChanged(nameof(CanUseIdentificationCorrection));
                OnPropertyChanged(nameof(IsCorrectionPanelVisible));
                CorrectionPlaceholderCommand.RaiseCanExecuteChanged();
                SetDefaultSourceCommand.RaiseCanExecuteChanged();
                ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
                RefreshPlayerCommandState();
            }
        }
    }

    public bool HasNoSources => !HasSources;

    public bool CanResetSourcesToUnidentified => HasEpisode
                                                 && HasSources
                                                 && !IsOpeningPlayer
                                                 && !_playerWindowService.IsPlayerOpen;

    public bool IsWatched
    {
        get => _isWatched;
        private set
        {
            if (SetProperty(ref _isWatched, value))
            {
                OnPropertyChanged(nameof(WatchedButtonText));
            }
        }
    }

    public bool CanToggleWatched => HasEpisode && !IsUpdatingWatched;

    public string WatchedButtonText => IsUpdatingWatched
        ? "更新中..."
        : IsWatched
            ? "取消已看"
            : "标记已看";

    public bool IsUpdatingWatched
    {
        get => _isUpdatingWatched;
        private set
        {
            if (SetProperty(ref _isUpdatingWatched, value))
            {
                RefreshWatchedCommandState();
            }
        }
    }

    public bool IsOpeningPlayer
    {
        get => _isOpeningPlayer;
        private set
        {
            if (SetProperty(ref _isOpeningPlayer, value))
            {
                RefreshPlayerCommandState();
                ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanOpenPlayer => HasEpisode && HasSources && !IsOpeningPlayer && !_playerWindowService.IsPlayerOpen;

    public string PrimaryPlayButtonText => IsOpeningPlayer || _playerWindowService.IsPlayerOpen
        ? "播放器打开中"
        : HasSources
            ? "播放"
            : "暂无播放源";

    public string SourcePlayButtonText => IsOpeningPlayer || _playerWindowService.IsPlayerOpen
        ? "播放器打开中"
        : "播放此源";

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        var selectedEpisodeId = _navigationStateService.SelectedTvEpisodeId;
        if (!selectedEpisodeId.HasValue)
        {
            Clear("请先选择一个剧集。");
            return;
        }

        try
        {
            var model = await _tvDetailQueryService.GetEpisodeDetailAsync(
                selectedEpisodeId.Value,
                cancellationToken: cancellationToken);
            if (model is null)
            {
                Clear("未找到对应剧集，可能已被移出。");
                return;
            }

            var isNewEpisode = _episodeId != model.EpisodeId;
            _episodeId = model.EpisodeId;
            _seasonId = model.SeasonId;
            _defaultMediaFileId = model.DefaultMediaFileId;
            HasEpisode = true;
            SeriesName = string.IsNullOrWhiteSpace(model.SeriesName) ? "-" : model.SeriesName;
            SeasonName = string.IsNullOrWhiteSpace(model.SeasonName) ? model.SeasonNumberText : model.SeasonName;
            SeasonNumberText = model.SeasonNumberText;
            EpisodeNumberText = model.EpisodeNumberText;
            TitleText = model.DisplayTitle;
            Overview = model.DisplayOverview;
            AirDateText = model.AirDateText;
            RuntimeText = model.RuntimeText;
            WatchedText = model.WatchedText;
            IsWatched = model.IsWatched;
            ProgressText = model.ProgressText;
            SourceCountText = model.SourceCountText;
            SourceSummary = model.SourceSummary;
            LastPlayedText = model.LastPlayedText;
            IdentificationStatusText = model.IdentificationStatusText;
            IsUnidentified = model.IsUnidentified;
            HasSources = model.HasSources;
            Sources.Clear();
            foreach (var source in model.Sources)
            {
                Sources.Add(source);
            }

            if (!model.HasSources && SelectedDetailTabIndex == 1)
            {
                SelectedDetailTabIndex = 0;
            }

            if (isNewEpisode)
            {
                ManualSearchQuery = model.DisplayTitle;
            ManualSearchYear = string.Empty;
            TvCorrectionQuery = model.SeriesName;
            CorrectionSeasonNumber = model.SeasonNumber.ToString();
            CorrectionEpisodeNumber = model.EpisodeNumber.ToString();
            SearchCandidates.Clear();
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            ClearUnknownSeasonTargets();
            SelectedUnknownSeasonTarget = null;
            IsUnknownSeasonPickerDialogOpen = false;
            _correctionMediaFileId = null;
            _correctionSourceFileName = string.Empty;
            _correctionSourcePath = string.Empty;
            OnPropertyChanged(nameof(IsCorrectionPanelVisible));
            SelectedDetailTabIndex = 0;
            SelectedCorrectionTarget = CorrectionTargetTvEpisodeText;
            CorrectionSourceDisplay = "请选择一个播放源。";
            ClearCorrectionPreview();
            OnPropertyChanged(nameof(HasSearchCandidates));
                OnPropertyChanged(nameof(HasTvSearchCandidates));
            UnknownSeasonSearchQuery = model.SeriesName;
            UnknownSeasonEpisodeNumber = model.EpisodeNumber.ToString();
            }
            else if (_correctionMediaFileId.HasValue && Sources.All(source => source.MediaFileId != _correctionMediaFileId.Value))
            {
                _correctionMediaFileId = null;
                _correctionSourceFileName = string.Empty;
                _correctionSourcePath = string.Empty;
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
                PreviewMovieCorrectionCommand.RaiseCanExecuteChanged();
                PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
                SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
                ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
                AiSuggestSearchCommand.RaiseCanExecuteChanged();
            }
            OnPropertyChanged(nameof(CanResetSourcesToUnidentified));
            NavigateBackToSeasonCommand.RaiseCanExecuteChanged();
            SetDefaultSourceCommand.RaiseCanExecuteChanged();
            ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
            RefreshWatchedCommandState();
            RefreshPlayerCommandState();
            StatusMessage = model.HasSources
                ? $"已加载 {model.EpisodeNumberText}，可从默认源或指定播放源打开播放器。"
                : $"已加载 {model.EpisodeNumberText}，暂无播放源。";
            ScheduleDetailLazyProbe(model.EpisodeId, model.Sources);
        }
        catch (Exception exception)
        {
            Clear($"加载剧集详情失败：{DescribeException(exception)}");
        }
    }

    private void ScheduleDetailLazyProbe(int episodeId, IReadOnlyCollection<TvEpisodeSourceItem> sources)
    {
        var mediaFileIds = sources
            .Select(source => source.MediaFileId)
            .Where(mediaFileId => _lazyProbeCheckedMediaFileIds.Add(mediaFileId))
            .ToArray();
        if (mediaFileIds.Length == 0)
        {
            return;
        }

        _ = RunDetailLazyProbeAsync(episodeId, mediaFileIds);
    }

    private async Task RunDetailLazyProbeAsync(int episodeId, IReadOnlyCollection<int> mediaFileIds)
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
                "episode",
                episodeId,
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
                $"event=media-probe-detail-lazy-refresh contentKind=episode queuedCount={result.QueuedCount} refreshStrategy=probe-status-changed-event");
        }
        catch (Exception exception)
        {
            SetProbeBusyState(requestedMediaFileIds, isBusy: false);
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-refresh contentKind=episode skippedReason=refresh-error error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
    }

    private bool CanManualProbeSource(object? parameter)
    {
        return parameter is TvEpisodeSourceItem source
               && _episodeId.HasValue
               && Sources.Any(item => item.MediaFileId == source.MediaFileId)
               && source.MediaProbeStatus != MediaProbeStatus.Pending
               && !_probingMediaFileIds.Contains(source.MediaFileId);
    }

    private bool CanSetDefaultSource(object? parameter)
    {
        return parameter is TvEpisodeSourceItem source
               && _episodeId.HasValue
               && HasEpisode
               && HasSources
               && !source.IsDefault
               && Sources.Any(item => item.MediaFileId == source.MediaFileId);
    }

    private async Task SetDefaultSourceAsync(object? parameter)
    {
        if (parameter is not TvEpisodeSourceItem source)
        {
            StatusMessage = "请先选择要设为默认的播放源。";
            return;
        }

        if (!_episodeId.HasValue || !Sources.Any(item => item.MediaFileId == source.MediaFileId))
        {
            StatusMessage = "该播放源不属于当前剧集。";
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetEpisodeDefaultMediaFileAsync(
                _episodeId.Value,
                source.MediaFileId,
                CancellationToken.None);
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = $"默认播放源已切换为：{source.SourceTypeText} · {source.DisplayFileName}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"设置默认播放源失败：{DescribeException(exception)}";
        }
        finally
        {
            SetDefaultSourceCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanResetSourceRecognition(object? parameter)
    {
        return parameter is TvEpisodeSourceItem source
               && _episodeId.HasValue
               && CanResetSourcesToUnidentified
               && Sources.Any(item => item.MediaFileId == source.MediaFileId);
    }

    private async Task ResetSourceRecognitionAsync(object? parameter)
    {
        if (parameter is not TvEpisodeSourceItem source)
        {
            StatusMessage = "请先选择要拆分的播放源。";
            return;
        }

        if (!_episodeId.HasValue || !Sources.Any(item => item.MediaFileId == source.MediaFileId))
        {
            StatusMessage = "该播放源不属于当前剧集。";
            return;
        }

        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "确认从当前集拆分？",
            $"会将该播放源从当前剧集中拆出，并回到 Other / 未识别项承接；不会删除本地或网盘中的真实文件，也不会清空剧集 metadata、已看状态或进度。\n\n播放源：{source.DisplayFileName}",
            "从当前集拆分",
            "取消",
            ConfirmationDialogVariant.Warning);
        if (!confirmed)
        {
            StatusMessage = "已取消拆分播放源。";
            return;
        }

        try
        {
            await _tvSeasonCollectionService.ResetEpisodeSourceToUnidentifiedAsync(
                _episodeId.Value,
                source.MediaFileId,
                CancellationToken.None);
            _probingMediaFileIds.Remove(source.MediaFileId);
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = HasSources
                ? "播放源已从当前集拆分，真实文件未被删除。"
                : "最后一个播放源已从当前集拆分，真实文件未被删除；该剧集仍保留。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"拆分播放源失败：{DescribeException(exception)}";
        }
        finally
        {
            SetDefaultSourceCommand.RaiseCanExecuteChanged();
            ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
            ManualProbeSourceCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task ManualProbeSourceAsync(object? parameter)
    {
        if (parameter is not TvEpisodeSourceItem source)
        {
            StatusMessage = "请先选择要探测的播放源。";
            return;
        }

        if (!Sources.Any(item => item.MediaFileId == source.MediaFileId))
        {
            StatusMessage = "该播放源不属于当前剧集。";
            return;
        }

        if (!_probingMediaFileIds.Add(source.MediaFileId))
        {
            return;
        }

        ManualProbeSourceCommand.RaiseCanExecuteChanged();
        StatusMessage = "正在手动探测该播放源。";
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-manual-started contentKind=episode mediaFileId={source.MediaFileId}");
        try
        {
            await _mediaProbeService.ProbeMediaFileAsync(source.MediaFileId, force: true);
            if (_episodeId.HasValue && _navigationStateService.SelectedTvEpisodeId == _episodeId.Value)
            {
                await ActivateAsync();
            }

            StatusMessage = "手动探测已完成，播放源信息已刷新。";
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-manual-completed contentKind=episode mediaFileId={source.MediaFileId}");
        }
        catch (Exception exception)
        {
            StatusMessage = $"手动探测失败：{DescribeException(exception)}";
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-manual-failed contentKind=episode mediaFileId={source.MediaFileId} error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
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

        if (!_episodeId.HasValue
            || _navigationStateService.SelectedTvEpisodeId != _episodeId.Value
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
            if (!_episodeId.HasValue || _navigationStateService.SelectedTvEpisodeId != _episodeId.Value)
            {
                ScanIdentificationDiagnostics.Write(
                    "event=media-probe-detail-lazy-refresh contentKind=episode skippedReason=page-changed");
                return;
            }

            await ActivateAsync();
            ScanIdentificationDiagnostics.Write(
                "event=media-probe-detail-lazy-refresh contentKind=episode status=completed refreshStrategy=probe-status-changed-event");
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=media-probe-detail-lazy-refresh contentKind=episode skippedReason=refresh-error error={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 220)}");
        }
        finally
        {
            _isProbeCompletionRefreshQueued = false;
        }
    }

    private void NavigateBackToSeason()
    {
        if (_seasonId.HasValue)
        {
            _navigationStateService.RequestTvSeasonDetail(_seasonId.Value, _episodeId);
        }
    }

    private async Task ToggleWatchedAsync()
    {
        if (!_episodeId.HasValue)
        {
            StatusMessage = "请先选择剧集。";
            return;
        }

        var targetWatched = !IsWatched;
        IsUpdatingWatched = true;
        try
        {
            await _tvSeasonCollectionService.SetEpisodeWatchedAsync(
                _episodeId.Value,
                targetWatched,
                CancellationToken.None,
                "Manual");
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = targetWatched ? "已标记为已看。" : "已标记为未看。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新观看状态失败：{DescribeException(exception)}";
        }
        finally
        {
            IsUpdatingWatched = false;
            RefreshWatchedCommandState();
        }
    }

    private async Task OpenPlayerAsync(object? parameter)
    {
        if (!_episodeId.HasValue)
        {
            StatusMessage = "请先选择剧集。";
            return;
        }

        if (!HasSources)
        {
            StatusMessage = "当前剧集没有可播放源。";
            return;
        }

        var mediaFileId = parameter is TvEpisodeSourceItem source
            ? source.MediaFileId
            : _defaultMediaFileId;
        await OpenEpisodePlayerAsync(mediaFileId);
    }

    private async Task PlaySourceAsync(object? parameter)
    {
        if (parameter is not TvEpisodeSourceItem source)
        {
            StatusMessage = "请先选择要播放的源。";
            return;
        }

        if (!Sources.Any(item => item.MediaFileId == source.MediaFileId))
        {
            StatusMessage = "该播放源不属于当前剧集。";
            return;
        }

        await OpenEpisodePlayerAsync(source.MediaFileId);
    }

    private async Task OpenEpisodePlayerAsync(int? mediaFileId)
    {
        if (!_episodeId.HasValue)
        {
            StatusMessage = "请先选择剧集。";
            return;
        }

        if (!CanOpenPlayer)
        {
            StatusMessage = "播放器已打开或正在打开，请关闭播放器后再切换播放源。";
            return;
        }

        if (!mediaFileId.HasValue || !Sources.Any(source => source.MediaFileId == mediaFileId.Value))
        {
            StatusMessage = "默认播放源不可用，请刷新后重试。";
            return;
        }

        SetOpeningPlayer(true);
        try
        {
            await _playerWindowService.OpenEpisodeAsync(_episodeId.Value, mediaFileId.Value);
            StatusMessage = "播放器已打开。";
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"播放器打开失败：{DescribeException(exception)}";
        }
        finally
        {
            if (!_playerWindowService.IsPlayerOpen)
            {
                SetOpeningPlayer(false);
            }
        }
    }

    private void SetOpeningPlayer(bool value)
    {
        IsOpeningPlayer = value;
    }

    private void RefreshPlayerCommandState()
    {
        OnPropertyChanged(nameof(CanOpenPlayer));
        OnPropertyChanged(nameof(CanResetSourcesToUnidentified));
        OnPropertyChanged(nameof(PrimaryPlayButtonText));
        OnPropertyChanged(nameof(SourcePlayButtonText));
        OpenPlayerCommand?.RaiseCanExecuteChanged();
        PlaySourceCommand?.RaiseCanExecuteChanged();
        ResetSourceRecognitionCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshWatchedCommandState()
    {
        OnPropertyChanged(nameof(CanToggleWatched));
        OnPropertyChanged(nameof(WatchedButtonText));
        ToggleWatchedCommand?.RaiseCanExecuteChanged();
    }

    private void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        SetOpeningPlayer(false);
        _ = RefreshAfterPlayerClosedAsync();
    }

    private async Task RefreshAfterPlayerClosedAsync()
    {
        if (!_episodeId.HasValue || _navigationStateService.SelectedTvEpisodeId != _episodeId.Value)
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

    private void BeginDefaultSourceCorrection()
    {
        var source = Sources.FirstOrDefault(x => x.IsDefault) ?? Sources.FirstOrDefault();
        if (source is null)
        {
            StatusMessage = "当前剧集没有可修正的播放源。";
            return;
        }

        BeginSourceCorrection(source);
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
            $"event=single-source-correction-ai-assist-started page=episode targetKind={targetKind} mediaFileId={_correctionMediaFileId}");

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
                $"event=single-source-correction-ai-assist-failed page=episode targetKind={targetKind} mediaFileId={_correctionMediaFileId} reason=cancelled");
        }
        catch (Exception exception)
        {
            StatusMessage = $"AI 辅助搜索失败：{DescribeException(exception)}";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-failed page=episode targetKind={targetKind} mediaFileId={_correctionMediaFileId} reason=exception");
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
            Overview,
            _correctionSourcePath);
        if (suggestionResult.Status != AiSearchSuggestionStatus.Success)
        {
            StatusMessage = string.IsNullOrWhiteSpace(suggestionResult.Message)
                ? "AI 未返回可用电影搜索词，请手动输入后搜索。"
                : $"AI 未返回可用电影搜索词：{suggestionResult.Message}";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-skipped page=episode targetKind=movie mediaFileId={_correctionMediaFileId} reason={FormatAiSuggestionStatus(suggestionResult.Status)} message={ScanIdentificationDiagnostics.FormatValue(suggestionResult.Message, 180)}");
            return;
        }

        var suggestion = suggestionResult.Suggestion;

        ManualSearchQuery = suggestion.Query;
        ManualSearchYear = suggestion.ReleaseYear?.ToString() ?? string.Empty;
        StatusMessage = FormatAiSearchSuggestionStatus("电影", suggestion);
        await SearchCandidatesAsync();
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-succeeded page=episode targetKind=movie mediaFileId={_correctionMediaFileId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={SearchCandidates.Count}");
    }

    private async Task AiSuggestTvSearchAsync()
    {
        var suggestionResult = await _aiClassificationService.SuggestTvEpisodeCorrectionSearchQueryAsync(
            TitleText,
            _correctionSourceFileName,
            seriesTitle: SeriesName,
            seasonNumber: TryParsePositiveOrZero(CorrectionSeasonNumber),
            episodeNumber: TryParsePositive(CorrectionEpisodeNumber),
            overview: Overview,
            sourcePath: _correctionSourcePath);
        if (suggestionResult.Status != AiSearchSuggestionStatus.Success)
        {
            StatusMessage = string.IsNullOrWhiteSpace(suggestionResult.Message)
                ? "AI 未返回可用电视剧搜索词，请手动输入后搜索。"
                : $"AI 未返回可用电视剧搜索词：{suggestionResult.Message}";
            ScanIdentificationDiagnostics.Write(
                $"event=single-source-correction-ai-assist-skipped page=episode targetKind=tv-episode mediaFileId={_correctionMediaFileId} reason={FormatAiSuggestionStatus(suggestionResult.Status)} message={ScanIdentificationDiagnostics.FormatValue(suggestionResult.Message, 180)}");
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
        await SearchCandidatesAsync();
        ScanIdentificationDiagnostics.Write(
            $"event=single-source-correction-ai-assist-succeeded page=episode targetKind=tv-episode mediaFileId={_correctionMediaFileId} status={FormatAiSuggestionStatus(suggestionResult.Status)} candidateCount={TvSeriesCandidateGroups.Count}");
    }

    private bool CanAiSuggestSearch()
    {
        return !IsCorrectionBusy
               && HasEpisode
               && IsCorrectionPanelVisible
               && !IsCorrectionTargetUnknownSeason;
    }

    private bool CanBeginSourceCorrection(object? parameter)
    {
        return HasEpisode
               && parameter is TvEpisodeSourceItem source
               && Sources.Any(item => item.MediaFileId == source.MediaFileId);
    }

    private void BeginSourceCorrection(object? parameter)
    {
        if (parameter is not TvEpisodeSourceItem source || !CanBeginSourceCorrection(parameter))
        {
            return;
        }

        _correctionMediaFileId = null;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));

        CorrectionSourceDisplay = $"{source.SourceTypeText} · {source.DisplayFileName}";
        SelectedCorrectionTarget = CorrectionTargetTvEpisodeText;
        _correctionSourceFileName = source.DisplayFileName;
        _correctionSourcePath = FirstNonEmpty(source.FilePath, source.RemoteUri, source.LocationText);
        ManualSearchQuery = string.IsNullOrWhiteSpace(ManualSearchQuery) ? TitleText : ManualSearchQuery;
        TvCorrectionQuery = string.IsNullOrWhiteSpace(TvCorrectionQuery) ? SeriesName : TvCorrectionQuery;
        UnknownSeasonSearchQuery = string.IsNullOrWhiteSpace(UnknownSeasonSearchQuery) ? SeriesName : UnknownSeasonSearchQuery;
        UnknownSeasonEpisodeNumber = string.IsNullOrWhiteSpace(CorrectionEpisodeNumber) ? "1" : CorrectionEpisodeNumber;
        ClearCorrectionPreview();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
        ClearUnknownSeasonTargets();
        SelectedUnknownSeasonTarget = null;
        IsUnknownSeasonPickerDialogOpen = false;
        ClearSelectedTvCorrectionTarget();
        _correctionMediaFileId = source.MediaFileId;
        SelectedDetailTabIndex = 1;
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        CancelCorrectionCommand.RaiseCanExecuteChanged();
        PreviewMovieCorrectionCommand.RaiseCanExecuteChanged();
        SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = $"已选择播放源“{source.DisplayFileName}”，请搜索目标；点击候选后会直接修正。";
    }

    private void CancelCorrection()
    {
        _correctionMediaFileId = null;
        _correctionSourceFileName = string.Empty;
        _correctionSourcePath = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetTvEpisodeText;
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
        PreviewMovieCorrectionCommand.RaiseCanExecuteChanged();
        SelectTvEpisodeCorrectionTargetCommand.RaiseCanExecuteChanged();
        PreviewTvEpisodeCorrectionCommand.RaiseCanExecuteChanged();
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = "已取消本次修正，未修改任何数据。";
    }

    private async Task SearchCandidatesAsync()
    {
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

        if (IsCorrectionTargetUnknownSeason)
        {
            await SearchUnknownSeasonTargetsAsync();
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
            StatusMessage = $"TMDB 搜索失败：{DescribeException(exception)}";
        }
    }

    private async Task SearchTvCandidatesAsync()
    {
        var query = string.IsNullOrWhiteSpace(TvCorrectionQuery) ? SeriesName : TvCorrectionQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = "请输入要搜索的电视剧名。";
            return;
        }

        try
        {
            var page = await _tmdbService.SearchTvSeriesAsync(query, 1);
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            ClearSelectedTvCorrectionTarget();
            foreach (var candidate in page.Results.Take(12))
            {
                TvSearchCandidates.Add(candidate);
                var details = await _tmdbService.GetTvSeriesDetailsAsync(candidate.TmdbId, cancellationToken: CancellationToken.None);
                TvSeriesCandidateGroups.Add(new TmdbTvSeriesCorrectionSeriesGroup(candidate, details));
            }

            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = TvSeriesCandidateGroups.Count == 0
                ? "没有找到符合条件的 TMDB 电视剧结果。"
                : $"已找到 {TvSeriesCandidateGroups.Count} 个电视剧候选；可展开选择季，或直接修正到剧并使用输入的季号/集号。";
        }
        catch (Exception exception)
        {
            TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
            OnPropertyChanged(nameof(HasTvSearchCandidates));
            StatusMessage = $"TMDB 搜索失败：{DescribeException(exception)}";
        }
    }

    private bool CanSearchUnknownSeasonTargets()
    {
        return !IsCorrectionBusy
               && HasEpisode
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
        return IsUnknownSeasonPickerDialogOpen
               && parameter is UnknownTvSeasonCorrectionTargetItem;
    }

    private void SelectUnknownSeasonTarget(object? parameter)
    {
        if (parameter is not UnknownTvSeasonCorrectionTargetItem target)
        {
            return;
        }

        SelectedUnknownSeasonTarget = target;
        IsUnknownSeasonPickerDialogOpen = false;
        StatusMessage = $"已选择未识别季：{target.DisplayTitle}。请输入集号后加入。";
    }

    private async Task SearchUnknownSeasonTargetsAsync()
    {
        if (!CanSearchUnknownSeasonTargets())
        {
            StatusMessage = "请先选择播放源，并把目标类型切换为“加入已有未识别季”。";
            return;
        }

        try
        {
            IsCorrectionBusy = true;
            var targets = await _singleSourceCorrectionService.SearchUnknownSeasonTargetsAsync(
                null,
                CancellationToken.None);

            SetUnknownSeasonTargets(targets);
            StatusMessage = UnknownSeasonTargets.Count == 0
                ? "没有找到可加入的 no-TMDB / 未识别季。"
                : $"已找到 {UnknownSeasonTargets.Count} 个可加入的未识别季。";
        }
        catch (Exception exception)
        {
            ClearUnknownSeasonTargets();
            StatusMessage = $"加载未识别季失败：{DescribeException(exception)}";
        }
        finally
        {
            IsCorrectionBusy = false;
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

        UnknownSeasonTargets.Clear();
        foreach (var target in orderedTargets)
        {
            UnknownSeasonTargets.Add(target);
        }

        UnknownSeasonSeriesGroups.Clear();
        foreach (var group in UnknownTvSeasonCorrectionSeriesGroup.FromTargets(orderedTargets))
        {
            UnknownSeasonSeriesGroups.Add(group);
        }

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
        _correctionSourceFileName = string.Empty;
        _correctionSourcePath = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetTvEpisodeText;
        CorrectionSourceDisplay = "请选择一个播放源。";
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
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
    }

    private void Clear(string statusMessage)
    {
        _episodeId = null;
        _seasonId = null;
        _defaultMediaFileId = null;
        HasEpisode = false;
        SeriesName = "-";
        SeasonName = "-";
        SeasonNumberText = "-";
        EpisodeNumberText = "-";
        TitleText = "未选择剧集";
        Overview = "请先选择一个剧集。";
        AirDateText = "-";
        RuntimeText = "-";
        WatchedText = "-";
        ProgressText = "暂无进度";
        SourceCountText = "暂无播放源";
        SourceSummary = "暂无播放源";
        LastPlayedText = "-";
        IdentificationStatusText = "未加载";
        IsUnidentified = false;
        IsWatched = false;
        HasSources = false;
        Sources.Clear();
        SearchCandidates.Clear();
        TvSearchCandidates.Clear();
            TvSeriesCandidateGroups.Clear();
        ClearUnknownSeasonTargets();
        SelectedUnknownSeasonTarget = null;
        IsUnknownSeasonPickerDialogOpen = false;
        _correctionMediaFileId = null;
        _correctionSourceFileName = string.Empty;
        _correctionSourcePath = string.Empty;
        OnPropertyChanged(nameof(IsCorrectionPanelVisible));
        SelectedDetailTabIndex = 0;
        SelectedCorrectionTarget = CorrectionTargetTvEpisodeText;
        CorrectionSourceDisplay = "请选择一个播放源。";
        ClearCorrectionPreview();
        OnPropertyChanged(nameof(HasSearchCandidates));
        OnPropertyChanged(nameof(HasTvSearchCandidates));
        SearchUnknownSeasonTargetsCommand.RaiseCanExecuteChanged();
        ApplyUnknownSeasonCorrectionCommand.RaiseCanExecuteChanged();
        AiSuggestSearchCommand.RaiseCanExecuteChanged();
        StatusMessage = statusMessage;
        NavigateBackToSeasonCommand.RaiseCanExecuteChanged();
        SetDefaultSourceCommand.RaiseCanExecuteChanged();
        ResetSourceRecognitionCommand.RaiseCanExecuteChanged();
        RefreshWatchedCommandState();
        RefreshPlayerCommandState();
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
