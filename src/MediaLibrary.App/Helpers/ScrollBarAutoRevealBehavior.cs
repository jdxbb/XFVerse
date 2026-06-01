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
            new PropertyMetadata(false));

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
        viewer.SetValue(IsViewerSubscribedProperty, true);
    }

    private static void Detach(FrameworkElement element)
    {
        foreach (var viewer in FindVisualDescendants<ScrollViewer>(element, includeRoot: true))
        {
            viewer.ScrollChanged -= OnViewerScrollChanged;
            viewer.PreviewMouseWheel -= OnViewerPreviewMouseWheel;
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
            && (Math.Abs(e.VerticalChange) > double.Epsilon || Math.Abs(e.HorizontalChange) > double.Epsilon))
        {
            Reveal(viewer);
        }
    }

    private static void OnViewerPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            Reveal(viewer);
        }
    }

    private static void Reveal(ScrollViewer viewer)
    {
        foreach (var scrollBar in FindVisualDescendants<ScrollBar>(viewer, includeRoot: false))
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
                foreach (var scrollBar in FindVisualDescendants<ScrollBar>(viewer, includeRoot: false))
                {
                    SetIsRevealed(scrollBar, false);
                }
            };
            viewer.SetValue(RevealTimerProperty, timer);
        }

        timer.Stop();
        timer.Start();
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
