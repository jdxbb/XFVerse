using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MediaLibrary.App.Controls;

public partial class CorrectionDialogShell : UserControl
{
    public CorrectionDialogShell()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(CorrectionDialogShell), new PropertyMetadata(false));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(CorrectionDialogShell), new PropertyMetadata("识别修正"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(string), typeof(CorrectionDialogShell), new PropertyMetadata(string.Empty));

    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(CorrectionDialogShell), new PropertyMetadata(string.Empty));

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly DependencyProperty DialogContentProperty =
        DependencyProperty.Register(nameof(DialogContent), typeof(object), typeof(CorrectionDialogShell), new PropertyMetadata(null));

    public object? DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    public static readonly DependencyProperty FooterContentProperty =
        DependencyProperty.Register(nameof(FooterContent), typeof(object), typeof(CorrectionDialogShell), new PropertyMetadata(null));

    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(CorrectionDialogShell), new PropertyMetadata(null));

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(CorrectionDialogShell), new PropertyMetadata(null));

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public static readonly DependencyProperty CancelTextProperty =
        DependencyProperty.Register(nameof(CancelText), typeof(string), typeof(CorrectionDialogShell), new PropertyMetadata("取消"));

    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public static readonly DependencyProperty UseDefaultActionsProperty =
        DependencyProperty.Register(nameof(UseDefaultActions), typeof(bool), typeof(CorrectionDialogShell), new PropertyMetadata(true));

    public bool UseDefaultActions
    {
        get => (bool)GetValue(UseDefaultActionsProperty);
        set => SetValue(UseDefaultActionsProperty, value);
    }

    public static readonly DependencyProperty DialogWidthProperty =
        DependencyProperty.Register(nameof(DialogWidth), typeof(double), typeof(CorrectionDialogShell), new PropertyMetadata(980d));

    public double DialogWidth
    {
        get => (double)GetValue(DialogWidthProperty);
        set => SetValue(DialogWidthProperty, value);
    }

    public static readonly DependencyProperty DialogMaxHeightProperty =
        DependencyProperty.Register(nameof(DialogMaxHeight), typeof(double), typeof(CorrectionDialogShell), new PropertyMetadata(720d));

    public double DialogMaxHeight
    {
        get => (double)GetValue(DialogMaxHeightProperty);
        set => SetValue(DialogMaxHeightProperty, value);
    }
}
