using System.Windows;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.Views.Dialogs;

namespace MediaLibrary.App.Services.Implementations;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText,
        ConfirmationDialogVariant variant = ConfirmationDialogVariant.Normal)
    {
        var owner = ResolveOwner();
        if (owner is not null)
        {
            if (owner.WindowState == WindowState.Minimized)
            {
                owner.WindowState = WindowState.Normal;
            }
        }

        var dialog = new ConfirmationDialogWindow(title, message, confirmButtonText, cancelButtonText, variant)
        {
            Owner = owner,
            ShowActivated = true
        };

        return Task.FromResult(dialog.ShowDialog() == true);
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
            .FirstOrDefault(window => window.IsVisible
                                      && window.IsActive
                                      && window is not ConfirmationDialogWindow);
        if (activeWindow is not null)
        {
            return activeWindow;
        }

        return application.MainWindow is { IsVisible: true } mainWindow
            ? mainWindow
            : application.Windows.OfType<Window>().FirstOrDefault(window => window.IsVisible);
    }
}
