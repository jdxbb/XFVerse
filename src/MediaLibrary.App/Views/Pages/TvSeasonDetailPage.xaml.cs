using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class TvSeasonDetailPage : UserControl
{
    private TvSeasonDetailViewModel? _subscribedViewModel;
    private bool _isRestoringScrollOffset;

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
        if (_subscribedViewModel is null && DataContext is TvSeasonDetailViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            _subscribedViewModel.TargetEpisodeLocated += OnTargetEpisodeLocated;
        }

        QueueRestoreEpisodeListScrollOffset();
    }

    private void OnEpisodeListBoxLoaded(object sender, RoutedEventArgs e)
    {
        QueueRestoreEpisodeListScrollOffset();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            QueueRestoreEpisodeListScrollOffset();
        }
        else
        {
            StoreCurrentEpisodeListScrollOffset();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetEpisodeLocated -= OnTargetEpisodeLocated;
        }

        _subscribedViewModel = e.NewValue as TvSeasonDetailViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetEpisodeLocated += OnTargetEpisodeLocated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentEpisodeListScrollOffset();
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetEpisodeLocated -= OnTargetEpisodeLocated;
            _subscribedViewModel = null;
        }
    }

    private void OnTargetEpisodeLocated(object? sender, TvSeasonDetailViewModel.TvSeasonTargetEpisodeLocatedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => ScrollToTargetEpisode(e.EpisodeId), DispatcherPriority.ContextIdle);
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

    private void QueueRestoreEpisodeListScrollOffset()
    {
        if (DataContext is not TvSeasonDetailViewModel { EpisodeListScrollOffset: > 0 })
        {
            return;
        }

        _isRestoringScrollOffset = true;
        _ = Dispatcher.InvokeAsync(() => RestoreEpisodeListScrollOffset(0), DispatcherPriority.Loaded);
    }

    private void RestoreEpisodeListScrollOffset(int attempt)
    {
        if (DataContext is not TvSeasonDetailViewModel viewModel)
        {
            _isRestoringScrollOffset = false;
            return;
        }

        EpisodeListBox.UpdateLayout();
        var scrollViewer = FindDescendant<ScrollViewer>(EpisodeListBox);
        if (scrollViewer is null || viewModel.EpisodeListScrollOffset <= 0)
        {
            if (attempt < 16)
            {
                _ = Dispatcher.InvokeAsync(() => RestoreEpisodeListScrollOffset(attempt + 1), DispatcherPriority.ContextIdle);
                return;
            }

            _isRestoringScrollOffset = false;
            return;
        }

        if (scrollViewer.ScrollableHeight <= 0 && attempt < 16)
        {
            _ = Dispatcher.InvokeAsync(() => RestoreEpisodeListScrollOffset(attempt + 1), DispatcherPriority.ContextIdle);
            return;
        }

        scrollViewer.ScrollToVerticalOffset(Math.Min(viewModel.EpisodeListScrollOffset, scrollViewer.ScrollableHeight));
        _ = Dispatcher.InvokeAsync(
            () => _isRestoringScrollOffset = false,
            DispatcherPriority.ContextIdle);
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
