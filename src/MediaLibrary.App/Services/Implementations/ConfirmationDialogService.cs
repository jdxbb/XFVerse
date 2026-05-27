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
        var dialog = new ConfirmationDialogWindow(title, message, confirmButtonText, cancelButtonText, variant)
        {
            Owner = ResolveOwner()
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

        return application.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive)
               ?? application.MainWindow;
    }
}
