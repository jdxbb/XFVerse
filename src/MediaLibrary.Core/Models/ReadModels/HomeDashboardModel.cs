namespace MediaLibrary.Core.Models.ReadModels;

public sealed class HomeDashboardModel
{
    public int MovieCount { get; set; }

    public int SourceCount { get; set; }

    public int WatchedCount { get; set; }

    public int FavoriteCount { get; set; }

    public string LastScanStatus { get; set; } = string.Empty;

    public IReadOnlyList<HomeMovieItem> RecentlyAdded { get; set; } = [];

    public IReadOnlyList<HomeMovieItem> RecentlyPlayed { get; set; } = [];

    public IReadOnlyList<HomeMovieItem> Favorites { get; set; } = [];

    public IReadOnlyList<CollectionMovieItem> FavoriteCollectionItems { get; set; } = [];

    public IReadOnlyList<CollectionMovieItem> WantToWatchItems { get; set; } = [];

    public IReadOnlyList<AiRecommendationItem> Recommendations { get; set; } = [];

    public IReadOnlyList<ChartSliceItem> GenreDistribution { get; set; } = [];

    public IReadOnlyList<ChartSliceItem> YearDistribution { get; set; } = [];

    public IReadOnlyList<ChartSliceItem> WatchedDistribution { get; set; } = [];

    public IReadOnlyList<ChartSliceItem> RatingDistribution { get; set; } = [];
}
