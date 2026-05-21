namespace MediaLibrary.Core.Models.ReadModels;

public sealed class SingleSourceCorrectionApplyResult
{
    public int MediaFileId { get; set; }

    public SingleSourceCorrectionTargetKind TargetKind { get; set; }

    public int? TargetMovieId { get; set; }

    public int? TargetSeasonId { get; set; }

    public int? TargetEpisodeId { get; set; }

    public bool CreatedEpisode { get; set; }

    public bool AppendedAsAdditionalSource { get; set; }

    public bool OverwrittenTargetDefaultSource { get; set; }

    public bool OldDefaultFallback { get; set; }

    public string Message { get; set; } = string.Empty;
}
