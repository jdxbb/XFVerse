using System.Globalization;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class MovieRatingItem
{
    public string SourceName { get; set; } = string.Empty;

    public double ScoreValue { get; set; }

    public double ScoreScale { get; set; } = 10d;

    public int? VoteCount { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public DateTime? LastUpdatedAt { get; set; }

    public string ScoreDisplayText => ScoreValue > 0
        ? ScoreValue.ToString("0.0", CultureInfo.InvariantCulture)
        : "\u672A\u77E5";

    public string VoteCountDisplayText => VoteCount.HasValue
        ? VoteCount.Value.ToString("N0", CultureInfo.InvariantCulture)
        : "\u672A\u77E5";
}
