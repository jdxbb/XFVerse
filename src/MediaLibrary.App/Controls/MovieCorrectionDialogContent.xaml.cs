using System.Windows.Controls;
using System.Windows.Input;

namespace MediaLibrary.App.Controls;

public partial class MovieCorrectionDialogContent : UserControl
{
    public MovieCorrectionDialogContent()
    {
        InitializeComponent();
    }

    private void CorrectionTargetComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }
}
