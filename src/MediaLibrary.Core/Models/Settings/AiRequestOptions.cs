namespace MediaLibrary.Core.Models.Settings;

public sealed class AiRequestOptions
{
    public string? DeepSeekModelOverride { get; init; }

    public double? Temperature { get; init; }

    public bool? ThinkingEnabled { get; init; }

    public string? ReasoningEffort { get; init; }

    public TimeSpan? Timeout { get; init; }

    public static AiRequestOptions Recommendation { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-flash",
        Temperature = 0.35,
        Timeout = TimeSpan.FromSeconds(90)
    };

    public static AiRequestOptions WatchProfile { get; } = new()
    {
        DeepSeekModelOverride = "deepseek-v4-pro",
        ThinkingEnabled = true,
        ReasoningEffort = "high",
        Timeout = TimeSpan.FromSeconds(180)
    };
}
