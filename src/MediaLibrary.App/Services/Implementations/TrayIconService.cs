using System.Diagnostics;
using System.Drawing;
using System.Windows;
using MediaLibrary.App.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace MediaLibrary.App.Services.Implementations;

public sealed class TrayIconService : ITrayIconService
{
    private Forms.NotifyIcon? _notifyIcon;
    private Window? _mainWindow;
    private Action? _exitAction;

    public void Initialize(Window mainWindow, Action exitAction)
    {
        _mainWindow = mainWindow;
        _exitAction = exitAction;

        if (_notifyIcon is not null)
        {
            return;
        }

        var openItem = new Forms.ToolStripMenuItem("打开 XFVerse");
        openItem.Click += (_, _) => RestoreMainWindow();

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => _mainWindow?.Dispatcher.BeginInvoke(() => _exitAction?.Invoke());

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = ResolveIcon(),
            Text = "XFVerse",
            ContextMenuStrip = contextMenu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindow();
    }

    public void ShowMainWindowInTray()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = true;
    }

    public void RestoreMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Dispatcher.BeginInvoke(() =>
        {
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
            }
        });
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private static Icon ResolveIcon()
    {
        try
        {
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                return Icon.ExtractAssociatedIcon(processPath) ?? SystemIcons.Application;
            }
        }
        catch
        {
            // Fall back to the platform application icon.
        }

        return SystemIcons.Application;
    }
}
