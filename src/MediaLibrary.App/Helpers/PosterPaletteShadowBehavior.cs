using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MediaLibrary.App.Helpers;

public static class PosterPaletteShadowBehavior
{
    private const int HueBucketCount = 18;
    private const int MaxShadowColorCacheEntries = 2048;
    private const int PaletteDiagnosticsSampleInterval = 20;
    private static readonly Color FallbackShadowColor = Color.FromRgb(25, 34, 48);
    private static readonly TimeSpan PaletteDiagnosticsMinimumInterval = TimeSpan.FromMilliseconds(900);
    private static readonly object DiagnosticsSync = new();
    private static readonly ConcurrentDictionary<string, Color> ShadowColorCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> ShadowColorCacheOrder = new();
    private static readonly ConcurrentDictionary<string, Color> SourceShadowColorCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> SourceShadowColorCacheOrder = new();
    private static readonly Lazy<Color> PlaceholderPosterShadowColor = new(CreatePlaceholderPosterShadowColor);
    private static int _paletteDiagnosticsEventCount;
    private static long _paletteDiagnosticsTotalTicks;
    private static long _paletteDiagnosticsMaxTicks;
    private static long _lastPaletteDiagnosticsTimestamp;

    public static readonly DependencyProperty TargetNameProperty =
        DependencyProperty.RegisterAttached(
            "TargetName",
            typeof(string),
            typeof(PosterPaletteShadowBehavior),
            new PropertyMetadata(null, OnTargetNameChanged));

    public static readonly DependencyProperty TargetElementProperty =
        DependencyProperty.RegisterAttached(
            "TargetElement",
            typeof(UIElement),
            typeof(PosterPaletteShadowBehavior),
            new PropertyMetadata(null, OnTargetElementChanged));

    private static readonly DependencyProperty IsSubscribedProperty =
        DependencyProperty.RegisterAttached(
            "IsSubscribed",
            typeof(bool),
            typeof(PosterPaletteShadowBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty ApplyVersionProperty =
        DependencyProperty.RegisterAttached(
            "ApplyVersion",
            typeof(int),
            typeof(PosterPaletteShadowBehavior),
            new PropertyMetadata(0));

    public static string? GetTargetName(DependencyObject target)
    {
        return (string?)target.GetValue(TargetNameProperty);
    }

    public static void SetTargetName(DependencyObject target, string? value)
    {
        target.SetValue(TargetNameProperty, value);
    }

    public static UIElement? GetTargetElement(DependencyObject target)
    {
        return (UIElement?)target.GetValue(TargetElementProperty);
    }

    public static void SetTargetElement(DependencyObject target, UIElement? value)
    {
        target.SetValue(TargetElementProperty, value);
    }

    private static void OnTargetNameChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Image image)
        {
            return;
        }

        EnsureSubscribed(image);
        BeginApplyShadowColor(image);
    }

    private static void OnTargetElementChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Image image)
        {
            return;
        }

        EnsureSubscribed(image);
        BeginApplyShadowColor(image);
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
            BeginApplyShadowColor(image);
        }
    }

    private static void OnImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            BeginApplyShadowColor(image);
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

    private static void BeginApplyShadowColor(Image image)
    {
        if (!image.IsLoaded)
        {
            return;
        }

        var applyVersion = (int)image.GetValue(ApplyVersionProperty) + 1;
        image.SetValue(ApplyVersionProperty, applyVersion);
        var hasSourceKey = TryGetRequestedSourceKey(image, out var sourceKey);
        if (!hasSourceKey && image.Source is null)
        {
            ApplyPlaceholderShadowColor(image);
            return;
        }

        if (TryApplyCachedSourceShadowColor(image))
        {
            return;
        }

        if (TryApplyCachedBitmapShadowColor(image))
        {
            return;
        }

        if (image.Source is null)
        {
            ResetTargetShadowColor(image);
            return;
        }

        image.Dispatcher.BeginInvoke(
            () => ApplyShadowColor(image, hasSourceKey ? sourceKey : null, applyVersion),
            DispatcherPriority.Loaded);
    }

    private static void ApplyShadowColor(Image image, string? expectedSourceKey, int applyVersion)
    {
        if ((int)image.GetValue(ApplyVersionProperty) != applyVersion
            || !MatchesRequestedSourceKey(image, expectedSourceKey))
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var source = image.Source;
        if (ResolveTargetElement(image) is not UIElement target)
        {
            return;
        }

        var hasPaletteColor = TryGetPosterShadowColor(image, source, out var shadowColor);
        if (!hasPaletteColor)
        {
            ResetTargetShadowColor(image);
            return;
        }

        ApplyShadowColorToTarget(target, shadowColor, source, hasPaletteColor, startedAt);
    }

    private static void ApplyPlaceholderShadowColor(Image image)
    {
        if (ResolveTargetElement(image) is not { } target)
        {
            return;
        }

        ApplyShadowColorToTarget(
            target,
            PlaceholderPosterShadowColor.Value,
            source: null,
            hasPaletteColor: true,
            Stopwatch.GetTimestamp());
    }

    private static void ResetTargetShadowColor(Image image)
    {
        if (ResolveTargetElement(image) is not { } target)
        {
            return;
        }

        ApplyShadowColorToTarget(
            target,
            FallbackShadowColor,
            source: null,
            hasPaletteColor: false,
            Stopwatch.GetTimestamp());
    }

    private static bool MatchesRequestedSourceKey(Image image, string? expectedSourceKey)
    {
        var hasCurrentSourceKey = TryGetRequestedSourceKey(image, out var currentSourceKey);
        return expectedSourceKey is null
            ? !hasCurrentSourceKey
            : hasCurrentSourceKey && string.Equals(currentSourceKey, expectedSourceKey, StringComparison.Ordinal);
    }

    private static Color CreatePlaceholderPosterShadowColor()
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
            var placeholderImage = new Image { Source = bitmap };
            return TryGetPosterShadowColor(placeholderImage, bitmap, out var color)
                ? color
                : FallbackShadowColor;
        }
        catch
        {
            return FallbackShadowColor;
        }
    }

    private static bool TryApplyCachedSourceShadowColor(Image image)
    {
        if (!TryGetRequestedSourceKey(image, out var sourceKey) ||
            !SourceShadowColorCache.TryGetValue(sourceKey, out var shadowColor) ||
            ResolveTargetElement(image) is not UIElement target)
        {
            return false;
        }

        ApplyShadowColorToTarget(target, shadowColor, image.Source, hasPaletteColor: true, Stopwatch.GetTimestamp());
        return true;
    }

    private static bool TryApplyCachedBitmapShadowColor(Image image)
    {
        if (image.Source is not BitmapSource bitmap ||
            !ShadowColorCache.TryGetValue(BuildShadowColorCacheKey(bitmap), out var shadowColor) ||
            ResolveTargetElement(image) is not UIElement target)
        {
            return false;
        }

        if (TryGetRequestedSourceKey(image, out var sourceKey))
        {
            TryCacheSourceShadowColor(sourceKey, shadowColor);
        }

        ApplyShadowColorToTarget(target, shadowColor, image.Source, hasPaletteColor: true, Stopwatch.GetTimestamp());
        return true;
    }

    private static void ApplyShadowColorToTarget(
        UIElement target,
        Color shadowColor,
        ImageSource? source,
        bool hasPaletteColor,
        long startedAt)
    {
        var targetKind = "none";
        if (target is Image shadowImage)
        {
            targetKind = "cached-image";
            PosterCachedShadowBehavior.SetShadowColor(shadowImage, shadowColor);
        }
        else if (target.Effect is DropShadowEffect effect)
        {
            targetKind = "live-effect";
            var mutableEffect = effect.CloneCurrentValue();
            mutableEffect.Color = shadowColor;
            target.SetCurrentValue(UIElement.EffectProperty, mutableEffect);
        }
        else
        {
            return;
        }

        RecordPaletteShadowDiagnostics(source, Stopwatch.GetElapsedTime(startedAt), hasPaletteColor, targetKind);
    }

    private static UIElement? ResolveTargetElement(Image image)
    {
        if (GetTargetElement(image) is { } targetElement)
        {
            return targetElement;
        }

        var targetName = GetTargetName(image);
        return string.IsNullOrWhiteSpace(targetName)
            ? null
            : FindNamedElement(image, targetName);
    }

    private static FrameworkElement? FindNamedElement(FrameworkElement source, string targetName)
    {
        if (source.FindName(targetName) is FrameworkElement directMatch)
        {
            return directMatch;
        }

        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element
                && element.FindName(targetName) is FrameworkElement match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool TryGetPosterShadowColor(Image image, ImageSource? source, out Color color)
    {
        color = FallbackShadowColor;
        var hasSourceKey = TryGetRequestedSourceKey(image, out var sourceKey);
        if (hasSourceKey && SourceShadowColorCache.TryGetValue(sourceKey, out color))
        {
            return true;
        }

        if (source is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return false;
        }

        var cacheKey = BuildShadowColorCacheKey(bitmap);
        if (ShadowColorCache.TryGetValue(cacheKey, out color))
        {
            if (hasSourceKey)
            {
                TryCacheSourceShadowColor(sourceKey, color);
            }

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

            var stepX = Math.Max(1, width / 36);
            var stepY = Math.Max(1, height / 54);
            var hueBuckets = new HueBucket[HueBucketCount];
            double red = 0;
            double green = 0;
            double blue = 0;
            double weightTotal = 0;
            double visiblePixelCount = 0;
            double luminanceTotal = 0;
            double chromaticWeightTotal = 0;

            for (var y = 0; y < height; y += stepY)
            {
                var row = y * stride;
                for (var x = 0; x < width; x += stepX)
                {
                    var index = row + (x * 4);
                    var b = pixels[index];
                    var g = pixels[index + 1];
                    var r = pixels[index + 2];
                    var a = pixels[index + 3];
                    if (a < 32)
                    {
                        continue;
                    }

                    var max = Math.Max(r, Math.Max(g, b));
                    var min = Math.Min(r, Math.Min(g, b));
                    var saturation = max == 0 ? 0 : (max - min) / (double)max;
                    var luminance = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                    var chroma = max - min;
                    var midtoneLift = 1.0 - Math.Abs(luminance - 128) / 128.0;
                    var weight = 0.35
                        + (saturation * 3.1)
                        + (chroma / 255.0 * 1.4)
                        + (Math.Max(0, midtoneLift) * 0.65)
                        + (Math.Min(luminance, 190) / 255.0 * 0.35);

                    red += r * weight;
                    green += g * weight;
                    blue += b * weight;
                    weightTotal += weight;
                    visiblePixelCount++;
                    luminanceTotal += luminance;

                    if (saturation >= 0.18 && chroma >= 24 && luminance >= 24)
                    {
                        var hue = GetHue(r, g, b, max, min, chroma);
                        var bucketIndex = Math.Clamp((int)Math.Floor(hue / 360.0 * HueBucketCount), 0, HueBucketCount - 1);
                        var chromaticWeight = weight * (0.85 + (saturation * 1.45) + (chroma / 255.0));
                        hueBuckets[bucketIndex].Add(r, g, b, chromaticWeight);
                        chromaticWeightTotal += chromaticWeight;
                    }
                }
            }

            if (weightTotal <= 0)
            {
                return false;
            }

            var averageLuminance = visiblePixelCount <= 0 ? 0 : luminanceTotal / visiblePixelCount;
            if (TryCreateDominantHueShadowColor(hueBuckets, visiblePixelCount, chromaticWeightTotal, out color))
            {
                TryCacheShadowColor(cacheKey, color);
                if (hasSourceKey)
                {
                    TryCacheSourceShadowColor(sourceKey, color);
                }

                return true;
            }

            if (averageLuminance < 58 && chromaticWeightTotal < visiblePixelCount * 0.9)
            {
                color = CreateDarkNeutralShadowColor(averageLuminance);
                TryCacheShadowColor(cacheKey, color);
                if (hasSourceKey)
                {
                    TryCacheSourceShadowColor(sourceKey, color);
                }

                return true;
            }

            color = CreateTintedShadowColor(red / weightTotal, green / weightTotal, blue / weightTotal);
            TryCacheShadowColor(cacheKey, color);
            if (hasSourceKey)
            {
                TryCacheSourceShadowColor(sourceKey, color);
            }

            return true;
        }
        catch
        {
            color = FallbackShadowColor;
            return false;
        }
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

    private static string BuildShadowColorCacheKey(BitmapSource bitmap)
    {
        return $"{RuntimeHelpers.GetHashCode(bitmap):X}:{bitmap.PixelWidth}x{bitmap.PixelHeight}:{bitmap.Format}";
    }

    private static void TryCacheShadowColor(string cacheKey, Color color)
    {
        if (ShadowColorCache.TryAdd(cacheKey, color))
        {
            ShadowColorCacheOrder.Enqueue(cacheKey);
            while (ShadowColorCache.Count > MaxShadowColorCacheEntries
                   && ShadowColorCacheOrder.TryDequeue(out var oldestKey))
            {
                ShadowColorCache.TryRemove(oldestKey, out _);
            }
        }
    }

    private static void TryCacheSourceShadowColor(string sourceKey, Color color)
    {
        if (SourceShadowColorCache.TryAdd(sourceKey, color))
        {
            SourceShadowColorCacheOrder.Enqueue(sourceKey);
            while (SourceShadowColorCache.Count > MaxShadowColorCacheEntries
                   && SourceShadowColorCacheOrder.TryDequeue(out var oldestKey))
            {
                SourceShadowColorCache.TryRemove(oldestKey, out _);
            }
        }
    }

    private static void RecordPaletteShadowDiagnostics(ImageSource? source, TimeSpan elapsed, bool hasPaletteColor, string targetKind)
    {
        string message;
        lock (DiagnosticsSync)
        {
            _paletteDiagnosticsEventCount++;
            _paletteDiagnosticsTotalTicks += elapsed.Ticks;
            _paletteDiagnosticsMaxTicks = Math.Max(_paletteDiagnosticsMaxTicks, elapsed.Ticks);

            var now = Stopwatch.GetTimestamp();
            var isSlow = elapsed.TotalMilliseconds >= 8d;
            if (!isSlow
                && (_paletteDiagnosticsEventCount < PaletteDiagnosticsSampleInterval
                    || (_lastPaletteDiagnosticsTimestamp > 0
                        && Stopwatch.GetElapsedTime(_lastPaletteDiagnosticsTimestamp, now) < PaletteDiagnosticsMinimumInterval)))
            {
                return;
            }

            var sampleCount = Math.Max(1, _paletteDiagnosticsEventCount);
            var averageMs = TimeSpan.FromTicks(_paletteDiagnosticsTotalTicks / sampleCount).TotalMilliseconds;
            var maxMs = TimeSpan.FromTicks(_paletteDiagnosticsMaxTicks).TotalMilliseconds;
            message =
                $"source={GetImageSourceId(source)} target={targetKind} pixels={GetPixelSize(source)} " +
                $"elapsedMs={elapsed.TotalMilliseconds:0} avgMs={averageMs:0} maxMs={maxMs:0} samples={sampleCount} " +
                $"computed={hasPaletteColor.ToString().ToLowerInvariant()} slow={isSlow.ToString().ToLowerInvariant()}";

            _paletteDiagnosticsEventCount = 0;
            _paletteDiagnosticsTotalTicks = 0;
            _paletteDiagnosticsMaxTicks = 0;
            _lastPaletteDiagnosticsTimestamp = now;
        }

        PosterCacheDiagnostics.Write("palette-shadow", message);
    }

    private static string GetImageSourceId(ImageSource? source)
    {
        return source switch
        {
            BitmapImage { UriSource: { } uriSource } => PosterCacheDiagnostics.SourceId(uriSource.ToString()),
            BitmapSource bitmap => $"bitmap:{bitmap.PixelWidth}x{bitmap.PixelHeight}",
            _ => "none"
        };
    }

    private static string GetPixelSize(ImageSource? source)
    {
        return source is BitmapSource bitmap
            ? $"{bitmap.PixelWidth}x{bitmap.PixelHeight}"
            : "0x0";
    }

    private static bool TryCreateDominantHueShadowColor(
        HueBucket[] hueBuckets,
        double visiblePixelCount,
        double chromaticWeightTotal,
        out Color color)
    {
        color = FallbackShadowColor;
        if (visiblePixelCount <= 0 || chromaticWeightTotal < visiblePixelCount * 0.42)
        {
            return false;
        }

        var bestIndex = 0;
        var bestClusterWeight = 0d;
        for (var index = 0; index < hueBuckets.Length; index++)
        {
            var clusterWeight = GetBucket(hueBuckets, index - 1).Weight
                + hueBuckets[index].Weight
                + GetBucket(hueBuckets, index + 1).Weight;
            if (clusterWeight > bestClusterWeight)
            {
                bestClusterWeight = clusterWeight;
                bestIndex = index;
            }
        }

        if (bestClusterWeight < visiblePixelCount * 0.18
            || bestClusterWeight < chromaticWeightTotal * 0.24)
        {
            return false;
        }

        var cluster = HueBucket.Combine(
            GetBucket(hueBuckets, bestIndex - 1),
            hueBuckets[bestIndex],
            GetBucket(hueBuckets, bestIndex + 1));
        if (cluster.Weight <= 0)
        {
            return false;
        }

        color = CreateTintedShadowColor(
            cluster.Red / cluster.Weight,
            cluster.Green / cluster.Weight,
            cluster.Blue / cluster.Weight);
        return true;
    }

    private static HueBucket GetBucket(HueBucket[] hueBuckets, int index)
    {
        var wrappedIndex = (index + hueBuckets.Length) % hueBuckets.Length;
        return hueBuckets[wrappedIndex];
    }

    private static double GetHue(byte red, byte green, byte blue, byte max, byte min, int chroma)
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

    private static Color CreateTintedShadowColor(double red, double green, double blue)
    {
        var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        var saturationBoost = 2.05;
        var brightnessBoost = 1.32;
        var liftedLuminance = Math.Max(luminance, 72);

        return Color.FromRgb(
            ToShadowChannel(liftedLuminance + ((red - luminance) * saturationBoost), brightnessBoost),
            ToShadowChannel(liftedLuminance + ((green - luminance) * saturationBoost), brightnessBoost),
            ToShadowChannel(liftedLuminance + ((blue - luminance) * saturationBoost), brightnessBoost));
    }

    private static byte ToShadowChannel(double value, double brightnessBoost)
    {
        return (byte)Math.Clamp((value * brightnessBoost) + 24, 34, 238);
    }

    private static Color CreateDarkNeutralShadowColor(double luminance)
    {
        var channel = (byte)Math.Clamp((luminance * 0.55) + 18, 22, 58);
        return Color.FromRgb(channel, (byte)Math.Clamp(channel + 4, 24, 64), (byte)Math.Clamp(channel + 10, 30, 76));
    }

    private struct HueBucket
    {
        public double Red { get; private set; }

        public double Green { get; private set; }

        public double Blue { get; private set; }

        public double Weight { get; private set; }

        public void Add(byte red, byte green, byte blue, double weight)
        {
            Red += red * weight;
            Green += green * weight;
            Blue += blue * weight;
            Weight += weight;
        }

        public static HueBucket Combine(HueBucket first, HueBucket second, HueBucket third)
        {
            return new HueBucket
            {
                Red = first.Red + second.Red + third.Red,
                Green = first.Green + second.Green + third.Green,
                Blue = first.Blue + second.Blue + third.Blue,
                Weight = first.Weight + second.Weight + third.Weight
            };
        }
    }
}
