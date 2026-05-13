namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbTvSeasonDetailResult
{
    public int SeriesTmdbId { get; set; }

    public int TmdbId { get; set; }

    public int SeasonNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string AirDate { get; set; } = string.Empty;

    public int EpisodeCount { get; set; }

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public IReadOnlyList<TmdbTvEpisodeMetadataItem> Episodes { get; set; } = [];
}
