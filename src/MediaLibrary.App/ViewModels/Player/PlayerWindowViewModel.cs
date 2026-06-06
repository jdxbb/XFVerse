using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.Models.Player;
using MediaLibrary.App.Playback;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Player;

public sealed class PlayerWindowViewModel : ViewModelBase, IDisposable
{
    private const string AudioSwitchingStatusMessage = "\u97f3\u8f68\u5207\u6362\u4e2d...";
    private const string SubtitleSwitchingStatusMessage = "\u6b63\u5728\u5207\u6362\u5b57\u5e55...";
    private const int EnginePositionUiThrottleMilliseconds = 125;

    private enum PlaybackSourceMode
    {
        None,
        CompleteFile,
        LocalFile,
        WebDavDirect
    }

    private enum MainPlaybackUiState
    {
        Idle,
        Opening,
        LoadingMetadata,
        Starting,
        Playing,
        Paused,
        Seeking,
        Buffering,
        Recovering,
        Error,
        Closing
    }

    private enum PlayerOperationNoticeKind
    {
        None,
        Playback,
        Subtitle,
        Audio,
        Cache
    }

    private enum SubtitleSelectionKind
    {
        None,
        Embedded,
        External
    }

    private sealed record SubtitleSelection(
        SubtitleSelectionKind Kind,
        int? EmbeddedTrackId,
        int? ExternalSubtitleId,
        int? ExternalMpvTrackId,
        string SubtitleKey,
        string DisplayName,
        bool IsUserSelected,
        int RequestId)
    {
        public static SubtitleSelection None(int requestId = 0, bool isUserSelected = false)
        {
            return new SubtitleSelection(
                SubtitleSelectionKind.None,
                null,
                null,
                null,
                "none",
                NoSubtitleText,
                isUserSelected,
                requestId);
        }
    }

    private const string NoSubtitleText = "\u65e0";
    private const string EmbeddedPrefix = "\u5185\u5d4c\uff1a";
    private const string ExternalPrefix = "\u5916\u6302\uff1a";
    private const string SubtitleButtonPrefix = "\u5b57\u5e55\uff1a";
    private const string AudioTrackButtonPrefix = "\u97f3\u8f68\uff1a";
    private static readonly TimeSpan SubtitleTrackDiscoveryStableWindow = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan SubtitleTrackDiscoveryMaximumWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ShutdownWatchHistorySaveTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan PlayerPreferencesSaveDebounce = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan ResumeMessageDisplayDuration = TimeSpan.FromSeconds(3);
    private const int ResumeDurationToleranceSeconds = 300;
    private const double ResumeDurationToleranceRatio = 0.05d;

    private readonly IPlaybackSourceService _playbackSourceService;
    private readonly IWatchHistoryService _watchHistoryService;
    private readonly IVideoCacheService _videoCacheService;
    private readonly IPlayerPreferencesService _playerPreferencesService;
    private readonly IPlaybackEngineFactory _playbackEngineFactory;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IOpenSubtitlesClientService _openSubtitlesClientService;
    private readonly IOnlineSubtitleBindingQueryService _onlineSubtitleBindingQueryService;
    private readonly IOnlineSubtitleBindingService _onlineSubtitleBindingService;
    private readonly IOnlineSubtitleCacheService _onlineSubtitleCacheService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _playerPreferencesSaveTimer;
    private readonly SemaphoreSlim _playbackReloadLock = new(1, 1);
    private readonly SemaphoreSlim _subtitleSwitchLock = new(1, 1);
    private readonly SemaphoreSlim _audioTrackSwitchLock = new(1, 1);
    private readonly List<PlaybackSubtitleItem> _onlinePlaybackSubtitles = [];
    private readonly List<OnlineSubtitleMenuItemViewModel> _temporaryOnlineSubtitleMenuItems = [];
    private readonly PlaybackSubtitleItem _noneSubtitleItem = new()
    {
        DisplayName = NoSubtitleText,
        OriginalName = NoSubtitleText,
        UniqueKey = "none",
        TooltipText = "\u5b57\u5e55\u7c7b\u578b\uff1a\u65e0\r\nUniqueKey\uff1anone",
        Type = PlaybackSubtitleType.None,
        SubtitleMediaFileId = 0,
        FileName = NoSubtitleText,
        MatchType = SubtitleMatchType.Unknown,
        Priority = int.MinValue,
        IsNoneOption = true
    };

    private IPlaybackEngine? _playbackEngine;
    private IntPtr _playbackHostHandle;
    private VideoCachePlaybackLease? _currentVideoCacheLease;
    private PlaybackSessionModel? _session;
    private PlaybackContentType _currentContentType = PlaybackContentType.Movie;
    private PlaybackEpisodeNavigationItem? _previousEpisode;
    private PlaybackEpisodeNavigationItem? _nextEpisode;
    private PlaybackSourceItem? _selectedSource;
    private PlaybackSubtitleItem? _selectedSubtitle;
    private PlaybackSubtitleItem? _appliedSubtitle;
    private PlaybackAudioTrackItem? _selectedAudioTrack;
    private PlaybackAudioTrackItem? _appliedAudioTrack;
    private bool _suppressSourceAutoPlay;
    private bool _suppressSubtitleSelection;
    private bool _suppressAudioTrackSelection;
    private bool _hasUserSelectedSubtitle;
    private bool _hasUserSelectedAudioTrack;
    private bool _isApplyingSubtitle;
    private bool _isApplyingAudioTrack;
    private bool _isAudioTrackDiscoveryReady;
    private bool _hasAppliedAutomaticSubtitleForCurrentMedia;
    private bool _hasAppliedAutomaticAudioTrackForCurrentMedia;
    private DateTime _suppressAutomaticTrackSelectionUntilUtc;
    private SubtitleSelection _currentSubtitleSelection = SubtitleSelection.None();
    private SubtitleSelection? _pendingSubtitleSelection;
    private readonly Dictionary<string, int> _externalSubtitleMpvTrackIds = new(StringComparer.OrdinalIgnoreCase);
    private int _subtitleRefreshVersion;
    private int _subtitleApplyVersion;
    private int _subtitleSwitchUiVersion;
    private int? _pendingSubtitleSwitchSourceId;
    private int? _pendingSubtitleSwitchTargetSid;
    private bool _pendingSubtitleSwitchConfirmed;
    private DateTime _pendingSubtitleSwitchStartedUtc;
    private double _pendingSubtitleSwitchStartPosition;
    private bool _pendingSubtitleSwitchTimePositionLogged;
    private int _audioTrackRefreshVersion;
    private int _audioTrackSwitchUiVersion;
    private int? _pendingAudioTrackSwitchSourceId;
    private int? _pendingAudioTrackSwitchTargetAid;
    private bool _pendingAudioTrackSwitchConfirmed;
    private int _mpvTrackListChangeVersion;
    private int _subtitleTrackDiscoveryVersion;
    private int _subtitleTrackDiscoverySequence;
    private DateTime _subtitleTrackDiscoveryStartedUtc;
    private bool _isSubtitleTrackDiscoveryReady = true;
    private readonly HashSet<int> _lastSubtitleDiscoveryEmbeddedTrackIds = [];
    private int? _watchHistoryId;
    private int? _activeMediaFileId;
    private int? _pendingWatchHistoryMediaFileId;
    private int _pendingWatchHistoryInitialPositionSeconds;
    private bool _pendingWatchHistoryStartQueued;
    private DateTime _historyStartedAt;
    private bool _isUpdatingPosition;
    private bool _isStopping;
    private bool _isPlaybackRunning;
    private bool _isReloadingMedia;
    private bool _isStopped;
    private bool _disposed;
    private bool _isMpvMediaLoaded;
    private bool _isAwaitingInitialPlaybackProgress;
    private readonly object _enginePositionUiThrottleLock = new();
    private DateTime _lastEnginePositionUiUpdateUtc = DateTime.MinValue;
    private int _lastEnginePositionUiUpdateSecond = -1;
    private int _playbackDiagnosticsVersion;
    private PlaybackSourceMode _currentPlaybackMode = PlaybackSourceMode.None;
    private PlaybackSourceItem? _currentPlaybackSource;
    private bool _isSeekInProgress;
    private int _bufferingStateVersion;
    private DateTime _seekStartedAtUtc;
    private DateTime _suppressBufferingUntilUtc;
    private DateTime _holdPausedSeekDisplayUntilUtc;
    private double _seekTargetSeconds;
    private double _seekStartedFromSeconds;
    private double _lastObservedPlaybackSecondsAfterSeek = -1d;
    private double _pausedSeekDisplaySeconds;
    private string _movieTitle = "\u64ad\u653e\u5668";
    private string _statusMessage = "\u51c6\u5907\u64ad\u653e";
    private string _resumeMessage = string.Empty;
    private string _bufferingStatusText = string.Empty;
    private MainPlaybackUiState _mainPlaybackUiState = MainPlaybackUiState.Idle;
    private PlayerOperationNoticeKind _operationNoticeKind = PlayerOperationNoticeKind.None;
    private string _displayStatusText = "\u51c6\u5907\u64ad\u653e";
    private string _displayNoticeText = string.Empty;
    private string _bufferingOverlayText = string.Empty;
    private string? _mainPlaybackErrorText;
    private bool _isNoticeVisible;
    private bool _isBufferingOverlayVisible;
    private int _operationNoticeVersion;
    private string _playPauseText = "\u6682\u505c";
    private double _positionSeconds;
    private double _durationSeconds;
    private double _bufferingPercent;
    private bool _isBuffering;
    private int _volume = 80;
    private int _lastNonZeroVolume = 50;
    private int _brightness = 100;
    private int? _lastAppliedPlayerVolume;
    private int? _lastAppliedPlayerBrightness;
    private bool? _lastAppliedPlayerMuted;
    private bool _isMuted;
    private bool _isApplyingPlayerPreferences;
    private bool _hasUserChangedPlayerPreferencesThisSession;
    private bool _hasPendingPlayerPreferencesSave;
    private string _pendingPlayerPreferencesSaveReason = "preferences";
    private CancellationTokenSource? _resumeMessageClearCts;
    private int _temporaryOnlineSubtitleId;

    public PlayerWindowViewModel(
        IPlaybackSourceService playbackSourceService,
        IWatchHistoryService watchHistoryService,
        IVideoCacheService videoCacheService,
        IPlayerPreferencesService playerPreferencesService,
        IPlaybackEngineFactory playbackEngineFactory,
        IDataRefreshService dataRefreshService,
        IOpenSubtitlesClientService openSubtitlesClientService,
        IOnlineSubtitleBindingQueryService onlineSubtitleBindingQueryService,
        IOnlineSubtitleBindingService onlineSubtitleBindingService,
        IOnlineSubtitleCacheService onlineSubtitleCacheService,
        ISettingsService settingsService)
    {
        _playbackSourceService = playbackSourceService;
        _watchHistoryService = watchHistoryService;
        _videoCacheService = videoCacheService;
        _playerPreferencesService = playerPreferencesService;
        _playbackEngineFactory = playbackEngineFactory;
        _dataRefreshService = dataRefreshService;
        _openSubtitlesClientService = openSubtitlesClientService;
        _onlineSubtitleBindingQueryService = onlineSubtitleBindingQueryService;
        _onlineSubtitleBindingService = onlineSubtitleBindingService;
        _onlineSubtitleCacheService = onlineSubtitleCacheService;
        _settingsService = settingsService;
        _videoCacheService.StatusChanged += OnVideoCacheStatusChanged;

        TogglePlayPauseCommand = new RelayCommand(TogglePlayPause);
        StopCommand = new AsyncRelayCommand(StopAsync, () => !_isStopping);
        GoPreviousEpisodeCommand = new AsyncRelayCommand(
            () => OpenAdjacentEpisodeAsync(_previousEpisode, "previous-episode"),
            () => CanGoPreviousEpisode);
        GoNextEpisodeCommand = new AsyncRelayCommand(
            () => OpenAdjacentEpisodeAsync(_nextEpisode, "next-episode"),
            () => CanGoNextEpisode);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;

        _playerPreferencesSaveTimer = new DispatcherTimer
        {
            Interval = PlayerPreferencesSaveDebounce
        };
        _playerPreferencesSaveTimer.Tick += OnPlayerPreferencesSaveTimerTick;

        _ = LoadPlayerPreferencesAsync();
    }

    public void SetPlaybackHostHandle(IntPtr hostHandle)
    {
        if (hostHandle == IntPtr.Zero || _disposed)
        {
            return;
        }

        _playbackHostHandle = hostHandle;
    }

    public ObservableCollection<PlaybackSourceItem> Sources { get; } = [];

    public ObservableCollection<PlaybackSubtitleItem> Subtitles { get; } = [];

    public ObservableCollection<PlaybackSubtitleItem> EmbeddedSubtitles { get; } = [];

    public ObservableCollection<PlaybackSubtitleItem> ExternalSubtitles { get; } = [];

    public ObservableCollection<OnlineSubtitleMenuItemViewModel> OnlineSubtitleMenuItems { get; } = [];

    public ObservableCollection<PlaybackAudioTrackItem> AudioTracks { get; } = [];

    public PlaybackSubtitleItem NoneSubtitle => _noneSubtitleItem;

    public RelayCommand TogglePlayPauseCommand { get; }

    public AsyncRelayCommand StopCommand { get; }

    public AsyncRelayCommand GoPreviousEpisodeCommand { get; }

    public AsyncRelayCommand GoNextEpisodeCommand { get; }

    public string MovieTitle
    {
        get => _movieTitle;
        private set => SetProperty(ref _movieTitle, value);
    }

    public bool CanGoPreviousEpisode => _currentContentType == PlaybackContentType.Episode
                                        && _previousEpisode?.HasPlayableSource == true;

    public bool CanGoNextEpisode => _currentContentType == PlaybackContentType.Episode
                                    && _nextEpisode?.HasPlayableSource == true;

    public string PreviousEpisodeToolTip => _currentContentType != PlaybackContentType.Episode
        ? "\u7535\u5f71\u64ad\u653e\u4e0d\u652f\u6301\u4e0a\u4e00\u96c6"
        : _previousEpisode is null
            ? "\u5df2\u662f\u672c\u5b63\u7b2c\u4e00\u96c6"
            : _previousEpisode.HasPlayableSource
                ? _previousEpisode.DisplayText
                : "\u4e0a\u4e00\u96c6\u6682\u65e0\u53ef\u7528\u64ad\u653e\u6e90";

    public string NextEpisodeToolTip => _currentContentType != PlaybackContentType.Episode
        ? "\u7535\u5f71\u64ad\u653e\u4e0d\u652f\u6301\u4e0b\u4e00\u96c6"
        : _nextEpisode is null
            ? "\u5df2\u662f\u672c\u5b63\u6700\u540e\u4e00\u96c6"
            : _nextEpisode.HasPlayableSource
                ? _nextEpisode.DisplayText
                : "\u4e0b\u4e00\u96c6\u6682\u65e0\u53ef\u7528\u64ad\u653e\u6e90";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(AudioTrackMenuStatusText));
            }
        }
    }

    public string ResumeMessage
    {
        get => _resumeMessage;
        private set => SetProperty(ref _resumeMessage, value);
    }

    public bool IsBuffering
    {
        get => _isBuffering;
        private set => SetProperty(ref _isBuffering, value);
    }

    public double BufferingPercent
    {
        get => _bufferingPercent;
        private set => SetProperty(ref _bufferingPercent, value);
    }

    public string BufferingStatusText
    {
        get => _bufferingStatusText;
        private set => SetProperty(ref _bufferingStatusText, value);
    }

    public string DisplayStatusText
    {
        get => _displayStatusText;
        private set => SetProperty(ref _displayStatusText, value);
    }

    public string DisplayNoticeText
    {
        get => _displayNoticeText;
        private set => SetProperty(ref _displayNoticeText, value);
    }

    public bool IsNoticeVisible
    {
        get => _isNoticeVisible;
        private set => SetProperty(ref _isNoticeVisible, value);
    }

    public string BufferingOverlayText
    {
        get => _bufferingOverlayText;
        private set => SetProperty(ref _bufferingOverlayText, value);
    }

    public bool IsBufferingOverlayVisible
    {
        get => _isBufferingOverlayVisible;
        private set => SetProperty(ref _isBufferingOverlayVisible, value);
    }

    public string PlayPauseText
    {
        get => _playPauseText;
        private set => SetProperty(ref _playPauseText, value);
    }

    public string SubtitleButtonText => $"{SubtitleButtonPrefix}{(SelectedSubtitle ?? _noneSubtitleItem).DisplayName}";

    public string SubtitleButtonToolTip => (SelectedSubtitle ?? _noneSubtitleItem).TooltipText;

    public bool IsSubtitleTrackDiscoveryReady
    {
        get => _isSubtitleTrackDiscoveryReady;
        private set => SetProperty(ref _isSubtitleTrackDiscoveryReady, value);
    }

    public string AudioTrackButtonText => _selectedAudioTrack is null
        ? "\u97f3\u8f68"
        : $"{AudioTrackButtonPrefix}{_selectedAudioTrack.DisplayName}";

    public string AudioTrackButtonToolTip => _selectedAudioTrack?.TooltipText ?? "\u97f3\u8f68\u4e0d\u53ef\u7528";

    public bool IsAudioTrackDiscoveryReady
    {
        get => _isAudioTrackDiscoveryReady;
        private set
        {
            if (SetProperty(ref _isAudioTrackDiscoveryReady, value))
            {
                OnPropertyChanged(nameof(AudioTrackMenuStatusText));
            }
        }
    }

    public string AudioTrackMenuStatusText
    {
        get
        {
            if (_isApplyingAudioTrack || HasPendingAudioTrackSwitch())
            {
                return AudioSwitchingStatusMessage;
            }

            return string.Equals(StatusMessage, "\u97f3\u8f68\u5207\u6362\u5931\u8d25\u3002", StringComparison.Ordinal)
                ? "\u4e0a\u6b21\u97f3\u8f68\u5207\u6362\u5931\u8d25\uff0c\u5df2\u56de\u5230\u5f53\u524d\u97f3\u8f68\u3002"
                : string.Empty;
        }
    }

    public string SourceButtonText => SelectedSource is null
        ? "\u64ad\u653e\u6e90"
        : $"{SelectedSource.SourceTypeText} - {SelectedSource.FileName}";

    public PlaybackSourceItem? SelectedSource
    {
        get => _selectedSource;
        set
        {
            var previousSource = _selectedSource;
            if (SetProperty(ref _selectedSource, value) && value is not null)
            {
                OnPropertyChanged(nameof(SourceButtonText));
                if (_suppressSourceAutoPlay)
                {
                    ResetSubtitleStateForNewMedia();
                    ResetAudioTrackStateForNewMedia();
                    BuildSubtitleListForCurrentSource(value);
                }
                else
                {
                    UpdateSourceProgressSnapshot(previousSource, (int)Math.Max(0, PositionSeconds));
                    _ = SwitchSourceAsync(value, keepPosition: false);
                }
            }
        }
    }

    public PlaybackAudioTrackItem? SelectedAudioTrack
    {
        get => _selectedAudioTrack;
        set
        {
            if (SetProperty(ref _selectedAudioTrack, value))
            {
                OnPropertyChanged(nameof(AudioTrackButtonText));
                OnPropertyChanged(nameof(AudioTrackButtonToolTip));
                if (!_suppressAudioTrackSelection)
                {
                    _ = SwitchAudioTrackFromUiAsync(value);
                }
            }
        }
    }

    public PlaybackSubtitleItem? SelectedSubtitle
    {
        get => _selectedSubtitle;
        set
        {
            if (SetProperty(ref _selectedSubtitle, value))
            {
                OnPropertyChanged(nameof(SubtitleButtonText));
                OnPropertyChanged(nameof(SubtitleButtonToolTip));
                if (!_suppressSubtitleSelection)
                {
                    _ = SwitchSubtitleFromUiAsync(value);
                }
            }
        }
    }

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            var previousSeconds = _positionSeconds;
            if (!SetProperty(ref _positionSeconds, value) || _isUpdatingPosition)
            {
                return;
            }

            var engine = _playbackEngine;
            if (engine is not null
                && _isMpvMediaLoaded
                && engine.Duration > TimeSpan.Zero
                && Math.Abs(engine.Position.TotalSeconds - value) > 1)
            {
                var targetSeconds = Math.Max(0d, value);
                engine.Seek(TimeSpan.FromSeconds(targetSeconds));
                UpdateSeekState(targetSeconds, previousSeconds);
            }

            OnPropertyChanged(nameof(PositionText));
        }
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set
        {
            if (SetProperty(ref _durationSeconds, value))
            {
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public int Volume
    {
        get => _volume;
        set
        {
            var normalizedVolume = Math.Clamp(value, 0, 200);
            if (SetProperty(ref _volume, normalizedVolume))
            {
                if (_volume > 0)
                {
                    _lastNonZeroVolume = _volume;
                    _isMuted = false;
                }
                else
                {
                    _isMuted = true;
                }

                ApplyVolumeToPlayer();
                OnPropertyChanged(nameof(VolumeText));
                OnPropertyChanged(nameof(VolumeFeedbackText));
                OnPropertyChanged(nameof(IsVolumeBoosted));
                if (!_isApplyingPlayerPreferences)
                {
                    _hasUserChangedPlayerPreferencesThisSession = true;
                }

                SchedulePlayerPreferencesSave("volume");
            }
        }
    }

    public string VolumeText => $"{Volume}%";

    public string VolumeFeedbackText => $"{Volume}%";

    public bool IsVolumeBoosted => Volume > 100;

    public int Brightness
    {
        get => _brightness;
        set
        {
            var normalizedBrightness = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _brightness, normalizedBrightness))
            {
                ApplyBrightnessToPlayer();
                OnPropertyChanged(nameof(BrightnessText));
                if (!_isApplyingPlayerPreferences)
                {
                    _hasUserChangedPlayerPreferencesThisSession = true;
                }

                SchedulePlayerPreferencesSave("brightness");
            }
        }
    }

    public string BrightnessText => $"{Brightness}%";

    public string PositionText => FormatTime((int)PositionSeconds);

    public string DurationText => FormatTime((int)DurationSeconds);

    public async Task InitializeAsync(int movieId, int? mediaFileId, CancellationToken cancellationToken = default)
    {
        _session = await _playbackSourceService.GetPlaybackSessionAsync(movieId, mediaFileId, cancellationToken);
        await ApplySessionAsync(
            _session,
            "initialize-no-session",
            "\u672a\u627e\u5230\u5f71\u7247\u64ad\u653e\u4fe1\u606f\u3002",
            "\u5f53\u524d\u5f71\u7247\u6ca1\u6709\u53ef\u64ad\u653e\u7684\u89c6\u9891\u6e90\u3002",
            cancellationToken);
    }

    public async Task InitializeEpisodeAsync(int episodeId, int? mediaFileId, CancellationToken cancellationToken = default)
    {
        _session = await _playbackSourceService.GetEpisodePlaybackSessionAsync(episodeId, mediaFileId, cancellationToken);
        await ApplySessionAsync(
            _session,
            "initialize-no-episode-session",
            "\u672a\u627e\u5230\u8be5\u96c6\u7684\u64ad\u653e\u4fe1\u606f\u3002",
            "\u5f53\u524d\u96c6\u6682\u65e0\u53ef\u64ad\u653e\u7684\u89c6\u9891\u6e90\u3002",
            cancellationToken);
    }

    private async Task ApplySessionAsync(
        PlaybackSessionModel? session,
        string missingSessionReason,
        string missingSessionMessage,
        string missingSourceMessage,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        if (session is null)
        {
            SetMainPlaybackUiState(MainPlaybackUiState.Error, missingSessionReason, missingSessionMessage);
            return;
        }

        ApplySessionMetadata(session);
        Sources.Clear();
        foreach (var source in session.Sources)
        {
            Sources.Add(source);
        }

        await RefreshVideoCacheStatusesAsync(cancellationToken);

        _suppressSourceAutoPlay = true;
        SelectedSource = Sources.FirstOrDefault(x => x.MediaFileId == session.SelectedMediaFileId) ?? Sources.FirstOrDefault();
        _suppressSourceAutoPlay = false;
        if (SelectedSource is null)
        {
            SetMainPlaybackUiState(MainPlaybackUiState.Error, "initialize-no-source", missingSourceMessage);
            return;
        }

        await RefreshOnlineSubtitleMenuItemsAsync(cancellationToken);
        await PlayCurrentSourceAsync(false);
    }

    private void ApplySessionMetadata(PlaybackSessionModel session)
    {
        _session = session;
        _currentContentType = session.ContentType;
        _previousEpisode = session.PreviousEpisode;
        _nextEpisode = session.NextEpisode;
        MovieTitle = string.IsNullOrWhiteSpace(session.DisplayTitle) ? "\u64ad\u653e\u5668" : session.DisplayTitle;
        RaiseEpisodeNavigationStateChanged();
    }

    public async Task SaveAndCloseAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        MpvPlaybackDiagnostics.Write("player-close-start");
        MpvPlaybackDiagnostics.Write("player-r4-shutdown-start reason=close");
        try
        {
            await FlushPlayerPreferencesAsync("close", TimeSpan.FromMilliseconds(300));
            await ShutdownPlaybackAsync("close", resetPosition: false, updateUi: false);
        }
        catch
        {
            // Window close must remain non-fatal even if the playback engine is already tearing down.
        }
        finally
        {
            MpvPlaybackDiagnostics.Write($"player-close-complete elapsedMs={stopwatch.ElapsedMilliseconds}");
            MpvPlaybackDiagnostics.Write($"player-r4-shutdown-complete reason=close elapsedMs={stopwatch.ElapsedMilliseconds}");
            if (stopwatch.ElapsedMilliseconds >= 3000)
            {
                MpvPlaybackDiagnostics.Write($"player-close-slow stage=save-and-close elapsedMs={stopwatch.ElapsedMilliseconds}");
                MpvPlaybackDiagnostics.Write($"player-r4-shutdown-slow stage=save-and-close elapsedMs={stopwatch.ElapsedMilliseconds}");
            }
        }
    }

    public void SelectSubtitleFromMenu(PlaybackSubtitleItem subtitle)
    {
        MpvPlaybackDiagnostics.Write(
            $"subtitle-debug-click targetTrackId={FormatOptionalTrackId(subtitle.TrackId)} type={subtitle.Type}");
        if (subtitle.Type == PlaybackSubtitleType.EmbeddedTrack && !IsSubtitleTrackDiscoveryReady)
        {
            MpvPlaybackDiagnostics.Write("subtitle-menu-open-before-ready");
        }

        _ = SwitchSubtitleFromUiAsync(subtitle);
    }

    public void NotifySubtitleMenuOpened()
    {
        if (!IsSubtitleTrackDiscoveryReady)
        {
            MpvPlaybackDiagnostics.Write("subtitle-menu-open-before-ready");
        }
    }

    public void PauseForOnlineSubtitleSearch()
    {
        if (_disposed)
        {
            return;
        }

        var isCurrentlyPlaying = _playbackEngine?.IsPlaying ?? _isPlaybackRunning;
        if (!isCurrentlyPlaying)
        {
            MpvPlaybackDiagnostics.Write("online-subtitle-search-open paused=false");
            return;
        }

        try
        {
            _playbackEngine?.Pause();
        }
        catch
        {
            // The search dialog should still open even if the engine is between lifecycle states.
        }

        SetPlaybackState(false);
        ClearSeekRecovery(resetBuffering: true);
        SetMainPlaybackUiState(MainPlaybackUiState.Paused, "online-subtitle-search-open");
        MpvPlaybackDiagnostics.Write("online-subtitle-search-open paused=true");
    }

    public OnlineSubtitleSearchViewModel CreateOnlineSubtitleSearchViewModel()
    {
        var context = OnlineSubtitleSearchContext.FromPlayback(
            _session,
            SelectedSource,
            "zh-cn");
        return new OnlineSubtitleSearchViewModel(
            _openSubtitlesClientService,
            _settingsService,
            _onlineSubtitleBindingService,
            _onlineSubtitleCacheService,
            context,
            ApplyDownloadedOnlineSubtitleAsync);
    }

    public bool HasPlayableOnlineSubtitleSearchContext => _session is not null && SelectedSource is not null;

    public void SelectOnlineSubtitleFromMenu(OnlineSubtitleMenuItemViewModel subtitle)
    {
        _ = SelectOnlineSubtitleFromMenuAsync(subtitle);
    }

    public void DeleteOnlineSubtitleFromMenu(OnlineSubtitleMenuItemViewModel subtitle)
    {
        _ = DeleteOnlineSubtitleFromMenuAsync(subtitle);
    }

    public void SelectAudioTrackFromMenu(PlaybackAudioTrackItem audioTrack)
    {
        _ = SwitchAudioTrackFromUiAsync(audioTrack);
    }

    private Task SwitchSubtitleFromUiAsync(PlaybackSubtitleItem? subtitle)
    {
        return ApplySubtitleSelectionAsync(subtitle, isUserInitiated: true);
    }

    private Task SwitchAudioTrackFromUiAsync(PlaybackAudioTrackItem? audioTrack)
    {
        return ApplyAudioTrackSelectionAsync(audioTrack, isUserInitiated: true);
    }

    public bool IsSubtitleSelected(PlaybackSubtitleItem subtitle)
    {
        return IsSameSubtitle(SelectedSubtitle ?? _noneSubtitleItem, subtitle);
    }

    public bool IsAudioTrackSelected(PlaybackAudioTrackItem audioTrack)
    {
        return IsSameAudioTrack(SelectedAudioTrack, audioTrack);
    }

    public void SeekBySeconds(int deltaSeconds)
    {
        if (!_isMpvMediaLoaded)
        {
            return;
        }

        var duration = _playbackEngine?.Duration.TotalSeconds > 0 ? _playbackEngine.Duration.TotalSeconds : DurationSeconds;
        var target = PositionSeconds + deltaSeconds;
        if (duration > 0)
        {
            target = Math.Min(duration, target);
        }

        PositionSeconds = Math.Max(0, target);
    }

    public void AdjustVolume(int delta)
    {
        Volume = Math.Clamp(Volume + delta, 0, 200);
    }

    public void AdjustBrightness(int delta)
    {
        Brightness = Math.Clamp(Brightness + delta, 0, 100);
    }

    public void ToggleMute()
    {
        if (!_isMuted && Volume > 0)
        {
            _lastNonZeroVolume = Volume;
            Volume = 0;
            return;
        }

        Volume = _lastNonZeroVolume > 0 ? _lastNonZeroVolume : 50;
    }

    private void RaiseEpisodeNavigationStateChanged()
    {
        OnPropertyChanged(nameof(CanGoPreviousEpisode));
        OnPropertyChanged(nameof(CanGoNextEpisode));
        OnPropertyChanged(nameof(PreviousEpisodeToolTip));
        OnPropertyChanged(nameof(NextEpisodeToolTip));
        GoPreviousEpisodeCommand.RaiseCanExecuteChanged();
        GoNextEpisodeCommand.RaiseCanExecuteChanged();
    }

    private async Task OpenAdjacentEpisodeAsync(PlaybackEpisodeNavigationItem? target, string reason)
    {
        if (_disposed || target is null)
        {
            return;
        }

        if (!target.HasPlayableSource)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Playback, "\u76ee\u6807\u96c6\u6682\u65e0\u53ef\u7528\u64ad\u653e\u6e90\u3002", reason);
            return;
        }

        await SwitchToEpisodeAsync(target.EpisodeId, reason);
    }

    private async Task SwitchToEpisodeAsync(int episodeId, string reason)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await PersistProgressSnapshotAsync((int)Math.Max(0, PositionSeconds), IsCompleted());
            _watchHistoryId = null;
            _activeMediaFileId = null;
            ClearPendingWatchHistoryStart();

            var nextSession = await _playbackSourceService.GetEpisodePlaybackSessionAsync(episodeId);
            if (nextSession is null)
            {
                SetOperationNotice(PlayerOperationNoticeKind.Playback, "\u672a\u627e\u5230\u76ee\u6807\u96c6\u7684\u64ad\u653e\u4fe1\u606f\u3002", reason);
                return;
            }

            if (nextSession.Sources.Count == 0)
            {
                ApplySessionMetadata(nextSession);
                Sources.Clear();
                SetMainPlaybackUiState(MainPlaybackUiState.Error, "episode-no-source", "\u76ee\u6807\u96c6\u6682\u65e0\u53ef\u7528\u64ad\u653e\u6e90\u3002");
                return;
            }

            await ApplySessionAsync(
                nextSession,
                "episode-session-missing",
                "\u672a\u627e\u5230\u76ee\u6807\u96c6\u7684\u64ad\u653e\u4fe1\u606f\u3002",
                "\u76ee\u6807\u96c6\u6682\u65e0\u53ef\u7528\u64ad\u653e\u6e90\u3002",
                CancellationToken.None);
            SetOperationNotice(PlayerOperationNoticeKind.Playback, "\u5df2\u5207\u6362\u5230\u76ee\u6807\u96c6\u3002", reason);
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"episode-switch-failed reason={reason} errorType={exception.GetType().Name}");
            SetOperationNotice(PlayerOperationNoticeKind.Playback, "\u5207\u6362\u96c6\u6570\u5931\u8d25\uff0c\u53ef\u91cd\u8bd5\u3002", reason);
        }
    }

    private async Task PlayCurrentSourceAsync(bool keepPosition, bool preservePlaybackState = false)
    {
        if (_disposed || SelectedSource is null || _session is null)
        {
            return;
        }

        var source = SelectedSource;
        var selectedSubtitle = SelectedSubtitle ?? _noneSubtitleItem;
        var shouldResumePlaying = ShouldResumePlaybackAfterReload(preservePlaybackState);
        var reloadSucceeded = await ReloadCurrentMediaAsync(
            source,
            selectedSubtitle,
            keepPosition,
            preservePlaybackState,
            updateResumeMessage: true,
            attachExternalSubtitleToMedia: false);
        if (!reloadSucceeded)
        {
            _watchHistoryId = null;
            _activeMediaFileId = null;
            ClearPendingWatchHistoryStart();
            _timer.Stop();
            StopCommand.RaiseCanExecuteChanged();
            TracePlayback($"playback-state-reset-after-failure mediaFileId={source.MediaFileId}");
            return;
        }

        if (PlaybackLoadReturnsOnCommandSubmitted())
        {
            QueueWatchHistoryStartAfterPlaybackReady(source.MediaFileId, ResolveSourceResumePosition(source));
            SetMainPlaybackUiState(shouldResumePlaying ? MainPlaybackUiState.Starting : MainPlaybackUiState.Paused, "load-submitted");
            SetPlaybackState(false);
            StopCommand.RaiseCanExecuteChanged();
            MpvPlaybackDiagnostics.Write($"mpv-r1-watch-history-deferred mediaFileId={source.MediaFileId}");
            return;
        }

        _watchHistoryId = await StartWatchHistoryForCurrentSessionAsync(
            source.MediaFileId,
            ResolveSourceResumePosition(source));
        MpvPlaybackDiagnostics.Write($"mpv-watch-history-start mediaFileId={source.MediaFileId}");
        _activeMediaFileId = source.MediaFileId;
        _historyStartedAt = DateTime.UtcNow;
        SetMainPlaybackUiState(shouldResumePlaying ? MainPlaybackUiState.Playing : MainPlaybackUiState.Paused, "watch-history-started");
        SetPlaybackState(shouldResumePlaying);
        _timer.Start();
        StopCommand.RaiseCanExecuteChanged();
    }

    private bool ShouldResumePlaybackAfterReload(bool preservePlaybackState)
    {
        return !preservePlaybackState || _isPlaybackRunning || !_isMpvMediaLoaded;
    }

    private bool PlaybackEngineDefersTrackFeatures()
    {
        return _playbackEngine is IPlaybackEngineFeatureFlags { DefersTrackFeatures: true };
    }

    private bool PlaybackLoadReturnsOnCommandSubmitted()
    {
        return _playbackEngine is IPlaybackEngineFeatureFlags { LoadReturnsOnCommandSubmitted: true };
    }

    private void QueueWatchHistoryStartAfterPlaybackReady(int mediaFileId, int initialPositionSeconds)
    {
        _pendingWatchHistoryMediaFileId = mediaFileId;
        _pendingWatchHistoryInitialPositionSeconds = Math.Max(0, initialPositionSeconds);
        _pendingWatchHistoryStartQueued = true;
    }

    private void ClearPendingWatchHistoryStart()
    {
        _pendingWatchHistoryMediaFileId = null;
        _pendingWatchHistoryInitialPositionSeconds = 0;
        _pendingWatchHistoryStartQueued = false;
    }

    private Task<int> StartWatchHistoryForCurrentSessionAsync(int mediaFileId, int initialPositionSeconds)
    {
        if (_session?.ContentType == PlaybackContentType.Episode && _session.EpisodeId.HasValue)
        {
            return _watchHistoryService.StartEpisodeAsync(
                _session.EpisodeId.Value,
                mediaFileId,
                initialPositionSeconds);
        }

        if (_session is null)
        {
            throw new InvalidOperationException("Playback session is not initialized.");
        }

        return _watchHistoryService.StartAsync(
            _session.MovieId,
            mediaFileId,
            initialPositionSeconds);
    }

    private async Task StartPendingWatchHistoryAfterPlaybackReadyAsync()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => _ = StartPendingWatchHistoryAfterPlaybackReadyAsync());
            return;
        }

        if (_disposed || _session is null || SelectedSource is null)
        {
            ClearPendingWatchHistoryStart();
            return;
        }

        if (!_pendingWatchHistoryStartQueued || !_pendingWatchHistoryMediaFileId.HasValue)
        {
            return;
        }

        var mediaFileId = _pendingWatchHistoryMediaFileId.Value;
        if (SelectedSource.MediaFileId != mediaFileId)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r1-watch-history-skipped mediaFileId={mediaFileId} currentMediaFileId={SelectedSource.MediaFileId} reason=source-changed");
            ClearPendingWatchHistoryStart();
            return;
        }

        if (_watchHistoryId.HasValue && _activeMediaFileId == mediaFileId)
        {
            ClearPendingWatchHistoryStart();
            return;
        }

        var initialPositionSeconds = _pendingWatchHistoryInitialPositionSeconds;
        ClearPendingWatchHistoryStart();
        try
        {
            var watchHistoryId = await StartWatchHistoryForCurrentSessionAsync(
                mediaFileId,
                initialPositionSeconds);
            if (_disposed || SelectedSource?.MediaFileId != mediaFileId)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r1-watch-history-skipped mediaFileId={mediaFileId} reason=source-changed-after-start");
                return;
            }

            _watchHistoryId = watchHistoryId;
            MpvPlaybackDiagnostics.Write($"mpv-watch-history-start mediaFileId={mediaFileId}");
            _activeMediaFileId = mediaFileId;
            _historyStartedAt = DateTime.UtcNow;
            SetMainPlaybackUiState(MainPlaybackUiState.Playing, "watch-history-ready");
            _timer.Start();
            StopCommand.RaiseCanExecuteChanged();
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r1-watch-history-start-failed mediaFileId={mediaFileId} errorType={exception.GetType().Name}");
        }
    }

    private async Task RefreshVideoCacheStatusesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var source in Sources.Where(x => x.ProtocolType == ProtocolType.WebDav))
        {
            await RefreshVideoCacheStatusAsync(source, cancellationToken);
        }
    }

    private async Task RefreshVideoCacheStatusAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _videoCacheService.GetStatusAsync(source, cancellationToken);
            ApplyVideoCacheStatus(source, status);
        }
        catch
        {
            // Cache status is advisory; playback must continue through the normal WebDAV path.
        }
    }

    private void OnVideoCacheStatusChanged(object? sender, VideoCacheChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            try
            {
                _ = dispatcher.InvokeAsync(
                    () =>
                    {
                        if (!_disposed)
                        {
                            ApplyVideoCacheStatusChanged(e);
                        }
                    });
            }
            catch
            {
            }

            return;
        }

        ApplyVideoCacheStatusChanged(e);
    }

    private void ApplyVideoCacheStatusChanged(VideoCacheChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var source in Sources.Where(x => x.MediaFileId == e.MediaFileId))
        {
            ApplyVideoCacheStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = e.Status,
                    ProgressPercent = e.ProgressPercent,
                    Error = e.Error
                });
        }
    }

    private void ApplyVideoCacheStatus(PlaybackSourceItem source, VideoCacheStatusResult status)
    {
        if (_disposed)
        {
            return;
        }

        source.VideoCacheStatus = status.Status;
        source.VideoCacheProgressPercent = status.ProgressPercent;
        source.VideoCacheError = status.Error;
        OnPropertyChanged(nameof(SourceButtonText));
    }

    private async Task SwitchSourceAsync(PlaybackSourceItem source, bool keepPosition)
    {
        if (ReferenceEquals(SelectedSource, source) && !_disposed)
        {
            ClearOperationNotice("source-switch");
            SetMainPlaybackUiState(MainPlaybackUiState.Opening, "source-switch");
            SetBufferingState(0d);
        }

        await _subtitleSwitchLock.WaitAsync();
        var oldMediaFileId = _activeMediaFileId ?? _currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0;
        try
        {
            MpvPlaybackDiagnostics.Write($"mpv-source-switch-start oldMediaFileId={oldMediaFileId} newMediaFileId={source.MediaFileId}");
            if (!ReferenceEquals(SelectedSource, source) || _disposed)
            {
                return;
            }

            ResetSubtitleStateForNewMedia();
            ResetAudioTrackStateForNewMedia();
            BuildSubtitleListForCurrentSource(source);
            await RefreshOnlineSubtitleMenuItemsAsync();
            MpvPlaybackDiagnostics.Write($"mpv-source-switch-load-new-start mediaFileId={source.MediaFileId}");
            await PlayCurrentSourceAsync(keepPosition);
            MpvPlaybackDiagnostics.Write($"mpv-source-switch-load-new-result mediaFileId={source.MediaFileId} success={(_activeMediaFileId == source.MediaFileId).ToString().ToLowerInvariant()}");
            MpvPlaybackDiagnostics.Write(_activeMediaFileId == source.MediaFileId
                ? $"mpv-source-switch-success mediaFileId={source.MediaFileId}"
                : $"mpv-source-switch-failed mediaFileId={source.MediaFileId} reason=load-failed");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-source-switch-failed mediaFileId={source.MediaFileId} errorType={exception.GetType().Name}");
            ResetBufferingState();
            SetStoppedState();
            SetPlaybackState(false);
            SetMainPlaybackUiState(MainPlaybackUiState.Error, "source-switch-failed", "\u64ad\u653e\u6e90\u5207\u6362\u5931\u8d25\uff0c\u53ef\u91cd\u8bd5\u3002");
        }
        finally
        {
            _subtitleSwitchLock.Release();
        }
    }

    private void ResetSubtitleStateForNewMedia()
    {
        Interlocked.Increment(ref _subtitleRefreshVersion);
        Interlocked.Increment(ref _subtitleApplyVersion);
        _appliedSubtitle = null;
        _hasUserSelectedSubtitle = false;
        _hasAppliedAutomaticSubtitleForCurrentMedia = false;
        _isApplyingSubtitle = false;
        _externalSubtitleMpvTrackIds.Clear();
        _currentSubtitleSelection = SubtitleSelection.None();
        _pendingSubtitleSelection = null;
        _pendingSubtitleSwitchSourceId = null;
        _pendingSubtitleSwitchTargetSid = null;
        _pendingSubtitleSwitchConfirmed = false;
        _pendingSubtitleSwitchStartedUtc = DateTime.MinValue;
        _pendingSubtitleSwitchStartPosition = 0d;
        _pendingSubtitleSwitchTimePositionLogged = false;
        _suppressAutomaticTrackSelectionUntilUtc = DateTime.MinValue;
        BeginSubtitleTrackDiscovery(SelectedSource?.MediaFileId ?? _currentPlaybackSource?.MediaFileId ?? 0);
        ClearSeekRecovery(resetBuffering: true);
    }

    private void ResetAudioTrackStateForNewMedia()
    {
        Interlocked.Increment(ref _audioTrackRefreshVersion);
        _appliedAudioTrack = null;
        _hasUserSelectedAudioTrack = false;
        _hasAppliedAutomaticAudioTrackForCurrentMedia = false;
        _isApplyingAudioTrack = false;
        IsAudioTrackDiscoveryReady = false;
        AudioTracks.Clear();
        SetSelectedAudioTrackSilently(null);
    }

    private void BeginSubtitleTrackDiscovery(int mediaFileId)
    {
        _subtitleTrackDiscoveryStartedUtc = DateTime.UtcNow;
        _subtitleTrackDiscoverySequence = 0;
        _lastSubtitleDiscoveryEmbeddedTrackIds.Clear();
        IsSubtitleTrackDiscoveryReady = false;
        var version = Interlocked.Increment(ref _subtitleTrackDiscoveryVersion);
        MpvPlaybackDiagnostics.Write($"subtitle-track-discovery-start mediaFileId={mediaFileId}");
        MpvPlaybackDiagnostics.Write("subtitle-menu-ready state=false reason=media-load");
        _ = CompleteSubtitleTrackDiscoveryOnTimeoutAsync(mediaFileId, version);
    }

    private async Task CompleteSubtitleTrackDiscoveryOnTimeoutAsync(int mediaFileId, int version)
    {
        try
        {
            await Task.Delay(SubtitleTrackDiscoveryMaximumWait);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() => MarkSubtitleTrackDiscoveryReady(mediaFileId, version, "timeout"));
                return;
            }

            MarkSubtitleTrackDiscoveryReady(mediaFileId, version, "timeout");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"subtitle-track-discovery-timeout-failed errorType={exception.GetType().Name}");
        }
    }

    private void RecordSubtitleTrackDiscoveryUpdate(int mediaFileId, IReadOnlyList<PlaybackSubtitleItem> tracks)
    {
        if (_disposed || IsSubtitleTrackDiscoveryReady)
        {
            return;
        }

        var version = Volatile.Read(ref _subtitleTrackDiscoveryVersion);
        var sequence = Interlocked.Increment(ref _subtitleTrackDiscoverySequence);
        var elapsedMs = GetSubtitleTrackDiscoveryElapsedMs();
        var currentIds = tracks
            .Where(track => track.TrackId.HasValue)
            .Select(track => track.TrackId!.Value)
            .OrderBy(trackId => trackId)
            .ToHashSet();

        MpvPlaybackDiagnostics.Write(
            $"subtitle-track-discovery-update subCount={tracks.Count} embeddedCount={currentIds.Count} externalTrackCount=0");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-track-list-update seq={sequence} elapsedMs={elapsedMs} embeddedIds={FormatTrackIds(currentIds)} externalIds=none selectedSid=unknown");

        foreach (var trackId in currentIds.Except(_lastSubtitleDiscoveryEmbeddedTrackIds).OrderBy(trackId => trackId))
        {
            MpvPlaybackDiagnostics.Write($"subtitle-track-added source=mpv-embedded trackId={trackId}");
        }

        foreach (var trackId in _lastSubtitleDiscoveryEmbeddedTrackIds.Except(currentIds).OrderBy(trackId => trackId))
        {
            MpvPlaybackDiagnostics.Write($"subtitle-track-removed source=mpv-embedded trackId={trackId}");
        }

        _lastSubtitleDiscoveryEmbeddedTrackIds.Clear();
        foreach (var trackId in currentIds)
        {
            _lastSubtitleDiscoveryEmbeddedTrackIds.Add(trackId);
        }

        _ = CompleteSubtitleTrackDiscoveryWhenStableAsync(mediaFileId, version, sequence);
    }

    private async Task CompleteSubtitleTrackDiscoveryWhenStableAsync(int mediaFileId, int version, int sequence)
    {
        try
        {
            await Task.Delay(SubtitleTrackDiscoveryStableWindow);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() => MarkSubtitleTrackDiscoveryReady(mediaFileId, version, "stable", sequence));
                return;
            }

            MarkSubtitleTrackDiscoveryReady(mediaFileId, version, "stable", sequence);
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"subtitle-track-discovery-stable-failed errorType={exception.GetType().Name}");
        }
    }

    private void MarkSubtitleTrackDiscoveryReady(int mediaFileId, int version, string reason, int? sequence = null)
    {
        if (_disposed
            || version != Volatile.Read(ref _subtitleTrackDiscoveryVersion)
            || IsSubtitleTrackDiscoveryReady
            || SelectedSource?.MediaFileId != mediaFileId)
        {
            return;
        }

        if (sequence.HasValue && sequence.Value != Volatile.Read(ref _subtitleTrackDiscoverySequence))
        {
            return;
        }

        if (!_isMpvMediaLoaded)
        {
            MpvPlaybackDiagnostics.Write(
                $"subtitle-menu-ready state=false reason=waiting-file-loaded trigger={reason}");
            return;
        }

        var elapsedMs = GetSubtitleTrackDiscoveryElapsedMs();
        IsSubtitleTrackDiscoveryReady = true;
        if (string.Equals(reason, "timeout", StringComparison.Ordinal))
        {
            MpvPlaybackDiagnostics.Write(
                $"subtitle-track-discovery-timeout elapsedMs={elapsedMs} embeddedCount={EmbeddedSubtitles.Count}");
        }
        else
        {
            MpvPlaybackDiagnostics.Write(
                $"subtitle-track-discovery-stable elapsedMs={elapsedMs} embeddedCount={EmbeddedSubtitles.Count}");
        }

        MpvPlaybackDiagnostics.Write($"subtitle-menu-ready state=true reason={reason}");
        if (!_hasAppliedAutomaticSubtitleForCurrentMedia
            && !_hasUserSelectedSubtitle
            && !_isApplyingSubtitle
            && DateTime.UtcNow >= _suppressAutomaticTrackSelectionUntilUtc)
        {
            MpvPlaybackDiagnostics.Write("subtitle-auto-select-start reason=track-discovery-ready");
            _ = ApplyAutomaticDefaultSubtitleAsync();
        }
    }

    private long GetSubtitleTrackDiscoveryElapsedMs()
    {
        return _subtitleTrackDiscoveryStartedUtc == DateTime.MinValue
            ? 0
            : Math.Max(0, (long)(DateTime.UtcNow - _subtitleTrackDiscoveryStartedUtc).TotalMilliseconds);
    }

    private long GetSubtitleSwitchElapsedMs()
    {
        return _pendingSubtitleSwitchStartedUtc == DateTime.MinValue
            ? 0
            : Math.Max(0, (long)(DateTime.UtcNow - _pendingSubtitleSwitchStartedUtc).TotalMilliseconds);
    }

    private void BuildSubtitleListForCurrentSource(PlaybackSourceItem source)
    {
        MpvPlaybackDiagnostics.Write("subtitle-audit-found method=source-external-candidates");
        MpvPlaybackDiagnostics.Write($"subtitle-menu-build-start mediaFileId={source.MediaFileId}");
        Subtitles.Clear();
        EmbeddedSubtitles.Clear();
        ExternalSubtitles.Clear();
        OnlineSubtitleMenuItems.Clear();
        _onlinePlaybackSubtitles.Clear();
        _temporaryOnlineSubtitleMenuItems.Clear();
        Subtitles.Add(_noneSubtitleItem);

        var seenExternalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var subtitle in source.Subtitles
                     .Where(x => x.Type == PlaybackSubtitleType.ExternalFile)
                     .OrderBy(x => !IsAutoSubtitle(x))
                     .ThenBy(x => x.Priority))
        {
            subtitle.Type = PlaybackSubtitleType.ExternalFile;
            PrepareSubtitleDisplay(subtitle, ExternalSubtitles.Count + 1, source.FileName);
            if (!seenExternalKeys.Add(EnsureSubtitleUniqueKey(subtitle)))
            {
                continue;
            }

            Subtitles.Add(subtitle);
            ExternalSubtitles.Add(subtitle);
        }

        SetSelectedSubtitleSilently(_noneSubtitleItem);
        _currentSubtitleSelection = SubtitleSelection.None();
        MpvPlaybackDiagnostics.Write(
            $"subtitle-menu-build-result embeddedCount=0 externalCandidateCount={ExternalSubtitles.Count} mpvExternalCount=0 dedupedCount={source.Subtitles.Count(x => x.Type == PlaybackSubtitleType.ExternalFile) - ExternalSubtitles.Count} selectedKind=none");
    }

    private async Task RefreshOnlineSubtitleMenuItemsAsync(CancellationToken cancellationToken = default)
    {
        OnlineSubtitleMenuItems.Clear();
        _onlinePlaybackSubtitles.RemoveAll(IsLongTermOnlineSubtitle);
        if (_session is null)
        {
            AddTemporaryOnlineSubtitleMenuItems();
            return;
        }

        var (movieId, episodeId, mediaFileId) = GetCurrentOnlineSubtitleTargets();

        if (!movieId.HasValue && !episodeId.HasValue && !mediaFileId.HasValue)
        {
            AddTemporaryOnlineSubtitleMenuItems();
            return;
        }

        var bindings = await _onlineSubtitleBindingQueryService.GetActiveBindingsAsync(
            movieId,
            episodeId,
            mediaFileId,
            cancellationToken);

        foreach (var binding in bindings)
        {
            AddOnlineBindingMenuItem(binding);
        }

        AddTemporaryOnlineSubtitleMenuItems();
    }

    private void AddOnlineBindingMenuItem(OnlineSubtitleBindingListItem binding)
    {
        var displayName = BuildOnlineSubtitleMenuDisplayName(binding);
        var subtitle = binding.HasCacheFile
            ? CreateOnlinePlaybackSubtitle(
                binding.Id,
                displayName,
                binding.FileName,
                _onlineSubtitleCacheService.GetAbsolutePath(binding.CacheRelativePath))
            : null;
        if (subtitle is not null)
        {
            UpsertOnlinePlaybackSubtitle(subtitle);
        }

        var item = new OnlineSubtitleMenuItemViewModel
        {
            BindingId = binding.Id,
            MovieId = binding.MovieId,
            EpisodeId = binding.EpisodeId,
            MediaFileId = binding.MediaFileId,
            TargetKind = binding.TargetKind,
            DisplayName = displayName,
            ToolTip = binding.HasCacheFile
                ? "\u5df2\u7ed1\u5b9a\u7684\u5728\u7ebf\u5b57\u5e55\uff0c\u53ef\u5207\u6362\u64ad\u653e\u6216\u5220\u9664\u7ed1\u5b9a\u3002"
                : "\u5df2\u7ed1\u5b9a\uff0c\u4f46\u5f53\u524d\u7f13\u5b58\u6587\u4ef6\u4e0d\u53ef\u7528\uff0c\u53ef\u91cd\u65b0\u4e0b\u8f7d\u3002",
            HasCacheFile = binding.HasCacheFile,
            IsTemporary = false,
            SubtitleUniqueKey = subtitle?.UniqueKey ?? $"online:binding:{binding.Id}",
            CacheRelativePath = binding.CacheRelativePath,
            FileName = binding.FileName
        };
        OnlineSubtitleMenuItems.Add(item);
    }

    private void AddTemporaryOnlineSubtitleMenuItems()
    {
        foreach (var item in _temporaryOnlineSubtitleMenuItems)
        {
            OnlineSubtitleMenuItems.Add(item);
        }
    }

    private static string BuildOnlineSubtitleMenuDisplayName(OnlineSubtitleBindingListItem binding)
    {
        var name = new[] { binding.DisplayName, binding.ReleaseName, binding.FileName }
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? $"\u5728\u7ebf\u5b57\u5e55 {binding.Id}";
        var language = !string.IsNullOrWhiteSpace(binding.LanguageName)
            ? binding.LanguageName
            : binding.LanguageCode;
        var displayName = string.IsNullOrWhiteSpace(language) ? name : $"{language} \u00b7 {name}";
        return binding.MediaFileId.HasValue ? $"Source \u00b7 {displayName}" : displayName;
    }

    private async Task<OnlineSubtitlePlaybackApplyResult> ApplyDownloadedOnlineSubtitleAsync(
        OnlineSubtitlePlaybackRequest request)
    {
        if (_disposed || SelectedSource is null)
        {
            return new OnlineSubtitlePlaybackApplyResult(false, "\u64ad\u653e\u5668\u5c1a\u672a\u51c6\u5907\u597d\uff0c\u5b57\u5e55\u5df2\u4fdd\u5b58\u4f46\u672a\u81ea\u52a8\u5207\u6362\u3002");
        }

        try
        {
            PlaybackSubtitleItem subtitle;
            if (request.Binding is not null)
            {
                var displayName = BuildOnlineSubtitleMenuDisplayName(request.Binding);
                subtitle = CreateOnlinePlaybackSubtitle(
                    request.Binding.Id,
                    displayName,
                    request.Binding.FileName,
                    request.AbsolutePath);
                UpsertOnlinePlaybackSubtitle(subtitle);
                await RefreshOnlineSubtitleMenuItemsAsync();
            }
            else
            {
                subtitle = CreateTemporaryOnlinePlaybackSubtitle(request);
                UpsertOnlinePlaybackSubtitle(subtitle);
                UpsertTemporaryOnlineSubtitleMenuItem(request, subtitle);
            }

            var switched = await ApplyOnlineSubtitleSelectionAsync(subtitle, isUserInitiated: true);
            if (!switched)
            {
                return new OnlineSubtitlePlaybackApplyResult(false, "\u5b57\u5e55\u5df2\u52a0\u5165\u83dc\u5355\uff0c\u4f46\u81ea\u52a8\u5207\u6362\u5931\u8d25\uff0c\u53ef\u624b\u52a8\u9009\u62e9\u3002");
            }

            if (request.Binding is not null)
            {
                await _onlineSubtitleBindingService.MarkUsedAsync(
                    request.Binding.Id,
                    request.Binding.MovieId,
                    request.Binding.EpisodeId,
                    request.Binding.MediaFileId);
            }

            return new OnlineSubtitlePlaybackApplyResult(true, "\u5b57\u5e55\u5df2\u4e0b\u8f7d\u5e76\u5207\u6362\u3002");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"online-subtitle-apply-failed errorType={exception.GetType().Name}");
            return new OnlineSubtitlePlaybackApplyResult(false, "\u5b57\u5e55\u5df2\u4fdd\u5b58\uff0c\u4f46\u52a0\u5165\u64ad\u653e\u5668\u5217\u8868\u5931\u8d25\u3002");
        }
    }

    private async Task SelectOnlineSubtitleFromMenuAsync(OnlineSubtitleMenuItemViewModel menuItem)
    {
        if (!menuItem.HasCacheFile)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u8be5\u5728\u7ebf\u5b57\u5e55\u7684\u7f13\u5b58\u6587\u4ef6\u4e0d\u53ef\u7528\uff0c\u8bf7\u91cd\u65b0\u4e0b\u8f7d\u3002", "online-subtitle-cache-missing");
            return;
        }

        var subtitle = _onlinePlaybackSubtitles.FirstOrDefault(
            x => string.Equals(EnsureSubtitleUniqueKey(x), menuItem.SubtitleUniqueKey, StringComparison.OrdinalIgnoreCase));
        if (subtitle is null && !menuItem.IsTemporary && menuItem.BindingId > 0)
        {
            try
            {
                subtitle = CreateOnlinePlaybackSubtitle(
                    menuItem.BindingId,
                    menuItem.DisplayName,
                    menuItem.FileName,
                    _onlineSubtitleCacheService.GetAbsolutePath(menuItem.CacheRelativePath));
                UpsertOnlinePlaybackSubtitle(subtitle);
            }
            catch (Exception exception)
            {
                MpvPlaybackDiagnostics.Write($"online-subtitle-menu-select-failed errorType={exception.GetType().Name}");
                SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u5728\u7ebf\u5b57\u5e55\u7f13\u5b58\u8def\u5f84\u4e0d\u53ef\u7528\uff0c\u8bf7\u91cd\u65b0\u4e0b\u8f7d\u3002", "online-subtitle-cache-path-invalid");
                return;
            }
        }

        if (subtitle is null)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u627e\u4e0d\u5230\u8be5\u5728\u7ebf\u5b57\u5e55\uff0c\u8bf7\u91cd\u65b0\u4e0b\u8f7d\u3002", "online-subtitle-menu-item-missing");
            return;
        }

        var switched = await ApplyOnlineSubtitleSelectionAsync(subtitle, isUserInitiated: true);
        if (!switched)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u5728\u7ebf\u5b57\u5e55\u5207\u6362\u5931\u8d25\u3002", "online-subtitle-switch-failed");
            return;
        }

        if (!menuItem.IsTemporary && menuItem.BindingId > 0)
        {
            await _onlineSubtitleBindingService.MarkUsedAsync(
                menuItem.BindingId,
                menuItem.MovieId,
                menuItem.EpisodeId,
                menuItem.MediaFileId);
        }
    }

    private async Task DeleteOnlineSubtitleFromMenuAsync(OnlineSubtitleMenuItemViewModel menuItem)
    {
        if (menuItem.IsTemporary)
        {
            RemoveTemporaryOnlineSubtitle(menuItem);
            return;
        }

        if (!menuItem.MovieId.HasValue && !menuItem.EpisodeId.HasValue && !menuItem.MediaFileId.HasValue)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u5f53\u524d\u5185\u5bb9\u672a\u8bc6\u522b\uff0c\u6ca1\u6709\u957f\u671f\u5728\u7ebf\u5b57\u5e55\u7ed1\u5b9a\u53ef\u5220\u9664\u3002", "online-subtitle-delete-no-target");
            return;
        }

        var deleted = await _onlineSubtitleBindingService.SoftDeleteAsync(
            menuItem.BindingId,
            menuItem.MovieId,
            menuItem.EpisodeId,
            menuItem.MediaFileId);
        if (!deleted)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u672a\u627e\u5230\u53ef\u5220\u9664\u7684\u5728\u7ebf\u5b57\u5e55\u7ed1\u5b9a\u3002", "online-subtitle-delete-missing");
            return;
        }

        await SwitchToNoneIfOnlineSubtitleSelectedAsync(menuItem.SubtitleUniqueKey);
        _onlinePlaybackSubtitles.RemoveAll(
            x => string.Equals(EnsureSubtitleUniqueKey(x), menuItem.SubtitleUniqueKey, StringComparison.OrdinalIgnoreCase));
        await RefreshOnlineSubtitleMenuItemsAsync();
        SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u5df2\u5220\u9664\u5728\u7ebf\u5b57\u5e55\u7ed1\u5b9a\uff0c\u7f13\u5b58\u6587\u4ef6\u5df2\u4fdd\u7559\u3002", "online-subtitle-binding-deleted");
    }

    private async Task<bool> ApplyOnlineSubtitleSelectionAsync(PlaybackSubtitleItem subtitle, bool isUserInitiated)
    {
        if (SelectedSource is null || _disposed)
        {
            return false;
        }

        if (isUserInitiated)
        {
            _hasUserSelectedSubtitle = true;
        }

        var applyVersion = Interlocked.Increment(ref _subtitleApplyVersion);
        return await SwitchSubtitleOnMpvAsync(
            SelectedSource,
            subtitle,
            isUserInitiated,
            applyVersion);
    }

    private async Task SwitchToNoneIfOnlineSubtitleSelectedAsync(string subtitleUniqueKey)
    {
        if (!string.IsNullOrWhiteSpace(subtitleUniqueKey)
            && string.Equals(_currentSubtitleSelection.SubtitleKey, subtitleUniqueKey, StringComparison.OrdinalIgnoreCase))
        {
            await SwitchSubtitleFromUiAsync(_noneSubtitleItem);
        }
    }

    private void RemoveTemporaryOnlineSubtitle(OnlineSubtitleMenuItemViewModel menuItem)
    {
        _temporaryOnlineSubtitleMenuItems.RemoveAll(
            x => string.Equals(x.SubtitleUniqueKey, menuItem.SubtitleUniqueKey, StringComparison.OrdinalIgnoreCase));
        _onlinePlaybackSubtitles.RemoveAll(
            x => string.Equals(EnsureSubtitleUniqueKey(x), menuItem.SubtitleUniqueKey, StringComparison.OrdinalIgnoreCase));
        _ = SwitchToNoneIfOnlineSubtitleSelectedAsync(menuItem.SubtitleUniqueKey);
        OnlineSubtitleMenuItems.Remove(menuItem);
        SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u5df2\u79fb\u9664\u5f53\u524d\u64ad\u653e\u4f1a\u8bdd\u7684\u4e34\u65f6\u5728\u7ebf\u5b57\u5e55\uff0c\u7f13\u5b58\u6587\u4ef6\u5df2\u4fdd\u7559\u3002", "online-subtitle-temporary-removed");
    }

    private void UpsertTemporaryOnlineSubtitleMenuItem(
        OnlineSubtitlePlaybackRequest request,
        PlaybackSubtitleItem subtitle)
    {
        _temporaryOnlineSubtitleMenuItems.RemoveAll(
            x => string.Equals(x.SubtitleUniqueKey, subtitle.UniqueKey, StringComparison.OrdinalIgnoreCase));
        var item = new OnlineSubtitleMenuItemViewModel
        {
            BindingId = 0,
            TemporaryId = Interlocked.Increment(ref _temporaryOnlineSubtitleId),
            DisplayName = request.DisplayName,
            ToolTip = "\u5f53\u524d\u672a\u8bc6\u522b\u64ad\u653e\u9879\u7684\u4e34\u65f6\u5728\u7ebf\u5b57\u5e55\uff0c\u4e0d\u4f1a\u5199\u5165\u957f\u671f\u7ed1\u5b9a\u3002",
            HasCacheFile = true,
            IsTemporary = true,
            SubtitleUniqueKey = subtitle.UniqueKey,
            FileName = request.FileName
        };
        _temporaryOnlineSubtitleMenuItems.Add(item);
        var existingItem = OnlineSubtitleMenuItems.FirstOrDefault(
            x => string.Equals(x.SubtitleUniqueKey, subtitle.UniqueKey, StringComparison.OrdinalIgnoreCase));
        if (existingItem is not null)
        {
            OnlineSubtitleMenuItems.Remove(existingItem);
        }

        OnlineSubtitleMenuItems.Add(item);
    }

    private void UpsertOnlinePlaybackSubtitle(PlaybackSubtitleItem subtitle)
    {
        var uniqueKey = EnsureSubtitleUniqueKey(subtitle);
        _onlinePlaybackSubtitles.RemoveAll(
            x => string.Equals(EnsureSubtitleUniqueKey(x), uniqueKey, StringComparison.OrdinalIgnoreCase));
        _onlinePlaybackSubtitles.Add(subtitle);
    }

    private static PlaybackSubtitleItem CreateOnlinePlaybackSubtitle(
        int bindingId,
        string displayName,
        string fileName,
        string absolutePath)
    {
        return new PlaybackSubtitleItem
        {
            Type = PlaybackSubtitleType.ExternalFile,
            BindingId = bindingId,
            SubtitleMediaFileId = 0,
            DisplayName = displayName,
            OriginalName = string.IsNullOrWhiteSpace(fileName) ? displayName : fileName,
            FileName = string.IsNullOrWhiteSpace(fileName) ? displayName : fileName,
            PlaybackUrl = absolutePath,
            UniqueKey = $"online:binding:{bindingId}",
            TooltipText = "\u5728\u7ebf\u4e0b\u8f7d\u5b57\u5e55"
        };
    }

    private static PlaybackSubtitleItem CreateTemporaryOnlinePlaybackSubtitle(OnlineSubtitlePlaybackRequest request)
    {
        var keySource = string.IsNullOrWhiteSpace(request.ProviderFileId)
            ? request.AbsolutePath
            : request.ProviderFileId;
        return new PlaybackSubtitleItem
        {
            Type = PlaybackSubtitleType.ExternalFile,
            SubtitleMediaFileId = 0,
            DisplayName = request.DisplayName,
            OriginalName = request.FileName,
            FileName = request.FileName,
            PlaybackUrl = request.AbsolutePath,
            UniqueKey = $"online:temporary:{NormalizeExternalSubtitleKey(keySource)}",
            TooltipText = "\u5f53\u524d\u64ad\u653e\u4f1a\u8bdd\u4e34\u65f6\u5728\u7ebf\u5b57\u5e55"
        };
    }

    private static bool IsLongTermOnlineSubtitle(PlaybackSubtitleItem subtitle)
    {
        return subtitle.BindingId.HasValue && subtitle.BindingId.Value > 0;
    }

    private (int? MovieId, int? EpisodeId, int? MediaFileId) GetCurrentOnlineSubtitleTargets()
    {
        if (_session is null)
        {
            return (null, null, null);
        }

        var mediaFileId = SelectedSource?.MediaFileId > 0
            ? SelectedSource.MediaFileId
            : _session.SelectedMediaFileId > 0
                ? _session.SelectedMediaFileId
                : (int?)null;

        if (_session.ContentType == PlaybackContentType.Episode
            && _session.EpisodeId is > 0
            && IsRecognized(_session.SeasonIdentificationStatus))
        {
            return (null, _session.EpisodeId.Value, mediaFileId);
        }

        if (_session.ContentType == PlaybackContentType.Movie
            && _session.MovieId > 0
            && IsRecognized(_session.MovieIdentificationStatus))
        {
            return (_session.MovieId, null, mediaFileId);
        }

        return (null, null, mediaFileId);
    }

    public bool IsOnlineSubtitleSelected(OnlineSubtitleMenuItemViewModel item)
    {
        return !string.IsNullOrWhiteSpace(item.SubtitleUniqueKey)
               && string.Equals(_currentSubtitleSelection.SubtitleKey, item.SubtitleUniqueKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecognized(IdentificationStatus status)
    {
        return status is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed;
    }

    private async Task ApplySubtitleSelectionAsync(PlaybackSubtitleItem? requestedSubtitle, bool isUserInitiated)
    {
        if (SelectedSource is null || _disposed)
        {
            return;
        }

        var source = SelectedSource;
        var selectedSubtitle = FindMatchingSubtitle(requestedSubtitle ?? _noneSubtitleItem) ?? _noneSubtitleItem;

        if (!isUserInitiated && _hasUserSelectedSubtitle && !IsSameSubtitle(selectedSubtitle, SelectedSubtitle ?? _noneSubtitleItem))
        {
            return;
        }

        if (isUserInitiated)
        {
            _hasUserSelectedSubtitle = true;
            SuppressAutomaticTrackSelection();
            await _playbackSourceService.SetPreferredSubtitleAsync(
                source.MediaFileId,
                selectedSubtitle.Type == PlaybackSubtitleType.ExternalFile ? selectedSubtitle.SubtitleMediaFileId : null);
        }

        var useMpvSubtitlePath = _isMpvMediaLoaded && _playbackEngine is not null;
        if (!useMpvSubtitlePath && !IsSameSubtitle(SelectedSubtitle ?? _noneSubtitleItem, selectedSubtitle))
        {
            SetSelectedSubtitleSilently(selectedSubtitle);
        }

        if (!useMpvSubtitlePath)
        {
            return;
        }

        var applyVersion = Interlocked.Increment(ref _subtitleApplyVersion);
        await _subtitleSwitchLock.WaitAsync();
        _isApplyingSubtitle = true;
        var statusBeforeSubtitleSwitch = StatusMessage;
        var positionBeforeSubtitleSwitch = PositionSeconds;

        try
        {
            if (SelectedSource is null
                || SelectedSource.MediaFileId != source.MediaFileId
                || _disposed
                || (!useMpvSubtitlePath && !IsSameSubtitle(SelectedSubtitle ?? _noneSubtitleItem, selectedSubtitle)))
            {
                return;
            }

            if (useMpvSubtitlePath)
            {
                var switched = await SwitchSubtitleOnMpvAsync(source, selectedSubtitle, isUserInitiated, applyVersion);
                if (!switched && applyVersion == _subtitleApplyVersion)
                {
                    SetSelectedSubtitleSilently(_appliedSubtitle ?? _noneSubtitleItem);
                    OnPropertyChanged(nameof(SubtitleButtonText));
                }

                return;
            }

            return;
        }
        finally
        {
            if (applyVersion == _subtitleApplyVersion)
            {
                _isApplyingSubtitle = false;
            }

            if (_isMpvMediaLoaded)
            {
                if (isUserInitiated && StatusMessage == SubtitleSwitchingStatusMessage && !HasPendingSubtitleSwitch())
                {
                    _ = RestoreStatusMessageAfterSubtitleSwitchAsync(
                        source.MediaFileId,
                        positionBeforeSubtitleSwitch,
                        statusBeforeSubtitleSwitch);
                }
            }

            _subtitleSwitchLock.Release();
        }
    }

    private async Task ApplyAutomaticDefaultSubtitleAsync()
    {
        if (_hasAppliedAutomaticSubtitleForCurrentMedia
            || _hasUserSelectedSubtitle
            || SelectedSource is null
            || _disposed
            || DateTime.UtcNow < _suppressAutomaticTrackSelectionUntilUtc)
        {
            return;
        }

        _hasAppliedAutomaticSubtitleForCurrentMedia = true;
        var defaultSubtitle = ResolveAutomaticDefaultSubtitle();
        await ApplySubtitleSelectionAsync(defaultSubtitle, isUserInitiated: false);
    }

    private PlaybackSubtitleItem ResolveAutomaticDefaultSubtitle()
    {
        var preferredEmbedded = EmbeddedSubtitles.FirstOrDefault(IsPreferredChineseSubtitle);
        if (preferredEmbedded is not null)
        {
            return preferredEmbedded;
        }

        if (EmbeddedSubtitles.Count > 0)
        {
            return _noneSubtitleItem;
        }

        return ExternalSubtitles.FirstOrDefault(x => x.MatchType == SubtitleMatchType.SameName && IsAutoSubtitle(x))
               ?? _noneSubtitleItem;
    }

    private async Task ApplyAudioTrackSelectionAsync(PlaybackAudioTrackItem? requestedTrack, bool isUserInitiated)
    {
        if (requestedTrack is null || SelectedSource is null || _disposed)
        {
            return;
        }

        var sourceId = SelectedSource.MediaFileId;
        var selectedTrack = FindMatchingAudioTrack(requestedTrack);
        if (selectedTrack is null)
        {
            return;
        }

        if (isUserInitiated)
        {
            _hasUserSelectedAudioTrack = true;
            SuppressAutomaticTrackSelection();
        }

        if (!IsSameAudioTrack(SelectedAudioTrack, selectedTrack))
        {
            SetSelectedAudioTrackSilently(selectedTrack);
        }

        if (!_isMpvMediaLoaded)
        {
            return;
        }

        if (_isMpvMediaLoaded && _playbackEngine is not null)
        {
            await _audioTrackSwitchLock.WaitAsync();
            _isApplyingAudioTrack = true;
            OnPropertyChanged(nameof(AudioTrackMenuStatusText));
            SuppressAutomaticTrackSelection();
            var statusBeforeAudioSwitch = StatusMessage;
            if (isUserInitiated)
            {
                BeginAudioTrackSwitchUiState(sourceId, selectedTrack);
            }

            try
            {
                if (await _playbackEngine.SetAudioTrackAsync(selectedTrack.TrackId))
                {
                    _appliedAudioTrack = selectedTrack;
                    SetSelectedAudioTrackSilently(selectedTrack);
                    if (isUserInitiated && !HasPendingAudioTrackSwitch())
                    {
                        _ = RestoreStatusMessageAfterTrackSwitchAsync(
                            sourceId,
                            AudioSwitchingStatusMessage,
                            "\u97f3\u8f68\u5df2\u5207\u6362\u3002",
                            statusBeforeAudioSwitch);
                    }
                }
                else if (isUserInitiated)
                {
                    StatusMessage = "\u97f3\u8f68\u5207\u6362\u5931\u8d25\u3002";
                    SetOperationNotice(PlayerOperationNoticeKind.Audio, "\u97f3\u8f68\u5207\u6362\u5931\u8d25\u3002", "audio-switch-failed");
                    FailPendingAudioTrackSwitch("set-aid-failed");
                    SyncSelectedAudioTrackToCurrent();
                }
            }
            finally
            {
                _isApplyingAudioTrack = false;
                OnPropertyChanged(nameof(AudioTrackMenuStatusText));
                _audioTrackSwitchLock.Release();
            }

            return;
        }

    }

    private async Task ApplyAutomaticDefaultAudioTrackAsync()
    {
        if (_hasAppliedAutomaticAudioTrackForCurrentMedia
            || _hasUserSelectedAudioTrack
            || SelectedSource is null
            || _disposed
            || AudioTracks.Count == 0
            || DateTime.UtcNow < _suppressAutomaticTrackSelectionUntilUtc)
        {
            return;
        }

        var defaultTrack = ResolveAutomaticDefaultAudioTrack();
        if (defaultTrack is not null)
        {
            _hasAppliedAutomaticAudioTrackForCurrentMedia = true;
            await ApplyAudioTrackSelectionAsync(defaultTrack, isUserInitiated: false);
        }
    }

    private async Task RestoreStatusMessageAfterAudioSwitchAsync(int sourceId, string statusMessage)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        if (!_disposed
            && SelectedSource?.MediaFileId == sourceId
            && StatusMessage == AudioSwitchingStatusMessage)
        {
            StatusMessage = statusMessage;
            SetOperationNotice(PlayerOperationNoticeKind.Audio, statusMessage, "audio-switch-restore");
            return;
        }

        if (_disposed
            || SelectedSource?.MediaFileId != sourceId
            || StatusMessage != AudioSwitchingStatusMessage)
        {
            return;
        }

        StatusMessage = statusMessage;
        SetOperationNotice(PlayerOperationNoticeKind.Audio, statusMessage, "audio-switch-restore");
    }

    private async Task RestoreStatusMessageAfterTrackSwitchAsync(
        int sourceId,
        string switchingMessage,
        string successMessage,
        string fallbackMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!_disposed
               && SelectedSource?.MediaFileId == sourceId
               && DateTime.UtcNow < deadline)
        {
            if ((_playbackEngine is null || !_playbackEngine.IsBuffering) && !IsBuffering)
            {
                break;
            }

            await Task.Delay(250);
        }

        if (_disposed
            || SelectedSource?.MediaFileId != sourceId
            || StatusMessage != switchingMessage)
        {
            return;
        }

        var finalMessage = (_playbackEngine is not null && _playbackEngine.IsBuffering) || IsBuffering
            ? fallbackMessage
            : successMessage;
        StatusMessage = finalMessage;
        SetOperationNotice(
            switchingMessage == AudioSwitchingStatusMessage ? PlayerOperationNoticeKind.Audio : PlayerOperationNoticeKind.Subtitle,
            finalMessage,
            "track-switch-restore");
    }

    private async Task RestoreStatusMessageAfterSubtitleSwitchAsync(
        int sourceId,
        double positionBeforeSwitch,
        string fallbackMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        var shouldWaitForPlaybackProgress = _isPlaybackRunning
                                            && !_isStopped;
        while (!_disposed
               && SelectedSource?.MediaFileId == sourceId
               && DateTime.UtcNow < deadline)
        {
            var isStillBuffering = (_playbackEngine is not null && _playbackEngine.IsBuffering) || IsBuffering;
            var hasProgressed = PositionSeconds >= positionBeforeSwitch + 0.3d;
            if (!isStillBuffering && (!shouldWaitForPlaybackProgress || hasProgressed))
            {
                break;
            }

            await Task.Delay(250);
        }

        if (_disposed
            || SelectedSource?.MediaFileId != sourceId
            || StatusMessage != SubtitleSwitchingStatusMessage)
        {
            return;
        }

        var stillWaitingForProgress = shouldWaitForPlaybackProgress && PositionSeconds < positionBeforeSwitch + 0.3d;
        if (stillWaitingForProgress)
        {
            TracePlayback(
                $"mpv-subtitle-switch-waiting-for-playback mediaFileId={sourceId} positionBefore={Math.Floor(positionBeforeSwitch)} positionNow={Math.Floor(PositionSeconds)}");
            StatusMessage = "\u5b57\u5e55\u5207\u6362\u5df2\u63d0\u4ea4\uff0c\u7b49\u5f85\u64ad\u653e\u6062\u590d\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, "\u5b57\u5e55\u5207\u6362\u5df2\u63d0\u4ea4\uff0c\u7b49\u5f85\u64ad\u653e\u6062\u590d\u3002", "subtitle-switch-waiting-progress");
            return;
        }

        var finalMessage = (_playbackEngine is not null && _playbackEngine.IsBuffering) || IsBuffering
            ? fallbackMessage
            : "\u5b57\u5e55\u5df2\u5207\u6362\u3002";
        StatusMessage = finalMessage;
        SetOperationNotice(PlayerOperationNoticeKind.Subtitle, finalMessage, "subtitle-switch-restore");
    }

    private void BeginSubtitleSwitchUiState(int sourceId, PlaybackSubtitleItem selectedSubtitle)
    {
        var targetSid = selectedSubtitle.Type == PlaybackSubtitleType.EmbeddedTrack
            ? selectedSubtitle.TrackId
            : selectedSubtitle.Type == PlaybackSubtitleType.None
                ? null
                : selectedSubtitle.TrackId;
        var version = Interlocked.Increment(ref _subtitleSwitchUiVersion);
        _pendingSubtitleSwitchSourceId = sourceId;
        _pendingSubtitleSwitchTargetSid = targetSid;
        _pendingSubtitleSwitchConfirmed = false;
        _pendingSubtitleSelection = BuildSubtitleSelection(selectedSubtitle, true, version);
        StatusMessage = SubtitleSwitchingStatusMessage;
        SetOperationNotice(PlayerOperationNoticeKind.Subtitle, SubtitleSwitchingStatusMessage, "subtitle-switch-start", TimeSpan.FromSeconds(4));
        MpvPlaybackDiagnostics.Write($"mpv-subtitle-switch-ui-start targetSid={FormatOptionalTrackId(targetSid)}");
        _ = WatchSubtitleSwitchTimeoutAsync(sourceId, targetSid, version);
    }

    private async Task WatchSubtitleSwitchTimeoutAsync(int sourceId, int? targetSid, int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => HandleSubtitleSwitchTimeout(sourceId, targetSid, version));
            return;
        }

        HandleSubtitleSwitchTimeout(sourceId, targetSid, version);
    }

    private void HandleSubtitleSwitchTimeout(int sourceId, int? targetSid, int version)
    {
        if (!IsPendingSubtitleSwitch(sourceId, targetSid, version))
        {
            return;
        }

        var pendingSelection = _pendingSubtitleSelection;
        var state = pendingSelection is { Kind: SubtitleSelectionKind.External, ExternalMpvTrackId: null }
            ? "waiting-external-sid"
            : IsBuffering || (_playbackEngine?.IsBuffering == true)
                ? "buffering"
                : "waiting-confirmation";
        var requestId = _pendingSubtitleSelection?.RequestId ?? version;
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-timeout requestId={requestId} kind={FormatSubtitleSelectionKind(pendingSelection?.Kind ?? SubtitleSelectionKind.None)} state={state}");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-slow-stage requestId={requestId} slowStage={state} elapsedMs={GetSubtitleSwitchElapsedMs()}");
        if (StatusMessage == SubtitleSwitchingStatusMessage)
        {
            StatusMessage = state == "buffering"
                ? "\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e..."
                : "\u5b57\u5e55\u5207\u6362\u8f83\u6162\uff0c\u6b63\u5728\u7b49\u5f85\u6570\u636e\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, StatusMessage, "subtitle-switch-timeout", TimeSpan.FromSeconds(4));
        }
    }

    private bool IsPendingSubtitleSwitch(int sourceId, int? targetSid, int version)
    {
        if (_disposed
            || _pendingSubtitleSelection is not { } selection
            || selection.RequestId != version
            || _pendingSubtitleSwitchSourceId != sourceId
            || SelectedSource?.MediaFileId != sourceId)
        {
            return false;
        }

        if (selection.Kind == SubtitleSelectionKind.External && !selection.ExternalMpvTrackId.HasValue)
        {
            return true;
        }

        return _pendingSubtitleSwitchTargetSid == targetSid;
    }

    private bool HasPendingSubtitleSwitch()
    {
        return !_disposed
               && _pendingSubtitleSwitchSourceId.HasValue
               && SelectedSource?.MediaFileId == _pendingSubtitleSwitchSourceId.Value;
    }

    private void CompletePendingSubtitleSwitch(int? observedSid)
    {
        if (!HasPendingSubtitleSwitch() || _pendingSubtitleSelection is not { } selection)
        {
            return;
        }

        var matches = selection.Kind switch
        {
            SubtitleSelectionKind.None => !observedSid.HasValue,
            SubtitleSelectionKind.Embedded => selection.EmbeddedTrackId == observedSid,
            SubtitleSelectionKind.External => observedSid.HasValue
                                              && (!selection.ExternalMpvTrackId.HasValue
                                                  || selection.ExternalMpvTrackId == observedSid),
            _ => false
        };
        if (!matches)
        {
            return;
        }

        if (selection.Kind == SubtitleSelectionKind.External && observedSid.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(selection.SubtitleKey))
            {
                _externalSubtitleMpvTrackIds[selection.SubtitleKey] = observedSid.Value;
                _pendingSubtitleSelection = selection with { ExternalMpvTrackId = observedSid };
                selection = _pendingSubtitleSelection;
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-external-map-created externalSubtitleId={selection.ExternalSubtitleId.GetValueOrDefault()} mpvTrackId={observedSid.Value}");
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r3-external-subtitle-mapped appId={selection.ExternalSubtitleId.GetValueOrDefault()} trackId={observedSid.Value}");
            }
        }

        _pendingSubtitleSwitchConfirmed = true;
        _currentSubtitleSelection = selection;
        var confirmedSubtitle = ResolveSubtitleFromSelection(selection);
        _appliedSubtitle = confirmedSubtitle;
        SetSelectedSubtitleSilently(confirmedSubtitle);
        OnPropertyChanged(nameof(SubtitleButtonText));
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-stage requestId={selection.RequestId} stage=sid-property-changed elapsedMs={GetSubtitleSwitchElapsedMs()} sid={FormatOptionalTrackId(observedSid)}");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-confirmed requestId={selection.RequestId} kind={FormatSubtitleSelectionKind(selection.Kind)} sid={FormatOptionalTrackId(observedSid)}");
        if (!IsBuffering && _playbackEngine?.IsBuffering != true)
        {
            ClearPendingSubtitleSwitch(success: true);
        }
        else if (StatusMessage == SubtitleSwitchingStatusMessage)
        {
            StatusMessage = "\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e...";
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, StatusMessage, "subtitle-switch-waiting-data", TimeSpan.FromSeconds(4));
        }
    }

    private void ClearPendingSubtitleSwitch(bool success)
    {
        if (!_pendingSubtitleSwitchSourceId.HasValue)
        {
            return;
        }

        if (success && (StatusMessage == SubtitleSwitchingStatusMessage
                        || StatusMessage == "\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e..."
                        || StatusMessage == "\u5b57\u5e55\u5207\u6362\u5df2\u63d0\u4ea4\uff0c\u7b49\u5f85\u64ad\u653e\u6062\u590d\u3002"
                        || StatusMessage == "\u5b57\u5e55\u5207\u6362\u8f83\u6162\uff0c\u6b63\u5728\u7b49\u5f85\u6570\u636e\u3002"))
        {
            StatusMessage = _currentSubtitleSelection.Kind == SubtitleSelectionKind.None
                ? "\u5b57\u5e55\u5df2\u5173\u95ed\u3002"
                : "\u5b57\u5e55\u5df2\u5207\u6362\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, StatusMessage, "subtitle-switch-success");
        }

        _pendingSubtitleSwitchSourceId = null;
        _pendingSubtitleSwitchTargetSid = null;
        _pendingSubtitleSwitchConfirmed = false;
        _pendingSubtitleSelection = null;
        Interlocked.Increment(ref _subtitleSwitchUiVersion);
    }

    private void FailPendingSubtitleSwitch(string errorType)
    {
        var requestId = _pendingSubtitleSelection?.RequestId ?? _subtitleSwitchUiVersion;
        MpvPlaybackDiagnostics.Write($"subtitle-switch-failed requestId={requestId} errorType={errorType}");
        if (StatusMessage == SubtitleSwitchingStatusMessage
            || StatusMessage == "\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e..."
            || StatusMessage == "\u5b57\u5e55\u5207\u6362\u8f83\u6162\uff0c\u6b63\u5728\u7b49\u5f85\u6570\u636e\u3002")
        {
            StatusMessage = "\u5b57\u5e55\u5207\u6362\u5931\u8d25\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, StatusMessage, "subtitle-switch-failed");
        }

        ClearPendingSubtitleSwitch(success: false);
    }

    private static string FormatOptionalTrackId(int? trackId)
    {
        return trackId.HasValue
            ? trackId.Value.ToString(CultureInfo.InvariantCulture)
            : "no";
    }

    private static string FormatTrackIds(IEnumerable<int> trackIds)
    {
        var values = trackIds
            .OrderBy(trackId => trackId)
            .Select(trackId => trackId.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        return values.Length == 0 ? "none" : string.Join(",", values);
    }

    private void BeginAudioTrackSwitchUiState(int sourceId, PlaybackAudioTrackItem selectedTrack)
    {
        var targetAid = selectedTrack.TrackId;
        var version = Interlocked.Increment(ref _audioTrackSwitchUiVersion);
        _pendingAudioTrackSwitchSourceId = sourceId;
        _pendingAudioTrackSwitchTargetAid = targetAid;
        _pendingAudioTrackSwitchConfirmed = false;
        StatusMessage = AudioSwitchingStatusMessage;
        OnPropertyChanged(nameof(AudioTrackMenuStatusText));
        SetOperationNotice(PlayerOperationNoticeKind.Audio, AudioSwitchingStatusMessage, "audio-switch-start", TimeSpan.FromSeconds(4));
        MpvPlaybackDiagnostics.Write($"mpv-audio-switch-ui-start targetAid={FormatOptionalTrackId(targetAid)}");
        _ = WatchAudioTrackSwitchTimeoutAsync(sourceId, targetAid, version);
    }

    private async Task WatchAudioTrackSwitchTimeoutAsync(int sourceId, int? targetAid, int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => HandleAudioTrackSwitchTimeout(sourceId, targetAid, version));
            return;
        }

        HandleAudioTrackSwitchTimeout(sourceId, targetAid, version);
    }

    private void HandleAudioTrackSwitchTimeout(int sourceId, int? targetAid, int version)
    {
        if (!IsPendingAudioTrackSwitch(sourceId, targetAid, version))
        {
            return;
        }

        var state = IsBuffering || (_playbackEngine?.IsBuffering == true)
            ? "buffering"
            : "waiting-confirmation";
        MpvPlaybackDiagnostics.Write(
            $"mpv-audio-switch-timeout state={state} targetAid={FormatOptionalTrackId(targetAid)}");
        if (StatusMessage == AudioSwitchingStatusMessage)
        {
            StatusMessage = state == "buffering"
                ? "\u6b63\u5728\u7b49\u5f85\u97f3\u9891/\u89c6\u9891\u6570\u636e..."
                : "\u97f3\u8f68\u5207\u6362\u5df2\u63d0\u4ea4\uff0c\u7b49\u5f85\u64ad\u653e\u6062\u590d\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Audio, StatusMessage, "audio-switch-timeout", TimeSpan.FromSeconds(4));
        }
    }

    private bool IsPendingAudioTrackSwitch(int sourceId, int? targetAid, int version)
    {
        return !_disposed
               && version == _audioTrackSwitchUiVersion
               && _pendingAudioTrackSwitchSourceId == sourceId
               && _pendingAudioTrackSwitchTargetAid == targetAid
               && SelectedSource?.MediaFileId == sourceId;
    }

    private bool HasPendingAudioTrackSwitch()
    {
        return !_disposed
               && _pendingAudioTrackSwitchSourceId.HasValue
               && SelectedSource?.MediaFileId == _pendingAudioTrackSwitchSourceId.Value;
    }

    private void CompletePendingAudioTrackSwitch(int? observedAid)
    {
        if (!HasPendingAudioTrackSwitch() || _pendingAudioTrackSwitchTargetAid != observedAid)
        {
            return;
        }

        _pendingAudioTrackSwitchConfirmed = true;
        MpvPlaybackDiagnostics.Write($"mpv-audio-switch-confirmed aid={FormatOptionalTrackId(observedAid)}");
        if (!IsBuffering && _playbackEngine?.IsBuffering != true)
        {
            ClearPendingAudioTrackSwitch(success: true);
        }
        else if (StatusMessage == AudioSwitchingStatusMessage)
        {
            StatusMessage = "\u6b63\u5728\u7b49\u5f85\u97f3\u9891/\u89c6\u9891\u6570\u636e...";
            SetOperationNotice(PlayerOperationNoticeKind.Audio, StatusMessage, "audio-switch-waiting-data", TimeSpan.FromSeconds(4));
        }
    }

    private void ClearPendingAudioTrackSwitch(bool success)
    {
        if (!_pendingAudioTrackSwitchSourceId.HasValue)
        {
            return;
        }

        if (success && (StatusMessage == AudioSwitchingStatusMessage
                        || StatusMessage == "\u6b63\u5728\u7b49\u5f85\u97f3\u9891/\u89c6\u9891\u6570\u636e..."
                        || StatusMessage == "\u97f3\u8f68\u5207\u6362\u5df2\u63d0\u4ea4\uff0c\u7b49\u5f85\u64ad\u653e\u6062\u590d\u3002"))
        {
            StatusMessage = "\u97f3\u8f68\u5df2\u5207\u6362\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Audio, StatusMessage, "audio-switch-success");
        }

        _pendingAudioTrackSwitchSourceId = null;
        _pendingAudioTrackSwitchTargetAid = null;
        _pendingAudioTrackSwitchConfirmed = false;
        Interlocked.Increment(ref _audioTrackSwitchUiVersion);
        OnPropertyChanged(nameof(AudioTrackMenuStatusText));
    }

    private void FailPendingAudioTrackSwitch(string errorType)
    {
        MpvPlaybackDiagnostics.Write($"mpv-audio-switch-failed errorType={errorType}");
        if (StatusMessage == AudioSwitchingStatusMessage)
        {
            StatusMessage = "\u97f3\u8f68\u5207\u6362\u5931\u8d25\u3002";
            SetOperationNotice(PlayerOperationNoticeKind.Audio, StatusMessage, "audio-switch-failed");
        }

        ClearPendingAudioTrackSwitch(success: false);
    }

    private PlaybackAudioTrackItem? ResolveAutomaticDefaultAudioTrack()
    {
        var currentTrackId = SafeGetCurrentAudioTrackId();
        return AudioTracks.FirstOrDefault(x => x.TrackId == currentTrackId)
               ?? AudioTracks.FirstOrDefault();
    }

    private async Task<bool> ReloadCurrentMediaAsync(
        PlaybackSourceItem source,
        PlaybackSubtitleItem selectedSubtitle,
        bool keepPosition,
        bool preservePlaybackState,
        bool updateResumeMessage,
        bool attachExternalSubtitleToMedia)
    {
        var shouldResumePlaying = ShouldResumePlaybackAfterReload(preservePlaybackState);
        await PersistProgressSnapshotAsync((int)Math.Max(0, PositionSeconds), false);
        var resumePosition = keepPosition ? (int)Math.Max(0, PositionSeconds) : ResolveSourceResumePosition(source);
        MpvPlaybackDiagnostics.Write(
            $"watch-history-unified-resume-resolved mediaFileId={source.MediaFileId} resume={resumePosition} keepPosition={keepPosition.ToString().ToLowerInvariant()}");

        _timer.Stop();
        _isReloadingMedia = true;
        Interlocked.Increment(ref _subtitleRefreshVersion);
        Interlocked.Increment(ref _audioTrackRefreshVersion);
        await _playbackReloadLock.WaitAsync();
        try
        {
            Interlocked.Increment(ref _playbackDiagnosticsVersion);
            TracePlayback(
                $"playback-reload-start mediaFileId={source.MediaFileId} sourceConnectionId={source.SourceConnectionId} engine=mpv");
            _currentPlaybackMode = PlaybackSourceMode.None;
            _currentPlaybackSource = source;

            await StopPlaybackEngineSafelyAsync();
            ReleaseCurrentVideoCacheLease("source-switch");
            if (_disposed)
            {
                return false;
            }

            ClearSeekRecovery(resetBuffering: true);
            ResetBufferingState();

            if (!await EnsurePlaybackEngineAsync())
            {
                ResetPlaybackInitializationFailureState(
                    source,
                    "engine-unavailable",
                    string.IsNullOrWhiteSpace(StatusMessage)
                        ? "\u64ad\u653e\u521d\u59cb\u5316\u5931\u8d25\uff0c\u53ef\u91cd\u8bd5\u3002"
                        : StatusMessage);
                return false;
            }

            var playbackUrl = source.PlaybackUrl;
            var isLocalFile = source.ProtocolType == ProtocolType.Local;
            if (source.ProtocolType == ProtocolType.Local)
            {
                if (!TryResolveLocalPlaybackPath(source, out playbackUrl))
                {
                    ResetPlaybackInitializationFailureState(
                        source,
                        "local-file-unavailable",
                        "本地文件不存在或不可访问。请检查本地目录后重试。");
                    return false;
                }

                _currentPlaybackMode = PlaybackSourceMode.LocalFile;
                TracePlayback(
                    $"playback-source-mode mediaFileId={source.MediaFileId} mode=local-file fileSize={source.FileSize}");
            }
            else
            {
                var cacheAcquireResult = await TryAcquireVideoCachePlaybackAsync(source);
                _currentVideoCacheLease = cacheAcquireResult.Lease;
                if (cacheAcquireResult.IsHit && !string.IsNullOrWhiteSpace(cacheAcquireResult.LocalFilePath))
                {
                    playbackUrl = cacheAcquireResult.LocalFilePath;
                    isLocalFile = true;
                    _currentPlaybackMode = PlaybackSourceMode.CompleteFile;
                    TracePlayback(
                        $"playback-source-mode mediaFileId={source.MediaFileId} mode=complete-file fileSize={source.FileSize}");
                    SetOperationNotice(PlayerOperationNoticeKind.Cache, "\u4f7f\u7528\u672c\u5730\u7f13\u5b58\u64ad\u653e", "complete-cache-hit");
                }
                else
                {
                    _currentPlaybackMode = PlaybackSourceMode.WebDavDirect;
                    TracePlayback(
                        $"playback-source-mode mediaFileId={source.MediaFileId} mode=webdav-direct fileSize={source.FileSize}");
                }
            }

            var uriKind = isLocalFile ? "local-file" : "webdav-direct";
            TracePlayback(
                $"playback-media-create-start mediaFileId={source.MediaFileId} mode={FormatPlaybackMode(_currentPlaybackMode)} uriKind={uriKind} uriLength={playbackUrl.Length}");
            TracePlayback(
                $"playback-play-start mediaFileId={source.MediaFileId} mode={FormatPlaybackMode(_currentPlaybackMode)} engine=mpv");
            ResetEnginePositionUiThrottle();
            _isAwaitingInitialPlaybackProgress = shouldResumePlaying;
            var loadRequest = new PlaybackLoadRequest
            {
                MediaFileId = source.MediaFileId,
                SourceConnectionId = source.SourceConnectionId,
                PlaybackUrl = playbackUrl,
                ProtocolType = source.ProtocolType,
                Username = isLocalFile ? string.Empty : source.Username,
                Password = isLocalFile ? string.Empty : source.Password,
                IsLocalFile = isLocalFile,
                StartPositionSeconds = Math.Max(0, resumePosition),
                FileSize = source.FileSize,
                LastModifiedAt = source.LastModifiedAt,
                VideoCodec = source.VideoCodec,
                ResolutionWidth = source.ResolutionWidth,
                ResolutionHeight = source.ResolutionHeight
            };
            var playbackEngine = _playbackEngine!;
            await Task.Run(() => playbackEngine.LoadAsync(loadRequest));
            TracePlayback(
                $"playback-media-create-success mediaFileId={source.MediaFileId} mode={FormatPlaybackMode(_currentPlaybackMode)} uriKind={uriKind}");
            TracePlayback(
                $"playback-play-return mediaFileId={source.MediaFileId} mode={FormatPlaybackMode(_currentPlaybackMode)} result=true");

            _isMpvMediaLoaded = true;
            if (PlaybackEngineDefersTrackFeatures())
            {
                MpvPlaybackDiagnostics.Write($"mpv-r1-track-init-skipped mediaFileId={source.MediaFileId} reason=minimal-core");
            }
            else
            {
                RecordSubtitleTrackDiscoveryUpdate(source.MediaFileId, playbackEngine.SubtitleTracks);
            }
            _isStopped = false;
            ApplyVolumeToPlayer();
            ApplyBrightnessToPlayer();

            if (resumePosition > 5 && updateResumeMessage)
            {
                SetResumeMessage($"\u5df2\u4ece {FormatTime(resumePosition)} \u7ee7\u7eed\u64ad\u653e");
            }
            else if (updateResumeMessage)
            {
                ClearResumeMessage();
            }

            if (!PlaybackEngineDefersTrackFeatures())
            {
                await ApplySelectedSubtitleToMpvAsync(source, selectedSubtitle);
            }

            if (!shouldResumePlaying)
            {
                playbackEngine.Pause();
            }
            else
            {
                playbackEngine.Play();
            }
        }
        catch (Exception exception)
        {
            TracePlayback(
                $"playback-init-failed mediaFileId={source.MediaFileId} mode=mpv errorType={exception.GetType().Name}");
            TracePlayback(
                $"playback-reload-failed mediaFileId={source.MediaFileId} reason=mpv-init-failed errorType={exception.GetType().Name}");
            await StopPlaybackEngineSafelyAsync();
            ResetPlaybackInitializationFailureState(
                source,
                "mpv-init-failed",
                exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.Message
                    : "\u64ad\u653e\u521d\u59cb\u5316\u5931\u8d25\uff0c\u53ef\u91cd\u8bd5\u3002");
            return false;
        }
        finally
        {
            _isReloadingMedia = false;
            _playbackReloadLock.Release();
        }

        if (PlaybackLoadReturnsOnCommandSubmitted() && shouldResumePlaying)
        {
            SetPlaybackState(false);
            SetMainPlaybackUiState(MainPlaybackUiState.Starting, "reload-load-submitted");
        }
        else
        {
            SetPlaybackState(shouldResumePlaying);
        }
        TracePlayback(
            $"playback-reload-complete mediaFileId={source.MediaFileId} success=true mode={FormatPlaybackMode(_currentPlaybackMode)} engine=mpv");
        return true;
    }

    private void ResetPlaybackInitializationFailureState(
        PlaybackSourceItem source,
        string reason,
        string statusMessage)
    {
        ReleaseCurrentVideoCacheLease("stop");
        ClearSeekRecovery(resetBuffering: true);
        ClearPendingWatchHistoryStart();
        ResetBufferingState();
        SetStoppedState();
        SetPlaybackState(false);
        _isMpvMediaLoaded = false;
        _isAwaitingInitialPlaybackProgress = false;
        _currentPlaybackMode = PlaybackSourceMode.None;
        SetMainPlaybackUiState(MainPlaybackUiState.Error, reason, statusMessage);
        TracePlayback(
            $"playback-state-reset-after-failure mediaFileId={source.MediaFileId} reason={reason}");
        TracePlayback(
            $"playback-reload-complete mediaFileId={source.MediaFileId} success=false engine=mpv reason={reason}");
    }

    private async Task<bool> EnsurePlaybackEngineAsync()
    {
        if (_playbackEngine is not null)
        {
            return true;
        }

        if (_playbackHostHandle == IntPtr.Zero)
        {
            SetMainPlaybackUiState(MainPlaybackUiState.Error, "missing-playback-host", "\u64ad\u653e\u5668\u7a97\u53e3\u5c1a\u672a\u5c31\u7eea\uff0c\u8bf7\u91cd\u8bd5\u3002");
            TracePlayback("playback-reload-failed reason=missing-playback-host");
            return false;
        }

        IPlaybackEngine? engine = null;
        try
        {
            engine = _playbackEngineFactory.Create();
            SubscribePlaybackEngine(engine);
            await Task.Run(() => engine.InitializeAsync(_playbackHostHandle));
            _playbackEngine = engine;
            ResetAppliedPlayerPreferenceCache();
            TracePlayback("playback-engine-selected engine=mpv");
            return true;
        }
        catch (Exception exception)
        {
            if (engine is not null)
            {
                try
                {
                    UnsubscribePlaybackEngine(engine);
                    engine.Dispose();
                }
                catch
                {
                }
            }

            TracePlayback($"playback-engine-init-failed engine=mpv errorType={exception.GetType().Name}");
            var errorMessage = exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)
                ? exception.Message
                : "\u64ad\u653e\u521d\u59cb\u5316\u5931\u8d25\uff0c\u53ef\u91cd\u8bd5\u3002";
            SetMainPlaybackUiState(MainPlaybackUiState.Error, "engine-init-failed", errorMessage);
            return false;
        }
    }

    private async Task ApplySelectedSubtitleToMpvAsync(PlaybackSourceItem source, PlaybackSubtitleItem selectedSubtitle)
    {
        _ = await SwitchSubtitleOnMpvAsync(
            source,
            selectedSubtitle,
            isUserInitiated: false,
            applyVersion: Interlocked.Increment(ref _subtitleApplyVersion));
    }

    private async Task<bool> SwitchSubtitleOnMpvAsync(
        PlaybackSourceItem source,
        PlaybackSubtitleItem selectedSubtitle,
        bool isUserInitiated,
        int applyVersion)
    {
        var engine = _playbackEngine;
        if (engine is null)
        {
            return false;
        }

        var requestId = Interlocked.Increment(ref _subtitleSwitchUiVersion);
        var selection = BuildSubtitleSelection(selectedSubtitle, isUserInitiated, requestId);
        _pendingSubtitleSelection = selection;
        _pendingSubtitleSwitchSourceId = source.MediaFileId;
        _pendingSubtitleSwitchTargetSid = selection.Kind switch
        {
            SubtitleSelectionKind.Embedded => selection.EmbeddedTrackId,
            SubtitleSelectionKind.External => selection.ExternalMpvTrackId,
            _ => null
        };
        _pendingSubtitleSwitchConfirmed = false;
        _pendingSubtitleSwitchStartedUtc = DateTime.UtcNow;
        _pendingSubtitleSwitchStartPosition = PositionSeconds;
        _pendingSubtitleSwitchTimePositionLogged = false;
        StatusMessage = SubtitleSwitchingStatusMessage;
        if (isUserInitiated)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Subtitle, SubtitleSwitchingStatusMessage, "subtitle-switch-start", TimeSpan.FromSeconds(4));
        }
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-stage requestId={requestId} stage=click elapsedMs=0");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-request requestId={requestId} kind={FormatSubtitleSelectionKind(selection.Kind)} target={FormatSubtitleSelectionTarget(selection)}");
        MpvPlaybackDiagnostics.Write($"mpv-subtitle-switch-ui-start targetSid={FormatOptionalTrackId(_pendingSubtitleSwitchTargetSid)}");
        _ = WatchSubtitleSwitchTimeoutAsync(source.MediaFileId, _pendingSubtitleSwitchTargetSid, requestId);

        var stopwatch = Stopwatch.StartNew();
        bool result;
        switch (selection.Kind)
        {
            case SubtitleSelectionKind.None:
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-stage requestId={requestId} stage=command-sent elapsedMs={GetSubtitleSwitchElapsedMs()}");
                MpvPlaybackDiagnostics.Write($"subtitle-switch-command-sent requestId={requestId} command=sid target=no");
                result = await engine.SetSubtitleTrackAsync(null);
                break;

            case SubtitleSelectionKind.Embedded when selection.EmbeddedTrackId.HasValue:
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-stage requestId={requestId} stage=command-sent elapsedMs={GetSubtitleSwitchElapsedMs()}");
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-command-sent requestId={requestId} command=sid target={selection.EmbeddedTrackId.Value}");
                result = await engine.SetSubtitleTrackAsync(selection.EmbeddedTrackId.Value);
                break;

            case SubtitleSelectionKind.External when selection.ExternalMpvTrackId.HasValue:
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-stage requestId={requestId} stage=command-sent elapsedMs={GetSubtitleSwitchElapsedMs()}");
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-external-reuse externalSubtitleId={selection.ExternalSubtitleId.GetValueOrDefault()} mpvTrackId={selection.ExternalMpvTrackId.Value}");
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-command-sent requestId={requestId} command=sid target={selection.ExternalMpvTrackId.Value}");
                result = await engine.SetSubtitleTrackAsync(selection.ExternalMpvTrackId.Value);
                break;

            case SubtitleSelectionKind.External:
                if (string.IsNullOrWhiteSpace(selectedSubtitle.PlaybackUrl))
                {
                    result = false;
                    break;
                }

                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-stage requestId={requestId} stage=command-sent elapsedMs={GetSubtitleSwitchElapsedMs()}");
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-command-sent requestId={requestId} command=sub-add urlLength={selectedSubtitle.PlaybackUrl.Length} auth={(string.IsNullOrWhiteSpace(source.Username) ? "none" : "basic")}");
                result = await engine.AddExternalSubtitleAsync(
                    selectedSubtitle.PlaybackUrl,
                    source.Username,
                    source.Password,
                    select: true);
                break;

            default:
                result = false;
                break;
        }

        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-command-return requestId={requestId} elapsedMs={stopwatch.ElapsedMilliseconds} result={result.ToString().ToLowerInvariant()}");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-stage requestId={requestId} stage=command-return elapsedMs={GetSubtitleSwitchElapsedMs()}");
        MpvPlaybackDiagnostics.Write($"subtitle-switch-no-reload-confirmed requestId={requestId}");
        if (!result)
        {
            FailPendingSubtitleSwitch("command-failed");
            return false;
        }

        if (selection.Kind == SubtitleSelectionKind.None)
        {
            CompletePendingSubtitleSwitch(null);
        }

        if (applyVersion != _subtitleApplyVersion)
        {
            return true;
        }

        return true;
    }

    private SubtitleSelection BuildSubtitleSelection(
        PlaybackSubtitleItem selectedSubtitle,
        bool isUserInitiated,
        int requestId)
    {
        return selectedSubtitle.Type switch
        {
            PlaybackSubtitleType.EmbeddedTrack => new SubtitleSelection(
                SubtitleSelectionKind.Embedded,
                selectedSubtitle.TrackId,
                null,
                null,
                EnsureSubtitleUniqueKey(selectedSubtitle),
                selectedSubtitle.DisplayName,
                isUserInitiated,
                requestId),
            PlaybackSubtitleType.ExternalFile => new SubtitleSelection(
                SubtitleSelectionKind.External,
                null,
                selectedSubtitle.SubtitleMediaFileId,
                TryGetExternalSubtitleMpvTrackId(selectedSubtitle),
                EnsureSubtitleUniqueKey(selectedSubtitle),
                selectedSubtitle.DisplayName,
                isUserInitiated,
                requestId),
            _ => SubtitleSelection.None(requestId, isUserInitiated)
        };
    }

    private PlaybackSubtitleItem ResolveSubtitleFromSelection(SubtitleSelection selection)
    {
        return selection.Kind switch
        {
            SubtitleSelectionKind.Embedded => EmbeddedSubtitles.FirstOrDefault(
                subtitle => selection.EmbeddedTrackId.HasValue
                            && subtitle.TrackId == selection.EmbeddedTrackId.Value)
                ?? _noneSubtitleItem,
            SubtitleSelectionKind.External => ExternalSubtitles.FirstOrDefault(
                subtitle => string.Equals(EnsureSubtitleUniqueKey(subtitle), selection.SubtitleKey, StringComparison.OrdinalIgnoreCase)
                            || (selection.ExternalSubtitleId.HasValue
                                && subtitle.SubtitleMediaFileId == selection.ExternalSubtitleId.Value))
                ?? _onlinePlaybackSubtitles.FirstOrDefault(
                    subtitle => string.Equals(EnsureSubtitleUniqueKey(subtitle), selection.SubtitleKey, StringComparison.OrdinalIgnoreCase))
                ?? _noneSubtitleItem,
            _ => _noneSubtitleItem
        };
    }

    private int? TryGetExternalSubtitleMpvTrackId(PlaybackSubtitleItem subtitle)
    {
        return _externalSubtitleMpvTrackIds.TryGetValue(EnsureSubtitleUniqueKey(subtitle), out var trackId)
            ? trackId
            : null;
    }

    private static string FormatSubtitleSelectionKind(SubtitleSelectionKind kind)
    {
        return kind switch
        {
            SubtitleSelectionKind.Embedded => "embedded",
            SubtitleSelectionKind.External => "external",
            _ => "none"
        };
    }

    private static string FormatSubtitleSelectionTarget(SubtitleSelection selection)
    {
        return selection.Kind switch
        {
            SubtitleSelectionKind.Embedded => FormatOptionalTrackId(selection.EmbeddedTrackId),
            SubtitleSelectionKind.External => selection.ExternalSubtitleId?.ToString(CultureInfo.InvariantCulture) ?? "external",
            _ => "no"
        };
    }

    private void SubscribePlaybackEngine(IPlaybackEngine engine)
    {
        engine.Opening += OnPlaybackEngineOpening;
        engine.Playing += OnPlaybackEnginePlaying;
        engine.Paused += OnPlaybackEnginePaused;
        engine.Buffering += OnPlaybackEngineBuffering;
        engine.PositionChanged += OnPlaybackEnginePositionChanged;
        engine.DurationChanged += OnPlaybackEngineDurationChanged;
        engine.EndReached += OnPlaybackEngineEndReached;
        engine.EncounteredError += OnPlaybackEngineEncounteredError;
        engine.TracksChanged += OnPlaybackEngineTracksChanged;
        engine.SubtitleTrackChanged += OnPlaybackEngineSubtitleTrackChanged;
        engine.AudioTrackChanged += OnPlaybackEngineAudioTrackChanged;
    }

    private void UnsubscribePlaybackEngine(IPlaybackEngine engine)
    {
        engine.Opening -= OnPlaybackEngineOpening;
        engine.Playing -= OnPlaybackEnginePlaying;
        engine.Paused -= OnPlaybackEnginePaused;
        engine.Buffering -= OnPlaybackEngineBuffering;
        engine.PositionChanged -= OnPlaybackEnginePositionChanged;
        engine.DurationChanged -= OnPlaybackEngineDurationChanged;
        engine.EndReached -= OnPlaybackEngineEndReached;
        engine.EncounteredError -= OnPlaybackEngineEncounteredError;
        engine.TracksChanged -= OnPlaybackEngineTracksChanged;
        engine.SubtitleTrackChanged -= OnPlaybackEngineSubtitleTrackChanged;
        engine.AudioTrackChanged -= OnPlaybackEngineAudioTrackChanged;
    }

    private void OnPlaybackEngineOpening(object? sender, EventArgs e)
    {
        TracePlayback(
            $"playback-opening mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0} mode={FormatPlaybackMode(_currentPlaybackMode)} engine=mpv");
        DispatchMainPlaybackUiState(MainPlaybackUiState.Opening, "engine-opening");
        if (_isSeekInProgress)
        {
            DispatchSeekRecoveringState();
        }
        else if (ShouldDisplayBufferingState())
        {
            DispatchStartupLoadingState();
        }
        else
        {
            DispatchClearBufferingState();
        }
    }

    private void OnPlaybackEnginePlaying(object? sender, EventArgs e)
    {
        TracePlayback(
            $"playback-playing mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0} mode={FormatPlaybackMode(_currentPlaybackMode)} engine=mpv");
        var engineIsPlaying = _playbackEngine?.IsPlaying ?? true;
        var awaitingInitialProgress = _isAwaitingInitialPlaybackProgress && !_isSeekInProgress;
        if (awaitingInitialProgress)
        {
            DispatchMainPlaybackUiState(MainPlaybackUiState.Starting, "engine-playing-awaiting-initial-position");
            MpvPlaybackDiagnostics.Write("player-ui-startup-overlay-held reason=awaiting-initial-position");
        }
        else
        {
            DispatchMainPlaybackUiState(MainPlaybackUiState.Playing, "engine-playing");
        }

        if (_isSeekInProgress)
        {
            ClearSeekRecovery(resetBuffering: true);
        }
        else if (!awaitingInitialProgress)
        {
            DispatchBufferingState(100d);
        }

        DispatchPlaybackState(engineIsPlaying);
        if (engineIsPlaying)
        {
            if (!awaitingInitialProgress)
            {
                ScheduleResumeMessageClearAfterPlaybackStarted();
            }

            _ = StartPendingWatchHistoryAfterPlaybackReadyAsync();
        }

        if (!engineIsPlaying)
        {
            DispatchMainPlaybackUiState(MainPlaybackUiState.Paused, "engine-playing-paused");
        }

        if (PlaybackEngineDefersTrackFeatures())
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r1-track-refresh-skipped mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0} reason=minimal-core");
            return;
        }

        _ = RefreshEmbeddedSubtitleTracksAsync(applyDefaultAfterRefresh: true);
        _ = RefreshAudioTracksAsync(applyDefaultAfterRefresh: true);
    }

    private void OnPlaybackEnginePaused(object? sender, EventArgs e)
    {
        ClearSeekRecovery(resetBuffering: true);
        DispatchClearBufferingState();
        DispatchPlaybackState(false);
        DispatchMainPlaybackUiState(MainPlaybackUiState.Paused, "engine-paused");
    }

    private void OnPlaybackEngineBuffering(object? sender, PlaybackBufferingEventArgs e)
    {
        TracePlayback(
            $"playback-buffering mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0} mode={FormatPlaybackMode(_currentPlaybackMode)} percent={e.Percent.ToString("F1", CultureInfo.InvariantCulture)} engine=mpv");
        if (_isSeekInProgress)
        {
            if (e.Percent >= 99.5d)
            {
                ClearSeekRecovery(resetBuffering: true);
                return;
            }

            DispatchSeekRecoveringState(e.Percent);
        }
        else if (HasPendingSubtitleSwitch())
        {
            HandleSubtitleSwitchBuffering(e);
        }
        else if (HasPendingAudioTrackSwitch())
        {
            HandleAudioTrackSwitchBuffering(e);
        }
        else if (ShouldDisplayBufferingState())
        {
            DispatchBufferingState(e.Percent);
        }
        else
        {
            DispatchClearBufferingState();
        }
    }

    private void HandleSubtitleSwitchBuffering(PlaybackBufferingEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => HandleSubtitleSwitchBuffering(e));
            return;
        }

        var pausedForCache = e.PausedForCache ?? e.Percent < 99.5d;
        var requestId = _pendingSubtitleSelection?.RequestId ?? _subtitleSwitchUiVersion;
        MpvPlaybackDiagnostics.Write(
            $"mpv-subtitle-switch-buffering pausedForCache={pausedForCache.ToString().ToLowerInvariant()} percent={e.Percent.ToString("F1", CultureInfo.InvariantCulture)}");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-switch-stage requestId={requestId} stage=paused-for-cache elapsedMs={GetSubtitleSwitchElapsedMs()} value={pausedForCache.ToString().ToLowerInvariant()}");
        if (pausedForCache || e.Percent < 99.5d)
        {
            IsBuffering = true;
            BufferingPercent = Math.Clamp(e.Percent, 0d, 100d);
            BufferingStatusText = BufferingPercent > 0d
                ? $"\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e {BufferingPercent:0}%"
                : "\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e...";
            LogBufferingUiState("subtitle-switch", "subtitle-wait", BufferingPercent, "paused-for-cache");
            if (StatusMessage == SubtitleSwitchingStatusMessage)
            {
                StatusMessage = "\u6b63\u5728\u7b49\u5f85\u5b57\u5e55/\u89c6\u9891\u6570\u636e...";
                SetOperationNotice(PlayerOperationNoticeKind.Subtitle, StatusMessage, "subtitle-switch-buffering", TimeSpan.FromSeconds(4));
            }

            return;
        }

        DispatchClearBufferingState();
        if (_pendingSubtitleSwitchConfirmed)
        {
            ClearPendingSubtitleSwitch(success: true);
        }
    }

    private void HandleAudioTrackSwitchBuffering(PlaybackBufferingEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => HandleAudioTrackSwitchBuffering(e));
            return;
        }

        var pausedForCache = e.PausedForCache ?? e.Percent < 99.5d;
        MpvPlaybackDiagnostics.Write(
            $"mpv-audio-switch-buffering pausedForCache={pausedForCache.ToString().ToLowerInvariant()} percent={e.Percent.ToString("F1", CultureInfo.InvariantCulture)}");
        if (pausedForCache || e.Percent < 99.5d)
        {
            IsBuffering = true;
            BufferingPercent = Math.Clamp(e.Percent, 0d, 100d);
            BufferingStatusText = BufferingPercent > 0d
                ? $"\u6b63\u5728\u7b49\u5f85\u97f3\u9891/\u89c6\u9891\u6570\u636e {BufferingPercent:0}%"
                : "\u6b63\u5728\u7b49\u5f85\u97f3\u9891/\u89c6\u9891\u6570\u636e...";
            LogBufferingUiState("audio-switch", "audio-wait", BufferingPercent, "paused-for-cache");
            if (StatusMessage == AudioSwitchingStatusMessage)
            {
                StatusMessage = "\u6b63\u5728\u7b49\u5f85\u97f3\u9891/\u89c6\u9891\u6570\u636e...";
                SetOperationNotice(PlayerOperationNoticeKind.Audio, StatusMessage, "audio-switch-buffering", TimeSpan.FromSeconds(4));
            }

            return;
        }

        DispatchClearBufferingState();
        if (_pendingAudioTrackSwitchConfirmed)
        {
            ClearPendingAudioTrackSwitch(success: true);
        }
    }

    private void OnPlaybackEnginePositionChanged(object? sender, PlaybackPositionChangedEventArgs e)
    {
        var playbackSeconds = Math.Max(0d, e.Position.TotalSeconds);
        if (!ShouldUpdatePositionUi(playbackSeconds))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ApplyEnginePosition(playbackSeconds));
            return;
        }

        ApplyEnginePosition(playbackSeconds);
    }

    private bool ShouldUpdatePositionUi(double playbackSeconds)
    {
        var now = DateTime.UtcNow;
        var positionSecond = (int)Math.Floor(playbackSeconds);
        lock (_enginePositionUiThrottleLock)
        {
            if (positionSecond == _lastEnginePositionUiUpdateSecond
                && now - _lastEnginePositionUiUpdateUtc < TimeSpan.FromMilliseconds(EnginePositionUiThrottleMilliseconds))
            {
                return false;
            }

            _lastEnginePositionUiUpdateUtc = now;
            _lastEnginePositionUiUpdateSecond = positionSecond;
            return true;
        }
    }

    private void ResetEnginePositionUiThrottle()
    {
        lock (_enginePositionUiThrottleLock)
        {
            _lastEnginePositionUiUpdateUtc = DateTime.MinValue;
            _lastEnginePositionUiUpdateSecond = -1;
        }
    }

    private void OnPlaybackEngineDurationChanged(object? sender, PlaybackDurationChangedEventArgs e)
    {
        var durationSeconds = Math.Max(0d, e.Duration.TotalSeconds);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => DurationSeconds = durationSeconds);
            return;
        }

        DurationSeconds = durationSeconds;
    }

    private async void OnPlaybackEngineEndReached(object? sender, EventArgs e)
    {
        ClearSeekRecovery(resetBuffering: false);
        DispatchResetBufferingState();
        DispatchStoppedState();
        DispatchPlaybackState(false);
        try
        {
            await PersistProgressSnapshotAsync((int)Math.Max(0, PositionSeconds), true);
            MpvPlaybackDiagnostics.Write($"mpv-end-reached-handled mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0}");
            if (_currentContentType == PlaybackContentType.Episode)
            {
                await TryAutoPlayNextEpisodeOnDispatcherAsync();
            }
        }
        catch
        {
        }
    }

    private async Task TryAutoPlayNextEpisodeOnDispatcherAsync()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(TryAutoPlayNextEpisodeAsync).Task.Unwrap();
            return;
        }

        await TryAutoPlayNextEpisodeAsync();
    }

    private async Task TryAutoPlayNextEpisodeAsync()
    {
        if (_disposed || _session?.ContentType != PlaybackContentType.Episode)
        {
            return;
        }

        var nextEpisode = _session.NextEpisode;
        if (nextEpisode is null)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Playback, "\u672c\u5b63\u5df2\u64ad\u653e\u5230\u6700\u540e\u4e00\u96c6\u3002", "episode-auto-next-end");
            return;
        }

        if (!nextEpisode.HasPlayableSource)
        {
            SetOperationNotice(PlayerOperationNoticeKind.Playback, "\u4e0b\u4e00\u96c6\u6682\u65e0\u53ef\u7528\u64ad\u653e\u6e90\u3002", "episode-auto-next-no-source");
            return;
        }

        await OpenAdjacentEpisodeAsync(nextEpisode, "episode-auto-next");
    }

    private void OnPlaybackEngineEncounteredError(object? sender, PlaybackEngineErrorEventArgs e)
    {
        TracePlayback(
            $"playback-encountered-error mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0} mode={FormatPlaybackMode(_currentPlaybackMode)} engine=mpv errorType={e.ErrorType}");
        if (HasPendingSubtitleSwitch())
        {
            FailPendingSubtitleSwitch(e.ErrorType);
        }

        ReleaseCurrentVideoCacheLease("stop");
        DispatchResetBufferingState();
        DispatchStoppedState();
        DispatchPlaybackState(false);
        DispatchMainPlaybackUiState(MainPlaybackUiState.Error, "engine-error", ResolvePlaybackEngineErrorMessage(e));
    }

    private string ResolvePlaybackEngineErrorMessage(PlaybackEngineErrorEventArgs error)
    {
        if (!string.IsNullOrWhiteSpace(error.Message))
        {
            return error.Message;
        }

        return error.ErrorType switch
        {
            "webdav-auth-failed" or "webdav-access-denied" => "\u0057\u0065\u0062\u0044\u0041\u0056 \u9274\u6743\u5931\u8d25\uff0c\u8bf7\u68c0\u67e5\u8d26\u53f7\u6216\u8fde\u63a5\u3002",
            "network-timeout" or "network-unreachable" or "webdav-open-failed" or "startup-timeout" => "\u64ad\u653e\u6e90\u8fde\u63a5\u5931\u8d25\uff0c\u8bf7\u68c0\u67e5 WebDAV \u8fde\u63a5\u3002",
            "webdav-file-not-found" => "\u64ad\u653e\u6e90\u6587\u4ef6\u4e0d\u5b58\u5728\u6216\u65e0\u6cd5\u8bbf\u95ee\u3002",
            _ => _currentPlaybackMode == PlaybackSourceMode.WebDavDirect
                ? "\u64ad\u653e\u6e90\u8fde\u63a5\u5931\u8d25\uff0c\u8bf7\u68c0\u67e5 WebDAV \u8fde\u63a5\u3002"
                : "\u64ad\u653e\u51fa\u9519\uff0c\u5df2\u505c\u6b62\u3002"
        };
    }

    private void OnPlaybackEngineSubtitleTrackChanged(object? sender, PlaybackSubtitleTrackChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OnPlaybackEngineSubtitleTrackChanged(sender, e));
            return;
        }

        CompletePendingSubtitleSwitch(e.TrackId);
    }

    private void OnPlaybackEngineAudioTrackChanged(object? sender, PlaybackAudioTrackChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => OnPlaybackEngineAudioTrackChanged(sender, e));
            return;
        }

        CompletePendingAudioTrackSwitch(e.TrackId);
    }

    private async void OnPlaybackEngineTracksChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            TracePlayback(
                $"playback-tracks-changed mediaFileId={_currentPlaybackSource?.MediaFileId ?? SelectedSource?.MediaFileId ?? 0} engine=mpv");
            if (_playbackEngine is null || SelectedSource is null)
            {
                return;
            }

            var sourceId = SelectedSource.MediaFileId;
            RecordSubtitleTrackDiscoveryUpdate(sourceId, _playbackEngine.SubtitleTracks);
            var trackListChangeVersion = Interlocked.Increment(ref _mpvTrackListChangeVersion);
            await Task.Delay(TimeSpan.FromMilliseconds(700));
            if (_disposed
                || trackListChangeVersion != _mpvTrackListChangeVersion
                || SelectedSource?.MediaFileId != sourceId
                || _playbackEngine is null)
            {
                return;
            }

            var audioVersion = Interlocked.Increment(ref _audioTrackRefreshVersion);
            var subtitleVersion = Interlocked.Increment(ref _subtitleRefreshVersion);
            MpvPlaybackDiagnostics.Write(
                $"subtitle-track-list-update requestId={_pendingSubtitleSelection?.RequestId ?? 0} subCount={_playbackEngine.SubtitleTracks.Count} externalTrackCount=0");
            await DispatchAudioTracksAsync(_playbackEngine.AudioTracks, audioVersion);
            await DispatchEmbeddedSubtitleTracksAsync(_playbackEngine.SubtitleTracks, subtitleVersion);
            if (HasPendingSubtitleSwitch())
            {
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-stage requestId={_pendingSubtitleSelection?.RequestId ?? _subtitleSwitchUiVersion} stage=track-selected elapsedMs={GetSubtitleSwitchElapsedMs()} selectedSid=unknown");
            }

            if (_disposed || SelectedSource?.MediaFileId != sourceId)
            {
                return;
            }

            var allowAutomaticSelection = DateTime.UtcNow >= _suppressAutomaticTrackSelectionUntilUtc;
            if (allowAutomaticSelection
                && !_hasAppliedAutomaticAudioTrackForCurrentMedia
                && !_hasUserSelectedAudioTrack
                && !_isApplyingAudioTrack)
            {
                await ApplyAutomaticDefaultAudioTrackAsync();
            }

            if (allowAutomaticSelection
                && !_hasAppliedAutomaticSubtitleForCurrentMedia
                && !_hasUserSelectedSubtitle
                && !_isApplyingSubtitle
                && IsSubtitleTrackDiscoveryReady)
            {
                MpvPlaybackDiagnostics.Write("subtitle-auto-select-start reason=media-load");
                await ApplyAutomaticDefaultSubtitleAsync();
            }
            else if (!allowAutomaticSelection || _hasUserSelectedSubtitle || _isApplyingSubtitle || !IsSubtitleTrackDiscoveryReady)
            {
                var reason = _hasUserSelectedSubtitle
                    ? "user-selected"
                    : _isApplyingSubtitle
                        ? "subtitle-switch"
                        : !IsSubtitleTrackDiscoveryReady
                            ? "track-discovery"
                            : "track-list-update";
                MpvPlaybackDiagnostics.Write($"subtitle-auto-select-skipped reason={reason}");
            }
        }
        catch (Exception exception)
        {
            TracePlayback($"mpv-tracks-update-failed errorType={exception.GetType().Name}");
        }
    }

    private void ApplyEnginePosition(double playbackSeconds)
    {
        if (_disposed)
        {
            return;
        }

        if (!_isPlaybackRunning
            && !IsPlaybackEnginePlaying()
            && DateTime.UtcNow < _holdPausedSeekDisplayUntilUtc)
        {
            playbackSeconds = _pausedSeekDisplaySeconds;
        }

        _isUpdatingPosition = true;
        PositionSeconds = playbackSeconds;
        _isUpdatingPosition = false;
        OnPropertyChanged(nameof(PositionText));
        ReleaseStartupOverlayOnInitialProgress(playbackSeconds);
        UpdateSeekRecoveryProgress(playbackSeconds);
        if (HasPendingSubtitleSwitch() && !_pendingSubtitleSwitchTimePositionLogged)
        {
            var moving = Math.Abs(playbackSeconds - _pendingSubtitleSwitchStartPosition) >= 0.25d;
            if (moving || GetSubtitleSwitchElapsedMs() >= 1500)
            {
                _pendingSubtitleSwitchTimePositionLogged = true;
                MpvPlaybackDiagnostics.Write(
                    $"subtitle-switch-stage requestId={_pendingSubtitleSelection?.RequestId ?? _subtitleSwitchUiVersion} stage=time-pos-check elapsedMs={GetSubtitleSwitchElapsedMs()} moving={moving.ToString().ToLowerInvariant()}");
            }
        }

        if (_pendingSubtitleSwitchConfirmed
            && !IsBuffering
            && _playbackEngine?.IsBuffering != true)
        {
            ClearPendingSubtitleSwitch(success: true);
        }
    }

    private void ReleaseStartupOverlayOnInitialProgress(double playbackSeconds)
    {
        if (!_isAwaitingInitialPlaybackProgress)
        {
            return;
        }

        _isAwaitingInitialPlaybackProgress = false;
        MpvPlaybackDiagnostics.Write(
            $"player-ui-startup-overlay-released reason=initial-position seconds={playbackSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (_mainPlaybackUiState is MainPlaybackUiState.Opening
            or MainPlaybackUiState.LoadingMetadata
            or MainPlaybackUiState.Starting
            or MainPlaybackUiState.Recovering)
        {
            var isPlaying = IsPlaybackEnginePlaying();
            SetMainPlaybackUiState(isPlaying ? MainPlaybackUiState.Playing : MainPlaybackUiState.Paused, "initial-playback-progress");
            if (isPlaying)
            {
                ScheduleResumeMessageClearAfterPlaybackStarted();
            }
        }
        else
        {
            RefreshBufferingOverlayProjection("initial-playback-progress");
            if (IsPlaybackEnginePlaying())
            {
                ScheduleResumeMessageClearAfterPlaybackStarted();
            }
        }
    }

    private async Task<VideoCacheAcquireResult> TryAcquireVideoCachePlaybackAsync(PlaybackSourceItem source)
    {
        if (source.ProtocolType != ProtocolType.WebDav)
        {
            return VideoCacheAcquireResult.Miss;
        }

        try
        {
            var result = await _videoCacheService.AcquirePlaybackAsync(source);
            if (result.IsHit)
            {
                ApplyVideoCacheStatus(
                    source,
                    new VideoCacheStatusResult
                    {
                        Status = VideoCacheStatus.InUse,
                        ProgressPercent = 100d,
                        LocalFilePath = result.LocalFilePath,
                        Error = "\u672c\u5730\u7f13\u5b58\u6b63\u5728\u4f7f\u7528\uff0c\u505c\u6b62\u64ad\u653e\u540e\u53ef\u5220\u9664\u3002"
                    });
            }

            return result;
        }
        catch
        {
            return VideoCacheAcquireResult.Miss;
        }
    }

    private static string FormatPlaybackMode(PlaybackSourceMode mode)
    {
        return mode switch
        {
            PlaybackSourceMode.CompleteFile => "complete-file",
            PlaybackSourceMode.LocalFile => "local-file",
            PlaybackSourceMode.WebDavDirect => "webdav-direct",
            _ => "none"
        };
    }

    private static bool TryResolveLocalPlaybackPath(PlaybackSourceItem source, out string playbackPath)
    {
        playbackPath = string.IsNullOrWhiteSpace(source.PlaybackUrl) ? source.FilePath : source.PlaybackUrl;
        if (string.IsNullOrWhiteSpace(playbackPath))
        {
            return false;
        }

        try
        {
            return File.Exists(playbackPath);
        }
        catch
        {
            return false;
        }
    }

    private async Task TryStartAutoVideoCacheAsync(PlaybackSourceItem source)
    {
        if (_disposed || source.ProtocolType != ProtocolType.WebDav)
        {
            return;
        }

        try
        {
            await _videoCacheService.TryEnqueueAutoDownloadAsync(source);
        }
        catch
        {
            // Auto caching is advisory; playback must continue through the normal WebDAV path.
        }
    }

    private void ReleaseCurrentVideoCacheLease(string reason = "lease-release")
    {
        try
        {
            _currentVideoCacheLease?.Dispose(reason);
        }
        catch
        {
        }
        finally
        {
            _currentVideoCacheLease = null;
        }
    }

    private static int ResolveSourceResumePosition(PlaybackSourceItem source)
    {
        return source.ResumePositionSeconds > 0
            ? source.ResumePositionSeconds
            : source.LastPlayPositionSeconds;
    }

    private async Task RefreshAudioTracksAsync(bool applyDefaultAfterRefresh)
    {
        var sourceId = SelectedSource?.MediaFileId;
        var refreshVersion = Interlocked.Increment(ref _audioTrackRefreshVersion);
        IReadOnlyList<PlaybackAudioTrackItem> tracks = [];
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await Task.Delay(attempt == 0 ? 120 : 220);
            if (_disposed
                || refreshVersion != _audioTrackRefreshVersion
                || sourceId != SelectedSource?.MediaFileId)
            {
                return;
            }

            tracks = ReadAudioTracks();
            if (tracks.Count > 0 || attempt == 7)
            {
                break;
            }
        }

        await DispatchAudioTracksAsync(tracks, refreshVersion);

        if (_disposed
            || refreshVersion != _audioTrackRefreshVersion
            || sourceId != SelectedSource?.MediaFileId)
        {
            return;
        }

        if (_hasUserSelectedAudioTrack)
        {
            if (SelectedAudioTrack is not null)
            {
                await ApplyAudioTrackSelectionAsync(SelectedAudioTrack, isUserInitiated: false);
            }
            else
            {
                SyncSelectedAudioTrackToCurrent();
            }

            return;
        }

        if (applyDefaultAfterRefresh
            && !_hasAppliedAutomaticAudioTrackForCurrentMedia
            && DateTime.UtcNow >= _suppressAutomaticTrackSelectionUntilUtc
            && !_isApplyingAudioTrack)
        {
            await ApplyAutomaticDefaultAudioTrackAsync();
        }
    }

    private IReadOnlyList<PlaybackAudioTrackItem> ReadAudioTracks()
    {
        if (_isMpvMediaLoaded && _playbackEngine is not null)
        {
            return _playbackEngine.AudioTracks
                .Select(CloneAudioTrack)
                .ToList();
        }

        return [];
    }

    private async Task DispatchAudioTracksAsync(
        IReadOnlyList<PlaybackAudioTrackItem> tracks,
        int refreshVersion)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => UpdateAudioTracks(tracks, refreshVersion));
            return;
        }

        UpdateAudioTracks(tracks, refreshVersion);
    }

    private void UpdateAudioTracks(IReadOnlyList<PlaybackAudioTrackItem> tracks, int refreshVersion)
    {
        if (_disposed || refreshVersion != _audioTrackRefreshVersion)
        {
            return;
        }

        IsAudioTrackDiscoveryReady = true;
        var preparedTracks = PrepareAudioTrackDisplays(tracks);
        var selectedTrack = SelectedAudioTrack;
        AudioTracks.Clear();
        foreach (var track in preparedTracks)
        {
            AudioTracks.Add(track);
        }

        if (selectedTrack is not null)
        {
            var replacement = FindMatchingAudioTrack(selectedTrack);
            if (replacement is not null)
            {
                SetSelectedAudioTrackSilently(replacement);
                return;
            }
        }

        SyncSelectedAudioTrackToCurrent();
    }

    private async Task RefreshEmbeddedSubtitleTracksAsync(bool applyDefaultAfterRefresh)
    {
        var sourceId = SelectedSource?.MediaFileId;
        var refreshVersion = Interlocked.Increment(ref _subtitleRefreshVersion);
        IReadOnlyList<PlaybackSubtitleItem> tracks = [];
        var lastNonEmptyCount = -1;
        var stableCountReads = 0;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(attempt == 0 ? 160 : 260);
            if (_disposed
                || refreshVersion != _subtitleRefreshVersion
                || sourceId != SelectedSource?.MediaFileId)
            {
                return;
            }

            tracks = ReadEmbeddedSubtitleTracks();
            if (tracks.Count > 0)
            {
                if (tracks.Count == lastNonEmptyCount)
                {
                    stableCountReads++;
                }
                else
                {
                    lastNonEmptyCount = tracks.Count;
                    stableCountReads = 1;
                }
            }

            if ((tracks.Count > 0 && stableCountReads >= 2) || attempt == 9)
            {
                break;
            }
        }

        await DispatchEmbeddedSubtitleTracksAsync(tracks, refreshVersion);

        if (_disposed
            || refreshVersion != _subtitleRefreshVersion
            || sourceId != SelectedSource?.MediaFileId)
        {
            return;
        }

        if (_hasUserSelectedSubtitle)
        {
            MpvPlaybackDiagnostics.Write("subtitle-auto-select-skipped reason=user-selected");
            return;
        }

        if (applyDefaultAfterRefresh
            && !_hasAppliedAutomaticSubtitleForCurrentMedia
            && DateTime.UtcNow >= _suppressAutomaticTrackSelectionUntilUtc
            && !_isApplyingSubtitle)
        {
            MpvPlaybackDiagnostics.Write("subtitle-auto-select-start reason=media-load");
            await ApplyAutomaticDefaultSubtitleAsync();
        }
        else if (!applyDefaultAfterRefresh)
        {
            MpvPlaybackDiagnostics.Write("subtitle-auto-select-skipped reason=track-list-update");
        }
    }

    private IReadOnlyList<PlaybackSubtitleItem> ReadEmbeddedSubtitleTracks()
    {
        if (_isMpvMediaLoaded && _playbackEngine is not null)
        {
            return _playbackEngine.SubtitleTracks
                .Where(x => x.Type == PlaybackSubtitleType.EmbeddedTrack)
                .Select(CloneSubtitleTrack)
                .ToList();
        }

        return [];
    }

    private async Task DispatchEmbeddedSubtitleTracksAsync(
        IReadOnlyList<PlaybackSubtitleItem> tracks,
        int refreshVersion)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => UpdateEmbeddedSubtitleTracks(tracks, refreshVersion));
            return;
        }

        UpdateEmbeddedSubtitleTracks(tracks, refreshVersion);
    }

    private void UpdateEmbeddedSubtitleTracks(IReadOnlyList<PlaybackSubtitleItem> tracks, int refreshVersion)
    {
        if (_disposed || refreshVersion != _subtitleRefreshVersion)
        {
            return;
        }

        var preparedTracks = PrepareEmbeddedSubtitleDisplays(tracks, SelectedSource?.FileName);
        var selectedSubtitle = SelectedSubtitle;
        var incomingByTrackId = preparedTracks
            .Where(track => track.TrackId.HasValue)
            .GroupBy(track => track.TrackId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var existingIds = EmbeddedSubtitles
            .Where(track => track.TrackId.HasValue)
            .Select(track => track.TrackId!.Value)
            .OrderBy(trackId => trackId)
            .ToArray();
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-vm-subtitle-menu-merge-start incomingIds={FormatTrackIds(incomingByTrackId.Keys)} existingEmbeddedIds={FormatTrackIds(existingIds)}");
        if (_isMpvMediaLoaded && preparedTracks.Count == 0 && EmbeddedSubtitles.Count > 0)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-embedded-subtitle-refresh-skipped reason=empty-transient existingCount={EmbeddedSubtitles.Count}");
            return;
        }

        foreach (var (trackId, incomingTrack) in incomingByTrackId.OrderBy(pair => pair.Key))
        {
            var existingIndex = FindEmbeddedSubtitleIndex(trackId);
            if (existingIndex >= 0)
            {
                if (!AreSameEmbeddedSubtitleTrack(EmbeddedSubtitles[existingIndex], incomingTrack))
                {
                    EmbeddedSubtitles[existingIndex] = incomingTrack;
                    MpvPlaybackDiagnostics.Write($"mpv-r3-vm-subtitle-menu-updated trackId={trackId}");
                }

                continue;
            }

            EmbeddedSubtitles.Add(incomingTrack);
            MpvPlaybackDiagnostics.Write($"mpv-r3-vm-subtitle-menu-added trackId={trackId}");
        }

        foreach (var trackId in existingIds.Where(trackId => !incomingByTrackId.ContainsKey(trackId)))
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-vm-subtitle-menu-preserved trackId={trackId} reason=missing-in-incoming");
        }

        ReorderEmbeddedSubtitlesByTrackId();
        RebuildSubtitleMenuEmbeddedItems();

        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-vm-subtitle-menu-result embeddedIds={FormatTrackIds(EmbeddedSubtitles.Where(track => track.TrackId.HasValue).Select(track => track.TrackId!.Value))}");
        MpvPlaybackDiagnostics.Write(
            $"subtitle-menu-build-result embeddedCount={EmbeddedSubtitles.Count} externalCandidateCount={ExternalSubtitles.Count} mpvExternalCount=0 dedupedCount={preparedTracks.Count - incomingByTrackId.Count} selectedKind={FormatSubtitleSelectionKind(_currentSubtitleSelection.Kind)}");

        if (selectedSubtitle?.Type == PlaybackSubtitleType.EmbeddedTrack)
        {
            var replacement = FindMatchingSubtitle(selectedSubtitle);
            if (replacement is not null)
            {
                SetSelectedSubtitleSilently(replacement);
            }
        }
    }

    private int FindEmbeddedSubtitleIndex(int trackId)
    {
        for (var index = 0; index < EmbeddedSubtitles.Count; index++)
        {
            if (EmbeddedSubtitles[index].TrackId == trackId)
            {
                return index;
            }
        }

        return -1;
    }

    private void ReorderEmbeddedSubtitlesByTrackId()
    {
        var ordered = EmbeddedSubtitles
            .Where(track => track.TrackId.HasValue)
            .OrderBy(track => track.TrackId!.Value)
            .ToArray();
        for (var targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
        {
            var currentIndex = EmbeddedSubtitles.IndexOf(ordered[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                EmbeddedSubtitles.Move(currentIndex, targetIndex);
            }
        }
    }

    private void RebuildSubtitleMenuEmbeddedItems()
    {
        for (var index = Subtitles.Count - 1; index >= 0; index--)
        {
            if (Subtitles[index].Type == PlaybackSubtitleType.EmbeddedTrack)
            {
                Subtitles.RemoveAt(index);
            }
        }

        var insertIndex = Math.Min(1, Subtitles.Count);
        foreach (var track in EmbeddedSubtitles)
        {
            Subtitles.Insert(insertIndex++, track);
        }
    }

    private static bool AreSameEmbeddedSubtitleTracks(
        IReadOnlyList<PlaybackSubtitleItem> current,
        IReadOnlyList<PlaybackSubtitleItem> incoming)
    {
        if (current.Count != incoming.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (!AreSameEmbeddedSubtitleTrack(current[index], incoming[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSameEmbeddedSubtitleTrack(PlaybackSubtitleItem current, PlaybackSubtitleItem incoming)
    {
        return current.TrackId == incoming.TrackId
               && !string.IsNullOrWhiteSpace(current.UniqueKey)
               && string.Equals(current.UniqueKey, incoming.UniqueKey, StringComparison.Ordinal)
               && string.Equals(current.DisplayName, incoming.DisplayName, StringComparison.Ordinal)
               && string.Equals(current.OriginalName, incoming.OriginalName, StringComparison.Ordinal)
               && string.Equals(current.TooltipText, incoming.TooltipText, StringComparison.Ordinal)
               && current.IsPreferred == incoming.IsPreferred
               && current.IsAutoLoaded == incoming.IsAutoLoaded
               && current.Priority == incoming.Priority;
    }


    private static PlaybackAudioTrackItem CloneAudioTrack(PlaybackAudioTrackItem track)
    {
        return new PlaybackAudioTrackItem
        {
            DisplayName = track.DisplayName,
            OriginalName = track.OriginalName,
            TooltipText = track.TooltipText,
            TrackId = track.TrackId,
            IsSelected = track.IsSelected,
            Priority = track.Priority
        };
    }

    private static PlaybackSubtitleItem CloneSubtitleTrack(PlaybackSubtitleItem track)
    {
        return new PlaybackSubtitleItem
        {
            DisplayName = track.DisplayName,
            OriginalName = track.OriginalName,
            TooltipText = track.TooltipText,
            UniqueKey = track.UniqueKey,
            Type = track.Type,
            TrackId = track.TrackId,
            BindingId = track.BindingId,
            MediaFileId = track.MediaFileId,
            SubtitleMediaFileId = track.SubtitleMediaFileId,
            FileName = track.FileName,
            FilePath = track.FilePath,
            PlaybackUrl = track.PlaybackUrl,
            MatchType = track.MatchType,
            IsAuto = track.IsAuto,
            IsPreferred = track.IsPreferred,
            IsAutoLoaded = track.IsAutoLoaded,
            Priority = track.Priority,
            IsNoneOption = track.IsNoneOption
        };
    }

    private void TogglePlayPause()
    {
        if (_isStopping)
        {
            MpvPlaybackDiagnostics.Write("player-command-ignored reason=stopping command=toggle-play-pause");
            return;
        }

        if (_isReloadingMedia)
        {
            if (_playbackEngine is not null)
            {
                MpvPlaybackDiagnostics.Write("player-command-handled reason=media-loading command=toggle-play-pause");
                ToggleEnginePauseState();
            }
            else
            {
                MpvPlaybackDiagnostics.Write("player-command-ignored reason=engine-loading command=toggle-play-pause");
            }

            return;
        }

        if (!_isMpvMediaLoaded && _playbackEngine is not null && !_isStopped)
        {
            MpvPlaybackDiagnostics.Write("player-command-handled reason=mpv-not-yet-visible command=toggle-play-pause");
            ToggleEnginePauseState();
            return;
        }

        if (!_isMpvMediaLoaded || _isStopped)
        {
            MpvPlaybackDiagnostics.Write("player-command-ignored reason=media-not-loaded command=toggle-play-pause action=restart");
            _ = RestartStoppedPlaybackAsync();
            return;
        }

        ToggleEnginePauseState();
    }

    private void ToggleEnginePauseState()
    {
        var engineIsPlaying = _playbackEngine?.IsPlaying;
        var isCurrentlyPlaying = engineIsPlaying ?? _isPlaybackRunning;
        if (isCurrentlyPlaying)
        {
            _playbackEngine?.Pause();
            SetPlaybackState(false);
            ClearSeekRecovery(resetBuffering: true);
            SetMainPlaybackUiState(MainPlaybackUiState.Paused, "toggle-pause");
        }
        else
        {
            _playbackEngine?.Play();

            SetPlaybackState(true);
            _timer.Start();
            SetMainPlaybackUiState(MainPlaybackUiState.Playing, "toggle-play");
        }
    }

    private async Task RestartStoppedPlaybackAsync()
    {
        if (_disposed || SelectedSource is null)
        {
            return;
        }

        if (IsCompleted())
        {
            _isUpdatingPosition = true;
            PositionSeconds = 0;
            _isUpdatingPosition = false;
            OnPropertyChanged(nameof(PositionText));
        }

        await PlayCurrentSourceAsync(keepPosition: true);
    }

    private async Task StopAsync()
    {
        await ShutdownPlaybackAsync("stop", resetPosition: true, updateUi: true);
    }

    private Task ShutdownPlaybackAsync(string reason, bool resetPosition, bool updateUi)
    {
        MpvPlaybackDiagnostics.Write($"player-shutdown-entry reason={reason}");
        MpvPlaybackDiagnostics.Write($"player-r4-shutdown-start reason={reason}");
        return StopPlaybackAsync(reason, resetPosition, updateUi);
    }

    private async Task StopPlaybackAsync(string reason, bool resetPosition, bool updateUi)
    {
        if (_isStopping)
        {
            MpvPlaybackDiagnostics.Write("player-command-ignored reason=stopping command=stop");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _isStopping = true;
        StopCommand.RaiseCanExecuteChanged();
        try
        {
            _timer.Stop();
            MpvPlaybackDiagnostics.Write("player-r4-timer-stopped");
            var positionSnapshot = (int)Math.Max(0, PositionSeconds);
            var completed = IsCompleted();

            if (updateUi)
            {
                ClearOperationNotice($"shutdown-{reason}");
                SetPlaybackState(false);
                SetMainPlaybackUiState(reason == "close" ? MainPlaybackUiState.Closing : MainPlaybackUiState.Idle, $"shutdown-{reason}-start");
            }

            await StopPlaybackEngineSafelyAsync();
            ReleaseCurrentVideoCacheLease("stop");
            _isMpvMediaLoaded = false;
            if (updateUi && SelectedSource?.ProtocolType == ProtocolType.WebDav)
            {
                await RefreshVideoCacheStatusAsync(SelectedSource);
            }

            await PersistProgressSnapshotWithTimeoutAsync(positionSnapshot, completed, discardInvalid: true);
            _watchHistoryId = null;
            _activeMediaFileId = null;
            ClearPendingWatchHistoryStart();
            _isStopped = true;
            _isAwaitingInitialPlaybackProgress = false;
            ClearSeekRecovery(resetBuffering: false);
            ResetBufferingState();

            if (resetPosition)
            {
                _isUpdatingPosition = true;
                PositionSeconds = 0;
                _isUpdatingPosition = false;
                OnPropertyChanged(nameof(PositionText));
                ClearResumeMessage();
            }

            if (updateUi)
            {
                SetPlaybackState(false);
                SetMainPlaybackUiState(MainPlaybackUiState.Idle, $"shutdown-{reason}-complete");
            }
        }
        finally
        {
            _isStopping = false;
            StopCommand.RaiseCanExecuteChanged();
            MpvPlaybackDiagnostics.Write($"player-stop-complete elapsedMs={stopwatch.ElapsedMilliseconds} updateUi={updateUi.ToString().ToLowerInvariant()}");
            MpvPlaybackDiagnostics.Write($"player-r4-shutdown-complete reason={reason} elapsedMs={stopwatch.ElapsedMilliseconds}");
            if (stopwatch.ElapsedMilliseconds >= 3000)
            {
                MpvPlaybackDiagnostics.Write($"player-close-slow stage=stop-playback elapsedMs={stopwatch.ElapsedMilliseconds}");
                MpvPlaybackDiagnostics.Write($"player-r4-shutdown-slow stage=stop-playback elapsedMs={stopwatch.ElapsedMilliseconds}");
            }
        }
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var engine = _playbackEngine;
        if (engine is null)
        {
            return;
        }

        var playbackSeconds = Math.Max(0, engine.Position.TotalSeconds);
        if (!_isPlaybackRunning
            && !IsPlaybackEnginePlaying()
            && DateTime.UtcNow < _holdPausedSeekDisplayUntilUtc)
        {
            playbackSeconds = _pausedSeekDisplaySeconds;
        }

        _isUpdatingPosition = true;
        PositionSeconds = playbackSeconds;
        _isUpdatingPosition = false;
        OnPropertyChanged(nameof(PositionText));
        UpdateSeekRecoveryProgress(playbackSeconds);

        if (engine.Duration > TimeSpan.Zero)
        {
            DurationSeconds = engine.Duration.TotalSeconds;
        }

        if (_watchHistoryId.HasValue && DateTime.UtcNow.Second % 10 == 0)
        {
            await PersistProgressSnapshotAsync((int)Math.Max(0, PositionSeconds), IsCompleted());
        }
    }

    private async Task PersistProgressSnapshotAsync(int positionSeconds, bool isCompleted, bool discardInvalid = false)
    {
        if (!_watchHistoryId.HasValue)
        {
            return;
        }

        var watched = (int)Math.Max(0, (DateTime.UtcNow - _historyStartedAt).TotalSeconds);
        var normalizedPosition = Math.Max(0, positionSeconds);
        var mediaDurationSeconds = GetCurrentMediaDurationSeconds();
        if (normalizedPosition <= 0 || watched < 3)
        {
            if (mediaDurationSeconds.HasValue)
            {
                try
                {
                    var autoWatchedChanged = await _watchHistoryService.SaveProgressAsync(
                        _watchHistoryId.Value,
                        normalizedPosition,
                        watched,
                        isCompleted,
                        mediaDurationSeconds.Value);
                    NotifyAutoWatchedChanged(autoWatchedChanged);
                    MpvPlaybackDiagnostics.Write($"mpv-watch-history-save mediaFileId={_activeMediaFileId ?? SelectedSource?.MediaFileId ?? 0} positionSeconds={normalizedPosition} completed={isCompleted.ToString().ToLowerInvariant()}");
                }
                catch
                {
                    // Duration persistence is best-effort while the media is still warming up.
                }
            }

            if (discardInvalid)
            {
                try
                {
                    await _watchHistoryService.DiscardAsync(_watchHistoryId.Value);
                }
                catch
                {
                    // Discarding invalid ultra-short sessions is best-effort.
                }
            }

            return;
        }

        try
        {
            var autoWatchedChanged = await _watchHistoryService.SaveProgressAsync(
                _watchHistoryId.Value,
                normalizedPosition,
                watched,
                isCompleted,
                mediaDurationSeconds);
            UpdateActiveSourceProgressSnapshot(normalizedPosition);
            NotifyAutoWatchedChanged(autoWatchedChanged);
            MpvPlaybackDiagnostics.Write($"mpv-watch-history-save mediaFileId={_activeMediaFileId ?? SelectedSource?.MediaFileId ?? 0} positionSeconds={normalizedPosition} completed={isCompleted.ToString().ToLowerInvariant()}");
        }
        catch
        {
            // Saving progress is best-effort; playback window shutdown must not crash the app.
        }
    }

    private async Task PersistProgressSnapshotWithTimeoutAsync(int positionSeconds, bool isCompleted, bool discardInvalid)
    {
        var stopwatch = Stopwatch.StartNew();
        MpvPlaybackDiagnostics.Write("player-r4-watch-history-save-start");
        var saveTask = PersistProgressSnapshotAsync(positionSeconds, isCompleted, discardInvalid);
        var completed = await Task.WhenAny(saveTask, Task.Delay(ShutdownWatchHistorySaveTimeout));
        if (completed == saveTask)
        {
            await saveTask;
            MpvPlaybackDiagnostics.Write($"player-r4-watch-history-save-complete elapsedMs={stopwatch.ElapsedMilliseconds}");
            return;
        }

        MpvPlaybackDiagnostics.Write($"player-r4-watch-history-save-timeout elapsedMs={stopwatch.ElapsedMilliseconds}");
        _ = saveTask.ContinueWith(
            task =>
            {
                if (task.IsFaulted)
                {
                    MpvPlaybackDiagnostics.Write(
                        $"player-r4-watch-history-save-background-failed errorType={task.Exception?.GetBaseException().GetType().Name ?? "unknown"}");
                }
                else
                {
                    MpvPlaybackDiagnostics.Write("player-r4-watch-history-save-background-complete");
                }
            },
            TaskScheduler.Default);
    }

    private void NotifyAutoWatchedChanged(bool autoWatchedChanged)
    {
        if (!autoWatchedChanged)
        {
            return;
        }

        _dataRefreshService.NotifyCollectionChanged();
    }

    private void UpdateActiveSourceProgressSnapshot(int positionSeconds)
    {
        var source = _activeMediaFileId.HasValue
            ? Sources.FirstOrDefault(x => x.MediaFileId == _activeMediaFileId.Value)
            : SelectedSource;

        UpdateSourceProgressSnapshot(source, positionSeconds);
        if (source is null || positionSeconds <= 0)
        {
            return;
        }

        var syncedCount = 0;
        foreach (var candidate in Sources)
        {
            if (candidate.MediaFileId == source.MediaFileId)
            {
                continue;
            }

            if (!CanSharePlaybackProgress(source.DurationSeconds, candidate.DurationSeconds, positionSeconds))
            {
                continue;
            }

            UpdateSourceProgressSnapshot(candidate, positionSeconds);
            syncedCount++;
            MpvPlaybackDiagnostics.Write(
                $"watch-history-unified-resume-applied targetMediaFileId={candidate.MediaFileId} resume={positionSeconds} reason=session-progress-sync");
        }

        if (syncedCount > 0)
        {
            var contentKey = _session?.ContentType == PlaybackContentType.Episode ? "episodeId" : "movieId";
            var contentId = _session?.ContentType == PlaybackContentType.Episode
                ? _session.EpisodeId.GetValueOrDefault()
                : _session?.MovieId ?? 0;
            MpvPlaybackDiagnostics.Write(
                $"watch-history-unified-resume-selected {contentKey}={contentId} resume={positionSeconds} fromMediaFileId={source.MediaFileId} reason=session-progress-sync");
        }
    }

    private static void UpdateSourceProgressSnapshot(PlaybackSourceItem? source, int positionSeconds)
    {
        if (source is null || positionSeconds <= 0)
        {
            return;
        }

        source.LastPlayPositionSeconds = positionSeconds;
        source.ResumePositionSeconds = positionSeconds;
        source.LastPlayedAt = DateTime.UtcNow;
    }

    private static bool CanSharePlaybackProgress(int? sourceDurationSeconds, int? targetDurationSeconds, int positionSeconds)
    {
        if (!sourceDurationSeconds.HasValue || !targetDurationSeconds.HasValue)
        {
            return false;
        }

        if (IsNearOrPastEnding(positionSeconds, targetDurationSeconds.Value))
        {
            return false;
        }

        return AreDurationsCompatible(sourceDurationSeconds.Value, targetDurationSeconds.Value);
    }

    private static bool AreDurationsCompatible(int sourceDurationSeconds, int targetDurationSeconds)
    {
        if (sourceDurationSeconds <= 0 || targetDurationSeconds <= 0)
        {
            return false;
        }

        var diffSeconds = Math.Abs(sourceDurationSeconds - targetDurationSeconds);
        var diffRatio = diffSeconds / (double)Math.Max(sourceDurationSeconds, targetDurationSeconds);
        return diffSeconds <= ResumeDurationToleranceSeconds
               || diffRatio <= ResumeDurationToleranceRatio;
    }

    private static bool IsNearOrPastEnding(int positionSeconds, int durationSeconds)
    {
        return durationSeconds > 0 && positionSeconds >= durationSeconds - 30;
    }

    private int? GetCurrentMediaDurationSeconds()
    {
        var engineDuration = _playbackEngine?.Duration ?? TimeSpan.Zero;
        if (engineDuration > TimeSpan.Zero)
        {
            return (int)Math.Round(engineDuration.TotalSeconds);
        }

        return DurationSeconds > 0
            ? (int)Math.Round(DurationSeconds)
            : null;
    }

    private bool IsCompleted()
    {
        return DurationSeconds > 0 && PositionSeconds / DurationSeconds >= 0.9;
    }

    private async Task LoadPlayerPreferencesAsync()
    {
        try
        {
            var preferences = await _playerPreferencesService.LoadAsync();
            if (_disposed || _hasUserChangedPlayerPreferencesThisSession)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (!_disposed && !_hasUserChangedPlayerPreferencesThisSession)
                    {
                        ApplyPlayerPreferences(preferences);
                    }
                });
                return;
            }

            ApplyPlayerPreferences(preferences);
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"player-ux1-preferences-load-failed errorType={exception.GetType().Name}");
        }
    }

    private void ApplyPlayerPreferences(PlayerPreferencesModel preferences)
    {
        _isApplyingPlayerPreferences = true;
        try
        {
            var restoredVolume = Math.Clamp(preferences.Volume, 0, 200);
            var restoredBrightness = Math.Clamp(preferences.Brightness, 0, 100);
            _isMuted = preferences.Muted || restoredVolume <= 0;
            if (restoredVolume > 0)
            {
                _lastNonZeroVolume = restoredVolume;
            }
            else if (_lastNonZeroVolume <= 0)
            {
                _lastNonZeroVolume = 50;
            }

            var displayedVolume = _isMuted ? 0 : Math.Max(0, restoredVolume);
            if (SetProperty(ref _volume, displayedVolume, nameof(Volume)))
            {
                OnPropertyChanged(nameof(VolumeText));
                OnPropertyChanged(nameof(VolumeFeedbackText));
            }

            if (SetProperty(ref _brightness, restoredBrightness, nameof(Brightness)))
            {
                OnPropertyChanged(nameof(BrightnessText));
            }

            ApplyVolumeToPlayer();
            ApplyBrightnessToPlayer();
            MpvPlaybackDiagnostics.Write(
                $"player-ux1-preferences-restored muted={_isMuted.ToString().ToLowerInvariant()} volume={(_isMuted ? _lastNonZeroVolume : _volume)} brightness={_brightness}");
        }
        finally
        {
            _isApplyingPlayerPreferences = false;
        }
    }

    private void SchedulePlayerPreferencesSave(string reason)
    {
        if (_disposed || _isApplyingPlayerPreferences)
        {
            return;
        }

        _pendingPlayerPreferencesSaveReason = MergePlayerPreferencesSaveReason(
            _pendingPlayerPreferencesSaveReason,
            _hasPendingPlayerPreferencesSave,
            reason);
        _hasPendingPlayerPreferencesSave = true;
        _playerPreferencesSaveTimer.Stop();
        _playerPreferencesSaveTimer.Start();
    }

    private async void OnPlayerPreferencesSaveTimerTick(object? sender, EventArgs e)
    {
        _playerPreferencesSaveTimer.Stop();
        if (_disposed || !_hasPendingPlayerPreferencesSave)
        {
            return;
        }

        var reason = _pendingPlayerPreferencesSaveReason;
        _hasPendingPlayerPreferencesSave = false;
        _pendingPlayerPreferencesSaveReason = "preferences";
        await SavePlayerPreferencesSnapshotAsync(reason, CancellationToken.None);
    }

    private static string MergePlayerPreferencesSaveReason(
        string pendingReason,
        bool hasPendingReason,
        string newReason)
    {
        if (!hasPendingReason || string.Equals(pendingReason, newReason, StringComparison.Ordinal))
        {
            return newReason;
        }

        return "preferences";
    }

    private async Task FlushPlayerPreferencesAsync(string reason, TimeSpan timeout)
    {
        _playerPreferencesSaveTimer.Stop();
        _hasPendingPlayerPreferencesSave = false;
        _pendingPlayerPreferencesSaveReason = "preferences";

        using var timeoutCts = new CancellationTokenSource(timeout);
        try
        {
            await SavePlayerPreferencesSnapshotAsync(reason, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            MpvPlaybackDiagnostics.Write($"player-ux1-preferences-save-timeout reason={reason}");
        }
    }

    private async Task SavePlayerPreferencesSnapshotAsync(string reason, CancellationToken cancellationToken)
    {
        var preferences = CreatePlayerPreferencesSnapshot();
        try
        {
            await _playerPreferencesService.SaveAsync(preferences, cancellationToken);
            MpvPlaybackDiagnostics.Write(
                $"player-ux1-preferences-saved reason={reason} muted={preferences.Muted.ToString().ToLowerInvariant()} volume={preferences.Volume} brightness={preferences.Brightness}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"player-ux1-preferences-save-failed reason={reason} errorType={exception.GetType().Name}");
        }
    }

    private PlayerPreferencesModel CreatePlayerPreferencesSnapshot()
    {
        var rememberedVolume = _isMuted && _lastNonZeroVolume > 0
            ? _lastNonZeroVolume
            : _volume;
        return new PlayerPreferencesModel
        {
            Volume = Math.Clamp(rememberedVolume, 0, 200),
            Muted = _isMuted,
            Brightness = Math.Clamp(_brightness, 0, 100)
        };
    }

    private void ApplyVolumeToPlayer()
    {
        try
        {
            var playbackEngine = _playbackEngine;
            if (playbackEngine is null)
            {
                return;
            }

            var effectiveVolume = _isMuted ? 0 : _volume;
            if (_lastAppliedPlayerVolume != effectiveVolume)
            {
                playbackEngine.SetVolume(effectiveVolume);
                _lastAppliedPlayerVolume = effectiveVolume;
            }

            if (_lastAppliedPlayerMuted != _isMuted)
            {
                playbackEngine.SetMute(_isMuted);
                _lastAppliedPlayerMuted = _isMuted;
            }
        }
        catch
        {
            // The playback engine may reject volume changes while it is being replaced or disposed.
        }
    }

    private void ApplyBrightnessToPlayer()
    {
        try
        {
            var playbackEngine = _playbackEngine;
            if (playbackEngine is null || _lastAppliedPlayerBrightness == _brightness)
            {
                return;
            }

            playbackEngine.SetBrightness(_brightness);
            _lastAppliedPlayerBrightness = _brightness;
        }
        catch
        {
            // Brightness adjustment is best-effort while the playback engine is loading or disposing.
        }
    }

    private void ResetAppliedPlayerPreferenceCache()
    {
        _lastAppliedPlayerVolume = null;
        _lastAppliedPlayerBrightness = null;
        _lastAppliedPlayerMuted = null;
    }

    private async Task StopPlaybackEngineSafelyAsync()
    {
        if (_disposed)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var stopTask = Task.Run(StopPlaybackEngineNowSafely);
        var completed = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromMilliseconds(500)));
        if (completed == stopTask)
        {
            await stopTask;
            MpvPlaybackDiagnostics.Write($"mpv-stop-complete elapsedMs={stopwatch.ElapsedMilliseconds}");
            return;
        }

        MpvPlaybackDiagnostics.Write($"player-r4-shutdown-slow stage=stop-media-player elapsedMs={stopwatch.ElapsedMilliseconds}");
    }

    private void StopPlaybackEngineNowSafely()
    {
        try
        {
            _playbackEngine?.Stop();
        }
        catch
        {
            // Stop can race with native teardown on immediate window close.
        }
    }

    private bool ShouldDisplayBufferingState()
    {
        if (_disposed
            || DateTime.UtcNow < _suppressBufferingUntilUtc)
        {
            return false;
        }

        return _isReloadingMedia || _isPlaybackRunning || IsPlaybackEnginePlaying();
    }

    private bool IsPlaybackEnginePlaying()
    {
        try
        {
            return _playbackEngine?.IsPlaying ?? false;
        }
        catch
        {
            return false;
        }
    }

    private void DispatchBufferingState(double percent)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetBufferingState(percent));
            return;
        }

        SetBufferingState(percent);
    }

    private void DispatchStartupLoadingState()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(SetStartupLoadingState);
            return;
        }

        SetStartupLoadingState();
    }

    private void DispatchSeekRecoveringState(double percent = 0d)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetSeekRecoveringState(percent));
            return;
        }

        SetSeekRecoveringState(percent);
    }

    private void DispatchWaitingForDataState(string reason)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetWaitingForDataState(reason));
            return;
        }

        SetWaitingForDataState(reason);
    }

    private void UpdateSeekState(double targetSeconds, double startedFromSeconds)
    {
        if (_disposed || !_isMpvMediaLoaded)
        {
            return;
        }

        if (_isPlaybackRunning || IsPlaybackEnginePlaying())
        {
            BeginSeekRecovery(targetSeconds, startedFromSeconds);
            return;
        }

        _pausedSeekDisplaySeconds = targetSeconds;
        _holdPausedSeekDisplayUntilUtc = DateTime.UtcNow.AddSeconds(2);
        SuppressShortBufferingNoise();
        ClearSeekRecovery(resetBuffering: true);
    }

    private void BeginSeekRecovery(double targetSeconds, double startedFromSeconds)
    {
        _isSeekInProgress = true;
        _seekStartedAtUtc = DateTime.UtcNow;
        _seekTargetSeconds = Math.Max(0d, targetSeconds);
        _seekStartedFromSeconds = Math.Max(0d, startedFromSeconds);
        _lastObservedPlaybackSecondsAfterSeek = -1d;
        SetSeekRecoveringState();
        _ = ClearSeekRecoveryAfterTimeoutAsync(_seekStartedAtUtc);
    }

    private void UpdateSeekRecoveryProgress(double playbackSeconds)
    {
        if (!_isSeekInProgress)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _seekStartedAtUtc >= TimeSpan.FromSeconds(10))
        {
            HandleSeekRecoveryTimeout(_seekStartedAtUtc);
            return;
        }

        if (!IsAtSeekTarget(playbackSeconds))
        {
            return;
        }

        if (_lastObservedPlaybackSecondsAfterSeek < 0d)
        {
            _lastObservedPlaybackSecondsAfterSeek = playbackSeconds;
            return;
        }

        if (playbackSeconds > _lastObservedPlaybackSecondsAfterSeek + 0.25d)
        {
            ClearSeekRecovery(resetBuffering: true);
            return;
        }

        _lastObservedPlaybackSecondsAfterSeek = playbackSeconds;
    }

    private bool IsAtSeekTarget(double playbackSeconds)
    {
        return _seekTargetSeconds >= _seekStartedFromSeconds
            ? playbackSeconds >= _seekTargetSeconds - 2d
            : playbackSeconds <= _seekTargetSeconds + 2d;
    }

    private async Task ClearSeekRecoveryAfterTimeoutAsync(DateTime seekStartedAtUtc)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        HandleSeekRecoveryTimeout(seekStartedAtUtc);
    }

    private void HandleSeekRecoveryTimeout(DateTime seekStartedAtUtc)
    {
        if (_disposed || !_isSeekInProgress || _seekStartedAtUtc != seekStartedAtUtc)
        {
            return;
        }

        var elapsedMs = (long)Math.Max(0d, (DateTime.UtcNow - seekStartedAtUtc).TotalMilliseconds);
        MpvPlaybackDiagnostics.Write(
            $"mpv-seek-timeout elapsedMs={elapsedMs} target={_seekTargetSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        _isSeekInProgress = false;
        _lastObservedPlaybackSecondsAfterSeek = -1d;
        if (_playbackEngine?.IsBuffering == true)
        {
            DispatchWaitingForDataState("seek-timeout");
            return;
        }

        DispatchClearBufferingState();
    }

    private void ClearSeekRecovery(bool resetBuffering)
    {
        _isSeekInProgress = false;
        _lastObservedPlaybackSecondsAfterSeek = -1d;
        if (resetBuffering)
        {
            DispatchClearBufferingState();
        }

        DispatchMainPlaybackUiState(_isStopped ? MainPlaybackUiState.Idle : (_isPlaybackRunning ? MainPlaybackUiState.Playing : MainPlaybackUiState.Paused), "seek-recovery-clear");
    }

    private void SuppressShortBufferingNoise()
    {
        _suppressBufferingUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
        if (!_isSeekInProgress)
        {
            DispatchClearBufferingState();
        }
    }

    private void SuppressAutomaticTrackSelection()
    {
        _suppressAutomaticTrackSelectionUntilUtc = DateTime.UtcNow.AddSeconds(3);
    }

    private void SetMainPlaybackUiState(MainPlaybackUiState state, string reason, string? errorText = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetMainPlaybackUiState(state, reason, errorText));
            return;
        }

        if (_disposed && state != MainPlaybackUiState.Closing)
        {
            return;
        }

        if (_mainPlaybackUiState == MainPlaybackUiState.Closing && state != MainPlaybackUiState.Closing)
        {
            MpvPlaybackDiagnostics.Write(
                $"player-ui-state-skip current=Closing requested={state} reason={SanitizeLogToken(reason)}");
            return;
        }

        if (ShouldHoldStartupUiState(state))
        {
            MpvPlaybackDiagnostics.Write(
                $"player-ui-state-held current={_mainPlaybackUiState} requested={state} reason={SanitizeLogToken(reason)} gate=awaiting-initial-position");
            RefreshBufferingOverlayProjection($"hold-{reason}");
            return;
        }

        var oldState = _mainPlaybackUiState;
        _mainPlaybackUiState = state;
        _mainPlaybackErrorText = state == MainPlaybackUiState.Error ? errorText : null;
        if (state is MainPlaybackUiState.Error or MainPlaybackUiState.Closing)
        {
            ClearOperationNotice($"{reason}-main-priority");
        }

        RefreshDisplayStatusProjection(reason);
        if (oldState != state)
        {
            MpvPlaybackDiagnostics.Write(
                $"player-ui-state-changed old={oldState} new={state} reason={SanitizeLogToken(reason)}");
        }
    }

    private void DispatchMainPlaybackUiState(MainPlaybackUiState state, string reason, string? errorText = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetMainPlaybackUiState(state, reason, errorText));
            return;
        }

        SetMainPlaybackUiState(state, reason, errorText);
    }

    private bool ShouldHoldStartupUiState(MainPlaybackUiState requestedState)
    {
        if (!_isAwaitingInitialPlaybackProgress)
        {
            return false;
        }

        if (requestedState is not (MainPlaybackUiState.Playing or MainPlaybackUiState.Paused or MainPlaybackUiState.Idle))
        {
            return false;
        }

        return _mainPlaybackUiState is MainPlaybackUiState.Opening
            or MainPlaybackUiState.LoadingMetadata
            or MainPlaybackUiState.Starting
            or MainPlaybackUiState.Buffering
            or MainPlaybackUiState.Recovering;
    }

    private void DispatchOperationNotice(PlayerOperationNoticeKind kind, string text, string reason, TimeSpan? duration = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetOperationNotice(kind, text, reason, duration));
            return;
        }

        SetOperationNotice(kind, text, reason, duration);
    }

    private void SetResumeMessage(string text)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetResumeMessage(text));
            return;
        }

        CancelResumeMessageClear();
        ResumeMessage = text;
    }

    private void ClearResumeMessage()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ClearResumeMessage);
            return;
        }

        CancelResumeMessageClear();
        ResumeMessage = string.Empty;
    }

    private void ScheduleResumeMessageClearAfterPlaybackStarted()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ScheduleResumeMessageClearAfterPlaybackStarted);
            return;
        }

        if (_disposed || string.IsNullOrWhiteSpace(ResumeMessage))
        {
            return;
        }

        var previousCts = _resumeMessageClearCts;
        previousCts?.Cancel();

        var cts = new CancellationTokenSource();
        _resumeMessageClearCts = cts;
        _ = ClearResumeMessageAfterDelayAsync(cts);
    }

    private async Task ClearResumeMessageAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(ResumeMessageDisplayDuration, cts.Token);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() => ClearResumeMessageFromDelay(cts));
                return;
            }

            ClearResumeMessageFromDelay(cts);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_resumeMessageClearCts, cts))
            {
                _resumeMessageClearCts = null;
            }

            cts.Dispose();
        }
    }

    private void ClearResumeMessageFromDelay(CancellationTokenSource cts)
    {
        if (!_disposed && ReferenceEquals(_resumeMessageClearCts, cts))
        {
            ResumeMessage = string.Empty;
        }
    }

    private void CancelResumeMessageClear()
    {
        _resumeMessageClearCts?.Cancel();
    }

    private void RefreshDisplayStatusProjection(string reason)
    {
        DisplayStatusText = ResolveDisplayStatusText();
        StatusMessage = DisplayStatusText;
        RefreshBufferingOverlayProjection(reason);
    }

    private string ResolveDisplayStatusText()
    {
        return _mainPlaybackUiState switch
        {
            MainPlaybackUiState.Closing => "\u6b63\u5728\u5173\u95ed\u64ad\u653e\u5668...",
            MainPlaybackUiState.Error => string.IsNullOrWhiteSpace(_mainPlaybackErrorText) ? "\u64ad\u653e\u5931\u8d25" : _mainPlaybackErrorText,
            MainPlaybackUiState.Opening => "\u6b63\u5728\u6253\u5f00\u5a92\u4f53...",
            MainPlaybackUiState.LoadingMetadata => "\u6b63\u5728\u8bfb\u53d6\u5a92\u4f53\u4fe1\u606f...",
            MainPlaybackUiState.Starting => "\u6b63\u5728\u542f\u52a8\u64ad\u653e...",
            MainPlaybackUiState.Seeking => SelectedSource is null ? "\u6b63\u5728\u64ad\u653e" : $"\u6b63\u5728\u64ad\u653e\uff1a{SelectedSource.FileName}",
            MainPlaybackUiState.Buffering => SelectedSource is null ? "\u6b63\u5728\u64ad\u653e" : $"\u6b63\u5728\u64ad\u653e\uff1a{SelectedSource.FileName}",
            MainPlaybackUiState.Recovering => "\u6b63\u5728\u6062\u590d\u64ad\u653e...",
            MainPlaybackUiState.Paused => "\u5df2\u6682\u505c",
            MainPlaybackUiState.Playing => SelectedSource is null ? "\u6b63\u5728\u64ad\u653e" : $"\u6b63\u5728\u64ad\u653e\uff1a{SelectedSource.FileName}",
            _ => _isStopped ? "\u5df2\u505c\u6b62" : "\u51c6\u5907\u64ad\u653e"
        };
    }

    private void SetOperationNotice(PlayerOperationNoticeKind kind, string text, string reason, TimeSpan? duration = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetOperationNotice(kind, text, reason, duration));
            return;
        }

        if (_disposed
            || _mainPlaybackUiState is MainPlaybackUiState.Error or MainPlaybackUiState.Closing
            || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _operationNoticeKind = kind;
        DisplayNoticeText = text;
        IsNoticeVisible = true;
        var version = Interlocked.Increment(ref _operationNoticeVersion);
        MpvPlaybackDiagnostics.Write(
            $"player-ui-notice-shown kind={kind} reason={SanitizeLogToken(reason)}");
        _ = ClearOperationNoticeAfterDelayAsync(version, duration ?? TimeSpan.FromSeconds(3));
    }

    private void ClearOperationNotice(string reason)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ClearOperationNotice(reason));
            return;
        }

        if (_operationNoticeKind == PlayerOperationNoticeKind.None
            && !IsNoticeVisible
            && string.IsNullOrEmpty(DisplayNoticeText))
        {
            return;
        }

        _operationNoticeKind = PlayerOperationNoticeKind.None;
        DisplayNoticeText = string.Empty;
        IsNoticeVisible = false;
        Interlocked.Increment(ref _operationNoticeVersion);
        MpvPlaybackDiagnostics.Write(
            $"player-ui-notice-cleared reason={SanitizeLogToken(reason)}");
    }

    private async Task ClearOperationNoticeAfterDelayAsync(int version, TimeSpan delay)
    {
        await Task.Delay(delay);
        if (_disposed
            || version != _operationNoticeVersion
            || _mainPlaybackUiState is MainPlaybackUiState.Error or MainPlaybackUiState.Closing)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() =>
            {
                if (version == _operationNoticeVersion)
                {
                    ClearOperationNotice("expired");
                }
            });
            return;
        }

        ClearOperationNotice("expired");
    }

    private void RefreshBufferingOverlayProjection(string reason)
    {
        var visible = !_disposed
            && _mainPlaybackUiState is MainPlaybackUiState.Opening
                or MainPlaybackUiState.LoadingMetadata
                or MainPlaybackUiState.Starting
                or MainPlaybackUiState.Seeking
                or MainPlaybackUiState.Buffering
                or MainPlaybackUiState.Recovering;
        var text = visible ? ResolveBufferingOverlayText() : "\u7f13\u51b2\u4e2d...";
        if (BufferingOverlayText != text)
        {
            BufferingOverlayText = text;
        }

        if (IsBufferingOverlayVisible != visible)
        {
            IsBufferingOverlayVisible = visible;
            MpvPlaybackDiagnostics.Write(
                $"player-ui-buffering-overlay visible={visible.ToString().ToLowerInvariant()} reason={SanitizeLogToken(reason)}");
        }
    }

    private string ResolveBufferingOverlayText()
    {
        return _mainPlaybackUiState switch
        {
            MainPlaybackUiState.Opening => "\u6b63\u5728\u6253\u5f00\u5a92\u4f53...",
            MainPlaybackUiState.LoadingMetadata => "\u6b63\u5728\u8bfb\u53d6\u5a92\u4f53\u4fe1\u606f...",
            MainPlaybackUiState.Starting => "\u6b63\u5728\u51c6\u5907\u64ad\u653e...",
            MainPlaybackUiState.Seeking => "\u6b63\u5728\u8df3\u8f6c...",
            MainPlaybackUiState.Recovering => "\u6b63\u5728\u6062\u590d\u64ad\u653e...",
            MainPlaybackUiState.Buffering when BufferingPercent > 0d && BufferingPercent < 99.5d => $"\u7f13\u51b2\u4e2d {BufferingPercent:0}%",
            _ => "\u7f13\u51b2\u4e2d..."
        };
    }

    private static string SanitizeLogToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Replace(' ', '-').Replace('\r', '-').Replace('\n', '-');
    }

    private int SetSeekRecoveringState(double percent = 0d)
    {
        if (_disposed)
        {
            return _bufferingStateVersion;
        }

        var normalizedPercent = Math.Clamp(percent, 0d, 99d);
        BufferingPercent = normalizedPercent;
        IsBuffering = true;
        BufferingStatusText = "\u7f13\u51b2\u4e2d...";
        SetMainPlaybackUiState(MainPlaybackUiState.Seeking, "seek-buffering");
        LogBufferingUiState("seek", "seek-buffering", normalizedPercent, "seek");
        return Interlocked.Increment(ref _bufferingStateVersion);
    }

    private int SetStartupLoadingState()
    {
        if (_disposed)
        {
            return _bufferingStateVersion;
        }

        BufferingPercent = 0d;
        IsBuffering = true;
        BufferingStatusText = "\u6b63\u5728\u51c6\u5907\u64ad\u653e...";
        SetMainPlaybackUiState(MainPlaybackUiState.Starting, "startup-loading");
        LogBufferingUiState("startup", "startup", 0d, "opening");
        return Interlocked.Increment(ref _bufferingStateVersion);
    }

    private int SetBufferingState(double percent)
    {
        if (_disposed)
        {
            return _bufferingStateVersion;
        }

        var normalizedPercent = Math.Clamp(percent, 0d, 100d);
        if (IsBuffering
            && normalizedPercent < 100d
            && normalizedPercent < BufferingPercent
            && BufferingPercent < 100d)
        {
            normalizedPercent = BufferingPercent;
        }

        BufferingPercent = normalizedPercent;
        IsBuffering = normalizedPercent < 100d;
        BufferingStatusText = IsBuffering ? $"\u6b63\u5728\u7f13\u51b2 {normalizedPercent:0}%" : string.Empty;
        if (IsBuffering)
        {
            if (_mainPlaybackUiState is MainPlaybackUiState.Opening or MainPlaybackUiState.LoadingMetadata or MainPlaybackUiState.Starting or MainPlaybackUiState.Recovering)
            {
                RefreshBufferingOverlayProjection("buffering-during-startup");
            }
            else
            {
                SetMainPlaybackUiState(MainPlaybackUiState.Buffering, "buffering");
            }
        }
        else if (_mainPlaybackUiState == MainPlaybackUiState.Buffering)
        {
            SetMainPlaybackUiState(_isStopped ? MainPlaybackUiState.Idle : (_isPlaybackRunning ? MainPlaybackUiState.Playing : MainPlaybackUiState.Paused), "buffering-clear");
        }
        else
        {
            RefreshBufferingOverlayProjection("buffering-clear");
        }

        LogBufferingUiState(IsBuffering ? "network" : "clear", "network", normalizedPercent, "mpv-buffering");
        var version = Interlocked.Increment(ref _bufferingStateVersion);
        if (IsBuffering)
        {
            _ = ClearTransientBufferingStateAsync(version);
        }

        return version;
    }

    private int SetWaitingForDataState(string reason)
    {
        if (_disposed)
        {
            return _bufferingStateVersion;
        }

        IsBuffering = true;
        BufferingPercent = 0d;
        BufferingStatusText = "\u6b63\u5728\u7b49\u5f85\u6570\u636e...";
        SetMainPlaybackUiState(MainPlaybackUiState.Buffering, $"waiting-data-{reason}");
        LogBufferingUiState("network", "waiting-data", null, reason);
        var version = Interlocked.Increment(ref _bufferingStateVersion);
        _ = ClearTransientBufferingStateAsync(version);
        return version;
    }

    private static void LogBufferingUiState(string state, string textKind, double? percent, string reason)
    {
        MpvPlaybackDiagnostics.Write(
            $"mpv-buffering-ui state={state} textKind={textKind} percent={(percent.HasValue ? percent.Value.ToString("0.#", CultureInfo.InvariantCulture) : "unknown")} reason={reason}");
    }

    private void ClearBufferingState()
    {
        SetBufferingState(100d);
    }

    private void ResetBufferingState()
    {
        if (_disposed)
        {
            return;
        }

        _isSeekInProgress = false;
        _lastObservedPlaybackSecondsAfterSeek = -1d;
        BufferingPercent = 0d;
        IsBuffering = false;
        BufferingStatusText = string.Empty;
        if (_mainPlaybackUiState == MainPlaybackUiState.Buffering)
        {
            SetMainPlaybackUiState(_isStopped ? MainPlaybackUiState.Idle : (_isPlaybackRunning ? MainPlaybackUiState.Playing : MainPlaybackUiState.Paused), "buffering-reset");
        }
        else
        {
            RefreshBufferingOverlayProjection("buffering-reset");
        }

        Interlocked.Increment(ref _bufferingStateVersion);
    }

    private async Task ClearTransientBufferingStateAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));
        if (_disposed
            || _isSeekInProgress
            || version != _bufferingStateVersion
            || !IsBuffering
            || (_playbackEngine is not null && _playbackEngine.IsBuffering))
        {
            return;
        }

        ClearBufferingState();
    }

    private void DispatchClearBufferingState()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ClearBufferingState);
            return;
        }

        ClearBufferingState();
    }

    private void DispatchResetBufferingState()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(ResetBufferingState);
            return;
        }

        ResetBufferingState();
    }

    private void DispatchPlaybackState(bool isPlaying)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => SetPlaybackState(isPlaying));
            return;
        }

        SetPlaybackState(isPlaying);
    }

    private void DispatchStoppedState()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(SetStoppedState);
            return;
        }

        SetStoppedState();
    }

    private void SetStoppedState()
    {
        _isStopped = true;
        _isAwaitingInitialPlaybackProgress = false;
        ClearResumeMessage();
        _timer.Stop();
        if (_mainPlaybackUiState != MainPlaybackUiState.Closing)
        {
            SetMainPlaybackUiState(MainPlaybackUiState.Idle, "stopped");
        }
    }

    private void DispatchStatusMessage(string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => StatusMessage = message);
            return;
        }

        StatusMessage = message;
    }

    private void SetPlaybackState(bool isPlaying)
    {
        if (_disposed)
        {
            return;
        }

        _isPlaybackRunning = isPlaying;
        if (isPlaying)
        {
            _isStopped = false;
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        PlayPauseText = isPlaying ? "\u6682\u505c" : "\u64ad\u653e";
        if (_mainPlaybackUiState is not MainPlaybackUiState.Buffering
            and not MainPlaybackUiState.Seeking
            and not MainPlaybackUiState.Opening
            and not MainPlaybackUiState.LoadingMetadata
            and not MainPlaybackUiState.Starting
            and not MainPlaybackUiState.Recovering
            and not MainPlaybackUiState.Error
            and not MainPlaybackUiState.Closing)
        {
            SetMainPlaybackUiState(isPlaying ? MainPlaybackUiState.Playing : (_isStopped ? MainPlaybackUiState.Idle : MainPlaybackUiState.Paused), "playback-state");
        }

        MpvPlaybackDiagnostics.Write($"player-ui-playback-state state={(isPlaying ? "playing" : "paused")} reason=set-playback-state");
    }

    private PlaybackSubtitleItem? FindMatchingSubtitle(PlaybackSubtitleItem subtitle)
    {
        var uniqueKey = EnsureSubtitleUniqueKey(subtitle);
        return subtitle.Type switch
        {
            PlaybackSubtitleType.None => _noneSubtitleItem,
            PlaybackSubtitleType.EmbeddedTrack => EmbeddedSubtitles.FirstOrDefault(
                x => string.Equals(EnsureSubtitleUniqueKey(x), uniqueKey, StringComparison.OrdinalIgnoreCase)),
            PlaybackSubtitleType.ExternalFile => ExternalSubtitles.FirstOrDefault(
                x => string.Equals(EnsureSubtitleUniqueKey(x), uniqueKey, StringComparison.OrdinalIgnoreCase))
                ?? _onlinePlaybackSubtitles.FirstOrDefault(
                    x => string.Equals(EnsureSubtitleUniqueKey(x), uniqueKey, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private PlaybackAudioTrackItem? FindMatchingAudioTrack(PlaybackAudioTrackItem audioTrack)
    {
        return AudioTracks.FirstOrDefault(x => x.TrackId == audioTrack.TrackId)
               ?? AudioTracks.FirstOrDefault(x => string.Equals(
                   NormalizeAudioTrackName(x.DisplayName, x.Priority),
                   NormalizeAudioTrackName(audioTrack.DisplayName, audioTrack.Priority),
                   StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameSubtitle(PlaybackSubtitleItem left, PlaybackSubtitleItem right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        return string.Equals(
            EnsureSubtitleUniqueKey(left),
            EnsureSubtitleUniqueKey(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameAudioTrack(PlaybackAudioTrackItem? left, PlaybackAudioTrackItem? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.TrackId == right.TrackId;
    }

    private void SetSelectedSubtitleSilently(PlaybackSubtitleItem subtitle)
    {
        _suppressSubtitleSelection = true;
        try
        {
            _selectedSubtitle = subtitle;
            OnPropertyChanged(nameof(SelectedSubtitle));
            OnPropertyChanged(nameof(SubtitleButtonText));
            OnPropertyChanged(nameof(SubtitleButtonToolTip));
        }
        finally
        {
            _suppressSubtitleSelection = false;
        }
    }

    private void SetSelectedAudioTrackSilently(PlaybackAudioTrackItem? audioTrack)
    {
        _suppressAudioTrackSelection = true;
        try
        {
            foreach (var item in AudioTracks)
            {
                item.IsSelected = audioTrack is not null && item.TrackId == audioTrack.TrackId;
            }

            _selectedAudioTrack = audioTrack;
            OnPropertyChanged(nameof(SelectedAudioTrack));
            OnPropertyChanged(nameof(AudioTrackButtonText));
            OnPropertyChanged(nameof(AudioTrackButtonToolTip));
        }
        finally
        {
            _suppressAudioTrackSelection = false;
        }
    }

    private void SyncSelectedAudioTrackToCurrent()
    {
        var currentTrackId = SafeGetCurrentAudioTrackId();
        var currentTrack = AudioTracks.FirstOrDefault(x => x.TrackId == currentTrackId)
                           ?? AudioTracks.FirstOrDefault();
        _appliedAudioTrack = currentTrack;
        SetSelectedAudioTrackSilently(currentTrack);
    }

    private static IReadOnlyList<PlaybackAudioTrackItem> PrepareAudioTrackDisplays(
        IReadOnlyList<PlaybackAudioTrackItem> tracks)
    {
        var result = new List<PlaybackAudioTrackItem>(tracks.Count);
        var index = 0;
        foreach (var track in tracks)
        {
            index++;
            var originalName = string.IsNullOrWhiteSpace(track.OriginalName)
                ? track.DisplayName
                : track.OriginalName;
            if (string.IsNullOrWhiteSpace(originalName))
            {
                originalName = $"\u97f3\u8f68 {index}";
            }

            var displayName = BuildAudioTrackSummary(originalName, index);
            result.Add(new PlaybackAudioTrackItem
            {
                DisplayName = displayName,
                OriginalName = originalName,
                TooltipText = BuildAudioTrackTooltip(originalName, displayName, track.TrackId),
                TrackId = track.TrackId,
                IsSelected = track.IsSelected,
                Priority = index
            });
        }

        return result;
    }

    private static IReadOnlyList<PlaybackSubtitleItem> PrepareEmbeddedSubtitleDisplays(
        IReadOnlyList<PlaybackSubtitleItem> tracks,
        string? sourceFileName)
    {
        var result = new List<PlaybackSubtitleItem>(tracks.Count);
        var index = 0;
        foreach (var track in tracks.OrderBy(x => x.TrackId ?? int.MaxValue).ThenBy(x => x.Priority))
        {
            if (track.Type != PlaybackSubtitleType.EmbeddedTrack || !track.TrackId.HasValue)
            {
                continue;
            }

            index++;
            var item = CloneSubtitleTrack(track);
            item.Type = PlaybackSubtitleType.EmbeddedTrack;
            item.Priority = index;
            item.FileName = string.IsNullOrWhiteSpace(item.FileName)
                ? item.OriginalName
                : item.FileName;
            PrepareSubtitleDisplay(item, index, sourceFileName);
            result.Add(item);
        }

        DisambiguateDuplicateEmbeddedSubtitleSummaries(result);
        return result;
    }

    private static void PrepareSubtitleDisplay(
        PlaybackSubtitleItem subtitle,
        int fallbackIndex,
        string? sourceFileName = null)
    {
        var originalName = ResolveSubtitleOriginalName(subtitle, fallbackIndex);
        subtitle.OriginalName = originalName;
        subtitle.UniqueKey = BuildSubtitleUniqueKey(subtitle);
        subtitle.DisplayName = BuildSubtitleSummary(subtitle, fallbackIndex, originalName, sourceFileName);
        subtitle.TooltipText = BuildSubtitleTooltip(subtitle, originalName);
    }

    private static string BuildSubtitleUniqueKey(PlaybackSubtitleItem subtitle)
    {
        return subtitle.Type switch
        {
            PlaybackSubtitleType.None => "none",
            PlaybackSubtitleType.EmbeddedTrack => subtitle.TrackId.HasValue
                ? $"embedded:{subtitle.TrackId.Value}"
                : $"embedded-name:{NormalizeSubtitleName(subtitle.FileName)}",
            PlaybackSubtitleType.ExternalFile => subtitle.BindingId.HasValue
                ? $"external:binding:{subtitle.BindingId.Value}"
                : subtitle.SubtitleMediaFileId > 0
                    ? $"external:media:{subtitle.SubtitleMediaFileId}"
                    : $"external:path:{NormalizeExternalSubtitleKey(subtitle.PlaybackUrl, subtitle.FilePath, subtitle.FileName)}",
            _ => string.Empty
        };
    }

    private static string NormalizeExternalSubtitleKey(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Replace('\\', '/').ToLowerInvariant();
            }
        }

        return string.Empty;
    }

    private static string EnsureSubtitleUniqueKey(PlaybackSubtitleItem subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle.UniqueKey))
        {
            subtitle.UniqueKey = BuildSubtitleUniqueKey(subtitle);
        }

        return subtitle.UniqueKey;
    }

    private static string ResolveSubtitleOriginalName(PlaybackSubtitleItem subtitle, int fallbackIndex)
    {
        if (!string.IsNullOrWhiteSpace(subtitle.FileName))
        {
            return subtitle.FileName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(subtitle.OriginalName))
        {
            return subtitle.OriginalName.Trim();
        }

        var displayName = RemoveKnownPrefix(subtitle.DisplayName);
        return string.IsNullOrWhiteSpace(displayName)
            ? $"{GetSubtitleFallbackCore(subtitle.Type)} {Math.Max(1, fallbackIndex)}"
            : displayName.Trim();
    }

    private static string BuildSubtitleSummary(
        PlaybackSubtitleItem subtitle,
        int fallbackIndex,
        string originalName,
        string? sourceFileName)
    {
        if (subtitle.Type == PlaybackSubtitleType.None)
        {
            return NoSubtitleText;
        }

        var languageSummary = ResolveSubtitleLanguageSummary(originalName);
        var modifiers = ResolveSubtitleModifiers(originalName);
        var format = ResolveSubtitleFormat(subtitle, originalName);
        if (!string.IsNullOrWhiteSpace(languageSummary) || modifiers.Count > 0)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(languageSummary))
            {
                parts.Add(languageSummary);
            }

            parts.AddRange(modifiers);
            return AppendSubtitleFormat(string.Join(" \u00b7 ", parts), format);
        }

        if (subtitle.Type == PlaybackSubtitleType.ExternalFile)
        {
            if (IsSameNameExternalSubtitle(subtitle, originalName, sourceFileName))
            {
                return AppendSubtitleFormat("\u540c\u540d", format);
            }

            var cleanName = CleanSubtitleFileNameForDisplay(originalName);
            if (!string.IsNullOrWhiteSpace(cleanName))
            {
                return AppendSubtitleFormat(cleanName, format);
            }
        }

        var fallback = $"{GetSubtitleFallbackCore(subtitle.Type)} {Math.Max(1, fallbackIndex)}";
        return AppendSubtitleFormat(fallback, format);
    }

    private static string AppendSubtitleFormat(string core, string format)
    {
        return string.IsNullOrWhiteSpace(format) ? core : $"{core} {format}";
    }

    private static void DisambiguateDuplicateEmbeddedSubtitleSummaries(IReadOnlyList<PlaybackSubtitleItem> tracks)
    {
        foreach (var group in tracks
                     .Where(x => x.Type == PlaybackSubtitleType.EmbeddedTrack)
                     .GroupBy(x => x.DisplayName)
                     .Where(x => x.Count() > 1))
        {
            var sequence = 1;
            foreach (var track in group
                         .OrderBy(x => x.Priority)
                         .ThenBy(x => x.TrackId ?? int.MaxValue))
            {
                track.DisplayName = $"{track.DisplayName}\uff08{sequence++}\uff09";
                track.TooltipText = BuildSubtitleTooltip(track, track.OriginalName);
            }
        }
    }

    private static string BuildSubtitleTooltip(PlaybackSubtitleItem subtitle, string originalName)
    {
        if (subtitle.Type == PlaybackSubtitleType.None)
        {
            return "\u5b57\u5e55\u7c7b\u578b\uff1a\u65e0\r\nUniqueKey\uff1anone";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"\u5b57\u5e55\u7c7b\u578b\uff1a{GetSubtitleTypeText(subtitle.Type)}");
        AppendTooltipLine(builder, "UniqueKey", subtitle.UniqueKey);
        AppendTooltipLine(builder, "\u663e\u793a\u6458\u8981", subtitle.DisplayName);
        AppendTooltipLine(builder, "\u8bed\u8a00\u6458\u8981", ResolveSubtitleLanguageSummary(originalName));
        AppendTooltipLine(builder, "\u539f\u59cb\u540d\u79f0", originalName);
        AppendTooltipLine(builder, "\u5b8c\u6574\u6587\u4ef6\u540d", subtitle.FileName);
        AppendTooltipLine(builder, "\u5b8c\u6574\u8def\u5f84", subtitle.FilePath);
        AppendTooltipLine(builder, "URL", subtitle.PlaybackUrl);

        if (subtitle.MediaFileId.HasValue)
        {
            builder.AppendLine($"MediaFileId\uff1a{subtitle.MediaFileId.Value}");
        }

        if (subtitle.BindingId.HasValue)
        {
            builder.AppendLine($"BindingId\uff1a{subtitle.BindingId.Value}");
        }

        if (subtitle.TrackId.HasValue)
        {
            builder.AppendLine($"TrackId\uff1a{subtitle.TrackId.Value}");
        }

        if (subtitle.MatchType != SubtitleMatchType.Unknown)
        {
            builder.AppendLine($"\u5339\u914d\u65b9\u5f0f\uff1a{GetSubtitleMatchText(subtitle.MatchType)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildAudioTrackSummary(string originalName, int fallbackIndex)
    {
        var language = ResolveAudioLanguageSummary(originalName);
        var codec = ResolveAudioCodecSummary(originalName);
        var channels = ResolveAudioChannelSummary(originalName);
        var parts = new List<string>
        {
            string.IsNullOrWhiteSpace(language) ? $"\u97f3\u8f68 {Math.Max(1, fallbackIndex)}" : language
        };

        if (!string.IsNullOrWhiteSpace(codec))
        {
            parts.Add(codec);
        }

        if (!string.IsNullOrWhiteSpace(channels))
        {
            parts.Add(channels);
        }

        return string.Join(" \u00b7 ", parts);
    }

    private static string BuildAudioTrackTooltip(string originalName, string displayName, int trackId)
    {
        var builder = new StringBuilder();
        AppendTooltipLine(builder, "\u539f\u59cb\u97f3\u8f68\u540d\u79f0", originalName);
        builder.AppendLine($"TrackId\uff1a{trackId}");
        AppendTooltipLine(builder, "\u663e\u793a\u6458\u8981", displayName);
        AppendTooltipLine(builder, "\u8bed\u8a00", ResolveAudioLanguageSummary(originalName));
        AppendTooltipLine(builder, "\u7f16\u7801", ResolveAudioCodecSummary(originalName));
        AppendTooltipLine(builder, "\u58f0\u9053", ResolveAudioChannelSummary(originalName));
        return builder.ToString().TrimEnd();
    }

    private static string ResolveSubtitleLanguageSummary(string value)
    {
        if (HasKeyword(value, "\u7b80\u82f1", "\u7b80\u82f1\u53cc\u8bed", "\u7b80\u82f1\u53cc\u5b57", "\u7b80\u82f1\u5b57\u5e55", "\u7b80\u82f1\u7279\u6548")
            || HasKeyword(value, "chs&eng", "chs-eng", "chs_eng", "sc-eng", "zh-cn-eng", "zh_cn_eng"))
        {
            return "\u7b80\u82f1\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u7e41\u82f1", "\u7e41\u82f1\u53cc\u8bed", "\u7e41\u82f1\u53cc\u5b57")
            || HasKeyword(value, "cht&eng", "cht-eng", "tc-eng", "zh-tw-eng", "zh_tw_eng"))
        {
            return "\u7e41\u82f1\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u4e2d\u82f1", "\u4e2d\u82f1\u53cc\u8bed", "\u4e2d\u82f1\u53cc\u5b57", "\u4e2d\u82f1\u53cc\u5b57\u5e55")
            || HasKeyword(value, "chinese-english", "chinese_eng", "chinese english"))
        {
            return "\u4e2d\u82f1\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u7b80\u65e5") || HasKeyword(value, "chs&jpn", "zh-cn-jpn", "zh-cn-ja"))
        {
            return "\u7b80\u65e5\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u7e41\u65e5") || HasKeyword(value, "cht&jpn", "zh-tw-jpn", "zh-tw-ja"))
        {
            return "\u7e41\u65e5\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u4e2d\u65e5", "\u65e5\u4e2d") || HasKeyword(value, "zh-ja", "zh-jp", "japanese-chinese"))
        {
            return "\u4e2d\u65e5\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u7b80\u97e9") || HasKeyword(value, "chs&kor", "zh-cn-kor"))
        {
            return "\u7b80\u97e9\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u7e41\u97e9") || HasKeyword(value, "cht&kor", "zh-tw-kor"))
        {
            return "\u7e41\u97e9\u53cc\u8bed";
        }

        if (HasKeyword(value, "\u4e2d\u97e9", "\u97e9\u4e2d") || HasKeyword(value, "zh-ko", "korean-chinese"))
        {
            return "\u4e2d\u97e9\u53cc\u8bed";
        }

        var simplified = HasKeyword(
            value,
            "chs",
            "sc",
            "zh-cn",
            "zh_cn",
            "simplified",
            "simplified chinese",
            "\u7b80\u4f53",
            "\u7c21\u9ad4",
            "\u7b80\u4e2d",
            "\u7c21\u4e2d",
            "\u7b80\u4f53\u4e2d\u6587",
            "\u7c21\u9ad4\u4e2d\u6587",
            "gb",
            "gbk",
            "gb2312");
        var traditional = HasKeyword(
            value,
            "cht",
            "tc",
            "zh-tw",
            "zh_tw",
            "traditional",
            "traditional chinese",
            "\u7e41\u4f53",
            "\u7e41\u9ad4",
            "\u7e41\u4e2d",
            "\u7e41\u4f53\u4e2d\u6587",
            "\u7e41\u9ad4\u4e2d\u6587",
            "\u6b63\u4f53",
            "\u6b63\u9ad4",
            "\u6b63\u4f53\u4e2d\u6587",
            "\u6b63\u9ad4\u4e2d\u6587",
            "big5");
        var genericChinese = HasKeyword(value, "chinese", "zho", "chi", "\u4e2d\u6587", "\u6c49\u8bed", "\u83ef\u8a9e", "\u534e\u8bed")
                             || (HasKeyword(value, "zh") && !HasUnknownLanguageMarker(value, "zh"));
        var chinese = simplified || traditional || genericChinese;
        var english = HasKeyword(value, "eng", "en", "english", "\u82f1\u6587", "\u82f1\u8bed");
        var japanese = HasKeyword(value, "jpn", "ja", "japanese", "\u65e5\u6587", "\u65e5\u8bed");
        var korean = HasKeyword(value, "kor", "ko", "korean", "\u97e9\u6587", "\u97e9\u8bed");

        if (chinese && english)
        {
            if (simplified)
            {
                return "\u7b80\u82f1\u53cc\u8bed";
            }

            if (traditional)
            {
                return "\u7e41\u82f1\u53cc\u8bed";
            }

            return "\u4e2d\u82f1\u53cc\u8bed";
        }

        if (chinese && japanese)
        {
            if (simplified)
            {
                return "\u7b80\u65e5\u53cc\u8bed";
            }

            if (traditional)
            {
                return "\u7e41\u65e5\u53cc\u8bed";
            }

            return "\u4e2d\u65e5\u53cc\u8bed";
        }

        if (chinese && korean)
        {
            if (simplified)
            {
                return "\u7b80\u97e9\u53cc\u8bed";
            }

            if (traditional)
            {
                return "\u7e41\u97e9\u53cc\u8bed";
            }

            return "\u4e2d\u97e9\u53cc\u8bed";
        }

        if (simplified)
        {
            return "\u7b80\u4e2d";
        }

        if (traditional)
        {
            return "\u7e41\u4e2d";
        }

        if (genericChinese)
        {
            return "\u4e2d\u6587";
        }

        if (english)
        {
            return "\u82f1\u6587";
        }

        if (japanese)
        {
            return "\u65e5\u6587";
        }

        return korean ? "\u97e9\u6587" : string.Empty;
    }

    private static IReadOnlyList<string> ResolveSubtitleModifiers(string value)
    {
        var modifiers = new List<string>();
        if (HasKeyword(value, "\u7279\u6548", "\u7279\u6548\u5b57\u5e55", "\u7279\u6548\u53cc\u8bed", "\u7279\u6548\u53cc\u5b57", "effect", "effects", "styled", "style", "karaoke"))
        {
            modifiers.Add("\u7279\u6548");
        }

        if (HasKeyword(value, "sdh", "cc", "closed caption", "closed captions"))
        {
            modifiers.Add("SDH");
        }
        else if (HasKeyword(value, "hearing impaired", "hi", "\u542c\u969c", "\u542c\u529b\u969c\u788d"))
        {
            modifiers.Add("\u542c\u969c");
        }

        if (HasKeyword(value, "forced", "forced only", "force", "\u5f3a\u5236", "\u5f3a\u5236\u5b57\u5e55", "\u4ec5\u5916\u8bed", "\u5916\u8bed\u5f3a\u5236"))
        {
            modifiers.Add("\u5f3a\u5236");
        }

        if (HasKeyword(value, "signs", "signs & songs", "signs and songs", "songs", "song", "\u6b4c\u66f2", "\u6b4c\u8bcd", "\u6807\u8bc6", "\u6807\u724c"))
        {
            modifiers.Add("\u6807\u8bc6\u6b4c\u66f2");
        }

        if (HasKeyword(value, "commentary", "comment", "comments", "\u8bc4\u8bba", "\u5bfc\u8bc4", "\u5bfc\u6f14\u8bc4\u8bba"))
        {
            modifiers.Add("\u8bc4\u8bba");
        }

        return modifiers;
    }

    private static bool HasDualSubtitleMarker(string value)
    {
        return HasKeyword(value, "\u53cc\u8bed", "\u53cc\u5b57", "\u53cc\u5b57\u5e55", "bilingual", "dual", "dual-sub", "dual subtitle", "dual subtitles")
               || value.Contains('&', StringComparison.Ordinal)
               || value.Contains('-', StringComparison.Ordinal)
               || value.Contains('_', StringComparison.Ordinal);
    }

    private static bool HasUnknownLanguageMarker(string value, string language)
    {
        return Regex.IsMatch(
            NormalizeKeywordSource(value),
            $@"(^|[^a-z0-9]){Regex.Escape(language)}[\s._-]*unknown($|[^a-z0-9])",
            RegexOptions.IgnoreCase);
    }

    private static string ResolveAudioLanguageSummary(string value)
    {
        if (HasKeyword(value, "cantonese", "yue", "\u7ca4\u8bed", "\u5ee3\u6771\u8a71", "\u5e7f\u4e1c\u8bdd"))
        {
            return "\u7ca4\u8bed";
        }

        if (HasKeyword(value, "chinese", "mandarin", "putonghua", "chi", "zho", "zh", "zh-cn", "chs", "\u4e2d\u6587", "\u56fd\u8bed", "\u666e\u901a\u8bdd"))
        {
            return "\u56fd\u8bed";
        }

        if (HasKeyword(value, "english", "eng", "en", "\u82f1\u8bed", "\u82f1\u6587"))
        {
            return "\u82f1\u8bed";
        }

        if (HasKeyword(value, "japanese", "jpn", "ja", "\u65e5\u8bed", "\u65e5\u6587"))
        {
            return "\u65e5\u8bed";
        }

        return HasKeyword(value, "korean", "kor", "ko", "\u97e9\u8bed", "\u97e9\u6587")
            ? "\u97e9\u8bed"
            : string.Empty;
    }

    private static string ResolveAudioCodecSummary(string value)
    {
        var normalized = NormalizeKeywordSource(value);
        var compact = normalized
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        if (compact.Contains("dtshd", StringComparison.OrdinalIgnoreCase))
        {
            return "DTS-HD";
        }

        if (compact.Contains("truehd", StringComparison.OrdinalIgnoreCase))
        {
            return compact.Contains("atmos", StringComparison.OrdinalIgnoreCase) ? "TrueHD Atmos" : "TrueHD";
        }

        if (compact.Contains("eac3", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("e-ac-3", StringComparison.OrdinalIgnoreCase))
        {
            return "EAC3";
        }

        if (HasKeyword(value, "atmos"))
        {
            return "Atmos";
        }

        foreach (var codec in new[] { "aac", "ac3", "dts", "flac", "opus", "mp3" })
        {
            if (HasKeyword(value, codec))
            {
                return codec.ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    private static string ResolveAudioChannelSummary(string value)
    {
        var normalized = NormalizeKeywordSource(value);
        if (Regex.IsMatch(normalized, @"(?<!\d)7[\._\s-]?1(?!\d)", RegexOptions.IgnoreCase)
            || HasKeyword(value, "8ch"))
        {
            return "7.1";
        }

        if (Regex.IsMatch(normalized, @"(?<!\d)5[\._\s-]?1(?!\d)", RegexOptions.IgnoreCase)
            || HasKeyword(value, "6ch"))
        {
            return "5.1";
        }

        if (Regex.IsMatch(normalized, @"(?<!\d)2[\._\s-]?0(?!\d)", RegexOptions.IgnoreCase)
            || HasKeyword(value, "stereo", "2ch"))
        {
            return "2.0";
        }

        return string.Empty;
    }

    private static string ResolveSubtitleFormat(PlaybackSubtitleItem subtitle, string originalName)
    {
        var extension = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = Path.GetExtension(subtitle.FileName);
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = Path.GetExtension(subtitle.FilePath);
        }

        if (string.IsNullOrWhiteSpace(extension)
            && Uri.TryCreate(subtitle.PlaybackUrl, UriKind.Absolute, out var playbackUri))
        {
            extension = Path.GetExtension(Uri.UnescapeDataString(playbackUri.LocalPath));
        }

        return extension.ToLowerInvariant() switch
        {
            ".ass" => "ASS",
            ".ssa" => "SSA",
            ".srt" => "SRT",
            ".vtt" => "VTT",
            ".sub" => "SUB",
            _ => string.Empty
        };
    }

    private static bool IsSameNameExternalSubtitle(
        PlaybackSubtitleItem subtitle,
        string originalName,
        string? sourceFileName)
    {
        if (subtitle.MatchType == SubtitleMatchType.SameName)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            return false;
        }

        var subtitleTokens = GetComparableTitleTokens(originalName);
        var sourceTokens = GetComparableTitleTokens(sourceFileName);
        if (subtitleTokens.Count < 2 || sourceTokens.Count < 2)
        {
            return false;
        }

        var intersectionCount = subtitleTokens.Intersect(sourceTokens, StringComparer.OrdinalIgnoreCase).Count();
        var coverage = intersectionCount / (double)Math.Min(subtitleTokens.Count, sourceTokens.Count);
        return coverage >= 0.75d;
    }

    private static string CleanSubtitleFileNameForDisplay(string originalName)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return string.Empty;
        }

        baseName = StripCompoundSubtitleNoiseSegments(baseName);
        var tokens = Regex.Split(baseName, @"[\.\s\-\[\]\(\)\{\}]+")
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsSubtitleDisplayNoiseToken(x))
            .ToList();
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var result = string.Join(" ", tokens).Trim();
        return HasReadableContent(result) ? MiddleEllipsis(result, 42) : string.Empty;
    }

    private static IReadOnlyList<string> GetComparableTitleTokens(string value)
    {
        var baseName = Path.GetFileNameWithoutExtension(value);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = value;
        }

        baseName = StripCompoundSubtitleNoiseSegments(baseName);
        return Regex.Split(NormalizeKeywordSource(baseName), @"[^a-z0-9\u4e00-\u9fff]+")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsComparableTitleNoiseToken(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSubtitleDisplayNoiseToken(string token)
    {
        var normalized = NormalizeNoiseToken(token);
        return IsComparableTitleNoiseToken(normalized)
               || normalized is "ass" or "ssa" or "srt" or "vtt" or "sub"
               || normalized is "chs" or "cht" or "sc" or "tc" or "eng" or "jpn" or "kor" or "en" or "ja" or "ko"
               || normalized is "zhunknown" or "enunknown" or "unknown";
    }

    private static bool IsComparableTitleNoiseToken(string token)
    {
        var normalized = NormalizeNoiseToken(token);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(19|20)\d{2}$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(2160p|1080p|720p|480p|4k|8k|51|71)$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return normalized is
            "bluray" or "bdrip" or "brrip" or "webdl" or "webrip" or "web" or "remux" or "uhd" or "hdr" or "hdr10" or "dv"
            or "hevc" or "x264" or "x265" or "h264" or "h265" or "avc" or "aac" or "ac3" or "eac3"
            or "dts" or "dtsx" or "dtshd" or "truehd" or "atmos" or "flac" or "opus" or "mp3"
            or "ma" or "ddp" or "proper" or "repack" or "extended" or "director" or "cut";
    }

    private static string StripCompoundSubtitleNoiseSegments(string value)
    {
        var result = value;
        foreach (var pattern in new[]
                 {
                     @"(?<![a-z0-9])web[\s._-]?dl(?![a-z0-9])",
                     @"(?<![a-z0-9])dts[\s._-]?x(?![a-z0-9])",
                     @"(?<![a-z0-9])dts[\s._-]?hd(?![a-z0-9])",
                     @"(?<![a-z0-9])h[\s._-]?264(?![a-z0-9])",
                     @"(?<![a-z0-9])h[\s._-]?265(?![a-z0-9])",
                     @"(?<![a-z0-9])5[\s._-]?1(?![a-z0-9])",
                     @"(?<![a-z0-9])7[\s._-]?1(?![a-z0-9])"
                 })
        {
            result = Regex.Replace(result, pattern, " ", RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static string NormalizeNoiseToken(string token)
    {
        return token
            .Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static bool HasReadableContent(string value)
    {
        return value.Any(char.IsLetterOrDigit) || value.Any(IsCjk);
    }

    private static string MiddleEllipsis(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var prefixLength = Math.Max(8, (maxLength - 1) / 2);
        var suffixLength = Math.Max(8, maxLength - prefixLength - 1);
        return $"{value[..prefixLength]}\u2026{value[^suffixLength..]}";
    }

    private static string GetSubtitleFallbackCore(PlaybackSubtitleType type)
    {
        return "\u5b57\u5e55";
    }

    private static string GetSubtitleTypeText(PlaybackSubtitleType type)
    {
        return type switch
        {
            PlaybackSubtitleType.EmbeddedTrack => "\u5185\u5d4c",
            PlaybackSubtitleType.ExternalFile => "\u5916\u6302",
            _ => "\u65e0"
        };
    }

    private static string GetSubtitleMatchText(SubtitleMatchType matchType)
    {
        return matchType switch
        {
            SubtitleMatchType.SameName => "SameName\uff08\u540c\u540d\uff09",
            SubtitleMatchType.SameFolder => "SameFolder\uff08\u540c\u76ee\u5f55\uff09",
            _ => "\u672a\u77e5"
        };
    }

    private static bool HasKeyword(string value, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeKeywordSource(value);
        var tokens = GetKeywordTokens(normalized);
        foreach (var keyword in keywords)
        {
            var normalizedKeyword = NormalizeKeywordSource(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                continue;
            }

            if (normalizedKeyword.Any(char.IsWhiteSpace)
                || normalizedKeyword.Contains('-', StringComparison.Ordinal)
                || normalizedKeyword.Any(IsCjk))
            {
                if (normalized.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (tokens.Contains(normalizedKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeKeywordSource(string value)
    {
        var normalized = value
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Replace('&', ' ')
            .Trim()
            .ToLowerInvariant();
        return Regex.Replace(normalized, @"\s+", " ");
    }

    private static HashSet<string> GetKeywordTokens(string normalized)
    {
        return Regex.Split(normalized, @"[^a-z0-9\u4e00-\u9fff]+")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCjk(char value)
    {
        return value >= '\u4e00' && value <= '\u9fff';
    }

    private static void AppendTooltipLine(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}\uff1a{value.Trim()}");
        }
    }

    private int SafeGetCurrentAudioTrackId()
    {
        if (_isMpvMediaLoaded && _playbackEngine is not null)
        {
            return _playbackEngine.AudioTracks.FirstOrDefault(x => x.IsSelected)?.TrackId
                   ?? SelectedAudioTrack?.TrackId
                   ?? -1;
        }

        return SelectedAudioTrack?.TrackId ?? -1;
    }

    private static bool IsAutoSubtitle(PlaybackSubtitleItem subtitle)
    {
        return subtitle.IsPreferred || subtitle.IsAuto || subtitle.IsAutoLoaded;
    }

    private static bool IsPreferredChineseSubtitle(PlaybackSubtitleItem subtitle)
    {
        var value = $"{subtitle.DisplayName} {subtitle.FileName}";
        return ContainsIgnoreCase(value, "\u4e2d\u6587")
               || ContainsIgnoreCase(value, "\u7b80\u4f53")
               || ContainsIgnoreCase(value, "\u7b80\u4e2d")
               || ContainsIgnoreCase(value, "Chinese")
               || ContainsIgnoreCase(value, "CHS")
               || ContainsIgnoreCase(value, "zh-CN")
               || ContainsIgnoreCase(value, "zh")
               || ContainsIgnoreCase(value, "Mandarin");
    }

    private static bool IsDisableAudioTrack(string? trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return false;
        }

        return string.Equals(trackName, "Disable", StringComparison.OrdinalIgnoreCase)
               || string.Equals(trackName, "Disabled", StringComparison.OrdinalIgnoreCase)
               || string.Equals(trackName, "None", StringComparison.OrdinalIgnoreCase)
               || string.Equals(trackName, "\u65e0", StringComparison.OrdinalIgnoreCase)
               || string.Equals(trackName, "\u5173\u95ed", StringComparison.OrdinalIgnoreCase)
               || trackName.Contains("Disable audio", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAudioTrackName(string? trackName, int index)
    {
        if (string.IsNullOrWhiteSpace(trackName) || IsDisableAudioTrack(trackName))
        {
            return $"\u97f3\u8f68 {Math.Max(1, index)}";
        }

        return trackName.Trim();
    }

    private static string RemoveKnownPrefix(string value)
    {
        if (value.StartsWith(EmbeddedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[EmbeddedPrefix.Length..];
        }

        if (value.StartsWith(ExternalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[ExternalPrefix.Length..];
        }

        return value;
    }

    private static string NormalizeSubtitleName(string? value)
    {
        return RemoveKnownPrefix(value ?? string.Empty).Trim();
    }

    private static bool ContainsIgnoreCase(string? value, string candidate)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatTime(int seconds)
    {
        if (seconds <= 0)
        {
            return "00:00";
        }

        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static void TracePlayback(string message)
    {
        VideoCacheDiagnostics.Write("PLAYER", message);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SetMainPlaybackUiState(MainPlaybackUiState.Closing, "dispose");
        ClearOperationNotice("dispose");
        ResetBufferingState();
        CancelResumeMessageClear();
        _ = FlushPlayerPreferencesAsync("dispose", TimeSpan.FromMilliseconds(300));
        _disposed = true;
        _timer.Stop();
        _playerPreferencesSaveTimer.Stop();
        MpvPlaybackDiagnostics.Write("player-r4-timer-stopped");
        _videoCacheService.StatusChanged -= OnVideoCacheStatusChanged;
        DisposePlaybackResources("dispose");
    }

    private void DisposePlaybackResources(string reason)
    {
        MpvPlaybackDiagnostics.Write($"player-shutdown-entry reason={reason}");
        MpvPlaybackDiagnostics.Write($"player-r4-shutdown-start reason={reason}");
        var stopwatch = Stopwatch.StartNew();
        var disposeLocked = false;
        try
        {
            disposeLocked = _playbackReloadLock.Wait(TimeSpan.FromMilliseconds(200));
            if (!disposeLocked)
            {
                MpvPlaybackDiagnostics.Write("player-r4-shutdown-skip reason=reload-lock-timeout");
            }
            if (disposeLocked)
            {
                StopPlaybackEngineNowSafely();
            }

            ReleaseCurrentVideoCacheLease("dispose");

            if (_playbackEngine is not null)
            {
                UnsubscribePlaybackEngine(_playbackEngine);
                _playbackEngine.Dispose();
                _playbackEngine = null;
                ResetAppliedPlayerPreferenceCache();
            }
        }
        catch
        {
        }
        finally
        {
            MpvPlaybackDiagnostics.Write($"player-r4-shutdown-complete reason={reason} elapsedMs={stopwatch.ElapsedMilliseconds}");
            if (stopwatch.ElapsedMilliseconds >= 1000)
            {
                MpvPlaybackDiagnostics.Write($"player-r4-shutdown-slow stage=dispose-resources elapsedMs={stopwatch.ElapsedMilliseconds}");
            }
        }

        if (disposeLocked)
        {
            try
            {
                _playbackReloadLock.Release();
            }
            catch
            {
            }
        }

        try
        {
            if (disposeLocked)
            {
                _playbackReloadLock.Dispose();
                _subtitleSwitchLock.Dispose();
                _audioTrackSwitchLock.Dispose();
            }
        }
        catch
        {
        }
    }
}
