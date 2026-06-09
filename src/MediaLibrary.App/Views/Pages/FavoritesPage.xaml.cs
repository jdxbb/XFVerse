using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class FavoritesPage : UserControl
{
    private const int FavoritesScrollRestoreMaxAttempts = 16;
    private INotifyPropertyChanged? _favoritesPropertyChangedSource;
    private bool _isRestoringFavoritesScrollOffset;
    private int _favoritesScrollApplyVersion;

    public FavoritesPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachFavoritesState();
        QueueApplyFavoritesScrollOffset();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        StoreCurrentFavoritesScrollOffsets();
        DetachFavoritesState();
        AttachFavoritesState();
        QueueApplyFavoritesScrollOffset();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StoreCurrentFavoritesScrollOffsets();
        DetachFavoritesState();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            StoreCurrentFavoritesScrollOffsets();
            return;
        }

        QueueApplyFavoritesScrollOffset();
    }

    private void FavoritesListBox_Loaded(object sender, RoutedEventArgs e)
    {
        QueueApplyFavoritesScrollOffset();
    }

    private void FavoritesListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            StoreFavoritesListBoxScrollOffset(listBox, e.VerticalOffset);
        }
    }

    private void AttachFavoritesState()
    {
        if (_favoritesPropertyChangedSource is not null)
        {
            return;
        }

        if (DataContext is INotifyPropertyChanged source)
        {
            _favoritesPropertyChangedSource = source;
            source.PropertyChanged += OnFavoritesPropertyChanged;
        }
    }

    private void DetachFavoritesState()
    {
        if (_favoritesPropertyChangedSource is null)
        {
            return;
        }

        _favoritesPropertyChangedSource.PropertyChanged -= OnFavoritesPropertyChanged;
        _favoritesPropertyChangedSource = null;
    }

    private void OnFavoritesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FavoritesViewModel.SelectedTabIndex)
            or nameof(FavoritesViewModel.ShowFavoriteMovies)
            or nameof(FavoritesViewModel.ShowWantToWatchMovies)
            or nameof(FavoritesViewModel.IsLoading)
            or nameof(FavoritesViewModel.HasLoadError))
        {
            QueueApplyFavoritesScrollOffset();
        }
    }

    private void StoreFavoritesListBoxScrollOffset(ListBox listBox, double verticalOffset)
    {
        if (_isRestoringFavoritesScrollOffset
            || !listBox.IsVisible
            || DataContext is not FavoritesViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(listBox, FavoritePosterListBox))
        {
            viewModel.FavoriteScrollOffset = verticalOffset;
        }
        else if (ReferenceEquals(listBox, WantToWatchPosterListBox))
        {
            viewModel.WantToWatchScrollOffset = verticalOffset;
        }
    }

    private void StoreCurrentFavoritesScrollOffsets()
    {
        if (_isRestoringFavoritesScrollOffset || DataContext is not FavoritesViewModel viewModel)
        {
            return;
        }

        StoreCurrentFavoritesListBoxScrollOffset(FavoritePosterListBox, offset => viewModel.FavoriteScrollOffset = offset);
        StoreCurrentFavoritesListBoxScrollOffset(WantToWatchPosterListBox, offset => viewModel.WantToWatchScrollOffset = offset);
    }

    private static void StoreCurrentFavoritesListBoxScrollOffset(ListBox listBox, Action<double> storeOffset)
    {
        if (!listBox.IsVisible)
        {
            return;
        }

        var scrollViewer = FindVisualDescendant<ScrollViewer>(listBox);
        if (scrollViewer is not null)
        {
            storeOffset(scrollViewer.VerticalOffset);
        }
    }

    private void QueueApplyFavoritesScrollOffset()
    {
        if (DataContext is not FavoritesViewModel viewModel)
        {
            return;
        }

        _isRestoringFavoritesScrollOffset = true;
        var applyVersion = ++_favoritesScrollApplyVersion;
        if (TryApplyFavoritesScrollOffset(viewModel))
        {
            _ = Dispatcher.InvokeAsync(
                () => FinishFavoritesScrollRestore(applyVersion),
                DispatcherPriority.ContextIdle);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyFavoritesScrollOffset(0, applyVersion),
            DispatcherPriority.Loaded);
    }

    private void ApplyFavoritesScrollOffset(int attempt, int applyVersion)
    {
        if (applyVersion != _favoritesScrollApplyVersion)
        {
            return;
        }

        if (DataContext is not FavoritesViewModel viewModel)
        {
            FinishFavoritesScrollRestore(applyVersion);
            return;
        }

        if (TryApplyFavoritesScrollOffset(viewModel) || attempt >= FavoritesScrollRestoreMaxAttempts)
        {
            FinishFavoritesScrollRestore(applyVersion);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyFavoritesScrollOffset(attempt + 1, applyVersion),
            DispatcherPriority.ContextIdle);
    }

    private bool TryApplyFavoritesScrollOffset(FavoritesViewModel viewModel)
    {
        var (source, targetOffset) = viewModel.SelectedTabIndex == 1
            ? (WantToWatchPosterListBox, viewModel.WantToWatchScrollOffset)
            : (FavoritePosterListBox, viewModel.FavoriteScrollOffset);
        targetOffset = Math.Max(0d, targetOffset);
        if (!source.IsVisible)
        {
            return targetOffset <= 0d;
        }

        source.UpdateLayout();
        var scrollViewer = FindVisualDescendant<ScrollViewer>(source);
        if (scrollViewer is null)
        {
            return false;
        }

        if (targetOffset > 0d && scrollViewer.ScrollableHeight <= 0d)
        {
            return false;
        }

        var clampedOffset = Math.Min(targetOffset, scrollViewer.ScrollableHeight);
        if (Math.Abs(scrollViewer.VerticalOffset - clampedOffset) > 0.5d)
        {
            scrollViewer.ScrollToVerticalOffset(clampedOffset);
        }

        return true;
    }

    private void FinishFavoritesScrollRestore(int applyVersion)
    {
        if (applyVersion == _favoritesScrollApplyVersion)
        {
            _isRestoringFavoritesScrollOffset = false;
        }
    }

    private static T? FindVisualDescendant<T>(DependencyObject source)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(source);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindVisualDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
