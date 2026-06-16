using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MediaLibrary.App.Controls;

public sealed class SplineAreaChart : FrameworkElement
{
    private static readonly Color RhythmAxisBlue = Color.FromRgb(66, 132, 218);
    private static readonly Color RhythmLowPurple = Color.FromRgb(138, 100, 204);
    private static readonly Color RhythmMiddlePinkOrange = Color.FromRgb(239, 142, 110);
    private static readonly Color RhythmHighRed = Color.FromRgb(221, 73, 80);
    private readonly GradientStop _peakGlowCenterStop;
    private readonly GradientStop _peakGlowEdgeStop;
    private readonly RadialGradientBrush _peakGlowBrush;
    private readonly SolidColorBrush _peakCoreBrush = new();

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable),
        typeof(SplineAreaChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush),
        typeof(Brush),
        typeof(SplineAreaChart),
        new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridLineBrushProperty = DependencyProperty.Register(
        nameof(GridLineBrush),
        typeof(Brush),
        typeof(SplineAreaChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public Brush GridLineBrush
    {
        get => (Brush)GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    public SplineAreaChart()
    {
        _peakGlowCenterStop = new GradientStop(Colors.CornflowerBlue, 0d);
        _peakGlowEdgeStop = new GradientStop(Colors.Transparent, 1d);
        _peakGlowBrush = new RadialGradientBrush
        {
            GradientStops =
            {
                _peakGlowCenterStop,
                _peakGlowEdgeStop
            }
        };
        _peakGlowBrush.BeginAnimation(
            Brush.OpacityProperty,
            new DoubleAnimation(0.58d, 1d, TimeSpan.FromSeconds(1.45d))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = RenderSize.Width;
        var height = RenderSize.Height;
        if (width <= 1d || height <= 1d)
        {
            return;
        }

        const double horizontalPadding = 12d;
        const double topPadding = 18d;
        const double bottomPadding = 12d;
        const double lineThickness = 3d;
        var bottom = height - bottomPadding;
        var zeroBaseline = bottom - 2d;
        var plotHeight = Math.Max(1d, zeroBaseline - topPadding);
        var gridPen = new Pen(GridLineBrush, 1d)
        {
            DashStyle = new DashStyle([4d, 5d], 0d)
        };
        gridPen.Freeze();
        foreach (var ratio in new[] { 0.25d, 0.5d, 0.75d })
        {
            var y = topPadding + (plotHeight * ratio);
            drawingContext.DrawLine(gridPen, new Point(horizontalPadding, y), new Point(width - horizontalPadding, y));
        }

        var values = ReadValues();
        if (values.Count == 0)
        {
            return;
        }

        var maximum = Math.Max(1d, values.Max());
        var plotWidth = Math.Max(1d, width - (horizontalPadding * 2d));
        var points = new List<Point>(values.Count + 2)
        {
            new(horizontalPadding, zeroBaseline)
        };
        var step = plotWidth / values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            var x = horizontalPadding + (step * (index + 0.5d));
            var normalized = Math.Clamp(values[index] / maximum, 0d, 1d);
            var y = values[index] <= 0d
                ? zeroBaseline
                : topPadding + (plotHeight * (1d - normalized));
            points.Add(new Point(x, y));
        }
        points.Add(new Point(width - horizontalPadding, zeroBaseline));

        var segments = BuildSplineSegments(points, topPadding, zeroBaseline);
        var lineGeometry = CreateSplineGeometry(points, segments, closeToBottom: false, zeroBaseline);
        var areaGeometry = CreateSplineGeometry(points, segments, closeToBottom: true, zeroBaseline);
        var areaBrush = CreateRhythmAreaBrush();
        areaBrush.Freeze();
        var lineBrush = CreateRhythmLineBrush();
        lineBrush.Freeze();
        var linePen = new Pen(lineBrush, lineThickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        linePen.Freeze();
        var strokeSafePlotHeight = Math.Max(1d, bottom - topPadding);
        drawingContext.PushClip(new RectangleGeometry(new Rect(horizontalPadding, topPadding, plotWidth, strokeSafePlotHeight)));
        drawingContext.DrawGeometry(areaBrush, null, areaGeometry);
        drawingContext.DrawGeometry(null, linePen, lineGeometry);
        drawingContext.Pop();

        if (values.Max() <= 0d)
        {
            return;
        }

        var mathematicalPeak = FindSplinePeak(segments);
        var peak = new Point(
            Math.Clamp(mathematicalPeak.X, horizontalPadding, width - horizontalPadding),
            Math.Clamp(mathematicalPeak.Y, topPadding, zeroBaseline));
        UpdatePeakBrushes(GetRhythmColorForY(peak.Y, topPadding, zeroBaseline));
        drawingContext.DrawEllipse(_peakGlowBrush, null, peak, 17d, 17d);
        drawingContext.DrawEllipse(_peakCoreBrush, new Pen(Brushes.White, 1.5d), peak, 4.5d, 4.5d);
    }

    private static LinearGradientBrush CreateRhythmLineBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0.5d, 0d),
            EndPoint = new Point(0.5d, 1d),
            GradientStops =
            {
                new GradientStop(RhythmHighRed, 0d),
                new GradientStop(RhythmHighRed, 0.25d),
                new GradientStop(RhythmMiddlePinkOrange, 0.50d),
                new GradientStop(RhythmLowPurple, 0.75d),
                new GradientStop(RhythmAxisBlue, 1d)
            }
        };
    }

    private static LinearGradientBrush CreateRhythmAreaBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0.5d, 0d),
            EndPoint = new Point(0.5d, 1d),
            GradientStops =
            {
                CreateAreaGradientStop(RhythmHighRed, 0d),
                CreateAreaGradientStop(RhythmHighRed, 0.25d),
                CreateAreaGradientStop(RhythmMiddlePinkOrange, 0.50d),
                CreateAreaGradientStop(RhythmLowPurple, 0.75d),
                CreateAreaGradientStop(RhythmAxisBlue, 1d)
            }
        };
    }

    private static GradientStop CreateAreaGradientStop(Color color, double offset)
    {
        var alpha = (byte)Math.Round(118d - (110d * Math.Clamp(offset, 0d, 1d)));
        return new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), offset);
    }

    private static Color GetRhythmColorForY(double y, double top, double bottom)
    {
        var offset = Math.Clamp((y - top) / Math.Max(1d, bottom - top), 0d, 1d);
        if (offset <= 0.25d)
        {
            return RhythmHighRed;
        }

        if (offset <= 0.50d)
        {
            return Mix(RhythmHighRed, RhythmMiddlePinkOrange, (offset - 0.25d) / 0.25d);
        }

        if (offset <= 0.75d)
        {
            return Mix(RhythmMiddlePinkOrange, RhythmLowPurple, (offset - 0.50d) / 0.25d);
        }

        return Mix(RhythmLowPurple, RhythmAxisBlue, (offset - 0.75d) / 0.25d);
    }

    private static Color Mix(Color left, Color right, double rightWeight)
    {
        var weight = Math.Clamp(rightWeight, 0d, 1d);
        return Color.FromRgb(
            (byte)Math.Round((left.R * (1d - weight)) + (right.R * weight)),
            (byte)Math.Round((left.G * (1d - weight)) + (right.G * weight)),
            (byte)Math.Round((left.B * (1d - weight)) + (right.B * weight)));
    }

    private List<double> ReadValues()
    {
        var result = new List<double>();
        if (Values is null)
        {
            return result;
        }

        foreach (var value in Values)
        {
            if (value is not null && double.TryParse(value.ToString(), out var parsed))
            {
                result.Add(Math.Max(0d, parsed));
            }
        }

        return result;
    }

    private void UpdatePeakBrushes(Color accentColor)
    {
        _peakGlowCenterStop.Color = Color.FromArgb(210, accentColor.R, accentColor.G, accentColor.B);
        _peakGlowEdgeStop.Color = Color.FromArgb(0, accentColor.R, accentColor.G, accentColor.B);
        _peakCoreBrush.Color = accentColor;
    }

    private static IReadOnlyList<BezierSegment> BuildSplineSegments(
        IReadOnlyList<Point> points,
        double top,
        double bottom)
    {
        if (points.Count < 2)
        {
            return [];
        }

        var secondDerivatives = CalculateNaturalSplineSecondDerivatives(points);
        var segments = new List<BezierSegment>(Math.Max(0, points.Count - 1));
        for (var index = 0; index < points.Count - 1; index++)
        {
            var current = points[index];
            var next = points[index + 1];
            var interval = Math.Max(0.0001d, next.X - current.X);
            var startSlope = ((next.Y - current.Y) / interval)
                             - (interval * ((2d * secondDerivatives[index]) + secondDerivatives[index + 1]) / 6d);
            var endSlope = ((next.Y - current.Y) / interval)
                           + (interval * (secondDerivatives[index] + (2d * secondDerivatives[index + 1])) / 6d);
            var control1 = new Point(
                current.X + (interval / 3d),
                Math.Clamp(current.Y + (startSlope * interval / 3d), top, bottom));
            var control2 = new Point(
                next.X - (interval / 3d),
                Math.Clamp(next.Y - (endSlope * interval / 3d), top, bottom));
            segments.Add(new BezierSegment(current, control1, control2, next));
        }

        return segments;
    }

    private static double[] CalculateNaturalSplineSecondDerivatives(IReadOnlyList<Point> points)
    {
        var count = points.Count;
        var lower = new double[count];
        var diagonal = new double[count];
        var upper = new double[count];
        var rightHandSide = new double[count];
        diagonal[0] = 1d;
        diagonal[^1] = 1d;

        for (var index = 1; index < count - 1; index++)
        {
            var previousInterval = Math.Max(0.0001d, points[index].X - points[index - 1].X);
            var nextInterval = Math.Max(0.0001d, points[index + 1].X - points[index].X);
            lower[index] = previousInterval;
            diagonal[index] = 2d * (previousInterval + nextInterval);
            upper[index] = nextInterval;
            rightHandSide[index] = 6d
                                   * (((points[index + 1].Y - points[index].Y) / nextInterval)
                                      - ((points[index].Y - points[index - 1].Y) / previousInterval));
        }

        for (var index = 1; index < count; index++)
        {
            var factor = lower[index] / Math.Max(0.0001d, diagonal[index - 1]);
            diagonal[index] -= factor * upper[index - 1];
            rightHandSide[index] -= factor * rightHandSide[index - 1];
        }

        var result = new double[count];
        result[^1] = rightHandSide[^1] / Math.Max(0.0001d, diagonal[^1]);
        for (var index = count - 2; index >= 0; index--)
        {
            result[index] = (rightHandSide[index] - (upper[index] * result[index + 1]))
                            / Math.Max(0.0001d, diagonal[index]);
        }

        return result;
    }

    private static StreamGeometry CreateSplineGeometry(
        IReadOnlyList<Point> points,
        IReadOnlyList<BezierSegment> segments,
        bool closeToBottom,
        double bottom)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        if (closeToBottom)
        {
            context.BeginFigure(new Point(points[0].X, bottom), true, true);
            context.LineTo(points[0], true, false);
        }
        else
        {
            context.BeginFigure(points[0], false, false);
        }

        foreach (var segment in segments)
        {
            context.BezierTo(segment.Control1, segment.Control2, segment.End, true, false);
        }

        if (closeToBottom)
        {
            context.LineTo(new Point(points[^1].X, bottom), true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point FindSplinePeak(IReadOnlyList<BezierSegment> segments)
    {
        var peak = segments[0].Start;
        foreach (var segment in segments)
        {
            foreach (var t in FindVerticalExtrema(segment))
            {
                var candidate = Evaluate(segment, t);
                if (candidate.Y < peak.Y)
                {
                    peak = candidate;
                }
            }
        }

        return peak;
    }

    private static IEnumerable<double> FindVerticalExtrema(BezierSegment segment)
    {
        yield return 0d;
        yield return 1d;

        var a = -segment.Start.Y + (3d * segment.Control1.Y) - (3d * segment.Control2.Y) + segment.End.Y;
        var b = (3d * segment.Start.Y) - (6d * segment.Control1.Y) + (3d * segment.Control2.Y);
        var c = (-3d * segment.Start.Y) + (3d * segment.Control1.Y);
        var quadratic = 3d * a;
        var linear = 2d * b;
        const double epsilon = 0.000001d;
        if (Math.Abs(quadratic) < epsilon)
        {
            if (Math.Abs(linear) >= epsilon)
            {
                var root = -c / linear;
                if (root is > 0d and < 1d)
                {
                    yield return root;
                }
            }

            yield break;
        }

        var discriminant = (linear * linear) - (4d * quadratic * c);
        if (discriminant < 0d)
        {
            yield break;
        }

        var squareRoot = Math.Sqrt(discriminant);
        var first = (-linear + squareRoot) / (2d * quadratic);
        var second = (-linear - squareRoot) / (2d * quadratic);
        if (first is > 0d and < 1d)
        {
            yield return first;
        }

        if (second is > 0d and < 1d && Math.Abs(second - first) >= epsilon)
        {
            yield return second;
        }
    }

    private static Point Evaluate(BezierSegment segment, double t)
    {
        var inverse = 1d - t;
        var startWeight = inverse * inverse * inverse;
        var control1Weight = 3d * inverse * inverse * t;
        var control2Weight = 3d * inverse * t * t;
        var endWeight = t * t * t;
        return new Point(
            (startWeight * segment.Start.X) + (control1Weight * segment.Control1.X) +
            (control2Weight * segment.Control2.X) + (endWeight * segment.End.X),
            (startWeight * segment.Start.Y) + (control1Weight * segment.Control1.Y) +
            (control2Weight * segment.Control2.Y) + (endWeight * segment.End.Y));
    }

    private readonly record struct BezierSegment(Point Start, Point Control1, Point Control2, Point End);
}

public sealed class DonutChart : FrameworkElement
{
    private readonly ToolTip _segmentToolTip;

    public static readonly DependencyProperty PrimaryRatioProperty = DependencyProperty.Register(
        nameof(PrimaryRatio),
        typeof(double),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PrimaryBrushProperty = DependencyProperty.Register(
        nameof(PrimaryBrush),
        typeof(Brush),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryBrushProperty = DependencyProperty.Register(
        nameof(SecondaryBrush),
        typeof(Brush),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(Brushes.HotPink, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness),
        typeof(double),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(18d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PrimaryToolTipProperty = DependencyProperty.Register(
        nameof(PrimaryToolTip),
        typeof(string),
        typeof(DonutChart),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryToolTipProperty = DependencyProperty.Register(
        nameof(SecondaryToolTip),
        typeof(string),
        typeof(DonutChart),
        new PropertyMetadata(null));

    public double PrimaryRatio
    {
        get => (double)GetValue(PrimaryRatioProperty);
        set => SetValue(PrimaryRatioProperty, value);
    }

    public Brush PrimaryBrush
    {
        get => (Brush)GetValue(PrimaryBrushProperty);
        set => SetValue(PrimaryBrushProperty, value);
    }

    public Brush SecondaryBrush
    {
        get => (Brush)GetValue(SecondaryBrushProperty);
        set => SetValue(SecondaryBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    public string? PrimaryToolTip
    {
        get => (string?)GetValue(PrimaryToolTipProperty);
        set => SetValue(PrimaryToolTipProperty, value);
    }

    public string? SecondaryToolTip
    {
        get => (string?)GetValue(SecondaryToolTipProperty);
        set => SetValue(SecondaryToolTipProperty, value);
    }

    public DonutChart()
    {
        _segmentToolTip = new ToolTip
        {
            Placement = PlacementMode.Mouse,
            PlacementTarget = this,
            StaysOpen = true
        };
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var thickness = Math.Max(2d, RingThickness);
        var radius = Math.Max(1d, (Math.Min(RenderSize.Width, RenderSize.Height) - thickness) / 2d);
        var center = new Point(RenderSize.Width / 2d, RenderSize.Height / 2d);
        var trackPen = CreateRingPen(TrackBrush, thickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var primaryRatio = Math.Clamp(PrimaryRatio, 0d, 1d);
        if (primaryRatio <= 0d)
        {
            DrawArc(drawingContext, center, radius, -90d, 359.99d, CreateRingPen(SecondaryBrush, thickness));
            return;
        }

        if (primaryRatio >= 1d)
        {
            drawingContext.DrawEllipse(null, CreateRingPen(PrimaryBrush, thickness), center, radius, radius);
            return;
        }

        var primaryDegrees = primaryRatio * 360d;
        var secondaryDegrees = (1d - primaryRatio) * 360d;
        var gapDegrees = Math.Min(2d, Math.Min(primaryDegrees, secondaryDegrees) * 0.2d);
        var primarySweep = Math.Max(0d, primaryDegrees - gapDegrees);
        var secondarySweep = Math.Max(0d, secondaryDegrees - gapDegrees);
        DrawArc(drawingContext, center, radius, -90d + (gapDegrees / 2d), primarySweep, CreateRingPen(PrimaryBrush, thickness));
        DrawArc(
            drawingContext,
            center,
            radius,
            -90d + primaryDegrees + (gapDegrees / 2d),
            secondarySweep,
            CreateRingPen(SecondaryBrush, thickness));
    }

    private static Pen CreateRingPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat
        };
        pen.Freeze();
        return pen;
    }

    private static void DrawArc(DrawingContext drawingContext, Point center, double radius, double startDegrees, double sweepDegrees, Pen pen)
    {
        if (sweepDegrees <= 0.01d)
        {
            return;
        }

        var startRadians = startDegrees * Math.PI / 180d;
        var endRadians = (startDegrees + sweepDegrees) * Math.PI / 180d;
        var start = new Point(center.X + (Math.Cos(startRadians) * radius), center.Y + (Math.Sin(startRadians) * radius));
        var end = new Point(center.X + (Math.Cos(endRadians) * radius), center.Y + (Math.Sin(endRadians) * radius));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0d, sweepDegrees > 180d, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        var center = new Point(RenderSize.Width / 2d, RenderSize.Height / 2d);
        var distance = (point - center).Length;
        var thickness = Math.Max(2d, RingThickness);
        var radius = Math.Max(1d, (Math.Min(RenderSize.Width, RenderSize.Height) - thickness) / 2d);
        if (Math.Abs(distance - radius) > (thickness / 2d) + 2d)
        {
            CloseSegmentToolTip();
            return;
        }

        var angle = (Math.Atan2(point.Y - center.Y, point.X - center.X) * 180d / Math.PI + 450d) % 360d;
        var primaryBoundary = Math.Clamp(PrimaryRatio, 0d, 1d) * 360d;
        var content = angle < primaryBoundary ? PrimaryToolTip : SecondaryToolTip;
        if (string.IsNullOrWhiteSpace(content))
        {
            CloseSegmentToolTip();
            return;
        }

        _segmentToolTip.Content = content;
        _segmentToolTip.IsOpen = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        CloseSegmentToolTip();
        base.OnMouseLeave(e);
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        return new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    private void CloseSegmentToolTip()
    {
        _segmentToolTip.IsOpen = false;
    }
}

public sealed class WeekPartIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(string),
        typeof(WeekPartIcon),
        new FrameworkPropertyMetadata("Workday", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground),
        typeof(Brush),
        typeof(WeekPartIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var scale = Math.Min(RenderSize.Width, RenderSize.Height) / 24d;
        if (scale <= 0d)
        {
            return;
        }

        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        if (string.Equals(Kind, "Weekend", StringComparison.OrdinalIgnoreCase))
        {
            DrawPopcorn(drawingContext);
        }
        else
        {
            DrawComputer(drawingContext);
        }

        drawingContext.Pop();
    }

    private void DrawComputer(DrawingContext drawingContext)
    {
        var pen = new Pen(Foreground, 1.8d) { LineJoin = PenLineJoin.Round };
        drawingContext.DrawRoundedRectangle(null, pen, new Rect(3d, 4d, 18d, 12d), 2d, 2d);
        drawingContext.DrawLine(pen, new Point(9d, 19d), new Point(15d, 19d));
        drawingContext.DrawLine(pen, new Point(12d, 16d), new Point(12d, 19d));
    }

    private void DrawPopcorn(DrawingContext drawingContext)
    {
        var pen = new Pen(Foreground, 1.55d) { LineJoin = PenLineJoin.Round };
        var bucket = new StreamGeometry();
        using (var context = bucket.Open())
        {
            context.BeginFigure(new Point(5d, 10d), false, true);
            context.LineTo(new Point(19d, 10d), true, false);
            context.LineTo(new Point(17d, 21d), true, false);
            context.LineTo(new Point(7d, 21d), true, false);
        }

        bucket.Freeze();
        drawingContext.DrawGeometry(null, pen, bucket);
        drawingContext.DrawLine(pen, new Point(10d, 11d), new Point(10.5d, 20d));
        drawingContext.DrawLine(pen, new Point(14d, 11d), new Point(13.5d, 20d));
        foreach (var center in new[]
                 {
                     new Point(7d, 8d), new Point(10d, 6d), new Point(13d, 7.5d),
                     new Point(16d, 6d), new Point(18d, 8.5d)
                 })
        {
            drawingContext.DrawEllipse(null, pen, center, 2.4d, 2.4d);
        }
    }
}
