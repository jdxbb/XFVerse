using System.Runtime.InteropServices;

namespace MediaLibrary.App.Playback.Mpv;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MpvEvent
{
    public readonly MpvEventId EventId;

    public readonly int Error;

    public readonly ulong ReplyUserData;

    public readonly IntPtr Data;
}
