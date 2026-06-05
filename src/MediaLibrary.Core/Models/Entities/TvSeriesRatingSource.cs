namespace MediaLibrary.Core.Models.Entities;

public sealed class TvSeriesRatingSource
{
    public int Id { get; set; }

    public int TvSeriesId { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public double ScoreValue { get; set; }

    public double ScoreScale { get; set; } = 10d;

    public int? VoteCount { get; set; }

    public string? SourceUrl { get; set; }

    public DateTime? LastUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TvSeries? Series { get; set; }
}
