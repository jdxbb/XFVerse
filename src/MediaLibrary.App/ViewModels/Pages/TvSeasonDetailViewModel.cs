using System.Collections.ObjectModel;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class TvSeasonDetailViewModel : PageViewModelBase
{
    private const string TmdbRatingLoadingText = "TMDB 季评分加载中...";
    private const string ImdbRatingLoadingText = "IMDb 剧集评分加载中...";
    private const string RatingUnavailableText = "暂无评分";
    private const string SeasonRatingUnavailableText = "暂无季评分";
    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
    private readonly ITvMetadataHydrationService _metadataHydrationService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IUnknownSeasonCorrectionService _unknownSeasonCorrectionService;
    private int? _seasonId;
    private int? _seriesId;
    private string _seriesName = "-";
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
    private LibraryVisibilityState _libraryVisibilityState = LibraryVisibilityState.Auto;
    private string _tmdbRatingDisplay = SeasonRatingUnavailableText;
    private string _imdbRatingDisplay = string.Empty;
    private bool _isSeasonCorrectionPanelOpen;
    private bool _isSeasonCorrectionBusy;
    private string _seasonCorrectionSeasonNumber = "1";
    private string _seasonCorrectionStatusMessage = string.Empty;
    private string _seasonCorrectionConfirmationText = string.Empty;
    private bool _isRecognizedSeasonPickerDialogOpen;
    private RecognizedTvSeasonCorrectionSeasonItem? _selectedRecognizedSeasonTarget;

    public TvSeasonDetailViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService,
        ITvMetadataHydrationService metadataHydrationService,
        IPlayerWindowService playerWindowService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IDataRefreshService dataRefreshService,
        IUnknownSeasonCorrectionService unknownSeasonCorrectionService)
        : base("电视剧季", "查看电视剧季详情、聚合进度和集列表。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        _metadataHydrationService = metadataHydrationService;
        _playerWindowService = playerWindowService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _dataRefreshService = dataRefreshService;
        _unknownSeasonCorrectionService = unknownSeasonCorrectionService;
        NavigateBackToSeriesCommand = new RelayCommand(NavigateBackToSeries, () => _seriesId.HasValue);
        OpenEpisodeDetailCommand = new RelayCommand(OpenEpisodeDetail);
        PlayEpisodeCommand = new AsyncRelayCommand(PlayEpisodeAsync, CanPlayEpisode);
        ToggleFavoriteCommand = new AsyncRelayCommand(() => ToggleFavoriteAsync(), () => HasSeason && (IsFavorite || IsSeasonWatched));
        ToggleWantToWatchCommand = new AsyncRelayCommand(() => ToggleWantToWatchAsync(), () => HasSeason && (IsWantToWatch || IsSeasonUnwatched));
        ToggleNotInterestedCommand = new AsyncRelayCommand(() => ToggleNotInterestedAsync(), () => HasSeason);
        MarkSeasonWatchedCommand = new AsyncRelayCommand(() => SetSeasonWatchedAsync(true), () => HasSeason);
        MarkSeasonUnwatchedCommand = new AsyncRelayCommand(() => SetSeasonWatchedAsync(false), () => HasSeason);
        AddSeasonToLibraryCommand = new AsyncRelayCommand(AddSeasonToLibraryAsync, () => CanAddSeasonToLibrary);
        MarkEpisodeWatchedCommand = new AsyncRelayCommand(parameter => SetEpisodeWatchedAsync(parameter, true));
        MarkEpisodeUnwatchedCommand = new AsyncRelayCommand(parameter => SetEpisodeWatchedAsync(parameter, false));
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
        OpenSeasonCorrectionCommand = new RelayCommand(OpenSeasonCorrection, () => CanCorrectSeasonToRecognized);
        CancelSeasonCorrectionCommand = new RelayCommand(CancelSeasonCorrection, () => IsSeasonCorrectionPanelOpen && !IsSeasonCorrectionBusy);
        SearchSeasonCorrectionCandidatesCommand = new AsyncRelayCommand(SearchSeasonCorrectionCandidatesAsync, () => IsSeasonCorrectionPanelOpen && !IsSeasonCorrectionBusy);
        CloseRecognizedSeasonPickerCommand = new RelayCommand(CloseRecognizedSeasonPicker, () => IsRecognizedSeasonPickerDialogOpen && !IsSeasonCorrectionBusy);
        SelectRecognizedSeasonTargetCommand = new RelayCommand(SelectRecognizedSeasonTarget, CanSelectRecognizedSeasonTarget);
        ApplySeasonCorrectionCommand = new AsyncRelayCommand(ApplySeasonCorrectionAsync, () => CanApplySeasonCorrection);
        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;
    }

    public ObservableCollection<TvSeasonEpisodeListItem> Episodes { get; } = [];

    public ObservableCollection<RecognizedTvSeasonCorrectionSeriesGroup> RecognizedSeasonSeriesGroups { get; } = [];

    public RelayCommand NavigateBackToSeriesCommand { get; }

    public RelayCommand OpenEpisodeDetailCommand { get; }

    public AsyncRelayCommand PlayEpisodeCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand ToggleWantToWatchCommand { get; }

    public AsyncRelayCommand ToggleNotInterestedCommand { get; }

    public AsyncRelayCommand MarkSeasonWatchedCommand { get; }

    public AsyncRelayCommand MarkSeasonUnwatchedCommand { get; }

    public AsyncRelayCommand AddSeasonToLibraryCommand { get; }

    public AsyncRelayCommand MarkEpisodeWatchedCommand { get; }

    public AsyncRelayCommand MarkEpisodeUnwatchedCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public RelayCommand OpenSeasonCorrectionCommand { get; }

    public RelayCommand CancelSeasonCorrectionCommand { get; }

    public AsyncRelayCommand SearchSeasonCorrectionCandidatesCommand { get; }

    public RelayCommand CloseRecognizedSeasonPickerCommand { get; }

    public RelayCommand SelectRecognizedSeasonTargetCommand { get; }

    public AsyncRelayCommand ApplySeasonCorrectionCommand { get; }

    public string SeriesName { get => _seriesName; private set => SetProperty(ref _seriesName, value); }

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
                RaiseSeasonStateCommandCanExecuteChanged();
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool HasNoSeason => !HasSeason;

    public bool HasEpisodes => HasSeason && Episodes.Count > 0;

    public bool HasNoEpisodes => HasSeason && Episodes.Count == 0;

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
                RaiseSeasonCorrectionCommandStates();
            }
        }
    }

    public bool CanCorrectSeasonToRecognized => HasSeason
                                                && IsUnidentified
                                                && Episodes.Any(x => x.ActiveSourceCount > 0)
                                                && !IsSeasonCorrectionBusy;

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
                RaiseSeasonCorrectionCommandStates();
            }
        }
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
        private set => SetProperty(ref _seasonCorrectionStatusMessage, value);
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

    public bool HasSelectedSeasonCorrectionTarget => _selectedRecognizedSeasonTarget is not null
                                                     && TryGetSeasonCorrectionSeasonNumber(out _);

    public string SelectedRecognizedSeasonTargetDisplay => _selectedRecognizedSeasonTarget is null
        ? "尚未选择目标已识别季。"
        : _selectedRecognizedSeasonTarget.DisplayTitle;

    public bool CanApplySeasonCorrection => IsSeasonCorrectionPanelOpen
                                            && !IsSeasonCorrectionBusy
                                            && _seasonId.HasValue
                                            && HasSelectedSeasonCorrectionTarget;

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteButtonText));
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
                RaiseSeasonStateCommandCanExecuteChanged();
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
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public string FavoriteButtonText => IsFavorite ? "取消喜爱" : "喜爱";

    public string WantToWatchButtonText => IsWantToWatch ? "取消想看" : "想看";

    public string NotInterestedButtonText => IsNotInterested ? "取消不想看" : "不想看";

    public bool IsVisibleInLibrary
    {
        get => _isVisibleInLibrary;
        private set
        {
            if (SetProperty(ref _isVisibleInLibrary, value))
            {
                OnPropertyChanged(nameof(CanAddSeasonToLibrary));
                AddSeasonToLibraryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanAddSeasonToLibrary => HasSeason && !IsVisibleInLibrary;

    public string AddSeasonToLibraryButtonText => _libraryVisibilityState == LibraryVisibilityState.Hidden
        ? "恢复到媒体库"
        : "加入媒体库";

    private LibraryVisibilityState CurrentLibraryVisibilityState
    {
        get => _libraryVisibilityState;
        set
        {
            if (SetProperty(ref _libraryVisibilityState, value))
            {
                OnPropertyChanged(nameof(AddSeasonToLibraryButtonText));
            }
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
            var model = await _tvDetailQueryService.GetSeasonDetailAsync(selectedSeasonId.Value, cancellationToken);
            if (model is null)
            {
                Clear("未找到对应电视剧季，可能已被移出。");
                return;
            }

            _seasonId = model.SeasonId;
            _seriesId = model.SeriesId;
            HasSeason = true;
            SeriesName = model.SeriesName;
            Name = model.IsUnidentified ? "未识别电视剧季" : model.Name;
            Overview = string.IsNullOrWhiteSpace(model.Overview) ? "暂无简介。" : model.Overview;
            PosterDisplayUrl = model.PosterDisplayUrl;
            SeasonNumberText = model.SeasonNumberText;
            AirDateText = model.AirDateText;
            GenreDisplay = string.IsNullOrWhiteSpace(model.GenreDisplay) ? "未提供" : model.GenreDisplay;
            SetRatingDisplayParts(TmdbRatingLoadingText, ImdbRatingLoadingText);
            SourceSummary = model.SourceSummary;
            ProgressText = model.ProgressText;
            InLibraryText = model.InLibraryText;
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
            Episodes.Clear();
            foreach (var episode in model.Episodes)
            {
                Episodes.Add(episode);
            }

            OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
            if (!CanCorrectSeasonToRecognized)
            {
                ClearSeasonCorrectionState();
            }
            else if (string.IsNullOrWhiteSpace(SeasonCorrectionSeasonNumber))
            {
                SeasonCorrectionSeasonNumber = model.SeasonNumber > 0 ? model.SeasonNumber.ToString() : "1";
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
            var selectedEpisodeId = _navigationStateService.SelectedTvEpisodeId;
            StatusMessage = selectedEpisodeId.HasValue
                ? $"已加载集列表，目标集 ID：{selectedEpisodeId.Value}。"
                : Episodes.Count == 0
                    ? EpisodeEmptyText
                    : $"已加载 {Episodes.Count} 集。";
            _ = LoadRatingDisplayAsync(model.SeasonId, cancellationToken);
            if (shouldEnsureEpisodeMetadata)
            {
                _ = EnsureSeasonEpisodesAndRefreshAsync(model.SeasonId, cancellationToken);
            }
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

            ProgressText = model.ProgressText;
            InLibraryText = model.InLibraryText;
            IsSeasonWatched = model.IsSeasonWatched;
            IsSeasonUnwatched = model.IsSeasonUnwatched;
            IsEpisodeMetadataLoading = false;
            OnPropertyChanged(nameof(HasEpisodes));
            OnPropertyChanged(nameof(HasNoEpisodes));
            OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
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

    private async Task LoadRatingDisplayAsync(int seasonId, CancellationToken cancellationToken)
    {
        try
        {
            var tmdbRatingDisplay = await _tvDetailQueryService.GetSeasonTmdbRatingDisplayAsync(seasonId, cancellationToken);
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
        if (_seriesId.HasValue)
        {
            _navigationStateService.RequestTvSeriesOverview(_seriesId.Value);
        }
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
        SeasonCorrectionSeasonNumber = TryParseDisplaySeasonNumber(SeasonNumberText, out var seasonNumber)
            ? seasonNumber.ToString()
            : "1";
        SeasonCorrectionStatusMessage = "搜索并从弹窗中选择目标已识别季。";
        UpdateSeasonCorrectionConfirmation();
    }

    private void CancelSeasonCorrection()
    {
        ClearSeasonCorrectionState();
    }

    private async Task SearchSeasonCorrectionCandidatesAsync()
    {
        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = "正在加载本地已识别季...";
            RecognizedSeasonSeriesGroups.Clear();
            OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
            _selectedRecognizedSeasonTarget = null;
            OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
            UpdateSeasonCorrectionConfirmation();

            var targets = await _tvDetailQueryService.GetRecognizedSeasonCorrectionTargetsAsync();
            foreach (var group in RecognizedTvSeasonCorrectionSeriesGroup.FromTargets(targets))
            {
                RecognizedSeasonSeriesGroups.Add(group);
            }

            OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
            IsRecognizedSeasonPickerDialogOpen = true;
            SeasonCorrectionStatusMessage = RecognizedSeasonSeriesGroups.Count == 0
                ? "本地库中没有可选择的已识别季。"
                : $"已加载 {RecognizedSeasonSeriesGroups.Count} 个可展开的已识别剧。";
        }
        catch (Exception exception)
        {
            SeasonCorrectionStatusMessage = $"加载本地已识别季失败：{DescribeException(exception)}";
        }
        finally
        {
            IsSeasonCorrectionBusy = false;
        }
    }

    private bool CanSelectRecognizedSeasonTarget(object? parameter)
    {
        return IsRecognizedSeasonPickerDialogOpen
               && !IsSeasonCorrectionBusy
               && parameter is RecognizedTvSeasonCorrectionSeasonItem;
    }

    private void SelectRecognizedSeasonTarget(object? parameter)
    {
        if (parameter is not RecognizedTvSeasonCorrectionSeasonItem target)
        {
            SeasonCorrectionStatusMessage = "请选择目标已识别季。";
            return;
        }

        _selectedRecognizedSeasonTarget = target;
        SeasonCorrectionSeasonNumber = target.SeasonNumber.ToString();
        IsRecognizedSeasonPickerDialogOpen = false;
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        SeasonCorrectionStatusMessage = $"已选择：{target.DisplayTitle}。";
        UpdateSeasonCorrectionConfirmation();
        LogSeasonCorrectionPreview();
    }

    private void CloseRecognizedSeasonPicker()
    {
        IsRecognizedSeasonPickerDialogOpen = false;
    }

    private async Task ApplySeasonCorrectionAsync()
    {
        if (!_seasonId.HasValue || _selectedRecognizedSeasonTarget is null)
        {
            SeasonCorrectionStatusMessage = "请选择目标已识别季。";
            return;
        }

        if (!TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
        {
            SeasonCorrectionStatusMessage = "季号必须是正整数。";
            return;
        }

        var sourceSeasonId = _seasonId.Value;
        var targetSeriesTmdbId = _selectedRecognizedSeasonTarget.TmdbSeriesId;
        try
        {
            IsSeasonCorrectionBusy = true;
            SeasonCorrectionStatusMessage = "正在修正未识别季...";
            var result = await _unknownSeasonCorrectionService.ApplyUnknownSeasonToRecognizedSeasonAsync(
                sourceSeasonId,
                targetSeriesTmdbId,
                seasonNumber);

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
            await _tvSeasonCollectionService.SetWatchedAsync(_seasonId.Value, isWatched, changeSource: "Manual");
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = isWatched ? "已标记整季为已看。" : "已标记整季为未看。";
        }
        catch (Exception exception)
        {
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
            if (_libraryVisibilityState == LibraryVisibilityState.Hidden)
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
            StatusMessage = "已加入媒体库。";
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
            await _tvSeasonCollectionService.SetEpisodeWatchedAsync(episode.EpisodeId, isWatched, changeSource: "Manual");
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = $"{episode.EpisodeNumberText} 已标记为{(isWatched ? "已看" : "未看")}。";
        }
        catch (Exception exception)
        {
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
        IsRecognizedSeasonPickerDialogOpen = false;
        RecognizedSeasonSeriesGroups.Clear();
        _selectedRecognizedSeasonTarget = null;
        SeasonCorrectionConfirmationText = string.Empty;
        SeasonCorrectionStatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasRecognizedSeasonTargets));
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        RaiseSeasonCorrectionCommandStates();
    }

    private void UpdateSeasonCorrectionConfirmation()
    {
        OnPropertyChanged(nameof(HasSelectedSeasonCorrectionTarget));
        OnPropertyChanged(nameof(SelectedRecognizedSeasonTargetDisplay));
        if (_selectedRecognizedSeasonTarget is null)
        {
            SeasonCorrectionConfirmationText = string.Empty;
            return;
        }

        if (!TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
        {
            SeasonCorrectionConfirmationText = "季号必须是正整数。";
            return;
        }

        var sourceCount = Episodes.Sum(x => x.ActiveSourceCount);
        SeasonCorrectionConfirmationText =
            $"将把当前未识别季的 {sourceCount} 个播放源修正到《{_selectedRecognizedSeasonTarget.SeriesTitle}》S{seasonNumber:D2}。不会删除真实文件，不补空集。";
    }

    private void LogSeasonCorrectionPreview()
    {
        if (!_seasonId.HasValue || _selectedRecognizedSeasonTarget is null || !TryGetSeasonCorrectionSeasonNumber(out var seasonNumber))
        {
            return;
        }

        ScanIdentificationDiagnostics.Write(
            $"event=season-correction-preview-created sourceSeasonId={_seasonId.Value} targetSeriesTmdbId={_selectedRecognizedSeasonTarget.TmdbSeriesId} targetSeasonNumber={seasonNumber} movedSourceCount={Episodes.Sum(x => x.ActiveSourceCount)}");
    }

    private bool TryGetSeasonCorrectionSeasonNumber(out int seasonNumber)
    {
        return int.TryParse(SeasonCorrectionSeasonNumber?.Trim(), out seasonNumber)
               && seasonNumber > 0;
    }

    private static bool TryParseDisplaySeasonNumber(string value, out int seasonNumber)
    {
        seasonNumber = 1;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out seasonNumber) && seasonNumber > 0;
    }

    private void Clear(string statusMessage)
    {
        _seasonId = null;
        _seriesId = null;
        HasSeason = false;
        SeriesName = "-";
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
        ClearSeasonCorrectionState();
        NavigateBackToSeriesCommand.RaiseCanExecuteChanged();
        RaiseSeasonStateCommandCanExecuteChanged();
        RaiseSeasonCorrectionCommandStates();
        RefreshEpisodePlayCommandState();
        OnPropertyChanged(nameof(HasEpisodes));
        OnPropertyChanged(nameof(HasNoEpisodes));
        OnPropertyChanged(nameof(CanCorrectSeasonToRecognized));
    }

    private void RaiseSeasonStateCommandCanExecuteChanged()
    {
        ToggleFavoriteCommand.RaiseCanExecuteChanged();
        ToggleWantToWatchCommand.RaiseCanExecuteChanged();
        ToggleNotInterestedCommand.RaiseCanExecuteChanged();
        MarkSeasonWatchedCommand.RaiseCanExecuteChanged();
        MarkSeasonUnwatchedCommand.RaiseCanExecuteChanged();
        AddSeasonToLibraryCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSeasonCorrectionCommandStates()
    {
        OpenSeasonCorrectionCommand.RaiseCanExecuteChanged();
        CancelSeasonCorrectionCommand.RaiseCanExecuteChanged();
        SearchSeasonCorrectionCandidatesCommand.RaiseCanExecuteChanged();
        CloseRecognizedSeasonPickerCommand.RaiseCanExecuteChanged();
        SelectRecognizedSeasonTargetCommand.RaiseCanExecuteChanged();
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
}
