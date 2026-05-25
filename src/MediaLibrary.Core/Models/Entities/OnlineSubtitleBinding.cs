namespace MediaLibrary.Core.Models.Entities;

public sealed class OnlineSubtitleBinding
{
    public int Id { get; set; }

    public int? MovieId { get; set; }

    public int? EpisodeId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderSubtitleId { get; set; } = string.Empty;

    public string ProviderFileId { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;

    public string LanguageName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ReleaseName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string CacheRelativePath { get; set; } = string.Empty;

    public string CacheHash { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public int? DownloadCount { get; set; }

    public double? Rating { get; set; }

    public int? Votes { get; set; }

    public bool IsHearingImpaired { get; set; }

    public bool IsMachineTranslated { get; set; }

    public bool IsAiTranslated { get; set; }

    public bool IsTrustedUploader { get; set; }

    public double? Fps { get; set; }

    public DateTime? UploadedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public bool IsDeleted { get; set; }

    public string MetadataJson { get; set; } = string.Empty;

    public Movie? Movie { get; set; }

    public TvEpisode? Episode { get; set; }
}
