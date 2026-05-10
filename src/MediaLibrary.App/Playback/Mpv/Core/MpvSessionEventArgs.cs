namespace MediaLibrary.App.Playback.Mpv.Core;

public class MpvSessionEventArgs : EventArgs
{
    public MpvSessionEventArgs(Guid sessionId)
    {
        SessionId = sessionId;
    }

    public Guid SessionId { get; }
}

public sealed class MpvSessionPositionChangedEventArgs : MpvSessionEventArgs
{
    public MpvSessionPositionChangedEventArgs(Guid sessionId, TimeSpan position)
        : base(sessionId)
    {
        Position = position;
    }

    public TimeSpan Position { get; }
}

public sealed class MpvSessionDurationChangedEventArgs : MpvSessionEventArgs
{
    public MpvSessionDurationChangedEventArgs(Guid sessionId, TimeSpan duration)
        : base(sessionId)
    {
        Duration = duration;
    }

    public TimeSpan Duration { get; }
}

public sealed class MpvSessionStateChangedEventArgs : MpvSessionEventArgs
{
    public MpvSessionStateChangedEventArgs(
        Guid sessionId,
        bool isPlaying,
        bool isPaused,
        bool isBuffering,
        double bufferingPercent,
        bool? pausedForCache)
        : base(sessionId)
    {
        IsPlaying = isPlaying;
        IsPaused = isPaused;
        IsBuffering = isBuffering;
        BufferingPercent = bufferingPercent;
        PausedForCache = pausedForCache;
    }

    public bool IsPlaying { get; }

    public bool IsPaused { get; }

    public bool IsBuffering { get; }

    public double BufferingPercent { get; }

    public bool? PausedForCache { get; }
}

public sealed class MpvSessionErrorEventArgs : MpvSessionEventArgs
{
    public MpvSessionErrorEventArgs(Guid sessionId, string errorType, string? message = null)
        : base(sessionId)
    {
        ErrorType = errorType;
        Message = message;
    }

    public string ErrorType { get; }

    public string? Message { get; }
}

public sealed class MpvSessionEndFileEventArgs : MpvSessionEventArgs
{
    public MpvSessionEndFileEventArgs(Guid sessionId, int reason, int error)
        : base(sessionId)
    {
        Reason = reason;
        Error = error;
    }

    public int Reason { get; }

    public int Error { get; }
}

public sealed class MpvSessionTracksChangedEventArgs : MpvSessionEventArgs
{
    public MpvSessionTracksChangedEventArgs(Guid sessionId, MpvTrackListSnapshot snapshot)
        : base(sessionId)
    {
        Snapshot = snapshot;
    }

    public MpvTrackListSnapshot Snapshot { get; }
}

public sealed class MpvSessionSubtitleTrackChangedEventArgs : MpvSessionEventArgs
{
    public MpvSessionSubtitleTrackChangedEventArgs(Guid sessionId, int? trackId)
        : base(sessionId)
    {
        TrackId = trackId;
    }

    public int? TrackId { get; }
}

public sealed class MpvSessionAudioTrackChangedEventArgs : MpvSessionEventArgs
{
    public MpvSessionAudioTrackChangedEventArgs(Guid sessionId, int? trackId)
        : base(sessionId)
    {
        TrackId = trackId;
    }

    public int? TrackId { get; }
}
