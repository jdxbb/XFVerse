using System.Globalization;
using MediaLibrary.App.Helpers;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Playback.Mpv.Core;

public sealed class MpvPlaybackEngineAdapter : IPlaybackEngine, IPlaybackEngineFeatureFlags
{
    private readonly MpvPlayerSessionFactory _sessionFactory;
    private readonly object _syncRoot = new();
    private MpvPlayerSession? _currentSession;
    private Guid _currentSessionId;
    private IntPtr _hostHandle;
    private bool _disposed;
    private TimeSpan _duration = TimeSpan.Zero;
    private TimeSpan _position = TimeSpan.Zero;
    private bool _isPlaying;
    private bool _isBuffering;
    private bool? _lastPausedState;

    public MpvPlaybackEngineAdapter(MpvPlayerSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public TimeSpan Duration => _duration;

    public TimeSpan Position => _position;

    public bool IsPlaying => _isPlaying;

    public bool IsBuffering => _isBuffering;

    public bool DefersTrackFeatures => false;

    public bool LoadReturnsOnCommandSubmitted => true;

    public IReadOnlyList<PlaybackAudioTrackItem> AudioTracks { get; private set; } = [];

    public IReadOnlyList<PlaybackSubtitleItem> SubtitleTracks { get; private set; } = [];

    public event EventHandler? Opening;

    public event EventHandler? Playing;

    public event EventHandler? Paused;

    public event EventHandler<PlaybackBufferingEventArgs>? Buffering;

    public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

    public event EventHandler<PlaybackDurationChangedEventArgs>? DurationChanged;

    public event EventHandler? EndReached;

    public event EventHandler<PlaybackEngineErrorEventArgs>? EncounteredError;

    public event EventHandler? TracksChanged;

    public event EventHandler<PlaybackSubtitleTrackChangedEventArgs>? SubtitleTrackChanged;

    public event EventHandler<PlaybackAudioTrackChangedEventArgs>? AudioTrackChanged;

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
            _hostHandle = hostHandle;
        }

        return Task.CompletedTask;
    }

    public async Task LoadAsync(PlaybackLoadRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var hostHandle = _hostHandle;
        if (hostHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Playback host is not ready.");
        }

        var oldSession = DetachCurrentSession("load-replace");
        if (oldSession is not null)
        {
            _ = DisposeSessionInBackground(oldSession);
        }

        var session = _sessionFactory.Create();
        Subscribe(session);
        SetCurrentSession(session);

        try
        {
            await session.InitializeAsync(hostHandle, cancellationToken);
            await session.LoadAsync(
                new MpvLoadRequest
                {
                    MediaFileId = request.MediaFileId,
                    SourceConnectionId = request.SourceConnectionId,
                    PlaybackUrl = request.PlaybackUrl,
                    ProtocolType = request.ProtocolType,
                    Username = request.Username,
                    Password = request.Password,
                    IsLocalFile = request.IsLocalFile,
                    StartPositionSeconds = request.StartPositionSeconds,
                    FileSize = request.FileSize,
                    VideoCodec = request.VideoCodec,
                    ResolutionWidth = request.ResolutionWidth,
                    ResolutionHeight = request.ResolutionHeight
                },
                cancellationToken);
        }
        catch
        {
            DetachSessionIfCurrent(session, "load-failed");
            _ = DisposeSessionInBackground(session);
            throw;
        }
    }

    public void Play()
    {
        GetCurrentSession()?.Play();
    }

    public void Pause()
    {
        GetCurrentSession()?.Pause();
    }

    public void Stop()
    {
        var session = DetachCurrentSession("stop");
        if (session is null)
        {
            return;
        }

        MpvPlaybackDiagnostics.Write($"mpv-r4-stop-detach sessionId={session.SessionId}");
        _ = DisposeSessionInBackground(session);
    }

    public void Seek(TimeSpan position)
    {
        GetCurrentSession()?.Seek(position);
    }

    public void SetVolume(int volume)
    {
        GetCurrentSession()?.SetVolume(volume);
    }

    public void SetBrightness(int brightness)
    {
        GetCurrentSession()?.SetBrightness(brightness);
    }

    public void SetMute(bool muted)
    {
        GetCurrentSession()?.SetMute(muted);
    }

    public Task<bool> SetAudioTrackAsync(int? trackId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = GetCurrentSession();
        return Task.FromResult(session is not null && session.SetAudioTrack(trackId));
    }

    public Task<bool> SetSubtitleTrackAsync(int? trackId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = GetCurrentSession();
        return Task.FromResult(session is not null && session.SetSubtitleTrack(trackId));
    }

    public Task<bool> AddExternalSubtitleAsync(
        string playbackUrl,
        string username,
        string password,
        bool select,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = GetCurrentSession();
        return Task.FromResult(session is not null && session.AddExternalSubtitle(playbackUrl, username, password, select));
    }

    public Task ClearSubtitleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = GetCurrentSession()?.SetSubtitleTrack(null);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        MpvPlayerSession? session;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                MpvPlaybackDiagnostics.Write("mpv-r4-dispose-complete adapter=true result=already-disposed");
                return;
            }

            _disposed = true;
            session = DetachCurrentSessionUnderLock("dispose");
        }

        if (session is null)
        {
            MpvPlaybackDiagnostics.Write("mpv-r4-dispose-complete adapter=true result=no-session");
            return;
        }

        MpvPlaybackDiagnostics.Write($"mpv-core-adapter-dispose-detached sessionId={session.SessionId}");
        MpvPlaybackDiagnostics.Write($"mpv-r4-dispose-detach adapter=true sessionId={session.SessionId}");
        _ = DisposeSessionInBackground(session);
    }

    private static async Task DisposeSessionInBackground(MpvPlayerSession session)
    {
        try
        {
            await session.DisposeAsync().AsTask().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write(
                $"mpv-core-adapter-dispose-background-failed sessionId={session.SessionId} errorType={exception.GetType().Name}");
        }
    }

    private void SetCurrentSession(MpvPlayerSession session)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _currentSession = session;
            _currentSessionId = session.SessionId;
            _duration = TimeSpan.Zero;
            _position = TimeSpan.Zero;
            _isPlaying = false;
            _isBuffering = false;
            _lastPausedState = null;
            AudioTracks = [];
            SubtitleTracks = [];
        }
    }

    private MpvPlayerSession? DetachCurrentSession(string reason)
    {
        lock (_syncRoot)
        {
            return DetachCurrentSessionUnderLock(reason);
        }
    }

    private void DetachSessionIfCurrent(MpvPlayerSession session, string reason)
    {
        var detached = false;
        lock (_syncRoot)
        {
            if (_currentSessionId == session.SessionId)
            {
                _ = DetachCurrentSessionUnderLock(reason);
                detached = true;
            }
        }

        if (!detached)
        {
            Unsubscribe(session);
        }
    }

    private MpvPlayerSession? DetachCurrentSessionUnderLock(string reason)
    {
        var previous = _currentSession;
        _currentSession = null;
        _currentSessionId = Guid.Empty;
        _duration = TimeSpan.Zero;
        _position = TimeSpan.Zero;
        _isPlaying = false;
        _isBuffering = false;
        _lastPausedState = null;
        AudioTracks = [];
        SubtitleTracks = [];

        if (previous is not null)
        {
            Unsubscribe(previous);
            MpvPlaybackDiagnostics.Write(
                $"mpv-r4-session-detached sessionId={previous.SessionId} reason={SanitizeLogPart(reason)}");
        }

        return previous;
    }

    private MpvPlayerSession? GetCurrentSession()
    {
        lock (_syncRoot)
        {
            return _disposed ? null : _currentSession;
        }
    }

    private bool IsCurrentSession(Guid sessionId)
    {
        lock (_syncRoot)
        {
            return !_disposed && _currentSessionId == sessionId;
        }
    }

    private void Subscribe(MpvPlayerSession session)
    {
        session.Opening += OnSessionOpening;
        session.PlaybackRestarted += OnSessionPlaybackRestarted;
        session.PositionChanged += OnSessionPositionChanged;
        session.DurationChanged += OnSessionDurationChanged;
        session.StateChanged += OnSessionStateChanged;
        session.EndFile += OnSessionEndFile;
        session.Error += OnSessionError;
        session.TracksChanged += OnSessionTracksChanged;
        session.SubtitleTrackChanged += OnSessionSubtitleTrackChanged;
        session.AudioTrackChanged += OnSessionAudioTrackChanged;
    }

    private void Unsubscribe(MpvPlayerSession session)
    {
        session.Opening -= OnSessionOpening;
        session.PlaybackRestarted -= OnSessionPlaybackRestarted;
        session.PositionChanged -= OnSessionPositionChanged;
        session.DurationChanged -= OnSessionDurationChanged;
        session.StateChanged -= OnSessionStateChanged;
        session.EndFile -= OnSessionEndFile;
        session.Error -= OnSessionError;
        session.TracksChanged -= OnSessionTracksChanged;
        session.SubtitleTrackChanged -= OnSessionSubtitleTrackChanged;
        session.AudioTrackChanged -= OnSessionAudioTrackChanged;
    }

    private void OnSessionOpening(object? sender, MpvSessionEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        Opening?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionPlaybackRestarted(object? sender, MpvSessionEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        _isPlaying = true;
        _isBuffering = false;
        Playing?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionPositionChanged(object? sender, MpvSessionPositionChangedEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        _position = e.Position;
        PositionChanged?.Invoke(this, new PlaybackPositionChangedEventArgs(e.Position));
    }

    private void OnSessionDurationChanged(object? sender, MpvSessionDurationChangedEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        _duration = e.Duration;
        DurationChanged?.Invoke(this, new PlaybackDurationChangedEventArgs(e.Duration));
    }

    private void OnSessionStateChanged(object? sender, MpvSessionStateChangedEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        _isPlaying = e.IsPlaying;
        _isBuffering = e.IsBuffering;
        if (e.IsBuffering || e.PausedForCache.HasValue)
        {
            Buffering?.Invoke(this, new PlaybackBufferingEventArgs(e.BufferingPercent, e.PausedForCache));
        }

        if (_lastPausedState != e.IsPaused)
        {
            _lastPausedState = e.IsPaused;
            if (e.IsPaused)
            {
                Paused?.Invoke(this, EventArgs.Empty);
            }
            else if (e.IsPlaying)
            {
                Playing?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnSessionEndFile(object? sender, MpvSessionEndFileEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        if (e.Reason == 0)
        {
            EndReached?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSessionError(object? sender, MpvSessionErrorEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        EncounteredError?.Invoke(this, new PlaybackEngineErrorEventArgs(e.ErrorType, e.Message));
    }

    private void OnSessionTracksChanged(object? sender, MpvSessionTracksChangedEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        AudioTracks = e.Snapshot.AudioTracks
            .GroupBy(track => track.Id)
            .Select(group => group.First())
            .Select(ToPlaybackAudioTrack)
            .ToArray();
        SubtitleTracks = e.Snapshot.EmbeddedSubtitleTracks
            .Select(ToPlaybackSubtitleTrack)
            .ToArray();
        TracksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionSubtitleTrackChanged(object? sender, MpvSessionSubtitleTrackChangedEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        SubtitleTracks = SubtitleTracks
            .Select(track =>
            {
                track.IsAutoLoaded = e.TrackId.HasValue && track.TrackId == e.TrackId.Value;
                return track;
            })
            .ToArray();
        SubtitleTrackChanged?.Invoke(this, new PlaybackSubtitleTrackChangedEventArgs(e.TrackId));
    }

    private void OnSessionAudioTrackChanged(object? sender, MpvSessionAudioTrackChangedEventArgs e)
    {
        if (!TryAcceptEvent(e.SessionId))
        {
            return;
        }

        AudioTracks = AudioTracks
            .Select(track =>
            {
                track.IsSelected = e.TrackId.HasValue && track.TrackId == e.TrackId.Value;
                return track;
            })
            .ToArray();
        AudioTrackChanged?.Invoke(this, new PlaybackAudioTrackChangedEventArgs(e.TrackId));
    }

    private bool TryAcceptEvent(Guid sessionId)
    {
        Guid currentSessionId;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-r4-event-discarded oldSession={sessionId} currentSession={Guid.Empty} reason=disposed-session");
                return false;
            }

            currentSessionId = _currentSessionId;
        }

        if (sessionId == currentSessionId)
        {
            return true;
        }

        MpvPlaybackDiagnostics.Write(
            $"mpv-core-event-discarded oldSession={sessionId} currentSession={currentSessionId}");
        MpvPlaybackDiagnostics.Write(
            $"mpv-r4-event-discarded oldSession={sessionId} currentSession={currentSessionId} reason=stale-session");
        return false;
    }

    private static string SanitizeLogPart(string value)
    {
        return value.Replace(' ', '-').Replace('\r', '-').Replace('\n', '-');
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MpvPlaybackEngineAdapter));
        }
    }

    private static string FormatOptionalTrackId(int? trackId)
    {
        return trackId.HasValue ? trackId.Value.ToString(CultureInfo.InvariantCulture) : "none";
    }

    private static PlaybackAudioTrackItem ToPlaybackAudioTrack(MpvTrackInfo track)
    {
        var title = BuildTrackName(track, "音轨");
        return new PlaybackAudioTrackItem
        {
            DisplayName = title,
            OriginalName = title,
            TooltipText = BuildTrackTooltip(track),
            TrackId = track.Id,
            IsSelected = track.IsSelected,
            Priority = track.Id
        };
    }

    private static PlaybackSubtitleItem ToPlaybackSubtitleTrack(MpvTrackInfo track)
    {
        var title = BuildTrackName(track, "字幕");
        return new PlaybackSubtitleItem
        {
            DisplayName = $"内嵌：{title}",
            OriginalName = title,
            TooltipText = BuildTrackTooltip(track),
            UniqueKey = $"embedded:{track.Id}",
            Type = PlaybackSubtitleType.EmbeddedTrack,
            TrackId = track.Id,
            FileName = title,
            MatchType = SubtitleMatchType.Unknown,
            Priority = track.Id,
            IsPreferred = track.IsDefault,
            IsAutoLoaded = track.IsSelected
        };
    }

    private static string BuildTrackName(MpvTrackInfo track, string fallbackPrefix)
    {
        var parts = new[] { track.Language, track.Title, track.Codec }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? $"{fallbackPrefix} {track.Id}" : string.Join(" / ", parts);
    }

    private static string BuildTrackTooltip(MpvTrackInfo track)
    {
        var parts = new List<string>
        {
            $"id={track.Id}",
            $"type={track.Kind}"
        };

        if (!string.IsNullOrWhiteSpace(track.Language))
        {
            parts.Add($"lang={track.Language}");
        }

        if (!string.IsNullOrWhiteSpace(track.Codec))
        {
            parts.Add($"codec={track.Codec}");
        }

        if (track.IsDefault)
        {
            parts.Add("default=true");
        }

        if (track.IsForced)
        {
            parts.Add("forced=true");
        }

        return string.Join(Environment.NewLine, parts);
    }
}
