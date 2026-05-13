namespace MediaLibrary.Core.Models.Entities;

public sealed class UserTvSeasonCollectionItem
{
    public int Id { get; set; }

    public int? TvSeasonId { get; set; }

    public int? TvSeriesId { get; set; }

    public int? TmdbSeriesId { get; set; }

    public int? TmdbSeasonId { get; set; }

    public int SeasonNumber { get; set; } = 1;

    public string SeriesTitle { get; set; } = string.Empty;

    public string OriginalSeriesTitle { get; set; } = string.Empty;

    public string SeasonTitle { get; set; } = string.Empty;

    public int? FirstAirYear { get; set; }

    public DateTime? AirDate { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public bool IsWantToWatch { get; set; } = true;

    public bool IsNotInterested { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
