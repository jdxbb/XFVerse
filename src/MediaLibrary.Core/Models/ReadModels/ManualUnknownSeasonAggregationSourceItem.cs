namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ManualUnknownSeasonAggregationSourceItem
{
    public int MediaFileId { get; set; }

    public int SortIndex { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourceSummary { get; set; } = string.Empty;

    public string CurrentBindingText { get; set; } = string.Empty;

    public int SuggestedEpisodeNumber { get; set; }

    public bool EpisodeNumberParsedFromFileName { get; set; }
}
