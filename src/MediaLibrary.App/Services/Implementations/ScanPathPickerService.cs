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
        return Task.FromResult(PickLocalDirectoriesCore(initialPath, allowMultiple: false).FirstOrDefault());
    }

    public Task<IReadOnlyList<string>> PickLocalDirectoriesAsync(string? initialPath = null)
    {
        return Task.FromResult<IReadOnlyList<string>>(PickLocalDirectoriesCore(initialPath, allowMultiple: true));
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

    private static IReadOnlyList<string> PickLocalDirectoriesCore(string? initialPath, bool allowMultiple)
    {
        var owner = ResolveOwner();
        var dialog = new OpenFolderDialog
        {
            Title = "选择本地扫描目录",
            Multiselect = allowMultiple
        };

        var normalizedInitialPath = NormalizeExistingDirectory(initialPath) ?? ResolveDefaultInitialDirectory();
        if (!string.IsNullOrWhiteSpace(normalizedInitialPath))
        {
            dialog.InitialDirectory = normalizedInitialPath;
        }

        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != true)
        {
            return [];
        }

        if (!allowMultiple)
        {
            return string.IsNullOrWhiteSpace(dialog.FolderName) ? [] : [dialog.FolderName];
        }

        var selectedFolders = dialog.FolderNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return selectedFolders.Length > 0
            ? selectedFolders
            : string.IsNullOrWhiteSpace(dialog.FolderName) ? [] : [dialog.FolderName];
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

    private static string? ResolveDefaultInitialDirectory()
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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
