using System.Windows;

namespace MediaLibrary.App.Services.Interfaces;

public interface ITrayIconService : IDisposable
{
    void Initialize(Window mainWindow, Action exitAction);

    void ShowMainWindowInTray();

    void RestoreMainWindow();
}
