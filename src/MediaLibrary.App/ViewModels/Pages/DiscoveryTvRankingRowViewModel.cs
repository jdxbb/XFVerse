namespace MediaLibrary.App.ViewModels.Pages;

public sealed class DiscoveryTvRankingRowViewModel
{
    public DiscoveryTvRankingRowViewModel(
        DiscoveryTvSeriesCardViewModel leftItem,
        DiscoveryTvSeriesCardViewModel? rightItem)
    {
        LeftItem = leftItem;
        RightItem = rightItem;
    }

    public DiscoveryTvSeriesCardViewModel LeftItem { get; }

    public DiscoveryTvSeriesCardViewModel? RightItem { get; }

    public bool HasRightItem => RightItem is not null;
}
