namespace MediaLibrary.Core.Models.ReadModels;

public enum AiSearchSuggestionStatus
{
    Success,
    NoResult,
    Failed
}

public sealed class AiSearchSuggestionResult
{
    public AiSearchSuggestionStatus Status { get; set; }

    public AiSearchSuggestion Suggestion { get; set; } = new();

    public AiSearchSuggestion FallbackSuggestion { get; set; } = new();

    public string Message { get; set; } = string.Empty;
}
