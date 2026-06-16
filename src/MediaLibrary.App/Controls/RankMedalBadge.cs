using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaLibrary.App.Controls;

public sealed class RankMedalBadge : FrameworkElement
{
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
        var style = RankNumberStyle.ForRank(rank);
        var fontSize = CalculateFontSize(text, bounds);
        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily("Segoe UI Variable Display, Segoe UI, Arial"),
                FontStyles.Normal,
                FontWeights.Black,
                FontStretches.Normal),
            fontSize,
            style.FillBrush,
            pixelsPerDip)
        {
            TextAlignment = TextAlignment.Center
        };

        var measuredGeometry = formattedText.BuildGeometry(new Point());
        var measuredBounds = measuredGeometry.Bounds;
        var medalCenter = new Point(bounds.Left + bounds.Width / 2d, bounds.Top + bounds.Height / 2d);
        var offsetRatio = GetNumberOffsetRatio(rank);
        var origin = new Point(
            medalCenter.X - measuredBounds.Left - (measuredBounds.Width / 2d) + (bounds.Height * offsetRatio.X),
            medalCenter.Y - measuredBounds.Top - (measuredBounds.Height / 2d) + (bounds.Height * offsetRatio.Y));
        var geometry = formattedText.BuildGeometry(origin);
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

    private static Point GetNumberOffsetRatio(int rank)
    {
        return rank switch
        {
            1 => new Point(-0.012d, -0.052d),
            2 => new Point(-0.014d, -0.066d),
            3 => new Point(-0.014d, -0.078d),
            _ => new Point(0d, -0.078d)
        };
    }

    private sealed record RankNumberStyle(
        SolidColorBrush FillBrush,
        SolidColorBrush OutlineBrush,
        SolidColorBrush ShadowBrush,
        SolidColorBrush HighlightBrush)
    {
        public static RankNumberStyle ForRank(int rank)
        {
            return rank switch
            {
                1 => Create("#6A3F00", "#4DFFF1B8", "#33000000", "#2EFFFFFF"),
                2 => Create("#3F4652", "#47FFFFFF", "#2E000000", "#33FFFFFF"),
                3 => Create("#5B2E16", "#40FFD8B0", "#33000000", "#26FFFFFF"),
                _ => Create("#4B5563", "#40FFFFFF", "#29000000", "#26FFFFFF")
            };
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
