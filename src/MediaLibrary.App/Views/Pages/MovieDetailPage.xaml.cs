using System.Windows.Controls;
using System.Windows.Input;

namespace MediaLibrary.App.Views.Pages;

public partial class MovieDetailPage : UserControl
{
    private const double OverviewMouseWheelScrollStep = 48d;

    public MovieDetailPage()
    {
        InitializeComponent();
    }

    private void OnOverviewPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var direction = e.Delta > 0 ? -1d : 1d;
        var wheelTicks = Math.Max(1d, Math.Abs(e.Delta) / 120d);
        var targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * OverviewMouseWheelScrollStep * wheelTicks,
            0d,
            scrollViewer.ScrollableHeight);
        if (Math.Abs(targetOffset - scrollViewer.VerticalOffset) <= double.Epsilon)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private void CorrectionTargetComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            e.Handled = true;
        }
    }
}
