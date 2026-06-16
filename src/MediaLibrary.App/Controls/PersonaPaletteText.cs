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

        var fill = new LinearGradientBrush(inkTop, inkBottom, new Point(0.5d, 0d), new Point(0.5d, 1d));
        fill.Freeze();
        var outlineBrush = new SolidColorBrush(Color.FromArgb(220, outline.R, outline.G, outline.B));
        outlineBrush.Freeze();
        var outlinePen = new Pen(outlineBrush, 1d)
        {
            LineJoin = PenLineJoin.Round
        };
        outlinePen.Freeze();
        var shadowSource = isDarkTheme
            ? SelectLightest(palette)
            : SelectDarkest(palette);
        var shadowCore = isDarkTheme
            ? Mix(shadowSource, Colors.White, 0.22d)
            : Mix(shadowSource, Colors.Black, 0.42d);
        var shadowAuraBrush = CreateFrozenBrush(Color.FromArgb(
            isDarkTheme ? (byte)92 : (byte)54,
            shadowCore.R,
            shadowCore.G,
            shadowCore.B));
        var shadowSoftBrush = CreateFrozenBrush(Color.FromArgb(
            isDarkTheme ? (byte)132 : (byte)82,
            shadowCore.R,
            shadowCore.G,
            shadowCore.B));
        var shadowCoreBrush = CreateFrozenBrush(Color.FromArgb(
            isDarkTheme ? (byte)174 : (byte)108,
            shadowCore.R,
            shadowCore.G,
            shadowCore.B));
        DrawTranslatedGeometry(drawingContext, geometry, shadowAuraBrush, -2.2d, 8d);
        DrawTranslatedGeometry(drawingContext, geometry, shadowAuraBrush, 2.2d, 8d);
        DrawTranslatedGeometry(drawingContext, geometry, shadowSoftBrush, -1.1d, 6d);
        DrawTranslatedGeometry(drawingContext, geometry, shadowSoftBrush, 1.1d, 6d);
        DrawTranslatedGeometry(drawingContext, geometry, shadowCoreBrush, 0d, 3d);
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

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void DrawTranslatedGeometry(
        DrawingContext drawingContext,
        Geometry geometry,
        Brush brush,
        double offsetX,
        double offsetY)
    {
        drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
        drawingContext.DrawGeometry(brush, null, geometry);
        drawingContext.Pop();
    }

}
