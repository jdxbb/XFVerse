using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MediaLibrary.App.Helpers;

public static class TextBoxPlaceholderBehavior
{
    private const double DefaultPlaceholderHorizontalOffset = 2d;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText",
            typeof(string),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(string.Empty));

    private static readonly DependencyPropertyKey IsPlaceholderVisiblePropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsPlaceholderVisible",
            typeof(bool),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(true));

    public static readonly DependencyProperty IsPlaceholderVisibleProperty =
        IsPlaceholderVisiblePropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey PlaceholderHorizontalOffsetPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "PlaceholderHorizontalOffset",
            typeof(double),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(DefaultPlaceholderHorizontalOffset));

    public static readonly DependencyProperty PlaceholderHorizontalOffsetProperty =
        PlaceholderHorizontalOffsetPropertyKey.DependencyProperty;

    private static readonly DependencyProperty IsImeCompositionActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsImeCompositionActive",
            typeof(bool),
            typeof(TextBoxPlaceholderBehavior),
            new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    public static string GetPlaceholderText(DependencyObject target)
    {
        return (string)target.GetValue(PlaceholderTextProperty);
    }

    public static void SetPlaceholderText(DependencyObject target, string value)
    {
        target.SetValue(PlaceholderTextProperty, value);
    }

    public static bool GetIsPlaceholderVisible(DependencyObject target)
    {
        return (bool)target.GetValue(IsPlaceholderVisibleProperty);
    }

    public static double GetPlaceholderHorizontalOffset(DependencyObject target)
    {
        return (double)target.GetValue(PlaceholderHorizontalOffsetProperty);
    }

    private static void OnIsEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            textBox.TextChanged += OnTextChanged;
            textBox.Loaded += OnLoaded;
            textBox.SizeChanged += OnSizeChanged;
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
            textBox.PreviewTextInput += OnPreviewTextInput;
            TextCompositionManager.AddPreviewTextInputStartHandler(textBox, OnPreviewTextInputStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(textBox, OnPreviewTextInputUpdate);
            UpdatePlaceholderVisibility(textBox);
            QueuePlaceholderAlignment(textBox);
        }
        else
        {
            textBox.TextChanged -= OnTextChanged;
            textBox.Loaded -= OnLoaded;
            textBox.SizeChanged -= OnSizeChanged;
            textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
            textBox.PreviewTextInput -= OnPreviewTextInput;
            TextCompositionManager.RemovePreviewTextInputStartHandler(textBox, OnPreviewTextInputStart);
            TextCompositionManager.RemovePreviewTextInputUpdateHandler(textBox, OnPreviewTextInputUpdate);
            textBox.ClearValue(IsImeCompositionActiveProperty);
            textBox.ClearValue(IsPlaceholderVisiblePropertyKey);
            textBox.ClearValue(PlaceholderHorizontalOffsetPropertyKey);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            QueuePlaceholderAlignment(textBox);
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            QueuePlaceholderAlignment(textBox);
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdatePlaceholderVisibility(textBox);
        }
    }

    private static void OnPreviewTextInputStart(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SetValue(IsImeCompositionActiveProperty, true);
            UpdatePlaceholderVisibility(textBox);
        }
    }

    private static void OnPreviewTextInputUpdate(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SetValue(IsImeCompositionActiveProperty, true);
            UpdatePlaceholderVisibility(textBox);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            QueueCompositionReset(textBox);
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SetValue(IsImeCompositionActiveProperty, false);
            UpdatePlaceholderVisibility(textBox);
        }
    }

    private static void QueueCompositionReset(TextBox textBox)
    {
        _ = textBox.Dispatcher.BeginInvoke(
            () =>
            {
                textBox.SetValue(IsImeCompositionActiveProperty, false);
                UpdatePlaceholderVisibility(textBox);
            },
            DispatcherPriority.Input);
    }

    private static void QueuePlaceholderAlignment(TextBox textBox)
    {
        _ = textBox.Dispatcher.BeginInvoke(
            () => UpdatePlaceholderAlignment(textBox),
            DispatcherPriority.Loaded);
    }

    private static void UpdatePlaceholderAlignment(TextBox textBox)
    {
        if (!textBox.IsLoaded)
        {
            return;
        }

        var caretRect = textBox.GetRectFromCharacterIndex(0);
        var contentInset = textBox.BorderThickness.Left + textBox.Padding.Left;
        var horizontalOffset = caretRect.IsEmpty
            ? DefaultPlaceholderHorizontalOffset
            : Math.Max(0d, caretRect.Left - contentInset);
        textBox.SetValue(PlaceholderHorizontalOffsetPropertyKey, horizontalOffset);
    }

    private static void UpdatePlaceholderVisibility(TextBox textBox)
    {
        var isImeCompositionActive = (bool)textBox.GetValue(IsImeCompositionActiveProperty);
        textBox.SetValue(
            IsPlaceholderVisiblePropertyKey,
            string.IsNullOrEmpty(textBox.Text) && !isImeCompositionActive);
    }
}
