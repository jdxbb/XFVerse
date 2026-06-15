using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace MediaLibrary.App.Helpers;

public static class ScrollBarAutoRevealBehavior
{
    private static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(900);

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollBarAutoRevealBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsRevealedProperty =
        DependencyProperty.RegisterAttached(
            "IsRevealed",
            typeof(bool),
            typeof(ScrollBarAutoRevealBehavior),
            new PropertyMetadata(false, OnIsRevealedChanged));

    private static readonly DependencyProperty RevealTimerProperty =
        DependencyProperty.RegisterAttached(
            "RevealTimer",
            typeof(DispatcherTimer),
            typeof(ScrollBarAutoRevealBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty IsViewerSubscribedProperty =
        DependencyProperty.RegisterAttached(
            "IsViewerSubscribed",
            typeof(bool),
            typeof(ScrollBarAutoRevealBehavior),
            new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    public static bool GetIsRevealed(DependencyObject target)
    {
        return (bool)target.GetValue(IsRevealedProperty);
    }

    public static void SetIsRevealed(DependencyObject target, bool value)
    {
        target.SetValue(IsRevealedProperty, value);
    }

    public static void Hide(ScrollViewer viewer)
    {
        SetIsRevealed(viewer, false);
        foreach (var scrollBar in FindOwnedScrollBars(viewer))
        {
            SetIsRevealed(scrollBar, false);
        }

        if (viewer.GetValue(RevealTimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
        }
    }

    private static void OnIsEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.Loaded += OnElementLoaded;
            element.Unloaded += OnElementUnloaded;
            if (element.IsLoaded)
            {
                BeginAttach(element);
            }
        }
        else
        {
            element.Loaded -= OnElementLoaded;
            element.Unloaded -= OnElementUnloaded;
            Detach(element);
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            BeginAttach(element);
        }
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Detach(element);
        }
    }

    private static void BeginAttach(FrameworkElement element)
    {
        _ = element.Dispatcher.BeginInvoke(
            () =>
            {
                foreach (var viewer in FindVisualDescendants<ScrollViewer>(element, includeRoot: true))
                {
                    AttachViewer(viewer);
                }
            },
            DispatcherPriority.Loaded);
    }

    private static void AttachViewer(ScrollViewer viewer)
    {
        if ((bool)viewer.GetValue(IsViewerSubscribedProperty))
        {
            return;
        }

        viewer.ScrollChanged += OnViewerScrollChanged;
        viewer.PreviewMouseWheel += OnViewerPreviewMouseWheel;
        viewer.MouseEnter += OnViewerMouseEnter;
        viewer.MouseLeave += OnViewerMouseLeave;
        viewer.SetValue(IsViewerSubscribedProperty, true);
    }

    private static void Detach(FrameworkElement element)
    {
        foreach (var viewer in FindVisualDescendants<ScrollViewer>(element, includeRoot: true))
        {
            viewer.ScrollChanged -= OnViewerScrollChanged;
            viewer.PreviewMouseWheel -= OnViewerPreviewMouseWheel;
            viewer.MouseEnter -= OnViewerMouseEnter;
            viewer.MouseLeave -= OnViewerMouseLeave;
            viewer.SetValue(IsViewerSubscribedProperty, false);
            if (viewer.GetValue(RevealTimerProperty) is DispatcherTimer timer)
            {
                timer.Stop();
                viewer.ClearValue(RevealTimerProperty);
            }
        }
    }

    private static void OnViewerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer
            && IsSelfScrollEvent(viewer, e.OriginalSource)
            && (Math.Abs(e.VerticalChange) > double.Epsilon || Math.Abs(e.HorizontalChange) > double.Epsilon))
        {
            Reveal(viewer);
        }
    }

    private static void OnViewerPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            if (!IsSelfScrollEvent(viewer, e.OriginalSource))
            {
                return;
            }

            var direction = e.Delta < 0 ? 1 : -1;
            if (CanScrollVertically(viewer, direction))
            {
                Reveal(viewer);
            }
        }
    }

    private static void OnViewerMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is ScrollViewer viewer && viewer.ScrollableHeight > double.Epsilon)
        {
            Reveal(viewer);
        }
    }

    private static void OnViewerMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            if (viewer.IsMouseCaptureWithin)
            {
                Reveal(viewer);
                return;
            }

            Hide(viewer);
        }
    }

    private static void Reveal(ScrollViewer viewer)
    {
        SetIsRevealed(viewer, true);
        foreach (var scrollBar in FindOwnedScrollBars(viewer))
        {
            SetIsRevealed(scrollBar, true);
        }

        var timer = viewer.GetValue(RevealTimerProperty) as DispatcherTimer;
        if (timer is null)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, viewer.Dispatcher)
            {
                Interval = RevealDuration
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (viewer.IsMouseOver || viewer.IsMouseCaptureWithin)
                {
                    return;
                }

                SetIsRevealed(viewer, false);
                foreach (var scrollBar in FindOwnedScrollBars(viewer))
                {
                    SetIsRevealed(scrollBar, false);
                }
            };
            viewer.SetValue(RevealTimerProperty, timer);
        }

        timer.Stop();
        timer.Start();
    }

    private static void OnIsRevealedChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is ScrollViewer viewer)
        {
            TextScrollOverflowCueBehavior.Refresh(viewer);
        }
    }

    private static bool CanScrollVertically(ScrollViewer viewer, int direction)
    {
        if (viewer.ScrollableHeight <= double.Epsilon)
        {
            return false;
        }

        return direction > 0
            ? viewer.VerticalOffset < viewer.ScrollableHeight - double.Epsilon
            : viewer.VerticalOffset > double.Epsilon;
    }

    private static bool IsSelfScrollEvent(ScrollViewer viewer, object originalSource)
    {
        if (originalSource is not DependencyObject source)
        {
            return true;
        }

        var sourceViewer = source is ScrollViewer scrollViewer
            ? scrollViewer
            : FindNearestAncestor<ScrollViewer>(source);
        return sourceViewer is null || ReferenceEquals(sourceViewer, viewer);
    }

    private static IEnumerable<ScrollBar> FindOwnedScrollBars(ScrollViewer viewer)
    {
        foreach (var scrollBar in FindVisualDescendants<ScrollBar>(viewer, includeRoot: false))
        {
            if (ReferenceEquals(FindNearestAncestor<ScrollViewer>(scrollBar), viewer))
            {
                yield return scrollBar;
            }
        }
    }

    private static T? FindNearestAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var current = GetParent(source); current is not null; current = GetParent(current))
        {
            if (current is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        return current is Visual
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root, bool includeRoot)
        where T : DependencyObject
    {
        if (includeRoot && root is T typedRoot)
        {
            yield return typedRoot;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualDescendants<T>(child, includeRoot: false))
            {
                yield return descendant;
            }
        }
    }
}
