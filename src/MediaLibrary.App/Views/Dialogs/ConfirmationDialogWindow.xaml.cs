using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaLibrary.App.Services.Interfaces;

namespace MediaLibrary.App.Views.Dialogs;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText,
        ConfirmationDialogVariant variant = ConfirmationDialogVariant.Normal)
    {
        InitializeComponent();

        Title = title;
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ConfirmButton.Content = confirmButtonText;
        CancelButton.Content = cancelButtonText;
        ApplyVariant(variant);
    }

    private void ApplyVariant(ConfirmationDialogVariant variant)
    {
        var iconText = "i";
        var variantLabel = "普通确认";
        var badgeStyleKey = "InfoBadgeStyle";
        var confirmButtonStyleKey = "PrimaryButtonStyle";
        var iconForegroundKey = "BrushInfoForeground";
        var iconBackgroundKey = "BrushInfoBackground";
        var iconBorderKey = "BrushInfoBorder";

        if (variant == ConfirmationDialogVariant.Warning)
        {
            iconText = "!";
            variantLabel = "操作提醒";
            badgeStyleKey = "WarningBadgeStyle";
            confirmButtonStyleKey = "WarningButtonStyle";
            iconForegroundKey = "BrushWarningForeground";
            iconBackgroundKey = "BrushWarningBackground";
            iconBorderKey = "BrushWarningBorder";
        }
        else if (variant == ConfirmationDialogVariant.Danger)
        {
            iconText = "!";
            variantLabel = "危险操作";
            badgeStyleKey = "DangerBadgeStyle";
            confirmButtonStyleKey = "DangerButtonStyle";
            iconForegroundKey = "BrushErrorForeground";
            iconBackgroundKey = "BrushErrorBackground";
            iconBorderKey = "BrushErrorBorder";
            DialogSurface.Style = (Style)FindResource("DangerConfirmDialogStyle");
        }

        VariantIconText.Text = iconText;
        VariantLabelBlock.Text = variantLabel;
        VariantBadge.Style = (Style)FindResource(badgeStyleKey);
        ConfirmButton.Style = (Style)FindResource(confirmButtonStyleKey);
        VariantIconText.Foreground = (Brush)FindResource(iconForegroundKey);
        VariantIconShell.Background = (Brush)FindResource(iconBackgroundKey);
        VariantIconShell.BorderBrush = (Brush)FindResource(iconBorderKey);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        CancelButton.Focus();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during the initial click.
        }
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
