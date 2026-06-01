using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MediaLibrary.App.Helpers;

public static class PosterPaletteBackdropBehavior
{
    private const int HueBucketCount = 18;
    private const int MaxPaletteCacheEntries = 512;
    private static readonly PosterBackdropPalette FallbackPalette = new(
        Color.FromRgb(42, 65, 92),
        Color.FromRgb(78, 103, 132),
        Color.FromRgb(108, 82, 114));
    private static readonly ConcurrentDictionary<string, PosterBackdropPalette> PaletteCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> PaletteCacheOrder = new();
    private static readonly Lazy<PosterBackdropPalette> PlaceholderPosterPalette = new(CreatePlaceholderPosterPalette);

    public static readonly DependencyProperty TargetElementProperty =
        DependencyProperty.RegisterAttached(
            "TargetElement",
            typeof(Border),
            typeof(PosterPaletteBackdropBehavior),
            new PropertyMetadata(null, OnTargetElementChanged));

    private static readonly DependencyProperty IsSubscribedProperty =
        DependencyProperty.RegisterAttached(
            "IsSubscribed",
            typeof(bool),
            typeof(PosterPaletteBackdropBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty ApplyVersionProperty =
        DependencyProperty.RegisterAttached(
            "ApplyVersion",
            typeof(int),
            typeof(PosterPaletteBackdropBehavior),
            new PropertyMetadata(0));

    public static Border? GetTargetElement(DependencyObject target)
    {
        return (Border?)target.GetValue(TargetElementProperty);
    }

    public static void SetTargetElement(DependencyObject target, Border? value)
    {
        target.SetValue(TargetElementProperty, value);
    }

    private static void OnTargetElementChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Image image)
        {
            return;
        }

        EnsureSubscribed(image);
        BeginApplyPalette(image);
    }

    private static void EnsureSubscribed(Image image)
    {
        if ((bool)image.GetValue(IsSubscribedProperty))
        {
            return;
        }

        var descriptor = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));
        descriptor.AddValueChanged(image, OnImageSourceChanged);
        image.Loaded += OnImageLoaded;
        image.Unloaded += OnImageUnloaded;
        image.SetValue(IsSubscribedProperty, true);
    }

    private static void OnImageSourceChanged(object? sender, EventArgs e)
    {
        if (sender is Image image)
        {
            BeginApplyPalette(image);
        }
    }

    private static void OnImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            BeginApplyPalette(image);
        }
    }

    private static void OnImageUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image)
        {
            return;
        }

        var descriptor = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));
        descriptor.RemoveValueChanged(image, OnImageSourceChanged);
        image.Loaded -= OnImageLoaded;
        image.Unloaded -= OnImageUnloaded;
        image.SetValue(IsSubscribedProperty, false);
    }

    private static void BeginApplyPalette(Image image)
    {
        if (!image.IsLoaded || GetTargetElement(image) is not { } target)
        {
            return;
        }

        var applyVersion = (int)image.GetValue(ApplyVersionProperty) + 1;
        image.SetValue(ApplyVersionProperty, applyVersion);
        var hasSourceKey = TryGetRequestedSourceKey(image, out var sourceKey);
        if (!hasSourceKey)
        {
            PosterCachedBackdropBehavior.SetPalette(target, PlaceholderPosterPalette.Value);
            return;
        }

        if (PaletteCache.TryGetValue(sourceKey, out var cached))
        {
            PosterCachedBackdropBehavior.SetPalette(target, cached);
            return;
        }

        if (image.Source is null)
        {
            PosterCachedBackdropBehavior.SetPalette(target, PlaceholderPosterPalette.Value);
            return;
        }

        image.Dispatcher.BeginInvoke(
            () => ApplyPalette(image, sourceKey, applyVersion),
            DispatcherPriority.Loaded);
    }

    private static void ApplyPalette(Image image, string expectedSourceKey, int applyVersion)
    {
        if ((int)image.GetValue(ApplyVersionProperty) != applyVersion
            || !TryGetRequestedSourceKey(image, out var currentSourceKey)
            || !string.Equals(currentSourceKey, expectedSourceKey, StringComparison.Ordinal)
            || GetTargetElement(image) is not { } target)
        {
            return;
        }

        var palette = TryExtractPalette(image.Source, out var extracted)
            ? extracted
            : FallbackPalette;
        CachePalette(expectedSourceKey, palette);

        PosterCachedBackdropBehavior.SetPalette(target, palette);
    }

    private static bool TryExtractPalette(ImageSource? source, out PosterBackdropPalette palette)
    {
        palette = FallbackPalette;
        if (source is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return false;
        }

        var cacheKey = BuildBitmapCacheKey(bitmap);
        if (PaletteCache.TryGetValue(cacheKey, out palette))
        {
            return true;
        }

        try
        {
            var converted = bitmap.Format == PixelFormats.Bgra32 || bitmap.Format == PixelFormats.Pbgra32
                ? bitmap
                : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            var stepX = Math.Max(1, width / 38);
            var stepY = Math.Max(1, height / 56);
            var hueBuckets = new HueBucket[HueBucketCount];
            double averageRed = 0;
            double averageGreen = 0;
            double averageBlue = 0;
            double averageWeight = 0;

            for (var y = 0; y < height; y += stepY)
            {
                var row = y * stride;
                for (var x = 0; x < width; x += stepX)
                {
                    var index = row + (x * 4);
                    var blue = pixels[index];
                    var green = pixels[index + 1];
                    var red = pixels[index + 2];
                    var alpha = pixels[index + 3];
                    if (alpha < 32)
                    {
                        continue;
                    }

                    var max = Math.Max(red, Math.Max(green, blue));
                    var min = Math.Min(red, Math.Min(green, blue));
                    var chroma = max - min;
                    var saturation = max == 0 ? 0 : chroma / (double)max;
                    var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
                    if (luminance < 18 || luminance > 240)
                    {
                        continue;
                    }

                    var midtoneLift = 1 - (Math.Abs(luminance - 128) / 128);
                    var weight = 0.25 + (saturation * 2.2) + (Math.Max(0, midtoneLift) * 0.55);
                    averageRed += red * weight;
                    averageGreen += green * weight;
                    averageBlue += blue * weight;
                    averageWeight += weight;

                    if (saturation < 0.14 || chroma < 18)
                    {
                        continue;
                    }

                    var hue = GetHue(red, green, blue, max, chroma);
                    var bucketIndex = Math.Clamp((int)Math.Floor(hue / 360d * HueBucketCount), 0, HueBucketCount - 1);
                    hueBuckets[bucketIndex].Add(red, green, blue, weight * (1 + saturation));
                }
            }

            if (averageWeight <= 0)
            {
                return false;
            }

            var average = CreateBackdropColor(
                averageRed / averageWeight,
                averageGreen / averageWeight,
                averageBlue / averageWeight);
            var rankedBuckets = hueBuckets
                .Select((bucket, index) => new RankedHue(index, bucket))
                .Where(item => item.Bucket.Weight > 0)
                .OrderByDescending(item => item.Bucket.Weight)
                .ToList();
            var primary = rankedBuckets.Count > 0
                ? CreateBackdropColor(rankedBuckets[0].Bucket)
                : average;
            var secondary = FindDistinctColor(rankedBuckets, primaryIndex: rankedBuckets.FirstOrDefault().Index, excludedIndex: null)
                ?? CreateCompanionColor(primary, average, Color.FromRgb(72, 108, 142));
            var accent = FindDistinctColor(
                    rankedBuckets,
                    primaryIndex: rankedBuckets.FirstOrDefault().Index,
                    excludedIndex: FindClosestBucketIndex(rankedBuckets, secondary))
                ?? CreateCompanionColor(secondary, primary, Color.FromRgb(132, 92, 118));

            palette = new PosterBackdropPalette(primary, secondary, accent);
            CachePalette(cacheKey, palette);
            return true;
        }
        catch
        {
            palette = FallbackPalette;
            return false;
        }
    }

    private static PosterBackdropPalette CreatePlaceholderPosterPalette()
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(
                "pack://application:,,,/MediaLibrary.App;component/Assets/poster-placeholder.png",
                UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return TryExtractPalette(bitmap, out var palette) ? palette : FallbackPalette;
        }
        catch
        {
            return FallbackPalette;
        }
    }

    private static Color? FindDistinctColor(IReadOnlyList<RankedHue> rankedBuckets, int primaryIndex, int? excludedIndex)
    {
        foreach (var candidate in rankedBuckets)
        {
            if (candidate.Index == excludedIndex || HueDistance(candidate.Index, primaryIndex) < 3)
            {
                continue;
            }

            return CreateBackdropColor(candidate.Bucket);
        }

        return null;
    }

    private static int? FindClosestBucketIndex(IReadOnlyList<RankedHue> rankedBuckets, Color color)
    {
        if (rankedBuckets.Count == 0)
        {
            return null;
        }

        return rankedBuckets
            .OrderBy(item => ColorDistance(CreateBackdropColor(item.Bucket), color))
            .First()
            .Index;
    }

    private static int HueDistance(int left, int right)
    {
        var direct = Math.Abs(left - right);
        return Math.Min(direct, HueBucketCount - direct);
    }

    private static double ColorDistance(Color left, Color right)
    {
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return Math.Sqrt((red * red) + (green * green) + (blue * blue));
    }

    private static Color CreateBackdropColor(HueBucket bucket)
    {
        return CreateBackdropColor(
            bucket.Red / bucket.Weight,
            bucket.Green / bucket.Weight,
            bucket.Blue / bucket.Weight);
    }

    private static Color CreateBackdropColor(double red, double green, double blue)
    {
        var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        const double saturation = 1.18;
        return Color.FromRgb(
            ToBackdropChannel(luminance + ((red - luminance) * saturation)),
            ToBackdropChannel(luminance + ((green - luminance) * saturation)),
            ToBackdropChannel(luminance + ((blue - luminance) * saturation)));
    }

    private static Color CreateCompanionColor(Color left, Color right, Color fallback)
    {
        var blended = Mix(Mix(left, right, 0.38), fallback, 0.34);
        return CreateBackdropColor(blended.R, blended.G, blended.B);
    }

    private static Color Mix(Color left, Color right, double amount)
    {
        var clamped = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(left.R + ((right.R - left.R) * clamped)),
            (byte)Math.Round(left.G + ((right.G - left.G) * clamped)),
            (byte)Math.Round(left.B + ((right.B - left.B) * clamped)));
    }

    private static byte ToBackdropChannel(double value)
    {
        return (byte)Math.Clamp((value * 0.82) + 18, 28, 206);
    }

    private static double GetHue(byte red, byte green, byte blue, byte max, int chroma)
    {
        if (chroma == 0)
        {
            return 0;
        }

        double hue;
        if (max == red)
        {
            hue = ((green - blue) / (double)chroma) % 6;
        }
        else if (max == green)
        {
            hue = ((blue - red) / (double)chroma) + 2;
        }
        else
        {
            hue = ((red - green) / (double)chroma) + 4;
        }

        hue *= 60;
        return hue < 0 ? hue + 360 : hue;
    }

    private static bool TryGetRequestedSourceKey(Image image, out string sourceKey)
    {
        sourceKey = string.Empty;
        var requestedSource = PosterCacheImageBehavior.GetSource(image);
        if (string.IsNullOrWhiteSpace(requestedSource))
        {
            return false;
        }

        sourceKey = requestedSource.Trim();
        return sourceKey.Length > 0;
    }

    private static string BuildBitmapCacheKey(BitmapSource bitmap)
    {
        return $"bitmap:{RuntimeHelpers.GetHashCode(bitmap):X}:{bitmap.PixelWidth}x{bitmap.PixelHeight}:{bitmap.Format}";
    }

    private static void CachePalette(string cacheKey, PosterBackdropPalette palette)
    {
        if (!PaletteCache.TryAdd(cacheKey, palette))
        {
            return;
        }

        PaletteCacheOrder.Enqueue(cacheKey);
        while (PaletteCache.Count > MaxPaletteCacheEntries
               && PaletteCacheOrder.TryDequeue(out var oldestKey))
        {
            PaletteCache.TryRemove(oldestKey, out _);
        }
    }

    private readonly record struct RankedHue(int Index, HueBucket Bucket);

    private struct HueBucket
    {
        public double Red;
        public double Green;
        public double Blue;
        public double Weight;

        public void Add(byte red, byte green, byte blue, double weight)
        {
            Red += red * weight;
            Green += green * weight;
            Blue += blue * weight;
            Weight += weight;
        }
    }
}
