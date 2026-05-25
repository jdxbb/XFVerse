using System.Windows;
using System.Windows.Input;
using MediaLibrary.App.ViewModels.Player;

namespace MediaLibrary.App.Views.Player;

public partial class OnlineSubtitleSearchWindow : Window
{
    public OnlineSubtitleSearchWindow(OnlineSubtitleSearchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is OnlineSubtitleSearchViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not OnlineSubtitleSearchViewModel viewModel)
        {
            return;
        }

        if (viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
