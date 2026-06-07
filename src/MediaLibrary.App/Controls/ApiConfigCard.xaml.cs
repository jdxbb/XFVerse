using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MediaLibrary.App.Controls;

[ContentProperty(nameof(FieldsContent))]
public partial class ApiConfigCard : UserControl
{
    public ApiConfigCard()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ApiConfigCard), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(ApiConfigCard), new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(ApiConfigCard), new PropertyMetadata(string.Empty));

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly DependencyProperty StatusKindProperty =
        DependencyProperty.Register(nameof(StatusKind), typeof(string), typeof(ApiConfigCard), new PropertyMetadata("untested"));

    public string StatusKind
    {
        get => (string)GetValue(StatusKindProperty);
        set => SetValue(StatusKindProperty, value);
    }

    public static readonly DependencyProperty FeedbackTextProperty =
        DependencyProperty.Register(nameof(FeedbackText), typeof(string), typeof(ApiConfigCard), new PropertyMetadata(string.Empty));

    public string FeedbackText
    {
        get => (string)GetValue(FeedbackTextProperty);
        set => SetValue(FeedbackTextProperty, value);
    }

    public static readonly DependencyProperty FieldsContentProperty =
        DependencyProperty.Register(nameof(FieldsContent), typeof(object), typeof(ApiConfigCard), new PropertyMetadata(null));

    public object? FieldsContent
    {
        get => GetValue(FieldsContentProperty);
        set => SetValue(FieldsContentProperty, value);
    }

    public static readonly DependencyProperty ActionsContentProperty =
        DependencyProperty.Register(nameof(ActionsContent), typeof(object), typeof(ApiConfigCard), new PropertyMetadata(null));

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }
}
