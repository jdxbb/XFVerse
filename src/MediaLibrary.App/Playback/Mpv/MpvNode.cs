using System.Runtime.InteropServices;

namespace MediaLibrary.App.Playback.Mpv;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MpvNode
{
    public readonly IntPtr Value;

    public readonly MpvFormat Format;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct MpvNodeList
{
    public readonly int Count;

    public readonly IntPtr Values;

    public readonly IntPtr Keys;
}
