namespace MediaLibrary.Core.Models.Entities;

public sealed class TvSeries
{
    public int Id { get; set; }

    public int? TmdbSeriesId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? OriginalName { get; set; }

    public string? Overview { get; set; }

    public string? PosterLocalPath { get; set; }

    public string? PosterRemoteUrl { get; set; }

    public string? Country { get; set; }

    public string? Language { get; set; }

    public DateTime? FirstAirDate { get; set; }

    public int? FirstAirYear { get; set; }

    public string? GenresText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TvSeason> Seasons { get; set; } = new List<TvSeason>();
}
