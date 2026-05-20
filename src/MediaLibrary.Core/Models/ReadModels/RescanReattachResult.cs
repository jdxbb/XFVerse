namespace MediaLibrary.Core.Models.ReadModels;

public sealed class RescanReattachResult
{
    public int CandidateCount { get; set; }

    public int EpisodeCandidateCount { get; set; }

    public int MovieCandidateCount { get; set; }

    public int SucceededCount { get; set; }

    public int SkippedCount { get; set; }

    public int PlaceholderFallbackCount { get; set; }
}
