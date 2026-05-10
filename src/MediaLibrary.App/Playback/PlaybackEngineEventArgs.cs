namespace MediaLibrary.App.Playback;

public sealed class PlaybackBufferingEventArgs : EventArgs
{
    public PlaybackBufferingEventArgs(double percent, bool? pausedForCache = null)
    {
        Percent = percent;
        PausedForCache = pausedForCache;
    }

    public double Percent { get; }

    public bool? PausedForCache { get; }
}

public sealed class PlaybackPositionChangedEventArgs : EventArgs
{
    public PlaybackPositionChangedEventArgs(TimeSpan position)
    {
        Position = position;
    }

    public TimeSpan Position { get; }
}

public sealed class PlaybackDurationChangedEventArgs : EventArgs
{
    public PlaybackDurationChangedEventArgs(TimeSpan duration)
    {
        Duration = duration;
    }

    public TimeSpan Duration { get; }
}

public sealed class PlaybackEngineErrorEventArgs : EventArgs
{
    public PlaybackEngineErrorEventArgs(string errorType, string? message = null)
    {
        ErrorType = errorType;
        Message = message;
    }

    public string ErrorType { get; }

    public string? Message { get; }
}

public sealed class PlaybackSubtitleTrackChangedEventArgs : EventArgs
{
    public PlaybackSubtitleTrackChangedEventArgs(int? trackId)
    {
        TrackId = trackId;
    }

    public int? TrackId { get; }
}

public sealed class PlaybackAudioTrackChangedEventArgs : EventArgs
{
    public PlaybackAudioTrackChangedEventArgs(int? trackId)
    {
        TrackId = trackId;
    }

    public int? TrackId { get; }
}
