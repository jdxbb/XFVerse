namespace MediaLibrary.Core.Models.ReadModels;

public sealed class MovieRatingItem
{
    public string SourceName { get; set; } = string.Empty;

    public double ScoreValue { get; set; }

    public double ScoreScale { get; set; } = 10d;

    public int? VoteCount { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public DateTime? LastUpdatedAt { get; set; }
}
