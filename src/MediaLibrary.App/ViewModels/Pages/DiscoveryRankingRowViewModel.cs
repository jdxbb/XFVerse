namespace MediaLibrary.App.ViewModels.Pages;

public sealed class DiscoveryRankingRowViewModel
{
    public DiscoveryRankingRowViewModel(
        DiscoveryMovieCardViewModel leftItem,
        DiscoveryMovieCardViewModel? rightItem)
    {
        LeftItem = leftItem;
        RightItem = rightItem;
    }

    public DiscoveryMovieCardViewModel LeftItem { get; }

    public DiscoveryMovieCardViewModel? RightItem { get; }

    public bool HasRightItem => RightItem is not null;
}
