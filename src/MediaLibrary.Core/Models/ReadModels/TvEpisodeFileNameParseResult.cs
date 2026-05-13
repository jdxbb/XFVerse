namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvEpisodeFileNameParseResult
{
    public bool IsEpisodeLike { get; set; }

    public bool IsSeasonContextOnly { get; set; }

    public bool IsMultiEpisode { get; set; }

    public int SeasonNumber { get; set; } = 1;

    public int EpisodeNumber { get; set; }

    public string SeriesNameCandidate { get; set; } = string.Empty;

    public string EpisodeTitleCandidate { get; set; } = string.Empty;

    public string MatchKind { get; set; } = string.Empty;
}
