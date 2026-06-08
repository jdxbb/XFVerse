namespace MediaLibrary.Core.Models.ReadModels;

public sealed record OpenSubtitlesLanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public sealed class OpenSubtitlesClientOptions
{
    public string Endpoint { get; init; } = "https://api.opensubtitles.com/api/v1";

    public string ApiKey { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string DefaultLanguageCode { get; init; } = "zh-cn";
}

public sealed class OpenSubtitlesSearchRequest
{
    public string Query { get; init; } = string.Empty;

    public string ImdbId { get; init; } = string.Empty;

    public int? TmdbId { get; init; }

    public string ParentImdbId { get; init; } = string.Empty;

    public int? ParentTmdbId { get; init; }

    public int? SeasonNumber { get; init; }

    public int? EpisodeNumber { get; init; }

    public string Languages { get; init; } = "zh-cn";

    public string MovieHash { get; init; } = string.Empty;

    public long? FileSize { get; init; }

    public string FileNameHint { get; init; } = string.Empty;

    public int? Year { get; init; }

    public string Type { get; init; } = string.Empty;

    public int Page { get; init; } = 1;

    public string OrderBy { get; init; } = string.Empty;

    public string OrderDirection { get; init; } = string.Empty;
}

public sealed class OpenSubtitlesSearchPage
{
    public IReadOnlyList<OpenSubtitlesSearchItem> Results { get; init; } = [];

    public int Page { get; init; }

    public int TotalPages { get; init; }

    public int TotalCount { get; init; }

    public string ResultMessage { get; init; } = string.Empty;
}

public sealed class OpenSubtitlesSearchItem
{
    public string SubtitleId { get; init; } = string.Empty;

    public string LanguageCode { get; init; } = string.Empty;

    public string LanguageName { get; init; } = string.Empty;

    public string ReleaseName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string ProviderFileId { get; init; } = string.Empty;

    public int? DownloadCount { get; init; }

    public double? Rating { get; init; }

    public int? Votes { get; init; }

    public bool IsHearingImpaired { get; init; }

    public bool IsMachineTranslated { get; init; }

    public bool IsAiTranslated { get; init; }

    public bool IsTrustedUploader { get; init; }

    public double? Fps { get; init; }

    public DateTime? UploadedAt { get; init; }

    public string FeatureTitle { get; init; } = string.Empty;

    public int? FeatureYear { get; init; }

    public int? SeasonNumber { get; init; }

    public int? EpisodeNumber { get; init; }

    public string UploaderName { get; init; } = string.Empty;

    public string RawMetadataJson { get; init; } = string.Empty;
}

public sealed class OpenSubtitlesDownloadContractRequest
{
    public string FileId { get; init; } = string.Empty;

    public string SubFormat { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public bool ForceDownload { get; init; }
}

public sealed class OpenSubtitlesDownloadContractResult
{
    public bool Succeeded { get; init; }

    public string DownloadUrl { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public int? Requests { get; init; }

    public int? Remaining { get; init; }

    public string ResetTime { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public OpenSubtitlesErrorKind ErrorKind { get; init; } = OpenSubtitlesErrorKind.None;
}

public sealed class OpenSubtitlesDownloadResult
{
    public bool Succeeded { get; init; }

    public byte[] Content { get; init; } = [];

    public string FileName { get; init; } = string.Empty;

    public int? Requests { get; init; }

    public int? Remaining { get; init; }

    public string ResetTime { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public OpenSubtitlesErrorKind ErrorKind { get; init; } = OpenSubtitlesErrorKind.None;
}

public sealed class OpenSubtitlesProbeResult
{
    public bool IsApiKeyConfigured { get; init; }

    public bool IsApiKeyAccepted { get; init; }

    public bool LoginAttempted { get; init; }

    public bool LoginSucceeded { get; init; }

    public string Token { get; init; } = string.Empty;

    public bool QuotaProbeAttempted { get; init; }

    public bool QuotaProbeSucceeded { get; init; }

    public int? RemainingDownloads { get; init; }

    public int? AllowedDownloads { get; init; }

    public string Message { get; init; } = string.Empty;

    public OpenSubtitlesErrorKind ErrorKind { get; init; } = OpenSubtitlesErrorKind.None;
}

public enum OpenSubtitlesErrorKind
{
    None = 0,
    NotConfigured = 1,
    Unauthorized = 2,
    Forbidden = 3,
    RateLimited = 4,
    ServerError = 5,
    Network = 6,
    InvalidResponse = 7,
    Unknown = 8
}
