namespace MediaLibrary.Core.Models.ReadModels;

public sealed class UnknownTvSeasonAppendResult
{
    public int CandidateCount { get; set; }

    public int EpisodeCandidateCount { get; set; }

    public int SucceededCount { get; set; }

    public int CreatedEpisodeCount { get; set; }

    public int AppendedSourceCount { get; set; }

    public int SkippedCount { get; set; }
}
