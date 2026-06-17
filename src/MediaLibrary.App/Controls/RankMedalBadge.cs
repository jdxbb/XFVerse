using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaLibrary.App.Controls;

public sealed class RankMedalBadge : FrameworkElement
{
    private const double GoldNumberLeftNudgeRatio = -0.003d;
    private const double GoldNumberTinyUpNudgeRatio = -0.002d;
    private const double SilverNumberUpNudgeRatio = -0.016d;
    private const double BronzeNumberExtraUpNudgeRatio = -0.012d;
    private const double OrdinaryNumberUpNudgeRatio = -0.010d;
    private static readonly FontFamily RankNumberFontFamily = new("Arial Black, Segoe UI Variable Display, Segoe UI, Arial");

    public static readonly DependencyProperty RankProperty = DependencyProperty.Register(
        nameof(Rank),
        typeof(int),
        typeof(RankMedalBadge),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly ImageSource GoldMedal = LoadMedal("\u91D1\u724C.png");
    private static readonly ImageSource SilverMedal = LoadMedal("\u94F6\u724C.png");
    private static readonly ImageSource BronzeMedal = LoadMedal("\u94DC\u724C.png");
    private static readonly ImageSource DefaultMedal = LoadMedal("\u666E\u901A.png");

    public int Rank
    {
        get => (int)GetValue(RankProperty);
        set => SetValue(RankProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(0d, 0d, ActualWidth, ActualHeight);
        if (bounds.Width <= 0d || bounds.Height <= 0d)
        {
            return;
        }

        var rank = Math.Max(1, Rank);
        drawingContext.DrawImage(SelectMedal(rank), bounds);
        DrawRankNumber(drawingContext, rank, bounds, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 58d : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 58d : availableSize.Height;
        return new Size(width, height);
    }

    private static ImageSource LoadMedal(string fileName)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/medals/" + fileName, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ImageSource SelectMedal(int rank)
    {
        return rank switch
        {
            1 => GoldMedal,
            2 => SilverMedal,
            3 => BronzeMedal,
            _ => DefaultMedal
        };
    }

    private static void DrawRankNumber(DrawingContext drawingContext, int rank, Rect bounds, double pixelsPerDip)
    {
        var text = rank.ToString(CultureInfo.InvariantCulture);
        var style = RankNumberStyle.CreateDefault();
        var fontSize = CalculateFontSize(text, bounds);
        var typeface = new Typeface(
            RankNumberFontFamily,
            FontStyles.Normal,
            FontWeights.Black,
            FontStretches.Normal);
        var digitCellWidth = CalculateDigitCellWidth(typeface, fontSize, style.FillBrush, pixelsPerDip, bounds);
        var tabularTextWidth = digitCellWidth * text.Length;
        var layoutWidth = CalculateLayoutWidth(tabularTextWidth, text, bounds);
        var medalCenter = new Point(bounds.Left + bounds.Width / 2d, bounds.Top + bounds.Height / 2d);
        var offsetRatio = GetNumberOffsetRatio(rank);
        var layoutLeft = medalCenter.X - (layoutWidth / 2d) + (bounds.Height * offsetRatio.X);
        var digitLeft = layoutLeft + ((layoutWidth - tabularTextWidth) / 2d);
        var digitCenterY = medalCenter.Y + (bounds.Height * offsetRatio.Y);
        var geometry = BuildTabularNumberGeometry(
            text,
            typeface,
            fontSize,
            style.FillBrush,
            pixelsPerDip,
            digitLeft,
            digitCenterY,
            digitCellWidth);
        var offset = Math.Max(0.4d, bounds.Height * 0.012d);
        var outlineThickness = Math.Max(0.75d, bounds.Height * 0.028d);

        drawingContext.PushTransform(new TranslateTransform(0d, offset));
        drawingContext.DrawGeometry(style.ShadowBrush, null, geometry);
        drawingContext.Pop();

        drawingContext.PushTransform(new TranslateTransform(0d, -offset));
        drawingContext.DrawGeometry(style.HighlightBrush, null, geometry);
        drawingContext.Pop();

        drawingContext.DrawGeometry(null, new Pen(style.OutlineBrush, outlineThickness), geometry);
        drawingContext.DrawGeometry(style.FillBrush, null, geometry);
    }

    private static double CalculateFontSize(string text, Rect bounds)
    {
        var ratio = text.Length switch
        {
            <= 1 => 0.34d,
            2 => 0.28d,
            _ => 0.22d
        };
        return Math.Max(7d, bounds.Height * ratio);
    }

    private static FormattedText CreateFormattedText(
        string text,
        Typeface typeface,
        double fontSize,
        Brush brush,
        double pixelsPerDip)
    {
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush,
            pixelsPerDip)
        {
            TextAlignment = TextAlignment.Left
        };
    }

    private static Geometry BuildTabularNumberGeometry(
        string text,
        Typeface typeface,
        double fontSize,
        Brush brush,
        double pixelsPerDip,
        double left,
        double centerY,
        double digitCellWidth)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (var i = 0; i < text.Length; i++)
        {
            var digitText = CreateFormattedText(text[i].ToString(), typeface, fontSize, brush, pixelsPerDip);
            var digitBounds = digitText.BuildGeometry(new Point()).Bounds;
            var digitOrigin = new Point(
                left + (i * digitCellWidth) + (digitCellWidth / 2d) - digitBounds.Left - (digitBounds.Width / 2d),
                centerY - digitBounds.Top - (digitBounds.Height / 2d));
            group.Children.Add(digitText.BuildGeometry(digitOrigin));
        }

        return group;
    }

    private static double CalculateDigitCellWidth(
        Typeface typeface,
        double fontSize,
        Brush brush,
        double pixelsPerDip,
        Rect bounds)
    {
        var width = 0d;
        for (var digit = 0; digit <= 9; digit++)
        {
            var digitText = CreateFormattedText(digit.ToString(CultureInfo.InvariantCulture), typeface, fontSize, brush, pixelsPerDip);
            var digitBounds = digitText.BuildGeometry(new Point()).Bounds;
            width = Math.Max(width, Math.Max(digitText.WidthIncludingTrailingWhitespace, digitBounds.Width));
        }

        return Math.Max(width, bounds.Height * 0.2d);
    }

    private static double CalculateLayoutWidth(double tabularTextWidth, string text, Rect bounds)
    {
        var minimumRatio = text.Length switch
        {
            <= 1 => 0.48d,
            2 => 0.62d,
            _ => 0.74d
        };
        var sideBearingAllowance = bounds.Height * 0.08d;
        return Math.Max(bounds.Width * minimumRatio, tabularTextWidth + sideBearingAllowance);
    }

    private static Point GetNumberOffsetRatio(int rank)
    {
        return rank switch
        {
            1 => new Point(-0.012d + GoldNumberLeftNudgeRatio, -0.058d + GoldNumberTinyUpNudgeRatio),
            2 => new Point(-0.014d, -0.078d + SilverNumberUpNudgeRatio),
            3 => new Point(-0.014d, -0.102d + SilverNumberUpNudgeRatio + BronzeNumberExtraUpNudgeRatio),
            _ => new Point(0d, -0.090d + OrdinaryNumberUpNudgeRatio)
        };
    }

    private sealed record RankNumberStyle(
        SolidColorBrush FillBrush,
        SolidColorBrush OutlineBrush,
        SolidColorBrush ShadowBrush,
        SolidColorBrush HighlightBrush)
    {
        public static RankNumberStyle CreateDefault()
        {
            return Create("#FFFFF7", "#A8000000", "#73000000", "#40FFFFFF");
        }

        private static RankNumberStyle Create(string fill, string outline, string shadow, string highlight)
        {
            return new RankNumberStyle(
                CreateBrush(fill),
                CreateBrush(outline),
                CreateBrush(shadow),
                CreateBrush(highlight));
        }

        private static SolidColorBrush CreateBrush(string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            return brush;
        }
    }
}
