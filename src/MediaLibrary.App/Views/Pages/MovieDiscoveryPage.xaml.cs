using System.Windows.Controls;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class MovieDiscoveryPage : UserControl
{
    public MovieDiscoveryPage()
    {
        InitializeComponent();
    }

    private void RankingScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer
            || DataContext is not MovieDiscoveryViewModel viewModel
            || scrollViewer.ScrollableHeight <= 0
            || scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 360)
        {
            return;
        }

        viewModel.RequestLoadMoreRankingsFromScroll();
    }
}
