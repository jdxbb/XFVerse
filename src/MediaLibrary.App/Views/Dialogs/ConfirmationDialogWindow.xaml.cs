using System.Windows;

namespace MediaLibrary.App.Views.Dialogs;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText)
    {
        InitializeComponent();

        Title = title;
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ConfirmButton.Content = confirmButtonText;
        CancelButton.Content = cancelButtonText;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
