using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class TvSeason
{
    public int Id { get; set; }

    public int TvSeriesId { get; set; }

    public int? TmdbSeasonId { get; set; }

    public int SeasonNumber { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string? Overview { get; set; }

    public string? PosterLocalPath { get; set; }

    public string? PosterRemoteUrl { get; set; }

    public DateTime? AirDate { get; set; }

    public int? TmdbEpisodeCount { get; set; }

    public double? IdentifiedConfidence { get; set; }

    public IdentificationStatus IdentificationStatus { get; set; } = IdentificationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TvSeries? Series { get; set; }

    public ICollection<TvEpisode> Episodes { get; set; } = new List<TvEpisode>();

    public ICollection<UserTvSeasonCollectionItem> CollectionItems { get; set; } = new List<UserTvSeasonCollectionItem>();
}
