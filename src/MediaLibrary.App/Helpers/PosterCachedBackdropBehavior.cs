using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaLibrary.App.Helpers;

public readonly record struct PosterBackdropPalette(Color Primary, Color Secondary, Color Accent);

public static class PosterCachedBackdropBehavior
{
    private const int BackdropWidth = 320;
    private const int BackdropHeight = 200;
    private const int MaxBackdropCacheEntries = 64;
    private static readonly PosterBackdropPalette DefaultPalette = new(
        Color.FromRgb(42, 65, 92),
        Color.FromRgb(78, 103, 132),
        Color.FromRgb(108, 82, 114));
    private static readonly ConcurrentDictionary<string, ImageBrush> BackdropCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> BackdropCacheOrder = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PosterCachedBackdropBehavior),
            new PropertyMetadata(false, OnBackdropPropertyChanged));

    public static readonly DependencyProperty PaletteProperty =
        DependencyProperty.RegisterAttached(
            "Palette",
            typeof(PosterBackdropPalette),
            typeof(PosterCachedBackdropBehavior),
            new PropertyMetadata(DefaultPalette, OnBackdropPropertyChanged));

    private static readonly DependencyProperty ApplyVersionProperty =
        DependencyProperty.RegisterAttached(
            "ApplyVersion",
            typeof(int),
            typeof(PosterCachedBackdropBehavior),
            new PropertyMetadata(0));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    public static PosterBackdropPalette GetPalette(DependencyObject target)
    {
        return (PosterBackdropPalette)target.GetValue(PaletteProperty);
    }

    public static void SetPalette(DependencyObject target, PosterBackdropPalette value)
    {
        target.SetValue(PaletteProperty, value);
    }

    private static void OnBackdropPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is Border border && GetIsEnabled(border))
        {
            ApplyBackdropWhenReady(border);
        }
    }

    private static void ApplyBackdropWhenReady(Border border)
    {
        var palette = GetPalette(border);
        if (TryGetCachedBackdrop(palette, out var cached))
        {
            border.SetCurrentValue(Border.BackgroundProperty, cached);
            return;
        }

        var applyVersion = (int)border.GetValue(ApplyVersionProperty) + 1;
        border.SetValue(ApplyVersionProperty, applyVersion);
        _ = ApplyBackdropAsync(border, palette, applyVersion);
    }

    private static async Task ApplyBackdropAsync(Border border, PosterBackdropPalette palette, int applyVersion)
    {
        var brush = await Task.Run(() => GetOrCreateBackdrop(palette)).ConfigureAwait(false);
        await border.Dispatcher.InvokeAsync(
            () =>
            {
                if (!GetIsEnabled(border)
                    || (int)border.GetValue(ApplyVersionProperty) != applyVersion
                    || GetPalette(border) != palette)
                {
                    return;
                }

                border.SetCurrentValue(Border.BackgroundProperty, brush);
            });
    }

    private static bool TryGetCachedBackdrop(PosterBackdropPalette palette, out ImageBrush brush)
    {
        return BackdropCache.TryGetValue(BuildCacheKey(QuantizePalette(palette)), out brush!);
    }

    private static ImageBrush GetOrCreateBackdrop(PosterBackdropPalette palette)
    {
        var quantized = QuantizePalette(palette);
        var cacheKey = BuildCacheKey(quantized);
        if (BackdropCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var bitmap = CreateBackdropBitmap(quantized);
        var brush = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
        brush.Freeze();

        if (BackdropCache.TryAdd(cacheKey, brush))
        {
            BackdropCacheOrder.Enqueue(cacheKey);
            while (BackdropCache.Count > MaxBackdropCacheEntries
                   && BackdropCacheOrder.TryDequeue(out var oldestKey))
            {
                BackdropCache.TryRemove(oldestKey, out _);
            }
        }

        return brush;
    }

    private static PosterBackdropPalette QuantizePalette(PosterBackdropPalette palette)
    {
        return new PosterBackdropPalette(
            QuantizeColor(palette.Primary),
            QuantizeColor(palette.Secondary),
            QuantizeColor(palette.Accent));
    }

    private static string BuildCacheKey(PosterBackdropPalette palette)
    {
        return $"{ToCachePart(palette.Primary)}:{ToCachePart(palette.Secondary)}:{ToCachePart(palette.Accent)}";
    }

    private static BitmapSource CreateBackdropBitmap(PosterBackdropPalette palette)
    {
        var pixels = new byte[BackdropWidth * BackdropHeight * 4];
        var deepNeutral = Color.FromRgb(30, 38, 52);
        var baseColor = Mix(deepNeutral, palette.Primary, 0.42);
        var primary = Mix(palette.Primary, Colors.White, 0.12);
        var secondary = Mix(palette.Secondary, Colors.White, 0.16);
        var accent = Mix(palette.Accent, Colors.White, 0.11);

        for (var y = 0; y < BackdropHeight; y++)
        {
            var normalizedY = y / (double)(BackdropHeight - 1);
            for (var x = 0; x < BackdropWidth; x++)
            {
                var normalizedX = x / (double)(BackdropWidth - 1);
                var waveX = normalizedX + (Math.Sin((normalizedY * 5.2) + 0.8) * 0.055);
                var waveY = normalizedY + (Math.Sin((normalizedX * 4.5) + 1.7) * 0.045);
                var leftFlow = Diffuse(waveX, waveY, 0.10, 0.14, 0.74, 0.66);
                var rightFlow = Diffuse(waveX, waveY, 0.91, 0.23, 0.69, 0.62);
                var lowerFlow = Diffuse(waveX, waveY, 0.54, 1.05, 0.93, 0.76);
                var glassBand = Diffuse(waveX, waveY, 0.46, 0.36, 0.82, 0.15);
                var glassEdge = Diffuse(waveX, waveY, 0.16, 0.76, 0.28, 0.56);

                var blended = Mix(baseColor, primary, leftFlow * 0.66);
                blended = Mix(blended, secondary, rightFlow * 0.72);
                blended = Mix(blended, accent, lowerFlow * 0.62);
                blended = Mix(blended, Colors.White, (glassBand * 0.105) + (glassEdge * 0.055));

                var alpha = (byte)Math.Clamp(
                    138 + (leftFlow * 32) + (rightFlow * 26) + (lowerFlow * 18) + (glassBand * 10),
                    0,
                    220);
                var index = ((y * BackdropWidth) + x) * 4;
                pixels[index] = Premultiply(blended.B, alpha);
                pixels[index + 1] = Premultiply(blended.G, alpha);
                pixels[index + 2] = Premultiply(blended.R, alpha);
                pixels[index + 3] = alpha;
            }
        }

        var bitmap = BitmapSource.Create(
            BackdropWidth,
            BackdropHeight,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            pixels,
            BackdropWidth * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static double Diffuse(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
    {
        var dx = (x - centerX) / radiusX;
        var dy = (y - centerY) / radiusY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var value = Math.Clamp(1 - distance, 0, 1);
        return value * value * (3 - (2 * value));
    }

    private static Color Mix(Color left, Color right, double amount)
    {
        var clamped = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(left.R + ((right.R - left.R) * clamped)),
            (byte)Math.Round(left.G + ((right.G - left.G) * clamped)),
            (byte)Math.Round(left.B + ((right.B - left.B) * clamped)));
    }

    private static byte Premultiply(byte value, byte alpha)
    {
        return (byte)Math.Round(value * (alpha / 255d));
    }

    private static Color QuantizeColor(Color color)
    {
        return Color.FromRgb(QuantizeChannel(color.R), QuantizeChannel(color.G), QuantizeChannel(color.B));
    }

    private static byte QuantizeChannel(byte value)
    {
        return (byte)Math.Clamp((int)Math.Round(value / 16d) * 16, byte.MinValue, byte.MaxValue);
    }

    private static string ToCachePart(Color color)
    {
        return $"{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
