using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class SubtitleBindingItem
{
    public int SubtitleMediaFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public SubtitleMatchType MatchType { get; set; }

    public string Language { get; set; } = string.Empty;

    public bool IsAutoLoaded { get; set; }

    public int Priority { get; set; }
}
