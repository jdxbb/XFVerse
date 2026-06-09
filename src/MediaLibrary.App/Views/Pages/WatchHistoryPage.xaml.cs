using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class WatchHistoryPage : UserControl
{
    private const int HistoryScrollRestoreMaxAttempts = 16;
    private WatchHistoryViewModel? _subscribedViewModel;
    private bool _isRestoringHistoryScrollOffset;
    private int _historyScrollApplyVersion;

    public WatchHistoryPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachWatchHistoryState(DataContext as WatchHistoryViewModel);
        QueueApplyHistoryScrollOffset();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        StoreCurrentHistoryScrollOffset();
        AttachWatchHistoryState(e.NewValue as WatchHistoryViewModel);
        QueueApplyHistoryScrollOffset();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentHistoryScrollOffset();
        DetachWatchHistoryState();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            StoreCurrentHistoryScrollOffset();
            return;
        }

        QueueApplyHistoryScrollOffset();
    }

    private void OnHistoryScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        QueueApplyHistoryScrollOffset();
    }

    private void OnHistoryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isRestoringHistoryScrollOffset || DataContext is not WatchHistoryViewModel viewModel)
        {
            return;
        }

        if (e.VerticalOffset <= 0d
            && viewModel.ScrollOffset > 0d
            && (Math.Abs(e.ExtentHeightChange) > 0.1d || Math.Abs(e.ViewportHeightChange) > 0.1d))
        {
            return;
        }

        viewModel.ScrollOffset = e.VerticalOffset;
    }

    private void AttachWatchHistoryState(WatchHistoryViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            return;
        }

        DetachWatchHistoryState();
        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetDateLocated += OnTargetDateLocated;
            _subscribedViewModel.PropertyChanged += OnWatchHistoryPropertyChanged;
        }
    }

    private void DetachWatchHistoryState()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.TargetDateLocated -= OnTargetDateLocated;
        _subscribedViewModel.PropertyChanged -= OnWatchHistoryPropertyChanged;
        _subscribedViewModel = null;
    }

    private void OnWatchHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WatchHistoryViewModel.HasDayGroups)
            or nameof(WatchHistoryViewModel.IsLoading))
        {
            QueueApplyHistoryScrollOffset();
        }
    }

    private void OnTargetDateLocated(object? sender, WatchHistoryViewModel.WatchHistoryTargetDateLocatedEventArgs e)
    {
        _historyScrollApplyVersion++;
        _ = Dispatcher.InvokeAsync(() => ScrollToTargetDate(e.TargetDate), DispatcherPriority.ContextIdle);
    }

    private void ScrollToTargetDate(DateTime targetDate)
    {
        if (DataContext is not WatchHistoryViewModel viewModel)
        {
            return;
        }

        var targetGroup = viewModel.DayGroups.FirstOrDefault(group => group.Date == targetDate.Date);
        if (targetGroup is null)
        {
            return;
        }

        HistoryGroupsItemsControl.UpdateLayout();
        var container = HistoryGroupsItemsControl.ItemContainerGenerator.ContainerFromItem(targetGroup) as FrameworkElement;
        if (container is not null)
        {
            ScrollElementIntoView(container);
            HistoryScrollViewer.Opacity = 1d;
            _isRestoringHistoryScrollOffset = false;
            StoreCurrentHistoryScrollOffset();
            return;
        }

        HistoryScrollViewer.ScrollToTop();
        HistoryScrollViewer.Opacity = 1d;
        _isRestoringHistoryScrollOffset = false;
        StoreCurrentHistoryScrollOffset();
    }

    private void StoreCurrentHistoryScrollOffset()
    {
        if (_isRestoringHistoryScrollOffset || DataContext is not WatchHistoryViewModel viewModel)
        {
            return;
        }

        var currentOffset = HistoryScrollViewer.VerticalOffset;
        if (currentOffset <= 0d
            && viewModel.ScrollOffset > 0d
            && (!HistoryScrollViewer.IsVisible
                || HistoryScrollViewer.ExtentHeight <= 0d
                || HistoryScrollViewer.ScrollableHeight <= 0d))
        {
            return;
        }

        viewModel.ScrollOffset = currentOffset;
    }

    private void QueueApplyHistoryScrollOffset()
    {
        if (DataContext is not WatchHistoryViewModel viewModel)
        {
            return;
        }

        var targetOffset = Math.Max(0d, viewModel.ScrollOffset);
        if (targetOffset <= 0d)
        {
            HistoryScrollViewer.Opacity = 1d;
            return;
        }

        _isRestoringHistoryScrollOffset = true;
        HistoryScrollViewer.Opacity = 0d;
        var applyVersion = ++_historyScrollApplyVersion;
        if (TryApplyHistoryScrollOffset(viewModel))
        {
            FinishHistoryScrollRestore(applyVersion);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyHistoryScrollOffset(0, applyVersion),
            DispatcherPriority.Loaded);
    }

    private void ApplyHistoryScrollOffset(int attempt, int applyVersion)
    {
        if (applyVersion != _historyScrollApplyVersion)
        {
            return;
        }

        if (DataContext is not WatchHistoryViewModel viewModel)
        {
            FinishHistoryScrollRestore(applyVersion);
            return;
        }

        if (TryApplyHistoryScrollOffset(viewModel) || attempt >= HistoryScrollRestoreMaxAttempts)
        {
            FinishHistoryScrollRestore(applyVersion);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyHistoryScrollOffset(attempt + 1, applyVersion),
            DispatcherPriority.ContextIdle);
    }

    private bool TryApplyHistoryScrollOffset(WatchHistoryViewModel viewModel)
    {
        if (!HistoryScrollViewer.IsVisible)
        {
            return !viewModel.HasDayGroups;
        }

        HistoryScrollViewer.UpdateLayout();
        var targetOffset = Math.Max(0d, viewModel.ScrollOffset);
        if (targetOffset > 0d && HistoryScrollViewer.ScrollableHeight <= 0d)
        {
            return false;
        }

        var clampedOffset = Math.Min(targetOffset, HistoryScrollViewer.ScrollableHeight);
        if (Math.Abs(HistoryScrollViewer.VerticalOffset - clampedOffset) > 0.5d)
        {
            HistoryScrollViewer.ScrollToVerticalOffset(clampedOffset);
        }

        return true;
    }

    private void FinishHistoryScrollRestore(int applyVersion)
    {
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (applyVersion == _historyScrollApplyVersion)
                {
                    HistoryScrollViewer.Opacity = 1d;
                    _isRestoringHistoryScrollOffset = false;
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private void ScrollElementIntoView(FrameworkElement element)
    {
        try
        {
            var point = element.TransformToAncestor(HistoryScrollViewer).Transform(new Point(0d, 0d));
            var targetOffset = Math.Clamp(
                HistoryScrollViewer.VerticalOffset + point.Y,
                0d,
                HistoryScrollViewer.ScrollableHeight);
            HistoryScrollViewer.ScrollToVerticalOffset(targetOffset);
        }
        catch (InvalidOperationException)
        {
            element.BringIntoView();
        }
    }
}
