using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MediaLibrary.App.Views.Pages;

public partial class WatchInsightsPage : UserControl
{
    public WatchInsightsPage()
    {
        InitializeComponent();
    }

    private void ProfileSummaryScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (!NeedsInternalScroll(scrollViewer))
        {
            ForwardMouseWheelToParent(scrollViewer, e);
            return;
        }

        if (CanScrollVertically(scrollViewer, e.Delta))
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        }

        e.Handled = true;
    }

    private static bool NeedsInternalScroll(ScrollViewer scrollViewer)
    {
        scrollViewer.UpdateLayout();
        return scrollViewer.IsVisible
               && scrollViewer.ActualHeight > 0d
               && scrollViewer.ViewportHeight > 0d
               && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible
               && scrollViewer.ExtentHeight > scrollViewer.ViewportHeight + 1d
               && scrollViewer.ScrollableHeight > 1d;
    }

    private static void ForwardMouseWheelToParent(ScrollViewer scrollViewer, MouseWheelEventArgs e)
    {
        var parent = FindVisualParent<UIElement>(scrollViewer);
        if (parent is null)
        {
            return;
        }

        e.Handled = true;
        var forwardedArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = scrollViewer
        };
        parent.RaiseEvent(forwardedArgs);
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int wheelDelta)
    {
        return wheelDelta < 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
            : scrollViewer.VerticalOffset > 0;
    }

    private static T? FindVisualParent<T>(DependencyObject element)
        where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T typedParent)
            {
                return typedParent;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
