namespace MediaLibrary.App.Playback.Mpv.Core;

public sealed class MpvTrackListSnapshot
{
    public static MpvTrackListSnapshot Empty { get; } = new([]);

    public MpvTrackListSnapshot(IReadOnlyList<MpvTrackInfo> tracks)
    {
        Tracks = tracks;
        AudioTracks = tracks
            .Where(track => string.Equals(track.Kind, "audio", StringComparison.OrdinalIgnoreCase))
            .OrderBy(track => track.Id)
            .ToArray();
        EmbeddedSubtitleTracks = tracks
            .Where(track => string.Equals(track.Kind, "sub", StringComparison.OrdinalIgnoreCase) && !track.IsExternal)
            .OrderBy(track => track.Id)
            .ToArray();
        ExternalSubtitleTracks = tracks
            .Where(track => string.Equals(track.Kind, "sub", StringComparison.OrdinalIgnoreCase) && track.IsExternal)
            .OrderBy(track => track.Id)
            .ToArray();
        SelectedAudioTrackId = AudioTracks.FirstOrDefault(track => track.IsSelected)?.Id;
        SelectedSubtitleTrackId = tracks
            .FirstOrDefault(track => string.Equals(track.Kind, "sub", StringComparison.OrdinalIgnoreCase) && track.IsSelected)
            ?.Id;
    }

    public IReadOnlyList<MpvTrackInfo> Tracks { get; }

    public IReadOnlyList<MpvTrackInfo> AudioTracks { get; }

    public IReadOnlyList<MpvTrackInfo> EmbeddedSubtitleTracks { get; }

    public IReadOnlyList<MpvTrackInfo> ExternalSubtitleTracks { get; }

    public int? SelectedAudioTrackId { get; }

    public int? SelectedSubtitleTrackId { get; }
}
