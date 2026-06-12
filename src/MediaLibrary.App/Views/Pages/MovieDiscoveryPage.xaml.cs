using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.ViewModels.Main;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.App.Views.Pages;

public partial class MovieDiscoveryPage : UserControl
{
    private const double CollapsedSearchColumnWidth = 544;
    private const double ExpandedSearchColumnWidth = 440;
    private const double FilterMenuWheelScrollStep = 42;
    private const double RankingTabHoverMenuDelayMilliseconds = 420d;
    private const double RankingOverviewMouseWheelScrollStep = 48d;
    private const double RankingOuterMouseWheelScrollStep = 56d;
    private const double PosterPagerBottomThreshold = 1d;
    private readonly DispatcherTimer _rankingTabHoverMenuTimer;
    private Button? _openMenuButton;
    private ContextMenu? _openContextMenu;
    private Button? _pendingRankingTabMenuButton;
    private ContextMenu? _pendingRankingTabContextMenu;
    private INotifyPropertyChanged? _shellPropertyChangedSource;
    private INotifyPropertyChanged? _discoveryPropertyChangedSource;
    private bool _isRestoringDiscoveryScrollOffset;
    private int _discoveryScrollApplyVersion;

    public MovieDiscoveryPage()
    {
        _rankingTabHoverMenuTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RankingTabHoverMenuDelayMilliseconds)
        };
        _rankingTabHoverMenuTimer.Tick += RankingTabHoverMenuTimer_Tick;

        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachDiscoveryState();
        AttachShellState();
        UpdateSearchToolbarWidth();
        QueueApplyDiscoveryScrollOffset();
        QueueUpdateDiscoveryPosterPagerVisibility();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelRankingTabHoverMenu();
        StoreCurrentDiscoveryScrollOffsets();
        DetachShellState();
        DetachDiscoveryState();
        CloseOpenMenu();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachDiscoveryState();
        AttachDiscoveryState();
        UpdateSearchToolbarWidth();
        QueueApplyDiscoveryScrollOffset();
        QueueUpdateDiscoveryPosterPagerVisibility();
    }

    private void AttachShellState()
    {
        if (_shellPropertyChangedSource is not null)
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is INotifyPropertyChanged source)
        {
            _shellPropertyChangedSource = source;
            source.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void DetachShellState()
    {
        if (_shellPropertyChangedSource is null)
        {
            return;
        }

        _shellPropertyChangedSource.PropertyChanged -= OnShellPropertyChanged;
        _shellPropertyChangedSource = null;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsSidebarCollapsed)
            or nameof(MainWindowViewModel.IsSidebarExpanded)
            or nameof(MainWindowViewModel.SidebarColumnWidth))
        {
            UpdateSearchToolbarWidth();
        }
    }

    private void UpdateSearchToolbarWidth()
    {
        var isSidebarCollapsed = true;
        if (Window.GetWindow(this)?.DataContext is MainWindowViewModel shellViewModel)
        {
            isSidebarCollapsed = shellViewModel.IsSidebarCollapsed;
        }

        SearchToolbarColumn.Width = new GridLength(
            isSidebarCollapsed ? CollapsedSearchColumnWidth : ExpandedSearchColumnWidth);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            StoreCurrentDiscoveryScrollOffsets();
            CloseOpenMenu();
            return;
        }

        QueueApplyDiscoveryScrollOffset();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && !IsWithinTextInput(source))
        {
            Keyboard.ClearFocus();
        }
    }

    private void SearchTextBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
        }
    }

    private void ContextMenu_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ContextMenu contextMenu)
        {
            return;
        }

        var scrollViewer = FindVisualDescendant<ScrollViewer>(contextMenu);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var offsetDelta = e.Delta > 0 ? -FilterMenuWheelScrollStep : FilterMenuWheelScrollStep;
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + offsetDelta);
        e.Handled = true;
    }

    private void MenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
        }
    }

    private void OpenButtonContextMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } contextMenu } button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }

        OpenContextMenuForButton(button, contextMenu);
        e.Handled = true;
    }

    private void RankingTabButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRankingTabHoverMenu();

        if (DataContext is MovieDiscoveryViewModel { IsRankingTabSelected: false } viewModel)
        {
            CloseOpenMenu();
            if (viewModel.SelectDiscoveryTabCommand.CanExecute("1"))
            {
                viewModel.SelectDiscoveryTabCommand.Execute("1");
            }

            if (sender is Button { ContextMenu: { } contextMenu } button)
            {
                _ = Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (CanOpenRankingTabMenu())
                        {
                            OpenContextMenuForButton(button, contextMenu);
                        }
                    },
                    DispatcherPriority.Loaded);
            }

            e.Handled = true;
            return;
        }

        OpenButtonContextMenu(sender, e);
    }

    private void RankingTabButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!CanOpenRankingTabMenu()
            || sender is not Button { ContextMenu: { } contextMenu } button
            || IsOpenMenuButton(button))
        {
            return;
        }

        ScheduleRankingTabHoverMenu(button, contextMenu);
        e.Handled = true;
    }

    private void RankingTabButton_MouseLeave(object sender, MouseEventArgs e)
    {
        CancelRankingTabHoverMenu();
    }

    private void ScheduleRankingTabHoverMenu(Button button, ContextMenu contextMenu)
    {
        CancelRankingTabHoverMenu();
        _pendingRankingTabMenuButton = button;
        _pendingRankingTabContextMenu = contextMenu;
        _rankingTabHoverMenuTimer.Start();
    }

    private void RankingTabHoverMenuTimer_Tick(object? sender, EventArgs e)
    {
        _rankingTabHoverMenuTimer.Stop();

        var button = _pendingRankingTabMenuButton;
        var contextMenu = _pendingRankingTabContextMenu;
        _pendingRankingTabMenuButton = null;
        _pendingRankingTabContextMenu = null;

        if (button is null
            || contextMenu is null
            || !button.IsMouseOver
            || !CanOpenRankingTabMenu()
            || IsOpenMenuButton(button))
        {
            return;
        }

        OpenContextMenuForButton(button, contextMenu);
    }

    private void RankingTabButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (CanOpenRankingTabMenu())
        {
            return;
        }

        e.Handled = true;
    }

    private void RankingNextPageButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MovieDiscoveryViewModel viewModel)
        {
            return;
        }

        var buttonEnabled = sender is Button button && button.IsEnabled;
        AiPerfDiagnostics.WriteEvent(
            "event=ranking-next-ui-pointer "
            + $"mediaType={(viewModel.IsTvRankingSelected ? "tv" : "movie")} "
            + $"buttonEnabled={buttonEnabled} "
            + $"commandCanExecute={viewModel.GoNextRankingPageCommand.CanExecute(null)} "
            + $"activeCanNext={viewModel.CanGoNextActiveRankingPage} "
            + $"movieCanNext={viewModel.CanGoNextRankingPage} "
            + $"tvCanNext={viewModel.CanGoNextTvRankingPage} "
            + $"moviePage={viewModel.RankingPageIndex} "
            + $"movieTotalPages={viewModel.RankingTotalDisplayPages} "
            + $"tvPage={viewModel.TvRankingPageIndex} "
            + $"tvTotalPages={viewModel.TvRankingTotalDisplayPages} "
            + $"movieLoading={viewModel.IsRankingLoading} "
            + $"tvLoading={viewModel.IsTvRankingLoading} "
            + $"tvNavigating={viewModel.IsTvSeriesNavigating} "
            + $"menuOpen={_openContextMenu?.IsOpen == true}");
    }

    private bool CanOpenRankingTabMenu()
    {
        return DataContext is MovieDiscoveryViewModel { IsRankingTabSelected: true };
    }

    private void ContextMenu_Closed(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_openContextMenu, sender))
        {
            _openMenuButton = null;
            _openContextMenu = null;
        }
    }

    private void OpenContextMenuForButton(Button button, ContextMenu contextMenu)
    {
        CloseOpenMenu();
        contextMenu.PlacementTarget = button;
        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.Closed -= ContextMenu_Closed;
        contextMenu.Closed += ContextMenu_Closed;
        _openMenuButton = button;
        _openContextMenu = contextMenu;
        contextMenu.IsOpen = true;
        AlignContextMenuToButtonCenter(button, contextMenu);
        QueueUpdateSingleSelectMenuChecks(button, contextMenu);
        ConfigureSubmenusToOpenRight(contextMenu);
    }

    private bool IsOpenMenuButton(Button button)
    {
        return ReferenceEquals(_openMenuButton, button)
               && _openContextMenu is not null;
    }

    private void CloseOpenMenu()
    {
        CancelRankingTabHoverMenu();

        if (_openContextMenu is not null)
        {
            _openContextMenu.IsOpen = false;
        }

        _openMenuButton = null;
        _openContextMenu = null;
    }

    private void CancelRankingTabHoverMenu()
    {
        _rankingTabHoverMenuTimer.Stop();
        _pendingRankingTabMenuButton = null;
        _pendingRankingTabContextMenu = null;
    }

    private void AlignContextMenuToButtonCenter(Button button, ContextMenu contextMenu)
    {
        contextMenu.HorizontalOffset = 0;
        contextMenu.VerticalOffset = 4;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                if (!contextMenu.IsOpen)
                {
                    return;
                }

                contextMenu.HorizontalOffset = Math.Round((button.ActualWidth - contextMenu.ActualWidth) * 0.5);
            },
            DispatcherPriority.Loaded);
    }

    private void QueueUpdateSingleSelectMenuChecks(Button button, ContextMenu contextMenu)
    {
        var selectedValue = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(selectedValue))
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            () => UpdateSingleSelectMenuChecks(contextMenu, selectedValue),
            DispatcherPriority.Loaded);
    }

    private static void UpdateSingleSelectMenuChecks(ContextMenu contextMenu, string selectedValue)
    {
        contextMenu.UpdateLayout();
        for (var index = 0; index < contextMenu.Items.Count; index++)
        {
            var menuItem = contextMenu.ItemContainerGenerator.ContainerFromIndex(index) as MenuItem
                           ?? contextMenu.Items[index] as MenuItem;
            if (menuItem is null)
            {
                continue;
            }

            var optionValue = (menuItem.CommandParameter ?? menuItem.Header)?.ToString();
            if (string.IsNullOrWhiteSpace(optionValue))
            {
                continue;
            }

            menuItem.IsCheckable = true;
            menuItem.IsChecked = string.Equals(optionValue, selectedValue, StringComparison.Ordinal);
        }
    }

    private static void ConfigureSubmenusToOpenRight(ItemsControl root)
    {
        foreach (var menuItem in root.Items.OfType<MenuItem>())
        {
            menuItem.SubmenuOpened -= OnSubmenuOpened;
            menuItem.SubmenuOpened += OnSubmenuOpened;
            ConfigureSubmenuPopupToOpenRight(menuItem);
            ConfigureSubmenusToOpenRight(menuItem);
        }
    }

    private static void OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        ConfigureSubmenuPopupToOpenRight(menuItem);
        ConfigureSubmenusToOpenRight(menuItem);
        _ = menuItem.Dispatcher.BeginInvoke(
            () => ConfigureSubmenuPopupToOpenRight(menuItem),
            DispatcherPriority.Loaded);
    }

    private static void ConfigureSubmenuPopupToOpenRight(MenuItem menuItem)
    {
        menuItem.ApplyTemplate();
        if (menuItem.Template.FindName("PART_Popup", menuItem) is not Popup popup)
        {
            return;
        }

        popup.PlacementTarget = menuItem;
        popup.Placement = PlacementMode.Custom;
        popup.CustomPopupPlacementCallback = PlaceSubmenuOnRight;
        popup.HorizontalOffset = 0;
        popup.VerticalOffset = -7;
    }

    private static CustomPopupPlacement[] PlaceSubmenuOnRight(Size popupSize, Size targetSize, Point offset)
    {
        return
        [
            new CustomPopupPlacement(
                new Point(targetSize.Width, 0),
                PopupPrimaryAxis.Horizontal)
        ];
    }

    private void DiscoveryScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        QueueApplyDiscoveryScrollOffset();
    }

    private void DiscoveryScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            StoreDiscoveryScrollOffset(scrollViewer, e.VerticalOffset);
        }
    }

    private void DiscoveryListBox_Loaded(object sender, RoutedEventArgs e)
    {
        QueueApplyDiscoveryScrollOffset();
        if (sender is ListBox listBox)
        {
            QueueUpdateDiscoveryPosterPagerVisibility(listBox);
        }
    }

    private void DiscoveryListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            StoreDiscoveryListBoxScrollOffset(listBox, e.VerticalOffset);
            UpdateDiscoveryPosterPagerVisibility(listBox);
        }
    }

    private void AttachDiscoveryState()
    {
        if (_discoveryPropertyChangedSource is not null)
        {
            return;
        }

        if (DataContext is INotifyPropertyChanged source)
        {
            _discoveryPropertyChangedSource = source;
            source.PropertyChanged += OnDiscoveryPropertyChanged;
            if (source is MovieDiscoveryViewModel viewModel)
            {
                viewModel.RequestCloseFilterMenu += OnDiscoveryRequestCloseFilterMenu;
            }
        }
    }

    private void DetachDiscoveryState()
    {
        if (_discoveryPropertyChangedSource is null)
        {
            return;
        }

        if (_discoveryPropertyChangedSource is MovieDiscoveryViewModel viewModel)
        {
            viewModel.RequestCloseFilterMenu -= OnDiscoveryRequestCloseFilterMenu;
        }

        _discoveryPropertyChangedSource.PropertyChanged -= OnDiscoveryPropertyChanged;
        _discoveryPropertyChangedSource = null;
    }

    private void OnDiscoveryRequestCloseFilterMenu(object? sender, EventArgs e)
    {
        CloseOpenMenu();
    }

    private void OnDiscoveryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MovieDiscoveryViewModel.SelectedTabIndex)
            or nameof(MovieDiscoveryViewModel.SelectedSearchMediaType)
            or nameof(MovieDiscoveryViewModel.IsSearchPosterLayout)
            or nameof(MovieDiscoveryViewModel.HasSearchMovies)
            or nameof(MovieDiscoveryViewModel.HasSearchTvSeries)
            or nameof(MovieDiscoveryViewModel.IsSearchLoading)
            or nameof(MovieDiscoveryViewModel.IsTvSearchLoading)
            or nameof(MovieDiscoveryViewModel.SelectedRankingMediaType)
            or nameof(MovieDiscoveryViewModel.HasRankingMovies)
            or nameof(MovieDiscoveryViewModel.HasRankingTvSeries)
            or nameof(MovieDiscoveryViewModel.IsRankingLoading)
            or nameof(MovieDiscoveryViewModel.IsTvRankingLoading))
        {
            QueueApplyDiscoveryScrollOffset();
            QueueUpdateDiscoveryPosterPagerVisibility();
        }
    }

    private void StoreDiscoveryScrollOffset(ScrollViewer scrollViewer, double verticalOffset)
    {
        if (_isRestoringDiscoveryScrollOffset
            || !scrollViewer.IsVisible
            || DataContext is not MovieDiscoveryViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(scrollViewer, SearchMovieListScrollViewer))
        {
            viewModel.SearchMovieListScrollOffset = verticalOffset;
        }
        else if (ReferenceEquals(scrollViewer, SearchTvListScrollViewer))
        {
            viewModel.SearchTvListScrollOffset = verticalOffset;
        }
        else if (ReferenceEquals(scrollViewer, RankingMovieScrollViewer))
        {
            viewModel.RankingMovieScrollOffset = verticalOffset;
        }
        else if (ReferenceEquals(scrollViewer, RankingTvScrollViewer))
        {
            viewModel.RankingTvScrollOffset = verticalOffset;
        }
    }

    private void StoreDiscoveryListBoxScrollOffset(ListBox listBox, double verticalOffset)
    {
        if (_isRestoringDiscoveryScrollOffset
            || !listBox.IsVisible
            || DataContext is not MovieDiscoveryViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(listBox, SearchMoviePosterListBox))
        {
            viewModel.SearchMoviePosterScrollOffset = verticalOffset;
        }
        else if (ReferenceEquals(listBox, SearchTvPosterListBox))
        {
            viewModel.SearchTvPosterScrollOffset = verticalOffset;
        }
    }

    private void StoreCurrentDiscoveryScrollOffsets()
    {
        if (_isRestoringDiscoveryScrollOffset || DataContext is not MovieDiscoveryViewModel viewModel)
        {
            return;
        }

        StoreCurrentDiscoveryListBoxScrollOffset(SearchMoviePosterListBox, offset => viewModel.SearchMoviePosterScrollOffset = offset);
        StoreCurrentDiscoveryScrollOffset(SearchMovieListScrollViewer, offset => viewModel.SearchMovieListScrollOffset = offset);
        StoreCurrentDiscoveryListBoxScrollOffset(SearchTvPosterListBox, offset => viewModel.SearchTvPosterScrollOffset = offset);
        StoreCurrentDiscoveryScrollOffset(SearchTvListScrollViewer, offset => viewModel.SearchTvListScrollOffset = offset);
        StoreCurrentDiscoveryScrollOffset(RankingMovieScrollViewer, offset => viewModel.RankingMovieScrollOffset = offset);
        StoreCurrentDiscoveryScrollOffset(RankingTvScrollViewer, offset => viewModel.RankingTvScrollOffset = offset);
    }

    private static void StoreCurrentDiscoveryScrollOffset(ScrollViewer scrollViewer, Action<double> storeOffset)
    {
        if (scrollViewer.IsVisible)
        {
            storeOffset(scrollViewer.VerticalOffset);
        }
    }

    private static void StoreCurrentDiscoveryListBoxScrollOffset(ListBox listBox, Action<double> storeOffset)
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

    private void QueueApplyDiscoveryScrollOffset()
    {
        if (DataContext is not MovieDiscoveryViewModel viewModel)
        {
            return;
        }

        _isRestoringDiscoveryScrollOffset = true;
        var applyVersion = ++_discoveryScrollApplyVersion;
        if (TryApplyDiscoveryScrollOffset(viewModel))
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    if (applyVersion == _discoveryScrollApplyVersion)
                    {
                        _isRestoringDiscoveryScrollOffset = false;
                    }
                },
                DispatcherPriority.ContextIdle);
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () => ApplyDiscoveryScrollOffset(0, applyVersion),
            DispatcherPriority.Loaded);
    }

    private void ApplyDiscoveryScrollOffset(int attempt, int applyVersion)
    {
        if (applyVersion != _discoveryScrollApplyVersion)
        {
            return;
        }

        if (DataContext is not MovieDiscoveryViewModel viewModel)
        {
            _isRestoringDiscoveryScrollOffset = false;
            return;
        }

        if (!TryApplyDiscoveryScrollOffset(viewModel))
        {
            if (attempt < 16)
            {
                _ = Dispatcher.InvokeAsync(
                    () => ApplyDiscoveryScrollOffset(attempt + 1, applyVersion),
                    DispatcherPriority.ContextIdle);
                return;
            }

            _isRestoringDiscoveryScrollOffset = false;
            return;
        }

        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (applyVersion == _discoveryScrollApplyVersion)
                {
                    _isRestoringDiscoveryScrollOffset = false;
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private bool TryApplyDiscoveryScrollOffset(MovieDiscoveryViewModel viewModel)
    {
        var target = GetActiveDiscoveryScrollTarget(viewModel);
        if (target is null)
        {
            return true;
        }

        var (source, targetOffset) = target.Value;
        targetOffset = Math.Max(0d, targetOffset);
        if (!source.IsVisible)
        {
            return targetOffset <= 0d;
        }

        source.UpdateLayout();
        var scrollViewer = GetDiscoveryScrollViewer(source);
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

        if (source is ListBox listBox)
        {
            UpdateDiscoveryPosterPagerVisibility(listBox, scrollViewer);
        }

        return true;
    }

    private (FrameworkElement Source, double Offset)? GetActiveDiscoveryScrollTarget(MovieDiscoveryViewModel viewModel)
    {
        return viewModel.SelectedTabIndex switch
        {
            0 when viewModel.IsTvSearchSelected && viewModel.IsSearchPosterLayout =>
                (SearchTvPosterListBox, viewModel.SearchTvPosterScrollOffset),
            0 when viewModel.IsTvSearchSelected =>
                (SearchTvListScrollViewer, viewModel.SearchTvListScrollOffset),
            0 when viewModel.IsSearchPosterLayout =>
                (SearchMoviePosterListBox, viewModel.SearchMoviePosterScrollOffset),
            0 =>
                (SearchMovieListScrollViewer, viewModel.SearchMovieListScrollOffset),
            1 when viewModel.IsTvRankingSelected =>
                (RankingTvScrollViewer, viewModel.RankingTvScrollOffset),
            1 =>
                (RankingMovieScrollViewer, viewModel.RankingMovieScrollOffset),
            _ => null
        };
    }

    private static ScrollViewer? GetDiscoveryScrollViewer(FrameworkElement source)
    {
        return source as ScrollViewer ?? FindVisualDescendant<ScrollViewer>(source);
    }

    private void QueueUpdateDiscoveryPosterPagerVisibility()
    {
        _ = Dispatcher.InvokeAsync(
            () => UpdateDiscoveryPosterPagerVisibility(),
            DispatcherPriority.Loaded);
    }

    private void QueueUpdateDiscoveryPosterPagerVisibility(ListBox listBox)
    {
        _ = Dispatcher.InvokeAsync(
            () => UpdateDiscoveryPosterPagerVisibility(listBox),
            DispatcherPriority.Loaded);
    }

    private void UpdateDiscoveryPosterPagerVisibility()
    {
        UpdateDiscoveryPosterPagerVisibility(SearchMoviePosterListBox);
        UpdateDiscoveryPosterPagerVisibility(SearchTvPosterListBox);
    }

    private void UpdateDiscoveryPosterPagerVisibility(ListBox listBox)
    {
        var scrollViewer = FindVisualDescendant<ScrollViewer>(listBox);
        UpdateDiscoveryPosterPagerVisibility(listBox, scrollViewer);
    }

    private void UpdateDiscoveryPosterPagerVisibility(ListBox listBox, ScrollViewer? scrollViewer)
    {
        var pagerPanel = GetDiscoveryPosterPagerPanel(listBox);
        if (pagerPanel is null)
        {
            return;
        }

        if (!listBox.IsVisible || scrollViewer is null)
        {
            pagerPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var shouldShowPager = scrollViewer.ScrollableHeight <= PosterPagerBottomThreshold
            || scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - PosterPagerBottomThreshold;
        pagerPanel.Visibility = shouldShowPager ? Visibility.Visible : Visibility.Collapsed;
    }

    private StackPanel? GetDiscoveryPosterPagerPanel(ListBox listBox)
    {
        if (ReferenceEquals(listBox, SearchMoviePosterListBox))
        {
            return SearchMoviePosterPagerPanel;
        }

        if (ReferenceEquals(listBox, SearchTvPosterListBox))
        {
            return SearchTvPosterPagerPanel;
        }

        return null;
    }

    private void RankingOverviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (HasVerticalScrollableContent(scrollViewer))
        {
            _ = ScrollViewerBySmallWheelStep(scrollViewer, e.Delta, RankingOverviewMouseWheelScrollStep);
            HideAncestorScrollViewers(scrollViewer);
            e.Handled = true;
            return;
        }

        if (ScrollParentBySmallWheelStep(scrollViewer, e.Delta))
        {
            e.Handled = true;
        }
    }

    private static bool ScrollParentBySmallWheelStep(DependencyObject source, int wheelDelta)
    {
        var direction = wheelDelta < 0 ? 1 : -1;
        for (var current = GetParent(source); current is not null; current = GetParent(current))
        {
            if (current is ScrollViewer scrollViewer
                && CanScrollVertically(scrollViewer, direction)
                && ScrollViewerBySmallWheelStep(scrollViewer, wheelDelta, RankingOuterMouseWheelScrollStep))
            {
                return true;
            }
        }

        return false;
    }

    private static void HideAncestorScrollViewers(DependencyObject source)
    {
        for (var current = GetParent(source); current is not null; current = GetParent(current))
        {
            if (current is ScrollViewer scrollViewer)
            {
                ScrollBarAutoRevealBehavior.Hide(scrollViewer);
            }
        }
    }

    private static bool ScrollViewerBySmallWheelStep(ScrollViewer scrollViewer, int wheelDelta, double step)
    {
        var direction = wheelDelta < 0 ? 1 : -1;
        if (!CanScrollVertically(scrollViewer, direction))
        {
            return false;
        }

        var wheelTicks = Math.Max(1d, Math.Abs(wheelDelta) / 120d);
        var nextOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * step * wheelTicks,
            0d,
            scrollViewer.ScrollableHeight);
        if (Math.Abs(nextOffset - scrollViewer.VerticalOffset) <= 0.1d)
        {
            return false;
        }

        scrollViewer.ScrollToVerticalOffset(nextOffset);
        return true;
    }

    private static bool HasVerticalScrollableContent(ScrollViewer scrollViewer)
    {
        return scrollViewer.ScrollableHeight > double.Epsilon;
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int direction)
    {
        if (scrollViewer.ScrollableHeight <= double.Epsilon)
        {
            return false;
        }

        return direction > 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - double.Epsilon
            : scrollViewer.VerticalOffset > double.Epsilon;
    }

    private static bool IsWithinTextInput(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is TextBoxBase)
            {
                return true;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        return current switch
        {
            FrameworkElement element => element.Parent,
            FrameworkContentElement contentElement => contentElement.Parent,
            _ => null
        };
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
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
