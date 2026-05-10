using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class SubtitleBinding
{
    public int Id { get; set; }

    public int MediaFileId { get; set; }

    public int SubtitleMediaFileId { get; set; }

    public SubtitleMatchType MatchType { get; set; } = SubtitleMatchType.Unknown;

    public string? Language { get; set; }

    public bool IsAutoLoaded { get; set; }

    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MediaFile? MediaFile { get; set; }

    public MediaFile? SubtitleMediaFile { get; set; }
}
