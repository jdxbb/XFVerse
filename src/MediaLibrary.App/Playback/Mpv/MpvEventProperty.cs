using System.Runtime.InteropServices;

namespace MediaLibrary.App.Playback.Mpv;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MpvEventProperty
{
    public readonly IntPtr Name;

    public readonly MpvFormat Format;

    public readonly IntPtr Data;
}
