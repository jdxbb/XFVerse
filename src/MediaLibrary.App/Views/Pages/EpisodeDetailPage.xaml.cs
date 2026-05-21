using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MediaLibrary.App.Views.Pages;

public partial class EpisodeDetailPage : UserControl
{
    public EpisodeDetailPage()
    {
        InitializeComponent();
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
}
