using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MediaLibrary.App.ViewModels.Pages;

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

    private void SeasonCorrectionSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !HasSeasonSearchInputText())
        {
            return;
        }

        if (sender is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        if (DataContext is TvSeasonDetailViewModel viewModel
            && viewModel.SearchSeasonCorrectionCandidatesCommand.CanExecute(null))
        {
            viewModel.SearchSeasonCorrectionCandidatesCommand.Execute(null);
            e.Handled = true;
        }
    }

    private bool HasSeasonSearchInputText()
    {
        return HasText(SeasonCorrectionSearchTextBox)
            || HasText(SeasonCorrectionSeasonNumberTextBox);
    }

    private static bool HasText(TextBox textBox)
    {
        return textBox.IsVisible && !string.IsNullOrWhiteSpace(textBox.Text);
    }
}
