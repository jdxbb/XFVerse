using System.IO;
using System.Windows;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.Views.Dialogs;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.Win32;

namespace MediaLibrary.App.Services.Implementations;

public sealed class ScanPathPickerService : IScanPathPickerService
{
    private readonly IWebDavService _webDavService;

    public ScanPathPickerService(IWebDavService webDavService)
    {
        _webDavService = webDavService;
    }

    public Task<string?> PickLocalDirectoryAsync(string? initialPath = null)
    {
        var owner = ResolveOwner();
        var dialog = new OpenFolderDialog
        {
            Title = "选择本地扫描目录",
            Multiselect = false
        };

        var normalizedInitialPath = NormalizeExistingDirectory(initialPath);
        if (!string.IsNullOrWhiteSpace(normalizedInitialPath))
        {
            dialog.InitialDirectory = normalizedInitialPath;
        }

        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return Task.FromResult(result == true ? dialog.FolderName : null);
    }

    public Task<string?> PickWebDavDirectoryAsync(WebDavConnectionModel connection, string? initialPath = null)
    {
        var dialog = new WebDavPathPickerWindow(_webDavService, connection, initialPath)
        {
            Owner = ResolveOwner(),
            ShowActivated = true
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.SelectedPath : null);
    }

    private static string? NormalizeExistingDirectory(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        try
        {
            return Directory.Exists(trimmed) ? Path.GetFullPath(trimmed) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Window? ResolveOwner()
    {
        var application = Application.Current;
        if (application is null)
        {
            return null;
        }

        var activeWindow = application.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsVisible && window.IsActive);
        if (activeWindow is not null)
        {
            return activeWindow;
        }

        return application.MainWindow is { IsVisible: true } mainWindow
            ? mainWindow
            : application.Windows.OfType<Window>().FirstOrDefault(window => window.IsVisible);
    }
}
