using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MediaLibrary.App.Helpers;

namespace MediaLibrary.App.Views.Player;

public sealed class PlaybackHostView : HwndHost
{
    private const int WmSetCursor = 0x0020;
    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int WsClipChildren = 0x02000000;
    private const int WsClipSiblings = 0x04000000;
    private const int SsBlackRect = 0x00000004;
    private static readonly IntPtr IdcArrow = new(32512);
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    private int _lastLoggedWidth;
    private int _lastLoggedHeight;
    private bool _hideCursor;

    public IntPtr HostHandle { get; private set; }

    public void SetCursorHidden(bool hideCursor)
    {
        _hideCursor = hideCursor;
        if (HostHandle == IntPtr.Zero)
        {
            return;
        }

        _ = SetCursor(hideCursor ? IntPtr.Zero : LoadCursor(IntPtr.Zero, IdcArrow));
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        HostHandle = CreateWindowEx(
            0,
            "STATIC",
            string.Empty,
            WsChild | WsVisible | WsClipChildren | WsClipSiblings | SsBlackRect,
            0,
            0,
            Math.Max(1, (int)Math.Round(ActualWidth)),
            Math.Max(1, (int)Math.Round(ActualHeight)),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        MpvPlaybackDiagnostics.Write($"playback-host-created hwnd=0x{HostHandle.ToInt64():X}");
        return new HandleRef(this, HostHandle);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        var handle = hwnd.Handle;
        if (handle != IntPtr.Zero)
        {
            MpvPlaybackDiagnostics.Write($"playback-host-destroyed hwnd=0x{handle.ToInt64():X}");
            _ = DestroyWindow(handle);
        }

        HostHandle = IntPtr.Zero;
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        if (HostHandle == IntPtr.Zero)
        {
            return;
        }

        var width = Math.Max(1, (int)Math.Round(rcBoundingBox.Width));
        var height = Math.Max(1, (int)Math.Round(rcBoundingBox.Height));
        _ = SetWindowPos(
            HostHandle,
            IntPtr.Zero,
            0,
            0,
            width,
            height,
            SwpNoZOrder | SwpNoActivate);
        if (_lastLoggedWidth != width || _lastLoggedHeight != height)
        {
            _lastLoggedWidth = width;
            _lastLoggedHeight = height;
            MpvPlaybackDiagnostics.Write($"mpv-host-resize width={width} height={height}");
        }
    }

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSetCursor && _hideCursor)
        {
            _ = SetCursor(IntPtr.Zero);
            handled = true;
            return IntPtr.Zero;
        }

        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);
}
