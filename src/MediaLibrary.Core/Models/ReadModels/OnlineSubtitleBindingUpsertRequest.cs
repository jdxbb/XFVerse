namespace MediaLibrary.Core.Models.ReadModels;

public sealed class OnlineSubtitleBindingUpsertRequest
{
    public int? MovieId { get; init; }

    public int? EpisodeId { get; init; }

    public int? MediaFileId { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string ProviderSubtitleId { get; init; } = string.Empty;

    public string ProviderFileId { get; init; } = string.Empty;

    public string LanguageCode { get; init; } = string.Empty;

    public string LanguageName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ReleaseName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string CacheRelativePath { get; init; } = string.Empty;

    public string CacheHash { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public int? DownloadCount { get; init; }

    public double? Rating { get; init; }

    public int? Votes { get; init; }

    public bool IsHearingImpaired { get; init; }

    public bool IsMachineTranslated { get; init; }

    public bool IsAiTranslated { get; init; }

    public bool IsTrustedUploader { get; init; }

    public double? Fps { get; init; }

    public DateTime? UploadedAt { get; init; }

    public string MetadataJson { get; init; } = string.Empty;
}
