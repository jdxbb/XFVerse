namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ManualUnknownSeasonAggregationApplyResult
{
    public int SeriesId { get; set; }

    public int SeasonId { get; set; }

    public int SourceCount { get; set; }

    public int CreatedEpisodeCount { get; set; }

    public int AdditionalSourceCount { get; set; }
}
