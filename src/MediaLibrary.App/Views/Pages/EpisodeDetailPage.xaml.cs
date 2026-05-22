using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class EpisodeDetailPage : UserControl
{
    private INotifyPropertyChanged? _currentViewModel;

    public EpisodeDetailPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void CorrectionButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            () => CorrectionPanel.BringIntoView(),
            DispatcherPriority.Background);
    }

    private void CorrectionTargetComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            e.Handled = true;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _currentViewModel = e.NewValue as INotifyPropertyChanged;
        if (_currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshCorrectionTabVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EpisodeDetailViewModel.CanUseIdentificationCorrection)
            or nameof(EpisodeDetailViewModel.HasSources)
            or nameof(EpisodeDetailViewModel.HasEpisode))
        {
            Dispatcher.BeginInvoke(RefreshCorrectionTabVisibility, DispatcherPriority.Background);
        }
    }

    private void RefreshCorrectionTabVisibility()
    {
        if (DetailTabControl.Items.Count <= 1 || DetailTabControl.Items[1] is not TabItem correctionTab)
        {
            return;
        }

        var canUseCorrection = DataContext is EpisodeDetailViewModel viewModel
                               && viewModel.CanUseIdentificationCorrection;
        correctionTab.Visibility = canUseCorrection ? Visibility.Visible : Visibility.Collapsed;
        if (!canUseCorrection && DetailTabControl.SelectedIndex == 1)
        {
            DetailTabControl.SelectedIndex = 0;
        }
    }
}
