using MediaLibrary.App.Playback;
using MediaLibrary.App.Playback.Mpv.Core;

namespace MediaLibrary.App.Playback.Mpv;

public sealed class MpvPlaybackEngineFactory : IPlaybackEngineFactory
{
    private readonly MpvPlayerSessionFactory _sessionFactory = new();

    public IPlaybackEngine Create()
    {
        return new MpvPlaybackEngineAdapter(_sessionFactory);
    }
}
