namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ManualUnknownSeasonAggregationPrepareResult
{
    public bool IsValid { get; set; }

    public string Message { get; set; } = string.Empty;

    public string SuggestedSeriesTitle { get; set; } = string.Empty;

    public string SuggestedSeasonTitle { get; set; } = string.Empty;

    public IReadOnlyList<ManualUnknownSeasonAggregationSourceItem> Sources { get; set; } = [];
}
