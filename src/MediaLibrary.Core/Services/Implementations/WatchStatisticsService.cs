using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WatchStatisticsService : IWatchStatisticsService
{
    private const string StatisticsKind = "statistics";
    private const int CacheHours = 12;
    private const int ValidWatchSecondsThreshold = 60;
    private const int TopTagCount = 10;
    private const int TopSmallTagCount = 3;
    private const string StatisticsLogicVersion = "wi-6.2-month-active-state-v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly ViewingTimeBucketDefinition[] ViewingTimeBucketDefinitions =
    [
        new(0, 6, "0-6"),
        new(6, 9, "6-9"),
        new(9, 12, "9-12"),
        new(12, 15, "12-15"),
        new(15, 18, "15-18"),
        new(18, 21, "18-21"),
        new(21, 24, "21-24")
    ];

    private readonly IWatchInsightCacheService _cacheService;

    public WatchStatisticsService(IWatchInsightCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<WatchStatisticsSnapshot> GetStatisticsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        return await GetStatisticsAsync(
            WatchStatisticsTimeRange.Month,
            calendarMonth: null,
            forceRefresh,
            cancellationToken);
    }

    public async Task<WatchStatisticsSnapshot> GetStatisticsAsync(
        WatchStatisticsTimeRange timeRange,
        DateTime? calendarMonth = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var nowLocal = ToLocalTime(nowUtc);
        var normalizedRange = NormalizeTimeRange(timeRange);
        var normalizedCalendarMonth = NormalizeCalendarMonth(calendarMonth, nowLocal);
        var scopeKey = BuildScopeKey(normalizedRange, nowLocal, normalizedCalendarMonth);
        var fingerprint = await BuildSourceFingerprintAsync(scopeKey, cancellationToken);
        WatchInsightCacheSnapshot? cache = null;

        if (!forceRefresh)
        {
            cache = await _cacheService.GetAsync(StatisticsKind, scopeKey, cancellationToken);
            var missReason = GetCacheMissReason(cache, fingerprint, nowUtc);
            if (missReason is null && cache is not null)
            {
                if (TryDeserializeSnapshot(cache.PayloadJson, out var cachedSnapshot))
                {
                    cachedSnapshot.SourceFingerprint = cache.SourceFingerprint;
                    cachedSnapshot.ExpiresAtUtc = cache.ExpiresAtUtc;
                    cachedSnapshot.LoadedFromCache = true;
                    Log($"watch-statistics-cache-hit range={FormatRange(normalizedRange)} fingerprint={ShortFingerprint(fingerprint)}");
                    return cachedSnapshot;
                }

                missReason = "deserialize-failed";
            }

            Log($"watch-statistics-cache-miss range={FormatRange(normalizedRange)} reason={missReason ?? "missing"} fingerprint={ShortFingerprint(fingerprint)}");
        }

        try
        {
            var computeStopwatch = Stopwatch.StartNew();
            Log($"watch-statistics-compute-start range={FormatRange(normalizedRange)} calendarMonth={normalizedCalendarMonth:yyyy-MM}");
            var snapshot = await ComputeStatisticsAsync(
                fingerprint,
                nowUtc,
                normalizedRange,
                normalizedCalendarMonth,
                cancellationToken);
            computeStopwatch.Stop();
            Log(
                "watch-statistics-compute-complete "
                + $"range={FormatRange(normalizedRange)} "
                + $"elapsedMs={computeStopwatch.ElapsedMilliseconds} "
                + $"movieCount={snapshot.WatchedCount + snapshot.FavoriteCount + snapshot.WantToWatchCount + snapshot.NotInterestedCount} "
                + $"historyCount={snapshot.MonthlyWatchCount}");

            var payloadJson = JsonSerializer.Serialize(snapshot, JsonOptions);
            var upsertStopwatch = Stopwatch.StartNew();
            await _cacheService.UpsertAsync(
                StatisticsKind,
                scopeKey,
                payloadJson,
                fingerprint,
                snapshot.ExpiresAtUtc,
                isManualRefresh: forceRefresh,
                cancellationToken);
            upsertStopwatch.Stop();
            Log($"watch-statistics-cache-upsert elapsedMs={upsertStopwatch.ElapsedMilliseconds}");

            snapshot.LoadedFromCache = false;
            return snapshot;
        }
        catch (Exception exception)
        {
            Log($"watch-statistics-warning reason=compute-failed exception={exception.GetType().Name}");

            cache ??= await _cacheService.GetAsync(StatisticsKind, scopeKey, cancellationToken);
            if (cache is not null && TryDeserializeSnapshot(cache.PayloadJson, out var fallbackSnapshot))
            {
                fallbackSnapshot.LoadedFromCache = true;
                fallbackSnapshot.WarningMessages.Add("Statistics refresh failed; returning the previous cached result.");
                return fallbackSnapshot;
            }

            return CreateEmptySnapshot(
                fingerprint,
                nowUtc,
                normalizedRange,
                normalizedCalendarMonth,
                "Statistics refresh failed and no cached result is available.",
                "statistics-refresh-failed");
        }
    }

    private static string? GetCacheMissReason(
        WatchInsightCacheSnapshot? cache,
        string fingerprint,
        DateTime nowUtc)
    {
        if (cache is null)
        {
            return "missing";
        }

        if (cache.IsStale)
        {
            return "stale";
        }

        if (cache.ExpiresAtUtc.HasValue && cache.ExpiresAtUtc.Value <= nowUtc)
        {
            return "expired";
        }

        if (!string.Equals(cache.SourceFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return "fingerprint-changed";
        }

        return null;
    }

    private static bool TryDeserializeSnapshot(string payloadJson, out WatchStatisticsSnapshot snapshot)
    {
        try
        {
            var deserialized = JsonSerializer.Deserialize<WatchStatisticsSnapshot>(payloadJson, JsonOptions);
            if (deserialized is not null)
            {
                snapshot = deserialized;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        snapshot = new WatchStatisticsSnapshot();
        return false;
    }

    private async Task<string> BuildSourceFingerprintAsync(
        string scopeKey,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movieCount = await dbContext.Movies.AsNoTracking().CountAsync(cancellationToken);
        var movieMaxUpdatedAt = await dbContext.Movies
            .AsNoTracking()
            .Select(x => (DateTime?)x.UpdatedAt)
            .MaxAsync(cancellationToken);

        var mediaFileCount = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue)
            .CountAsync(cancellationToken);
        var mediaFileMaxUpdatedAt = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue)
            .Select(x => (DateTime?)x.UpdatedAt)
            .MaxAsync(cancellationToken);
        var mediaFileMaxCreatedAt = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue)
            .Select(x => (DateTime?)x.CreatedAt)
            .MaxAsync(cancellationToken);

        var watchHistoryCount = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue)
            .CountAsync(cancellationToken);
        var watchHistoryMaxActivityAt = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue)
            .Select(x => (DateTime?)(x.EndedAt ?? x.StartedAt))
            .MaxAsync(cancellationToken);
        var watchHistoryMaxCreatedAt = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue)
            .Select(x => (DateTime?)x.CreatedAt)
            .MaxAsync(cancellationToken);

        var meaningfulCollectionItems = dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsInLibrary || x.IsWatched || x.IsWantToWatch || x.IsNotInterested);
        var collectionItemCount = await meaningfulCollectionItems.CountAsync(cancellationToken);
        var collectionItemMaxUpdatedAt = await meaningfulCollectionItems
            .AsNoTracking()
            .Select(x => (DateTime?)x.UpdatedAt)
            .MaxAsync(cancellationToken);

        var ratingSourceCount = await dbContext.RatingSources.AsNoTracking().CountAsync(cancellationToken);
        var ratingSourceMaxUpdatedAt = await dbContext.RatingSources
            .AsNoTracking()
            .Select(x => (DateTime?)(x.LastUpdatedAt ?? x.CreatedAt))
            .MaxAsync(cancellationToken);
        var stateHistoryCount = await dbContext.UserMovieStateChangeHistories.AsNoTracking().CountAsync(cancellationToken);
        var stateHistoryMaxChangedAt = await dbContext.UserMovieStateChangeHistories
            .AsNoTracking()
            .Select(x => (DateTime?)x.ChangedAtUtc)
            .MaxAsync(cancellationToken);

        var rawFingerprint = string.Join(
            "|",
            $"scope:{scopeKey}",
            $"logic:{StatisticsLogicVersion}",
            $"movies:{movieCount}:{FormatFingerprintDate(movieMaxUpdatedAt)}",
            $"media:{mediaFileCount}:{FormatFingerprintDate(mediaFileMaxUpdatedAt)}:{FormatFingerprintDate(mediaFileMaxCreatedAt)}",
            $"history:{watchHistoryCount}:{FormatFingerprintDate(watchHistoryMaxActivityAt)}:{FormatFingerprintDate(watchHistoryMaxCreatedAt)}",
            $"collections:{collectionItemCount}:{FormatFingerprintDate(collectionItemMaxUpdatedAt)}",
            $"ratings:{ratingSourceCount}:{FormatFingerprintDate(ratingSourceMaxUpdatedAt)}",
            $"stateHistory:{stateHistoryCount}:{FormatFingerprintDate(stateHistoryMaxChangedAt)}");

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawFingerprint)))
            .ToLowerInvariant();
        stopwatch.Stop();
        Log($"watch-statistics-fingerprint-built elapsedMs={stopwatch.ElapsedMilliseconds} fingerprint={ShortFingerprint(fingerprint)}");
        return fingerprint;
    }

    private async Task<WatchStatisticsSnapshot> ComputeStatisticsAsync(
        string fingerprint,
        DateTime nowUtc,
        WatchStatisticsTimeRange timeRange,
        DateTime calendarMonth,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var identifiedMovies = await LoadIdentifiedMoviesAsync(dbContext, cancellationToken);
        var identifiedMovieIds = identifiedMovies.Select(x => x.Id).ToHashSet();
        var collectionItems = await LoadIdentifiedCollectionItemsAsync(dbContext, cancellationToken);
        var identifiedTmdbIds = identifiedMovies.Select(x => x.TmdbId)
            .Concat(collectionItems.Select(x => x.TmdbId))
            .ToHashSet();
        var collectionItemIds = collectionItems.Select(x => x.Id).ToHashSet();
        var mediaFiles = identifiedMovieIds.Count == 0
            ? []
            : await dbContext.MediaFiles
                .AsNoTracking()
                .Where(x => x.MovieId.HasValue && identifiedMovieIds.Contains(x.MovieId.Value))
                .Select(x => new MediaFileStatsRow
                {
                    Id = x.Id,
                    MovieId = x.MovieId!.Value,
                    DurationSeconds = x.DurationSeconds
                })
                .ToListAsync(cancellationToken);
        var ratingSources = identifiedMovieIds.Count == 0
            ? []
            : await dbContext.RatingSources
                .AsNoTracking()
                .Where(x => identifiedMovieIds.Contains(x.MovieId))
                .Select(x => new RatingSourceStatsRow
                {
                    MovieId = x.MovieId,
                    SourceName = x.SourceName,
                    ScoreValue = x.ScoreValue,
                    ScoreScale = x.ScoreScale,
                    VoteCount = x.VoteCount
                })
                .ToListAsync(cancellationToken);
        var histories = identifiedMovieIds.Count == 0
            ? new List<WatchHistoryStatsRow>()
            : await dbContext.WatchHistories
                .AsNoTracking()
                .Where(x => x.MovieId.HasValue
                    && identifiedMovieIds.Contains(x.MovieId.Value)
                    && x.DurationWatchedSeconds > ValidWatchSecondsThreshold)
                .Select(x => new WatchHistoryStatsRow
                {
                    Id = x.Id,
                    MovieId = x.MovieId!.Value,
                    MediaFileId = x.MediaFileId,
                    StartedAt = x.StartedAt,
                    EndedAt = x.EndedAt,
                    DurationWatchedSeconds = x.DurationWatchedSeconds
                })
                .ToListAsync(cancellationToken);
        var stateHistories = await dbContext.UserMovieStateChangeHistories
            .AsNoTracking()
            .Where(x => x.TmdbId > 0)
            .Select(x => new StateChangeStatsRow
            {
                Id = x.Id,
                TmdbId = x.TmdbId,
                MovieId = x.MovieId,
                UserMovieCollectionItemId = x.UserMovieCollectionItemId,
                StateType = x.StateType,
                NewValue = x.NewValue,
                ChangedAtUtc = x.ChangedAtUtc
            })
            .ToListAsync(cancellationToken);
        stateHistories = stateHistories
            .Where(x => identifiedTmdbIds.Contains(x.TmdbId)
                && (!x.MovieId.HasValue || identifiedMovieIds.Contains(x.MovieId.Value))
                && (!x.UserMovieCollectionItemId.HasValue || collectionItemIds.Contains(x.UserMovieCollectionItemId.Value)))
            .ToList();

        var movieById = identifiedMovies.ToDictionary(x => x.Id);
        var ratingByMovieId = ratingSources
            .GroupBy(x => x.MovieId)
            .ToDictionary(x => x.Key, x => CalculateWeightedRating(x));
        var mediaDurationByMovieId = mediaFiles
            .Where(x => x.DurationSeconds.HasValue && x.DurationSeconds.Value > 0)
            .GroupBy(x => x.MovieId)
            .ToDictionary(x => x.Key, x => x.Max(item => item.DurationSeconds!.Value));
        var mediaDurationById = mediaFiles
            .Where(x => x.DurationSeconds.HasValue && x.DurationSeconds.Value > 0)
            .ToDictionary(x => x.Id, x => x.DurationSeconds!.Value);
        var profileRows = BuildProfileRows(identifiedMovies, collectionItems, ratingByMovieId);

        var nowLocal = ToLocalTime(nowUtc);
        var currentMonthStart = new DateTime(nowLocal.Year, nowLocal.Month, 1);
        var rangeStart = timeRange == WatchStatisticsTimeRange.Month ? currentMonthStart : (DateTime?)null;
        var rangeEnd = timeRange == WatchStatisticsTimeRange.Month ? currentMonthStart.AddMonths(1) : (DateTime?)null;
        var rangeHistories = FilterHistoriesByRange(histories, rangeStart, rangeEnd);
        var calendarHistories = FilterHistoriesByRange(histories, calendarMonth, calendarMonth.AddMonths(1));
        var earliestHistoryMonth = histories.Count == 0
            ? currentMonthStart
            : histories
                .Select(x => ToMonthStart(ToLocalTime(x.StartedAt)))
                .Min();

        var snapshot = new WatchStatisticsSnapshot
        {
            GeneratedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddHours(CacheHours),
            SourceFingerprint = fingerprint,
            TimeRange = timeRange,
            RangeStartLocal = rangeStart,
            RangeEndLocal = rangeEnd,
            CalendarMonth = calendarMonth,
            EarliestCalendarMonth = earliestHistoryMonth,
            LatestCalendarMonth = currentMonthStart,
            HasAnyData = profileRows.Count > 0,
            HasWatchHistoryData = rangeHistories.Count > 0
        };

        ApplyStatusCounts(snapshot, identifiedMovies, collectionItems, stateHistories, timeRange, currentMonthStart);
        snapshot.TotalWatchSeconds = rangeHistories.Sum(x => (long)x.DurationWatchedSeconds);
        snapshot.MonthlyWatchCount = CountDistinctWatchedMovies(rangeHistories, movieById);
        snapshot.MonthlyFrequentTags = BuildMonthlyFrequentTags(rangeHistories, movieById, TopTagCount);
        snapshot.CalendarDays = BuildCalendarDays(calendarMonth, calendarHistories);
        ApplyMonthlyCards(snapshot, calendarHistories);
        var rangeProfileRows = BuildRangeProfileRows(rangeHistories, movieById, profileRows);
        ApplyDistributions(snapshot, rangeProfileRows);
        ApplyMonthlyTagRankings(snapshot, rangeHistories, movieById);
        snapshot.ViewingTimeDistribution = BuildViewingTimeDistribution(rangeHistories);
        snapshot.WeekdayWeekendStats = BuildWeekdayWeekendStats(rangeHistories);
        snapshot.DurationDistribution = BuildDurationDistribution(rangeHistories, movieById, mediaDurationByMovieId, mediaDurationById);
        ApplyTasteCombinationMap(snapshot, rangeHistories, movieById);
        ApplyWatchLikeComparison(snapshot, histories, movieById, identifiedMovies, collectionItems);

        snapshot.HasTagData = snapshot.TypeDistribution.Count > 0
            || snapshot.EmotionDistribution.Count > 0
            || snapshot.SceneDistribution.Count > 0
            || snapshot.MonthlyFrequentTags.Count > 0;

        if (!snapshot.HasAnyData)
        {
            snapshot.EmptyReason = "No identified movies are available for Watch Insights statistics.";
        }

        if (!snapshot.HasWatchHistoryData)
        {
            snapshot.WarningMessages.Add("No valid watch history is available in the selected range; watch-history modules will remain empty.");
        }

        snapshot.WarningMessages.Add("Status changes before the state history table was created cannot be reconstructed.");
        snapshot.WarningMessages.Add("Language distribution uses the current stored language field; original_language is not available yet.");
        return snapshot;
    }

    private static async Task<List<MovieStatsRow>> LoadIdentifiedMoviesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.TmdbId.HasValue
                && x.TmdbId.Value > 0
                && !string.IsNullOrWhiteSpace(x.Title)
                && (x.IdentificationStatus == IdentificationStatus.Matched
                    || x.IdentificationStatus == IdentificationStatus.ManualConfirmed))
            .Select(x => new MovieStatsRow
            {
                Id = x.Id,
                Title = x.Title,
                TmdbId = x.TmdbId!.Value,
                ReleaseYear = x.ReleaseYear,
                Country = x.Country,
                Language = x.Language,
                RuntimeMinutes = x.RuntimeMinutes,
                GenresText = x.GenresText,
                AiTagsText = x.AiTagsText,
                EmotionTagsText = x.EmotionTagsText,
                SceneTagsText = x.SceneTagsText,
                IsFavorite = x.IsFavorite,
                IsWatched = x.IsWatched,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<CollectionItemStatsRow>> LoadIdentifiedCollectionItemsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.TmdbId.HasValue
                && x.TmdbId.Value > 0
                && (x.IsInLibrary || x.IsWatched || x.IsFavorite || x.IsWantToWatch || x.IsNotInterested)
                && !string.IsNullOrWhiteSpace(x.Title)
                && (!x.MovieId.HasValue
                    || dbContext.Movies.Any(movie => movie.Id == x.MovieId.Value
                        && movie.TmdbId == x.TmdbId
                        && (movie.IdentificationStatus == IdentificationStatus.Matched
                            || movie.IdentificationStatus == IdentificationStatus.ManualConfirmed))))
            .Select(x => new CollectionItemStatsRow
            {
                Id = x.Id,
                MovieId = x.MovieId,
                TmdbId = x.TmdbId!.Value,
                Title = x.Title,
                ReleaseYear = x.ReleaseYear,
                Country = x.Country,
                Language = x.Language,
                RuntimeMinutes = x.RuntimeMinutes,
                GenresText = x.GenresText,
                TmdbRating = x.TmdbRating,
                TmdbVoteCount = x.TmdbVoteCount,
                OmdbScoreValue = x.OmdbScoreValue,
                OmdbScoreScale = x.OmdbScoreScale,
                OmdbVoteCount = x.OmdbVoteCount,
                IsFavorite = x.IsFavorite,
                IsWantToWatch = x.IsWantToWatch,
                IsWatched = x.IsWatched,
                IsNotInterested = x.IsNotInterested,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static void ApplyStatusCounts(
        WatchStatisticsSnapshot snapshot,
        IReadOnlyCollection<MovieStatsRow> movies,
        IReadOnlyCollection<CollectionItemStatsRow> collectionItems,
        IReadOnlyCollection<StateChangeStatsRow> stateHistories,
        WatchStatisticsTimeRange timeRange,
        DateTime currentMonthStart)
    {
        snapshot.UnwatchedCount = 0;
        snapshot.UnwatchedDeltaFromLastWeek = null;

        if (timeRange == WatchStatisticsTimeRange.All)
        {
            var watchedKeys = movies
                .Where(x => x.IsWatched)
                .Select(x => BuildTmdbKey(x.TmdbId))
                .ToHashSet(StringComparer.Ordinal);
            watchedKeys.UnionWith(collectionItems
                .Where(x => x.IsWatched)
                .Select(x => BuildTmdbKey(x.TmdbId)));

            var favoriteKeys = movies
                .Where(x => x.IsFavorite)
                .Select(x => BuildTmdbKey(x.TmdbId))
                .ToHashSet(StringComparer.Ordinal);
            favoriteKeys.UnionWith(collectionItems
                .Where(x => x.IsFavorite)
                .Select(x => BuildTmdbKey(x.TmdbId)));

            var wantToWatchKeys = collectionItems
                .Where(x => x.IsWantToWatch)
                .Select(x => BuildTmdbKey(x.TmdbId))
                .ToHashSet(StringComparer.Ordinal);

            var notInterestedKeys = collectionItems
                .Where(x => x.IsNotInterested)
                .Select(x => BuildTmdbKey(x.TmdbId))
                .ToHashSet(StringComparer.Ordinal);

            snapshot.WatchedCount = watchedKeys.Count;
            snapshot.FavoriteCount = favoriteKeys.Count;
            snapshot.WantToWatchCount = wantToWatchKeys.Count;
            snapshot.NotInterestedCount = notInterestedKeys.Count;
            snapshot.WatchedDeltaFromLastWeek = null;
            snapshot.FavoriteDeltaFromLastWeek = null;
            snapshot.WantToWatchDeltaFromLastWeek = null;
            snapshot.NotInterestedDeltaFromLastWeek = null;
            return;
        }

        var currentMonthEnd = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var watchedActiveStates = movies
            .Where(x => x.IsWatched)
            .Select(x => new ActiveStateStatsRow(BuildTmdbKey(x.TmdbId), x.CreatedAt))
            .Concat(collectionItems
                .Where(x => x.IsWatched)
                .Select(x => new ActiveStateStatsRow(BuildTmdbKey(x.TmdbId), x.CreatedAt)))
            .ToList();
        var favoriteActiveStates = movies
            .Where(x => x.IsFavorite)
            .Select(x => new ActiveStateStatsRow(BuildTmdbKey(x.TmdbId), x.CreatedAt))
            .Concat(collectionItems
                .Where(x => x.IsFavorite)
                .Select(x => new ActiveStateStatsRow(BuildTmdbKey(x.TmdbId), x.CreatedAt)))
            .ToList();
        var wantToWatchActiveStates = collectionItems
            .Where(x => x.IsWantToWatch)
            .Select(x => new ActiveStateStatsRow(BuildTmdbKey(x.TmdbId), x.CreatedAt))
            .ToList();
        var notInterestedActiveStates = collectionItems
            .Where(x => x.IsNotInterested)
            .Select(x => new ActiveStateStatsRow(BuildTmdbKey(x.TmdbId), x.CreatedAt))
            .ToList();

        snapshot.WatchedCount = CountMonthlyActiveStateAdds(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            watchedActiveStates,
            currentMonthStart,
            currentMonthEnd);
        snapshot.FavoriteCount = CountMonthlyActiveStateAdds(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            favoriteActiveStates,
            currentMonthStart,
            currentMonthEnd);
        snapshot.WantToWatchCount = CountMonthlyActiveStateAdds(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            wantToWatchActiveStates,
            currentMonthStart,
            currentMonthEnd);
        snapshot.NotInterestedCount = CountMonthlyActiveStateAdds(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            notInterestedActiveStates,
            currentMonthStart,
            currentMonthEnd);

        snapshot.WatchedDeltaFromLastWeek = CalculateMonthlyActiveStateDelta(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            watchedActiveStates,
            previousMonthStart,
            currentMonthStart,
            currentMonthEnd);
        snapshot.FavoriteDeltaFromLastWeek = CalculateMonthlyActiveStateDelta(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            favoriteActiveStates,
            previousMonthStart,
            currentMonthStart,
            currentMonthEnd);
        snapshot.WantToWatchDeltaFromLastWeek = CalculateMonthlyActiveStateDelta(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            wantToWatchActiveStates,
            previousMonthStart,
            currentMonthStart,
            currentMonthEnd);
        snapshot.NotInterestedDeltaFromLastWeek = CalculateMonthlyActiveStateDelta(
            stateHistories,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            notInterestedActiveStates,
            previousMonthStart,
            currentMonthStart,
            currentMonthEnd);
    }

    private static int CountMonthlyActiveStateAdds(
        IReadOnlyCollection<StateChangeStatsRow> stateHistories,
        string stateType,
        IEnumerable<ActiveStateStatsRow> activeStates,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var activeStateByKey = activeStates
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(item => item.FallbackChangedAtUtc)
                    .First(),
                StringComparer.Ordinal);
        if (activeStateByKey.Count == 0)
        {
            return 0;
        }

        var latestHistoryByKey = stateHistories
            .Where(x => string.Equals(x.StateType, stateType, StringComparison.Ordinal)
                && activeStateByKey.ContainsKey(BuildTmdbKey(x.TmdbId)))
            .GroupBy(x => BuildTmdbKey(x.TmdbId), StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(item => item.ChangedAtUtc)
                    .ThenByDescending(item => item.Id)
                    .First(),
                StringComparer.Ordinal);

        var count = 0;
        foreach (var activeState in activeStateByKey.Values)
        {
            if (latestHistoryByKey.TryGetValue(activeState.Key, out var latestHistory))
            {
                if (latestHistory.NewValue
                    && IsWithinLocalRange(latestHistory.ChangedAtUtc, rangeStart, rangeEnd))
                {
                    count++;
                    continue;
                }

                if (!latestHistory.NewValue
                    && activeState.FallbackChangedAtUtc > latestHistory.ChangedAtUtc
                    && IsWithinLocalRange(activeState.FallbackChangedAtUtc, rangeStart, rangeEnd))
                {
                    count++;
                }

                continue;
            }

            if (IsWithinLocalRange(activeState.FallbackChangedAtUtc, rangeStart, rangeEnd))
            {
                count++;
            }
        }

        return count;
    }

    private static int? CalculateMonthlyActiveStateDelta(
        IReadOnlyCollection<StateChangeStatsRow> stateHistories,
        string stateType,
        IReadOnlyCollection<ActiveStateStatsRow> activeStates,
        DateTime previousMonthStart,
        DateTime currentMonthStart,
        DateTime currentMonthEnd)
    {
        var previousCount = CountMonthlyActiveStateAdds(
            stateHistories,
            stateType,
            activeStates,
            previousMonthStart,
            currentMonthStart);
        if (previousCount == 0)
        {
            return null;
        }

        var currentCount = CountMonthlyActiveStateAdds(
            stateHistories,
            stateType,
            activeStates,
            currentMonthStart,
            currentMonthEnd);
        return currentCount - previousCount;
    }

    private static List<WatchStatisticsTagItem> BuildMonthlyFrequentTags(
        IReadOnlyCollection<WatchHistoryStatsRow> monthlyHistories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById,
        int take)
    {
        var accumulator = new Dictionary<string, TagAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var movie in EnumerateDistinctWatchedMovies(monthlyHistories, movieById))
        {
            foreach (var tag in GetTypeTags(movie).Concat(GetEmotionTags(movie)).Concat(GetSceneTags(movie)))
            {
                AddWeightedTag(accumulator, tag, 0);
            }
        }

        return BuildTagItems(accumulator, take);
    }

    private static List<WatchCalendarDay> BuildCalendarDays(
        DateTime monthStart,
        IReadOnlyCollection<WatchHistoryStatsRow> monthlyHistories)
    {
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var byDate = monthlyHistories
            .GroupBy(x => ToLocalTime(x.StartedAt).Date)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    WatchSeconds = x.Sum(item => (long)item.DurationWatchedSeconds),
                    WatchCount = x.Count()
                });
        var days = new List<WatchCalendarDay>(daysInMonth);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(monthStart.Year, monthStart.Month, day);
            byDate.TryGetValue(date, out var item);
            var watchSeconds = item?.WatchSeconds ?? 0;
            days.Add(new WatchCalendarDay
            {
                Date = date,
                WatchSeconds = watchSeconds,
                WatchCount = item?.WatchCount ?? 0,
                HeatLevel = CalculateHeatLevel(watchSeconds),
                HasValidWatch = watchSeconds > 0
            });
        }

        return days;
    }

    private static void ApplyMonthlyCards(
        WatchStatisticsSnapshot snapshot,
        IReadOnlyCollection<WatchHistoryStatsRow> monthlyHistories)
    {
        var byDate = monthlyHistories
            .GroupBy(x => ToLocalTime(x.StartedAt).Date)
            .Select(x => new
            {
                Date = x.Key,
                WatchSeconds = x.Sum(item => (long)item.DurationWatchedSeconds)
            })
            .OrderBy(x => x.Date)
            .ToList();

        snapshot.MonthlyWatchDays = byDate.Count;
        var activeDates = byDate.Select(x => x.Date).ToHashSet();
        snapshot.ContinuousWatchDays = CalculateLongestContinuousWatchDays(activeDates);
        var mostActive = byDate
            .OrderByDescending(x => x.WatchSeconds)
            .ThenBy(x => x.Date)
            .FirstOrDefault();
        if (mostActive is not null)
        {
            snapshot.MostActiveDate = mostActive.Date;
            snapshot.MostActiveDateWatchSeconds = mostActive.WatchSeconds;
        }
    }

    private static void ApplyDistributions(
        WatchStatisticsSnapshot snapshot,
        IReadOnlyCollection<MovieProfileRow> profileRows)
    {
        snapshot.TypeDistribution = BuildDistribution(profileRows.SelectMany(x => x.TypeTags));
        snapshot.EmotionDistribution = BuildDistribution(profileRows.SelectMany(x => x.EmotionTags));
        snapshot.SceneDistribution = BuildDistribution(profileRows.SelectMany(x => x.SceneTags));
        snapshot.YearDistribution = BuildDistribution(profileRows
            .Where(x => x.ReleaseYear.HasValue)
            .Select(x => x.ReleaseYear!.Value.ToString()));
        snapshot.CountryDistribution = BuildDistribution(profileRows.SelectMany(x => SplitTags(x.Country)));
        snapshot.LanguageDistribution = BuildDistribution(profileRows.SelectMany(x => SplitTags(x.Language)));
        snapshot.RatingDistribution = BuildDistribution(profileRows
            .Select(x => x.WeightedRating)
            .Where(x => x.HasValue)
            .Select(x => BuildRatingBucket(x!.Value)));
    }

    private static void ApplyMonthlyTagRankings(
        WatchStatisticsSnapshot snapshot,
        IReadOnlyCollection<WatchHistoryStatsRow> monthlyHistories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById)
    {
        snapshot.MonthlyTypeTagTop3 = BuildMonthlyTagRanking(monthlyHistories, movieById, GetTypeTags, TopSmallTagCount);
        snapshot.MonthlyEmotionTagTop3 = BuildMonthlyTagRanking(monthlyHistories, movieById, GetEmotionTags, TopSmallTagCount);
        snapshot.MonthlySceneTagTop3 = BuildMonthlyTagRanking(monthlyHistories, movieById, GetSceneTags, TopSmallTagCount);
    }

    private static List<WatchStatisticsTagItem> BuildMonthlyTagRanking(
        IReadOnlyCollection<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById,
        Func<MovieStatsRow, IReadOnlyCollection<string>> tagSelector,
        int take)
    {
        var accumulator = new Dictionary<string, TagAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var movie in EnumerateDistinctWatchedMovies(histories, movieById))
        {
            foreach (var tag in tagSelector(movie))
            {
                AddWeightedTag(accumulator, tag, 0);
            }
        }

        return BuildTagItems(accumulator, take);
    }

    private static List<ViewingTimeBucket> BuildViewingTimeDistribution(
        IReadOnlyCollection<WatchHistoryStatsRow> histories)
    {
        var buckets = ViewingTimeBucketDefinitions
            .Select(x => new ViewingTimeBucket
            {
                Label = x.Label,
                StartHour = x.StartHour,
                EndHour = x.EndHour
            })
            .ToList();

        foreach (var history in histories)
        {
            var localStartedAt = ToLocalTime(history.StartedAt);
            var bucket = buckets.First(x => localStartedAt.Hour >= x.StartHour && localStartedAt.Hour < x.EndHour);
            bucket.WatchCount++;
            bucket.WatchSeconds += history.DurationWatchedSeconds;
        }

        return buckets;
    }

    private static WeekdayWeekendWatchStats BuildWeekdayWeekendStats(
        IReadOnlyCollection<WatchHistoryStatsRow> histories)
    {
        var weekdaySeconds = 0L;
        var weekendSeconds = 0L;
        var weekdayDates = new HashSet<DateTime>();
        var weekendDates = new HashSet<DateTime>();

        foreach (var history in histories)
        {
            var date = ToLocalTime(history.StartedAt).Date;
            if (IsWeekend(date))
            {
                weekendSeconds += history.DurationWatchedSeconds;
                weekendDates.Add(date);
            }
            else
            {
                weekdaySeconds += history.DurationWatchedSeconds;
                weekdayDates.Add(date);
            }
        }

        var totalSeconds = weekdaySeconds + weekendSeconds;
        return new WeekdayWeekendWatchStats
        {
            WeekdayWatchSeconds = weekdaySeconds,
            WeekendWatchSeconds = weekendSeconds,
            WeekdayAverageSeconds = weekdayDates.Count == 0 ? 0 : weekdaySeconds / (double)weekdayDates.Count,
            WeekendAverageSeconds = weekendDates.Count == 0 ? 0 : weekendSeconds / (double)weekendDates.Count,
            WeekdayRatio = totalSeconds == 0 ? 0 : weekdaySeconds / (double)totalSeconds,
            WeekendRatio = totalSeconds == 0 ? 0 : weekendSeconds / (double)totalSeconds
        };
    }

    private static List<DurationDistributionItem> BuildDurationDistribution(
        IReadOnlyCollection<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById,
        IReadOnlyDictionary<int, int> mediaDurationByMovieId,
        IReadOnlyDictionary<int, int> mediaDurationById)
    {
        var runtimeByMovieId = new Dictionary<int, int>();
        foreach (var history in histories)
        {
            if (runtimeByMovieId.ContainsKey(history.MovieId))
            {
                continue;
            }

            if (movieById.TryGetValue(history.MovieId, out var movie)
                && movie.RuntimeMinutes.HasValue
                && movie.RuntimeMinutes.Value > 0)
            {
                runtimeByMovieId[history.MovieId] = movie.RuntimeMinutes.Value;
                continue;
            }

            if (mediaDurationById.TryGetValue(history.MediaFileId, out var mediaSeconds) && mediaSeconds > 0)
            {
                runtimeByMovieId[history.MovieId] = (int)Math.Ceiling(mediaSeconds / 60d);
                continue;
            }

            if (mediaDurationByMovieId.TryGetValue(history.MovieId, out var movieMediaSeconds) && movieMediaSeconds > 0)
            {
                runtimeByMovieId[history.MovieId] = (int)Math.Ceiling(movieMediaSeconds / 60d);
            }
        }

        var bucketCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Short"] = 0,
            ["Medium"] = 0,
            ["Long"] = 0,
            ["ExtraLong"] = 0
        };

        foreach (var runtimeMinutes in runtimeByMovieId.Values)
        {
            bucketCounts[BuildDurationBucket(runtimeMinutes)]++;
        }

        var total = bucketCounts.Values.Sum();
        return
        [
            BuildDurationDistributionItem("Short", bucketCounts["Short"], total, 0, 60),
            BuildDurationDistributionItem("Medium", bucketCounts["Medium"], total, 61, 120),
            BuildDurationDistributionItem("Long", bucketCounts["Long"], total, 121, 180),
            BuildDurationDistributionItem("ExtraLong", bucketCounts["ExtraLong"], total, 181, null)
        ];
    }

    private static void ApplyTasteCombinationMap(
        WatchStatisticsSnapshot snapshot,
        IReadOnlyCollection<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById)
    {
        var combinations = new Dictionary<string, CombinationAccumulator>(StringComparer.OrdinalIgnoreCase);
        var nodes = new Dictionary<string, TasteCombinationNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, TasteCombinationEdge>(StringComparer.OrdinalIgnoreCase);

        foreach (var movie in EnumerateDistinctWatchedMovies(histories, movieById))
        {
            var typeTags = GetTypeTags(movie);
            var emotionTags = GetEmotionTags(movie);
            var sceneTags = GetSceneTags(movie);
            if (typeTags.Count == 0 || emotionTags.Count == 0 || sceneTags.Count == 0)
            {
                continue;
            }

            foreach (var typeTag in typeTags)
            {
                foreach (var emotionTag in emotionTags)
                {
                    foreach (var sceneTag in sceneTags)
                    {
                        var key = $"{typeTag}|{emotionTag}|{sceneTag}";
                        if (!combinations.TryGetValue(key, out var accumulator))
                        {
                            accumulator = new CombinationAccumulator(typeTag, emotionTag, sceneTag);
                            combinations[key] = accumulator;
                        }

                        accumulator.OccurrenceCount++;
                        AddNode(nodes, "type", typeTag);
                        AddNode(nodes, "emotion", emotionTag);
                        AddNode(nodes, "scene", sceneTag);
                        AddEdge(edges, BuildNodeId("type", typeTag), BuildNodeId("emotion", emotionTag));
                        AddEdge(edges, BuildNodeId("emotion", emotionTag), BuildNodeId("scene", sceneTag));
                    }
                }
            }
        }

        snapshot.TasteCombinationTop10 = combinations.Values
            .Select(x => new TasteCombinationItem
            {
                Type = x.Type,
                Emotion = x.Emotion,
                Scene = x.Scene,
                OccurrenceCount = x.OccurrenceCount,
                WatchSeconds = x.WatchSeconds,
                Score = x.OccurrenceCount
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        snapshot.TasteCombinationNodes = nodes.Values
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        snapshot.TasteCombinationEdges = edges.Values
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.TargetId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ApplyWatchLikeComparison(
        WatchStatisticsSnapshot snapshot,
        IReadOnlyCollection<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById,
        IReadOnlyCollection<MovieStatsRow> identifiedMovies,
        IReadOnlyCollection<CollectionItemStatsRow> collectionItems)
    {
        snapshot.OftenWatchedTop3 = BuildMonthlyTagRanking(histories, movieById, GetTypeTags, TopSmallTagCount);

        var likedAccumulator = new Dictionary<string, TagAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var movie in identifiedMovies.Where(x => x.IsFavorite))
        {
            foreach (var tag in GetTypeTags(movie))
            {
                AddWeightedTag(likedAccumulator, tag, 0);
            }
        }
        foreach (var item in collectionItems.Where(x => x.IsFavorite))
        {
            foreach (var tag in SplitTags(item.GenresText))
            {
                AddWeightedTag(likedAccumulator, tag, 0);
            }
        }

        snapshot.OftenLikedTop3 = BuildTagItems(likedAccumulator, TopSmallTagCount);

        var wantedAccumulator = new Dictionary<string, TagAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in collectionItems.Where(x => x.IsWantToWatch))
        {
            foreach (var tag in SplitTags(item.GenresText))
            {
                AddWeightedTag(wantedAccumulator, tag, 0);
            }
        }

        snapshot.OftenWantedTop3 = BuildTagItems(wantedAccumulator, TopSmallTagCount);
        snapshot.InsightConclusion = string.Empty;
    }

    private static List<MovieProfileRow> BuildProfileRows(
        IReadOnlyCollection<MovieStatsRow> movies,
        IReadOnlyCollection<CollectionItemStatsRow> collectionItems,
        IReadOnlyDictionary<int, double?> ratingByMovieId)
    {
        var rowsByKey = new Dictionary<string, MovieProfileRow>(StringComparer.Ordinal);
        foreach (var movie in movies)
        {
            rowsByKey[BuildTmdbKey(movie.TmdbId)] = new MovieProfileRow
            {
                TmdbId = movie.TmdbId,
                ReleaseYear = movie.ReleaseYear,
                Country = movie.Country ?? string.Empty,
                Language = movie.Language ?? string.Empty,
                TypeTags = GetTypeTags(movie).ToList(),
                EmotionTags = GetEmotionTags(movie).ToList(),
                SceneTags = GetSceneTags(movie).ToList(),
                WeightedRating = ratingByMovieId.TryGetValue(movie.Id, out var rating) ? rating : null
            };
        }

        foreach (var item in collectionItems)
        {
            var key = BuildTmdbKey(item.TmdbId);
            if (rowsByKey.ContainsKey(key))
            {
                continue;
            }

            rowsByKey[key] = new MovieProfileRow
            {
                TmdbId = item.TmdbId,
                ReleaseYear = item.ReleaseYear,
                Country = item.Country,
                Language = item.Language,
                TypeTags = SplitTags(item.GenresText).ToList(),
                WeightedRating = CalculateWeightedRating(item)
            };
        }

        return rowsByKey.Values.ToList();
    }

    private static List<WatchDistributionItem> BuildDistribution(IEnumerable<string> labels)
    {
        var grouped = labels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new
            {
                Label = x.First(),
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var total = grouped.Sum(x => x.Count);

        return grouped
            .Select(x => new WatchDistributionItem
            {
                Label = x.Label,
                Count = x.Count,
                Percent = total == 0 ? 0 : x.Count / (double)total,
                Score = x.Count
            })
            .ToList();
    }

    private static void AddWeightedTag(
        IDictionary<string, TagAccumulator> accumulator,
        string tag,
        long watchSeconds)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (!accumulator.TryGetValue(tag, out var item))
        {
            item = new TagAccumulator(tag);
            accumulator[tag] = item;
        }

        item.Count++;
        item.WatchSeconds += watchSeconds;
    }

    private static List<WatchStatisticsTagItem> BuildTagItems(
        IEnumerable<TagAccumulator> accumulators,
        int take)
    {
        return accumulators
            .Select(x => new WatchStatisticsTagItem
            {
                Label = x.Label,
                Count = x.Count,
                WatchSeconds = x.WatchSeconds,
                Score = x.WatchSeconds > 0 ? x.WatchSeconds : x.Count
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static List<WatchStatisticsTagItem> BuildTagItems(
        IReadOnlyDictionary<string, TagAccumulator> accumulator,
        int take)
    {
        return BuildTagItems(accumulator.Values, take);
    }

    private static IReadOnlyCollection<string> GetTypeTags(MovieStatsRow movie)
    {
        var aiTags = SplitTags(movie.AiTagsText).ToList();
        return aiTags.Count > 0 ? aiTags : SplitTags(movie.GenresText).ToList();
    }

    private static IReadOnlyCollection<string> GetEmotionTags(MovieStatsRow movie)
    {
        return SplitTags(movie.EmotionTagsText).ToList();
    }

    private static IReadOnlyCollection<string> GetSceneTags(MovieStatsRow movie)
    {
        return SplitTags(movie.SceneTagsText).ToList();
    }

    private static IEnumerable<string> SplitTags(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "unknown",
            "none",
            "n/a",
            "not provided",
            "unclassified"
        };
        return text
            .Split(['、', '/', ',', '，', ';', '；', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !ignored.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int CalculateHeatLevel(long watchSeconds)
    {
        if (watchSeconds <= 0)
        {
            return 0;
        }

        return watchSeconds switch
        {
            <= 30 * 60 => 1,
            <= 60 * 60 => 2,
            <= 2 * 60 * 60 => 3,
            _ => 4
        };
    }

    private static int CalculateLongestContinuousWatchDays(IReadOnlySet<DateTime> activeDates)
    {
        if (activeDates.Count == 0)
        {
            return 0;
        }

        var longest = 0;
        var current = 0;
        DateTime? previous = null;
        foreach (var date in activeDates.OrderBy(x => x))
        {
            current = previous.HasValue && date == previous.Value.AddDays(1)
                ? current + 1
                : 1;
            longest = Math.Max(longest, current);
            previous = date;
        }

        return longest;
    }

    private static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static DurationDistributionItem BuildDurationDistributionItem(
        string label,
        int count,
        int total,
        int minMinutes,
        int? maxMinutes)
    {
        return new DurationDistributionItem
        {
            Label = label,
            Count = count,
            Percent = total == 0 ? 0 : count / (double)total,
            MinMinutes = minMinutes,
            MaxMinutes = maxMinutes
        };
    }

    private static string BuildDurationBucket(int runtimeMinutes)
    {
        if (runtimeMinutes <= 60)
        {
            return "Short";
        }

        if (runtimeMinutes <= 120)
        {
            return "Medium";
        }

        return runtimeMinutes <= 180 ? "Long" : "ExtraLong";
    }

    private static double? CalculateWeightedRating(IEnumerable<RatingSourceStatsRow> ratingSources)
    {
        var normalizedSources = ratingSources
            .Where(x => x.ScoreScale > 0 && x.ScoreValue > 0)
            .Select(x => new
            {
                Rating = NormalizeRatingToTen(x.ScoreValue, x.ScoreScale),
                Weight = Math.Max(1, x.VoteCount ?? 0)
            })
            .ToList();

        if (normalizedSources.Count == 0)
        {
            return null;
        }

        var weightSum = normalizedSources.Sum(x => x.Weight);
        return normalizedSources.Sum(x => x.Rating * x.Weight) / weightSum;
    }

    private static double? CalculateWeightedRating(CollectionItemStatsRow item)
    {
        var sources = new List<(double Rating, int Weight)>();
        if (item.TmdbRating.HasValue && item.TmdbRating.Value > 0)
        {
            sources.Add((NormalizeRatingToTen(item.TmdbRating.Value, 10d), Math.Max(1, item.TmdbVoteCount ?? 0)));
        }

        if (item.OmdbScoreValue.HasValue && item.OmdbScoreScale.HasValue && item.OmdbScoreScale.Value > 0)
        {
            sources.Add((NormalizeRatingToTen(item.OmdbScoreValue.Value, item.OmdbScoreScale.Value), Math.Max(1, item.OmdbVoteCount ?? 0)));
        }

        if (sources.Count == 0)
        {
            return null;
        }

        var weightSum = sources.Sum(x => x.Weight);
        return sources.Sum(x => x.Rating * x.Weight) / weightSum;
    }

    private static double NormalizeRatingToTen(double value, double scale)
    {
        return Math.Clamp(value / scale * 10d, 0d, 10d);
    }

    private static string BuildRatingBucket(double rating)
    {
        if (rating < 5d)
        {
            return "<5";
        }

        if (rating < 7d)
        {
            return "5-6.9";
        }

        return rating < 9d ? "7-8.9" : "9-10";
    }

    private static void AddNode(
        IDictionary<string, TasteCombinationNode> nodes,
        string kind,
        string label)
    {
        var id = BuildNodeId(kind, label);
        if (!nodes.TryGetValue(id, out var node))
        {
            node = new TasteCombinationNode
            {
                Id = id,
                Label = label,
                Kind = kind
            };
            nodes[id] = node;
        }

        node.Count++;
        node.Weight = node.Count;
    }

    private static void AddEdge(
        IDictionary<string, TasteCombinationEdge> edges,
        string sourceId,
        string targetId)
    {
        var id = $"{sourceId}->{targetId}";
        if (!edges.TryGetValue(id, out var edge))
        {
            edge = new TasteCombinationEdge
            {
                SourceId = sourceId,
                TargetId = targetId
            };
            edges[id] = edge;
        }

        edge.Count++;
        edge.Weight = edge.Count;
    }

    private static string BuildNodeId(string kind, string label)
    {
        return $"{kind}:{label.Trim().ToLowerInvariant()}";
    }

    private static WatchStatisticsTimeRange NormalizeTimeRange(WatchStatisticsTimeRange timeRange)
    {
        return Enum.IsDefined(timeRange) ? timeRange : WatchStatisticsTimeRange.Month;
    }

    private static DateTime NormalizeCalendarMonth(DateTime? calendarMonth, DateTime nowLocal)
    {
        var source = calendarMonth ?? nowLocal;
        return new DateTime(source.Year, source.Month, 1);
    }

    private static string BuildScopeKey(
        WatchStatisticsTimeRange timeRange,
        DateTime nowLocal,
        DateTime calendarMonth)
    {
        var rangeKey = timeRange == WatchStatisticsTimeRange.Month
            ? $"month:{nowLocal:yyyyMM}"
            : "all";
        return $"range:{rangeKey}:calendar:{calendarMonth:yyyyMM}";
    }

    private static string FormatRange(WatchStatisticsTimeRange timeRange)
    {
        return timeRange == WatchStatisticsTimeRange.All ? "all" : "month";
    }

    private static DateTime ToMonthStart(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1);
    }

    private static List<WatchHistoryStatsRow> FilterHistoriesByRange(
        IEnumerable<WatchHistoryStatsRow> histories,
        DateTime? rangeStart,
        DateTime? rangeEnd)
    {
        return histories
            .Where(x =>
            {
                var localStartedAt = ToLocalTime(x.StartedAt);
                return (!rangeStart.HasValue || localStartedAt >= rangeStart.Value)
                    && (!rangeEnd.HasValue || localStartedAt < rangeEnd.Value);
            })
            .ToList();
    }

    private static bool IsWithinLocalRange(
        DateTime value,
        DateTime? rangeStart,
        DateTime? rangeEnd)
    {
        var localValue = ToLocalTime(value);
        return (!rangeStart.HasValue || localValue >= rangeStart.Value)
            && (!rangeEnd.HasValue || localValue < rangeEnd.Value);
    }

    private static IEnumerable<MovieStatsRow> EnumerateDistinctWatchedMovies(
        IEnumerable<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById)
    {
        var seenTmdbIds = new HashSet<int>();
        foreach (var movieId in histories.Select(x => x.MovieId).Distinct())
        {
            if (movieById.TryGetValue(movieId, out var movie) && seenTmdbIds.Add(movie.TmdbId))
            {
                yield return movie;
            }
        }
    }

    private static int CountDistinctWatchedMovies(
        IEnumerable<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById)
    {
        return EnumerateDistinctWatchedMovies(histories, movieById).Count();
    }

    private static List<MovieProfileRow> BuildRangeProfileRows(
        IEnumerable<WatchHistoryStatsRow> histories,
        IReadOnlyDictionary<int, MovieStatsRow> movieById,
        IReadOnlyCollection<MovieProfileRow> allProfileRows)
    {
        var tmdbIds = EnumerateDistinctWatchedMovies(histories, movieById)
            .Select(x => x.TmdbId)
            .ToHashSet();
        return allProfileRows
            .Where(x => tmdbIds.Contains(x.TmdbId))
            .ToList();
    }

    private static WatchStatisticsSnapshot CreateEmptySnapshot(
        string fingerprint,
        DateTime nowUtc,
        WatchStatisticsTimeRange timeRange,
        DateTime calendarMonth,
        string emptyReason,
        string warning)
    {
        var nowLocal = ToLocalTime(nowUtc);
        var currentMonthStart = new DateTime(nowLocal.Year, nowLocal.Month, 1);
        return new WatchStatisticsSnapshot
        {
            GeneratedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddHours(CacheHours),
            SourceFingerprint = fingerprint,
            TimeRange = timeRange,
            CalendarMonth = calendarMonth,
            EarliestCalendarMonth = currentMonthStart,
            LatestCalendarMonth = currentMonthStart,
            CalendarDays = BuildCalendarDays(calendarMonth, []),
            EmptyReason = emptyReason,
            WarningMessages = [warning]
        };
    }

    private static string BuildTmdbKey(int tmdbId)
    {
        return $"tmdb:{tmdbId}";
    }

    private static string FormatFingerprintDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToUniversalTime().Ticks.ToString() : "0";
    }

    private static DateTime ToLocalTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Local)
        {
            return value;
        }

        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToLocalTime();
    }

    private static string ShortFingerprint(string fingerprint)
    {
        return fingerprint.Length <= 12 ? fingerprint : fingerprint[..12];
    }

    private static void Log(string message)
    {
        Debug.WriteLine("[WATCH-STATISTICS] " + message);
    }

    private readonly record struct ViewingTimeBucketDefinition(int StartHour, int EndHour, string Label);

    private sealed class MovieStatsRow
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int TmdbId { get; set; }

        public int? ReleaseYear { get; set; }

        public string? Country { get; set; }

        public string? Language { get; set; }

        public int? RuntimeMinutes { get; set; }

        public string? GenresText { get; set; }

        public string? AiTagsText { get; set; }

        public string? EmotionTagsText { get; set; }

        public string? SceneTagsText { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsWatched { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    private sealed class CollectionItemStatsRow
    {
        public int Id { get; set; }

        public int? MovieId { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int? ReleaseYear { get; set; }

        public string Country { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public int? RuntimeMinutes { get; set; }

        public string GenresText { get; set; } = string.Empty;

        public double? TmdbRating { get; set; }

        public int? TmdbVoteCount { get; set; }

        public double? OmdbScoreValue { get; set; }

        public double? OmdbScoreScale { get; set; }

        public int? OmdbVoteCount { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsWantToWatch { get; set; }

        public bool IsWatched { get; set; }

        public bool IsNotInterested { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    private sealed record ActiveStateStatsRow(string Key, DateTime FallbackChangedAtUtc);

    private sealed class MediaFileStatsRow
    {
        public int Id { get; set; }

        public int MovieId { get; set; }

        public int? DurationSeconds { get; set; }
    }

    private sealed class RatingSourceStatsRow
    {
        public int MovieId { get; set; }

        public string SourceName { get; set; } = string.Empty;

        public double ScoreValue { get; set; }

        public double ScoreScale { get; set; }

        public int? VoteCount { get; set; }
    }

    private sealed class WatchHistoryStatsRow
    {
        public int Id { get; set; }

        public int MovieId { get; set; }

        public int MediaFileId { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public int DurationWatchedSeconds { get; set; }
    }

    private sealed class StateChangeStatsRow
    {
        public long Id { get; set; }

        public int TmdbId { get; set; }

        public int? MovieId { get; set; }

        public int? UserMovieCollectionItemId { get; set; }

        public string StateType { get; set; } = string.Empty;

        public bool NewValue { get; set; }

        public DateTime ChangedAtUtc { get; set; }
    }

    private sealed class MovieProfileRow
    {
        public int TmdbId { get; set; }

        public int? ReleaseYear { get; set; }

        public string Country { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public List<string> TypeTags { get; set; } = [];

        public List<string> EmotionTags { get; set; } = [];

        public List<string> SceneTags { get; set; } = [];

        public double? WeightedRating { get; set; }
    }

    private sealed class TagAccumulator
    {
        public TagAccumulator(string label)
        {
            Label = label;
        }

        public string Label { get; }

        public int Count { get; set; }

        public long WatchSeconds { get; set; }
    }

    private sealed class CombinationAccumulator
    {
        public CombinationAccumulator(string type, string emotion, string scene)
        {
            Type = type;
            Emotion = emotion;
            Scene = scene;
        }

        public string Type { get; }

        public string Emotion { get; }

        public string Scene { get; }

        public int OccurrenceCount { get; set; }

        public long WatchSeconds { get; set; }
    }
}
