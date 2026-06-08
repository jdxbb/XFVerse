using System.Windows;
using System.Windows.Controls;

namespace MediaLibrary.App.Controls;

public sealed class EqualSpacingWrapPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(EqualSpacingWrapPanel),
            new FrameworkPropertyMetadata(194d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(double),
            typeof(EqualSpacingWrapPanel),
            new FrameworkPropertyMetadata(288d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemContentWidthProperty =
        DependencyProperty.Register(
            nameof(ItemContentWidth),
            typeof(double),
            typeof(EqualSpacingWrapPanel),
            new FrameworkPropertyMetadata(180d, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ViewportPaddingProperty =
        DependencyProperty.Register(
            nameof(ViewportPadding),
            typeof(Thickness),
            typeof(EqualSpacingWrapPanel),
            new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double ItemContentWidth
    {
        get => (double)GetValue(ItemContentWidthProperty);
        set => SetValue(ItemContentWidthProperty, value);
    }

    public Thickness ViewportPadding
    {
        get => (Thickness)GetValue(ViewportPaddingProperty);
        set => SetValue(ViewportPaddingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemWidth = Math.Max(1d, ItemWidth);
        var itemHeight = Math.Max(1d, ItemHeight);
        var viewportWidth = ResolveViewportWidth(availableSize.Width, InternalChildren.Count, itemWidth);
        var viewportHeight = ResolveViewportHeight(availableSize.Height);
        var padding = NormalizePadding(ViewportPadding, viewportWidth, viewportHeight);
        var columns = CalculateColumns(viewportWidth, padding, itemWidth, InternalChildren.Count);
        var rowCount = CalculateRowCount(columns, InternalChildren.Count);
        var childMeasureSize = new Size(itemWidth, itemHeight);

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childMeasureSize);
        }

        return new Size(
            viewportWidth,
            padding.Top + (rowCount * itemHeight) + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemWidth = Math.Max(1d, ItemWidth);
        var itemHeight = Math.Max(1d, ItemHeight);
        var viewportWidth = ResolveViewportWidth(finalSize.Width, InternalChildren.Count, itemWidth);
        var viewportHeight = ResolveViewportHeight(finalSize.Height);
        var padding = NormalizePadding(ViewportPadding, viewportWidth, viewportHeight);
        var columns = CalculateColumns(viewportWidth, padding, itemWidth, InternalChildren.Count);
        var arrangedLeft = CalculateArrangedLeft(viewportWidth, columns, itemWidth, ItemContentWidth);

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            InternalChildren[index].Arrange(
                new Rect(
                    arrangedLeft + (column * itemWidth),
                    padding.Top + (row * itemHeight),
                    itemWidth,
                    itemHeight));
        }

        return finalSize;
    }

    private static int CalculateColumns(double viewportWidth, Thickness padding, double itemWidth, int itemCount)
    {
        if (itemCount <= 0)
        {
            return 1;
        }

        var contentWidth = Math.Max(1d, viewportWidth - padding.Right);
        return Math.Max(1, (int)Math.Floor(contentWidth / itemWidth));
    }

    private static int CalculateRowCount(int columns, int itemCount)
    {
        return itemCount <= 0 ? 0 : (int)Math.Ceiling(itemCount / (double)Math.Max(1, columns));
    }

    private static double CalculateArrangedLeft(double viewportWidth, int columns, double itemWidth, double itemContentWidth)
    {
        var visibleItemWidth = itemContentWidth > 0d ? Math.Min(itemWidth, itemContentWidth) : itemWidth;
        var usedWidth = Math.Max(1d, ((Math.Max(1, columns) - 1) * itemWidth) + visibleItemWidth);
        return Math.Max(0d, (viewportWidth - usedWidth) * 0.5d);
    }

    private static double ResolveViewportWidth(double value, int itemCount, double itemWidth)
    {
        if (!double.IsInfinity(value) && !double.IsNaN(value) && value > 0d)
        {
            return value;
        }

        return Math.Max(itemWidth, itemCount * itemWidth);
    }

    private static double ResolveViewportHeight(double value)
    {
        return double.IsInfinity(value) || double.IsNaN(value) || value <= 0d ? 1d : value;
    }

    private static Thickness NormalizePadding(Thickness padding, double viewportWidth, double viewportHeight)
    {
        return new Thickness(
            ClampPadding(padding.Left, viewportWidth),
            ClampPadding(padding.Top, viewportHeight),
            ClampPadding(padding.Right, viewportWidth),
            ClampPadding(padding.Bottom, viewportHeight));
    }

    private static double ClampPadding(double value, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
        {
            return 0d;
        }

        return Math.Min(value, Math.Max(0d, maximum - 1d));
    }
}
