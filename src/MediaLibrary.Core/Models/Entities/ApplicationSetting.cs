namespace MediaLibrary.Core.Models.Entities;

public sealed class ApplicationSetting
{
    public int Id { get; set; }

    public string TmdbReadAccessToken { get; set; } = string.Empty;

    public string TmdbApiKey { get; set; } = string.Empty;

    public string OmdbApiKey { get; set; } = string.Empty;

    public string ThemeMode { get; set; } = "Light";

    public string AiBaseUrl { get; set; } = string.Empty;

    public string AiApiKey { get; set; } = string.Empty;

    public string AiModel { get; set; } = string.Empty;

    public string RecentAiRecommendationsJson { get; set; } = string.Empty;

    public string CurrentAiRecommendationsJson { get; set; } = string.Empty;

    public string AiRecommendationLibraryFingerprint { get; set; } = string.Empty;

    public string TmdbBaseUrl { get; set; } = string.Empty;

    public string OpenSubtitlesEndpoint { get; set; } = string.Empty;

    public string OpenSubtitlesApiKey { get; set; } = string.Empty;

    public string OpenSubtitlesUsername { get; set; } = string.Empty;

    public string OpenSubtitlesPasswordEncrypted { get; set; } = string.Empty;

    public string OpenSubtitlesTokenEncrypted { get; set; } = string.Empty;

    public string OpenSubtitlesDefaultLanguageCode { get; set; } = string.Empty;

    public bool IsOpenSubtitlesEnabled { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
