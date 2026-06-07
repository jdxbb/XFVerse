using System.Windows;
using System.Windows.Controls;

namespace MediaLibrary.App.Controls;

public partial class ScanPathCard : UserControl
{
    public ScanPathCard()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ScanPathCard), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty PathTextProperty =
        DependencyProperty.Register(nameof(PathText), typeof(string), typeof(ScanPathCard), new PropertyMetadata(string.Empty));

    public string PathText
    {
        get => (string)GetValue(PathTextProperty);
        set => SetValue(PathTextProperty, value);
    }

    public static readonly DependencyProperty DetailTextProperty =
        DependencyProperty.Register(nameof(DetailText), typeof(string), typeof(ScanPathCard), new PropertyMetadata(string.Empty));

    public string DetailText
    {
        get => (string)GetValue(DetailTextProperty);
        set => SetValue(DetailTextProperty, value);
    }

    public static readonly DependencyProperty ActionsContentProperty =
        DependencyProperty.Register(nameof(ActionsContent), typeof(object), typeof(ScanPathCard), new PropertyMetadata(null));

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }
}
