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
    private readonly Dictionary<ScrollBar, DispatcherTimer> _scrollBarHideTimers = [];
    private INotifyPropertyChanged? _shellPropertyChangedSource;

    public LibraryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachShellState();
        UpdateToolbarSearchWidth();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachShellState();
        foreach (var timer in _scrollBarHideTimers.Values)
        {
            timer.Stop();
        }

        _scrollBarHideTimers.Clear();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && !IsWithinTextInput(source))
        {
            Keyboard.ClearFocus();
        }
    }

    private void OpenButtonContextMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } contextMenu } button)
        {
            return;
        }

        contextMenu.PlacementTarget = button;
        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OpenDecadeFilterMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        var contextMenu = new ContextMenu();
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
        contextMenu.IsOpen = true;
        e.Handled = true;
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

    private void DetachShellState()
    {
        if (_shellPropertyChangedSource is not null)
        {
            _shellPropertyChangedSource.PropertyChanged -= OnShellPropertyChanged;
            _shellPropertyChangedSource = null;
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

    private void UpdateToolbarSearchWidth()
    {
        var isSidebarCollapsed = true;
        if (Window.GetWindow(this)?.DataContext is MainWindowViewModel shellViewModel)
        {
            isSidebarCollapsed = shellViewModel.IsSidebarCollapsed;
        }

        ToolbarSearchColumn.Width = new GridLength(
            isSidebarCollapsed ? CollapsedSearchColumnWidth : ExpandedSearchColumnWidth);
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
