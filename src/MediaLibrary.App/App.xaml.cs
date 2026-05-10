using System.Windows;
using MediaLibrary.App.Services;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            AppServiceProvider.Initialize();
            var databaseInitializer = AppServiceProvider.GetRequiredService<IDatabaseInitializer>();
            databaseInitializer.InitializeAsync().GetAwaiter().GetResult();
            var themeService = AppServiceProvider.GetRequiredService<IThemeService>();
            themeService.InitializeAsync().GetAwaiter().GetResult();
            base.OnStartup(e);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"应用初始化失败：{exception.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServiceProvider.Dispose();
        base.OnExit(e);
    }
}
