using System.Windows;

namespace MediaLibrary.App.Views.Dialogs;

public partial class UserProfileDialogWindow : Window
{
    public UserProfileDialogWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
