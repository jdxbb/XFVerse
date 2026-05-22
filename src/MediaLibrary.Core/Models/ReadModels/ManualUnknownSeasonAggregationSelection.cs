namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ManualUnknownSeasonAggregationSelection
{
    public string SelectionKey { get; set; } = string.Empty;

    public int MovieId { get; set; }

    public int SeasonId { get; set; }

    public int OrphanMediaFileId { get; set; }

    public IReadOnlyList<int> GroupedRangeMediaFileIds { get; set; } = [];
}
