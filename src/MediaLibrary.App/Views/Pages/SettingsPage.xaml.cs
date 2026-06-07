using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MediaLibrary.App.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void ApiConfigScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer
            || e.OriginalSource is not DependencyObject source
            || !ShouldRouteWheelToApiScrollViewer(source))
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static bool ShouldRouteWheelToApiScrollViewer(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is TextBoxBase or PasswordBox)
            {
                return true;
            }

            if (current is ComboBox comboBox)
            {
                return !comboBox.IsDropDownOpen;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject source)
    {
        return source is Visual or Visual3D
            ? VisualTreeHelper.GetParent(source)
            : LogicalTreeHelper.GetParent(source);
    }
}
