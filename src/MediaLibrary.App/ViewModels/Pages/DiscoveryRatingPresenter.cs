using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

internal static class DiscoveryRatingPresenter
{
    private const int VoteWeightCap = 100_000;

    public static DiscoveryRatingPresentation Build(
        double? tmdbScore,
        int? tmdbVotes,
        MovieRatingItem? omdbRating)
    {
        var hasTmdbScore = IsValidScore(tmdbScore);
        var hasOmdbScore = omdbRating is not null && IsValidScore(omdbRating.ScoreValue);
        if (!hasTmdbScore && !hasOmdbScore)
        {
            return new DiscoveryRatingPresentation(null, "--");
        }

        if (hasTmdbScore && hasOmdbScore)
        {
            var ratings = new[]
            {
                (Score: tmdbScore!.Value, Votes: Math.Min(Math.Max(tmdbVotes ?? 0, 0), VoteWeightCap)),
                (Score: omdbRating!.ScoreValue, Votes: Math.Min(Math.Max(omdbRating.VoteCount ?? 0, 0), VoteWeightCap))
            };
            var totalVotes = ratings.Sum(rating => rating.Votes);
            var weightedScore = totalVotes > 0
                ? ratings.Sum(rating => rating.Score * rating.Votes) / totalVotes
                : ratings.Average(rating => rating.Score);
            if (IsValidScore(weightedScore))
            {
                return new DiscoveryRatingPresentation(weightedScore, FormatRatingText(weightedScore));
            }
        }

        if (hasTmdbScore)
        {
            return new DiscoveryRatingPresentation(tmdbScore!.Value, FormatRatingText(tmdbScore.Value));
        }

        return new DiscoveryRatingPresentation(omdbRating!.ScoreValue, FormatRatingText(omdbRating.ScoreValue));
    }

    public static DiscoveryRatingPresentation Build(
        double? tmdbScore,
        int? tmdbVotes,
        double? omdbScore,
        double? omdbScale,
        int? omdbVotes,
        string omdbSourceUrl,
        DateTime? omdbLastUpdatedAt)
    {
        var omdbRating = omdbScore.HasValue
            ? new MovieRatingItem
            {
                SourceName = "OMDb",
                ScoreValue = omdbScale is > 0 ? Math.Clamp(omdbScore.Value / omdbScale.Value * 10d, 0d, 10d) : omdbScore.Value,
                ScoreScale = 10d,
                VoteCount = omdbVotes,
                SourceUrl = omdbSourceUrl,
                LastUpdatedAt = omdbLastUpdatedAt
            }
            : null;

        return Build(tmdbScore, tmdbVotes, omdbRating);
    }

    private static bool IsValidScore(double? score)
    {
        return score is > 0d and <= 10d;
    }

    public static bool IsHighDisplayRating(double? score)
    {
        return IsValidScore(score) && RoundRatingForDisplay(score!.Value) >= 8d;
    }

    private static string FormatRatingText(double score)
    {
        return RoundRatingForDisplay(score).ToString("0.0");
    }

    private static double RoundRatingForDisplay(double score)
    {
        return Math.Round(score, 1, MidpointRounding.AwayFromZero);
    }
}

internal sealed record DiscoveryRatingPresentation(double? Value, string Text);
