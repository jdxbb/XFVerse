namespace MediaLibrary.Core.Models.Settings;

public sealed class AiRequestOptions
{
    public string? DeepSeekModelOverride { get; init; }

    public string? RequestKind { get; init; }

    public string? OverrideReason { get; init; }

    public double? Temperature { get; init; }

    public bool? ThinkingEnabled { get; init; }

    public string? ReasoningEffort { get; init; }

    public TimeSpan? Timeout { get; init; }

    public static AiRequestOptions CorrectionPro { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-pro",
        RequestKind = "single-source-correction",
        OverrideReason = "correction-ai-pro-no-deep-thinking",
        Timeout = TimeSpan.FromSeconds(90)
    };

    public static AiRequestOptions BatchCorrectionPro { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-pro",
        RequestKind = "batch-ai-correction",
        OverrideReason = "batch-ai-pro-no-deep-thinking",
        Timeout = TimeSpan.FromSeconds(75)
    };

    public static AiRequestOptions MovieTaggingFlash { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-flash",
        RequestKind = "movie-tagging",
        OverrideReason = "scan-ai-flash"
    };

    public static AiRequestOptions Recommendation { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-flash",
        RequestKind = "recommendation",
        OverrideReason = "recommendation-flash",
        Temperature = 0.35,
        Timeout = TimeSpan.FromSeconds(90)
    };

    public static AiRequestOptions WatchProfile { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-pro",
        RequestKind = "watch-profile",
        OverrideReason = "watch-profile-pro-high-thinking",
        ThinkingEnabled = true,
        ReasoningEffort = "high",
        Timeout = TimeSpan.FromSeconds(180)
    };
}
