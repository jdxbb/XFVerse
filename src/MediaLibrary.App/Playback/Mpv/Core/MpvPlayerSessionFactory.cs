namespace MediaLibrary.App.Playback.Mpv.Core;

public sealed class MpvPlayerSessionFactory
{
    public MpvPlayerSession Create()
    {
        return new MpvPlayerSession();
    }
}
