namespace MediaLibrary.Core.Models.ReadModels;

public sealed class PlaybackSessionModel
{
    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public int? DefaultMediaFileId { get; set; }

    public int SelectedMediaFileId { get; set; }

    public IReadOnlyList<PlaybackSourceItem> Sources { get; set; } = [];
}
