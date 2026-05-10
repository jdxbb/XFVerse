namespace MediaLibrary.App.Playback;

public interface IPlaybackEngineFeatureFlags
{
    bool DefersTrackFeatures { get; }

    bool LoadReturnsOnCommandSubmitted { get; }
}
