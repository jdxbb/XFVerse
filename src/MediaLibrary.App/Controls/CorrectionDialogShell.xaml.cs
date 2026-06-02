using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MediaLibrary.App.Controls;

public partial class CorrectionDialogShell : UserControl
{
    private Window? _ownerWindow;
    private UIElement? _ownerTitleBar;
    private bool _ownerTitleBarWasHitTestVisible;

    public CorrectionDialogShell()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LostMouseCapture += OnLostMouseCapture;
    }

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(CorrectionDialogShell),
            new PropertyMetadata(false, OnIsOpenChanged));

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

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(CorrectionDialogShell), new PropertyMetadata(null));

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
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

    public static readonly DependencyProperty FooterSpacingProperty =
        DependencyProperty.Register(nameof(FooterSpacing), typeof(GridLength), typeof(CorrectionDialogShell), new PropertyMetadata(new GridLength(16d)));

    public GridLength FooterSpacing
    {
        get => (GridLength)GetValue(FooterSpacingProperty);
        set => SetValue(FooterSpacingProperty, value);
    }

    private static void OnIsOpenChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is CorrectionDialogShell shell)
        {
            shell.UpdateOwnerWindowInputBlocker();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateOwnerWindowInputBlocker();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachOwnerWindowInputBlocker();
    }

    private void UpdateOwnerWindowInputBlocker()
    {
        if (!IsOpen || !IsLoaded)
        {
            DetachOwnerWindowInputBlocker();
            return;
        }

        var ownerWindow = Window.GetWindow(this);
        if (ReferenceEquals(ownerWindow, _ownerWindow))
        {
            return;
        }

        DetachOwnerWindowInputBlocker();
        _ownerWindow = ownerWindow;
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnOwnerWindowPreviewMouseDown), true);
        _ownerWindow.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OnOwnerWindowPreviewMouseUp), true);
        _ownerWindow.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(OnOwnerWindowPreviewMouseMove), true);
        _ownerWindow.AddHandler(UIElement.PreviewTouchDownEvent, new EventHandler<TouchEventArgs>(OnOwnerWindowPreviewTouchDown), true);
        _ownerWindow.AddHandler(UIElement.PreviewTouchUpEvent, new EventHandler<TouchEventArgs>(OnOwnerWindowPreviewTouchUp), true);
        BlockOwnerTitleBarInput();
        TryCaptureDialogMouse();
        QueueDialogMouseCapture();
    }

    private void DetachOwnerWindowInputBlocker()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnOwnerWindowPreviewMouseDown));
        _ownerWindow.RemoveHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OnOwnerWindowPreviewMouseUp));
        _ownerWindow.RemoveHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(OnOwnerWindowPreviewMouseMove));
        _ownerWindow.RemoveHandler(UIElement.PreviewTouchDownEvent, new EventHandler<TouchEventArgs>(OnOwnerWindowPreviewTouchDown));
        _ownerWindow.RemoveHandler(UIElement.PreviewTouchUpEvent, new EventHandler<TouchEventArgs>(OnOwnerWindowPreviewTouchUp));
        if (Mouse.Captured is DependencyObject captured && IsDescendantOf(captured, this))
        {
            Mouse.Capture(null);
        }

        RestoreOwnerTitleBarInput();
        _ownerWindow = null;
    }

    private void BlockOwnerTitleBarInput()
    {
        if (_ownerWindow?.FindName("ShellTitleBar") is not UIElement titleBar)
        {
            return;
        }

        _ownerTitleBar = titleBar;
        _ownerTitleBarWasHitTestVisible = titleBar.IsHitTestVisible;
        titleBar.IsHitTestVisible = false;
        Mouse.Synchronize();
        Mouse.UpdateCursor();
    }

    private void RestoreOwnerTitleBarInput()
    {
        if (_ownerTitleBar is null)
        {
            return;
        }

        _ownerTitleBar.IsHitTestVisible = _ownerTitleBarWasHitTestVisible;
        _ownerTitleBar = null;
        Mouse.Synchronize();
        Mouse.UpdateCursor();
    }

    private void OnOwnerWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        BlockInputOutsideDialog(e);
    }

    private void OnOwnerWindowPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        BlockInputOutsideDialog(e);
    }

    private void OnOwnerWindowPreviewMouseMove(object sender, MouseEventArgs e)
    {
        BlockInputOutsideDialog(e);
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(TryCaptureDialogMouse, DispatcherPriority.Background);
    }

    private void QueueDialogMouseCapture()
    {
        _ = Dispatcher.BeginInvoke(TryCaptureDialogMouse, DispatcherPriority.Loaded);
        _ = Dispatcher.BeginInvoke(TryCaptureDialogMouse, DispatcherPriority.Input);
    }

    private void TryCaptureDialogMouse()
    {
        if (IsOpen && IsLoaded && Mouse.Captured is null)
        {
            if (Mouse.Capture(this, CaptureMode.SubTree))
            {
                Mouse.Synchronize();
            }
        }
    }

    private void OnOwnerWindowPreviewTouchDown(object? sender, TouchEventArgs e)
    {
        BlockInputOutsideDialog(e);
    }

    private void OnOwnerWindowPreviewTouchUp(object? sender, TouchEventArgs e)
    {
        BlockInputOutsideDialog(e);
    }

    private void BlockInputOutsideDialog(RoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || !IsDescendantOf(source, this))
        {
            e.Handled = true;
        }
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        return current is Visual
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
    }
}
