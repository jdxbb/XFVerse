using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove requires the left mouse button to remain pressed.
        }
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase
                or TextBoxBase
                or PasswordBox
                or Selector
                or MenuItem
                or Thumb
                or Slider
                or ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }
}
