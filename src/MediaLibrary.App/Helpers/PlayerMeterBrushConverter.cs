using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MediaLibrary.App.Helpers;

public sealed class PlayerMeterBrushConverter : IValueConverter
{
    private static readonly Color LowBlue = Color.FromRgb(142, 203, 255);
    private static readonly Color ProgressBlue = Color.FromRgb(78, 163, 255);
    private static readonly Color HighBlue = Color.FromRgb(28, 94, 214);
    private static readonly Color HighVolumeRed = Color.FromRgb(228, 72, 86);
    private static readonly SolidColorBrush[] BrightnessBrushes = CreateBrushCache(100);
    private static readonly SolidColorBrush[] VolumeBrushes = CreateBrushCache(200);
    private const double ProgressBlueValue = 100d;
    private const double HighBlueValue = 150d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value switch
        {
            int intValue => intValue,
            double doubleValue => doubleValue,
            _ => 0d
        };

        var max = 100d;
        if (parameter is string text
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0d)
        {
            max = parsed;
        }

        return CreateBrush(current, max);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static SolidColorBrush CreateBrush(double current, double max)
    {
        if (TryGetCachedBrush(current, max, out var cachedBrush))
        {
            return cachedBrush;
        }

        var brush = new SolidColorBrush(CreateColor(current, max));
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private static SolidColorBrush[] CreateBrushCache(int max)
    {
        var brushes = new SolidColorBrush[max + 1];
        for (var value = 0; value <= max; value++)
        {
            var brush = new SolidColorBrush(CreateColor(value, max));
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            brushes[value] = brush;
        }

        return brushes;
    }

    private static bool TryGetCachedBrush(double current, double max, out SolidColorBrush brush)
    {
        var roundedCurrent = (int)Math.Round(current);
        var roundedMax = (int)Math.Round(max);
        if (Math.Abs(current - roundedCurrent) > 0.001d || Math.Abs(max - roundedMax) > 0.001d)
        {
            brush = null!;
            return false;
        }

        if (roundedMax == 100)
        {
            brush = BrightnessBrushes[Math.Clamp(roundedCurrent, 0, 100)];
            return true;
        }

        if (roundedMax == 200)
        {
            brush = VolumeBrushes[Math.Clamp(roundedCurrent, 0, 200)];
            return true;
        }

        brush = null!;
        return false;
    }

    private static Color CreateColor(double current, double max)
    {
        var normalizedMax = Math.Max(1d, max);
        var normalizedCurrent = Math.Clamp(current, 0d, normalizedMax);

        if (normalizedMax <= ProgressBlueValue || normalizedCurrent <= ProgressBlueValue)
        {
            var upper = Math.Min(ProgressBlueValue, normalizedMax);
            return Interpolate(LowBlue, ProgressBlue, Math.Clamp(normalizedCurrent / upper, 0d, 1d));
        }

        if (normalizedMax > HighBlueValue && normalizedCurrent > HighBlueValue)
        {
            var redRatio = (normalizedCurrent - HighBlueValue) / (normalizedMax - HighBlueValue);
            return Interpolate(HighBlue, HighVolumeRed, Math.Clamp(redRatio, 0d, 1d));
        }

        var highBlueLimit = Math.Min(HighBlueValue, normalizedMax);
        var boostedRatio = (normalizedCurrent - ProgressBlueValue) / (highBlueLimit - ProgressBlueValue);
        return Interpolate(ProgressBlue, HighBlue, Math.Clamp(boostedRatio, 0d, 1d));
    }

    private static Color Interpolate(Color low, Color high, double ratio)
    {
        return Color.FromRgb(
            (byte)Math.Round(low.R + ((high.R - low.R) * ratio)),
            (byte)Math.Round(low.G + ((high.G - low.G) * ratio)),
            (byte)Math.Round(low.B + ((high.B - low.B) * ratio)));
    }
}

public sealed class PlayerMeterFillHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4
            || !TryGetDouble(values[0], out var value)
            || !TryGetDouble(values[1], out var minimum)
            || !TryGetDouble(values[2], out var maximum)
            || !TryGetDouble(values[3], out var trackHeight)
            || trackHeight <= 0d)
        {
            return 0d;
        }

        var range = maximum - minimum;
        if (range <= 0d)
        {
            return 0d;
        }

        var ratio = Math.Clamp((value - minimum) / range, 0d, 1d);
        return trackHeight * ratio;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool TryGetDouble(object value, out double result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case double doubleValue:
                result = doubleValue;
                return true;
            case decimal decimalValue:
                result = (double)decimalValue;
                return true;
            case string text:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default:
                result = 0d;
                return false;
        }
    }
}
