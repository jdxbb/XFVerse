namespace MediaLibrary.Core.Models.ReadModels;

public sealed class UnknownSeasonCorrectionApplyResult
{
    public int SourceSeasonId { get; set; }

    public int TargetSeriesId { get; set; }

    public int TargetSeasonId { get; set; }

    public int MovedSourceCount { get; set; }

    public int CreatedEpisodeCount { get; set; }

    public int AppendedSourceCount { get; set; }

    public bool OldContainerHidden { get; set; }
}
