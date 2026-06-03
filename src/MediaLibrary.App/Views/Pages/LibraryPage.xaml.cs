using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Main;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.App.Views.Pages;

public partial class LibraryPage : UserControl
{
    private const double CollapsedSearchColumnWidth = 816;
    private const double ExpandedSearchColumnWidth = 660;
    private const double RemovedLibraryMouseWheelScrollStep = 56d;
    private static readonly bool ScrollDiagnosticsEnabled =
        string.Equals(
            Environment.GetEnvironmentVariable("XFVERSE_LIBRARY_SCROLL_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
    private const int ScrollDiagnosticsSampleInterval = 40;
    private static readonly TimeSpan ScrollBarAutoHideDelay = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan ScrollDiagnosticsMinimumInterval = TimeSpan.FromMilliseconds(900);
    private readonly Dictionary<ScrollBar, DispatcherTimer> _scrollBarHideTimers = [];
    private readonly Dictionary<DependencyObject, ScrollBar> _verticalScrollBarsByScrollSource = [];
    private Button? _openMenuButton;
    private ContextMenu? _openContextMenu;
    private INotifyPropertyChanged? _shellPropertyChangedSource;
    private INotifyPropertyChanged? _libraryPropertyChangedSource;
    private int _scrollDiagnosticsEventCount;
    private long _scrollDiagnosticsTotalTicks;
    private long _scrollDiagnosticsMaxTicks;
    private long _lastScrollDiagnosticsTimestamp;

    public LibraryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachLibraryState();
        AttachShellState();
        UpdateToolbarSearchWidth();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseRemovedLibraryPanelIfOpen();
        DetachLibraryState();
        DetachShellState();
        foreach (var timer in _scrollBarHideTimers.Values)
        {
            timer.Stop();
        }

        _scrollBarHideTimers.Clear();
        _verticalScrollBarsByScrollSource.Clear();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            CloseRemovedLibraryPanelIfOpen();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachLibraryState();
        AttachLibraryState();
        UpdateToolbarSearchWidth();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && !IsWithinTextInput(source))
        {
            Keyboard.ClearFocus();
        }
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

        CloseOpenMenu();
        contextMenu.PlacementTarget = button;
        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.Closed -= ContextMenu_Closed;
        contextMenu.Closed += ContextMenu_Closed;
        _openMenuButton = button;
        _openContextMenu = contextMenu;
        contextMenu.IsOpen = true;
        AlignContextMenuToButtonCenter(button, contextMenu);
        ConfigureSubmenusToOpenRight(contextMenu);
        e.Handled = true;
    }

    private void OpenDecadeFilterMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }

        CloseOpenMenu();
        var contextMenu = new ContextMenu
        {
            Style = (Style)FindResource("LibraryFilterContextMenuStyle")
        };
        foreach (var option in viewModel.DecadeFilterOptions)
        {
            var menuItem = new MenuItem
            {
                Header = option,
                IsCheckable = true,
                IsChecked = viewModel.IsDecadeFilterSelected(option),
                StaysOpenOnClick = !string.Equals(option, "全部年代", StringComparison.Ordinal),
                Command = viewModel.SelectDecadeFilterCommand,
                CommandParameter = option,
                Style = (Style)FindResource("LibraryFilterMenuItemStyle")
            };
            menuItem.Click += (_, _) =>
            {
                _ = Dispatcher.BeginInvoke(
                    () => UpdateDecadeFilterMenuChecks(contextMenu, viewModel),
                    DispatcherPriority.Background);
            };
            contextMenu.Items.Add(menuItem);
        }

        button.ContextMenu = contextMenu;
        contextMenu.PlacementTarget = button;
        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.Closed -= ContextMenu_Closed;
        contextMenu.Closed += ContextMenu_Closed;
        _openMenuButton = button;
        _openContextMenu = contextMenu;
        contextMenu.IsOpen = true;
        AlignContextMenuToButtonCenter(button, contextMenu);
        e.Handled = true;
    }

    private void ContextMenu_Closed(object? sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ContextMenu contextMenu)
        {
            return;
        }

        if (ReferenceEquals(_openContextMenu, contextMenu))
        {
            _openMenuButton = null;
            _openContextMenu = null;
        }
    }

    private bool IsOpenMenuButton(Button button)
    {
        return ReferenceEquals(_openMenuButton, button)
               && _openContextMenu is not null;
    }

    private void CloseOpenMenu()
    {
        if (_openContextMenu is not null)
        {
            _openContextMenu.IsOpen = false;
        }

        _openMenuButton = null;
        _openContextMenu = null;
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

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        if (viewModel.ApplySearchCommand.CanExecute(null))
        {
            viewModel.ApplySearchCommand.Execute(null);
        }

        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
    }

    private void RemovedLibraryListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject source)
        {
            return;
        }

        var scrollViewer = FindVisualChildren<ScrollViewer>(source).FirstOrDefault();
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var direction = e.Delta > 0 ? -1d : 1d;
        var wheelTicks = Math.Max(1d, Math.Abs(e.Delta) / 120d);
        var targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * RemovedLibraryMouseWheelScrollStep * wheelTicks,
            0d,
            scrollViewer.ScrollableHeight);
        if (Math.Abs(targetOffset - scrollViewer.VerticalOffset) <= double.Epsilon)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private void OnLibraryListScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var startedAt = ScrollDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        try
        {
            if (sender is not DependencyObject source)
            {
                return;
            }

            var scrollBar = GetOrCacheVerticalScrollBar(source);
            if (scrollBar is null)
            {
                return;
            }

            scrollBar.SetCurrentValue(OpacityProperty, 1d);
            if (!_scrollBarHideTimers.TryGetValue(scrollBar, out var timer))
            {
                timer = new DispatcherTimer { Interval = ScrollBarAutoHideDelay };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (!scrollBar.IsMouseOver)
                    {
                        scrollBar.ClearValue(OpacityProperty);
                    }
                };
                _scrollBarHideTimers[scrollBar] = timer;
            }

            timer.Stop();
            timer.Start();
        }
        finally
        {
            if (ScrollDiagnosticsEnabled)
            {
                RecordScrollDiagnostics(sender, e, Stopwatch.GetElapsedTime(startedAt));
            }
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

    private static void UpdateDecadeFilterMenuChecks(ContextMenu contextMenu, LibraryViewModel viewModel)
    {
        if (!contextMenu.IsOpen)
        {
            return;
        }

        foreach (var item in contextMenu.Items.OfType<MenuItem>())
        {
            if (item.Header is string option)
            {
                item.IsChecked = viewModel.IsDecadeFilterSelected(option);
            }
        }
    }

    private ScrollBar? GetOrCacheVerticalScrollBar(DependencyObject source)
    {
        if (_verticalScrollBarsByScrollSource.TryGetValue(source, out var cachedScrollBar)
            && cachedScrollBar.IsLoaded)
        {
            return cachedScrollBar;
        }

        foreach (var scrollBar in FindVisualChildren<ScrollBar>(source))
        {
            if (scrollBar.Orientation != Orientation.Vertical)
            {
                continue;
            }

            _verticalScrollBarsByScrollSource[source] = scrollBar;
            return scrollBar;
        }

        _verticalScrollBarsByScrollSource.Remove(source);
        return null;
    }

    private void RecordScrollDiagnostics(object sender, ScrollChangedEventArgs e, TimeSpan elapsed)
    {
        if (!ScrollDiagnosticsEnabled)
        {
            return;
        }

        _scrollDiagnosticsEventCount++;
        _scrollDiagnosticsTotalTicks += elapsed.Ticks;
        _scrollDiagnosticsMaxTicks = Math.Max(_scrollDiagnosticsMaxTicks, elapsed.Ticks);

        var now = Stopwatch.GetTimestamp();
        var isSlow = elapsed.TotalMilliseconds >= 8d;
        if (!isSlow
            && (_scrollDiagnosticsEventCount < ScrollDiagnosticsSampleInterval
                || (_lastScrollDiagnosticsTimestamp > 0
                    && Stopwatch.GetElapsedTime(_lastScrollDiagnosticsTimestamp, now) < ScrollDiagnosticsMinimumInterval)))
        {
            return;
        }

        var sampleCount = Math.Max(1, _scrollDiagnosticsEventCount);
        var averageMs = TimeSpan.FromTicks(_scrollDiagnosticsTotalTicks / sampleCount).TotalMilliseconds;
        var maxMs = TimeSpan.FromTicks(_scrollDiagnosticsMaxTicks).TotalMilliseconds;
        var itemCount = sender is ListBox listBox ? listBox.Items.Count : 0;

        AiPerfDiagnostics.WriteEvent(
            "event=library-scroll-handler " +
            $"elapsedMs={elapsed.TotalMilliseconds:0} avgMs={averageMs:0} maxMs={maxMs:0} samples={sampleCount} " +
            $"items={itemCount} verticalOffset={e.VerticalOffset:0} verticalChange={e.VerticalChange:0} " +
            $"viewportHeight={e.ViewportHeight:0} extentHeight={e.ExtentHeight:0} slow={isSlow.ToString().ToLowerInvariant()}");

        _scrollDiagnosticsEventCount = 0;
        _scrollDiagnosticsTotalTicks = 0;
        _scrollDiagnosticsMaxTicks = 0;
        _lastScrollDiagnosticsTimestamp = now;
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

    private void AttachLibraryState()
    {
        if (_libraryPropertyChangedSource is not null)
        {
            return;
        }

        if (DataContext is INotifyPropertyChanged source)
        {
            _libraryPropertyChangedSource = source;
            source.PropertyChanged += OnLibraryPropertyChanged;
            if (source is LibraryViewModel viewModel)
            {
                viewModel.RequestCloseFilterMenu += OnLibraryRequestCloseFilterMenu;
            }
        }
    }

    private void DetachShellState()
    {
        if (_shellPropertyChangedSource is not null)
        {
            _shellPropertyChangedSource.PropertyChanged -= OnShellPropertyChanged;
            _shellPropertyChangedSource = null;
        }
    }

    private void DetachLibraryState()
    {
        if (_libraryPropertyChangedSource is not null)
        {
            if (_libraryPropertyChangedSource is LibraryViewModel viewModel)
            {
                viewModel.RequestCloseFilterMenu -= OnLibraryRequestCloseFilterMenu;
            }

            _libraryPropertyChangedSource.PropertyChanged -= OnLibraryPropertyChanged;
            _libraryPropertyChangedSource = null;
        }
    }

    private void OnLibraryRequestCloseFilterMenu(object? sender, EventArgs e)
    {
        CloseOpenMenu();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsSidebarCollapsed)
            or nameof(MainWindowViewModel.IsSidebarExpanded)
            or nameof(MainWindowViewModel.SidebarColumnWidth))
        {
            UpdateToolbarSearchWidth();
        }
    }

    private void OnLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryViewModel.IsBatchSelectionMode))
        {
            UpdateToolbarSearchWidth();
        }
    }

    private void UpdateToolbarSearchWidth()
    {
        var isSidebarCollapsed = true;
        if (Window.GetWindow(this)?.DataContext is MainWindowViewModel shellViewModel)
        {
            isSidebarCollapsed = shellViewModel.IsSidebarCollapsed;
        }

        ToolbarSearchColumn.Width = new GridLength(
            isSidebarCollapsed ? CollapsedSearchColumnWidth : ExpandedSearchColumnWidth);
        UpdateToolbarSecondRowLayout(isSidebarCollapsed);
    }

    private void UpdateToolbarSecondRowLayout(bool isSidebarCollapsed)
    {
        var showBatchEntry = isSidebarCollapsed
            || DataContext is LibraryViewModel { IsBatchSelectionMode: true };
        var buttons = showBatchEntry
            ? new Button[]
            {
                PlaybackSourceFilterButton,
                MediaSourceFilterButton,
                ContentTypeFilterButton,
                TagFilterButton,
                DecadeFilterButton,
                CollectionStatusFilterButton,
                WatchedStatusFilterButton,
                BatchSelectionToggleButton
            }
            : new Button[]
            {
                PlaybackSourceFilterButton,
                MediaSourceFilterButton,
                ContentTypeFilterButton,
                TagFilterButton,
                DecadeFilterButton,
                CollectionStatusFilterButton,
                WatchedStatusFilterButton
            };

        ToolbarSecondRowGrid.ColumnDefinitions.Clear();
        for (var index = 0; index < buttons.Length; index++)
        {
            ToolbarSecondRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (index < buttons.Length - 1)
            {
                ToolbarSecondRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            Grid.SetColumn(buttons[index], index * 2);
        }

        if (!showBatchEntry)
        {
            Grid.SetColumn(BatchSelectionToggleButton, 0);
        }
    }

    private void CloseRemovedLibraryPanelIfOpen()
    {
        if (DataContext is not LibraryViewModel { IsRemovedLibraryPanelOpen: true } viewModel)
        {
            return;
        }

        if (viewModel.CloseRemovedLibraryCommand.CanExecute(null))
        {
            viewModel.CloseRemovedLibraryCommand.Execute(null);
        }
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

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject source)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(source);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
