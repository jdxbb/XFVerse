using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WatchHistoryService : IWatchHistoryService
{
    private const double AggregateEvaluationPositionRatio = 0.7d;

    public async Task<int> StartAsync(
        int movieId,
        int mediaFileId,
        int initialPositionSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var normalizedInitialPosition = Math.Max(0, initialPositionSeconds);

        var history = new WatchHistory
        {
            MovieId = movieId,
            MediaFileId = mediaFileId,
            StartedAt = DateTime.UtcNow,
            LastPlayPositionSeconds = normalizedInitialPosition,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.WatchHistories.Add(history);

        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is not null)
        {
            movie.LastPlayedAt = DateTime.UtcNow;
            movie.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return history.Id;
    }

    public async Task<int> GetResumePositionAsync(int movieId, int mediaFileId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        return await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId == movieId && x.MediaFileId == mediaFileId && !x.IsCompleted)
            .OrderByDescending(x => x.LastPlayPositionSeconds > 0)
            .ThenByDescending(x => x.StartedAt)
            .Select(x => x.LastPlayPositionSeconds)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SaveProgressAsync(
        int watchHistoryId,
        int positionSeconds,
        int durationWatchedSeconds,
        bool isCompleted,
        int? mediaDurationSeconds = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var history = await dbContext.WatchHistories
            .FirstOrDefaultAsync(x => x.Id == watchHistoryId, cancellationToken);

        if (history is null)
        {
            return false;
        }

        var normalizedPosition = Math.Max(0, positionSeconds);
        var normalizedWatched = Math.Max(0, durationWatchedSeconds);
        var normalizedMediaDuration = mediaDurationSeconds.HasValue
            ? Math.Max(0, mediaDurationSeconds.Value)
            : 0;
        var mediaFile = await dbContext.MediaFiles
            .FirstOrDefaultAsync(x => x.Id == history.MediaFileId, cancellationToken);
        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == history.MovieId, cancellationToken);

        if (normalizedMediaDuration > 0 && mediaFile is not null)
        {
            if (!mediaFile.DurationSeconds.HasValue
                || Math.Abs(mediaFile.DurationSeconds.Value - normalizedMediaDuration) > 2)
            {
                mediaFile.DurationSeconds = normalizedMediaDuration;
                mediaFile.UpdatedAt = DateTime.UtcNow;
            }
        }

        var effectiveDurationSeconds = ResolveEffectiveDurationSeconds(
            normalizedMediaDuration,
            mediaFile,
            movie);

        if (normalizedPosition > 0 && normalizedWatched >= 3)
        {
            history.LastPlayPositionSeconds = normalizedPosition;
            history.DurationWatchedSeconds = Math.Max(history.DurationWatchedSeconds, normalizedWatched);
        }

        history.EndedAt = DateTime.UtcNow;

        var completionResult = await EvaluateCompletionAsync(
            dbContext,
            history,
            normalizedPosition,
            history.DurationWatchedSeconds,
            effectiveDurationSeconds,
            movie?.AutoWatchedBaselineAtUtc,
            isCompleted,
            cancellationToken);
        var finalHistoryCompleted = history.IsCompleted || completionResult.IsSingleWatchCompleted;
        if (!history.IsCompleted && finalHistoryCompleted)
        {
            history.IsCompleted = true;
        }

        var autoWatchedChanged = false;
        if (movie is not null)
        {
            movie.LastPlayedAt = DateTime.UtcNow;
            if (!movie.IsWatched
                && completionResult.ShouldMarkMovieWatched)
            {
                await ApplyAutoWatchedStateAsync(dbContext, movie, cancellationToken);
                autoWatchedChanged = true;
                LogAutoMarkedWatched(movie.Id, completionResult);
            }

            movie.UpdatedAt = DateTime.UtcNow;
        }

        LogCompletionEvaluation(
            history.MovieId,
            history.MediaFileId,
            isCompleted,
            normalizedPosition,
            effectiveDurationSeconds,
            history.DurationWatchedSeconds,
            finalHistoryCompleted,
            autoWatchedChanged,
            movie?.AutoWatchedBaselineAtUtc,
            completionResult.HistoriesConsidered,
            completionResult.HistoriesIgnoredBeforeBaseline,
            completionResult);

        await dbContext.SaveChangesAsync(cancellationToken);
        return autoWatchedChanged;
    }

    private static async Task<WatchCompletionResult> EvaluateCompletionAsync(
        AppDbContext dbContext,
        WatchHistory history,
        int normalizedPosition,
        int effectiveWatchedSeconds,
        int? effectiveDurationSeconds,
        DateTime? autoWatchedBaselineAtUtc,
        bool externalCompleted,
        CancellationToken cancellationToken)
    {
        if (!effectiveDurationSeconds.HasValue
            || effectiveDurationSeconds.Value <= WatchCompletionOptions.DefaultMinimumValidDurationSeconds)
        {
            LogCompletionSkip(history.MovieId, "no-duration");
            return WatchCompletionResult.NotCompleted("no-duration");
        }

        var histories = new List<WatchCompletionHistoryItem>();
        if (ShouldQueryMovieHistoriesForAggregate(
            normalizedPosition,
            effectiveWatchedSeconds,
            effectiveDurationSeconds.Value,
            externalCompleted))
        {
            histories = await dbContext.WatchHistories
                .AsNoTracking()
                .Where(x => x.MovieId == history.MovieId)
                .Select(x => new WatchCompletionHistoryItem
                {
                    Id = x.Id,
                    LastPlayPositionSeconds = x.LastPlayPositionSeconds,
                    DurationWatchedSeconds = x.DurationWatchedSeconds,
                    StartedAt = x.StartedAt
                })
                .ToListAsync(cancellationToken);
            histories.RemoveAll(x => x.Id == history.Id);
        }

        var historiesIgnoredBeforeBaseline = 0;
        if (autoWatchedBaselineAtUtc.HasValue)
        {
            historiesIgnoredBeforeBaseline = histories.Count(x => x.StartedAt <= autoWatchedBaselineAtUtc.Value);
            histories = histories
                .Where(x => x.StartedAt > autoWatchedBaselineAtUtc.Value)
                .ToList();
        }

        if (!autoWatchedBaselineAtUtc.HasValue || history.StartedAt > autoWatchedBaselineAtUtc.Value)
        {
            histories.Add(new WatchCompletionHistoryItem
            {
                Id = history.Id,
                LastPlayPositionSeconds = Math.Max(0, history.LastPlayPositionSeconds),
                DurationWatchedSeconds = Math.Max(0, effectiveWatchedSeconds),
                StartedAt = history.StartedAt
            });
        }
        else
        {
            historiesIgnoredBeforeBaseline++;
        }

        var result = WatchCompletionEvaluator.Evaluate(
            normalizedPosition,
            effectiveWatchedSeconds,
            effectiveDurationSeconds,
            histories);
        return result with
        {
            BaselineSet = autoWatchedBaselineAtUtc.HasValue,
            HistoriesConsidered = histories.Count,
            HistoriesIgnoredBeforeBaseline = historiesIgnoredBeforeBaseline
        };
    }

    private static bool ShouldQueryMovieHistoriesForAggregate(
        int normalizedPosition,
        int watchedSeconds,
        int durationSeconds,
        bool externalCompleted)
    {
        if (externalCompleted)
        {
            return true;
        }

        var minimumSingleWatchSeconds = (int)Math.Ceiling(Math.Min(
            WatchCompletionOptions.Default.MinimumSingleWatchSeconds,
            durationSeconds * WatchCompletionOptions.Default.MinimumSingleWatchRatio));
        return normalizedPosition >= durationSeconds * AggregateEvaluationPositionRatio
               || watchedSeconds >= minimumSingleWatchSeconds;
    }

    private static int? ResolveEffectiveDurationSeconds(
        int normalizedMediaDuration,
        MediaFile? mediaFile,
        Movie? movie)
    {
        if (normalizedMediaDuration > 0)
        {
            return normalizedMediaDuration;
        }

        if (mediaFile?.DurationSeconds is > 0)
        {
            return mediaFile.DurationSeconds.Value;
        }

        if (movie?.RuntimeMinutes is > 0)
        {
            return movie.RuntimeMinutes.Value * 60;
        }

        return null;
    }

    private static async Task ApplyAutoWatchedStateAsync(
        AppDbContext dbContext,
        Movie movie,
        CancellationToken cancellationToken)
    {
        movie.IsWatched = true;
        movie.UpdatedAt = DateTime.UtcNow;

        var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
        foreach (var item in collectionItems.Where(x => x.IsWantToWatch || !x.IsWatched))
        {
            item.IsWantToWatch = false;
            item.IsWatched = true;
            item.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task<List<UserMovieCollectionItem>> FindCollectionItemsForMovieAsync(
        AppDbContext dbContext,
        Movie movie,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.UserMovieCollectionItems
            .Where(x => x.MovieId == movie.Id
                        || (movie.TmdbId.HasValue && x.TmdbId == movie.TmdbId.Value)
                        || (!string.IsNullOrWhiteSpace(movie.ImdbId) && x.ImdbId == movie.ImdbId)
                        || (x.Title == movie.Title && x.ReleaseYear == movie.ReleaseYear))
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
    }

    private static void LogCompletionEvaluation(
        int movieId,
        int mediaFileId,
        bool externalCompleted,
        int positionSeconds,
        int? durationSeconds,
        int watchedSeconds,
        bool historyCompleted,
        bool movieAutoWatched,
        DateTime? autoWatchedBaselineAtUtc,
        int historiesConsidered,
        int historiesIgnoredBeforeBaseline,
        WatchCompletionResult result)
    {
        if (!ShouldLogCompletionDetails(externalCompleted, positionSeconds, durationSeconds, result))
        {
            return;
        }

        var normalizedDuration = durationSeconds.GetValueOrDefault();
        WatchCompletionDiagnostics.Write(
            $"watch-completion-input movieId={movieId} mediaFileId={mediaFileId} "
            + $"externalCompleted={externalCompleted.ToString().ToLowerInvariant()} "
            + $"position={positionSeconds} duration={FormatNullableDuration(durationSeconds)} watched={watchedSeconds}");
        WatchCompletionDiagnostics.Write(
            $"watch-completion-single-check movieId={movieId} mediaFileId={mediaFileId} "
            + $"nearEnd={result.NearEnd.ToString().ToLowerInvariant()} "
            + $"watchedEnough={result.WatchedEnough.ToString().ToLowerInvariant()} "
            + $"ratio={result.PositionRatio:0.000} remainingSeconds={result.RemainingSeconds} "
            + $"requiredWatched={result.RequiredSingleWatchSeconds}");
        WatchCompletionDiagnostics.Write(
            $"watch-completion-aggregate-check movieId={movieId} mediaFileId={mediaFileId} "
            + $"baselineSet={autoWatchedBaselineAtUtc.HasValue.ToString().ToLowerInvariant()} "
            + $"historiesConsidered={historiesConsidered} historiesIgnoredBeforeBaseline={historiesIgnoredBeforeBaseline} "
            + $"totalWatched={result.AggregateTotalWatchedSeconds} maxPosition={result.AggregateMaxPositionSeconds} "
            + $"requiredTotal={result.RequiredAggregateWatchedSeconds} "
            + $"requiredPosition={result.RequiredAggregatePositionSeconds} validRunCount={result.AggregateValidRunCount}");
        if (autoWatchedBaselineAtUtc.HasValue)
        {
            WatchCompletionDiagnostics.Write($"watch-completion-baseline-applied movieId={movieId}");
        }

        var reason = ResolveCompletionResultReason(externalCompleted, result);
        WatchCompletionDiagnostics.Write(
            $"watch-completion-result movieId={movieId} mediaFileId={mediaFileId} "
            + $"single={result.IsSingleWatchCompleted.ToString().ToLowerInvariant()} "
            + $"aggregate={result.IsAggregateCompleted.ToString().ToLowerInvariant()} "
            + $"historyCompleted={historyCompleted.ToString().ToLowerInvariant()} "
            + $"movieAutoWatched={movieAutoWatched.ToString().ToLowerInvariant()} reason={reason}");

        if (result.IsSingleWatchCompleted)
        {
            WatchCompletionDiagnostics.Write(
                $"watch-completion-single-pass movieId={movieId} mediaFileId={mediaFileId} "
                + $"reason={result.SingleCompletionReason} position={positionSeconds} duration={normalizedDuration} watched={watchedSeconds}");
            return;
        }

        if (result.IsAggregateCompleted)
        {
            WatchCompletionDiagnostics.Write(
                $"watch-completion-aggregate-pass movieId={movieId} mediaFileId={mediaFileId} "
                + $"totalWatched={result.AggregateTotalWatchedSeconds} maxPosition={result.AggregateMaxPositionSeconds} duration={normalizedDuration}");
        }

        if (externalCompleted && !result.IsSingleWatchCompleted)
        {
            WatchCompletionDiagnostics.Write(
                $"watch-completion-skip movieId={movieId} mediaFileId={mediaFileId} reason=external-completed-rejected");
        }
    }

    private static void LogAutoMarkedWatched(int movieId, WatchCompletionResult result)
    {
        var source = result.IsSingleWatchCompleted ? "single" : "aggregate";
        WatchCompletionDiagnostics.Write(
            $"watch-completion-auto-mark-watched movieId={movieId} source={source} "
            + $"baselineSet={result.BaselineSet.ToString().ToLowerInvariant()}");
    }

    private static void LogCompletionSkip(int movieId, string reason)
    {
        if (!string.Equals(reason, "no-duration", StringComparison.Ordinal))
        {
            return;
        }

        WatchCompletionDiagnostics.Write($"watch-completion-skip movieId={movieId} reason={reason}");
    }

    private static bool ShouldLogCompletionDetails(
        bool externalCompleted,
        int positionSeconds,
        int? durationSeconds,
        WatchCompletionResult result)
    {
        if (externalCompleted
            || result.IsSingleWatchCompleted
            || result.IsAggregateCompleted
            || result.NearEnd)
        {
            return true;
        }

        return durationSeconds is > 0
               && positionSeconds >= durationSeconds.Value * AggregateEvaluationPositionRatio;
    }

    private static string ResolveCompletionResultReason(bool externalCompleted, WatchCompletionResult result)
    {
        if (result.IsSingleWatchCompleted)
        {
            return result.SingleCompletionReason;
        }

        if (result.IsAggregateCompleted)
        {
            return "aggregate";
        }

        if (externalCompleted)
        {
            return "external-completed-rejected";
        }

        return string.IsNullOrWhiteSpace(result.SkipReason) ? "not-completed" : result.SkipReason;
    }

    private static string FormatNullableDuration(int? durationSeconds)
    {
        return durationSeconds.HasValue ? durationSeconds.Value.ToString() : "null";
    }

    public async Task DiscardAsync(int watchHistoryId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var history = await dbContext.WatchHistories
            .FirstOrDefaultAsync(x => x.Id == watchHistoryId, cancellationToken);

        if (history is null)
        {
            return;
        }

        if (history.LastPlayPositionSeconds > 0
            || history.DurationWatchedSeconds > 0
            || history.IsCompleted)
        {
            history.EndedAt ??= DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var previousLastPlayedAt = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId == history.MovieId && x.Id != watchHistoryId)
            .OrderByDescending(x => x.EndedAt ?? x.StartedAt)
            .Select(x => (DateTime?)(x.EndedAt ?? x.StartedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == history.MovieId, cancellationToken);
        if (movie is not null)
        {
            movie.LastPlayedAt = previousLastPlayedAt;
            movie.UpdatedAt = DateTime.UtcNow;
        }

        dbContext.WatchHistories.Remove(history);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
