namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WatchProfileInputSnapshot
{
    public DateTime GeneratedAtUtc { get; set; }

    public string SourceFingerprint { get; set; } = string.Empty;

    public bool CanGenerateProfile { get; set; }

    public string InsufficientReason { get; set; } = string.Empty;

    public int SignalMovieCount { get; set; }

    public int BucketCount { get; set; }

    public int TagCount { get; set; }

    public int LocalXAxisScore { get; set; }

    public int LocalYAxisScore { get; set; }

    public string LocalQuadrantName { get; set; } = string.Empty;

    public List<string> WarningMessages { get; set; } = [];

    public List<WatchProfileMovieSample> WatchedSamples { get; set; } = [];

    public List<WatchProfileMovieSample> FavoriteSamples { get; set; } = [];

    public List<WatchProfileMovieSample> WantToWatchSamples { get; set; } = [];

    public List<WatchProfileMovieSample> NotInterestedSamples { get; set; } = [];

    public List<WatchProfileHistorySample> RecentHistorySamples { get; set; } = [];

    public WatchProfileStatisticsSummary StatisticsSummary { get; set; } = new();
}

public sealed class WatchProfileMovieSample
{
    public int? MovieId { get; set; }

    public int TmdbId { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public double? WeightedRating { get; set; }

    public bool IsWatched { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsWantToWatch { get; set; }

    public bool IsNotInterested { get; set; }

    public long WatchSeconds { get; set; }

    public int WatchCount { get; set; }

    public int CompletedCount { get; set; }

    public DateTime? LastWatchedAtUtc { get; set; }

    public DateTime? SortAtUtc { get; set; }

    public List<string> TypeTags { get; set; } = [];

    public List<string> EmotionTags { get; set; } = [];

    public List<string> SceneTags { get; set; } = [];
}

public sealed class WatchProfileHistorySample
{
    public int MovieId { get; set; }

    public int TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public int WatchSeconds { get; set; }

    public bool IsCompleted { get; set; }
}

public sealed class WatchProfileStatisticsSummary
{
    public List<WatchStatisticsTagItem> TypeDistribution { get; set; } = [];

    public List<WatchStatisticsTagItem> EmotionDistribution { get; set; } = [];

    public List<WatchStatisticsTagItem> SceneDistribution { get; set; } = [];

    public List<WatchStatisticsTagItem> OftenWatchedTypes { get; set; } = [];

    public List<WatchStatisticsTagItem> OftenLikedTypes { get; set; } = [];

    public List<WatchStatisticsTagItem> OftenWantedTypes { get; set; } = [];

    public List<WatchStatisticsTagItem> MonthlyFrequentTags { get; set; } = [];

    public List<TasteCombinationItem> TasteCombinationTop10 { get; set; } = [];

    public List<ViewingTimeBucket> ViewingTimeDistribution { get; set; } = [];

    public WeekdayWeekendWatchStats WeekdayWeekendStats { get; set; } = new();
}
