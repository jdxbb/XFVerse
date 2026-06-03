using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class SeriesOverviewPage : UserControl
{
    private const double OverviewMouseWheelScrollStep = 48d;
    private bool _isRestoringScrollOffset;
    private int _seasonListScrollApplyVersion;
    private INotifyPropertyChanged? _viewModelPropertyChangedSource;

    public SeriesOverviewPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
        SeasonListBox.Loaded += OnSeasonListBoxLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelState();
        QueueApplySeasonListScrollOffset();
    }

    private void OnSeasonListBoxLoaded(object sender, RoutedEventArgs e)
    {
        QueueApplySeasonListScrollOffset();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModelState();
        AttachViewModelState();
        QueueApplySeasonListScrollOffset();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            StoreCurrentSeasonListScrollOffset();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentSeasonListScrollOffset();
        DetachViewModelState();
    }

    private void OnOverviewPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            ScrollViewerBySmallWheelStep(scrollViewer, e, OverviewMouseWheelScrollStep);
        }
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

    private void QueueApplySeasonListScrollOffset()
    {
        if (DataContext is not SeriesOverviewViewModel viewModel)
        {
            return;
        }

        _isRestoringScrollOffset = true;
        var applyVersion = ++_seasonListScrollApplyVersion;
        if (TryApplySeasonListScrollOffset(viewModel))
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    if (applyVersion == _seasonListScrollApplyVersion)
                    {
                        _isRestoringScrollOffset = false;
                    }
                },
                DispatcherPriority.ContextIdle);
            return;
        }

        _ = Dispatcher.InvokeAsync(() => ApplySeasonListScrollOffset(0, applyVersion), DispatcherPriority.Loaded);
    }

    private void ApplySeasonListScrollOffset(int attempt, int applyVersion)
    {
        if (applyVersion != _seasonListScrollApplyVersion)
        {
            return;
        }

        if (DataContext is not SeriesOverviewViewModel viewModel)
        {
            _isRestoringScrollOffset = false;
            return;
        }

        if (!TryApplySeasonListScrollOffset(viewModel))
        {
            if (attempt < 16)
            {
                _ = Dispatcher.InvokeAsync(
                    () => ApplySeasonListScrollOffset(attempt + 1, applyVersion),
                    DispatcherPriority.ContextIdle);
                return;
            }

            _isRestoringScrollOffset = false;
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (applyVersion == _seasonListScrollApplyVersion)
                {
                    _isRestoringScrollOffset = false;
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private bool TryApplySeasonListScrollOffset(SeriesOverviewViewModel viewModel)
    {
        SeasonListBox.UpdateLayout();
        var scrollViewer = FindDescendant<ScrollViewer>(SeasonListBox);
        if (scrollViewer is null)
        {
            return false;
        }

        var targetOffset = Math.Max(0, viewModel.SeasonListScrollOffset);
        if (targetOffset > 0 && scrollViewer.ScrollableHeight <= 0)
        {
            return false;
        }

        var clampedOffset = Math.Min(targetOffset, scrollViewer.ScrollableHeight);
        if (Math.Abs(scrollViewer.VerticalOffset - clampedOffset) > 0.5)
        {
            scrollViewer.ScrollToVerticalOffset(clampedOffset);
        }

        return true;
    }

    private void AttachViewModelState()
    {
        if (_viewModelPropertyChangedSource is not null)
        {
            return;
        }

        if (DataContext is INotifyPropertyChanged source)
        {
            _viewModelPropertyChangedSource = source;
            source.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void DetachViewModelState()
    {
        if (_viewModelPropertyChangedSource is null)
        {
            return;
        }

        _viewModelPropertyChangedSource.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModelPropertyChangedSource = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SeriesOverviewViewModel.SeasonListScrollOffset))
        {
            QueueApplySeasonListScrollOffset();
        }
    }

    private static void ScrollViewerBySmallWheelStep(
        ScrollViewer scrollViewer,
        System.Windows.Input.MouseWheelEventArgs e,
        double step)
    {
        if (scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var direction = e.Delta > 0 ? -1d : 1d;
        var wheelTicks = Math.Max(1d, Math.Abs(e.Delta) / 120d);
        var targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * step * wheelTicks,
            0d,
            scrollViewer.ScrollableHeight);
        if (Math.Abs(targetOffset - scrollViewer.VerticalOffset) <= double.Epsilon)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
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
