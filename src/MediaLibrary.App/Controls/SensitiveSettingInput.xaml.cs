using System.Windows;
using System.Windows.Controls;

namespace MediaLibrary.App.Controls;

public partial class SensitiveSettingInput : UserControl
{
    public SensitiveSettingInput()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(SensitiveSettingInput),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(SensitiveSettingInput), new PropertyMetadata(0));

    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public static readonly DependencyProperty IsRevealedProperty =
        DependencyProperty.Register(nameof(IsRevealed), typeof(bool), typeof(SensitiveSettingInput), new PropertyMetadata(false));

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    private void ToggleReveal_Click(object sender, RoutedEventArgs e)
    {
        IsRevealed = !IsRevealed;
    }
}
