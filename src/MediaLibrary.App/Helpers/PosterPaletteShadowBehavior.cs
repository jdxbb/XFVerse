using System.ComponentModel;
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
    private static readonly Color FallbackShadowColor = Color.FromRgb(25, 34, 48);

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

        image.Dispatcher.BeginInvoke(
            () => ApplyShadowColor(image),
            DispatcherPriority.Loaded);
    }

    private static void ApplyShadowColor(Image image)
    {
        if (ResolveTargetElement(image) is not UIElement target
            || target.Effect is not DropShadowEffect effect)
        {
            return;
        }

        var shadowColor = TryGetPosterShadowColor(image.Source, out var color)
            ? color
            : FallbackShadowColor;
        var mutableEffect = effect.CloneCurrentValue();
        mutableEffect.Color = shadowColor;
        target.SetCurrentValue(UIElement.EffectProperty, mutableEffect);
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

    private static bool TryGetPosterShadowColor(ImageSource? source, out Color color)
    {
        color = FallbackShadowColor;
        if (source is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return false;
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
                return true;
            }

            if (averageLuminance < 58 && chromaticWeightTotal < visiblePixelCount * 0.9)
            {
                color = CreateDarkNeutralShadowColor(averageLuminance);
                return true;
            }

            color = CreateTintedShadowColor(red / weightTotal, green / weightTotal, blue / weightTotal);
            return true;
        }
        catch
        {
            color = FallbackShadowColor;
            return false;
        }
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
