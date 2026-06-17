using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;

namespace MediaLibrary.App.Controls;

public sealed class PhosphorIcon : FrameworkElement
{
    private const double ViewBoxSize = 256d;
    private const double DefaultSize = 18d;
    private static readonly ConcurrentDictionary<string, Geometry> GeometryCache = new(StringComparer.OrdinalIgnoreCase);

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(string),
            typeof(PhosphorIcon),
            new FrameworkPropertyMetadata("x", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WeightProperty =
        DependencyProperty.Register(
            nameof(Weight),
            typeof(string),
            typeof(PhosphorIcon),
            new FrameworkPropertyMetadata("regular", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(
            typeof(PhosphorIcon),
            new FrameworkPropertyMetadata(
                SystemColors.ControlTextBrush,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Weight
    {
        get => (string)GetValue(WeightProperty);
        set => SetValue(WeightProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsNaN(Width) ? DefaultSize : Width;
        var height = double.IsNaN(Height) ? DefaultSize : Height;

        if (!double.IsInfinity(availableSize.Width))
        {
            width = Math.Min(width, availableSize.Width);
        }

        if (!double.IsInfinity(availableSize.Height))
        {
            height = Math.Min(height, availableSize.Height);
        }

        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0d || ActualHeight <= 0d)
        {
            return;
        }

        var geometry = GetIconGeometry(Icon, Weight);
        if (geometry.IsEmpty())
        {
            return;
        }

        var size = Math.Min(ActualWidth, ActualHeight);
        var scale = size / ViewBoxSize;
        var offsetX = (ActualWidth - size) / 2d;
        var offsetY = (ActualHeight - size) / 2d;

        drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(Foreground, null, geometry);
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static Geometry GetIconGeometry(string? icon, string? weight)
    {
        var normalizedIcon = NormalizeToken(icon, "x");
        var normalizedWeight = NormalizeToken(weight, "regular");

        if (normalizedIcon.EndsWith("-fill", StringComparison.OrdinalIgnoreCase))
        {
            normalizedWeight = "fill";
        }

        if (!string.Equals(normalizedWeight, "fill", StringComparison.OrdinalIgnoreCase))
        {
            normalizedWeight = "regular";
        }

        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{normalizedWeight}/{normalizedIcon}");

        return GeometryCache.GetOrAdd(cacheKey, _ => LoadGeometry(normalizedIcon, normalizedWeight));
    }

    private static string NormalizeToken(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim()
            .Replace('_', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();
    }

    private static Geometry LoadGeometry(string icon, string weight)
    {
        var geometry = TryLoadGeometry(icon, weight);
        if (!geometry.IsEmpty())
        {
            return geometry;
        }

        if (string.Equals(weight, "fill", StringComparison.OrdinalIgnoreCase)
            && !icon.EndsWith("-fill", StringComparison.OrdinalIgnoreCase))
        {
            geometry = TryLoadGeometry($"{icon}-fill", weight);
            if (!geometry.IsEmpty())
            {
                return geometry;
            }
        }

        if (!string.Equals(weight, "regular", StringComparison.OrdinalIgnoreCase))
        {
            var regularIcon = icon.EndsWith("-fill", StringComparison.OrdinalIgnoreCase)
                ? icon[..^5]
                : icon;
            geometry = TryLoadGeometry(regularIcon, "regular");
            if (!geometry.IsEmpty())
            {
                return geometry;
            }
        }

        return TryLoadGeometry("x", "regular");
    }

    private static Geometry TryLoadGeometry(string icon, string weight)
    {
        try
        {
            var resourcePath = $"Assets/Icons/Phosphor/{weight}/{icon}.svg";
            var streamInfo = Application.GetResourceStream(new Uri(resourcePath, UriKind.Relative));
            streamInfo ??= Application.GetResourceStream(new Uri($"pack://application:,,,/{resourcePath}", UriKind.Absolute));
            if (streamInfo is null)
            {
                return CreateEmptyGeometry();
            }

            using var stream = streamInfo.Stream;
            using var reader = new StreamReader(stream);
            var document = XDocument.Parse(reader.ReadToEnd(), LoadOptions.None);
            var paths = document
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("d")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Geometry.Parse(value!))
                .ToList();

            Geometry geometry = paths.Count switch
            {
                0 => CreateEmptyGeometry(),
                1 => paths[0],
                _ => new GeometryGroup { Children = new GeometryCollection(paths) }
            };

            geometry.Freeze();
            return geometry;
        }
        catch (Exception)
        {
            return CreateEmptyGeometry();
        }
    }

    private static Geometry CreateEmptyGeometry()
    {
        var geometry = new GeometryGroup();
        geometry.Freeze();
        return geometry;
    }
}
