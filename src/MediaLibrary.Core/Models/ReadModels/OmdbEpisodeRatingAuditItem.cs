namespace MediaLibrary.Core.Models.ReadModels;

public sealed class OmdbEpisodeRatingAuditItem
{
    public int? EpisodeNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ImdbId { get; set; } = string.Empty;

    public MovieRatingItem? Rating { get; set; }
}
