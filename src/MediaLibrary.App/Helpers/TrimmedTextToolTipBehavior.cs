using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MediaLibrary.App.Helpers;

public static class TrimmedTextToolTipBehavior
{
    public static readonly DependencyProperty FullTextProperty =
        DependencyProperty.RegisterAttached(
            "FullText",
            typeof(string),
            typeof(TrimmedTextToolTipBehavior),
            new PropertyMetadata(string.Empty, OnToolTipSourceChanged));

    public static readonly DependencyProperty VisibleTextProperty =
        DependencyProperty.RegisterAttached(
            "VisibleText",
            typeof(string),
            typeof(TrimmedTextToolTipBehavior),
            new PropertyMetadata(string.Empty, OnToolTipSourceChanged));

    public static string GetFullText(DependencyObject element)
    {
        return (string)element.GetValue(FullTextProperty);
    }

    public static void SetFullText(DependencyObject element, string value)
    {
        element.SetValue(FullTextProperty, value);
    }

    public static string GetVisibleText(DependencyObject element)
    {
        return (string)element.GetValue(VisibleTextProperty);
    }

    public static void SetVisibleText(DependencyObject element, string value)
    {
        element.SetValue(VisibleTextProperty, value);
    }

    private static void OnToolTipSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded;
        element.SizeChanged -= OnElementSizeChanged;
        element.Unloaded -= OnElementUnloaded;

        if (HasToolTipSource(element))
        {
            element.Loaded += OnElementLoaded;
            element.SizeChanged += OnElementSizeChanged;
            element.Unloaded += OnElementUnloaded;
        }

        UpdateToolTip(element);
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdateToolTip(element);
        }
    }

    private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdateToolTip(element);
        }
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded;
        element.SizeChanged -= OnElementSizeChanged;
        element.Unloaded -= OnElementUnloaded;
    }

    private static void UpdateToolTip(FrameworkElement owner)
    {
        var fullText = GetFullText(owner);
        if (string.IsNullOrWhiteSpace(fullText))
        {
            owner.ToolTip = null;
            return;
        }

        var textBlock = owner as TextBlock ?? FindVisualChild<TextBlock>(owner);
        if (textBlock is null || textBlock.ActualWidth <= 0)
        {
            owner.ToolTip = null;
            return;
        }

        var visibleText = GetVisibleText(owner);
        if (string.IsNullOrWhiteSpace(visibleText))
        {
            visibleText = GetInlineText(textBlock);
        }

        var logicallyTruncated = !string.Equals(Normalize(fullText), Normalize(visibleText), StringComparison.Ordinal);
        owner.ToolTip = logicallyTruncated || IsTextVisuallyTrimmed(textBlock, visibleText)
            ? fullText
            : null;
    }

    private static bool IsTextVisuallyTrimmed(TextBlock textBlock, string visibleText)
    {
        if (textBlock.TextTrimming == TextTrimming.None || string.IsNullOrWhiteSpace(visibleText))
        {
            return false;
        }

        var typeface = new Typeface(
            textBlock.FontFamily,
            textBlock.FontStyle,
            textBlock.FontWeight,
            textBlock.FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;
        var formattedText = new FormattedText(
            visibleText,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            typeface,
            textBlock.FontSize,
            Brushes.Transparent,
            pixelsPerDip);

        if (textBlock.TextWrapping != TextWrapping.NoWrap && textBlock.ActualHeight > 0)
        {
            formattedText.MaxTextWidth = Math.Max(textBlock.ActualWidth, 0d);
            formattedText.Trimming = textBlock.TextTrimming;
            return formattedText.Height > textBlock.ActualHeight + 1d;
        }

        return formattedText.WidthIncludingTrailingWhitespace > textBlock.ActualWidth + 1d;
    }

    private static bool HasToolTipSource(DependencyObject element)
    {
        return !string.IsNullOrWhiteSpace(GetFullText(element));
    }

    private static string GetInlineText(TextBlock textBlock)
    {
        if (!string.IsNullOrEmpty(textBlock.Text))
        {
            return textBlock.Text;
        }

        var builder = new StringBuilder();
        AppendInlineText(textBlock.Inlines, builder);
        return builder.ToString();
    }

    private static void AppendInlineText(InlineCollection inlines, StringBuilder builder)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Text);
                    break;
                case Span span:
                    AppendInlineText(span.Inlines, builder);
                    break;
                case LineBreak:
                    builder.AppendLine();
                    break;
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
