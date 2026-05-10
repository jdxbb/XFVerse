namespace MediaLibrary.Core.Models.ReadModels;

public enum AutoIdentifyStatus
{
    Success,
    NoResult,
    Failed,
    Cancelled
}

public sealed class AutoIdentifyResult
{
    public int MovieId { get; set; }

    public AutoIdentifyStatus Status { get; set; }

    public int? TargetMovieId { get; set; }

    public int? AppliedTmdbId { get; set; }

    public string Query { get; set; } = string.Empty;

    public int? QueryYear { get; set; }

    public string AppliedTitle { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
