using System.Runtime.InteropServices;

namespace MediaLibrary.App.Playback.Mpv;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MpvEventLogMessage
{
    public readonly IntPtr Prefix;

    public readonly IntPtr Level;

    public readonly IntPtr Text;

    public readonly int LogLevel;
}
