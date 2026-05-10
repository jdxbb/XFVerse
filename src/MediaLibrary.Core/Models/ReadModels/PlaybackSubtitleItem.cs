using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class PlaybackSubtitleItem
{
    private string _displayName = string.Empty;

    public string DisplayName
    {
        get => string.IsNullOrWhiteSpace(_displayName) ? FileName : _displayName;
        set => _displayName = value;
    }

    public string OriginalName { get; set; } = string.Empty;

    public string TooltipText { get; set; } = string.Empty;

    public string UniqueKey { get; set; } = string.Empty;

    public PlaybackSubtitleType Type { get; set; } = PlaybackSubtitleType.None;

    public int? TrackId { get; set; }

    public int? BindingId { get; set; }

    public int? MediaFileId { get; set; }

    public int SubtitleMediaFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string PlaybackUrl { get; set; } = string.Empty;

    public SubtitleMatchType MatchType { get; set; }

    public bool IsAuto { get; set; }

    public bool IsPreferred { get; set; }

    public bool IsAutoLoaded { get; set; }

    public int Priority { get; set; }

    public bool IsNoneOption { get; set; }
}
