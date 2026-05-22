namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ManualUnknownSeasonAggregationApplyRequest
{
    public string SeriesTitle { get; set; } = string.Empty;

    public string SeasonTitle { get; set; } = string.Empty;

    public int SeasonNumber { get; set; } = 1;

    public IReadOnlyList<ManualUnknownSeasonAggregationSourceAssignment> Sources { get; set; } = [];
}

public sealed class ManualUnknownSeasonAggregationSourceAssignment
{
    public int MediaFileId { get; set; }

    public int EpisodeNumber { get; set; }
}
