using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaLibrary.App.Helpers;

public class CachedShadowBorder : Border
{
    private BitmapSource? shadowImage;
    private ShadowRenderSpec? shadowImageSpec;
    private ShadowRenderSpec? pendingShadowSpec;
    private int shadowApplyVersion;

    public static readonly DependencyProperty ShadowColorProperty =
        DependencyProperty.Register(
            nameof(ShadowColor),
            typeof(Color),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(Color.FromRgb(214, 228, 255), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty QuantizeShadowColorProperty =
        DependencyProperty.Register(
            nameof(QuantizeShadowColor),
            typeof(bool),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowPaddingProperty =
        DependencyProperty.Register(
            nameof(ShadowPadding),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(36d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowInnerMarginProperty =
        DependencyProperty.Register(
            nameof(ShadowInnerMargin),
            typeof(Thickness),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowBlurRadiusProperty =
        DependencyProperty.Register(
            nameof(ShadowBlurRadius),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(28d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowDirectionProperty =
        DependencyProperty.Register(
            nameof(ShadowDirection),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(270d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowOpacityProperty =
        DependencyProperty.Register(
            nameof(ShadowOpacity),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(0.32d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowDepthProperty =
        DependencyProperty.Register(
            nameof(ShadowDepth),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowVisualOpacityProperty =
        DependencyProperty.Register(
            nameof(ShadowVisualOpacity),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowVisualScaleProperty =
        DependencyProperty.Register(
            nameof(ShadowVisualScale),
            typeof(double),
            typeof(CachedShadowBorder),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color ShadowColor
    {
        get => (Color)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public bool QuantizeShadowColor
    {
        get => (bool)GetValue(QuantizeShadowColorProperty);
        set => SetValue(QuantizeShadowColorProperty, value);
    }

    public double ShadowPadding
    {
        get => (double)GetValue(ShadowPaddingProperty);
        set => SetValue(ShadowPaddingProperty, value);
    }

    public Thickness ShadowInnerMargin
    {
        get => (Thickness)GetValue(ShadowInnerMarginProperty);
        set => SetValue(ShadowInnerMarginProperty, value);
    }

    public double ShadowBlurRadius
    {
        get => (double)GetValue(ShadowBlurRadiusProperty);
        set => SetValue(ShadowBlurRadiusProperty, value);
    }

    public double ShadowDirection
    {
        get => (double)GetValue(ShadowDirectionProperty);
        set => SetValue(ShadowDirectionProperty, value);
    }

    public double ShadowOpacity
    {
        get => (double)GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }

    public double ShadowDepth
    {
        get => (double)GetValue(ShadowDepthProperty);
        set => SetValue(ShadowDepthProperty, value);
    }

    public double ShadowVisualOpacity
    {
        get => (double)GetValue(ShadowVisualOpacityProperty);
        set => SetValue(ShadowVisualOpacityProperty, value);
    }

    public double ShadowVisualScale
    {
        get => (double)GetValue(ShadowVisualScaleProperty);
        set => SetValue(ShadowVisualScaleProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        DrawCachedShadow(drawingContext);
        base.OnRender(drawingContext);
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        return IsInsideRenderBounds(hitTestParameters.HitPoint)
            ? base.HitTestCore(hitTestParameters)
            : null;
    }

    protected override GeometryHitTestResult? HitTestCore(GeometryHitTestParameters hitTestParameters)
    {
        var renderBounds = new Rect(RenderSize);
        return hitTestParameters.HitGeometry.Bounds.IntersectsWith(renderBounds)
            ? base.HitTestCore(hitTestParameters)
            : null;
    }

    private bool IsInsideRenderBounds(Point point)
    {
        return point.X >= 0d
            && point.Y >= 0d
            && point.X <= RenderSize.Width
            && point.Y <= RenderSize.Height;
    }

    private void DrawCachedShadow(DrawingContext drawingContext)
    {
        var width = RenderSize.Width;
        var height = RenderSize.Height;
        if (width <= 0d || height <= 0d || ShadowOpacity <= 0d)
        {
            return;
        }

        var spec = new ShadowRenderSpec(
            width,
            height,
            Math.Max(0d, ShadowPadding),
            ShadowInnerMargin,
            CornerRadius,
            ShadowBlurRadius,
            ShadowDirection,
            ShadowOpacity,
            ShadowDepth,
            ShadowColor,
            QuantizeShadowColor);

        if (shadowImageSpec != spec || shadowImage is null)
        {
            if (PosterCachedShadowBehavior.TryGetCachedShadowImage(
                    spec.CardWidth,
                    spec.CardHeight,
                    spec.Padding,
                    spec.InnerMargin,
                    spec.CornerRadius,
                    spec.BlurRadius,
                    spec.Direction,
                    spec.Opacity,
                    spec.ShadowDepth,
                    spec.Color,
                    spec.QuantizeColor,
                    out var cachedImage))
            {
                shadowImage = cachedImage;
                shadowImageSpec = spec;
            }
            else
            {
                BeginLoadShadowImage(spec);
                return;
            }
        }

        var visualOpacity = Math.Clamp(ShadowVisualOpacity, 0d, 1d);
        var visualScale = Math.Clamp(ShadowVisualScale, 0.92d, 1.12d);
        var shadowBounds = new Rect(-spec.Padding, -spec.Padding, width + (spec.Padding * 2d), height + (spec.Padding * 2d));
        if (Math.Abs(visualScale - 1d) > 0.0001d)
        {
            var centerX = shadowBounds.Left + (shadowBounds.Width / 2d);
            var centerY = shadowBounds.Top + (shadowBounds.Height / 2d);
            drawingContext.PushTransform(new ScaleTransform(visualScale, visualScale, centerX, centerY));
        }

        if (visualOpacity < 0.9999d)
        {
            drawingContext.PushOpacity(visualOpacity);
        }

        drawingContext.DrawImage(shadowImage, shadowBounds);

        if (visualOpacity < 0.9999d)
        {
            drawingContext.Pop();
        }

        if (Math.Abs(visualScale - 1d) > 0.0001d)
        {
            drawingContext.Pop();
        }
    }

    private void BeginLoadShadowImage(ShadowRenderSpec spec)
    {
        if (pendingShadowSpec == spec)
        {
            return;
        }

        pendingShadowSpec = spec;
        var applyVersion = ++shadowApplyVersion;
        _ = LoadShadowImageAsync(spec, applyVersion);
    }

    private async Task LoadShadowImageAsync(ShadowRenderSpec spec, int applyVersion)
    {
        var image = await Task.Run(
            () => PosterCachedShadowBehavior.GetOrCreateShadowImage(
                spec.CardWidth,
                spec.CardHeight,
                spec.Padding,
                spec.InnerMargin,
                spec.CornerRadius,
                spec.BlurRadius,
                spec.Direction,
                spec.Opacity,
                spec.ShadowDepth,
                spec.Color,
                spec.QuantizeColor)).ConfigureAwait(false);

        await Dispatcher.InvokeAsync(
            () =>
            {
                if (applyVersion != shadowApplyVersion)
                {
                    return;
                }

                pendingShadowSpec = null;
                shadowImage = image;
                shadowImageSpec = spec;
                InvalidateVisual();
            });
    }

    private readonly record struct ShadowRenderSpec(
        double CardWidth,
        double CardHeight,
        double Padding,
        Thickness InnerMargin,
        CornerRadius CornerRadius,
        double BlurRadius,
        double Direction,
        double Opacity,
        double ShadowDepth,
        Color Color,
        bool QuantizeColor);
}
