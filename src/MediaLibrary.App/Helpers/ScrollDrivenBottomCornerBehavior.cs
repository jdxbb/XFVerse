using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MediaLibrary.App.Helpers;

public static class ScrollDrivenBottomCornerBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CollapsedBottomRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CollapsedBottomRadius",
            typeof(double),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(0d, OnCornerBehaviorPropertyChanged));

    public static readonly DependencyProperty AtBottomToleranceProperty =
        DependencyProperty.RegisterAttached(
            "AtBottomTolerance",
            typeof(double),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(0.5d, OnCornerBehaviorPropertyChanged));

    public static readonly DependencyProperty AtBottomBottomMarginProperty =
        DependencyProperty.RegisterAttached(
            "AtBottomBottomMargin",
            typeof(double),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(0d, OnCornerBehaviorPropertyChanged));

    private static readonly DependencyProperty OriginalCornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "OriginalCornerRadius",
            typeof(CornerRadius),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(default(CornerRadius)));

    private static readonly DependencyProperty HasOriginalCornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "HasOriginalCornerRadius",
            typeof(bool),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty OriginalMarginProperty =
        DependencyProperty.RegisterAttached(
            "OriginalMargin",
            typeof(Thickness),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(default(Thickness)));

    private static readonly DependencyProperty HasOriginalMarginProperty =
        DependencyProperty.RegisterAttached(
            "HasOriginalMargin",
            typeof(bool),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SubscribedScrollViewersProperty =
        DependencyProperty.RegisterAttached(
            "SubscribedScrollViewers",
            typeof(List<ScrollViewer>),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty IsRefreshQueuedProperty =
        DependencyProperty.RegisterAttached(
            "IsRefreshQueued",
            typeof(bool),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty DiscoveryRetryCountProperty =
        DependencyProperty.RegisterAttached(
            "DiscoveryRetryCount",
            typeof(int),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(0));

    private static readonly DependencyProperty BottomMarginAppliedOffsetProperty =
        DependencyProperty.RegisterAttached(
            "BottomMarginAppliedOffset",
            typeof(double),
            typeof(ScrollDrivenBottomCornerBehavior),
            new PropertyMetadata(double.NaN));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    public static double GetCollapsedBottomRadius(DependencyObject target)
    {
        return (double)target.GetValue(CollapsedBottomRadiusProperty);
    }

    public static void SetCollapsedBottomRadius(DependencyObject target, double value)
    {
        target.SetValue(CollapsedBottomRadiusProperty, value);
    }

    public static double GetAtBottomTolerance(DependencyObject target)
    {
        return (double)target.GetValue(AtBottomToleranceProperty);
    }

    public static void SetAtBottomTolerance(DependencyObject target, double value)
    {
        target.SetValue(AtBottomToleranceProperty, value);
    }

    public static double GetAtBottomBottomMargin(DependencyObject target)
    {
        return (double)target.GetValue(AtBottomBottomMarginProperty);
    }

    public static void SetAtBottomBottomMargin(DependencyObject target, double value)
    {
        target.SetValue(AtBottomBottomMarginProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Border border)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            border.Loaded += OnTargetLoaded;
            border.Unloaded += OnTargetUnloaded;
            if (border.IsLoaded)
            {
                AttachTarget(border);
            }
        }
        else
        {
            border.Loaded -= OnTargetLoaded;
            border.Unloaded -= OnTargetUnloaded;
            DetachTarget(border, restoreCornerRadius: true);
        }
    }

    private static void OnCornerBehaviorPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is Border border && GetIsEnabled(border))
        {
            QueueRefresh(border);
        }
    }

    private static void OnTargetLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border)
        {
            AttachTarget(border);
        }
    }

    private static void OnTargetUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border)
        {
            DetachTarget(border, restoreCornerRadius: false);
        }
    }

    private static void AttachTarget(Border border)
    {
        EnsureOriginalCornerRadius(border);
        EnsureOriginalMargin(border);
        border.SizeChanged -= OnTargetSizeChanged;
        border.SizeChanged += OnTargetSizeChanged;
        border.SetValue(DiscoveryRetryCountProperty, 0);
        Refresh(border);
        QueueRefresh(border);
    }

    private static void DetachTarget(Border border, bool restoreCornerRadius)
    {
        border.SizeChanged -= OnTargetSizeChanged;
        border.ClearValue(IsRefreshQueuedProperty);
        border.ClearValue(DiscoveryRetryCountProperty);
        border.ClearValue(BottomMarginAppliedOffsetProperty);

        foreach (var viewer in GetSubscribedScrollViewers(border))
        {
            viewer.ScrollChanged -= OnScrollViewerScrollChanged;
            viewer.IsVisibleChanged -= OnScrollViewerIsVisibleChanged;
        }

        border.ClearValue(SubscribedScrollViewersProperty);

        if (restoreCornerRadius && (bool)border.GetValue(HasOriginalCornerRadiusProperty))
        {
            border.CornerRadius = (CornerRadius)border.GetValue(OriginalCornerRadiusProperty);
        }

        if (restoreCornerRadius && (bool)border.GetValue(HasOriginalMarginProperty))
        {
            border.Margin = (Thickness)border.GetValue(OriginalMarginProperty);
        }
    }

    private static void OnTargetSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border)
        {
            QueueRefresh(border);
        }
    }

    private static void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            var target = FindEnabledOwnerBorder(viewer);
            if (target is not null)
            {
                QueueRefresh(target);
            }
        }
    }

    private static void OnScrollViewerIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            var target = FindEnabledOwnerBorder(viewer);
            if (target is not null)
            {
                QueueRefresh(target);
            }
        }
    }

    private static void QueueRefresh(Border border, DispatcherPriority priority = DispatcherPriority.Loaded)
    {
        if (!GetIsEnabled(border) || (bool)border.GetValue(IsRefreshQueuedProperty))
        {
            return;
        }

        border.SetValue(IsRefreshQueuedProperty, true);
        _ = border.Dispatcher.BeginInvoke(
            () =>
            {
                border.SetValue(IsRefreshQueuedProperty, false);
                if (GetIsEnabled(border) && border.IsLoaded)
                {
                    Refresh(border);
                }
            },
            priority);
    }

    private static void Refresh(Border border)
    {
        EnsureOriginalCornerRadius(border);
        EnsureOriginalMargin(border);
        DiscoverScrollViewers(border);
        UpdateTargetState(border);
        QueueDiscoveryRetryIfNeeded(border);
    }

    private static void EnsureOriginalCornerRadius(Border border)
    {
        if ((bool)border.GetValue(HasOriginalCornerRadiusProperty))
        {
            return;
        }

        border.SetValue(OriginalCornerRadiusProperty, border.CornerRadius);
        border.SetValue(HasOriginalCornerRadiusProperty, true);
    }

    private static void EnsureOriginalMargin(Border border)
    {
        if ((bool)border.GetValue(HasOriginalMarginProperty))
        {
            return;
        }

        border.SetValue(OriginalMarginProperty, border.Margin);
        border.SetValue(HasOriginalMarginProperty, true);
    }

    private static void DiscoverScrollViewers(Border border)
    {
        var subscribed = GetSubscribedScrollViewers(border);

        foreach (var viewer in FindVisualDescendants<ScrollViewer>(border))
        {
            if (subscribed.Contains(viewer))
            {
                continue;
            }

            viewer.ScrollChanged += OnScrollViewerScrollChanged;
            viewer.IsVisibleChanged += OnScrollViewerIsVisibleChanged;
            subscribed.Add(viewer);
        }

        for (var index = subscribed.Count - 1; index >= 0; index--)
        {
            var viewer = subscribed[index];
            if (IsDescendantOf(viewer, border))
            {
                continue;
            }

            viewer.ScrollChanged -= OnScrollViewerScrollChanged;
            viewer.IsVisibleChanged -= OnScrollViewerIsVisibleChanged;
            subscribed.RemoveAt(index);
        }
    }

    private static void QueueDiscoveryRetryIfNeeded(Border border)
    {
        if (GetSubscribedScrollViewers(border).Count > 0)
        {
            border.SetValue(DiscoveryRetryCountProperty, 0);
            return;
        }

        var retryCount = (int)border.GetValue(DiscoveryRetryCountProperty);
        if (retryCount >= 8)
        {
            return;
        }

        border.SetValue(DiscoveryRetryCountProperty, retryCount + 1);
        QueueRefresh(border, DispatcherPriority.ContextIdle);
    }

    private static void UpdateTargetState(Border border)
    {
        var restored = (CornerRadius)border.GetValue(OriginalCornerRadiusProperty);
        var originalMargin = (Thickness)border.GetValue(OriginalMarginProperty);
        var atBottomBottomMargin = Math.Max(0d, GetAtBottomBottomMargin(border));
        var currentBottomMargin = Math.Max(0d, border.Margin.Bottom - originalMargin.Bottom);
        var viewer = FindActiveScrollViewer(border);
        var shouldApplyBottomMargin = ShouldApplyBottomMargin(
            viewer,
            border,
            currentBottomMargin > double.Epsilon,
            atBottomBottomMargin);
        UpdateBottomMarginAppliedOffset(border, viewer, shouldApplyBottomMargin, currentBottomMargin > double.Epsilon);

        var bottomProgress = shouldApplyBottomMargin
            ? 1d
            : CalculateBottomProgress(viewer, border, currentBottomMargin);
        var collapsedBottomRadius = Math.Max(0d, GetCollapsedBottomRadius(border));
        var desiredCornerRadius = new CornerRadius(
            restored.TopLeft,
            restored.TopRight,
            Lerp(collapsedBottomRadius, restored.BottomRight, bottomProgress),
            Lerp(collapsedBottomRadius, restored.BottomLeft, bottomProgress));

        if (!CornerRadiusEquals(border.CornerRadius, desiredCornerRadius))
        {
            border.CornerRadius = desiredCornerRadius;
        }

        var desiredMargin = originalMargin;
        if (shouldApplyBottomMargin)
        {
            desiredMargin.Bottom = originalMargin.Bottom + atBottomBottomMargin;
        }

        if (!ThicknessEquals(border.Margin, desiredMargin))
        {
            border.Margin = desiredMargin;
        }
    }

    private static void UpdateBottomMarginAppliedOffset(
        Border border,
        ScrollViewer? viewer,
        bool shouldApplyBottomMargin,
        bool isCurrentlyApplied)
    {
        if (!shouldApplyBottomMargin)
        {
            border.SetValue(BottomMarginAppliedOffsetProperty, double.NaN);
            return;
        }

        var currentOffset = viewer?.VerticalOffset ?? 0d;
        var storedOffset = (double)border.GetValue(BottomMarginAppliedOffsetProperty);
        if (!isCurrentlyApplied || double.IsNaN(storedOffset) || currentOffset > storedOffset)
        {
            border.SetValue(BottomMarginAppliedOffsetProperty, currentOffset);
        }
    }

    private static ScrollViewer? FindActiveScrollViewer(Border border)
    {
        ScrollViewer? fallback = null;
        foreach (var viewer in GetSubscribedScrollViewers(border))
        {
            if (!viewer.IsVisible || viewer.ActualHeight <= 0 || viewer.ViewportHeight <= 0)
            {
                continue;
            }

            fallback ??= viewer;
            if (viewer.ScrollableHeight > GetAtBottomTolerance(border))
            {
                return viewer;
            }
        }

        return fallback;
    }

    private static double CalculateBottomProgress(ScrollViewer? viewer, Border border, double currentBottomMargin)
    {
        if (viewer is null)
        {
            return 1d;
        }

        var tolerance = Math.Max(0d, GetAtBottomTolerance(border));
        var usesLogicalScrollUnits = viewer.CanContentScroll;
        if (viewer.ScrollableHeight <= tolerance)
        {
            return 1d;
        }

        var rawDistanceToBottom = Math.Max(0d, viewer.ScrollableHeight - viewer.VerticalOffset);
        var distanceToBottom = usesLogicalScrollUnits
            ? rawDistanceToBottom
            : Math.Max(0d, rawDistanceToBottom - currentBottomMargin);
        if (distanceToBottom <= tolerance)
        {
            return 1d;
        }

        var transitionRange = usesLogicalScrollUnits ? 1d : 24d;
        return Math.Clamp(1d - ((distanceToBottom - tolerance) / transitionRange), 0d, 1d);
    }

    private static bool ShouldApplyBottomMargin(
        ScrollViewer? viewer,
        Border border,
        bool isCurrentlyApplied,
        double bottomMargin)
    {
        if (bottomMargin <= 0d)
        {
            return false;
        }

        if (viewer is null)
        {
            return true;
        }

        var tolerance = Math.Max(0d, GetAtBottomTolerance(border));
        if (viewer.ScrollableHeight <= tolerance)
        {
            return true;
        }

        var distanceToBottom = Math.Max(0d, viewer.ScrollableHeight - viewer.VerticalOffset);
        if (distanceToBottom <= tolerance)
        {
            return true;
        }

        if (!isCurrentlyApplied)
        {
            return false;
        }

        var appliedOffset = (double)border.GetValue(BottomMarginAppliedOffsetProperty);
        if (double.IsNaN(appliedOffset))
        {
            return true;
        }

        var releaseOffsetDelta = viewer.CanContentScroll ? 2d : 24d;
        return viewer.VerticalOffset >= appliedOffset - releaseOffsetDelta;
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }

    private static List<ScrollViewer> GetSubscribedScrollViewers(Border border)
    {
        if (border.GetValue(SubscribedScrollViewersProperty) is List<ScrollViewer> subscribed)
        {
            return subscribed;
        }

        subscribed = [];
        border.SetValue(SubscribedScrollViewersProperty, subscribed);
        return subscribed;
    }

    private static Border? FindEnabledOwnerBorder(DependencyObject source)
    {
        for (var current = GetParent(source); current is not null; current = GetParent(current))
        {
            if (current is Border border && GetIsEnabled(border))
            {
                return border;
            }
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        try
        {
            var visualParent = VisualTreeHelper.GetParent(current);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }
        catch (InvalidOperationException)
        {
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = GetVisualChildCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static int GetVisualChildCount(DependencyObject root)
    {
        try
        {
            return VisualTreeHelper.GetChildrenCount(root);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static bool CornerRadiusEquals(CornerRadius left, CornerRadius right)
    {
        return Math.Abs(left.TopLeft - right.TopLeft) < double.Epsilon
               && Math.Abs(left.TopRight - right.TopRight) < double.Epsilon
               && Math.Abs(left.BottomRight - right.BottomRight) < double.Epsilon
               && Math.Abs(left.BottomLeft - right.BottomLeft) < double.Epsilon;
    }

    private static bool ThicknessEquals(Thickness left, Thickness right)
    {
        return Math.Abs(left.Left - right.Left) < double.Epsilon
               && Math.Abs(left.Top - right.Top) < double.Epsilon
               && Math.Abs(left.Right - right.Right) < double.Epsilon
               && Math.Abs(left.Bottom - right.Bottom) < double.Epsilon;
    }
}
