namespace MediaLibrary.Core.Models.ReadModels;

public sealed class PlaybackAudioTrackItem
{
    public string DisplayName { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public string TooltipText { get; set; } = string.Empty;

    public int TrackId { get; set; }

    public bool IsSelected { get; set; }

    public int Priority { get; set; }
}
