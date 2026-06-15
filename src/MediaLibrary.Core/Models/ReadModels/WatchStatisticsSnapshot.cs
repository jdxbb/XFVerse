namespace MediaLibrary.Core.Models.ReadModels;

public enum WatchStatisticsTimeRange
{
    Month,
    All
}

public sealed class WatchStatisticsSnapshot
{
    public DateTime GeneratedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public string SourceFingerprint { get; set; } = string.Empty;

    public bool LoadedFromCache { get; set; }

    public bool HasAnyData { get; set; }

    public bool HasWatchHistoryData { get; set; }

    public bool HasTagData { get; set; }

    public string EmptyReason { get; set; } = string.Empty;

    public List<string> WarningMessages { get; set; } = [];

    public WatchStatisticsTimeRange TimeRange { get; set; } = WatchStatisticsTimeRange.Month;

    public DateTime? RangeStartLocal { get; set; }

    public DateTime? RangeEndLocal { get; set; }

    public int WatchedCount { get; set; }

    public int UnwatchedCount { get; set; }

    public int FavoriteCount { get; set; }

    public int WantToWatchCount { get; set; }

    public int NotInterestedCount { get; set; }

    public int? WatchedDeltaFromLastWeek { get; set; }

    public int? UnwatchedDeltaFromLastWeek { get; set; }

    public int? FavoriteDeltaFromLastWeek { get; set; }

    public int? WantToWatchDeltaFromLastWeek { get; set; }

    public int? NotInterestedDeltaFromLastWeek { get; set; }

    public long TotalWatchSeconds { get; set; }

    public long? TotalWatchSecondsDeltaFromLastMonth { get; set; }

    public int WatchDays { get; set; }

    public int? WatchDaysDeltaFromLastMonth { get; set; }

    public List<WatchStatisticsTagItem> MonthlyFrequentTags { get; set; } = [];

    public DateTime CalendarMonth { get; set; }

    public DateTime EarliestCalendarMonth { get; set; }

    public DateTime LatestCalendarMonth { get; set; }

    public List<WatchCalendarDay> CalendarDays { get; set; } = [];

    public int MonthlyWatchDays { get; set; }

    public int ContinuousWatchDays { get; set; }

    public DateTime? ContinuousWatchStartDate { get; set; }

    public DateTime? ContinuousWatchEndDate { get; set; }

    public DateTime? MostActiveDate { get; set; }

    public long MostActiveDateWatchSeconds { get; set; }

    public int MostActiveDateWatchCount { get; set; }

    public List<WatchDistributionItem> TypeDistribution { get; set; } = [];

    public List<WatchDistributionItem> EmotionDistribution { get; set; } = [];

    public List<WatchDistributionItem> SceneDistribution { get; set; } = [];

    public List<WatchDistributionItem> YearDistribution { get; set; } = [];

    public List<WatchDistributionItem> CountryDistribution { get; set; } = [];

    public List<WatchDistributionItem> LanguageDistribution { get; set; } = [];

    public List<WatchDistributionItem> RatingDistribution { get; set; } = [];

    public List<WatchStatisticsTagItem> MonthlyTypeTagTop3 { get; set; } = [];

    public List<WatchStatisticsTagItem> MonthlyEmotionTagTop3 { get; set; } = [];

    public List<WatchStatisticsTagItem> MonthlySceneTagTop3 { get; set; } = [];

    public List<ViewingTimeBucket> ViewingTimeDistribution { get; set; } = [];

    public WeekdayWeekendWatchStats WeekdayWeekendStats { get; set; } = new();

    public List<DurationDistributionItem> DurationDistribution { get; set; } = [];

    public List<TasteCombinationNode> TasteCombinationNodes { get; set; } = [];

    public List<TasteCombinationEdge> TasteCombinationEdges { get; set; } = [];

    public List<TasteCombinationItem> TasteCombinationTop10 { get; set; } = [];

    public List<WatchStatisticsTagItem> OftenWatchedTop3 { get; set; } = [];

    public List<WatchStatisticsTagItem> OftenLikedTop3 { get; set; } = [];

    public List<WatchStatisticsTagItem> OftenWantedTop3 { get; set; } = [];

    public string InsightConclusion { get; set; } = string.Empty;
}

public sealed class WatchStatisticsTagItem
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }

    public long WatchSeconds { get; set; }

    public double Score { get; set; }
}

public sealed class WatchCalendarDay
{
    public DateTime Date { get; set; }

    public long WatchSeconds { get; set; }

    public int WatchCount { get; set; }

    public int HeatLevel { get; set; }

    public bool HasValidWatch { get; set; }

    public bool IsCurrentMonth { get; set; }
}

public sealed class WatchDistributionItem
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }

    public double Percent { get; set; }

    public long WatchSeconds { get; set; }

    public double Score { get; set; }
}

public sealed class ViewingTimeBucket
{
    public string Label { get; set; } = string.Empty;

    public int StartHour { get; set; }

    public int EndHour { get; set; }

    public long WatchSeconds { get; set; }

    public int WatchCount { get; set; }
}

public sealed class WeekdayWeekendWatchStats
{
    public long WeekdayWatchSeconds { get; set; }

    public long WeekendWatchSeconds { get; set; }

    public double WeekdayAverageSeconds { get; set; }

    public double WeekendAverageSeconds { get; set; }

    public double WeekdayRatio { get; set; }

    public double WeekendRatio { get; set; }
}

public sealed class DurationDistributionItem
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }

    public long WatchSeconds { get; set; }

    public double Percent { get; set; }

    public int MinMinutes { get; set; }

    public int? MaxMinutes { get; set; }
}

public sealed class TasteCombinationNode
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public double Weight { get; set; }

    public long WatchSeconds { get; set; }

    public int Count { get; set; }
}

public sealed class TasteCombinationEdge
{
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public double Weight { get; set; }

    public long WatchSeconds { get; set; }

    public int Count { get; set; }
}

public sealed class TasteCombinationItem
{
    public string Type { get; set; } = string.Empty;

    public string Emotion { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public int OccurrenceCount { get; set; }

    public long WatchSeconds { get; set; }

    public double Score { get; set; }
}
