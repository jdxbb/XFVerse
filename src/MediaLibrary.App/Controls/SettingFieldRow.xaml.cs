using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MediaLibrary.App.Controls;

[ContentProperty(nameof(FieldContent))]
public partial class SettingFieldRow : UserControl
{
    public SettingFieldRow()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SettingFieldRow), new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingFieldRow), new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty FieldContentProperty =
        DependencyProperty.Register(nameof(FieldContent), typeof(object), typeof(SettingFieldRow), new PropertyMetadata(null));

    public object? FieldContent
    {
        get => GetValue(FieldContentProperty);
        set => SetValue(FieldContentProperty, value);
    }
}
