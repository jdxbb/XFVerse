using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WatchProfileInputService : IWatchProfileInputService
{
    private const int ValidWatchSecondsThreshold = 60;
    private const int MaxWatchedSamples = 30;
    private const int MaxFavoriteSamples = 30;
    private const int MaxWantSamples = 30;
    private const int MaxNotInterestedSamples = 50;
    private const int MaxHistorySamples = 30;

    private static readonly HashSet<string> ImmersiveTags =
    [
        "压抑", "悲伤", "震撼", "悬疑", "紧张", "沉重", "烧脑", "犯罪", "惊悚", "恐怖", "暗黑", "黑色"
    ];

    private static readonly HashSet<string> EasyTags =
    [
        "喜剧", "治愈", "轻松", "温暖", "合家欢", "下饭", "家庭", "亲子", "日常"
    ];

    private readonly IWatchStatisticsService _statisticsService;

    public WatchProfileInputService(IWatchStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public async Task<WatchProfileInputSnapshot> BuildProfileInputAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var nowUtc = DateTime.UtcNow;

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movies = await LoadIdentifiedMoviesAsync(dbContext, cancellationToken);
        var movieIds = movies.Select(x => x.Id).ToHashSet();
        var collectionItems = await LoadIdentifiedCollectionItemsAsync(dbContext, cancellationToken);
        var ratingSources = movieIds.Count == 0
            ? []
            : await LoadRatingSourcesAsync(dbContext, movieIds, cancellationToken);
        var histories = movieIds.Count == 0
            ? []
            : await LoadValidWatchHistoriesAsync(dbContext, movieIds, cancellationToken);

        var samples = BuildSignalSamples(movies, collectionItems, ratingSources, histories);
        var signalSamples = samples
            .Where(x => x.IsWatched || x.IsFavorite || x.IsWantToWatch || x.IsNotInterested || x.WatchCount > 0)
            .OrderByDescending(x => x.SortAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tagSet = signalSamples
            .SelectMany(x => x.TypeTags.Concat(x.EmotionTags).Concat(x.SceneTags))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bucketCount = CountBuckets(signalSamples);
        var xAxisScore = CalculateXAxisScore(signalSamples);
        var yAxisScore = CalculateYAxisScore(signalSamples);
        var statistics = await _statisticsService.GetStatisticsAsync(
            WatchStatisticsTimeRange.All,
            calendarMonth: null,
            forceRefresh: false,
            cancellationToken);
        var statisticsSummary = BuildStatisticsSummary(statistics, signalSamples);
        var fingerprint = BuildSourceFingerprint(signalSamples, histories, statisticsSummary);

        var snapshot = new WatchProfileInputSnapshot
        {
            GeneratedAtUtc = nowUtc,
            SourceFingerprint = fingerprint,
            SignalMovieCount = signalSamples.Count,
            BucketCount = bucketCount,
            TagCount = tagSet.Count,
            LocalXAxisScore = xAxisScore,
            LocalYAxisScore = yAxisScore,
            LocalQuadrantName = BuildQuadrantName(xAxisScore, yAxisScore),
            WatchedSamples = SelectPromptSamples(
                signalSamples.Where(x => x.IsWatched || x.WatchCount > 0),
                MaxWatchedSamples),
            FavoriteSamples = SelectPromptSamples(
                signalSamples.Where(x => x.IsFavorite),
                MaxFavoriteSamples),
            WantToWatchSamples = SelectPromptSamples(
                signalSamples.Where(x => x.IsWantToWatch),
                MaxWantSamples),
            NotInterestedSamples = SelectPromptSamples(
                signalSamples.Where(x => x.IsNotInterested),
                MaxNotInterestedSamples),
            RecentHistorySamples = histories
                .OrderByDescending(x => x.StartedAt)
                .Take(MaxHistorySamples)
                .Select(x =>
                {
                    var movie = movies.FirstOrDefault(item => item.Id == x.MovieId);
                    return new WatchProfileHistorySample
                    {
                        MovieId = x.MovieId,
                        TmdbId = movie?.TmdbId ?? 0,
                        Title = movie?.Title ?? string.Empty,
                        ReleaseYear = movie?.ReleaseYear,
                        StartedAtUtc = EnsureUtc(x.StartedAt),
                        WatchSeconds = x.DurationWatchedSeconds,
                        IsCompleted = x.IsCompleted
                    };
                })
                .Where(x => x.TmdbId > 0 && !string.IsNullOrWhiteSpace(x.Title))
                .ToList(),
            StatisticsSummary = statisticsSummary
        };

        ApplySufficiency(snapshot);
        stopwatch.Stop();
        Log(
            "watch-profile-input-built "
            + $"signalMovies={snapshot.SignalMovieCount} "
            + $"buckets={snapshot.BucketCount} "
            + $"tags={snapshot.TagCount} "
            + $"elapsedMs={stopwatch.ElapsedMilliseconds}");
        return snapshot;
    }

    private static async Task<List<ProfileMovieRow>> LoadIdentifiedMoviesAsync(
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
            .Select(x => new ProfileMovieRow
            {
                Id = x.Id,
                TmdbId = x.TmdbId!.Value,
                ImdbId = x.ImdbId ?? string.Empty,
                Title = x.Title,
                OriginalTitle = x.OriginalTitle ?? string.Empty,
                ReleaseYear = x.ReleaseYear,
                Country = x.Country ?? string.Empty,
                Language = x.Language ?? string.Empty,
                RuntimeMinutes = x.RuntimeMinutes,
                GenresText = x.GenresText ?? string.Empty,
                AiTagsText = x.AiTagsText ?? string.Empty,
                EmotionTagsText = x.EmotionTagsText ?? string.Empty,
                SceneTagsText = x.SceneTagsText ?? string.Empty,
                IsFavorite = x.IsFavorite,
                IsWatched = x.IsWatched,
                UserRating = x.UserRating,
                LastPlayedAt = x.LastPlayedAt,
                AutoWatchedBaselineAtUtc = x.AutoWatchedBaselineAtUtc,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<ProfileCollectionRow>> LoadIdentifiedCollectionItemsAsync(
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
                        && movie.TmdbId.HasValue
                        && movie.TmdbId.Value == x.TmdbId.Value
                        && !string.IsNullOrWhiteSpace(movie.Title)
                        && (movie.IdentificationStatus == IdentificationStatus.Matched
                            || movie.IdentificationStatus == IdentificationStatus.ManualConfirmed))))
            .Select(x => new ProfileCollectionRow
            {
                Id = x.Id,
                MovieId = x.MovieId,
                TmdbId = x.TmdbId!.Value,
                ImdbId = x.ImdbId,
                Title = x.Title,
                OriginalTitle = x.OriginalTitle,
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

    private static async Task<List<ProfileRatingRow>> LoadRatingSourcesAsync(
        AppDbContext dbContext,
        IReadOnlySet<int> movieIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.RatingSources
            .AsNoTracking()
            .Where(x => movieIds.Contains(x.MovieId))
            .Select(x => new ProfileRatingRow
            {
                Id = x.Id,
                MovieId = x.MovieId,
                SourceName = x.SourceName,
                ScoreValue = x.ScoreValue,
                ScoreScale = x.ScoreScale,
                VoteCount = x.VoteCount,
                LastUpdatedAt = x.LastUpdatedAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<ProfileHistoryRow>> LoadValidWatchHistoriesAsync(
        AppDbContext dbContext,
        IReadOnlySet<int> movieIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId.HasValue
                && movieIds.Contains(x.MovieId.Value)
                && x.DurationWatchedSeconds > ValidWatchSecondsThreshold)
            .Select(x => new ProfileHistoryRow
            {
                Id = x.Id,
                MovieId = x.MovieId!.Value,
                MediaFileId = x.MediaFileId,
                StartedAt = x.StartedAt,
                EndedAt = x.EndedAt,
                DurationWatchedSeconds = x.DurationWatchedSeconds,
                IsCompleted = x.IsCompleted,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static List<WatchProfileMovieSample> BuildSignalSamples(
        IReadOnlyCollection<ProfileMovieRow> movies,
        IReadOnlyCollection<ProfileCollectionRow> collectionItems,
        IReadOnlyCollection<ProfileRatingRow> ratingSources,
        IReadOnlyCollection<ProfileHistoryRow> histories)
    {
        var sampleByKey = new Dictionary<string, WatchProfileMovieSample>(StringComparer.Ordinal);
        var ratingsByMovieId = ratingSources
            .GroupBy(x => x.MovieId)
            .ToDictionary(x => x.Key, CalculateWeightedRating);
        var historiesByMovieId = histories
            .GroupBy(x => x.MovieId)
            .ToDictionary(x => x.Key, x => new
            {
                WatchSeconds = x.Sum(item => (long)item.DurationWatchedSeconds),
                WatchCount = x.Count(),
                CompletedCount = x.Count(item => item.IsCompleted),
                LastWatchedAtUtc = x.Max(item => EnsureUtc(item.EndedAt ?? item.StartedAt))
            });

        foreach (var movie in movies)
        {
            var sample = new WatchProfileMovieSample
            {
                MovieId = movie.Id,
                TmdbId = movie.TmdbId,
                ImdbId = movie.ImdbId,
                Title = movie.Title,
                OriginalTitle = movie.OriginalTitle,
                ReleaseYear = movie.ReleaseYear,
                Country = movie.Country,
                Language = movie.Language,
                RuntimeMinutes = movie.RuntimeMinutes,
                WeightedRating = ratingsByMovieId.TryGetValue(movie.Id, out var rating) ? rating : null,
                IsWatched = movie.IsWatched,
                IsFavorite = movie.IsFavorite,
                TypeTags = GetTypeTags(movie.AiTagsText, movie.GenresText),
                EmotionTags = SplitTags(movie.EmotionTagsText).ToList(),
                SceneTags = SplitTags(movie.SceneTagsText).ToList(),
                SortAtUtc = EnsureUtc(movie.LastPlayedAt ?? movie.CreatedAt)
            };

            if (historiesByMovieId.TryGetValue(movie.Id, out var history))
            {
                sample.WatchSeconds = history.WatchSeconds;
                sample.WatchCount = history.WatchCount;
                sample.CompletedCount = history.CompletedCount;
                sample.LastWatchedAtUtc = history.LastWatchedAtUtc;
                sample.SortAtUtc = history.LastWatchedAtUtc;
            }

            sampleByKey[BuildTmdbKey(movie.TmdbId)] = sample;
        }

        foreach (var item in collectionItems)
        {
            var key = BuildTmdbKey(item.TmdbId);
            if (!sampleByKey.TryGetValue(key, out var sample))
            {
                sample = new WatchProfileMovieSample
                {
                    MovieId = item.MovieId,
                    TmdbId = item.TmdbId,
                    ImdbId = item.ImdbId,
                    Title = item.Title,
                    OriginalTitle = item.OriginalTitle,
                    ReleaseYear = item.ReleaseYear,
                    Country = item.Country,
                    Language = item.Language,
                    RuntimeMinutes = item.RuntimeMinutes,
                    WeightedRating = CalculateWeightedRating(item),
                    TypeTags = SplitTags(item.GenresText).ToList(),
                    SortAtUtc = EnsureUtc(item.UpdatedAt)
                };
                sampleByKey[key] = sample;
            }

            var addsWatched = item.IsWatched && !sample.IsWatched;
            var addsFavorite = item.IsFavorite && !sample.IsFavorite;
            var addsWantToWatch = item.IsWantToWatch && !sample.IsWantToWatch;
            var addsNotInterested = item.IsNotInterested && !sample.IsNotInterested;

            sample.MovieId ??= item.MovieId;
            sample.IsWatched |= item.IsWatched;
            sample.IsFavorite |= item.IsFavorite;
            sample.IsWantToWatch |= item.IsWantToWatch;
            sample.IsNotInterested |= item.IsNotInterested;
            if (addsWatched || addsFavorite || addsWantToWatch || addsNotInterested)
            {
                sample.SortAtUtc = MaxUtc(sample.SortAtUtc, item.UpdatedAt);
            }
            if (sample.TypeTags.Count == 0)
            {
                sample.TypeTags = SplitTags(item.GenresText).ToList();
            }
        }

        return sampleByKey.Values.ToList();
    }

    private static List<WatchProfileMovieSample> SelectPromptSamples(
        IEnumerable<WatchProfileMovieSample> source,
        int maxCount)
    {
        var unique = source
            .GroupBy(x => x.TmdbId)
            .Select(x => x
                .OrderByDescending(item => item.SortAtUtc ?? DateTime.MinValue)
                .First())
            .ToList();
        if (unique.Count <= maxCount)
        {
            return unique
                .OrderByDescending(x => x.SortAtUtc ?? DateTime.MinValue)
                .ToList();
        }

        var selected = new List<WatchProfileMovieSample>();
        var selectedIds = new HashSet<int>();
        var recentCount = Math.Max(1, (int)Math.Round(maxCount * 0.6d));
        var oldestCount = Math.Max(1, (int)Math.Round(maxCount * 0.2d));
        var middleCount = Math.Max(0, maxCount - recentCount - oldestCount);

        AddSamples(unique.OrderByDescending(x => x.SortAtUtc ?? DateTime.MinValue), recentCount, selected, selectedIds);
        AddSamples(unique.OrderBy(x => x.SortAtUtc ?? DateTime.MinValue), oldestCount, selected, selectedIds);
        AddSamples(
            unique
                .Where(x => !selectedIds.Contains(x.TmdbId))
                .OrderBy(x => x.SortAtUtc ?? DateTime.MinValue)
                .Skip(Math.Max(0, (unique.Count - selected.Count - middleCount) / 2)),
            middleCount,
            selected,
            selectedIds);
        AddSamples(unique.OrderByDescending(x => x.SortAtUtc ?? DateTime.MinValue), maxCount - selected.Count, selected, selectedIds);
        return selected
            .OrderByDescending(x => x.SortAtUtc ?? DateTime.MinValue)
            .Take(maxCount)
            .ToList();
    }

    private static void AddSamples(
        IEnumerable<WatchProfileMovieSample> candidates,
        int count,
        ICollection<WatchProfileMovieSample> selected,
        ISet<int> selectedIds)
    {
        var added = 0;
        foreach (var candidate in candidates)
        {
            if (added >= count || count <= 0)
            {
                break;
            }

            if (selectedIds.Add(candidate.TmdbId))
            {
                selected.Add(candidate);
                added++;
            }
        }
    }

    private static WatchProfileStatisticsSummary BuildStatisticsSummary(
        WatchStatisticsSnapshot statistics,
        IReadOnlyCollection<WatchProfileMovieSample> signalSamples)
    {
        return new WatchProfileStatisticsSummary
        {
            TypeDistribution = BuildProfileTagDistribution(signalSamples.SelectMany(x => x.TypeTags), 10),
            EmotionDistribution = BuildProfileTagDistribution(signalSamples.SelectMany(x => x.EmotionTags), 10),
            SceneDistribution = BuildProfileTagDistribution(signalSamples.SelectMany(x => x.SceneTags), 10),
            OftenWatchedTypes = statistics.OftenWatchedTop3.ToList(),
            OftenLikedTypes = statistics.OftenLikedTop3.ToList(),
            OftenWantedTypes = statistics.OftenWantedTop3.ToList(),
            MonthlyFrequentTags = statistics.MonthlyFrequentTags.Take(10).ToList(),
            TasteCombinationTop10 = statistics.TasteCombinationTop10.ToList(),
            ViewingTimeDistribution = statistics.ViewingTimeDistribution.ToList(),
            WeekdayWeekendStats = statistics.WeekdayWeekendStats
        };
    }

    private static List<WatchStatisticsTagItem> BuildProfileTagDistribution(
        IEnumerable<string> labels,
        int take)
    {
        return labels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new WatchStatisticsTagItem
            {
                Label = x.First(),
                Count = x.Count(),
                Score = x.Count()
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static void ApplySufficiency(WatchProfileInputSnapshot snapshot)
    {
        if (snapshot.SignalMovieCount < 8)
        {
            snapshot.CanGenerateProfile = false;
            snapshot.InsufficientReason = snapshot.SignalMovieCount >= 5
                ? "有效影片样本偏少，先保留本地统计，不调用 AI。"
                : "有效影片样本少于 8 部，暂不调用 AI。";
            snapshot.WarningMessages.Add(snapshot.InsufficientReason);
            Log($"watch-profile-insufficient reason=signal-movies signalMovies={snapshot.SignalMovieCount}");
            return;
        }

        if (snapshot.BucketCount < 2)
        {
            snapshot.CanGenerateProfile = false;
            snapshot.InsufficientReason = "已看、喜爱、想看、不想看等有效信号类型少于 2 类，暂不调用 AI。";
            snapshot.WarningMessages.Add(snapshot.InsufficientReason);
            Log($"watch-profile-insufficient reason=buckets signalMovies={snapshot.SignalMovieCount}");
            return;
        }

        if (snapshot.TagCount == 0)
        {
            snapshot.CanGenerateProfile = false;
            snapshot.InsufficientReason = "有效影片缺少类型、情绪或场景标签，暂不调用 AI。";
            snapshot.WarningMessages.Add(snapshot.InsufficientReason);
            Log($"watch-profile-insufficient reason=tags signalMovies={snapshot.SignalMovieCount}");
            return;
        }

        snapshot.CanGenerateProfile = true;
    }

    private static int CountBuckets(IReadOnlyCollection<WatchProfileMovieSample> samples)
    {
        var count = 0;
        if (samples.Any(x => x.IsWatched || x.WatchCount > 0))
        {
            count++;
        }

        if (samples.Any(x => x.IsFavorite))
        {
            count++;
        }

        if (samples.Any(x => x.IsWantToWatch))
        {
            count++;
        }

        if (samples.Any(x => x.IsNotInterested))
        {
            count++;
        }

        return count;
    }

    private static int CalculateXAxisScore(IReadOnlyCollection<WatchProfileMovieSample> samples)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        var uniqueTypeCount = samples.SelectMany(x => x.TypeTags).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var countryCount = samples.SelectMany(x => SplitTags(x.Country)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var years = samples.Select(x => x.ReleaseYear).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var yearSpan = years.Count == 0 ? 0 : years.Max() - years.Min();
        var favoriteSamples = samples.Where(x => x.IsFavorite).ToList();
        var favoriteTopShare = CalculateTopTypeShare(favoriteSamples);

        var score = 0d;
        score += Math.Min(45d, uniqueTypeCount * 5d) - 15d;
        score += Math.Min(25d, countryCount * 5d) - 10d;
        score += Math.Min(25d, yearSpan / 2d) - 5d;
        score -= favoriteTopShare * 35d;
        return ClampInt(score, -100, 100);
    }

    private static int CalculateYAxisScore(IReadOnlyCollection<WatchProfileMovieSample> samples)
    {
        var tags = samples
            .SelectMany(x => x.TypeTags.Concat(x.EmotionTags).Concat(x.SceneTags))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (tags.Count == 0)
        {
            return 0;
        }

        var immersiveCount = tags.Count(tag => ContainsAny(tag, ImmersiveTags));
        var easyCount = tags.Count(tag => ContainsAny(tag, EasyTags));
        return ClampInt((immersiveCount - easyCount) / (double)tags.Count * 100d, -100, 100);
    }

    private static double CalculateTopTypeShare(IReadOnlyCollection<WatchProfileMovieSample> samples)
    {
        var typeTags = samples.SelectMany(x => x.TypeTags).ToList();
        if (typeTags.Count == 0)
        {
            return 0d;
        }

        return typeTags
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Max(x => x.Count()) / (double)typeTags.Count;
    }

    private static bool ContainsAny(string value, IEnumerable<string> needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildQuadrantName(int xAxisScore, int yAxisScore)
    {
        if (xAxisScore >= 0 && yAxisScore >= 0)
        {
            return "新鲜探索 × 情绪沉浸";
        }

        if (xAxisScore < 0 && yAxisScore >= 0)
        {
            return "熟悉安全 × 情绪沉浸";
        }

        return xAxisScore < 0 && yAxisScore < 0
            ? "熟悉安全 × 轻松消遣"
            : "新鲜探索 × 轻松消遣";
    }

    private static string BuildSourceFingerprint(
        IReadOnlyCollection<WatchProfileMovieSample> signalSamples,
        IReadOnlyCollection<ProfileHistoryRow> histories,
        WatchProfileStatisticsSummary statisticsSummary)
    {
        var builder = new StringBuilder();
        builder.Append("profile-input:v4-semantic-no-visibility-timestamps|");

        foreach (var sample in signalSamples.OrderBy(x => x.TmdbId))
        {
            builder
                .Append("signal:")
                .Append(sample.TmdbId).Append(':')
                .Append(NormalizeImdbId(sample.ImdbId)).Append(':')
                .Append(HashText(sample.Title)).Append(':')
                .Append(HashText(sample.OriginalTitle)).Append(':')
                .Append(sample.ReleaseYear?.ToString() ?? string.Empty).Append(':')
                .Append(HashText(sample.Country)).Append(':')
                .Append(HashText(sample.Language)).Append(':')
                .Append(sample.RuntimeMinutes?.ToString() ?? string.Empty).Append(':')
                .Append(FormatDouble(sample.WeightedRating)).Append(':')
                .Append(sample.IsWatched).Append(':')
                .Append(sample.IsFavorite).Append(':')
                .Append(sample.IsWantToWatch).Append(':')
                .Append(sample.IsNotInterested).Append(':')
                .Append(sample.WatchSeconds).Append(':')
                .Append(sample.WatchCount).Append(':')
                .Append(sample.CompletedCount).Append(':')
                .Append(FormatFingerprintDate(sample.LastWatchedAtUtc)).Append(':')
                .Append(BuildTagsHash(sample.TypeTags)).Append(':')
                .Append(BuildTagsHash(sample.EmotionTags)).Append(':')
                .Append(BuildTagsHash(sample.SceneTags))
                .Append('|');
        }

        foreach (var history in histories
                     .OrderBy(x => x.MovieId)
                     .ThenBy(x => x.StartedAt)
                     .ThenBy(x => x.DurationWatchedSeconds))
        {
            builder
                .Append("history:")
                .Append(history.MovieId).Append(':')
                .Append(history.DurationWatchedSeconds).Append(':')
                .Append(history.IsCompleted).Append(':')
                .Append(FormatFingerprintDate(history.StartedAt)).Append(':')
                .Append(FormatFingerprintDate(history.EndedAt))
                .Append('|');
        }

        AppendTagItems(builder, "type-distribution", statisticsSummary.TypeDistribution);
        AppendTagItems(builder, "emotion-distribution", statisticsSummary.EmotionDistribution);
        AppendTagItems(builder, "scene-distribution", statisticsSummary.SceneDistribution);
        AppendTagItems(builder, "often-watched", statisticsSummary.OftenWatchedTypes);
        AppendTagItems(builder, "often-liked", statisticsSummary.OftenLikedTypes);
        AppendTagItems(builder, "often-wanted", statisticsSummary.OftenWantedTypes);
        AppendTagItems(builder, "monthly-tags", statisticsSummary.MonthlyFrequentTags);
        AppendTasteCombinations(builder, statisticsSummary.TasteCombinationTop10);
        AppendViewingBuckets(builder, statisticsSummary.ViewingTimeDistribution);
        AppendWeekdayWeekend(builder, statisticsSummary.WeekdayWeekendStats);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void AppendTagItems(
        StringBuilder builder,
        string name,
        IEnumerable<WatchStatisticsTagItem> items)
    {
        builder.Append(name).Append(':');
        foreach (var item in items
                     .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Count)
                     .ThenBy(x => x.WatchSeconds))
        {
            builder
                .Append(HashText(item.Label)).Append(',')
                .Append(item.Count).Append(',')
                .Append(item.WatchSeconds).Append(',')
                .Append(FormatDouble(item.Score))
                .Append(';');
        }

        builder.Append('|');
    }

    private static void AppendTasteCombinations(
        StringBuilder builder,
        IEnumerable<TasteCombinationItem> items)
    {
        builder.Append("taste-combinations:");
        foreach (var item in items
                     .OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Emotion, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Scene, StringComparer.OrdinalIgnoreCase))
        {
            builder
                .Append(HashText(item.Type)).Append(',')
                .Append(HashText(item.Emotion)).Append(',')
                .Append(HashText(item.Scene)).Append(',')
                .Append(item.OccurrenceCount).Append(',')
                .Append(item.WatchSeconds).Append(',')
                .Append(FormatDouble(item.Score))
                .Append(';');
        }

        builder.Append('|');
    }

    private static void AppendViewingBuckets(
        StringBuilder builder,
        IEnumerable<ViewingTimeBucket> items)
    {
        builder.Append("viewing-time:");
        foreach (var item in items.OrderBy(x => x.StartHour).ThenBy(x => x.EndHour))
        {
            builder
                .Append(item.StartHour).Append('-')
                .Append(item.EndHour).Append(',')
                .Append(item.WatchSeconds).Append(',')
                .Append(item.WatchCount)
                .Append(';');
        }

        builder.Append('|');
    }

    private static void AppendWeekdayWeekend(
        StringBuilder builder,
        WeekdayWeekendWatchStats stats)
    {
        builder
            .Append("weekday-weekend:")
            .Append(stats.WeekdayWatchSeconds).Append(',')
            .Append(stats.WeekendWatchSeconds).Append(',')
            .Append(FormatDouble(stats.WeekdayAverageSeconds)).Append(',')
            .Append(FormatDouble(stats.WeekendAverageSeconds)).Append(',')
            .Append(FormatDouble(stats.WeekdayRatio)).Append(',')
            .Append(FormatDouble(stats.WeekendRatio))
            .Append('|');
    }

    private static string BuildTagsHash(IEnumerable<string> tags)
    {
        return HashText(string.Join(
            "|",
            tags
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
    }

    private static string NormalizeImdbId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string FormatDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string HashText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static List<string> GetTypeTags(string? aiTagsText, string? genresText)
    {
        var aiTags = SplitTags(aiTagsText).ToList();
        return aiTags.Count > 0 ? aiTags : SplitTags(genresText).ToList();
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

    private static double? CalculateWeightedRating(IEnumerable<ProfileRatingRow> ratingSources)
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

    private static double? CalculateWeightedRating(ProfileCollectionRow item)
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

    private static int ClampInt(double value, int min, int max)
    {
        return Math.Clamp((int)Math.Round(value), min, max);
    }

    private static DateTime? MaxUtc(DateTime? left, DateTime right)
    {
        var utcRight = EnsureUtc(right);
        return !left.HasValue || utcRight > left.Value ? utcRight : left;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static string FormatFingerprintDate(DateTime? value)
    {
        return value.HasValue ? EnsureUtc(value.Value).Ticks.ToString() : "0";
    }

    private static string BuildTmdbKey(int tmdbId)
    {
        return $"tmdb:{tmdbId}";
    }

    private static void Log(string message)
    {
        Debug.WriteLine("[WATCH-PROFILE] " + message);
        AiPerfDiagnostics.WriteEvent("event=" + message);
        WatchInsightsDiagnostics.Write("layer=profile-input " + message);
    }

    private sealed class ProfileMovieRow
    {
        public int Id { get; set; }

        public int TmdbId { get; set; }

        public string ImdbId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string OriginalTitle { get; set; } = string.Empty;

        public int? ReleaseYear { get; set; }

        public string Country { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public int? RuntimeMinutes { get; set; }

        public string GenresText { get; set; } = string.Empty;

        public string AiTagsText { get; set; } = string.Empty;

        public string EmotionTagsText { get; set; } = string.Empty;

        public string SceneTagsText { get; set; } = string.Empty;

        public bool IsFavorite { get; set; }

        public bool IsWatched { get; set; }

        public double? UserRating { get; set; }

        public DateTime? LastPlayedAt { get; set; }

        public DateTime? AutoWatchedBaselineAtUtc { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    private sealed class ProfileCollectionRow
    {
        public int Id { get; set; }

        public int? MovieId { get; set; }

        public int TmdbId { get; set; }

        public string ImdbId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string OriginalTitle { get; set; } = string.Empty;

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

    private sealed class ProfileRatingRow
    {
        public int Id { get; set; }

        public int MovieId { get; set; }

        public string SourceName { get; set; } = string.Empty;

        public double ScoreValue { get; set; }

        public double ScoreScale { get; set; }

        public int? VoteCount { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    private sealed class ProfileHistoryRow
    {
        public int Id { get; set; }

        public int MovieId { get; set; }

        public int MediaFileId { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public int DurationWatchedSeconds { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
