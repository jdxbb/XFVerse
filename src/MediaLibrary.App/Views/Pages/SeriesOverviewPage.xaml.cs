using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class SeriesOverviewPage : UserControl
{
    private bool _isRestoringScrollOffset;

    public SeriesOverviewPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        SeasonListBox.Loaded += OnSeasonListBoxLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        QueueRestoreSeasonListScrollOffset();
    }

    private void OnSeasonListBoxLoaded(object sender, RoutedEventArgs e)
    {
        QueueRestoreSeasonListScrollOffset();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            QueueRestoreSeasonListScrollOffset();
        }
        else
        {
            StoreCurrentSeasonListScrollOffset();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentSeasonListScrollOffset();
    }

    private void OnSeasonListScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isRestoringScrollOffset || DataContext is not SeriesOverviewViewModel viewModel)
        {
            return;
        }

        viewModel.SeasonListScrollOffset = e.VerticalOffset;
    }

    private void StoreCurrentSeasonListScrollOffset()
    {
        if (_isRestoringScrollOffset || DataContext is not SeriesOverviewViewModel viewModel)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(SeasonListBox);
        if (scrollViewer is not null)
        {
            viewModel.SeasonListScrollOffset = scrollViewer.VerticalOffset;
        }
    }

    private void QueueRestoreSeasonListScrollOffset()
    {
        if (DataContext is not SeriesOverviewViewModel { SeasonListScrollOffset: > 0 })
        {
            return;
        }

        _isRestoringScrollOffset = true;
        _ = Dispatcher.InvokeAsync(() => RestoreSeasonListScrollOffset(0), DispatcherPriority.Loaded);
    }

    private void RestoreSeasonListScrollOffset(int attempt)
    {
        if (DataContext is not SeriesOverviewViewModel viewModel)
        {
            _isRestoringScrollOffset = false;
            return;
        }

        SeasonListBox.UpdateLayout();
        var scrollViewer = FindDescendant<ScrollViewer>(SeasonListBox);
        if (scrollViewer is null || viewModel.SeasonListScrollOffset <= 0)
        {
            if (attempt < 16)
            {
                _ = Dispatcher.InvokeAsync(() => RestoreSeasonListScrollOffset(attempt + 1), DispatcherPriority.ContextIdle);
                return;
            }

            _isRestoringScrollOffset = false;
            return;
        }

        if (scrollViewer.ScrollableHeight <= 0 && attempt < 16)
        {
            _ = Dispatcher.InvokeAsync(() => RestoreSeasonListScrollOffset(attempt + 1), DispatcherPriority.ContextIdle);
            return;
        }

        scrollViewer.ScrollToVerticalOffset(Math.Min(viewModel.SeasonListScrollOffset, scrollViewer.ScrollableHeight));
        _ = Dispatcher.InvokeAsync(
            () => _isRestoringScrollOffset = false,
            DispatcherPriority.ContextIdle);
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
