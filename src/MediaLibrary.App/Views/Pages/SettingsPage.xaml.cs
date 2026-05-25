using System.ComponentModel;
using System.Windows.Controls;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class SettingsPage : UserControl
{
    private bool _isSyncingOpenSubtitlesPassword;
    private INotifyPropertyChanged? _currentViewModel;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncOpenSubtitlesPasswordBox();
        DataContextChanged += SettingsPage_DataContextChanged;
    }

    private void OpenSubtitlesPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isSyncingOpenSubtitlesPassword)
        {
            return;
        }

        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.OpenSubtitlesPassword = OpenSubtitlesPasswordBox.Password;
        }
    }

    private void SyncOpenSubtitlesPasswordBox()
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        if (OpenSubtitlesPasswordBox.Password == viewModel.OpenSubtitlesPassword)
        {
            return;
        }

        _isSyncingOpenSubtitlesPassword = true;
        try
        {
            OpenSubtitlesPasswordBox.Password = viewModel.OpenSubtitlesPassword;
        }
        finally
        {
            _isSyncingOpenSubtitlesPassword = false;
        }
    }

    private void SettingsPage_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged -= ViewModelPropertyChanged;
        }

        _currentViewModel = e.NewValue as INotifyPropertyChanged;
        if (_currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged += ViewModelPropertyChanged;
        }

        SyncOpenSubtitlesPasswordBox();
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.OpenSubtitlesPassword))
        {
            SyncOpenSubtitlesPasswordBox();
        }
    }
}
