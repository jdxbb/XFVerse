using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MediaLibrary.App.Helpers;

public static class TextScrollOverflowCueBehavior
{
    private const double DefaultCueClipHeight = 8d;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TextScrollOverflowCueBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty OriginalClipProperty =
        DependencyProperty.RegisterAttached(
            "OriginalClip",
            typeof(Geometry),
            typeof(TextScrollOverflowCueBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    public static void Refresh(ScrollViewer viewer)
    {
        if (!GetIsEnabled(viewer))
        {
            return;
        }

        ApplyCue(viewer);
    }

    private static void OnIsEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            viewer.Loaded += OnViewerLoaded;
            viewer.SizeChanged += OnViewerSizeChanged;
            viewer.ScrollChanged += OnViewerScrollChanged;
            viewer.LayoutUpdated += OnViewerLayoutUpdated;
            if (viewer.IsLoaded)
            {
                QueueRefresh(viewer);
            }
        }
        else
        {
            viewer.Loaded -= OnViewerLoaded;
            viewer.SizeChanged -= OnViewerSizeChanged;
            viewer.ScrollChanged -= OnViewerScrollChanged;
            viewer.LayoutUpdated -= OnViewerLayoutUpdated;
            RestoreOriginalClip(viewer);
        }
    }

    private static void OnViewerLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            QueueRefresh(viewer);
        }
    }

    private static void OnViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            QueueRefresh(viewer);
        }
    }

    private static void OnViewerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            ApplyCue(viewer);
        }
    }

    private static void OnViewerLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            ApplyCue(viewer);
        }
    }

    private static void QueueRefresh(ScrollViewer viewer)
    {
        _ = viewer.Dispatcher.BeginInvoke(
            () => ApplyCue(viewer),
            DispatcherPriority.Render);
    }

    private static void ApplyCue(ScrollViewer viewer)
    {
        if (viewer.Content is not UIElement content)
        {
            return;
        }

        if (content.ReadLocalValue(OriginalClipProperty) == DependencyProperty.UnsetValue)
        {
            content.SetValue(OriginalClipProperty, content.Clip);
        }

        var hasMoreBelow = viewer.ScrollableHeight > 0.5d
                           && viewer.VerticalOffset < viewer.ScrollableHeight - 0.5d;
        var cueVisibleHeight = ResolveCueVisibleHeight(content, viewer);
        var showCue = hasMoreBelow
                      && !ScrollBarAutoRevealBehavior.GetIsRevealed(viewer)
                      && cueVisibleHeight > 0d
                      && viewer.ViewportHeight > cueVisibleHeight;
        if (!showCue)
        {
            RestoreOriginalClip(viewer);
            return;
        }

        content.Clip = new RectangleGeometry(
            new Rect(
                0d,
                viewer.VerticalOffset,
                Math.Max(0d, viewer.ViewportWidth),
                cueVisibleHeight));
    }

    private static double ResolveCueVisibleHeight(UIElement content, ScrollViewer viewer)
    {
        if (content is not TextBlock textBlock)
        {
            return Math.Max(0d, viewer.ViewportHeight - DefaultCueClipHeight);
        }

        var lineHeight = double.IsNaN(textBlock.LineHeight)
            ? textBlock.FontFamily.LineSpacing * textBlock.FontSize
            : textBlock.LineHeight;
        var halfLineHeight = Math.Max(1d, lineHeight * 0.5d);
        var visibleBottom = viewer.VerticalOffset + viewer.ViewportHeight;
        var alignedVisibleBottom =
            Math.Floor((visibleBottom - halfLineHeight) / lineHeight) * lineHeight + halfLineHeight;
        return Math.Max(0d, alignedVisibleBottom - viewer.VerticalOffset);
    }

    private static void RestoreOriginalClip(ScrollViewer viewer)
    {
        if (viewer.Content is not UIElement content
            || content.ReadLocalValue(OriginalClipProperty) == DependencyProperty.UnsetValue)
        {
            return;
        }

        content.Clip = (Geometry?)content.GetValue(OriginalClipProperty);
    }
}
