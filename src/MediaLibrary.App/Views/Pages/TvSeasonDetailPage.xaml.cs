using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class TvSeasonDetailPage : UserControl
{
    private const double OverviewMouseWheelScrollStep = 48d;
    private TvSeasonDetailViewModel? _subscribedViewModel;
    private bool _isRestoringScrollOffset;
    private int _episodeListScrollApplyVersion;

    public TvSeasonDetailPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Unloaded += OnUnloaded;
        EpisodeListBox.Loaded += OnEpisodeListBoxLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelState();
        QueueApplyEpisodeListScrollOffset();
    }

    private void OnEpisodeListBoxLoaded(object sender, RoutedEventArgs e)
    {
        QueueApplyEpisodeListScrollOffset();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            StoreCurrentEpisodeListScrollOffset();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModelState();
        AttachViewModelState();
        QueueApplyEpisodeListScrollOffset();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentEpisodeListScrollOffset();
        DetachViewModelState();
    }

    private void OnTargetEpisodeLocated(object? sender, TvSeasonDetailViewModel.TvSeasonTargetEpisodeLocatedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => ScrollToTargetEpisode(e.EpisodeId), DispatcherPriority.ContextIdle);
    }

    private void OnOverviewPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            if (scrollViewer.ScrollableHeight <= 0 && IsDescendantOf(scrollViewer, EpisodeListBox))
            {
                var episodeListScrollViewer = FindDescendant<ScrollViewer>(EpisodeListBox);
                if (episodeListScrollViewer is not null)
                {
                    ScrollViewerBySmallWheelStep(episodeListScrollViewer, e, OverviewMouseWheelScrollStep);
                }

                return;
            }

            ScrollViewerBySmallWheelStep(scrollViewer, e, OverviewMouseWheelScrollStep);
            if (scrollViewer.ScrollableHeight > 0)
            {
                e.Handled = true;
            }
        }
    }

    private void OnEpisodeListScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isRestoringScrollOffset || DataContext is not TvSeasonDetailViewModel viewModel)
        {
            return;
        }

        viewModel.EpisodeListScrollOffset = e.VerticalOffset;
    }

    private void StoreCurrentEpisodeListScrollOffset()
    {
        if (_isRestoringScrollOffset || DataContext is not TvSeasonDetailViewModel viewModel)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(EpisodeListBox);
        if (scrollViewer is not null)
        {
            viewModel.EpisodeListScrollOffset = scrollViewer.VerticalOffset;
        }
    }

    private void QueueApplyEpisodeListScrollOffset()
    {
        if (DataContext is not TvSeasonDetailViewModel viewModel)
        {
            return;
        }

        _isRestoringScrollOffset = true;
        var applyVersion = ++_episodeListScrollApplyVersion;
        if (TryApplyEpisodeListScrollOffset(viewModel))
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    if (applyVersion == _episodeListScrollApplyVersion)
                    {
                        _isRestoringScrollOffset = false;
                    }
                },
                DispatcherPriority.ContextIdle);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyEpisodeListScrollOffset(0, applyVersion),
            DispatcherPriority.Loaded);
    }

    private void ApplyEpisodeListScrollOffset(int attempt, int applyVersion)
    {
        if (applyVersion != _episodeListScrollApplyVersion)
        {
            return;
        }

        if (DataContext is not TvSeasonDetailViewModel viewModel)
        {
            _isRestoringScrollOffset = false;
            return;
        }

        if (!TryApplyEpisodeListScrollOffset(viewModel))
        {
            if (attempt < 16)
            {
                _ = Dispatcher.InvokeAsync(
                    () => ApplyEpisodeListScrollOffset(attempt + 1, applyVersion),
                    DispatcherPriority.ContextIdle);
                return;
            }

            _isRestoringScrollOffset = false;
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (applyVersion == _episodeListScrollApplyVersion)
                {
                    _isRestoringScrollOffset = false;
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private bool TryApplyEpisodeListScrollOffset(TvSeasonDetailViewModel viewModel)
    {
        EpisodeListBox.UpdateLayout();
        var scrollViewer = FindDescendant<ScrollViewer>(EpisodeListBox);
        if (scrollViewer is null)
        {
            return false;
        }

        var targetOffset = Math.Max(0, viewModel.EpisodeListScrollOffset);
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

    private void ScrollToTargetEpisode(int episodeId)
    {
        if (DataContext is not TvSeasonDetailViewModel viewModel)
        {
            return;
        }

        var targetEpisode = viewModel.Episodes.FirstOrDefault(episode => episode.EpisodeId == episodeId);
        if (targetEpisode is null)
        {
            return;
        }

        EpisodeListBox.ScrollIntoView(targetEpisode);
        EpisodeListBox.UpdateLayout();
        var container = EpisodeListBox.ItemContainerGenerator.ContainerFromItem(targetEpisode) as FrameworkElement;
        if (container is not null)
        {
            container.BringIntoView();
            return;
        }

        if (EpisodeListBox.Items.Count > 0)
        {
            EpisodeListBox.ScrollIntoView(EpisodeListBox.Items[0]);
        }
    }

    private void AttachViewModelState()
    {
        if (_subscribedViewModel is not null)
        {
            return;
        }

        if (DataContext is not TvSeasonDetailViewModel viewModel)
        {
            return;
        }

        _subscribedViewModel = viewModel;
        viewModel.TargetEpisodeLocated += OnTargetEpisodeLocated;
        ((INotifyPropertyChanged)viewModel).PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModelState()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.TargetEpisodeLocated -= OnTargetEpisodeLocated;
        ((INotifyPropertyChanged)_subscribedViewModel).PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TvSeasonDetailViewModel.EpisodeListScrollOffset))
        {
            QueueApplyEpisodeListScrollOffset();
        }
    }

    private static void ScrollViewerBySmallWheelStep(
        ScrollViewer scrollViewer,
        MouseWheelEventArgs e,
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

    private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
    {
        var current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = current is FrameworkElement frameworkElement
                ? frameworkElement.Parent ?? VisualTreeHelper.GetParent(current)
                : VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
