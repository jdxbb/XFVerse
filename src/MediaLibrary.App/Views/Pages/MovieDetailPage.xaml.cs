using System.Windows.Controls;
using System.Windows.Input;

namespace MediaLibrary.App.Views.Pages;

public partial class MovieDetailPage : UserControl
{
    public MovieDetailPage()
    {
        InitializeComponent();
    }

    private void CorrectionTargetComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            e.Handled = true;
        }
    }
}
