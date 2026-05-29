using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.App.Controls;

public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const double LineScrollDelta = 48d;
    private const double MouseWheelScrollDelta = 96d;
    private static readonly bool DiagnosticsEnabled =
        string.Equals(
            Environment.GetEnvironmentVariable("XFVERSE_LIBRARY_LAYOUT_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
    private static readonly TimeSpan DiagnosticsMinimumInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LayoutDiagnosticsMinimumInterval = TimeSpan.FromMilliseconds(700);

    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(236d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(560d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemContentWidthProperty =
        DependencyProperty.Register(
            nameof(ItemContentWidth),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty OverscanRowsProperty =
        DependencyProperty.Register(
            nameof(OverscanRows),
            typeof(int),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ViewportPaddingProperty =
        DependencyProperty.Register(
            nameof(ViewportPadding),
            typeof(Thickness),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsMeasure));

    private Size _extent;
    private Size _viewport;
    private Point _offset;
    private double _effectiveItemHeight = 560d;
    private int _realizedFirstIndex = -1;
    private int _realizedLastIndex = -1;
    private int _realizedColumns = 1;
    private Size _lastChildMeasureSize;
    private string? _lastDiagnosticsSignature;
    private long _lastDiagnosticsTimestamp;
    private long _lastLayoutDiagnosticsTimestamp;

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

    public int OverscanRows
    {
        get => (int)GetValue(OverscanRowsProperty);
        set => SetValue(OverscanRowsProperty, value);
    }

    public Thickness ViewportPadding
    {
        get => (Thickness)GetValue(ViewportPaddingProperty);
        set => SetValue(ViewportPaddingProperty, value);
    }

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentWidth => _extent.Width;

    public double ExtentHeight => _extent.Height;

    public double ViewportWidth => _viewport.Width;

    public double ViewportHeight => _viewport.Height;

    public double HorizontalOffset => _offset.X;

    public double VerticalOffset => _offset.Y;

    public ScrollViewer? ScrollOwner { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var startedAt = DiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var itemsControl = ItemsControl.GetItemsOwner(this);
        var itemCount = itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;
        var viewportWidth = NormalizeViewportLength(availableSize.Width);
        var viewportHeight = NormalizeViewportLength(availableSize.Height);
        var itemWidth = Math.Max(1d, ItemWidth);
        var minimumItemHeight = Math.Max(1d, ItemHeight);
        var padding = NormalizePadding(ViewportPadding, viewportWidth, viewportHeight);
        var contentWidth = CalculateLayoutWidth(viewportWidth, padding);
        _effectiveItemHeight = minimumItemHeight;

        var columns = Math.Max(1, (int)Math.Floor(contentWidth / itemWidth));
        var rowCount = itemCount == 0 ? 0 : (int)Math.Ceiling(itemCount / (double)columns);
        UpdateScrollInfo(
            new Size(viewportWidth, viewportHeight),
            new Size(viewportWidth, padding.Top + (rowCount * _effectiveItemHeight) + padding.Bottom));

        var firstVisibleRow = rowCount == 0
            ? 0
            : Math.Max(0, (int)Math.Floor((VerticalOffset - padding.Top) / _effectiveItemHeight));
        var lastVisibleRow = rowCount == 0
            ? -1
            : Math.Min(rowCount - 1, (int)Math.Floor((VerticalOffset + ViewportHeight - padding.Top) / _effectiveItemHeight));
        var overscanRows = Math.Max(0, OverscanRows);
        var firstRow = Math.Max(0, firstVisibleRow - overscanRows);
        var lastRow = lastVisibleRow < 0 ? -1 : Math.Min(rowCount - 1, lastVisibleRow + overscanRows);
        var firstIndex = lastRow < firstRow ? 0 : firstRow * columns;
        var lastIndex = lastRow < firstRow ? -1 : Math.Min(itemCount - 1, ((lastRow + 1) * columns) - 1);
        var childMeasureSize = new Size(itemWidth, minimumItemHeight);
        var forceMeasureChildren = !AreClose(_lastChildMeasureSize, childMeasureSize);

        CleanUpItems(firstIndex, lastIndex);
        RealizeItems(firstIndex, lastIndex, childMeasureSize, forceMeasureChildren);
        _realizedFirstIndex = firstIndex;
        _realizedLastIndex = lastIndex;
        _realizedColumns = columns;
        _lastChildMeasureSize = childMeasureSize;

        if (DiagnosticsEnabled)
        {
            WriteVirtualizationDiagnostics(itemCount, InternalChildren.Count, columns, firstVisibleRow, lastVisibleRow);
            WriteLayoutPerfDiagnostics(
                "measure",
                Stopwatch.GetElapsedTime(startedAt),
                itemCount,
                InternalChildren.Count,
                columns,
                viewportWidth,
                viewportHeight);
        }

        return new Size(viewportWidth, viewportHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var startedAt = DiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var itemWidth = Math.Max(1d, ItemWidth);
        var viewportWidth = NormalizeViewportLength(finalSize.Width);
        var viewportHeight = NormalizeViewportLength(finalSize.Height);
        var padding = NormalizePadding(ViewportPadding, viewportWidth, viewportHeight);
        var contentWidth = CalculateLayoutWidth(viewportWidth, padding);
        var columns = Math.Max(1, (int)Math.Floor(contentWidth / itemWidth));
        var arrangedLeft = CalculateArrangedLeft(viewportWidth, columns, itemWidth, ItemContentWidth);
        UpdateScrollInfo(
            new Size(viewportWidth, viewportHeight),
            new Size(viewportWidth, padding.Top + (CalculateRowCount(columns) * _effectiveItemHeight) + padding.Bottom));

        var generator = ItemContainerGenerator;
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            var child = InternalChildren[childIndex];
            var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0));
            if (itemIndex < 0)
            {
                continue;
            }

            var row = itemIndex / columns;
            var column = itemIndex % columns;
            child.Arrange(new Rect(
                arrangedLeft + (column * itemWidth),
                padding.Top + (row * _effectiveItemHeight) - VerticalOffset,
                itemWidth,
                _effectiveItemHeight));
        }

        if (DiagnosticsEnabled)
        {
            var itemsControl = ItemsControl.GetItemsOwner(this);
            var itemCount = itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;
            WriteLayoutPerfDiagnostics(
                "arrange",
                Stopwatch.GetElapsedTime(startedAt),
                itemCount,
                InternalChildren.Count,
                columns,
                viewportWidth,
                viewportHeight);
        }

        return finalSize;
    }

    public void LineUp()
    {
        SetVerticalOffset(VerticalOffset - LineScrollDelta);
    }

    public void LineDown()
    {
        SetVerticalOffset(VerticalOffset + LineScrollDelta);
    }

    public void LineLeft()
    {
        SetHorizontalOffset(HorizontalOffset - 16d);
    }

    public void LineRight()
    {
        SetHorizontalOffset(HorizontalOffset + 16d);
    }

    public void PageUp()
    {
        SetVerticalOffset(VerticalOffset - ViewportHeight);
    }

    public void PageDown()
    {
        SetVerticalOffset(VerticalOffset + ViewportHeight);
    }

    public void PageLeft()
    {
        SetHorizontalOffset(HorizontalOffset - ViewportWidth);
    }

    public void PageRight()
    {
        SetHorizontalOffset(HorizontalOffset + ViewportWidth);
    }

    public void MouseWheelUp()
    {
        SetVerticalOffset(VerticalOffset - MouseWheelScrollDelta);
    }

    public void MouseWheelDown()
    {
        SetVerticalOffset(VerticalOffset + MouseWheelScrollDelta);
    }

    public void MouseWheelLeft()
    {
        SetHorizontalOffset(HorizontalOffset - 48d);
    }

    public void MouseWheelRight()
    {
        SetHorizontalOffset(HorizontalOffset + 48d);
    }

    public void SetHorizontalOffset(double offset)
    {
        var clamped = Clamp(offset, 0d, Math.Max(0d, ExtentWidth - ViewportWidth));
        if (Math.Abs(clamped - _offset.X) < 0.1d)
        {
            return;
        }

        _offset.X = clamped;
        InvalidateMeasure();
        ScrollOwner?.InvalidateScrollInfo();
    }

    public void SetVerticalOffset(double offset)
    {
        var clamped = Clamp(offset, 0d, Math.Max(0d, ExtentHeight - ViewportHeight));
        if (Math.Abs(clamped - _offset.Y) < 0.1d)
        {
            return;
        }

        _offset.Y = clamped;
        if (CanArrangeOnlyForVerticalOffset(clamped))
        {
            InvalidateArrange();
        }
        else
        {
            InvalidateMeasure();
        }

        ScrollOwner?.InvalidateScrollInfo();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
        {
            if (!ReferenceEquals(InternalChildren[childIndex], visual))
            {
                continue;
            }

            var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0));
            if (itemIndex < 0)
            {
                return Rect.Empty;
            }

            var padding = NormalizePadding(ViewportPadding, ViewportWidth, ViewportHeight);
            var contentWidth = CalculateLayoutWidth(ViewportWidth, padding);
            var columns = Math.Max(1, (int)Math.Floor(contentWidth / Math.Max(1d, ItemWidth)));
            var row = itemIndex / columns;
            var itemTop = padding.Top + (row * _effectiveItemHeight);
            var itemBottom = itemTop + _effectiveItemHeight;
            if (itemTop < VerticalOffset)
            {
                SetVerticalOffset(itemTop);
            }
            else if (itemBottom > VerticalOffset + ViewportHeight)
            {
                SetVerticalOffset(itemBottom - ViewportHeight);
            }

            return new Rect(0d, itemTop, ItemWidth, _effectiveItemHeight);
        }

        return Rect.Empty;
    }

    private void RealizeItems(int firstIndex, int lastIndex, Size childMeasureSize, bool forceMeasureChildren)
    {
        if (lastIndex < firstIndex)
        {
            _effectiveItemHeight = Math.Max(_effectiveItemHeight, childMeasureSize.Height);
            return;
        }

        var generator = ItemContainerGenerator;
        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
        using var generatorState = generator.StartAt(startPosition, GeneratorDirection.Forward, true);

        for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
        {
            var child = generator.GenerateNext(out var newlyRealized) as UIElement;
            if (child is null)
            {
                continue;
            }

            if (newlyRealized)
            {
                if (childIndex >= InternalChildren.Count)
                {
                    AddInternalChild(child);
                }
                else
                {
                    InsertInternalChild(childIndex, child);
                }

                generator.PrepareItemContainer(child);
            }

            if (newlyRealized || forceMeasureChildren || !child.IsMeasureValid)
            {
                child.Measure(childMeasureSize);
            }

            _effectiveItemHeight = Math.Max(_effectiveItemHeight, Math.Max(childMeasureSize.Height, child.DesiredSize.Height));
        }
    }

    private void CleanUpItems(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0));
            if (itemIndex >= firstIndex && itemIndex <= lastIndex)
            {
                continue;
            }

            generator.Remove(new GeneratorPosition(childIndex, 0), 1);
            RemoveInternalChildRange(childIndex, 1);
        }
    }

    private int CalculateRowCount(int columns)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        var itemCount = itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;
        return itemCount == 0 ? 0 : (int)Math.Ceiling(itemCount / (double)Math.Max(1, columns));
    }

    private bool CanArrangeOnlyForVerticalOffset(double verticalOffset)
    {
        if (_realizedFirstIndex < 0 || _realizedLastIndex < _realizedFirstIndex)
        {
            return false;
        }

        var itemsControl = ItemsControl.GetItemsOwner(this);
        var itemCount = itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;
        if (itemCount == 0)
        {
            return false;
        }

        var itemWidth = Math.Max(1d, ItemWidth);
        var itemHeight = Math.Max(1d, _effectiveItemHeight);
        var padding = NormalizePadding(ViewportPadding, ViewportWidth, ViewportHeight);
        var contentWidth = CalculateLayoutWidth(NormalizeViewportLength(ViewportWidth), padding);
        var columns = Math.Max(1, (int)Math.Floor(contentWidth / itemWidth));
        if (columns != _realizedColumns)
        {
            return false;
        }

        var rowCount = (int)Math.Ceiling(itemCount / (double)columns);
        var firstVisibleRow = Math.Max(0, (int)Math.Floor((verticalOffset - padding.Top) / itemHeight));
        var lastVisibleRow = Math.Min(rowCount - 1, (int)Math.Floor((verticalOffset + ViewportHeight - padding.Top) / itemHeight));
        var overscanRows = Math.Max(0, OverscanRows);
        var firstRow = Math.Max(0, firstVisibleRow - overscanRows);
        var lastRow = Math.Min(rowCount - 1, lastVisibleRow + overscanRows);
        var firstIndex = lastRow < firstRow ? 0 : firstRow * columns;
        var lastIndex = lastRow < firstRow ? -1 : Math.Min(itemCount - 1, ((lastRow + 1) * columns) - 1);

        return firstIndex >= _realizedFirstIndex && lastIndex <= _realizedLastIndex;
    }

    private void UpdateScrollInfo(Size viewport, Size extent)
    {
        var normalizedViewport = new Size(NormalizeViewportLength(viewport.Width), NormalizeViewportLength(viewport.Height));
        if (!AreClose(_viewport, normalizedViewport) || !AreClose(_extent, extent))
        {
            _viewport = normalizedViewport;
            _extent = extent;
            ScrollOwner?.InvalidateScrollInfo();
        }

        var clampedVerticalOffset = Math.Min(VerticalOffset, Math.Max(0d, _extent.Height - _viewport.Height));
        if (Math.Abs(clampedVerticalOffset - _offset.Y) >= 0.1d)
        {
            _offset.Y = clampedVerticalOffset;
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    private void WriteVirtualizationDiagnostics(
        int itemCount,
        int realizedCount,
        int columns,
        int firstVisibleRow,
        int lastVisibleRow)
    {
        if (!DiagnosticsEnabled)
        {
            return;
        }

        if (itemCount == 0)
        {
            return;
        }

        var signature = $"{itemCount}:{realizedCount}:{columns}:{ViewportWidth:0}:{ViewportHeight:0}:{_effectiveItemHeight:0}";
        if (string.Equals(signature, _lastDiagnosticsSignature, StringComparison.Ordinal))
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (_lastDiagnosticsTimestamp > 0
            && Stopwatch.GetElapsedTime(_lastDiagnosticsTimestamp, now) < DiagnosticsMinimumInterval)
        {
            return;
        }

        _lastDiagnosticsSignature = signature;
        _lastDiagnosticsTimestamp = now;
        AiPerfDiagnostics.WriteEvent(
            "event=library-poster-virtualization " +
            $"virtualizationEnabled=true items={itemCount} realized={realizedCount} " +
            $"columns={columns} firstVisibleRow={firstVisibleRow} lastVisibleRow={lastVisibleRow} " +
            $"itemWidth={ItemWidth:0} itemHeight={_effectiveItemHeight:0}");
    }

    private void WriteLayoutPerfDiagnostics(
        string stage,
        TimeSpan elapsed,
        int itemCount,
        int realizedCount,
        int columns,
        double viewportWidth,
        double viewportHeight)
    {
        if (!DiagnosticsEnabled)
        {
            return;
        }

        var isSlow = elapsed.TotalMilliseconds >= 12d;
        var now = Stopwatch.GetTimestamp();
        if (!isSlow
            && _lastLayoutDiagnosticsTimestamp > 0
            && Stopwatch.GetElapsedTime(_lastLayoutDiagnosticsTimestamp, now) < LayoutDiagnosticsMinimumInterval)
        {
            return;
        }

        _lastLayoutDiagnosticsTimestamp = now;
        AiPerfDiagnostics.WriteEvent(
            "event=library-virtualizing-wrap-panel " +
            $"stage={stage} elapsedMs={elapsed.TotalMilliseconds:0} items={itemCount} realized={realizedCount} " +
            $"columns={columns} viewportWidth={viewportWidth:0} viewportHeight={viewportHeight:0} " +
            $"offset={VerticalOffset:0} itemWidth={ItemWidth:0} itemHeight={_effectiveItemHeight:0} " +
            $"slow={isSlow.ToString().ToLowerInvariant()}");
    }

    private static double NormalizeViewportLength(double value)
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

    private static double CalculateLayoutWidth(double viewportWidth, Thickness padding)
    {
        return Math.Max(1d, NormalizeViewportLength(viewportWidth) - padding.Right);
    }

    private static double CalculateArrangedLeft(double viewportWidth, int columns, double itemWidth, double itemContentWidth)
    {
        var visibleItemWidth = itemContentWidth > 0d ? Math.Min(itemWidth, itemContentWidth) : itemWidth;
        var usedWidth = Math.Max(1d, ((Math.Max(1, columns) - 1) * itemWidth) + visibleItemWidth);
        return Math.Max(0d, (NormalizeViewportLength(viewportWidth) - usedWidth) * 0.5d);
    }

    private static double ClampPadding(double value, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
        {
            return 0d;
        }

        return Math.Min(value, Math.Max(0d, maximum - 1d));
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    private static bool AreClose(Size left, Size right)
    {
        return Math.Abs(left.Width - right.Width) < 0.1d && Math.Abs(left.Height - right.Height) < 0.1d;
    }
}
