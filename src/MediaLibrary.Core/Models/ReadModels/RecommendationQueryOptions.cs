namespace MediaLibrary.Core.Models.ReadModels;

public sealed class RecommendationQueryOptions
{
    public RecommendationLibraryScope LibraryScope { get; set; } = RecommendationLibraryScope.OutsideLibraryOnly;

    public RecommendationWatchFilter WatchFilter { get; set; } = RecommendationWatchFilter.UnwatchedOnly;

    public int BatchSeed { get; set; }

    public int Take { get; set; } = 3;

    public bool ForceRefresh { get; set; }
}
