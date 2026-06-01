using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MediaLibrary.App.Controls;

public partial class CorrectionDialogShell : UserControl
{
    public CorrectionDialogShell()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

    private Window? _ownerWindow;

    private static void OnIsOpenChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is CorrectionDialogShell shell)
        {
            shell.UpdateModalState((bool)e.NewValue);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsOpen)
        {
            UpdateModalState(true);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachOwnerWindow();
    }

    private void UpdateModalState(bool isOpen)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (!isOpen)
        {
            DetachOwnerWindow();
            return;
        }

        AttachOwnerWindow(Window.GetWindow(this));
        UpdateOverlayPlacement();
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                UpdateOverlayPlacement();
                DialogCard.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            },
            DispatcherPriority.Loaded);
    }

    private void AttachOwnerWindow(Window? ownerWindow)
    {
        if (ReferenceEquals(_ownerWindow, ownerWindow))
        {
            return;
        }

        DetachOwnerWindow();
        _ownerWindow = ownerWindow;
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.LocationChanged += OnOwnerWindowBoundsChanged;
        _ownerWindow.SizeChanged += OnOwnerWindowBoundsChanged;
        _ownerWindow.StateChanged += OnOwnerWindowStateChanged;
    }

    private void DetachOwnerWindow()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.LocationChanged -= OnOwnerWindowBoundsChanged;
        _ownerWindow.SizeChanged -= OnOwnerWindowBoundsChanged;
        _ownerWindow.StateChanged -= OnOwnerWindowStateChanged;
        _ownerWindow = null;
    }

    private void OnOwnerWindowBoundsChanged(object? sender, EventArgs e)
    {
        UpdateOverlayPlacement();
    }

    private void OnOwnerWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateOverlayPlacement();
    }

    private void UpdateOverlayPlacement()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        OverlayPopup.PlacementTarget = _ownerWindow;
        OverlayPopup.HorizontalOffset = 0d;
        OverlayPopup.VerticalOffset = 0d;
        OverlayLayer.Width = Math.Max(1d, _ownerWindow.ActualWidth);
        OverlayLayer.Height = Math.Max(1d, _ownerWindow.ActualHeight);
    }
}
