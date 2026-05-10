namespace MediaLibrary.Core.Models.Settings;

public sealed class RecommendationPreferenceModel
{
    public const int MaxTextLength = 500;

    public bool IsEnabled { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
