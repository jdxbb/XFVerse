using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MediaLibrary.App.Views.Pages;

public partial class ScanTasksPage : UserControl
{
    public ScanTasksPage()
    {
        InitializeComponent();
    }

    private void RootScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer rootScrollViewer
            || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var nestedScrollViewer = FindAncestor<ScrollViewer>(source);
        if (nestedScrollViewer is not null
            && !ReferenceEquals(nestedScrollViewer, rootScrollViewer)
            && CanScrollVertically(nestedScrollViewer, e.Delta))
        {
            return;
        }

        ScrollByWheel(rootScrollViewer, e.Delta);
        e.Handled = true;
    }

    private void NestedScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || !CanScrollVertically(scrollViewer, e.Delta))
        {
            return;
        }

        ScrollByWheel(scrollViewer, e.Delta);
        e.Handled = true;
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int wheelDelta)
    {
        return wheelDelta < 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
            : scrollViewer.VerticalOffset > 0;
    }

    private static void ScrollByWheel(ScrollViewer scrollViewer, int wheelDelta)
    {
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - wheelDelta);
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject source)
    {
        return source is Visual or Visual3D
            ? VisualTreeHelper.GetParent(source)
            : LogicalTreeHelper.GetParent(source);
    }
}
