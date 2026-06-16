using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaLibrary.App.Helpers;

public readonly record struct PosterBackdropPalette(Color Primary, Color Secondary, Color Accent);

public enum PosterBackdropVariant
{
    Page,
    Glass
}

public static class PosterCachedBackdropBehavior
{
    private const int BackdropWidth = 320;
    private const int BackdropHeight = 200;
    private const int GlassBackdropWidth = 96;
    private const int GlassBackdropHeight = 60;
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

    public static readonly DependencyProperty PaletteOverrideProperty =
        DependencyProperty.RegisterAttached(
            "PaletteOverride",
            typeof(PosterBackdropPalette?),
            typeof(PosterCachedBackdropBehavior),
            new PropertyMetadata(null, OnBackdropPropertyChanged));

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.RegisterAttached(
            "Variant",
            typeof(PosterBackdropVariant),
            typeof(PosterCachedBackdropBehavior),
            new PropertyMetadata(PosterBackdropVariant.Page, OnBackdropPropertyChanged));

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

    public static PosterBackdropPalette? GetPaletteOverride(DependencyObject target)
    {
        return (PosterBackdropPalette?)target.GetValue(PaletteOverrideProperty);
    }

    public static void SetPaletteOverride(DependencyObject target, PosterBackdropPalette? value)
    {
        target.SetValue(PaletteOverrideProperty, value);
    }

    public static PosterBackdropVariant GetVariant(DependencyObject target)
    {
        return (PosterBackdropVariant)target.GetValue(VariantProperty);
    }

    public static void SetVariant(DependencyObject target, PosterBackdropVariant value)
    {
        target.SetValue(VariantProperty, value);
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
        var palette = GetEffectivePalette(border);
        var variant = GetVariant(border);
        if (TryGetCachedBackdrop(palette, variant, out var cached))
        {
            border.SetCurrentValue(Border.BackgroundProperty, cached);
            return;
        }

        border.SetCurrentValue(Border.BackgroundProperty, CreateImmediateBackdropBrush(palette, variant));
        var applyVersion = (int)border.GetValue(ApplyVersionProperty) + 1;
        border.SetValue(ApplyVersionProperty, applyVersion);
        _ = ApplyBackdropAsync(border, palette, variant, applyVersion);
    }

    private static async Task ApplyBackdropAsync(
        Border border,
        PosterBackdropPalette palette,
        PosterBackdropVariant variant,
        int applyVersion)
    {
        var brush = await Task.Run(() => GetOrCreateBackdrop(palette, variant)).ConfigureAwait(false);
        await border.Dispatcher.InvokeAsync(
            () =>
            {
                if (!GetIsEnabled(border)
                    || (int)border.GetValue(ApplyVersionProperty) != applyVersion
                    || GetEffectivePalette(border) != palette
                    || GetVariant(border) != variant)
                {
                    return;
                }

                border.SetCurrentValue(Border.BackgroundProperty, brush);
            });
    }

    private static PosterBackdropPalette GetEffectivePalette(DependencyObject target)
    {
        return GetPaletteOverride(target) ?? GetPalette(target);
    }

    private static Brush CreateImmediateBackdropBrush(PosterBackdropPalette palette, PosterBackdropVariant variant)
    {
        var isGlass = variant == PosterBackdropVariant.Glass;
        var deepNeutral = isGlass ? Color.FromRgb(40, 46, 58) : Color.FromRgb(30, 38, 52);
        var baseColor = Mix(deepNeutral, palette.Primary, isGlass ? 0.58 : 0.42);
        var secondary = Mix(palette.Secondary, Colors.White, isGlass ? 0.25 : 0.16);
        var accent = Mix(palette.Accent, Colors.White, isGlass ? 0.20 : 0.11);
        var alpha = (byte)(isGlass ? 196 : 168);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(1d, 1d),
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(WithAlpha(baseColor, alpha), 0d));
        brush.GradientStops.Add(new GradientStop(WithAlpha(secondary, (byte)Math.Min(220, alpha + 20)), 0.48d));
        brush.GradientStops.Add(new GradientStop(WithAlpha(accent, (byte)Math.Min(220, alpha + 12)), 1d));
        brush.Freeze();
        return brush;
    }

    private static bool TryGetCachedBackdrop(
        PosterBackdropPalette palette,
        PosterBackdropVariant variant,
        out ImageBrush brush)
    {
        return BackdropCache.TryGetValue(BuildCacheKey(QuantizePalette(palette), variant), out brush!);
    }

    private static ImageBrush GetOrCreateBackdrop(PosterBackdropPalette palette, PosterBackdropVariant variant)
    {
        var quantized = QuantizePalette(palette);
        var cacheKey = BuildCacheKey(quantized, variant);
        if (BackdropCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var bitmap = CreateBackdropBitmap(quantized, variant);
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

    private static string BuildCacheKey(PosterBackdropPalette palette, PosterBackdropVariant variant)
    {
        return $"{variant}:{ToCachePart(palette.Primary)}:{ToCachePart(palette.Secondary)}:{ToCachePart(palette.Accent)}";
    }

    private static BitmapSource CreateBackdropBitmap(PosterBackdropPalette palette, PosterBackdropVariant variant)
    {
        var width = variant == PosterBackdropVariant.Glass ? GlassBackdropWidth : BackdropWidth;
        var height = variant == PosterBackdropVariant.Glass ? GlassBackdropHeight : BackdropHeight;
        var pixels = new byte[width * height * 4];
        var isGlass = variant == PosterBackdropVariant.Glass;
        var deepNeutral = isGlass ? Color.FromRgb(40, 46, 58) : Color.FromRgb(30, 38, 52);
        var baseColor = Mix(deepNeutral, palette.Primary, isGlass ? 0.58 : 0.42);
        var primary = Mix(palette.Primary, Colors.White, isGlass ? 0.22 : 0.12);
        var secondary = Mix(palette.Secondary, Colors.White, isGlass ? 0.25 : 0.16);
        var accent = Mix(palette.Accent, Colors.White, isGlass ? 0.20 : 0.11);

        for (var y = 0; y < height; y++)
        {
            var normalizedY = y / (double)(height - 1);
            for (var x = 0; x < width; x++)
            {
                var normalizedX = x / (double)(width - 1);
                var waveX = normalizedX + (Math.Sin((normalizedY * (isGlass ? 2.1 : 5.2)) + 0.8) * (isGlass ? 0.025 : 0.055));
                var waveY = normalizedY + (Math.Sin((normalizedX * (isGlass ? 1.9 : 4.5)) + 1.7) * (isGlass ? 0.020 : 0.045));
                var leftFlow = Diffuse(waveX, waveY, 0.10, 0.14, isGlass ? 0.94 : 0.74, isGlass ? 0.90 : 0.66);
                var rightFlow = Diffuse(waveX, waveY, 0.91, 0.23, isGlass ? 0.90 : 0.69, isGlass ? 0.86 : 0.62);
                var lowerFlow = Diffuse(waveX, waveY, 0.54, 1.05, isGlass ? 1.10 : 0.93, isGlass ? 0.98 : 0.76);
                var glassBand = Diffuse(waveX, waveY, 0.46, 0.36, isGlass ? 1.06 : 0.82, isGlass ? 0.34 : 0.15);
                var glassEdge = Diffuse(waveX, waveY, 0.16, 0.76, isGlass ? 0.52 : 0.28, isGlass ? 0.84 : 0.56);

                var blended = Mix(baseColor, primary, leftFlow * (isGlass ? 0.72 : 0.66));
                blended = Mix(blended, secondary, rightFlow * (isGlass ? 0.76 : 0.72));
                blended = Mix(blended, accent, lowerFlow * (isGlass ? 0.68 : 0.62));
                blended = Mix(
                    blended,
                    Colors.White,
                    (glassBand * (isGlass ? 0.18 : 0.105)) + (glassEdge * (isGlass ? 0.09 : 0.055)));

                var alpha = (byte)Math.Clamp(
                    (isGlass ? 176 : 138)
                    + (leftFlow * (isGlass ? 22 : 32))
                    + (rightFlow * (isGlass ? 20 : 26))
                    + (lowerFlow * (isGlass ? 14 : 18))
                    + (glassBand * (isGlass ? 8 : 10)),
                    0,
                    isGlass ? 224 : 220);
                var index = ((y * width) + x) * 4;
                pixels[index] = Premultiply(blended.B, alpha);
                pixels[index + 1] = Premultiply(blended.G, alpha);
                pixels[index + 2] = Premultiply(blended.R, alpha);
                pixels[index + 3] = alpha;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            pixels,
            width * 4);
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

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
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
