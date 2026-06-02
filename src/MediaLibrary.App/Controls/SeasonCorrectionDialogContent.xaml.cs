using System.Windows.Controls;
using System.Windows.Input;

namespace MediaLibrary.App.Controls;

public partial class SeasonCorrectionDialogContent : UserControl
{
    public SeasonCorrectionDialogContent()
    {
        InitializeComponent();
    }

    private void CorrectionTargetComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }
}
