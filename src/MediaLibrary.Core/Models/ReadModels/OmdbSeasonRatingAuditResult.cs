namespace MediaLibrary.Core.Models.ReadModels;

public sealed class OmdbSeasonRatingAuditResult
{
    public string ImdbId { get; set; } = string.Empty;

    public int SeasonNumber { get; set; }

    public bool HasResponse { get; set; }

    public MovieRatingItem? SeasonRating { get; set; }

    public IReadOnlyList<OmdbEpisodeRatingAuditItem> EpisodeRatings { get; set; } = [];

    public string ResultMessage { get; set; } = string.Empty;
}
