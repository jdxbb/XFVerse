using System.Windows;
using MediaLibrary.App.Services;
using MediaLibrary.App.ViewModels.Main;

namespace MediaLibrary.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = AppServiceProvider.GetRequiredService<MainWindowViewModel>();
    }
}
