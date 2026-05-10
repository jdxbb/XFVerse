using System.Runtime.InteropServices;

namespace MediaLibrary.App.Playback.Mpv;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MpvEventEndFile
{
    public readonly int Reason;

    public readonly int Error;

    public readonly long PlaylistEntryId;

    public readonly long PlaylistInsertId;

    public readonly int PlaylistInsertNumEntries;
}
