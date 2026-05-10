namespace MediaLibrary.Core.Models.Settings;

public sealed class ApplicationSettingModel
{
    public int? Id { get; set; }

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
}
