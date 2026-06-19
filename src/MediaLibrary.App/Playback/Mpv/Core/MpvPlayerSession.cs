using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MediaLibrary.App.Helpers;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.App.Playback.Mpv.Core;

public sealed class MpvPlayerSession : IAsyncDisposable
{
    private const int EndFileReasonEof = 0;
    private const int EndFileReasonStop = 2;
    private const string HwdecEnvironmentVariable = "MEDIA_LIBRARY_MPV_HWDEC";
    private const string DefaultHwdecValue = "auto-safe";
    private const string MpvSessionCacheRootName = "mpv-session";
    private const string MpvSessionActiveMarkerName = ".active.json";
    private static readonly TimeSpan DisposeWaitTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan[] ResumeSeekWatchIntervals =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(8)
    ];
    private static readonly TimeSpan[] EmbeddedSubtitleSwitchWatchIntervals =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5)
    ];
    private static readonly TimeSpan TrackDiscoveryStableWindow = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan TrackDiscoveryMaxWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan[] TrackDiscoveryProbeIntervals =
    [
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2)
    ];
    private static readonly TimeSpan[] TrackDiscoveryLateProbeIntervals =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20)
    ];

    private readonly object _syncRoot = new();
    private IntPtr _handle;
    private CancellationTokenSource? _eventLoopCancellation;
    private Task? _eventLoopTask;
    private bool _disposed;
    private bool _initialized;
    private bool _stopRequested;
    private bool _isPaused;
    private bool _isPlaying;
    private bool _isBuffering;
    private bool _fileLoaded;
    private bool _playbackRestarted;
    private long _asyncCommandReplyUserData;
    private bool _resumeSeekReadyRaised;
    private bool _resumeSeekCommandReturned;
    private bool _resumeSeekRecoveryAttempted;
    private bool? _coreIdle;
    private bool? _idleActive;
    private bool? _pausedForCache;
    private double _bufferingPercent;
    private TimeSpan _duration = TimeSpan.Zero;
    private TimeSpan _position = TimeSpan.Zero;
    private TimeSpan? _pendingStartPosition;
    private double? _resumeSeekTargetSeconds;
    private bool _loadTimeStartOptionUsed;
    private bool _loadTimeStartFirstPositionLogged;
    private string? _mpvSessionCacheDirectory;
    private string? _mpvSessionCacheActiveMarkerPath;
    private DateTime _loadCommandSubmittedUtc;
    private int _lastLoggedPositionSecond = -1;
    private int _lastLoggedDurationSecond = -1;
    private int _lastLoggedBufferingPercent = -1;
    private bool? _lastLoggedPausedForCache;
    private DateTime _lastPositionChangedUtc;
    private MpvCacheStateSnapshot _lastCacheStateSnapshot = MpvCacheStateSnapshot.Empty;
    private MpvTrackListSnapshot _lastTrackListSnapshot = MpvTrackListSnapshot.Empty;
    private readonly Dictionary<(int Id, string Kind), MpvTrackInfo> _trackRegistry = [];
    private CancellationTokenSource? _resumeSeekWatchCancellation;
    private CancellationTokenSource? _embeddedSubtitleSwitchWatchCancellation;
    private CancellationTokenSource? _trackDiscoveryCancellation;
    private MpvLoadRequest? _currentRequest;
    private string _lastObservedAid = "unknown";
    private string _lastObservedSid = "unknown";
    private bool _tracksChangedWhileResumeBlocked;
    private bool _trackDiscoveryStarted;
    private bool _trackMenuPublished;
    private int _trackRegistryUpdateSequence;
    private DateTime _trackDiscoveryStartedUtc;
    private DateTime _lastTrackRegistryUpdateUtc;
    private int _embeddedSubtitleSwitchRequestId;
    private int? _embeddedSubtitleSwitchTargetTrackId;
    private bool _embeddedSubtitleSwitchRecoveryAttempted;
    private DateTime _embeddedSubtitleSwitchStartedUtc;

    public MpvPlayerSession()
    {
        SessionId = Guid.NewGuid();
        MpvPlaybackDiagnostics.Write($"mpv-core-session-created sessionId={SessionId}");
    }

    public Guid SessionId { get; }

    public TimeSpan Duration => _duration;

    public TimeSpan Position => _position;

    public bool IsPlaying => _isPlaying;

    public bool IsBuffering => _isBuffering;

    public MpvTrackListSnapshot TrackListSnapshot => _lastTrackListSnapshot;

    public event EventHandler<MpvSessionEventArgs>? Opening;

    public event EventHandler<MpvSessionEventArgs>? FileLoaded;

    public event EventHandler<MpvSessionEventArgs>? PlaybackRestarted;

    public event EventHandler<MpvSessionPositionChangedEventArgs>? PositionChanged;

    public event EventHandler<MpvSessionDurationChangedEventArgs>? DurationChanged;

    public event EventHandler<MpvSessionStateChangedEventArgs>? StateChanged;

    public event EventHandler<MpvSessionEndFileEventArgs>? EndFile;

    public event EventHandler<MpvSessionErrorEventArgs>? Error;

    public event EventHandler<MpvSessionTracksChangedEventArgs>? TracksChanged;

    public event EventHandler<MpvSessionSubtitleTrackChangedEventArgs>? SubtitleTrackChanged;

    public event EventHandler<MpvSessionAudioTrackChangedEventArgs>? AudioTrackChanged;

    public Task InitializeAsync(IntPtr hostHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (hostHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Playback host is not ready.");
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (_initialized)
            {
                return Task.CompletedTask;
            }

            var loadResult = MpvNative.EnsureLoaded();
            if (!loadResult.Succeeded)
            {
                throw new InvalidOperationException(loadResult.Error ?? "mpv native files are missing.");
            }

            var handle = MpvNative.Create();
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("mpv_create returned a null handle.");
            }

            _handle = handle;
            TrySetOptionString("config", "no");
            TrySetOptionString("terminal", "no");
            TrySetOptionString("osc", "no");
            TrySetOptionString("input-default-bindings", "no");
            TrySetOptionString("force-window", "yes");
            TrySetOptionString("wid", hostHandle.ToInt64().ToString(CultureInfo.InvariantCulture));
            TrySetOptionString("volume-max", "200");
            TrySetOptionString("cache", "yes");
            TrySetOptionString("network-timeout", "20");
            ConfigureHwdecOption();

            var initializeResult = MpvNative.Initialize(handle);
            if (initializeResult < 0)
            {
                var error = FormatMpvError(initializeResult);
                _handle = IntPtr.Zero;
                MpvNative.TerminateDestroy(handle);
                throw new InvalidOperationException($"mpv_initialize failed: {error}");
            }

            _ = MpvNative.RequestLogMessages(handle, "warn");
            _ = MpvNative.ObserveProperty(handle, 1, "duration", MpvFormat.Double);
            _ = MpvNative.ObserveProperty(handle, 2, "time-pos", MpvFormat.Double);
            _ = MpvNative.ObserveProperty(handle, 3, "pause", MpvFormat.Flag);
            _ = MpvNative.ObserveProperty(handle, 4, "paused-for-cache", MpvFormat.Flag);
            _ = MpvNative.ObserveProperty(handle, 5, "cache-buffering-state", MpvFormat.Int64);
            _ = MpvNative.ObserveProperty(handle, 6, "eof-reached", MpvFormat.Flag);
            _ = MpvNative.ObserveProperty(handle, 7, "demuxer-cache-state", MpvFormat.Node);
            _ = MpvNative.ObserveProperty(handle, 8, "core-idle", MpvFormat.Flag);
            _ = MpvNative.ObserveProperty(handle, 9, "idle-active", MpvFormat.Flag);
            _ = MpvNative.ObserveProperty(handle, 10, "track-list", MpvFormat.Node);
            _ = MpvNative.ObserveProperty(handle, 11, "aid", MpvFormat.String);
            _ = MpvNative.ObserveProperty(handle, 12, "sid", MpvFormat.String);

            _eventLoopCancellation = new CancellationTokenSource();
            _eventLoopTask = Task.Run(() => RunEventLoop(_eventLoopCancellation.Token), CancellationToken.None);
            _initialized = true;
        }

        return Task.CompletedTask;
    }

    public Task LoadAsync(MpvLoadRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (!_initialized)
        {
            throw new InvalidOperationException("mpv session is not initialized.");
        }

        var handle = GetRequiredHandle();
        ReleaseMpvSessionCacheMarker("load-reset");
        CancelResumeSeekWatch();
        CancelEmbeddedSubtitleSwitchWatch();
        CancelTrackDiscovery();
        _currentRequest = request;
        _duration = TimeSpan.Zero;
        _position = TimeSpan.Zero;
        _isPlaying = false;
        _isPaused = false;
        _isBuffering = true;
        _fileLoaded = false;
        _playbackRestarted = false;
        _resumeSeekReadyRaised = false;
        _resumeSeekCommandReturned = false;
        _resumeSeekRecoveryAttempted = false;
        _coreIdle = null;
        _idleActive = null;
        _bufferingPercent = 0;
        _lastTrackListSnapshot = MpvTrackListSnapshot.Empty;
        _trackRegistry.Clear();
        _trackRegistryUpdateSequence = 0;
        _trackDiscoveryStarted = false;
        _trackMenuPublished = false;
        _trackDiscoveryStartedUtc = DateTime.MinValue;
        _lastTrackRegistryUpdateUtc = DateTime.MinValue;
        _lastObservedAid = "unknown";
        _lastObservedSid = "unknown";
        _tracksChangedWhileResumeBlocked = false;
        _embeddedSubtitleSwitchRequestId = 0;
        _embeddedSubtitleSwitchTargetTrackId = null;
        _embeddedSubtitleSwitchRecoveryAttempted = false;
        _pausedForCache = null;
        _stopRequested = false;
        var isWebDavResume = IsWebDavRequest(request) && request.StartPositionSeconds > 0;
        _resumeSeekTargetSeconds = null;
        _pendingStartPosition = !isWebDavResume && request.StartPositionSeconds > 0
            ? TimeSpan.FromSeconds(request.StartPositionSeconds)
            : null;
        _loadTimeStartOptionUsed = false;
        _loadTimeStartFirstPositionLogged = false;
        _loadCommandSubmittedUtc = DateTime.MinValue;
        _lastLoggedPositionSecond = -1;
        _lastLoggedDurationSecond = -1;
        _lastLoggedBufferingPercent = -1;
        _lastLoggedPausedForCache = null;
        _lastPositionChangedUtc = DateTime.MinValue;
        _lastCacheStateSnapshot = MpvCacheStateSnapshot.Empty;
        MpvPlaybackDiagnostics.Write($"mpv-r3-track-registry-reset sessionId={SessionId} reason=new-session");
        ConfigureRequestOptions(request);
        Opening?.Invoke(this, new MpvSessionEventArgs(SessionId));
        RaiseStateChanged();

        var sourceKind = request.IsLocalFile ? "file" : request.ProtocolType == ProtocolType.WebDav ? "webdav" : "unknown";
        MpvPlaybackDiagnostics.Write(
            $"mpv-core-load-start sessionId={SessionId} mediaFileId={request.MediaFileId} sourceKind={sourceKind} fileSize={request.FileSize}");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-load-start sessionId={SessionId} mediaFileId={request.MediaFileId} sourceKind={sourceKind} fileSize={request.FileSize} resumeSeconds={request.StartPositionSeconds}");
        if (isWebDavResume)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-start-option-config sessionId={SessionId} source=webdav resumeSeconds={request.StartPositionSeconds} enabled=true");
        }

        _loadCommandSubmittedUtc = DateTime.UtcNow;
        var result = isWebDavResume
            ? InvokeLoadfileWithStartOptionOrFallback(handle, request)
            : InvokeCommand(handle, "loadfile", request.PlaybackUrl, "replace");
        MpvPlaybackDiagnostics.Write($"mpv-core-load-command-return sessionId={SessionId} result={result}");
        MpvPlaybackDiagnostics.Write($"mpv-r2-load-command-return sessionId={SessionId} result={result}");
        if (result < 0)
        {
            throw new InvalidOperationException($"mpv loadfile failed: {FormatMpvError(result)}");
        }

        return Task.CompletedTask;
    }

    private int InvokeLoadfileWithStartOptionOrFallback(IntPtr handle, MpvLoadRequest request)
    {
        var startValue = FormatSeconds(request.StartPositionSeconds);
        MpvPlaybackDiagnostics.Write(
            $"mpv-r21a-start-option-attempt sessionId={SessionId} method=loadfile-option-index resumeSeconds={startValue}");
        var result = InvokeCommand(handle, "loadfile", request.PlaybackUrl, "replace", "-1", $"start={startValue}");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r21a-start-option-result sessionId={SessionId} method=loadfile-option-index result={result}");
        if (result < 0)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-start-option-attempt sessionId={SessionId} method=loadfile-option-direct resumeSeconds={startValue}");
            result = InvokeCommand(handle, "loadfile", request.PlaybackUrl, "replace", $"start={startValue}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-start-option-result sessionId={SessionId} method=loadfile-option-direct result={result}");
        }

        if (result >= 0)
        {
            _loadTimeStartOptionUsed = true;
            _resumeSeekTargetSeconds = null;
            _pendingStartPosition = null;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-start-option-used sessionId={SessionId} resumeSeconds={startValue}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-delayed-seek-skipped sessionId={SessionId} reason=start-option-used");
            return result;
        }

        _loadTimeStartOptionUsed = false;
        _resumeSeekTargetSeconds = request.StartPositionSeconds;
        _pendingStartPosition = null;
        MpvPlaybackDiagnostics.Write(
            $"mpv-r21a-delayed-seek-fallback sessionId={SessionId} reason=start-option-failed");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-resume-seek-pending sessionId={SessionId} mediaFileId={request.MediaFileId} seconds={startValue}");
        return InvokeCommand(handle, "loadfile", request.PlaybackUrl, "replace");
    }

    public void Play()
    {
        if (!TryGetHandle(out var handle))
        {
            return;
        }

        _stopRequested = false;
        var result = TrySetPropertyString(handle, "pause", "no");
        MpvPlaybackDiagnostics.Write($"mpv-core-play-command sessionId={SessionId} result={result.ToString().ToLowerInvariant()}");
    }

    public void Pause()
    {
        if (!TryGetHandle(out var handle))
        {
            return;
        }

        var result = TrySetPropertyString(handle, "pause", "yes");
        MpvPlaybackDiagnostics.Write($"mpv-core-pause-command sessionId={SessionId} result={result.ToString().ToLowerInvariant()}");
    }

    public void Stop()
    {
        if (!TryGetHandle(out var handle))
        {
            return;
        }

        CancelResumeSeekWatch();
        CancelEmbeddedSubtitleSwitchWatch();
        CancelTrackDiscovery();
        _stopRequested = true;
        var result = InvokeCommand(handle, "stop");
        _isPlaying = false;
        _isBuffering = false;
        MpvPlaybackDiagnostics.Write($"mpv-core-stop-command sessionId={SessionId} result={result}");
        RaiseStateChanged();
    }

    public void Seek(TimeSpan position)
    {
        if (!TryGetHandle(out var handle))
        {
            return;
        }

        var seconds = Math.Max(0, position.TotalSeconds);
        MpvPlaybackDiagnostics.Write(
            $"mpv-core-seek-start sessionId={SessionId} seconds={seconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        var result = InvokeCommand(
            handle,
            "seek",
            seconds.ToString("0.###", CultureInfo.InvariantCulture),
            "absolute+exact");
        MpvPlaybackDiagnostics.Write($"mpv-core-seek-result sessionId={SessionId} result={result}");
        if (result < 0)
        {
            Error?.Invoke(
                this,
                new MpvSessionErrorEventArgs(SessionId, "seek-failed", FormatMpvError(result)));
        }
    }

    public void SetVolume(int volume)
    {
        if (TryGetHandle(out var handle))
        {
            _ = TrySetPropertyString(handle, "volume", Math.Clamp(volume, 0, 200).ToString(CultureInfo.InvariantCulture));
        }
    }

    public void SetBrightness(int brightness)
    {
        if (!TryGetHandle(out var handle))
        {
            return;
        }

        var normalized = Math.Clamp(brightness, 0, 100);
        var mpvBrightness = (int)Math.Round(-40d + normalized * 0.4d);
        _ = TrySetPropertyString(handle, "brightness", mpvBrightness.ToString(CultureInfo.InvariantCulture));
    }

    public void SetMute(bool muted)
    {
        if (TryGetHandle(out var handle))
        {
            _ = TrySetPropertyString(handle, "mute", muted ? "yes" : "no");
        }
    }

    public bool SetAudioTrack(int? trackId)
    {
        if (!TryGetHandle(out var handle))
        {
            return false;
        }

        var value = trackId.HasValue
            ? trackId.Value.ToString(CultureInfo.InvariantCulture)
            : "auto";
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-audio-select sessionId={SessionId} kind={(trackId.HasValue ? "track" : "auto")} trackId={(trackId.HasValue ? value : "auto")}");
        var result = TrySetPropertyString(handle, "aid", value);
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-audio-command-return sessionId={SessionId} result={result.ToString().ToLowerInvariant()}");
        if (!result)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-audio-select-failed sessionId={SessionId} kind={(trackId.HasValue ? "track" : "auto")} reason=set-aid-failed");
        }

        return result;
    }

    public bool SetSubtitleTrack(int? trackId)
    {
        if (!TryGetHandle(out var handle))
        {
            return false;
        }

        CancelEmbeddedSubtitleSwitchWatch();
        var beforePosition = _position.TotalSeconds;
        var beforeCache = _lastCacheStateSnapshot;
        var beforeEvaluation = beforeCache.Evaluate(beforePosition);
        var requestId = 0;
        if (trackId.HasValue)
        {
            requestId = Interlocked.Increment(ref _embeddedSubtitleSwitchRequestId);
            _embeddedSubtitleSwitchTargetTrackId = trackId.Value;
            _embeddedSubtitleSwitchRecoveryAttempted = false;
            _embeddedSubtitleSwitchStartedUtc = DateTime.UtcNow;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-start sessionId={SessionId} requestId={requestId} fromSid={FormatOptionalTrackId(ParseObservedTrackId(_lastObservedSid))} toSid={trackId.Value} timePos={FormatSeconds(beforePosition)}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-before-cache sessionId={SessionId} requestId={requestId} inRange={MpvCacheStateSnapshot.FormatBool(beforeEvaluation.CurrentTimeInSeekableRange)} cacheDuration={MpvCacheStateSnapshot.FormatDouble(beforeCache.CacheDuration)} readerPts={MpvCacheStateSnapshot.FormatDouble(beforeCache.ReaderPts)} cacheEnd={MpvCacheStateSnapshot.FormatDouble(beforeCache.CacheEnd)}");
        }
        else
        {
            _embeddedSubtitleSwitchTargetTrackId = null;
            _embeddedSubtitleSwitchRecoveryAttempted = false;
        }

        var value = trackId.HasValue
            ? trackId.Value.ToString(CultureInfo.InvariantCulture)
            : "no";
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-subtitle-select sessionId={SessionId} kind={(trackId.HasValue ? "embedded" : "none")} trackId={value}");
        var stopwatch = Stopwatch.StartNew();
        var result = TrySetPropertyString(handle, "sid", value);
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-subtitle-command-return sessionId={SessionId} result={result.ToString().ToLowerInvariant()}");
        if (trackId.HasValue)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-command-return sessionId={SessionId} requestId={requestId} elapsedMs={stopwatch.ElapsedMilliseconds} result={result.ToString().ToLowerInvariant()}");
            MpvPlaybackDiagnostics.Write($"mpv-r3-embedded-switch-no-reload-confirmed sessionId={SessionId} requestId={requestId}");
        }

        if (!result)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-subtitle-select-failed sessionId={SessionId} kind={(trackId.HasValue ? "embedded" : "none")} reason=set-sid-failed");
        }
        else if (trackId.HasValue)
        {
            StartEmbeddedSubtitleSwitchWatch(requestId, trackId.Value, beforePosition);
        }

        return result;
    }

    public bool AddExternalSubtitle(string playbackUrl, string username, string password, bool select)
    {
        if (string.IsNullOrWhiteSpace(playbackUrl) || !TryGetHandle(out var handle))
        {
            return false;
        }

        var auth = "none";
        if (!string.IsNullOrWhiteSpace(username))
        {
            var credentialBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
            var authorization = Convert.ToBase64String(credentialBytes);
            _ = TrySetPropertyString(handle, "http-header-fields", $"Authorization: Basic {authorization}")
                || TrySetOptionString("http-header-fields", $"Authorization: Basic {authorization}");
            auth = "basic";
        }

        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-external-subtitle-add-start sessionId={SessionId} urlLength={playbackUrl.Length} auth={auth}");
        var replyUserData = unchecked((ulong)Interlocked.Increment(ref _asyncCommandReplyUserData));
        var result = InvokeCommandAsync(handle, replyUserData, "sub-add", playbackUrl, select ? "select" : "auto");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-external-subtitle-add-submit sessionId={SessionId} replyUserData={replyUserData} result={result}");
        if (result < 0)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-subtitle-select-failed sessionId={SessionId} kind=external reason={SanitizeLogPart(FormatMpvError(result))}");
            return false;
        }

        _ = RefreshTracksAfterExternalSubtitleAsync();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        IntPtr handle;
        CancellationTokenSource? cancellation;
        Task? eventLoopTask;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                MpvPlaybackDiagnostics.Write($"mpv-r4-dispose-complete sessionId={SessionId} result=already-disposed");
                return;
            }

            _disposed = true;
            handle = _handle;
            _handle = IntPtr.Zero;
            cancellation = _eventLoopCancellation;
            _eventLoopCancellation = null;
            eventLoopTask = _eventLoopTask;
            _eventLoopTask = null;
            DetachEventHandlers();
        }

        MpvPlaybackDiagnostics.Write($"mpv-core-dispose-start sessionId={SessionId}");
        MpvPlaybackDiagnostics.Write($"mpv-r4-dispose-start sessionId={SessionId}");
        MpvPlaybackDiagnostics.Write($"mpv-core-dispose-detach sessionId={SessionId}");
        MpvPlaybackDiagnostics.Write($"mpv-r4-dispose-detach sessionId={SessionId}");
        try
        {
            CancelResumeSeekWatch();
            CancelEmbeddedSubtitleSwitchWatch();
            CancelTrackDiscovery();
            ReleaseMpvSessionCacheMarker("dispose");
            TrySendQuitForDispose(handle);
            MpvPlaybackDiagnostics.Write($"mpv-core-event-loop-cancel sessionId={SessionId}");
            MpvPlaybackDiagnostics.Write($"mpv-r4-event-loop-cancel sessionId={SessionId}");
            cancellation?.Cancel();
            if (eventLoopTask is not null)
            {
                var completed = await Task.WhenAny(eventLoopTask, Task.Delay(DisposeWaitTimeout)).ConfigureAwait(false);
                if (completed != eventLoopTask)
                {
                    MpvPlaybackDiagnostics.Write($"mpv-core-event-loop-timeout sessionId={SessionId} elapsedMs={(int)DisposeWaitTimeout.TotalMilliseconds}");
                    MpvPlaybackDiagnostics.Write($"mpv-r4-event-loop-timeout sessionId={SessionId} elapsedMs={(int)DisposeWaitTimeout.TotalMilliseconds}");
                }
            }
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-core-dispose-wait-failed sessionId={SessionId} errorType={exception.GetType().Name}");
        }
        finally
        {
            cancellation?.Dispose();
        }

        MpvPlaybackDiagnostics.Write($"mpv-core-dispose-complete sessionId={SessionId} result=detached");
        MpvPlaybackDiagnostics.Write($"mpv-r4-dispose-complete sessionId={SessionId} result=detached");
        DestroyNativeHandleInBackground(handle);
    }

    private void RunEventLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var handle = GetHandleOrDefault();
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                var eventPointer = MpvNative.WaitEvent(handle, 0.1d);
                if (eventPointer == IntPtr.Zero)
                {
                    continue;
                }

                var mpvEvent = Marshal.PtrToStructure<MpvEvent>(eventPointer);
                if (mpvEvent.EventId == MpvEventId.None)
                {
                    continue;
                }

                if (_disposed && mpvEvent.EventId != MpvEventId.Shutdown)
                {
                    continue;
                }

                HandleEvent(mpvEvent);
                if (mpvEvent.EventId == MpvEventId.Shutdown)
                {
                    return;
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown.
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-core-event-loop-error sessionId={SessionId} errorType={exception.GetType().Name}");
            Error?.Invoke(
                this,
                new MpvSessionErrorEventArgs(SessionId, exception.GetType().Name, exception.Message));
        }
        finally
        {
            MpvPlaybackDiagnostics.Write($"mpv-core-event-loop-exit sessionId={SessionId}");
            MpvPlaybackDiagnostics.Write($"mpv-r4-event-loop-exit sessionId={SessionId}");
        }
    }

    private void HandleEvent(MpvEvent mpvEvent)
    {
        switch (mpvEvent.EventId)
        {
            case MpvEventId.FileLoaded:
                HandleFileLoaded();
                break;
            case MpvEventId.PlaybackRestart:
                HandlePlaybackRestart();
                break;
            case MpvEventId.EndFile:
                HandleEndFile(mpvEvent);
                break;
            case MpvEventId.PropertyChange:
                HandlePropertyChange(mpvEvent);
                break;
            case MpvEventId.LogMessage:
                HandleLogMessage(mpvEvent);
                break;
            case MpvEventId.CommandReply:
                HandleCommandReply(mpvEvent);
                break;
            case MpvEventId.TracksChanged:
            case MpvEventId.TrackSwitched:
                RefreshTrackListFromProperty(mpvEvent.EventId.ToString());
                break;
        }
    }

    private void HandleCommandReply(MpvEvent mpvEvent)
    {
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-async-command-reply sessionId={SessionId} replyUserData={mpvEvent.ReplyUserData} result={mpvEvent.Error}");
        if (mpvEvent.Error < 0)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-subtitle-select-failed sessionId={SessionId} kind=external reason={SanitizeLogPart(FormatMpvError(mpvEvent.Error))}");
        }
    }

    private void HandleFileLoaded()
    {
        _fileLoaded = true;
        MpvPlaybackDiagnostics.Write($"mpv-core-file-loaded sessionId={SessionId}");
        MpvPlaybackDiagnostics.Write($"mpv-r2-file-loaded sessionId={SessionId}");
        LogVideoFormatSummary();
        FileLoaded?.Invoke(this, new MpvSessionEventArgs(SessionId));
        StartTrackDiscoveryIfNeeded("file-loaded");

        var pendingStart = _pendingStartPosition;
        if (pendingStart.HasValue && pendingStart.Value.TotalSeconds > 0)
        {
            _pendingStartPosition = null;
            Task.Run(() => Seek(pendingStart.Value));
        }
    }

    private void HandlePlaybackRestart()
    {
        _playbackRestarted = true;
        _isPlaying = !_isPaused && !_stopRequested && !IsResumeSeekReadinessBlocked();
        _isBuffering = false;
        MpvPlaybackDiagnostics.Write($"mpv-core-playback-restart sessionId={SessionId}");
        MpvPlaybackDiagnostics.Write($"mpv-r2-playback-restart sessionId={SessionId}");
        StartTrackDiscoveryIfNeeded("playback-restart");
        if (TryApplyPendingResumeSeek())
        {
            RaiseStateChanged();
            return;
        }

        if (IsResumeSeekReadinessBlocked())
        {
            MpvPlaybackDiagnostics.Write($"mpv-r2-playback-restart-deferred sessionId={SessionId} reason=resume-seek-verifying");
            RaiseStateChanged();
            return;
        }

        RaisePlaybackRestarted("playback-restart");
        RaiseStateChanged();
    }

    private void HandleEndFile(MpvEvent mpvEvent)
    {
        if (mpvEvent.Data == IntPtr.Zero)
        {
            return;
        }

        var endFile = Marshal.PtrToStructure<MpvEventEndFile>(mpvEvent.Data);
        MpvPlaybackDiagnostics.Write($"mpv-core-end-file sessionId={SessionId} reason={endFile.Reason}");
        EndFile?.Invoke(this, new MpvSessionEndFileEventArgs(SessionId, endFile.Reason, endFile.Error));

        if (endFile.Reason == EndFileReasonEof)
        {
            CancelResumeSeekWatch();
            _isPlaying = false;
            _isBuffering = false;
            RaiseStateChanged();
            return;
        }

        if (endFile.Reason != EndFileReasonStop && !_stopRequested)
        {
            var error = new MpvSessionErrorEventArgs(
                SessionId,
                "end-file-error",
                endFile.Error == 0 ? null : FormatMpvError(endFile.Error));
            Error?.Invoke(this, error);
        }
    }

    private void DestroyNativeHandleInBackground(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var sessionId = SessionId;
        var destroyTask = Task.Factory.StartNew(
            () =>
        {
            try
            {
                MpvPlaybackDiagnostics.Write($"mpv-core-native-destroy-background-start sessionId={sessionId}");
                MpvPlaybackDiagnostics.Write($"mpv-r4-native-destroy-background-start sessionId={sessionId}");
                MpvNative.TerminateDestroy(handle);
                MpvPlaybackDiagnostics.Write($"mpv-core-native-destroy-background-complete sessionId={sessionId}");
                MpvPlaybackDiagnostics.Write($"mpv-r4-native-destroy-background-complete sessionId={sessionId}");
            }
            catch (Exception exception)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-core-native-destroy-background-failed sessionId={sessionId} errorType={exception.GetType().Name}");
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r4-native-destroy-background-failed sessionId={sessionId} errorType={exception.GetType().Name}");
            }
        },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (!destroyTask.IsCompleted)
            {
                MpvPlaybackDiagnostics.Write($"mpv-core-native-destroy-background-timeout sessionId={sessionId} elapsedMs=5000");
                MpvPlaybackDiagnostics.Write($"mpv-r4-native-destroy-background-timeout sessionId={sessionId} elapsedMs=5000");
            }
        });
    }

    private void DetachEventHandlers()
    {
        Opening = null;
        FileLoaded = null;
        PlaybackRestarted = null;
        PositionChanged = null;
        DurationChanged = null;
        StateChanged = null;
        EndFile = null;
        Error = null;
        TracksChanged = null;
        SubtitleTrackChanged = null;
        AudioTrackChanged = null;
    }

    private void TrySendQuitForDispose(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var result = InvokeCommand(handle, "quit");
            MpvPlaybackDiagnostics.Write($"mpv-core-dispose-quit-command sessionId={SessionId} result={result}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-core-dispose-quit-command-failed sessionId={SessionId} errorType={exception.GetType().Name}");
        }
    }

    private void HandlePropertyChange(MpvEvent mpvEvent)
    {
        if (mpvEvent.Data == IntPtr.Zero)
        {
            return;
        }

        var property = Marshal.PtrToStructure<MpvEventProperty>(mpvEvent.Data);
        var name = Marshal.PtrToStringUTF8(property.Name);
        if (string.IsNullOrWhiteSpace(name) || property.Data == IntPtr.Zero)
        {
            return;
        }

        switch (name)
        {
            case "duration" when property.Format == MpvFormat.Double:
                HandleDuration(Marshal.PtrToStructure<double>(property.Data));
                break;
            case "time-pos" when property.Format == MpvFormat.Double:
                HandlePosition(Marshal.PtrToStructure<double>(property.Data));
                break;
            case "pause" when property.Format == MpvFormat.Flag:
                HandlePause(ReadFlag(property.Data));
                break;
            case "paused-for-cache" when property.Format == MpvFormat.Flag:
                HandlePausedForCache(ReadFlag(property.Data));
                break;
            case "cache-buffering-state" when property.Format == MpvFormat.Int64:
                HandleBuffering(Marshal.ReadInt64(property.Data));
                break;
            case "eof-reached" when property.Format == MpvFormat.Flag && ReadFlag(property.Data):
                CancelResumeSeekWatch();
                _isPlaying = false;
                RaiseStateChanged();
                break;
            case "demuxer-cache-state" when property.Format == MpvFormat.Node:
                HandleDemuxerCacheState(property.Data);
                break;
            case "core-idle" when property.Format == MpvFormat.Flag:
                _coreIdle = ReadFlag(property.Data);
                break;
            case "idle-active" when property.Format == MpvFormat.Flag:
                _idleActive = ReadFlag(property.Data);
                break;
            case "track-list" when property.Format == MpvFormat.Node:
                HandleTrackList(property.Data, "property");
                break;
            case "aid" when property.Format == MpvFormat.String:
                HandleAid(ReadPropertyStringValue(property.Data));
                break;
            case "sid" when property.Format == MpvFormat.String:
                HandleSid(ReadPropertyStringValue(property.Data));
                break;
        }
    }

    private void HandleDuration(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            return;
        }

        _duration = TimeSpan.FromSeconds(seconds);
        var rounded = (int)Math.Round(seconds);
        if (rounded != _lastLoggedDurationSecond)
        {
            _lastLoggedDurationSecond = rounded;
            MpvPlaybackDiagnostics.Write($"mpv-core-duration sessionId={SessionId} seconds={rounded}");
        }

        DurationChanged?.Invoke(this, new MpvSessionDurationChangedEventArgs(SessionId, _duration));
    }

    private void HandlePosition(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            return;
        }

        _position = TimeSpan.FromSeconds(seconds);
        _lastPositionChangedUtc = DateTime.UtcNow;
        var rounded = (int)Math.Round(seconds);
        if (rounded != _lastLoggedPositionSecond)
        {
            _lastLoggedPositionSecond = rounded;
            MpvPlaybackDiagnostics.Write($"mpv-core-position sessionId={SessionId} seconds={rounded}");
        }

        if (_loadTimeStartOptionUsed && !_loadTimeStartFirstPositionLogged)
        {
            _loadTimeStartFirstPositionLogged = true;
            var elapsedMs = _loadCommandSubmittedUtc == DateTime.MinValue
                ? 0
                : (long)Math.Max(0d, (DateTime.UtcNow - _loadCommandSubmittedUtc).TotalMilliseconds);
            var snapshot = _lastCacheStateSnapshot;
            var evaluation = snapshot.Evaluate(seconds);
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-start-option-first-position sessionId={SessionId} seconds={FormatSeconds(seconds)} elapsedMs={elapsedMs}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r21a-start-option-cache-state sessionId={SessionId} currentTime={FormatSeconds(seconds)} inRange={MpvCacheStateSnapshot.FormatBool(evaluation.CurrentTimeInSeekableRange)} cacheDuration={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheDuration)} readerPts={MpvCacheStateSnapshot.FormatDouble(snapshot.ReaderPts)} cacheEnd={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheEnd)}");
        }

        PositionChanged?.Invoke(this, new MpvSessionPositionChangedEventArgs(SessionId, _position));
    }

    private void HandlePause(bool paused)
    {
        _isPaused = paused;
        _isPlaying = !paused && !_stopRequested && !IsResumeSeekReadinessBlocked();
        RaiseStateChanged();
    }

    private bool TryApplyPendingResumeSeek()
    {
        if (!_resumeSeekTargetSeconds.HasValue || _resumeSeekCommandReturned || _resumeSeekReadyRaised)
        {
            return false;
        }

        var target = _resumeSeekTargetSeconds.Value;
        if (!TryGetHandle(out var handle))
        {
            return false;
        }

        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-resume-seek-command-sent sessionId={SessionId} seconds={FormatSeconds(target)} mode=absolute+exact");
        var result = InvokeCommand(handle, "seek", FormatSeconds(target), "absolute+exact");
        _resumeSeekCommandReturned = result >= 0;
        MpvPlaybackDiagnostics.Write($"mpv-r2-resume-seek-command-return sessionId={SessionId} result={result}");
        if (result < 0)
        {
            RaiseResumeSeekFailed("seek-command-failed", FormatMpvError(result));
            return true;
        }

        StartResumeSeekWatch(isRecovery: false);
        return true;
    }

    private bool IsResumeSeekReadinessBlocked()
    {
        return _resumeSeekTargetSeconds.HasValue && !_resumeSeekReadyRaised;
    }

    private void StartResumeSeekWatch(bool isRecovery)
    {
        CancelResumeSeekWatch();
        if (!_resumeSeekTargetSeconds.HasValue)
        {
            return;
        }

        var target = _resumeSeekTargetSeconds.Value;
        var cancellation = new CancellationTokenSource();
        _resumeSeekWatchCancellation = cancellation;
        _ = Task.Run(() => WatchResumeSeekAsync(target, isRecovery, cancellation.Token), CancellationToken.None);
    }

    private async Task WatchResumeSeekAsync(double targetSeconds, bool isRecovery, CancellationToken cancellationToken)
    {
        try
        {
            var startedAt = DateTime.UtcNow;
            var previousPosition = _position.TotalSeconds;
            var previousFileCacheBytes = _lastCacheStateSnapshot.FileCacheBytes;
            var previousNearestDistance = _lastCacheStateSnapshot.Evaluate(_position.TotalSeconds).NearestRangeDistance;
            var prefix = isRecovery ? "mpv-r2-resume-seek-recovery-state" : "mpv-r2-resume-seek-watch-state";
            if (!isRecovery)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r2-resume-seek-watch-start sessionId={SessionId} target={FormatSeconds(targetSeconds)}");
            }

            foreach (var interval in ResumeSeekWatchIntervals)
            {
                var delay = startedAt + interval - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested || _disposed || _stopRequested)
                {
                    return;
                }

                var stage = $"after-{(int)interval.TotalSeconds}s";
                var currentPosition = _position.TotalSeconds;
                var moving = IsResumePositionMoving(targetSeconds, previousPosition, currentPosition);
                var snapshot = _lastCacheStateSnapshot;
                var evaluation = snapshot.Evaluate(currentPosition);
                LogResumeSeekState(prefix, stage, currentPosition, moving, snapshot, evaluation);

                if (IsResumeSeekHealthy(targetSeconds, currentPosition, moving, snapshot, evaluation, previousFileCacheBytes, previousNearestDistance))
                {
                    MarkResumeSeekHealthy(isRecovery ? "recovered" : "healthy");
                    return;
                }

                if (ShouldTreatResumeSeekAsStalled(interval, currentPosition, moving, snapshot, evaluation))
                {
                    if (!isRecovery && !_resumeSeekRecoveryAttempted)
                    {
                        _resumeSeekRecoveryAttempted = true;
                        MpvPlaybackDiagnostics.Write(
                            $"mpv-r2-resume-seek-stalled sessionId={SessionId} reason=range-not-covering-current-time stage={stage}");
                        StartResumeSeekRecovery(targetSeconds);
                        return;
                    }

                    if (isRecovery && interval >= TimeSpan.FromSeconds(8))
                    {
                        MpvPlaybackDiagnostics.Write(
                            $"mpv-r2-resume-seek-recovery-failed sessionId={SessionId} reason=range-not-recovered");
                        RaiseResumeSeekFailed("CacheRangeNotRecovered", "续播位置加载失败，请重试或从头播放。");
                        return;
                    }
                }

                previousPosition = currentPosition;
                previousFileCacheBytes = snapshot.FileCacheBytes;
                previousNearestDistance = evaluation.NearestRangeDistance;
            }

            if (!isRecovery)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r2-resume-seek-stalled sessionId={SessionId} reason=watch-timeout");
                if (!_resumeSeekRecoveryAttempted)
                {
                    _resumeSeekRecoveryAttempted = true;
                    StartResumeSeekRecovery(targetSeconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during stop/source switch/dispose.
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r2-resume-seek-watch-failed sessionId={SessionId} errorType={exception.GetType().Name}");
        }
    }

    private void StartResumeSeekRecovery(double targetSeconds)
    {
        if (!TryGetHandle(out var handle))
        {
            RaiseResumeSeekFailed("ResumeSeekFailed", "mpv handle unavailable.");
            return;
        }

        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-resume-seek-recovery-start sessionId={SessionId} strategy=absolute-keyframes seconds={FormatSeconds(targetSeconds)}");
        var result = InvokeCommand(handle, "seek", FormatSeconds(targetSeconds), "absolute+keyframes");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-resume-seek-recovery-command-return sessionId={SessionId} result={result}");
        if (result < 0)
        {
            RaiseResumeSeekFailed("ResumeSeekFailed", FormatMpvError(result));
            return;
        }

        StartResumeSeekWatch(isRecovery: true);
    }

    private void MarkResumeSeekHealthy(string reason)
    {
        if (_resumeSeekReadyRaised)
        {
            return;
        }

        _resumeSeekReadyRaised = true;
        CancelResumeSeekWatch();
        MpvPlaybackDiagnostics.Write(reason == "recovered"
            ? $"mpv-r2-resume-seek-recovery-success sessionId={SessionId}"
            : $"mpv-r2-resume-seek-healthy sessionId={SessionId}");
        RaisePlaybackRestarted($"resume-seek-{reason}");
        RaiseStateChanged();
    }

    private void RaiseResumeSeekFailed(string errorType, string? message)
    {
        CancelResumeSeekWatch();
        _isPlaying = false;
        _isBuffering = false;
        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-error type={errorType} sessionId={SessionId}");
        Error?.Invoke(this, new MpvSessionErrorEventArgs(SessionId, errorType, message));
        RaiseStateChanged();
    }

    private void RaisePlaybackRestarted(string reason)
    {
        _isPlaying = !_isPaused && !_stopRequested;
        MpvPlaybackDiagnostics.Write($"mpv-r2-playing-ready sessionId={SessionId} reason={reason}");
        PlaybackRestarted?.Invoke(this, new MpvSessionEventArgs(SessionId));
        if (_tracksChangedWhileResumeBlocked && _lastTrackListSnapshot.Tracks.Count > 0)
        {
            _tracksChangedWhileResumeBlocked = false;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-auto-track-select-applied sessionId={SessionId} reason=session-ready");
            TracksChanged?.Invoke(this, new MpvSessionTracksChangedEventArgs(SessionId, _lastTrackListSnapshot));
        }
    }

    private void StartEmbeddedSubtitleSwitchWatch(int requestId, int targetTrackId, double switchPosition)
    {
        if (_currentRequest is null || !IsWebDavRequest(_currentRequest))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _embeddedSubtitleSwitchWatchCancellation = cancellation;
        _ = Task.Run(
            () => WatchEmbeddedSubtitleSwitchAsync(requestId, targetTrackId, switchPosition, cancellation.Token),
            CancellationToken.None);
    }

    private async Task WatchEmbeddedSubtitleSwitchAsync(
        int requestId,
        int targetTrackId,
        double switchPosition,
        CancellationToken cancellationToken)
    {
        try
        {
            var startedAt = DateTime.UtcNow;
            var previousPosition = switchPosition;
            foreach (var interval in EmbeddedSubtitleSwitchWatchIntervals)
            {
                var delay = startedAt + interval - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested || _disposed || _stopRequested)
                {
                    return;
                }

                if (_embeddedSubtitleSwitchRequestId != requestId
                    || _embeddedSubtitleSwitchTargetTrackId != targetTrackId)
                {
                    return;
                }

                var stage = $"after-{(int)interval.TotalSeconds}s";
                var currentPosition = _position.TotalSeconds;
                var moving = currentPosition > previousPosition + 0.25d;
                var snapshot = _lastCacheStateSnapshot;
                var evaluation = snapshot.Evaluate(currentPosition);
                LogEmbeddedSubtitleSwitchState(
                    "mpv-r3-embedded-switch-after-cache",
                    requestId,
                    stage,
                    currentPosition,
                    moving,
                    snapshot,
                    evaluation);

                if (IsEmbeddedSubtitleSwitchRecovered(moving, snapshot, evaluation))
                {
                    return;
                }

                if (interval >= TimeSpan.FromSeconds(3)
                    && !_embeddedSubtitleSwitchRecoveryAttempted
                    && IsEmbeddedSubtitleSwitchStalled(moving, snapshot, evaluation))
                {
                    _embeddedSubtitleSwitchRecoveryAttempted = true;
                    StartEmbeddedSubtitleSwitchRecovery(requestId, currentPosition, cancellationToken);
                    return;
                }

                previousPosition = currentPosition;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during stop/source switch/dispose.
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-recovery-failed sessionId={SessionId} requestId={requestId} reason={exception.GetType().Name}");
        }
    }

    private void StartEmbeddedSubtitleSwitchRecovery(
        int requestId,
        double currentPosition,
        CancellationToken cancellationToken)
    {
        if (!TryGetHandle(out var handle))
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-recovery-failed sessionId={SessionId} requestId={requestId} reason=mpv-handle-unavailable");
            return;
        }

        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-embedded-switch-recovery-start sessionId={SessionId} requestId={requestId} strategy=absolute-keyframes currentTime={FormatSeconds(currentPosition)}");
        var result = InvokeCommand(handle, "seek", FormatSeconds(currentPosition), "absolute+keyframes");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-embedded-switch-recovery-command-return sessionId={SessionId} requestId={requestId} result={result}");
        if (result < 0)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-recovery-failed sessionId={SessionId} requestId={requestId} reason={SanitizeLogPart(FormatMpvError(result))}");
            return;
        }

        _ = Task.Run(
            () => WatchEmbeddedSubtitleSwitchRecoveryAsync(requestId, currentPosition, cancellationToken),
            CancellationToken.None);
    }

    private async Task WatchEmbeddedSubtitleSwitchRecoveryAsync(
        int requestId,
        double recoveryPosition,
        CancellationToken cancellationToken)
    {
        try
        {
            var startedAt = DateTime.UtcNow;
            var previousPosition = recoveryPosition;
            foreach (var interval in EmbeddedSubtitleSwitchWatchIntervals)
            {
                var delay = startedAt + interval - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested || _disposed || _stopRequested)
                {
                    return;
                }

                var stage = $"after-{(int)interval.TotalSeconds}s";
                var currentPosition = _position.TotalSeconds;
                var moving = currentPosition > previousPosition + 0.25d;
                var snapshot = _lastCacheStateSnapshot;
                var evaluation = snapshot.Evaluate(currentPosition);
                LogEmbeddedSubtitleSwitchState(
                    "mpv-r3-embedded-switch-recovery-state",
                    requestId,
                    stage,
                    currentPosition,
                    moving,
                    snapshot,
                    evaluation);

                if (IsEmbeddedSubtitleSwitchRecovered(moving, snapshot, evaluation))
                {
                    MpvPlaybackDiagnostics.Write(
                        $"mpv-r3-embedded-switch-recovery-success sessionId={SessionId} requestId={requestId}");
                    return;
                }

                previousPosition = currentPosition;
            }

            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-recovery-failed sessionId={SessionId} requestId={requestId} reason=range-not-recovered");
        }
        catch (OperationCanceledException)
        {
            // Expected during stop/source switch/dispose.
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-recovery-failed sessionId={SessionId} requestId={requestId} reason={exception.GetType().Name}");
        }
    }

    private void LogEmbeddedSubtitleSwitchState(
        string eventName,
        int requestId,
        string stage,
        double currentPosition,
        bool moving,
        MpvCacheStateSnapshot snapshot,
        MpvCacheRangeEvaluation evaluation)
    {
        MpvPlaybackDiagnostics.Write(
            $"{eventName} sessionId={SessionId} requestId={requestId} stage={stage} moving={moving.ToString().ToLowerInvariant()} pausedForCache={FormatNullableBool(_pausedForCache)} inRange={MpvCacheStateSnapshot.FormatBool(evaluation.CurrentTimeInSeekableRange)} cacheDuration={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheDuration)} readerPts={MpvCacheStateSnapshot.FormatDouble(snapshot.ReaderPts)} cacheEnd={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheEnd)} timePos={FormatSeconds(currentPosition)}");
    }

    private static bool IsEmbeddedSubtitleSwitchRecovered(
        bool moving,
        MpvCacheStateSnapshot snapshot,
        MpvCacheRangeEvaluation evaluation)
    {
        return moving
               || evaluation.CurrentTimeInSeekableRange == true
               || snapshot.CacheDuration.GetValueOrDefault() > 0.25d;
    }

    private static bool IsEmbeddedSubtitleSwitchStalled(
        bool moving,
        MpvCacheStateSnapshot snapshot,
        MpvCacheRangeEvaluation evaluation)
    {
        return !moving
               && evaluation.CurrentTimeInSeekableRange == false
               && snapshot.CacheDuration.GetValueOrDefault() <= 0.1d;
    }

    private static bool IsResumePositionMoving(double targetSeconds, double previousPosition, double currentPosition)
    {
        return currentPosition >= targetSeconds + 0.75d
               || (currentPosition >= targetSeconds - 2d && currentPosition > previousPosition + 0.35d);
    }

    private static bool IsResumeSeekHealthy(
        double targetSeconds,
        double currentPosition,
        bool moving,
        MpvCacheStateSnapshot snapshot,
        MpvCacheRangeEvaluation evaluation,
        long? previousFileCacheBytes,
        double? previousNearestDistance)
    {
        if (moving && currentPosition >= targetSeconds - 2d)
        {
            return true;
        }

        if (evaluation.CurrentTimeInSeekableRange == true
            && snapshot.CacheDuration.GetValueOrDefault() > 0.1d
            && evaluation.CacheCoversCurrentTime != false)
        {
            return true;
        }

        var bytesGrowing = snapshot.FileCacheBytes.HasValue
                           && previousFileCacheBytes.HasValue
                           && snapshot.FileCacheBytes.Value > previousFileCacheBytes.Value;
        var rangeApproaching = evaluation.NearestRangeDistance.HasValue
                               && previousNearestDistance.HasValue
                               && evaluation.NearestRangeDistance.Value < previousNearestDistance.Value;
        return bytesGrowing && rangeApproaching && evaluation.NearestRangeDistance.GetValueOrDefault(double.MaxValue) < 5d;
    }

    private static bool ShouldTreatResumeSeekAsStalled(
        TimeSpan elapsed,
        double currentPosition,
        bool moving,
        MpvCacheStateSnapshot snapshot,
        MpvCacheRangeEvaluation evaluation)
    {
        if (elapsed < TimeSpan.FromSeconds(5) || moving)
        {
            return false;
        }

        var rangeMissing = evaluation.CurrentTimeInSeekableRange != true;
        var cacheEmpty = !snapshot.CacheDuration.HasValue || snapshot.CacheDuration.Value <= 0.1d;
        var readerUnknown = !snapshot.ReaderPts.HasValue || !snapshot.CacheEnd.HasValue;
        var rangeFar = evaluation.NearestRangeDistance.GetValueOrDefault(double.MaxValue) > 10d;
        return rangeMissing && cacheEmpty && (readerUnknown || rangeFar || currentPosition > 10d);
    }

    private void LogResumeSeekState(
        string eventName,
        string stage,
        double currentPosition,
        bool moving,
        MpvCacheStateSnapshot snapshot,
        MpvCacheRangeEvaluation evaluation)
    {
        MpvPlaybackDiagnostics.Write(
            $"{eventName} sessionId={SessionId} stage={stage} fileLoaded={_fileLoaded.ToString().ToLowerInvariant()} playbackRestarted={_playbackRestarted.ToString().ToLowerInvariant()} timePos={FormatSeconds(currentPosition)} moving={moving.ToString().ToLowerInvariant()} pause={_isPaused.ToString().ToLowerInvariant()} pausedForCache={FormatNullableBool(_pausedForCache)} buffering={_bufferingPercent.ToString("0", CultureInfo.InvariantCulture)} coreIdle={FormatNullableBool(_coreIdle)} idleActive={FormatNullableBool(_idleActive)} currentTimeInSeekableRange={MpvCacheStateSnapshot.FormatBool(evaluation.CurrentTimeInSeekableRange)} nearestRangeDistance={MpvCacheStateSnapshot.FormatDouble(evaluation.NearestRangeDistance)} cacheDuration={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheDuration)} fileCacheBytes={MpvCacheStateSnapshot.FormatLong(snapshot.FileCacheBytes)} fwBytes={MpvCacheStateSnapshot.FormatLong(snapshot.FwBytes)} readerPts={MpvCacheStateSnapshot.FormatDouble(snapshot.ReaderPts)} cacheEnd={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheEnd)} seekableRangeCount={evaluation.RangeCount}");
    }

    private void HandlePausedForCache(bool pausedForCache)
    {
        _pausedForCache = pausedForCache;
        _isBuffering = pausedForCache;
        RaiseStateChanged();
    }

    private void HandleBuffering(long percent)
    {
        _bufferingPercent = Math.Clamp(percent, 0, 100);
        _isBuffering = _pausedForCache == true || (_bufferingPercent > 0 && _bufferingPercent < 100);
        if ((int)_bufferingPercent != _lastLoggedBufferingPercent)
        {
            _lastLoggedBufferingPercent = (int)_bufferingPercent;
            MpvPlaybackDiagnostics.Write(
                $"mpv-core-state sessionId={SessionId} pause={_isPaused.ToString().ToLowerInvariant()} pausedForCache={FormatNullableBool(_pausedForCache)} buffering={_bufferingPercent.ToString("0", CultureInfo.InvariantCulture)}");
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        if (_pausedForCache != _lastLoggedPausedForCache)
        {
            _lastLoggedPausedForCache = _pausedForCache;
            MpvPlaybackDiagnostics.Write(
                $"mpv-core-state sessionId={SessionId} pause={_isPaused.ToString().ToLowerInvariant()} pausedForCache={FormatNullableBool(_pausedForCache)} buffering={_bufferingPercent.ToString("0", CultureInfo.InvariantCulture)}");
        }

        StateChanged?.Invoke(
            this,
            new MpvSessionStateChangedEventArgs(
                SessionId,
                _isPlaying,
                _isPaused,
                _isBuffering,
                _bufferingPercent,
                _pausedForCache));
    }

    private void HandleLogMessage(MpvEvent mpvEvent)
    {
        if (mpvEvent.Data == IntPtr.Zero)
        {
            return;
        }

        var log = Marshal.PtrToStructure<MpvEventLogMessage>(mpvEvent.Data);
        var prefix = SanitizeLogPart(Marshal.PtrToStringUTF8(log.Prefix) ?? "unknown");
        var level = SanitizeLogPart(Marshal.PtrToStringUTF8(log.Level) ?? "unknown");
        MpvPlaybackDiagnostics.Write($"mpv-core-log-message sessionId={SessionId} prefix={prefix} level={level}");
        var text = Marshal.PtrToStringUTF8(log.Text) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var lower = text.ToLowerInvariant();
            MpvPlaybackDiagnostics.Write(
                $"mpv-r2-log-summary sessionId={SessionId} prefix={prefix} level={level} hasDemux={ContainsAny(lower, "demux", "lavf", "mkv", "matroska").ToString().ToLowerInvariant()} hasCache={ContainsAny(lower, "cache", "buffer").ToString().ToLowerInvariant()} hasNetwork={ContainsAny(lower, "http", "network", "timeout", "range").ToString().ToLowerInvariant()} hasVideo={ContainsAny(lower, "vd", "video", "ffmpeg", "d3d11", "dxva", "hevc").ToString().ToLowerInvariant()} hasError={ContainsAny(lower, "error", "failed", "invalid").ToString().ToLowerInvariant()}");
        }
    }

    private void HandleDemuxerCacheState(IntPtr data)
    {
        try
        {
            var snapshot = ParseDemuxerCacheState(data);
            if (snapshot is null)
            {
                return;
            }

            _lastCacheStateSnapshot = snapshot;
            var currentTime = _position.TotalSeconds;
            var evaluation = snapshot.Evaluate(currentTime);
            MpvPlaybackDiagnostics.Write(
                $"mpv-r2-cache-state sessionId={SessionId} cacheDuration={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheDuration)} fileCacheBytes={MpvCacheStateSnapshot.FormatLong(snapshot.FileCacheBytes)} fwBytes={MpvCacheStateSnapshot.FormatLong(snapshot.FwBytes)} readerPts={MpvCacheStateSnapshot.FormatDouble(snapshot.ReaderPts)} cacheEnd={MpvCacheStateSnapshot.FormatDouble(snapshot.CacheEnd)} seekableRangeCount={evaluation.RangeCount}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r2-cache-ranges sessionId={SessionId} rangeCount={evaluation.RangeCount} currentTime={FormatSeconds(currentTime)} currentTimeInSeekableRange={MpvCacheStateSnapshot.FormatBool(evaluation.CurrentTimeInSeekableRange)} minStart={MpvCacheStateSnapshot.FormatDouble(evaluation.MinStart)} maxEnd={MpvCacheStateSnapshot.FormatDouble(evaluation.MaxEnd)} nearestRangeDistance={MpvCacheStateSnapshot.FormatDouble(evaluation.NearestRangeDistance)} cacheCoversCurrentTime={MpvCacheStateSnapshot.FormatBool(evaluation.CacheCoversCurrentTime)}");
            for (var index = 0; index < Math.Min(3, snapshot.SeekableRanges.Count); index++)
            {
                var range = snapshot.SeekableRanges[index];
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r2-cache-range sessionId={SessionId} index={index} start={FormatSeconds(range.Start)} end={FormatSeconds(range.End)}");
            }
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-r2-cache-state-parse-failed sessionId={SessionId} errorType={exception.GetType().Name}");
        }
    }

    private void HandleTrackList(IntPtr data, string reason)
    {
        try
        {
            var tracks = ParseTrackList(data);
            var updateSnapshot = new MpvTrackListSnapshot(tracks);
            var mergeResult = MergeTrackRegistry(updateSnapshot);
            var snapshot = mergeResult.Snapshot;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-list-updated sessionId={SessionId} audioCount={updateSnapshot.AudioTracks.Count} embeddedSubCount={updateSnapshot.EmbeddedSubtitleTracks.Count} externalSubTrackCount={updateSnapshot.ExternalSubtitleTracks.Count} selectedAid={FormatOptionalTrackId(updateSnapshot.SelectedAudioTrackId)} selectedSid={FormatOptionalTrackId(updateSnapshot.SelectedSubtitleTrackId)} reason={SanitizeLogPart(reason)}");
            if (_trackMenuPublished)
            {
                foreach (var track in mergeResult.AddedTracks.Where(IsMenuTrack))
                {
                    MpvPlaybackDiagnostics.Write(
                        $"mpv-r3-late-track-added sessionId={SessionId} type={NormalizeTrackKind(track.Kind)} trackId={track.Id}");
                }

                if (mergeResult.HasMenuChanges)
                {
                    RaiseTracksChanged(snapshot);
                }

                return;
            }

            StartTrackDiscoveryIfNeeded($"track-list-{SanitizeLogPart(reason)}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-list-parse-error sessionId={SessionId} errorType={exception.GetType().Name}");
        }
    }

    private void HandleAid(string? aid)
    {
        aid = string.IsNullOrWhiteSpace(aid) ? "unknown" : aid.Trim();
        if (string.Equals(_lastObservedAid, aid, StringComparison.Ordinal))
        {
            return;
        }

        _lastObservedAid = aid;
        var trackId = ParseObservedTrackId(aid);
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-audio-selected sessionId={SessionId} aid={FormatOptionalTrackId(trackId)}");
        AudioTrackChanged?.Invoke(this, new MpvSessionAudioTrackChangedEventArgs(SessionId, trackId));
    }

    private void HandleSid(string? sid)
    {
        sid = string.IsNullOrWhiteSpace(sid) ? "unknown" : sid.Trim();
        if (string.Equals(_lastObservedSid, sid, StringComparison.Ordinal))
        {
            return;
        }

        _lastObservedSid = sid;
        var trackId = ParseObservedTrackId(sid);
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-subtitle-selected sessionId={SessionId} sid={FormatOptionalTrackId(trackId)}");
        if (_embeddedSubtitleSwitchTargetTrackId.HasValue
            && trackId == _embeddedSubtitleSwitchTargetTrackId.Value
            && _embeddedSubtitleSwitchRequestId > 0)
        {
            var elapsedMs = _embeddedSubtitleSwitchStartedUtc == DateTime.MinValue
                ? -1
                : (long)Math.Max(0d, (DateTime.UtcNow - _embeddedSubtitleSwitchStartedUtc).TotalMilliseconds);
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-embedded-switch-sid-confirmed sessionId={SessionId} requestId={_embeddedSubtitleSwitchRequestId} sid={trackId.Value} elapsedMs={elapsedMs}");
        }

        SubtitleTrackChanged?.Invoke(this, new MpvSessionSubtitleTrackChangedEventArgs(SessionId, trackId));
    }

    private void RaiseTracksChanged(MpvTrackListSnapshot snapshot)
    {
        if (IsResumeSeekReadinessBlocked())
        {
            _tracksChangedWhileResumeBlocked = true;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-auto-track-select-delayed sessionId={SessionId} reason=resume-seek-pending");
            return;
        }

        TracksChanged?.Invoke(this, new MpvSessionTracksChangedEventArgs(SessionId, snapshot));
    }

    private void StartTrackDiscoveryIfNeeded(string reason)
    {
        if (_trackMenuPublished || _trackDiscoveryStarted || _disposed || _stopRequested)
        {
            return;
        }

        if (!_fileLoaded && !_playbackRestarted)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-discovery-deferred sessionId={SessionId} reason=waiting-for-file-loaded trigger={SanitizeLogPart(reason)}");
            return;
        }

        _trackDiscoveryStarted = true;
        _trackDiscoveryStartedUtc = DateTime.UtcNow;
        _lastTrackRegistryUpdateUtc = _lastTrackRegistryUpdateUtc == DateTime.MinValue
            ? _trackDiscoveryStartedUtc
            : _lastTrackRegistryUpdateUtc;
        var cancellation = new CancellationTokenSource();
        _trackDiscoveryCancellation = cancellation;
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-track-discovery-start sessionId={SessionId} reason={SanitizeLogPart(reason)}");
        _ = Task.Run(() => RunTrackDiscoveryAsync(cancellation.Token), CancellationToken.None);
    }

    private async Task RunTrackDiscoveryAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var interval in TrackDiscoveryProbeIntervals)
            {
                var delay = _trackDiscoveryStartedUtc + interval - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested || _disposed || _stopRequested)
                {
                    return;
                }

                RefreshTrackListFromProperty($"startup-probe-{(int)interval.TotalMilliseconds}ms");
            }

            while (!cancellationToken.IsCancellationRequested && !_disposed && !_stopRequested)
            {
                var elapsed = DateTime.UtcNow - _trackDiscoveryStartedUtc;
                var stableFor = DateTime.UtcNow - _lastTrackRegistryUpdateUtc;
                if (elapsed >= TrackDiscoveryMaxWait || stableFor >= TrackDiscoveryStableWindow)
                {
                    var reason = elapsed >= TrackDiscoveryMaxWait ? "max-wait" : "stable-window";
                    if (FinalizeTrackDiscovery(reason))
                    {
                        await RunLateTrackDiscoveryProbesAsync(DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
                    }

                    return;
                }

                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during stop/source switch/dispose.
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-discovery-failed sessionId={SessionId} errorType={exception.GetType().Name}");
            if (FinalizeTrackDiscovery("error"))
            {
                await RunLateTrackDiscoveryProbesAsync(DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool FinalizeTrackDiscovery(string reason)
    {
        if (_trackMenuPublished || _disposed || _stopRequested)
        {
            return false;
        }

        _trackMenuPublished = true;
        var snapshot = _lastTrackListSnapshot;
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-track-discovery-published sessionId={SessionId} reason={SanitizeLogPart(reason)} elapsedMs={(long)Math.Max(0d, (DateTime.UtcNow - _trackDiscoveryStartedUtc).TotalMilliseconds)} audioCount={snapshot.AudioTracks.Count} embeddedSubCount={snapshot.EmbeddedSubtitleTracks.Count} embeddedSubIds={FormatTrackIdList(snapshot.EmbeddedSubtitleTracks)} audioIds={FormatTrackIdList(snapshot.AudioTracks)}");
        RaiseTracksChanged(snapshot);
        return true;
    }

    private async Task RunLateTrackDiscoveryProbesAsync(DateTime publishedUtc, CancellationToken cancellationToken)
    {
        foreach (var interval in TrackDiscoveryLateProbeIntervals)
        {
            var delay = publishedUtc + interval - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested || _disposed || _stopRequested)
            {
                return;
            }

            var stage = $"after-{(int)interval.TotalSeconds}s";
            var result = RefreshTrackListFromProperty($"late-probe-{stage}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-discovery-late-probe sessionId={SessionId} stage={stage} result={result.ToString().ToLowerInvariant()}");
            var snapshot = _lastTrackListSnapshot;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-discovery-late-merge sessionId={SessionId} stage={stage} embeddedSubIds={FormatTrackIdList(snapshot.EmbeddedSubtitleTracks)} audioIds={FormatTrackIdList(snapshot.AudioTracks)}");
        }
    }

    private bool RefreshTrackListFromProperty(string reason)
    {
        if (!TryGetHandle(out var handle))
        {
            return false;
        }

        var pointer = IntPtr.Zero;
        var hasNodeContents = false;
        try
        {
            pointer = Marshal.AllocHGlobal(Marshal.SizeOf<MpvNode>());
            Marshal.StructureToPtr(default(MpvNode), pointer, false);
            var result = MpvNative.GetProperty(handle, "track-list", MpvFormat.Node, pointer);
            if (result < 0)
            {
                return false;
            }

            hasNodeContents = true;
            HandleTrackList(pointer, reason);
            return true;
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-list-parse-error sessionId={SessionId} errorType={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                if (hasNodeContents)
                {
                    try
                    {
                        MpvNative.FreeNodeContents(pointer);
                    }
                    catch
                    {
                    }
                }

                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    private async Task RefreshTracksAfterExternalSubtitleAsync()
    {
        try
        {
            await Task.Delay(250).ConfigureAwait(false);
            if (!_disposed)
            {
                RefreshTrackListFromProperty("external-subtitle-add");
            }
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-list-parse-error sessionId={SessionId} errorType={exception.GetType().Name}");
        }
    }

    private TrackRegistryMergeResult MergeTrackRegistry(MpvTrackListSnapshot updateSnapshot)
    {
        _trackRegistryUpdateSequence++;
        var updateSeq = _trackRegistryUpdateSequence;
        _lastTrackRegistryUpdateUtc = DateTime.UtcNow;
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-track-registry-merge-start sessionId={SessionId} updateSeq={updateSeq} audioCount={updateSnapshot.AudioTracks.Count} embeddedSubCount={updateSnapshot.EmbeddedSubtitleTracks.Count} externalSubCount={updateSnapshot.ExternalSubtitleTracks.Count}");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-track-discovery-update sessionId={SessionId} updateSeq={updateSeq} audioCount={updateSnapshot.AudioTracks.Count} embeddedSubCount={updateSnapshot.EmbeddedSubtitleTracks.Count} externalSubCount={updateSnapshot.ExternalSubtitleTracks.Count}");

        var updateKeys = updateSnapshot.Tracks
            .Select(GetTrackRegistryKey)
            .ToHashSet();
        var addedTracks = new List<MpvTrackInfo>();
        var updatedTracks = new List<MpvTrackInfo>();

        foreach (var track in updateSnapshot.Tracks.OrderBy(track => track.Id))
        {
            var key = GetTrackRegistryKey(track);
            if (_trackRegistry.TryGetValue(key, out var existing))
            {
                var merged = MergeTrackInfo(existing, track);
                _trackRegistry[key] = merged;
                if (existing.IsExternal != merged.IsExternal
                    && string.Equals(merged.Kind, "sub", StringComparison.OrdinalIgnoreCase))
                {
                    MpvPlaybackDiagnostics.Write(
                        $"mpv-r3-track-classification-changed sessionId={SessionId} trackId={track.Id} oldExternal={existing.IsExternal.ToString().ToLowerInvariant()} newExternal={merged.IsExternal.ToString().ToLowerInvariant()}");
                }

                if (!Equals(existing, merged))
                {
                    updatedTracks.Add(merged);
                    MpvPlaybackDiagnostics.Write(
                        $"mpv-r3-track-registry-updated sessionId={SessionId} type={NormalizeTrackKind(track.Kind)} trackId={track.Id}");
                }

                continue;
            }

            _trackRegistry[key] = track;
            addedTracks.Add(track);
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-registry-added sessionId={SessionId} type={NormalizeTrackKind(track.Kind)} trackId={track.Id}");
        }

        foreach (var staleTrack in _trackRegistry.Values
                     .Where(track => !updateKeys.Contains(GetTrackRegistryKey(track)))
                     .OrderBy(track => track.Id))
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-registry-stale sessionId={SessionId} type={NormalizeTrackKind(staleTrack.Kind)} trackId={staleTrack.Id}");
        }

        var selectedAid = ParseObservedTrackId(_lastObservedAid) ?? updateSnapshot.SelectedAudioTrackId;
        var selectedSid = ParseObservedTrackId(_lastObservedSid) ?? updateSnapshot.SelectedSubtitleTrackId;
        var registryTracks = _trackRegistry.Values
            .Select(track => ApplyRegistrySelection(track, selectedAid, selectedSid))
            .OrderBy(track => TrackSortGroup(track))
            .ThenBy(track => track.Id)
            .ToArray();
        var registrySnapshot = new MpvTrackListSnapshot(registryTracks);
        _lastTrackListSnapshot = registrySnapshot;

        var staleEmbeddedCount = _trackRegistry.Values.Count(
            track => string.Equals(track.Kind, "sub", StringComparison.OrdinalIgnoreCase)
                     && !track.IsExternal
                     && !updateKeys.Contains(GetTrackRegistryKey(track)));
        var staleTrackIds = _trackRegistry.Values
            .Where(track => IsMenuTrack(track) && !updateKeys.Contains(GetTrackRegistryKey(track)))
            .OrderBy(track => track.Id)
            .Select(track => track.Id.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        var staleEmbeddedSubIds = _trackRegistry.Values
            .Where(track => string.Equals(track.Kind, "sub", StringComparison.OrdinalIgnoreCase)
                            && !track.IsExternal
                            && !updateKeys.Contains(GetTrackRegistryKey(track)))
            .OrderBy(track => track.Id)
            .Select(track => track.Id.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-track-registry-result sessionId={SessionId} audioIds={FormatTrackIdList(registrySnapshot.AudioTracks)} embeddedSubIds={FormatTrackIdList(registrySnapshot.EmbeddedSubtitleTracks)} externalSubIds={FormatTrackIdList(registrySnapshot.ExternalSubtitleTracks)} staleIds={(staleTrackIds.Length == 0 ? "none" : string.Join(',', staleTrackIds))} staleEmbeddedSubIds={(staleEmbeddedSubIds.Length == 0 ? "none" : string.Join(',', staleEmbeddedSubIds))} audioCount={registrySnapshot.AudioTracks.Count} embeddedSubCount={registrySnapshot.EmbeddedSubtitleTracks.Count} staleEmbeddedSubCount={staleEmbeddedCount}");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r3-menu-projection-result sessionId={SessionId} audioCount={registrySnapshot.AudioTracks.Count} embeddedSubCount={registrySnapshot.EmbeddedSubtitleTracks.Count} embeddedSubIds={FormatTrackIdList(registrySnapshot.EmbeddedSubtitleTracks)} audioIds={FormatTrackIdList(registrySnapshot.AudioTracks)}");
        var hasMenuChanges = addedTracks.Any(IsMenuTrack) || updatedTracks.Any(IsMenuTrack);
        return new TrackRegistryMergeResult(registrySnapshot, addedTracks, updatedTracks, hasMenuChanges);
    }

    private sealed record TrackRegistryMergeResult(
        MpvTrackListSnapshot Snapshot,
        IReadOnlyList<MpvTrackInfo> AddedTracks,
        IReadOnlyList<MpvTrackInfo> UpdatedTracks,
        bool HasMenuChanges);

    private static MpvTrackInfo MergeTrackInfo(MpvTrackInfo existing, MpvTrackInfo update)
    {
        return update with
        {
            IsExternal = update.IsExternal,
            Title = string.IsNullOrWhiteSpace(update.Title) ? existing.Title : update.Title,
            Language = string.IsNullOrWhiteSpace(update.Language) ? existing.Language : update.Language,
            Codec = string.IsNullOrWhiteSpace(update.Codec) ? existing.Codec : update.Codec,
            IsSelected = update.IsSelected || existing.IsSelected,
            IsDefault = update.IsDefault || existing.IsDefault,
            IsForced = update.IsForced || existing.IsForced,
            IsDependent = update.IsDependent || existing.IsDependent,
            IsVisualImpaired = update.IsVisualImpaired || existing.IsVisualImpaired,
            IsHearingImpaired = update.IsHearingImpaired || existing.IsHearingImpaired
        };
    }

    private static MpvTrackInfo ApplyRegistrySelection(MpvTrackInfo track, int? selectedAid, int? selectedSid)
    {
        return track.Kind switch
        {
            "audio" => track with { IsSelected = selectedAid.HasValue && track.Id == selectedAid.Value },
            "sub" => track with { IsSelected = selectedSid.HasValue && track.Id == selectedSid.Value },
            _ => track
        };
    }

    private static (int Id, string Kind) GetTrackRegistryKey(MpvTrackInfo track)
    {
        return (track.Id, NormalizeTrackKind(track.Kind));
    }

    private static bool IsMenuTrack(MpvTrackInfo track)
    {
        return string.Equals(track.Kind, "audio", StringComparison.OrdinalIgnoreCase)
               || (string.Equals(track.Kind, "sub", StringComparison.OrdinalIgnoreCase) && !track.IsExternal);
    }

    private static int TrackSortGroup(MpvTrackInfo track)
    {
        return NormalizeTrackKind(track.Kind) switch
        {
            "video" => 0,
            "audio" => 1,
            "sub" => 2,
            _ => 3
        };
    }

    private static string FormatTrackIdList(IEnumerable<MpvTrackInfo> tracks)
    {
        var ids = tracks
            .OrderBy(track => track.Id)
            .Select(track => track.Id.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        return ids.Length == 0 ? "none" : string.Join(',', ids);
    }

    private void LogTrackListDelta(MpvTrackListSnapshot snapshot)
    {
        var previousIds = _lastTrackListSnapshot.Tracks
            .Select(track => (track.Id, Kind: NormalizeTrackKind(track.Kind), track.IsExternal))
            .ToHashSet();
        var currentIds = snapshot.Tracks
            .Select(track => (track.Id, Kind: NormalizeTrackKind(track.Kind), track.IsExternal))
            .ToHashSet();

        foreach (var track in snapshot.Tracks.OrderBy(track => track.Id))
        {
            var key = (track.Id, Kind: NormalizeTrackKind(track.Kind), track.IsExternal);
            if (previousIds.Contains(key))
            {
                continue;
            }

            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-added sessionId={SessionId} type={NormalizeTrackKind(track.Kind)} source={(track.IsExternal ? "external" : "embedded")} trackId={track.Id}");
        }

        foreach (var track in _lastTrackListSnapshot.Tracks.OrderBy(track => track.Id))
        {
            var key = (track.Id, Kind: NormalizeTrackKind(track.Kind), track.IsExternal);
            if (currentIds.Contains(key))
            {
                continue;
            }

            MpvPlaybackDiagnostics.Write(
                $"mpv-r3-track-removed sessionId={SessionId} type={NormalizeTrackKind(track.Kind)} source={(track.IsExternal ? "external" : "embedded")} trackId={track.Id}");
        }
    }

    private static MpvCacheStateSnapshot? ParseDemuxerCacheState(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return null;
        }

        var root = Marshal.PtrToStructure<MpvNode>(data);
        var map = ReadNodeMap(root);
        if (map.Count == 0)
        {
            return null;
        }

        return new MpvCacheStateSnapshot(
            DateTime.UtcNow,
            GetNodeDouble(map, "cache-duration"),
            GetNodeInt64(map, "file-cache-bytes"),
            GetNodeInt64(map, "fw-bytes"),
            GetNodeDouble(map, "reader-pts"),
            GetNodeDouble(map, "cache-end"),
            ReadSeekableRanges(map, "seekable-ranges"));
    }

    private static IReadOnlyList<MpvSeekableRangeSnapshot> ReadSeekableRanges(Dictionary<string, MpvNode> map, string key)
    {
        if (!map.TryGetValue(key, out var node)
            || node.Format != MpvFormat.NodeArray
            || node.Value == IntPtr.Zero)
        {
            return [];
        }

        var list = Marshal.PtrToStructure<MpvNodeList>(node.Value);
        if (list.Count <= 0 || list.Count > 256 || list.Values == IntPtr.Zero)
        {
            return [];
        }

        var result = new List<MpvSeekableRangeSnapshot>(Math.Min(list.Count, 256));
        var nodeSize = Marshal.SizeOf<MpvNode>();
        for (var index = 0; index < list.Count; index++)
        {
            var nodePointer = IntPtr.Add(list.Values, index * nodeSize);
            var rangeNode = Marshal.PtrToStructure<MpvNode>(nodePointer);
            var rangeMap = ReadNodeMap(rangeNode);
            var start = GetNodeDouble(rangeMap, "start");
            var end = GetNodeDouble(rangeMap, "end");
            if (start.HasValue && end.HasValue && end.Value >= start.Value)
            {
                result.Add(new MpvSeekableRangeSnapshot(start.Value, end.Value));
            }
        }

        return result;
    }

    private static IReadOnlyList<MpvTrackInfo> ParseTrackList(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return [];
        }

        var root = Marshal.PtrToStructure<MpvNode>(data);
        if (root.Format != MpvFormat.NodeArray || root.Value == IntPtr.Zero)
        {
            return [];
        }

        var list = Marshal.PtrToStructure<MpvNodeList>(root.Value);
        if (list.Count <= 0 || list.Count > 256 || list.Values == IntPtr.Zero)
        {
            return [];
        }

        var result = new List<MpvTrackInfo>(list.Count);
        var nodeSize = Marshal.SizeOf<MpvNode>();
        for (var index = 0; index < list.Count; index++)
        {
            var nodePointer = IntPtr.Add(list.Values, index * nodeSize);
            var node = Marshal.PtrToStructure<MpvNode>(nodePointer);
            var map = ReadNodeMap(node);
            var id = GetNodeInt64(map, "id");
            var kind = GetNodeString(map, "type");
            if (!id.HasValue || id.Value < 0 || string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            var externalFileName = GetNodeString(map, "external-filename");
            var isExternal = GetNodeFlag(map, "external") || !string.IsNullOrWhiteSpace(externalFileName);
            result.Add(new MpvTrackInfo(
                (int)id.Value,
                NormalizeTrackKind(kind),
                GetNodeString(map, "title"),
                GetNodeString(map, "lang"),
                GetNodeString(map, "codec"),
                isExternal,
                GetNodeFlag(map, "selected"),
                GetNodeFlag(map, "default"),
                GetNodeFlag(map, "forced"),
                GetNodeFlag(map, "dependent"),
                GetNodeFlag(map, "visual-impaired"),
                GetNodeFlag(map, "hearing-impaired")));
        }

        return result;
    }

    private static Dictionary<string, MpvNode> ReadNodeMap(MpvNode node)
    {
        if (node.Format != MpvFormat.NodeMap || node.Value == IntPtr.Zero)
        {
            return [];
        }

        var list = Marshal.PtrToStructure<MpvNodeList>(node.Value);
        if (list.Count <= 0 || list.Count > 128 || list.Keys == IntPtr.Zero || list.Values == IntPtr.Zero)
        {
            return [];
        }

        var result = new Dictionary<string, MpvNode>(StringComparer.OrdinalIgnoreCase);
        var nodeSize = Marshal.SizeOf<MpvNode>();
        for (var index = 0; index < list.Count; index++)
        {
            var keyPointer = Marshal.ReadIntPtr(list.Keys, index * IntPtr.Size);
            var key = Marshal.PtrToStringUTF8(keyPointer);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var valuePointer = IntPtr.Add(list.Values, index * nodeSize);
            result[key] = Marshal.PtrToStructure<MpvNode>(valuePointer);
        }

        return result;
    }

    private static string? GetNodeString(Dictionary<string, MpvNode> map, string key)
    {
        if (!map.TryGetValue(key, out var node) || node.Format != MpvFormat.String || node.Value == IntPtr.Zero)
        {
            return null;
        }

        return Marshal.PtrToStringUTF8(node.Value);
    }

    private static bool GetNodeFlag(Dictionary<string, MpvNode> map, string key)
    {
        return map.TryGetValue(key, out var node) && node.Format == MpvFormat.Flag && node.Value.ToInt64() != 0;
    }

    private static long? GetNodeInt64(Dictionary<string, MpvNode> map, string key)
    {
        return map.TryGetValue(key, out var node) && node.Format == MpvFormat.Int64
            ? node.Value.ToInt64()
            : null;
    }

    private static double? GetNodeDouble(Dictionary<string, MpvNode> map, string key)
    {
        if (!map.TryGetValue(key, out var node))
        {
            return null;
        }

        if (node.Format == MpvFormat.Double)
        {
            var value = BitConverter.Int64BitsToDouble(node.Value.ToInt64());
            return double.IsFinite(value) ? value : null;
        }

        return node.Format == MpvFormat.Int64 ? node.Value.ToInt64() : null;
    }

    private static string? ReadPropertyStringValue(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return null;
        }

        var pointer = Marshal.ReadIntPtr(data);
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);
    }

    private static int? ParseObservedTrackId(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var trackId)
            ? trackId
            : null;
    }

    private static string FormatOptionalTrackId(int? trackId)
    {
        return trackId.HasValue
            ? trackId.Value.ToString(CultureInfo.InvariantCulture)
            : "no";
    }

    private static string NormalizeTrackKind(string? kind)
    {
        if (string.Equals(kind, "audio", StringComparison.OrdinalIgnoreCase))
        {
            return "audio";
        }

        if (string.Equals(kind, "sub", StringComparison.OrdinalIgnoreCase))
        {
            return "sub";
        }

        if (string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        return SanitizeLogPart(kind ?? "unknown");
    }

    private void ConfigureRequestOptions(MpvLoadRequest request)
    {
        var handle = GetRequiredHandle();
        var auth = "none";
        if (!request.IsLocalFile
            && request.ProtocolType == ProtocolType.WebDav
            && !string.IsNullOrWhiteSpace(request.Username))
        {
            var credentialBytes = Encoding.UTF8.GetBytes($"{request.Username}:{request.Password}");
            var authorization = Convert.ToBase64String(credentialBytes);
            _ = TrySetPropertyString(handle, "http-header-fields", $"Authorization: Basic {authorization}")
                || TrySetOptionString("http-header-fields", $"Authorization: Basic {authorization}");
            auth = "basic";
        }
        else
        {
            _ = TrySetPropertyString(handle, "http-header-fields", string.Empty)
                || TrySetOptionString("http-header-fields", string.Empty);
        }

        var cacheOnDisk = !request.IsLocalFile && request.ProtocolType == ProtocolType.WebDav ? "yes" : "no";
        _ = TrySetOptionString("cache", "yes");
        _ = TrySetOptionString("cache-on-disk", cacheOnDisk);
        _ = TrySetOptionString("network-timeout", "20");
        if (request.ProtocolType == ProtocolType.WebDav && !request.IsLocalFile)
        {
            var sessionCacheDir = TryCreateMpvSessionCacheDirectory(request);
            if (!string.IsNullOrWhiteSpace(sessionCacheDir))
            {
                _ = TrySetOptionString("demuxer-cache-dir", sessionCacheDir);
            }
            else
            {
                _ = TrySetOptionString("demuxer-cache-dir", string.Empty);
            }

            _ = TrySetOptionString("cache-pause", "yes");
            _ = TrySetOptionString("cache-pause-initial", "no");
            _ = TrySetOptionString("cache-pause-wait", "2");
            _ = TrySetOptionString("cache-secs", "300");
            _ = TrySetOptionString("demuxer-readahead-secs", "300");
            _ = TrySetOptionString("demuxer-max-bytes", "2147483648");
            _ = TrySetOptionString("demuxer-max-back-bytes", "536870912");
            MpvPlaybackDiagnostics.Write(
                $"mpv-r2-cache-options sessionId={SessionId} cache=yes cacheOnDisk={cacheOnDisk} cacheSecs=300 readaheadSecs=300 maxBytes=2147483648 maxBackBytes=536870912");
        }
        else
        {
            _ = TrySetOptionString("demuxer-cache-dir", string.Empty);
            MpvPlaybackDiagnostics.Write($"mpv-r6-session-cache-dir-cleared sessionId={SessionId} reason=non-webdav-or-local");
        }

        MpvPlaybackDiagnostics.Write($"mpv-core-request-options sessionId={SessionId} auth={auth} cacheOnDisk={cacheOnDisk}");
    }

    private string? TryCreateMpvSessionCacheDirectory(MpvLoadRequest request)
    {
        try
        {
            var sessionKey = SessionId.ToString("N")[..12];
            var root = Path.Combine(AppPaths.GetVideoCacheDirectory(), MpvSessionCacheRootName);
            var directory = Path.Combine(root, sessionKey);
            Directory.CreateDirectory(directory);

            var markerPath = Path.Combine(directory, MpvSessionActiveMarkerName);
            var marker = string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                {"version":1,"sessionId":"{{SessionId}}","processId":{{Environment.ProcessId}},"mediaFileId":{{request.MediaFileId}},"sourceConnectionId":{{request.SourceConnectionId}},"createdUtc":"{{DateTime.UtcNow:O}}"}
                """);
            File.WriteAllText(markerPath, marker, Encoding.UTF8);
            _mpvSessionCacheDirectory = directory;
            _mpvSessionCacheActiveMarkerPath = markerPath;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r6-session-cache-dir-set sessionId={SessionId} key={sessionKey} dirLength={directory.Length}");
            return directory;
        }
        catch (Exception exception)
        {
            _mpvSessionCacheDirectory = null;
            _mpvSessionCacheActiveMarkerPath = null;
            MpvPlaybackDiagnostics.Write(
                $"mpv-r6-session-cache-dir-failed sessionId={SessionId} errorType={exception.GetType().Name}");
            return null;
        }
    }

    private void ReleaseMpvSessionCacheMarker(string reason)
    {
        var markerPath = _mpvSessionCacheActiveMarkerPath;
        var directory = _mpvSessionCacheDirectory;
        _mpvSessionCacheActiveMarkerPath = null;
        _mpvSessionCacheDirectory = null;
        if (string.IsNullOrWhiteSpace(markerPath))
        {
            return;
        }

        try
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }

            MpvPlaybackDiagnostics.Write(
                $"mpv-r6-session-cache-marker-released sessionId={SessionId} reason={SanitizeLogPart(reason)} dirLength={directory?.Length ?? 0}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-r6-session-cache-marker-release-failed sessionId={SessionId} reason={SanitizeLogPart(reason)} errorType={exception.GetType().Name}");
        }
    }

    private void ConfigureHwdecOption()
    {
        var rawValue = Environment.GetEnvironmentVariable(HwdecEnvironmentVariable);
        var value = DefaultHwdecValue;
        var valueKind = "default";
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            if (string.Equals(rawValue, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, "false", StringComparison.OrdinalIgnoreCase))
            {
                value = "no";
                valueKind = "off";
            }
            else if (string.Equals(rawValue, "auto-safe", StringComparison.OrdinalIgnoreCase))
            {
                value = "auto-safe";
                valueKind = "auto-safe";
            }
            else
            {
                valueKind = "invalid";
            }
        }

        _ = TrySetOptionString("hwdec", value);
        MpvPlaybackDiagnostics.Write($"mpv-hwdec-config source=env valueKind={valueKind}");
        MpvPlaybackDiagnostics.Write($"mpv-hwdec-effective value={value}");
        MpvPlaybackDiagnostics.Write($"mpv-r2-hwdec-config sessionId={SessionId} source=env valueKind={valueKind}");
        MpvPlaybackDiagnostics.Write($"mpv-r2-hwdec-effective sessionId={SessionId} value={value}");
    }

    private void LogVideoFormatSummary()
    {
        var handle = GetHandleOrDefault();
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var codec = TryGetPropertyString(handle, "video-codec") ?? "unknown";
        var hwdec = TryGetPropertyString(handle, "hwdec-current") ?? "unknown";
        var pixfmt = TryGetPropertyString(handle, "video-params/pixelformat") ?? "unknown";
        var width = TryGetPropertyString(handle, "video-params/w") ?? "unknown";
        var height = TryGetPropertyString(handle, "video-params/h") ?? "unknown";
        MpvPlaybackDiagnostics.Write(
            $"mpv-r2-video-format sessionId={SessionId} codec={SanitizeLogPart(codec)} hwdec={SanitizeLogPart(hwdec)} pixfmt={SanitizeLogPart(pixfmt)} width={SanitizeLogPart(width)} height={SanitizeLogPart(height)}");
    }

    private static string? TryGetPropertyString(IntPtr handle, string name)
    {
        try
        {
            var pointer = MpvNative.GetPropertyString(handle, name);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(pointer);
            }
            finally
            {
                MpvNative.Free(pointer);
            }
        }
        catch
        {
            return null;
        }
    }

    private bool TrySetOptionString(string name, string value)
    {
        var handle = GetHandleOrDefault();
        return handle != IntPtr.Zero && TrySetOptionString(handle, name, value);
    }

    private static bool TrySetOptionString(IntPtr handle, string name, string value)
    {
        try
        {
            return MpvNative.SetOptionString(handle, name, value) >= 0;
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-core-set-option-failed name={name} errorType={exception.GetType().Name}");
            return false;
        }
    }

    private static bool TrySetPropertyString(IntPtr handle, string name, string value)
    {
        try
        {
            return MpvNative.SetPropertyString(handle, name, value) >= 0;
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-core-set-property-failed name={name} errorType={exception.GetType().Name}");
            return false;
        }
    }

    private IntPtr GetRequiredHandle()
    {
        var handle = GetHandleOrDefault();
        if (handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(MpvPlayerSession));
        }

        return handle;
    }

    private IntPtr GetHandleOrDefault()
    {
        lock (_syncRoot)
        {
            return _disposed ? IntPtr.Zero : _handle;
        }
    }

    private bool TryGetHandle(out IntPtr handle)
    {
        handle = GetHandleOrDefault();
        return handle != IntPtr.Zero;
    }

    private void CancelResumeSeekWatch()
    {
        var cancellation = _resumeSeekWatchCancellation;
        _resumeSeekWatchCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        catch
        {
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void CancelEmbeddedSubtitleSwitchWatch()
    {
        var cancellation = _embeddedSubtitleSwitchWatchCancellation;
        _embeddedSubtitleSwitchWatchCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        catch
        {
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void CancelTrackDiscovery()
    {
        var cancellation = _trackDiscoveryCancellation;
        _trackDiscoveryCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        catch
        {
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private static bool IsWebDavRequest(MpvLoadRequest request)
    {
        return !request.IsLocalFile && request.ProtocolType == ProtocolType.WebDav;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MpvPlayerSession));
        }
    }

    private static bool ReadFlag(IntPtr data)
    {
        return Marshal.ReadInt32(data) != 0;
    }

    private static string FormatSeconds(double seconds)
    {
        return seconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int InvokeCommand(IntPtr handle, params string[] args)
    {
        var stringPointers = new IntPtr[args.Length];
        var argv = IntPtr.Zero;
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(args[i] + '\0');
                var pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                stringPointers[i] = pointer;
            }

            argv = Marshal.AllocHGlobal(IntPtr.Size * (args.Length + 1));
            for (var i = 0; i < stringPointers.Length; i++)
            {
                Marshal.WriteIntPtr(argv, i * IntPtr.Size, stringPointers[i]);
            }

            Marshal.WriteIntPtr(argv, args.Length * IntPtr.Size, IntPtr.Zero);
            return MpvNative.Command(handle, argv);
        }
        finally
        {
            if (argv != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(argv);
            }

            foreach (var pointer in stringPointers)
            {
                if (pointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pointer);
                }
            }
        }
    }

    private static int InvokeCommandAsync(IntPtr handle, ulong replyUserData, params string[] args)
    {
        var stringPointers = new IntPtr[args.Length];
        var argv = IntPtr.Zero;
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(args[i] + '\0');
                var pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                stringPointers[i] = pointer;
            }

            argv = Marshal.AllocHGlobal(IntPtr.Size * (args.Length + 1));
            for (var i = 0; i < stringPointers.Length; i++)
            {
                Marshal.WriteIntPtr(argv, i * IntPtr.Size, stringPointers[i]);
            }

            Marshal.WriteIntPtr(argv, args.Length * IntPtr.Size, IntPtr.Zero);
            return MpvNative.CommandAsync(handle, replyUserData, argv);
        }
        finally
        {
            if (argv != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(argv);
            }

            foreach (var pointer in stringPointers)
            {
                if (pointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pointer);
                }
            }
        }
    }

    private static string FormatMpvError(int errorCode)
    {
        var pointer = MpvNative.ErrorString(errorCode);
        return Marshal.PtrToStringUTF8(pointer) ?? errorCode.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatNullableBool(bool? value)
    {
        return value.HasValue ? value.Value.ToString().ToLowerInvariant() : "unknown";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeLogPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '_');
        }

        return builder.ToString();
    }
}
