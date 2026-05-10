namespace MediaLibrary.App.Playback.Mpv.Core;

public sealed class MpvPlaybackState
{
    public TimeSpan Duration { get; init; }

    public TimeSpan Position { get; init; }

    public bool IsPlaying { get; init; }

    public bool IsPaused { get; init; }

    public bool IsBuffering { get; init; }

    public double BufferingPercent { get; init; }

    public bool? PausedForCache { get; init; }
}
