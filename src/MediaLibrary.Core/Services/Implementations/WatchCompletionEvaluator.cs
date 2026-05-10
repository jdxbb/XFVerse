namespace MediaLibrary.Core.Services.Implementations;

internal static class WatchCompletionEvaluator
{
    public static WatchCompletionResult Evaluate(
        int currentPositionSeconds,
        int durationWatchedSeconds,
        int? mediaDurationSeconds,
        IReadOnlyCollection<WatchCompletionHistoryItem> movieHistories,
        WatchCompletionOptions? options = null)
    {
        var effectiveOptions = options ?? WatchCompletionOptions.Default;
        var normalizedPosition = Math.Max(0, currentPositionSeconds);
        var normalizedWatched = Math.Max(0, durationWatchedSeconds);
        var normalizedDuration = mediaDurationSeconds.GetValueOrDefault();

        if (normalizedDuration <= effectiveOptions.MinimumValidDurationSeconds)
        {
            return WatchCompletionResult.NotCompleted("no-duration");
        }

        var ratio = normalizedPosition / (double)normalizedDuration;
        var nearEndByRatio = ratio >= effectiveOptions.SingleCompletionPositionRatio;
        var nearEndByTolerance = normalizedPosition >= normalizedDuration - effectiveOptions.EndToleranceSeconds;
        var minimumWatchedSeconds = CalculateMinimumSingleWatchSeconds(normalizedDuration, effectiveOptions);
        var hasEnoughWatchTime = normalizedWatched >= minimumWatchedSeconds;
        var isSingleCompleted = hasEnoughWatchTime && (nearEndByRatio || nearEndByTolerance);
        var singleReason = nearEndByRatio ? "ratio" : nearEndByTolerance ? "end-tolerance" : string.Empty;

        var effectiveHistories = movieHistories
            .Where(x => x.DurationWatchedSeconds > effectiveOptions.MinimumEffectiveHistorySeconds)
            .ToList();
        var totalWatchedSeconds = effectiveHistories.Sum(x => (long)x.DurationWatchedSeconds);
        var maxPositionSeconds = effectiveHistories.Count == 0
            ? 0
            : effectiveHistories.Max(x => x.LastPlayPositionSeconds);
        var requiredAggregateWatchedSeconds = (long)Math.Ceiling(normalizedDuration * effectiveOptions.AggregateWatchRatio);
        var requiredAggregatePositionSeconds = (int)Math.Ceiling(normalizedDuration * effectiveOptions.AggregatePositionRatio);
        var isAggregateCompleted = totalWatchedSeconds >= requiredAggregateWatchedSeconds
                                   && maxPositionSeconds >= requiredAggregatePositionSeconds;

        if (isSingleCompleted || isAggregateCompleted)
        {
            return new WatchCompletionResult
            {
                IsSingleWatchCompleted = isSingleCompleted,
                IsAggregateCompleted = isAggregateCompleted,
                ShouldMarkMovieWatched = true,
                SingleCompletionReason = isSingleCompleted ? singleReason : string.Empty,
                AggregateTotalWatchedSeconds = totalWatchedSeconds,
                AggregateMaxPositionSeconds = maxPositionSeconds,
                RequiredSingleWatchSeconds = minimumWatchedSeconds,
                RequiredAggregateWatchedSeconds = requiredAggregateWatchedSeconds,
                RequiredAggregatePositionSeconds = requiredAggregatePositionSeconds,
                AggregateValidRunCount = effectiveHistories.Count,
                NearEnd = nearEndByRatio || nearEndByTolerance,
                WatchedEnough = hasEnoughWatchTime,
                PositionRatio = ratio,
                RemainingSeconds = normalizedDuration - normalizedPosition
            };
        }

        var skipReason = !hasEnoughWatchTime
            ? "watched-too-short"
            : "not-near-end";
        return new WatchCompletionResult
        {
            SkipReason = skipReason,
            AggregateTotalWatchedSeconds = totalWatchedSeconds,
            AggregateMaxPositionSeconds = maxPositionSeconds,
            RequiredSingleWatchSeconds = minimumWatchedSeconds,
            RequiredAggregateWatchedSeconds = requiredAggregateWatchedSeconds,
            RequiredAggregatePositionSeconds = requiredAggregatePositionSeconds,
            AggregateValidRunCount = effectiveHistories.Count,
            NearEnd = nearEndByRatio || nearEndByTolerance,
            WatchedEnough = hasEnoughWatchTime,
            PositionRatio = ratio,
            RemainingSeconds = normalizedDuration - normalizedPosition
        };
    }

    private static int CalculateMinimumSingleWatchSeconds(
        int durationSeconds,
        WatchCompletionOptions options)
    {
        return (int)Math.Ceiling(Math.Min(
            options.MinimumSingleWatchSeconds,
            durationSeconds * options.MinimumSingleWatchRatio));
    }
}

internal sealed class WatchCompletionOptions
{
    public const int DefaultMinimumValidDurationSeconds = 60;
    public const int DefaultEndToleranceSeconds = 300;
    public const int DefaultMinimumSingleWatchSeconds = 20 * 60;
    public const int DefaultMinimumEffectiveHistorySeconds = 60;

    public static WatchCompletionOptions Default { get; } = new();

    public int MinimumValidDurationSeconds { get; init; } = DefaultMinimumValidDurationSeconds;

    public double SingleCompletionPositionRatio { get; init; } = 0.9d;

    public int EndToleranceSeconds { get; init; } = DefaultEndToleranceSeconds;

    public int MinimumSingleWatchSeconds { get; init; } = DefaultMinimumSingleWatchSeconds;

    public double MinimumSingleWatchRatio { get; init; } = 0.25d;

    public int MinimumEffectiveHistorySeconds { get; init; } = DefaultMinimumEffectiveHistorySeconds;

    public double AggregateWatchRatio { get; init; } = 0.8d;

    public double AggregatePositionRatio { get; init; } = 0.7d;
}

internal sealed record WatchCompletionResult
{
    public bool IsSingleWatchCompleted { get; init; }

    public bool IsAggregateCompleted { get; init; }

    public bool ShouldMarkMovieWatched { get; init; }

    public string SingleCompletionReason { get; init; } = string.Empty;

    public string SkipReason { get; init; } = string.Empty;

    public long AggregateTotalWatchedSeconds { get; init; }

    public int AggregateMaxPositionSeconds { get; init; }

    public int RequiredSingleWatchSeconds { get; init; }

    public long RequiredAggregateWatchedSeconds { get; init; }

    public int RequiredAggregatePositionSeconds { get; init; }

    public int AggregateValidRunCount { get; init; }

    public bool NearEnd { get; init; }

    public bool WatchedEnough { get; init; }

    public double PositionRatio { get; init; }

    public int RemainingSeconds { get; init; }

    public bool BaselineSet { get; init; }

    public int HistoriesConsidered { get; init; }

    public int HistoriesIgnoredBeforeBaseline { get; init; }

    public static WatchCompletionResult NotCompleted(string skipReason)
    {
        return new WatchCompletionResult
        {
            SkipReason = skipReason
        };
    }
}

internal sealed class WatchCompletionHistoryItem
{
    public int Id { get; init; }

    public int LastPlayPositionSeconds { get; init; }

    public int DurationWatchedSeconds { get; init; }

    public DateTime StartedAt { get; init; }
}
