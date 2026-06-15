using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MediaLibrary.App.Helpers;

namespace MediaLibrary.App.Controls;

public sealed class PersonaPaletteText : FrameworkElement
{
    private static readonly PosterBackdropPalette DefaultPalette = new(
        Color.FromRgb(42, 65, 92),
        Color.FromRgb(78, 103, 132),
        Color.FromRgb(108, 82, 114));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(PersonaPaletteText),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaletteProperty = DependencyProperty.Register(
        nameof(Palette),
        typeof(PosterBackdropPalette),
        typeof(PersonaPaletteText),
        new FrameworkPropertyMetadata(DefaultPalette, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(PersonaPaletteText),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(PersonaPaletteText),
        new FrameworkPropertyMetadata(56d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(PersonaPaletteText),
        new FrameworkPropertyMetadata(FontWeights.ExtraBold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThemeForegroundProperty = DependencyProperty.Register(
        nameof(ThemeForeground),
        typeof(Brush),
        typeof(PersonaPaletteText),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PosterBackdropPalette Palette
    {
        get => (PosterBackdropPalette)GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public Brush ThemeForeground
    {
        get => (Brush)GetValue(ThemeForegroundProperty);
        set => SetValue(ThemeForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = CreateFormattedText();
        return new Size(Math.Ceiling(text.WidthIncludingTrailingWhitespace + 20d), Math.Ceiling(text.Height + 18d));
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var text = CreateFormattedText();
        var origin = new Point(
            Math.Max(0d, (RenderSize.Width - text.WidthIncludingTrailingWhitespace) / 2d),
            Math.Max(0d, (RenderSize.Height - text.Height) / 2d));
        var geometry = text.BuildGeometry(origin);
        var palette = Palette;
        var isDarkTheme = ThemeForeground is SolidColorBrush themeBrush
                          && GetLuminance(themeBrush.Color) >= 150d;
        var inkTop = isDarkTheme
            ? Mix(Color.FromRgb(244, 245, 248), SelectLightest(palette), 0.16d)
            : Mix(Color.FromRgb(48, 56, 68), SelectLightest(palette), 0.12d);
        var inkBottom = isDarkTheme
            ? Mix(Color.FromRgb(205, 209, 218), palette.Primary, 0.14d)
            : Mix(Color.FromRgb(14, 21, 31), palette.Secondary, 0.18d);
        var outline = isDarkTheme
            ? Mix(SelectDarkest(palette), Colors.Black, 0.32d)
            : Mix(SelectLightest(palette), Colors.White, 0.46d);
        var shadow = isDarkTheme
            ? Mix(SelectLightest(palette), Colors.White, 0.48d)
            : Mix(palette.Accent, SelectDarkest(palette), 0.18d);

        DrawProjectedShadow(drawingContext, geometry, shadow, isDarkTheme);

        var fill = new LinearGradientBrush(inkTop, inkBottom, new Point(0.5d, 0d), new Point(0.5d, 1d));
        fill.Freeze();
        var outlineBrush = new SolidColorBrush(Color.FromArgb(220, outline.R, outline.G, outline.B));
        outlineBrush.Freeze();
        var outlinePen = new Pen(outlineBrush, 1d)
        {
            LineJoin = PenLineJoin.Round
        };
        outlinePen.Freeze();
        drawingContext.DrawGeometry(fill, outlinePen, geometry);
    }

    private FormattedText CreateFormattedText()
    {
        return new FormattedText(
            Text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal),
            FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static void DrawProjectedShadow(
        DrawingContext drawingContext,
        Geometry geometry,
        Color color,
        bool isDarkTheme)
    {
        var glowBrush = new SolidColorBrush(Color.FromArgb(
            isDarkTheme ? (byte)82 : (byte)62,
            color.R,
            color.G,
            color.B));
        glowBrush.Freeze();
        var glowPen = new Pen(glowBrush, isDarkTheme ? 6.5d : 5.5d)
        {
            LineJoin = PenLineJoin.Round
        };
        glowPen.Freeze();
        drawingContext.DrawGeometry(null, glowPen, geometry);

        foreach (var (offsetX, offsetY, opacity) in new[]
                 {
                     (-3d, 5d, isDarkTheme ? 0.28d : 0.22d),
                     (3d, 5d, isDarkTheme ? 0.30d : 0.24d),
                     (-2d, 8d, isDarkTheme ? 0.24d : 0.19d),
                     (2d, 8d, isDarkTheme ? 0.24d : 0.19d),
                     (0d, 11d, isDarkTheme ? 0.18d : 0.14d)
                 })
        {
            var shadowGeometry = geometry.Clone();
            shadowGeometry.Transform = new TranslateTransform(offsetX, offsetY);
            shadowGeometry.Freeze();
            var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255d * opacity), color.R, color.G, color.B));
            brush.Freeze();
            drawingContext.DrawGeometry(brush, null, shadowGeometry);
        }
    }

    private static Color SelectLightest(PosterBackdropPalette palette)
    {
        return new[] { palette.Primary, palette.Secondary, palette.Accent }
            .OrderByDescending(GetLuminance)
            .First();
    }

    private static Color SelectDarkest(PosterBackdropPalette palette)
    {
        return new[] { palette.Primary, palette.Secondary, palette.Accent }
            .OrderBy(GetLuminance)
            .First();
    }

    private static double GetLuminance(Color color)
    {
        return (0.2126d * color.R) + (0.7152d * color.G) + (0.0722d * color.B);
    }

    private static Color Mix(Color left, Color right, double rightWeight)
    {
        var weight = Math.Clamp(rightWeight, 0d, 1d);
        return Color.FromRgb(
            (byte)Math.Round((left.R * (1d - weight)) + (right.R * weight)),
            (byte)Math.Round((left.G * (1d - weight)) + (right.G * weight)),
            (byte)Math.Round((left.B * (1d - weight)) + (right.B * weight)));
    }
}
