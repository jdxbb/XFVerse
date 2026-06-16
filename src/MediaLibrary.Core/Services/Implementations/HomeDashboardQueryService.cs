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
        var movieCount = await moviesQuery.CountAsync(cancellationToken);
        var sourceCount = await dbContext.MediaFiles.AsNoTracking()
            .CountAsync(x => x.MediaType == MediaType.Video && !x.IsDeleted, cancellationToken);
        var watchedCount = await moviesQuery.CountAsync(x => x.IsWatched, cancellationToken);
        var favoriteCount = await moviesQuery.CountAsync(x => x.IsFavorite, cancellationToken);

        var recentlyAdded = await GetRecentlyAddedAsync(dbContext, cancellationToken);

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

        var recentlyAdded = await GetRecentlyAddedAsync(dbContext, cancellationToken);

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
        var movieHistoryRows = await dbContext.WatchHistories
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
        var episodeHistoryRows = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.Episode != null && x.MediaFile != null)
            .OrderByDescending(x => x.EndedAt ?? x.StartedAt)
            .Take(30)
            .Select(
                x => new RecentHistoryRow
                {
                    MovieId = 0,
                    EpisodeId = x.EpisodeId,
                    TvSeasonId = x.Episode!.TvSeasonId,
                    TvSeriesId = x.Episode.Season!.TvSeriesId,
                    SeasonNumber = x.Episode.Season.SeasonNumber,
                    EpisodeNumber = x.Episode.EpisodeNumber,
                    MediaFileId = x.MediaFileId,
                    Title = x.Episode.Season.Series!.Name + " S" + x.Episode.Season.SeasonNumber.ToString("D2")
                            + "E" + x.Episode.EpisodeNumber.ToString("D2")
                            + (string.IsNullOrWhiteSpace(x.Episode.Title) ? string.Empty : " " + x.Episode.Title),
                    ReleaseYear = x.Episode.AirDate.HasValue
                        ? x.Episode.AirDate.Value.Year
                        : x.Episode.Season.AirDate.HasValue
                            ? x.Episode.Season.AirDate.Value.Year
                            : x.Episode.Season.Series.FirstAirYear,
                    PosterRemoteUrl = x.Episode.Season.PosterRemoteUrl ?? x.Episode.Season.Series.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.Episode.Season.Series.GenresText ?? string.Empty,
                    Time = x.EndedAt ?? x.StartedAt,
                    LastPlayPositionSeconds = x.LastPlayPositionSeconds,
                    DurationSeconds = x.MediaFile!.DurationSeconds,
                    RuntimeMinutes = x.Episode.RuntimeMinutes,
                    IsCompleted = x.IsCompleted,
                    IsDeleted = x.MediaFile.IsDeleted
                })
            .ToListAsync(cancellationToken);

        return movieHistoryRows
            .Concat(episodeHistoryRows)
            .OrderByDescending(x => x.Time)
            .GroupBy(x => x.EpisodeId.HasValue ? $"episode:{x.EpisodeId.Value}" : $"movie:{x.MovieId}", StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(BuildRecentlyPlayedItem)
            .ToList();
    }

    private static async Task<IReadOnlyList<HomeMovieItem>> GetRecentlyAddedAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var movieRows = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.MediaFiles.Any(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video))
            .Select(
                x => new RecentlyAddedRow
                {
                    MovieId = x.Id,
                    Title = x.Title,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.GenresText ?? string.Empty,
                    Time = x.MediaFiles
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .Select(mediaFile => (DateTime?)mediaFile.CreatedAt)
                        .Max() ?? x.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var seasonRows = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(
                x => x.Episodes.Any(
                    episode => episode.MediaFiles.Any(
                        mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)))
            .Select(
                x => new RecentlyAddedRow
                {
                    MovieId = 0,
                    TvSeasonId = x.Id,
                    TvSeriesId = x.TvSeriesId,
                    SeasonNumber = x.SeasonNumber,
                    Title = x.Series!.Name,
                    SeasonTitle = x.Name,
                    ReleaseYear = x.AirDate.HasValue ? x.AirDate.Value.Year : x.Series.FirstAirYear,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? x.Series.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.Series.GenresText ?? string.Empty,
                    Time = x.Episodes
                        .SelectMany(episode => episode.MediaFiles)
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .Select(mediaFile => (DateTime?)mediaFile.CreatedAt)
                        .Max() ?? x.CreatedAt
                })
            .ToListAsync(cancellationToken);

        return movieRows
            .Concat(seasonRows)
            .OrderByDescending(x => x.Time)
            .ThenBy(x => x.Title)
            .Take(8)
            .Select(
                x => new HomeMovieItem
                {
                    MovieId = x.MovieId,
                    TvSeasonId = x.TvSeasonId,
                    TvSeriesId = x.TvSeriesId,
                    SeasonNumber = x.SeasonNumber,
                    Title = x.TvSeasonId.HasValue
                        ? BuildSeasonTitle(x.Title, x.SeasonTitle, x.SeasonNumber)
                        : x.Title,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl,
                    GenresText = x.GenresText,
                    Time = x.Time
                })
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
                    ReleaseDate = x.ReleaseDate,
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

        var externalFavoriteItems = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsFavorite && !x.IsNotInterested)
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
                    ReleaseDate = x.ReleaseDate,
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
                    IsLiked = true,
                    IsWantToWatch = x.IsWantToWatch,
                    IsWatched = x.IsWatched,
                    IsInLibrary = x.IsInLibrary,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var wantItems = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsWantToWatch && !x.IsFavorite && !x.IsWatched)
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
                    ReleaseDate = x.ReleaseDate,
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
            FavoriteCollectionItems = favoriteItems
                .Concat(externalFavoriteItems)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(6)
                .ToList(),
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
        IGrouping<string, RecentHistoryRow> group)
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
                EpisodeId = latest.EpisodeId,
                TvSeasonId = latest.TvSeasonId,
                TvSeriesId = latest.TvSeriesId,
                SeasonNumber = latest.SeasonNumber,
                EpisodeNumber = latest.EpisodeNumber,
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
                ProgressPercentText = hasProgressPercent ? FormatProgressPercent(progressPercent) : string.Empty,
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
                    ? $"{item.WatchPositionText} · {FormatProgressPercent(item.ProgressPercent)}"
                    : item.WatchPositionText;
        return item;
    }

    private static string FormatProgressPercent(double value)
    {
        return value == 0d ? "0%" : $"{value:0.0}%";
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

    private static string BuildSeasonTitle(string seriesTitle, string seasonTitle, int seasonNumber)
    {
        var normalizedSeries = string.IsNullOrWhiteSpace(seriesTitle) ? "未命名电视剧" : seriesTitle.Trim();
        var normalizedSeason = string.IsNullOrWhiteSpace(seasonTitle) ? $"S{seasonNumber:D2}" : seasonTitle.Trim();
        return normalizedSeason.Contains(normalizedSeries, StringComparison.OrdinalIgnoreCase)
            ? normalizedSeason
            : $"{normalizedSeries} {normalizedSeason}";
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

    private sealed class RecentlyAddedRow
    {
        public int MovieId { get; set; }

        public int? TvSeasonId { get; set; }

        public int? TvSeriesId { get; set; }

        public int SeasonNumber { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SeasonTitle { get; set; } = string.Empty;

        public int? ReleaseYear { get; set; }

        public string PosterRemoteUrl { get; set; } = string.Empty;

        public string GenresText { get; set; } = string.Empty;

        public DateTime Time { get; set; }
    }

    private sealed class RecentHistoryRow
    {
        public int MovieId { get; set; }

        public int? EpisodeId { get; set; }

        public int? TvSeasonId { get; set; }

        public int? TvSeriesId { get; set; }

        public int SeasonNumber { get; set; }

        public int EpisodeNumber { get; set; }

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
