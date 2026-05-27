using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MediaLibrary.App.ViewModels.Main;

namespace MediaLibrary.App.Views.Pages;

public partial class HomePage : UserControl
{
    private const double ExpandedAiPanelWidth = 360;
    private const double CollapsedAiPanelWidth = 454;
    private MainWindowViewModel? _shellViewModel;

    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachShellViewModel();
        UpdateAiPanelWidth();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
            _shellViewModel = null;
        }
    }

    private void AttachShellViewModel()
    {
        if (Window.GetWindow(this)?.DataContext is not MainWindowViewModel shellViewModel
            || ReferenceEquals(_shellViewModel, shellViewModel))
        {
            return;
        }

        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        }

        _shellViewModel = shellViewModel;
        _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarCollapsed))
        {
            UpdateAiPanelWidth();
        }
    }

    private void UpdateAiPanelWidth()
    {
        AiPanelColumn.Width = new GridLength(
            _shellViewModel?.IsSidebarCollapsed == true
                ? CollapsedAiPanelWidth
                : ExpandedAiPanelWidth);
    }
}
