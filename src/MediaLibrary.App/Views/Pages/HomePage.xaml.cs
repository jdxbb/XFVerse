using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Main;

namespace MediaLibrary.App.Views.Pages;

public partial class HomePage : UserControl
{
    private const double ExpandedAiPanelWidth = 360;
    private const double CollapsedAiPanelWidth = 400;
    private static readonly TimeSpan ReasonScrollBarAutoHideDelay = TimeSpan.FromMilliseconds(850);
    private readonly Dictionary<ScrollViewer, DispatcherTimer> _reasonScrollBarHideTimers = [];
    private MainWindowViewModel? _shellViewModel;

    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachShellViewModel();
        UpdateAiPanelWidth();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
            _shellViewModel = null;
        }

        foreach (var timer in _reasonScrollBarHideTimers.Values)
        {
            timer.Stop();
        }

        _reasonScrollBarHideTimers.Clear();
    }

    private void AttachShellViewModel()
    {
        if (Window.GetWindow(this)?.DataContext is not MainWindowViewModel shellViewModel
            || ReferenceEquals(_shellViewModel, shellViewModel))
        {
            return;
        }

        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        }

        _shellViewModel = shellViewModel;
        _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarCollapsed))
        {
            UpdateAiPanelWidth();
        }
    }

    private void UpdateAiPanelWidth()
    {
        AiPanelColumn.Width = new GridLength(
            _shellViewModel?.IsSidebarCollapsed == true
                ? CollapsedAiPanelWidth
                : ExpandedAiPanelWidth);
    }

    private void ReasonScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            HideReasonScrollBar(scrollViewer);
        }
    }

    private void ReasonScrollViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (_reasonScrollBarHideTimers.Remove(scrollViewer, out var timer))
        {
            timer.Stop();
        }
    }

    private void ReasonScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (Math.Abs(e.VerticalChange) < 0.1d)
        {
            return;
        }

        ShowReasonScrollBar(scrollViewer);
    }

    private void ShowReasonScrollBar(ScrollViewer scrollViewer)
    {
        if (FindVerticalScrollBar(scrollViewer) is not { } scrollBar)
        {
            return;
        }

        scrollBar.Opacity = scrollViewer.ScrollableHeight > 0 ? 1d : 0d;
        if (!_reasonScrollBarHideTimers.TryGetValue(scrollViewer, out var timer))
        {
            timer = new DispatcherTimer { Interval = ReasonScrollBarAutoHideDelay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                HideReasonScrollBar(scrollViewer);
            };
            _reasonScrollBarHideTimers[scrollViewer] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private static void HideReasonScrollBar(ScrollViewer scrollViewer)
    {
        if (FindVerticalScrollBar(scrollViewer) is { } scrollBar)
        {
            scrollBar.Opacity = 0d;
        }
    }

    private static ScrollBar? FindVerticalScrollBar(DependencyObject source)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(source); index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is ScrollBar { Orientation: Orientation.Vertical } scrollBar)
            {
                return scrollBar;
            }

            var nested = FindVerticalScrollBar(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
