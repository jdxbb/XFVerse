using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Controls;

public partial class MovieCorrectionDialogContent : UserControl
{
    private const double OverviewMouseWheelScrollStep = 48d;
    private const double ResultListMouseWheelScrollStep = 56d;

    public MovieCorrectionDialogContent()
    {
        InitializeComponent();
    }

    private void CorrectionTargetComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void ResultOverviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (HasVerticalScrollableContent(scrollViewer))
        {
            _ = ScrollViewerBySmallWheelStep(scrollViewer, e.Delta, OverviewMouseWheelScrollStep);
            HideAncestorScrollViewers(scrollViewer);
            e.Handled = true;
            return;
        }

        if (ScrollParentBySmallWheelStep(scrollViewer, e.Delta))
        {
            e.Handled = true;
        }
    }

    private void CorrectionSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !HasSearchInputText())
        {
            return;
        }

        if (sender is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        var command = DataContext switch
        {
            MovieDetailViewModel viewModel => viewModel.SearchCandidatesCommand,
            EpisodeDetailViewModel viewModel => viewModel.SearchCandidatesCommand,
            _ => null
        };

        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private static bool ScrollParentBySmallWheelStep(DependencyObject source, int wheelDelta)
    {
        var direction = wheelDelta < 0 ? 1 : -1;
        for (var current = GetParent(source); current is not null; current = GetParent(current))
        {
            if (current is ScrollViewer scrollViewer
                && CanScrollVertically(scrollViewer, direction)
                && ScrollViewerBySmallWheelStep(scrollViewer, wheelDelta, ResultListMouseWheelScrollStep))
            {
                return true;
            }
        }

        return false;
    }

    private static void HideAncestorScrollViewers(DependencyObject source)
    {
        for (var current = GetParent(source); current is not null; current = GetParent(current))
        {
            if (current is ScrollViewer scrollViewer)
            {
                ScrollBarAutoRevealBehavior.Hide(scrollViewer);
            }
        }
    }

    private static bool ScrollViewerBySmallWheelStep(ScrollViewer scrollViewer, int wheelDelta, double step)
    {
        var direction = wheelDelta < 0 ? 1 : -1;
        if (!CanScrollVertically(scrollViewer, direction))
        {
            return false;
        }

        var wheelTicks = Math.Max(1d, Math.Abs(wheelDelta) / 120d);
        var nextOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * step * wheelTicks,
            0d,
            scrollViewer.ScrollableHeight);
        if (Math.Abs(nextOffset - scrollViewer.VerticalOffset) <= 0.1d)
        {
            return false;
        }

        scrollViewer.ScrollToVerticalOffset(nextOffset);
        return true;
    }

    private bool HasSearchInputText()
    {
        return HasText(MovieSearchTextBox)
            || HasText(MovieYearTextBox)
            || HasText(TvSearchTextBox)
            || HasText(SeasonNumberTextBox)
            || HasText(EpisodeNumberTextBox);
    }

    private static bool HasText(TextBox textBox)
    {
        return textBox.IsVisible && !string.IsNullOrWhiteSpace(textBox.Text);
    }

    private static bool HasVerticalScrollableContent(ScrollViewer scrollViewer)
    {
        return scrollViewer.ScrollableHeight > double.Epsilon;
    }

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int direction)
    {
        if (scrollViewer.ScrollableHeight <= double.Epsilon)
        {
            return false;
        }

        return direction > 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - double.Epsilon
            : scrollViewer.VerticalOffset > double.Epsilon;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        return current is Visual
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
    }
}
