using System.Windows;
using System.Windows.Controls;

namespace MediaLibrary.App.Controls;

public partial class CacheCategoryCard : UserControl
{
    public CacheCategoryCard()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(CacheCategoryCard), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(CacheCategoryCard), new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty UsageLabelProperty =
        DependencyProperty.Register(nameof(UsageLabel), typeof(string), typeof(CacheCategoryCard), new PropertyMetadata("当前占用"));

    public string UsageLabel
    {
        get => (string)GetValue(UsageLabelProperty);
        set => SetValue(UsageLabelProperty, value);
    }

    public static readonly DependencyProperty UsageTextProperty =
        DependencyProperty.Register(nameof(UsageText), typeof(string), typeof(CacheCategoryCard), new PropertyMetadata(string.Empty));

    public string UsageText
    {
        get => (string)GetValue(UsageTextProperty);
        set => SetValue(UsageTextProperty, value);
    }

    public static readonly DependencyProperty DetailTextProperty =
        DependencyProperty.Register(nameof(DetailText), typeof(string), typeof(CacheCategoryCard), new PropertyMetadata(string.Empty));

    public string DetailText
    {
        get => (string)GetValue(DetailTextProperty);
        set => SetValue(DetailTextProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(CacheCategoryCard), new PropertyMetadata(string.Empty));

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(nameof(ActionContent), typeof(object), typeof(CacheCategoryCard), new PropertyMetadata(null));

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}
