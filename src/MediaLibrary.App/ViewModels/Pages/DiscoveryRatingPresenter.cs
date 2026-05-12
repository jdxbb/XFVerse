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
            return new DiscoveryRatingPresentation(null, "暂无评分");
        }

        if (hasTmdbScore
            && hasOmdbScore
            && tmdbVotes is > 0
            && omdbRating?.VoteCount is > 0)
        {
            var effectiveTmdbVotes = Math.Min(tmdbVotes.Value, VoteWeightCap);
            var effectiveOmdbVotes = Math.Min(omdbRating.VoteCount.Value, VoteWeightCap);
            var weightSum = effectiveTmdbVotes + effectiveOmdbVotes;
            if (weightSum > 0)
            {
                var weightedScore = (tmdbScore!.Value * effectiveTmdbVotes + omdbRating.ScoreValue * effectiveOmdbVotes) / weightSum;
                return new DiscoveryRatingPresentation(weightedScore, weightedScore.ToString("0.0"));
            }
        }

        if (hasTmdbScore)
        {
            return new DiscoveryRatingPresentation(tmdbScore!.Value, tmdbScore.Value.ToString("0.0"));
        }

        return new DiscoveryRatingPresentation(omdbRating!.ScoreValue, omdbRating.ScoreValue.ToString("0.0"));
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
}

internal sealed record DiscoveryRatingPresentation(double? Value, string Text);
