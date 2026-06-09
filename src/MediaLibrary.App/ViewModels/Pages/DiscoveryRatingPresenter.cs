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
            if (TryBuildDisplayScore(ratings, out var displayScore))
            {
                return new DiscoveryRatingPresentation(displayScore, FormatRatingText(displayScore));
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

    private static bool TryBuildDisplayScore(
        IReadOnlyList<(double Score, int Votes)> ratings,
        out double score)
    {
        var validRatings = ratings
            .Where(rating => IsValidScore(rating.Score))
            .ToArray();
        if (validRatings.Length == 0)
        {
            score = 0d;
            return false;
        }

        if (validRatings.Length == 1)
        {
            score = validRatings[0].Score;
            return true;
        }

        var totalVotes = validRatings.Sum(rating => rating.Votes);
        score = totalVotes > 0
            ? validRatings.Sum(rating => rating.Score * rating.Votes) / totalVotes
            : validRatings.Average(rating => rating.Score);
        return IsValidScore(score);
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
