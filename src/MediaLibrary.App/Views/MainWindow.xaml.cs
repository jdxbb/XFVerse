using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MediaLibrary.App.Services;
using MediaLibrary.App.ViewModels.Main;

namespace MediaLibrary.App.Views;

public partial class MainWindow : Window
{
    private const double DefaultNormalWidth = 1280;
    private const double DefaultNormalHeight = 820;
    private const double DefaultWindowMargin = 48;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;
    private static readonly TimeSpan PopupReopenSuppressionDelay = TimeSpan.FromMilliseconds(350);

    private HwndSource? _hwndSource;
    private Point? _pendingMaximizedDragStart;
    private DateTime _userMenuClosedAtUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = AppServiceProvider.GetRequiredService<MainWindowViewModel>();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        StateChanged += OnWindowStateChanged;
        UpdateMaximizeRestoreButton();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WindowProc);
        SourceInitialized -= OnSourceInitialized;
        Loaded -= OnLoaded;
        StateChanged -= OnWindowStateChanged;
        base.OnClosed(e);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        _hwndSource?.AddHook(WindowProc);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            CenterNormalWindowOnCurrentMonitor();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeRestoreButton();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UserMenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { IsUserMenuOpen: true } viewModel)
        {
            if (DateTime.UtcNow - _userMenuClosedAtUtc > PopupReopenSuppressionDelay)
            {
                return;
            }

            e.Handled = true;
            return;
        }

        viewModel.IsUserMenuOpen = false;
        e.Handled = true;
    }

    private void UserMenuPopup_Closed(object sender, EventArgs e)
    {
        _userMenuClosedAtUtc = DateTime.UtcNow;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SuppressNextUserMenuToggle(PopupReopenSuppressionDelay);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            e.Handled = true;
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            _pendingMaximizedDragStart = e.GetPosition(this);
            if (sender is UIElement element)
            {
                element.CaptureMouse();
            }

            e.Handled = true;
            return;
        }

        BeginWindowDrag(e);
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingMaximizedDragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        if (!HasMovedBeyondDragThreshold(_pendingMaximizedDragStart.Value, currentPosition))
        {
            return;
        }

        _pendingMaximizedDragStart = null;
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
        }

        BeginWindowDrag(e);
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _pendingMaximizedDragStart = null;
        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }
    }

    private void BeginWindowDrag(MouseEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreWindowForDrag(e);
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove requires the left mouse button to remain pressed.
        }
    }

    private static bool HasMovedBeyondDragThreshold(Point start, Point current)
    {
        return Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void RestoreWindowForDrag(MouseEventArgs e)
    {
        var restoreBounds = RestoreBounds;
        var mousePosition = PointToScreen(e.GetPosition(this));
        var source = PresentationSource.FromVisual(this);

        if (source?.CompositionTarget is not null)
        {
            mousePosition = source.CompositionTarget.TransformFromDevice.Transform(mousePosition);
        }

        WindowState = WindowState.Normal;
        Left = mousePosition.X - (restoreBounds.Width * 0.5);
        Top = Math.Max(SystemParameters.VirtualScreenTop, mousePosition.Y - 12);
    }

    private void ToggleMaximizeRestore()
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreToDefaultNormalSize();
            return;
        }

        WindowState = WindowState.Maximized;
    }

    private void RestoreToDefaultNormalSize()
    {
        WindowState = WindowState.Normal;
        CenterNormalWindowOnCurrentMonitor();
    }

    private void CenterNormalWindowOnCurrentMonitor()
    {
        var workArea = GetCurrentMonitorWorkAreaInDips() ?? SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, Math.Min(DefaultNormalWidth, Math.Max(MinWidth, workArea.Width - DefaultWindowMargin)));
        Height = Math.Max(MinHeight, Math.Min(DefaultNormalHeight, Math.Max(MinHeight, workArea.Height - DefaultWindowMargin)));
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) * 0.5);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) * 0.5);
    }

    private void UpdateMaximizeRestoreButton()
    {
        if (MaximizeRestoreButton is null)
        {
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            MaximizeRestoreButton.Content = "\uE923";
            MaximizeRestoreButton.ToolTip = "还原";
            return;
        }

        MaximizeRestoreButton.Content = "\uE922";
        MaximizeRestoreButton.ToolTip = "最大化";
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase
                or TextBoxBase
                or PasswordBox
                or Selector
                or MenuItem
                or Thumb
                or Slider
                or ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private nint WindowProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkArea(hwnd, lParam);
            handled = true;
        }

        return nint.Zero;
    }

    private void ApplyMonitorWorkArea(nint hwnd, nint lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == nint.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        var dpi = VisualTreeHelper.GetDpi(this);
        minMaxInfo.MinTrackSize.X = (int)Math.Ceiling(MinWidth * dpi.DpiScaleX);
        minMaxInfo.MinTrackSize.Y = (int)Math.Ceiling(MinHeight * dpi.DpiScaleY);

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    private Rect? GetCurrentMonitorWorkAreaInDips()
    {
        if (_hwndSource?.Handle is not { } hwnd || hwnd == nint.Zero)
        {
            return null;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == nint.Zero)
        {
            return null;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return null;
        }

        var workArea = monitorInfo.WorkArea;
        var topLeft = new Point(workArea.Left, workArea.Top);
        var bottomRight = new Point(workArea.Right, workArea.Bottom);

        if (_hwndSource.CompositionTarget is not null)
        {
            topLeft = _hwndSource.CompositionTarget.TransformFromDevice.Transform(topLeft);
            bottomRight = _hwndSource.CompositionTarget.TransformFromDevice.Transform(bottomRight);
        }

        return new Rect(topLeft, bottomRight);
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInfo
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointInfo Reserved;
        public PointInfo MaxSize;
        public PointInfo MaxPosition;
        public PointInfo MinTrackSize;
        public PointInfo MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInfo
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public RectInfo MonitorArea;
        public RectInfo WorkArea;
        public int Flags;
    }
}
