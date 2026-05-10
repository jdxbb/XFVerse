using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Playback;

public interface IPlaybackEngine : IDisposable
{
    TimeSpan Duration { get; }

    TimeSpan Position { get; }

    bool IsPlaying { get; }

    bool IsBuffering { get; }

    IReadOnlyList<PlaybackAudioTrackItem> AudioTracks { get; }

    IReadOnlyList<PlaybackSubtitleItem> SubtitleTracks { get; }

    event EventHandler? Opening;

    event EventHandler? Playing;

    event EventHandler? Paused;

    event EventHandler<PlaybackBufferingEventArgs>? Buffering;

    event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

    event EventHandler<PlaybackDurationChangedEventArgs>? DurationChanged;

    event EventHandler? EndReached;

    event EventHandler<PlaybackEngineErrorEventArgs>? EncounteredError;

    event EventHandler? TracksChanged;

    event EventHandler<PlaybackSubtitleTrackChangedEventArgs>? SubtitleTrackChanged;

    event EventHandler<PlaybackAudioTrackChangedEventArgs>? AudioTrackChanged;

    Task InitializeAsync(IntPtr hostHandle, CancellationToken cancellationToken = default);

    Task LoadAsync(PlaybackLoadRequest request, CancellationToken cancellationToken = default);

    void Play();

    void Pause();

    void Stop();

    void Seek(TimeSpan position);

    void SetVolume(int volume);

    void SetBrightness(int brightness);

    void SetMute(bool muted);

    Task<bool> SetAudioTrackAsync(int? trackId, CancellationToken cancellationToken = default);

    Task<bool> SetSubtitleTrackAsync(int? trackId, CancellationToken cancellationToken = default);

    Task<bool> AddExternalSubtitleAsync(
        string playbackUrl,
        string username,
        string password,
        bool select,
        CancellationToken cancellationToken = default);

    Task ClearSubtitleAsync(CancellationToken cancellationToken = default);
}
