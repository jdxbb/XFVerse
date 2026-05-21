namespace MediaLibrary.Core.Models.ReadModels;

public sealed class SingleSourceCorrectionApplyResult
{
    public int MediaFileId { get; set; }

    public SingleSourceCorrectionTargetKind TargetKind { get; set; }

    public int? TargetMovieId { get; set; }

    public int? TargetEpisodeId { get; set; }

    public string Message { get; set; } = string.Empty;
}
