using System.Windows;
using System.Windows.Media;

namespace MediaLibrary.App.Controls;

public sealed class RatingStarsBar : FrameworkElement
{
    public static readonly DependencyProperty ScoreValueProperty =
        DependencyProperty.Register(
            nameof(ScoreValue),
            typeof(double?),
            typeof(RatingStarsBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill),
            typeof(Brush),
            typeof(RatingStarsBar),
            new FrameworkPropertyMetadata(Brushes.Gold, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Brush),
            typeof(RatingStarsBar),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(RatingStarsBar),
            new FrameworkPropertyMetadata(0.65d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StarSizeProperty =
        DependencyProperty.Register(
            nameof(StarSize),
            typeof(double),
            typeof(RatingStarsBar),
            new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StarSpacingProperty =
        DependencyProperty.Register(
            nameof(StarSpacing),
            typeof(double),
            typeof(RatingStarsBar),
            new FrameworkPropertyMetadata(1.5d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public double? ScoreValue
    {
        get => (double?)GetValue(ScoreValueProperty);
        set => SetValue(ScoreValueProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }

    public double StarSpacing
    {
        get => (double)GetValue(StarSpacingProperty);
        set => SetValue(StarSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var starSize = Math.Max(0, StarSize);
        var spacing = Math.Max(0, StarSpacing);
        return new Size(starSize * 5 + spacing * 4, starSize);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var starSize = Math.Max(0, StarSize);
        if (starSize <= 0)
        {
            return;
        }

        var spacing = Math.Max(0, StarSpacing);
        var stars = Math.Clamp((ScoreValue ?? 0d) / 2d, 0d, 5d);
        var outlinePen = Stroke is null || StrokeThickness <= 0
            ? null
            : new Pen(Stroke, StrokeThickness)
            {
                LineJoin = PenLineJoin.Round
            };

        for (var index = 0; index < 5; index++)
        {
            var x = index * (starSize + spacing);
            var geometry = CreateStarGeometry(x, 0, starSize);
            var fillRatio = Math.Clamp(stars - index, 0d, 1d);

            if (Fill is not null && fillRatio > 0)
            {
                if (fillRatio < 1)
                {
                    drawingContext.PushClip(new RectangleGeometry(new Rect(x, 0, starSize * fillRatio, starSize)));
                    drawingContext.DrawGeometry(Fill, null, geometry);
                    drawingContext.Pop();
                }
                else
                {
                    drawingContext.DrawGeometry(Fill, null, geometry);
                }
            }

            if (outlinePen is not null)
            {
                drawingContext.DrawGeometry(null, outlinePen, geometry);
            }
        }
    }

    private static StreamGeometry CreateStarGeometry(double x, double y, double size)
    {
        var geometry = new StreamGeometry();
        var centerX = x + size / 2d;
        var centerY = y + size / 2d;
        var outerRadius = size / 2d;
        var innerRadius = outerRadius * 0.48d;

        using (var context = geometry.Open())
        {
            for (var pointIndex = 0; pointIndex < 10; pointIndex++)
            {
                var angle = (-90d + pointIndex * 36d) * Math.PI / 180d;
                var radius = pointIndex % 2 == 0 ? outerRadius : innerRadius;
                var point = new Point(
                    centerX + Math.Cos(angle) * radius,
                    centerY + Math.Sin(angle) * radius);

                if (pointIndex == 0)
                {
                    context.BeginFigure(point, true, true);
                }
                else
                {
                    context.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
