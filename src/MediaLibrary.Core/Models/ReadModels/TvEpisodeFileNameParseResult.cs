namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvEpisodeFileNameParseResult
{
    public bool IsEpisodeLike { get; set; }

    public bool IsSeasonContextOnly { get; set; }

    public bool IsMultiEpisode { get; set; }

    public bool MultiEpisodeFalsePositiveAvoided { get; set; }

    public bool VerifiedTitleNumberSequenceContext { get; set; }

    public bool PartHintDetected { get; set; }

    public bool EpisodeOffsetApplied { get; set; }

    public int? MultiEpisodeEndNumber { get; set; }

    public int? PartHint { get; set; }

    public int? EpisodeInPart { get; set; }

    public int? EpisodeOffset { get; set; }

    public string MultiEpisodePattern { get; set; } = string.Empty;

    public string EpisodeOffsetSkippedReason { get; set; } = string.Empty;

    public string EpisodeOffsetSource { get; set; } = string.Empty;

    public int SeasonNumber { get; set; } = 1;

    public int EpisodeNumber { get; set; }

    public string SeriesNameCandidate { get; set; } = string.Empty;

    public string EpisodeTitleCandidate { get; set; } = string.Empty;

    public string MatchKind { get; set; } = string.Empty;
}
