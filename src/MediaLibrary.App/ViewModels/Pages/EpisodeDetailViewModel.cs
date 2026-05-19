using System.Collections.ObjectModel;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class EpisodeDetailViewModel : PageViewModelBase
{
    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly IMediaProbeService _mediaProbeService;
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
    private bool _hasEpisode;
    private bool _isUnidentified;
    private bool _hasSources;
    private bool _isOpeningPlayer;

    public EpisodeDetailViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService,
        IPlayerWindowService playerWindowService,
        IMediaProbeService mediaProbeService)
        : base("剧集详情", "查看单集基础信息、识别状态、进度和播放源。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        _playerWindowService = playerWindowService;
        _mediaProbeService = mediaProbeService;
        NavigateBackToSeasonCommand = new RelayCommand(NavigateBackToSeason, () => _seasonId.HasValue);
        OpenPlayerCommand = new AsyncRelayCommand(OpenPlayerAsync, _ => CanOpenPlayer);
        PlaySourceCommand = new AsyncRelayCommand(PlaySourceAsync, _ => CanOpenPlayer);
        ManualProbeSourceCommand = new RelayCommand(parameter => _ = ManualProbeSourceAsync(parameter), CanManualProbeSource);
        CorrectionPlaceholderCommand = new RelayCommand(ShowCorrectionPlaceholder, () => HasEpisode);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
        _playerWindowService.PlayerWindowClosed += OnPlayerWindowClosed;
        _mediaProbeService.ProbeStatusChanged += OnProbeStatusChanged;
    }

    public ObservableCollection<TvEpisodeSourceItem> Sources { get; } = [];

    public RelayCommand NavigateBackToSeasonCommand { get; }

    public AsyncRelayCommand OpenPlayerCommand { get; }

    public AsyncRelayCommand PlaySourceCommand { get; }

    public RelayCommand ManualProbeSourceCommand { get; }

    public RelayCommand CorrectionPlaceholderCommand { get; }

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

    public bool HasEpisode
    {
        get => _hasEpisode;
        private set
        {
            if (SetProperty(ref _hasEpisode, value))
            {
                OnPropertyChanged(nameof(HasNoEpisode));
                CorrectionPlaceholderCommand.RaiseCanExecuteChanged();
                RefreshPlayerCommandState();
            }
        }
    }

    public bool HasNoEpisode => !HasEpisode;

    public bool IsUnidentified
    {
        get => _isUnidentified;
        private set => SetProperty(ref _isUnidentified, value);
    }

    public bool HasSources
    {
        get => _hasSources;
        private set
        {
            if (SetProperty(ref _hasSources, value))
            {
                OnPropertyChanged(nameof(HasNoSources));
                RefreshPlayerCommandState();
            }
        }
    }

    public bool HasNoSources => !HasSources;

    public bool IsOpeningPlayer
    {
        get => _isOpeningPlayer;
        private set
        {
            if (SetProperty(ref _isOpeningPlayer, value))
            {
                RefreshPlayerCommandState();
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

            NavigateBackToSeasonCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(PrimaryPlayButtonText));
        OnPropertyChanged(nameof(SourcePlayButtonText));
        OpenPlayerCommand?.RaiseCanExecuteChanged();
        PlaySourceCommand?.RaiseCanExecuteChanged();
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

    private void ShowCorrectionPlaceholder()
    {
        StatusMessage = "修正信息将在后续阶段支持；当前不会调用 AI、TMDB 或修改识别结果。";
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
        HasSources = false;
        Sources.Clear();
        StatusMessage = statusMessage;
        NavigateBackToSeasonCommand.RaiseCanExecuteChanged();
        RefreshPlayerCommandState();
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }
}
