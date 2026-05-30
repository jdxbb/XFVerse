using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaLibrary.App.Helpers;

public static class PosterCachedShadowBehavior
{
    private const int MaxShadowImageCacheEntries = 128;
    private const double ShadowDpi = 96d;
    private static readonly ConcurrentDictionary<string, BitmapSource> ShadowImageCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> ShadowImageCacheOrder = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(false, OnShadowPropertyChanged));

    public static readonly DependencyProperty ShadowColorProperty =
        DependencyProperty.RegisterAttached(
            "ShadowColor",
            typeof(Color),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(Color.FromRgb(25, 34, 48), OnShadowPropertyChanged));

    public static readonly DependencyProperty QuantizeColorProperty =
        DependencyProperty.RegisterAttached(
            "QuantizeColor",
            typeof(bool),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(false, OnShadowPropertyChanged));

    public static readonly DependencyProperty CardWidthProperty =
        DependencyProperty.RegisterAttached(
            "CardWidth",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(180d, OnShadowPropertyChanged));

    public static readonly DependencyProperty CardHeightProperty =
        DependencyProperty.RegisterAttached(
            "CardHeight",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(270d, OnShadowPropertyChanged));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.RegisterAttached(
            "Padding",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(84d, OnShadowPropertyChanged));

    public static readonly DependencyProperty InnerMarginProperty =
        DependencyProperty.RegisterAttached(
            "InnerMargin",
            typeof(Thickness),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(new Thickness(), OnShadowPropertyChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(new CornerRadius(12), OnShadowPropertyChanged));

    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.RegisterAttached(
            "BlurRadius",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(28d, OnShadowPropertyChanged));

    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.RegisterAttached(
            "Direction",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(270d, OnShadowPropertyChanged));

    public static readonly DependencyProperty OpacityProperty =
        DependencyProperty.RegisterAttached(
            "Opacity",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(0.42d, OnShadowPropertyChanged));

    public static readonly DependencyProperty ShadowDepthProperty =
        DependencyProperty.RegisterAttached(
            "ShadowDepth",
            typeof(double),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(7d, OnShadowPropertyChanged));

    private static readonly DependencyProperty IsLoadedSubscribedProperty =
        DependencyProperty.RegisterAttached(
            "IsLoadedSubscribed",
            typeof(bool),
            typeof(PosterCachedShadowBehavior),
            new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    public static Color GetShadowColor(DependencyObject target)
    {
        return (Color)target.GetValue(ShadowColorProperty);
    }

    public static void SetShadowColor(DependencyObject target, Color value)
    {
        target.SetValue(ShadowColorProperty, value);
    }

    public static bool GetQuantizeColor(DependencyObject target)
    {
        return (bool)target.GetValue(QuantizeColorProperty);
    }

    public static void SetQuantizeColor(DependencyObject target, bool value)
    {
        target.SetValue(QuantizeColorProperty, value);
    }

    public static double GetCardWidth(DependencyObject target)
    {
        return (double)target.GetValue(CardWidthProperty);
    }

    public static void SetCardWidth(DependencyObject target, double value)
    {
        target.SetValue(CardWidthProperty, value);
    }

    public static double GetCardHeight(DependencyObject target)
    {
        return (double)target.GetValue(CardHeightProperty);
    }

    public static void SetCardHeight(DependencyObject target, double value)
    {
        target.SetValue(CardHeightProperty, value);
    }

    public static double GetPadding(DependencyObject target)
    {
        return (double)target.GetValue(PaddingProperty);
    }

    public static void SetPadding(DependencyObject target, double value)
    {
        target.SetValue(PaddingProperty, value);
    }

    public static Thickness GetInnerMargin(DependencyObject target)
    {
        return (Thickness)target.GetValue(InnerMarginProperty);
    }

    public static void SetInnerMargin(DependencyObject target, Thickness value)
    {
        target.SetValue(InnerMarginProperty, value);
    }

    public static CornerRadius GetCornerRadius(DependencyObject target)
    {
        return (CornerRadius)target.GetValue(CornerRadiusProperty);
    }

    public static void SetCornerRadius(DependencyObject target, CornerRadius value)
    {
        target.SetValue(CornerRadiusProperty, value);
    }

    public static double GetBlurRadius(DependencyObject target)
    {
        return (double)target.GetValue(BlurRadiusProperty);
    }

    public static void SetBlurRadius(DependencyObject target, double value)
    {
        target.SetValue(BlurRadiusProperty, value);
    }

    public static double GetDirection(DependencyObject target)
    {
        return (double)target.GetValue(DirectionProperty);
    }

    public static void SetDirection(DependencyObject target, double value)
    {
        target.SetValue(DirectionProperty, value);
    }

    public static double GetOpacity(DependencyObject target)
    {
        return (double)target.GetValue(OpacityProperty);
    }

    public static void SetOpacity(DependencyObject target, double value)
    {
        target.SetValue(OpacityProperty, value);
    }

    public static double GetShadowDepth(DependencyObject target)
    {
        return (double)target.GetValue(ShadowDepthProperty);
    }

    public static void SetShadowDepth(DependencyObject target, double value)
    {
        target.SetValue(ShadowDepthProperty, value);
    }

    private static void OnShadowPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is Image image)
        {
            ApplyShadowImageWhenReady(image);
        }
    }

    private static void ApplyShadowImageWhenReady(Image image)
    {
        if (image.IsLoaded)
        {
            ApplyShadowImage(image);
            return;
        }

        if ((bool)image.GetValue(IsLoadedSubscribedProperty))
        {
            return;
        }

        image.SetValue(IsLoadedSubscribedProperty, true);
        image.Loaded += OnImageLoaded;
    }

    private static void OnImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image)
        {
            return;
        }

        image.Loaded -= OnImageLoaded;
        image.SetValue(IsLoadedSubscribedProperty, false);
        ApplyShadowImage(image);
    }

    private static void ApplyShadowImage(Image image)
    {
        if (!GetIsEnabled(image))
        {
            return;
        }

        var spec = CreateSpec(image);
        image.SetCurrentValue(Image.SourceProperty, GetOrCreateShadowImage(spec));
    }

    private static ShadowSpec CreateSpec(Image image)
    {
        var color = GetShadowColor(image);
        if (GetQuantizeColor(image))
        {
            color = QuantizeColor(color);
        }

        return new ShadowSpec(
            GetCardWidth(image),
            GetCardHeight(image),
            GetPadding(image),
            GetInnerMargin(image),
            GetCornerRadius(image),
            GetBlurRadius(image),
            GetDirection(image),
            GetOpacity(image),
            GetShadowDepth(image),
            color);
    }

    private static BitmapSource GetOrCreateShadowImage(ShadowSpec spec)
    {
        var key = spec.ToCacheKey();
        if (ShadowImageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = CreateShadowImage(spec);
        if (ShadowImageCache.TryAdd(key, image))
        {
            ShadowImageCacheOrder.Enqueue(key);
            while (ShadowImageCache.Count > MaxShadowImageCacheEntries
                   && ShadowImageCacheOrder.TryDequeue(out var oldestKey))
            {
                ShadowImageCache.TryRemove(oldestKey, out _);
            }
        }

        return image;
    }

    private static BitmapSource CreateShadowImage(ShadowSpec spec)
    {
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(spec.CardWidth + (spec.Padding * 2)));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(spec.CardHeight + (spec.Padding * 2)));
        var cardWidth = Math.Max(1, spec.CardWidth - spec.InnerMargin.Left - spec.InnerMargin.Right);
        var cardHeight = Math.Max(1, spec.CardHeight - spec.InnerMargin.Top - spec.InnerMargin.Bottom);
        var angle = spec.Direction * Math.PI / 180d;
        var offsetX = Math.Cos(angle) * spec.ShadowDepth;
        var offsetY = -Math.Sin(angle) * spec.ShadowDepth;
        var rect = new Rect(
            spec.Padding + spec.InnerMargin.Left + offsetX,
            spec.Padding + spec.InnerMargin.Top + offsetY,
            cardWidth,
            cardHeight);
        var radius = Math.Min(
            Math.Max(0, spec.CornerRadius.TopLeft),
            Math.Min(cardWidth, cardHeight) * 0.5d);
        var alpha = new double[pixelWidth * pixelHeight];
        RasterizeRoundedRect(alpha, pixelWidth, pixelHeight, rect, radius);

        var blurRadius = Math.Max(1, (int)Math.Round(spec.BlurRadius * 0.5d));
        for (var pass = 0; pass < 3; pass++)
        {
            BoxBlur(alpha, pixelWidth, pixelHeight, blurRadius);
        }

        var pixels = new byte[pixelWidth * pixelHeight * 4];
        var opacityScale = Math.Clamp(spec.Opacity, 0d, 1d) * (spec.Color.A / 255d);
        for (var index = 0; index < alpha.Length; index++)
        {
            var opacity = Math.Clamp(alpha[index] * opacityScale, 0d, 1d);
            var pixelIndex = index * 4;
            pixels[pixelIndex] = (byte)Math.Round(spec.Color.B * opacity);
            pixels[pixelIndex + 1] = (byte)Math.Round(spec.Color.G * opacity);
            pixels[pixelIndex + 2] = (byte)Math.Round(spec.Color.R * opacity);
            pixels[pixelIndex + 3] = (byte)Math.Round(255d * opacity);
        }

        var bitmap = BitmapSource.Create(
            pixelWidth,
            pixelHeight,
            ShadowDpi,
            ShadowDpi,
            PixelFormats.Pbgra32,
            null,
            pixels,
            pixelWidth * 4);
        if (bitmap.CanFreeze)
        {
            bitmap.Freeze();
        }

        return bitmap;
    }

    private static void RasterizeRoundedRect(double[] alpha, int width, int height, Rect rect, double radius)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var coverage = 0d;
                coverage += IsInsideRoundedRect(x + 0.25d, y + 0.25d, rect, radius) ? 0.25d : 0d;
                coverage += IsInsideRoundedRect(x + 0.75d, y + 0.25d, rect, radius) ? 0.25d : 0d;
                coverage += IsInsideRoundedRect(x + 0.25d, y + 0.75d, rect, radius) ? 0.25d : 0d;
                coverage += IsInsideRoundedRect(x + 0.75d, y + 0.75d, rect, radius) ? 0.25d : 0d;
                alpha[(y * width) + x] = coverage;
            }
        }
    }

    private static bool IsInsideRoundedRect(double x, double y, Rect rect, double radius)
    {
        if (x < rect.Left || x > rect.Right || y < rect.Top || y > rect.Bottom)
        {
            return false;
        }

        if (radius <= 0)
        {
            return true;
        }

        var centerX = Math.Clamp(x, rect.Left + radius, rect.Right - radius);
        var centerY = Math.Clamp(y, rect.Top + radius, rect.Bottom - radius);
        var dx = x - centerX;
        var dy = y - centerY;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static void BoxBlur(double[] alpha, int width, int height, int radius)
    {
        if (radius <= 0)
        {
            return;
        }

        var temp = new double[alpha.Length];
        var window = (radius * 2) + 1;

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var sum = 0d;
            for (var x = -radius; x <= radius; x++)
            {
                sum += alpha[row + Math.Clamp(x, 0, width - 1)];
            }

            for (var x = 0; x < width; x++)
            {
                temp[row + x] = sum / window;
                sum -= alpha[row + Math.Clamp(x - radius, 0, width - 1)];
                sum += alpha[row + Math.Clamp(x + radius + 1, 0, width - 1)];
            }
        }

        for (var x = 0; x < width; x++)
        {
            var sum = 0d;
            for (var y = -radius; y <= radius; y++)
            {
                sum += temp[(Math.Clamp(y, 0, height - 1) * width) + x];
            }

            for (var y = 0; y < height; y++)
            {
                alpha[(y * width) + x] = sum / window;
                sum -= temp[(Math.Clamp(y - radius, 0, height - 1) * width) + x];
                sum += temp[(Math.Clamp(y + radius + 1, 0, height - 1) * width) + x];
            }
        }
    }

    private static Color QuantizeColor(Color color)
    {
        return Color.FromArgb(
            color.A,
            QuantizeChannel(color.R),
            QuantizeChannel(color.G),
            QuantizeChannel(color.B));
    }

    private static byte QuantizeChannel(byte value)
    {
        return (byte)Math.Clamp((int)Math.Round(value / 8d) * 8, byte.MinValue, byte.MaxValue);
    }

    private readonly record struct ShadowSpec(
        double CardWidth,
        double CardHeight,
        double Padding,
        Thickness InnerMargin,
        CornerRadius CornerRadius,
        double BlurRadius,
        double Direction,
        double Opacity,
        double ShadowDepth,
        Color Color)
    {
        public string ToCacheKey()
        {
            return FormattableString.Invariant(
                $"{CardWidth:0.###}|{CardHeight:0.###}|{Padding:0.###}|{InnerMargin.Left:0.###},{InnerMargin.Top:0.###},{InnerMargin.Right:0.###},{InnerMargin.Bottom:0.###}|{CornerRadius.TopLeft:0.###},{CornerRadius.TopRight:0.###},{CornerRadius.BottomRight:0.###},{CornerRadius.BottomLeft:0.###}|{BlurRadius:0.###}|{Direction:0.###}|{Opacity:0.###}|{ShadowDepth:0.###}|{Color.A:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2}");
        }
    }
}
