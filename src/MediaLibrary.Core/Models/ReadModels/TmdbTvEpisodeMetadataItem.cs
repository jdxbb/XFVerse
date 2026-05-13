namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbTvEpisodeMetadataItem
{
    public int TmdbId { get; set; }

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string StillRemoteUrl { get; set; } = string.Empty;

    public string AirDate { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }
}
