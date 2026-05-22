namespace MediaLibrary.Core.Models.ReadModels;

public sealed class UnknownSeasonCorrectionApplyResult
{
    public int SourceSeasonId { get; set; }

    public int TargetSeriesId { get; set; }

    public int TargetSeasonId { get; set; }

    public string SourceSeasonKind { get; set; } = string.Empty;

    public string TargetSeasonKind { get; set; } = string.Empty;

    public int MovedSourceCount { get; set; }

    public int CreatedEpisodeCount { get; set; }

    public int AppendedSourceCount { get; set; }

    public bool OldContainerHidden { get; set; }

    public bool OldContainerPreserved { get; set; }

    public bool OldDefaultFallback { get; set; }

    public int RemappedSourceCount { get; set; }
}

public sealed class UnknownSeasonCorrectionEpisodeMapping
{
    public int MediaFileId { get; set; }

    public int OriginalEpisodeNumber { get; set; }

    public int TargetEpisodeNumber { get; set; }
}
