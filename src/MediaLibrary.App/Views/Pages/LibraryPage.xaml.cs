using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Main;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class LibraryPage : UserControl
{
    private const double CollapsedSearchColumnWidth = 816;
    private const double ExpandedSearchColumnWidth = 660;
    private static readonly TimeSpan ScrollBarAutoHideDelay = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan MenuReopenSuppressionDelay = TimeSpan.FromMilliseconds(350);
    private readonly Dictionary<ScrollBar, DispatcherTimer> _scrollBarHideTimers = [];
    private Button? _recentlyClosedMenuButton;
    private DateTime _recentlyClosedMenuAtUtc = DateTime.MinValue;
    private Button? _openMenuButton;
    private ContextMenu? _openContextMenu;
    private INotifyPropertyChanged? _shellPropertyChangedSource;
    private INotifyPropertyChanged? _libraryPropertyChangedSource;

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
            return;
        }

        if (!ShouldSuppressMenuOpen(button))
        {
            return;
        }

        e.Handled = true;
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

        if (ShouldSuppressMenuOpen(button))
        {
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

        if (ShouldSuppressMenuOpen(button))
        {
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
            contextMenu.Items.Add(new MenuItem
            {
                Header = option,
                Command = viewModel.SelectDecadeFilterCommand,
                CommandParameter = option,
                Style = (Style)FindResource("LibraryFilterMenuItemStyle")
            });
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
        if (sender is not ContextMenu { PlacementTarget: Button button })
        {
            return;
        }

        _recentlyClosedMenuButton = button;
        _recentlyClosedMenuAtUtc = DateTime.UtcNow;
        if (ReferenceEquals(_openContextMenu, sender))
        {
            _openMenuButton = null;
            _openContextMenu = null;
        }
    }

    private bool ShouldSuppressMenuOpen(Button button)
    {
        if (!ReferenceEquals(_recentlyClosedMenuButton, button))
        {
            return false;
        }

        if (DateTime.UtcNow - _recentlyClosedMenuAtUtc > MenuReopenSuppressionDelay)
        {
            _recentlyClosedMenuButton = null;
            return false;
        }

        _recentlyClosedMenuButton = null;
        return true;
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

    private void OnLibraryListScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not DependencyObject source)
        {
            return;
        }

        foreach (var scrollBar in FindVisualChildren<ScrollBar>(source))
        {
            if (scrollBar.Orientation != Orientation.Vertical)
            {
                continue;
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
            _libraryPropertyChangedSource.PropertyChanged -= OnLibraryPropertyChanged;
            _libraryPropertyChangedSource = null;
        }
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
