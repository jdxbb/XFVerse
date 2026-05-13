using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class HomeDashboardQueryService : IHomeDashboardQueryService
{
    private readonly IRecommendationService _recommendationService;
    private readonly IUserCollectionService _userCollectionService;

    public HomeDashboardQueryService(
        IRecommendationService recommendationService,
        IUserCollectionService userCollectionService)
    {
        _recommendationService = recommendationService;
        _userCollectionService = userCollectionService;
    }

    public async Task<HomeDashboardModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var overview = await GetLibraryOverviewAsync(cancellationToken);
        var collection = await GetCollectionPreviewAsync(cancellationToken);
        return new HomeDashboardModel
        {
            MovieCount = overview.MovieCount,
            SourceCount = overview.SourceCount,
            WatchedCount = overview.WatchedCount,
            FavoriteCount = overview.FavoriteCount,
            LastScanStatus = overview.LastScanStatus,
            RecentlyAdded = overview.RecentlyAdded,
            RecentlyPlayed = await GetRecentlyPlayedAsync(cancellationToken),
            Favorites = overview.Favorites,
            FavoriteCollectionItems = collection.FavoriteCollectionItems,
            WantToWatchItems = collection.WantToWatchItems,
            Recommendations = await GetRecommendationsPreviewAsync(cancellationToken),
            GenreDistribution = overview.GenreDistribution,
            YearDistribution = overview.YearDistribution,
            WatchedDistribution = overview.WatchedDistribution,
            RatingDistribution = overview.RatingDistribution
        };
    }

    public async Task<HomeDashboardModel> GetLibraryOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var moviesQuery = dbContext.Movies.AsNoTracking();
        var inLibraryMoviesQuery = moviesQuery
            .Where(x => x.MediaFiles.Any(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video));
        var movieCount = await moviesQuery.CountAsync(cancellationToken);
        var sourceCount = await dbContext.MediaFiles.AsNoTracking()
            .CountAsync(x => x.MediaType == MediaType.Video && !x.IsDeleted, cancellationToken);
        var watchedCount = await moviesQuery.CountAsync(x => x.IsWatched, cancellationToken);
        var favoriteCount = await moviesQuery.CountAsync(x => x.IsFavorite, cancellationToken);

        var recentlyAdded = await inLibraryMoviesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(
                x => new HomeMovieItem
                {
                    MovieId = x.Id,
                    Title = x.Title,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.GenresText ?? string.Empty,
                    Time = x.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var favorites = await moviesQuery
            .Where(x => x.IsFavorite)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(6)
            .Select(
                x => new HomeMovieItem
                {
                    MovieId = x.Id,
                    Title = x.Title,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.GenresText ?? string.Empty,
                    Time = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var latestLog = await dbContext.ScanTaskLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => $"{x.Status} | {x.ScannedCount} 个文件 | {x.CreatedAt:yyyy-MM-dd HH:mm}")
            .FirstOrDefaultAsync(cancellationToken);

        var genreRows = await moviesQuery
            .Select(x => x.GenresText)
            .Where(x => x != null && x != string.Empty)
            .ToListAsync(cancellationToken);

        var yearRows = await moviesQuery
            .Where(x => x.ReleaseYear.HasValue)
            .Select(x => x.ReleaseYear!.Value)
            .ToListAsync(cancellationToken);

        var ratingRows = await moviesQuery
            .Where(x => x.UserRating.HasValue)
            .Select(x => x.UserRating!.Value)
            .ToListAsync(cancellationToken);

        return new HomeDashboardModel
        {
            MovieCount = movieCount,
            SourceCount = sourceCount,
            WatchedCount = watchedCount,
            FavoriteCount = favoriteCount,
            LastScanStatus = latestLog ?? "暂无扫描记录",
            RecentlyAdded = recentlyAdded,
            Favorites = favorites,
            GenreDistribution = BuildGenreDistribution(genreRows),
            YearDistribution = BuildYearDistribution(yearRows),
            WatchedDistribution = BuildWatchedDistribution(watchedCount, movieCount - watchedCount),
            RatingDistribution = BuildRatingDistribution(ratingRows)
        };
    }

    public async Task<HomeDashboardModel> GetScanOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var moviesQuery = dbContext.Movies.AsNoTracking();
        var inLibraryMoviesQuery = moviesQuery
            .Where(x => x.MediaFiles.Any(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video));

        var recentlyAdded = await inLibraryMoviesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(
                x => new HomeMovieItem
                {
                    MovieId = x.Id,
                    Title = x.Title,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.GenresText ?? string.Empty,
                    Time = x.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var latestLog = await dbContext.ScanTaskLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => $"{x.Status} | {x.ScannedCount} 个文件 | {x.CreatedAt:yyyy-MM-dd HH:mm}")
            .FirstOrDefaultAsync(cancellationToken);

        return new HomeDashboardModel
        {
            MovieCount = await moviesQuery.CountAsync(cancellationToken),
            SourceCount = await dbContext.MediaFiles.AsNoTracking()
                .CountAsync(x => x.MediaType == MediaType.Video && !x.IsDeleted, cancellationToken),
            WatchedCount = await moviesQuery.CountAsync(x => x.IsWatched, cancellationToken),
            FavoriteCount = await moviesQuery.CountAsync(x => x.IsFavorite, cancellationToken),
            LastScanStatus = latestLog ?? "暂无扫描记录",
            RecentlyAdded = recentlyAdded
        };
    }

    public async Task<IReadOnlyList<HomeMovieItem>> GetRecentlyPlayedAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var recentHistoryRows = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.Movie != null && x.MediaFile != null)
            .OrderByDescending(x => x.EndedAt ?? x.StartedAt)
            .Take(30)
            .Select(
                x => new RecentHistoryRow
                {
                    MovieId = x.MovieId!.Value,
                    MediaFileId = x.MediaFileId,
                    Title = x.Movie!.Title,
                    ReleaseYear = x.Movie.ReleaseYear,
                    PosterRemoteUrl = x.Movie.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.Movie.GenresText ?? string.Empty,
                    Time = x.EndedAt ?? x.StartedAt,
                    LastPlayPositionSeconds = x.LastPlayPositionSeconds,
                    DurationSeconds = x.MediaFile!.DurationSeconds,
                    RuntimeMinutes = x.Movie.RuntimeMinutes,
                    IsCompleted = x.IsCompleted,
                    IsDeleted = x.MediaFile.IsDeleted
                })
            .ToListAsync(cancellationToken);

        return recentHistoryRows
            .GroupBy(x => x.MovieId)
            .Take(6)
            .Select(BuildRecentlyPlayedItem)
            .ToList();
    }

    public async Task<HomeDashboardModel> GetCollectionPreviewAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var favoriteItems = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.IsFavorite)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(6)
            .Select(
                x => new CollectionMovieItem
                {
                    MovieId = x.Id,
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle ?? string.Empty,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    Overview = x.Overview ?? string.Empty,
                    GenresText = x.GenresText ?? string.Empty,
                    AiTagsText = x.AiTagsText ?? string.Empty,
                    EmotionTagsText = x.EmotionTagsText ?? string.Empty,
                    SceneTagsText = x.SceneTagsText ?? string.Empty,
                    Country = x.Country ?? string.Empty,
                    Language = x.Language ?? string.Empty,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId ?? string.Empty,
                    IsLiked = true,
                    IsWantToWatch = false,
                    IsWatched = x.IsWatched,
                    IsInLibrary = x.MediaFiles.Any(media => !media.IsDeleted && media.MediaType == MediaType.Video),
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var wantItems = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsWantToWatch && !x.IsWatched)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(6)
            .Select(
                x => new CollectionMovieItem
                {
                    MovieId = x.MovieId,
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl,
                    Overview = x.Overview,
                    GenresText = x.GenresText,
                    AiTagsText = x.GenresText,
                    Country = x.Country,
                    Language = x.Language,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId,
                    TmdbRating = x.TmdbRating,
                    TmdbVoteCount = x.TmdbVoteCount,
                    OmdbScoreValue = x.OmdbScoreValue,
                    OmdbScoreScale = x.OmdbScoreScale,
                    OmdbVoteCount = x.OmdbVoteCount,
                    OmdbSourceUrl = x.OmdbSourceUrl,
                    OmdbLastUpdatedAt = x.OmdbLastUpdatedAt,
                    IsLiked = false,
                    IsWantToWatch = x.IsWantToWatch,
                    IsWatched = x.IsWatched,
                    IsInLibrary = x.IsInLibrary,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        return new HomeDashboardModel
        {
            FavoriteCollectionItems = favoriteItems,
            WantToWatchItems = wantItems
        };
    }

    public async Task<IReadOnlyList<AiRecommendationItem>> GetRecommendationsPreviewAsync(CancellationToken cancellationToken = default)
    {
        return (await GetRecommendationsPreviewStateAsync(cancellationToken)).Items;
    }

    public async Task<AiRecommendationPreviewState> GetRecommendationsPreviewStateAsync(CancellationToken cancellationToken = default)
    {
        return await _recommendationService.GetRecommendationPreviewStateAsync(
            new RecommendationQueryOptions
            {
                LibraryScope = RecommendationLibraryScope.OutsideLibraryOnly,
                WatchFilter = RecommendationWatchFilter.UnwatchedOnly,
                Take = 3
            },
            cancellationToken);
    }

    private static HomeMovieItem BuildRecentlyPlayedItem(
        IGrouping<int, RecentHistoryRow> group)
    {
        var latest = group.First();
        int? runtimeDurationSeconds = latest.RuntimeMinutes.HasValue
            ? latest.RuntimeMinutes.Value * 60
            : null;
        var groupRuntimeMinutes = group.Select(x => x.RuntimeMinutes)
            .FirstOrDefault(x => x.HasValue && x.Value > 0);
        int? groupRuntimeDurationSeconds = groupRuntimeMinutes.HasValue
            ? groupRuntimeMinutes.Value * 60
            : null;
        var durationSeconds = latest.DurationSeconds
                              ?? runtimeDurationSeconds
                              ?? groupRuntimeDurationSeconds;
        var progressPercent = ResolveProgressPercent(latest.LastPlayPositionSeconds, durationSeconds);
        var hasProgressPosition = latest.LastPlayPositionSeconds > 0;
        var hasProgressPercent = hasProgressPosition && durationSeconds.HasValue && durationSeconds.Value > 0;
        var lastPlayedText = FormatLastPlayedText(latest.Time);
        var resumePositionText = latest.LastPlayPositionSeconds > 0
            ? $"看到 {FormatDuration(latest.LastPlayPositionSeconds)}"
            : "暂无进度";

        return ApplyProgressText(
            new HomeMovieItem
            {
                MovieId = latest.MovieId,
                MediaFileId = latest.MediaFileId,
                Title = latest.Title,
                ReleaseYear = latest.ReleaseYear,
                PosterRemoteUrl = latest.PosterRemoteUrl,
                GenresText = latest.GenresText,
                Time = latest.Time,
                LastPlayedText = lastPlayedText,
                LastPlayedAtText = lastPlayedText,
                WatchPositionText = resumePositionText,
                ResumePositionText = resumePositionText,
                ProgressPercent = progressPercent,
                ProgressValue = progressPercent,
                ProgressPercentText = hasProgressPercent ? $"{progressPercent:0.#}%" : string.Empty,
                HasProgress = hasProgressPosition,
                HasProgressPosition = hasProgressPosition,
                HasProgressPercent = hasProgressPercent,
                CanContinuePlayback = !latest.IsDeleted
            });
    }

    private static HomeMovieItem ApplyProgressText(HomeMovieItem item)
    {
        if (!item.HasProgress)
        {
            item.ProgressText = "暂无进度";
            return item;
        }

        item.ProgressText = item.ProgressPercent >= 95
            ? "已看完"
            : item.ProgressPercent >= 90
                ? "接近看完"
                : item.HasProgressPercent
                    ? $"{item.WatchPositionText} · {item.ProgressPercent:0}%"
                    : item.WatchPositionText;
        return item;
    }

    private static string FormatLastPlayedText(DateTime time)
    {
        return $"上次播放 {time.ToLocalTime():MM-dd HH:mm}";
    }

    private static string FormatDuration(int seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return value.Hours > 0
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private static double ResolveProgressPercent(int positionSeconds, int? durationSeconds)
    {
        if (!durationSeconds.HasValue || durationSeconds.Value <= 0 || positionSeconds <= 0)
        {
            return 0d;
        }

        return Math.Clamp(Math.Round(positionSeconds * 100d / durationSeconds.Value, 1), 0d, 100d);
    }

    private static IReadOnlyList<ChartSliceItem> BuildGenreDistribution(IEnumerable<string?> genres)
    {
        var groups = genres
            .SelectMany(x => (x ?? string.Empty).Split(['、', ',', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .Take(6)
            .Select(x => new ChartSliceItem { Label = x.Key, Count = x.Count() })
            .ToList();

        ApplyPercent(groups);
        return groups;
    }

    private static IReadOnlyList<ChartSliceItem> BuildYearDistribution(IEnumerable<int> years)
    {
        var groups = years
            .GroupBy(year => $"{year / 10 * 10}s")
            .OrderBy(x => x.Key)
            .Select(x => new ChartSliceItem { Label = x.Key, Count = x.Count() })
            .ToList();

        ApplyPercent(groups);
        return groups;
    }

    private static IReadOnlyList<ChartSliceItem> BuildWatchedDistribution(int watched, int unwatched)
    {
        var groups = new List<ChartSliceItem>
        {
            new() { Label = "已看", Count = watched },
            new() { Label = "未看", Count = Math.Max(0, unwatched) }
        };
        ApplyPercent(groups);
        return groups;
    }

    private static IReadOnlyList<ChartSliceItem> BuildRatingDistribution(IEnumerable<double> ratings)
    {
        var groups = ratings
            .GroupBy(rating => rating switch
            {
                >= 9 => "9-10",
                >= 7 => "7-8.9",
                >= 5 => "5-6.9",
                _ => "5 以下"
            })
            .Select(x => new ChartSliceItem { Label = x.Key, Count = x.Count() })
            .ToList();

        ApplyPercent(groups);
        return groups;
    }

    private static void ApplyPercent(IReadOnlyCollection<ChartSliceItem> groups)
    {
        var total = groups.Sum(x => x.Count);
        foreach (var item in groups)
        {
            item.Percent = total == 0 ? 0 : Math.Round(item.Count * 100d / total, 1);
        }
    }

    private sealed class RecentHistoryRow
    {
        public int MovieId { get; set; }

        public int MediaFileId { get; set; }

        public string Title { get; set; } = string.Empty;

        public int? ReleaseYear { get; set; }

        public string PosterRemoteUrl { get; set; } = string.Empty;

        public string GenresText { get; set; } = string.Empty;

        public DateTime Time { get; set; }

        public int LastPlayPositionSeconds { get; set; }

        public int? DurationSeconds { get; set; }

        public int? RuntimeMinutes { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsDeleted { get; set; }
    }
}
