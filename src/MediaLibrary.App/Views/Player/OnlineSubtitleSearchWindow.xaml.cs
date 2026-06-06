using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Player;

namespace MediaLibrary.App.Views.Player;

public partial class OnlineSubtitleSearchWindow : Window
{
    private const double FilterMenuWheelScrollStep = 42d;
    private Button? _openMenuButton;
    private ContextMenu? _openContextMenu;

    public OnlineSubtitleSearchWindow(OnlineSubtitleSearchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += (_, _) => CloseOpenMenu();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is OnlineSubtitleSearchViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not OnlineSubtitleSearchViewModel viewModel)
        {
            return;
        }

        if (viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(this, null);
    }

    private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || IsTextInputElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(this, null);
    }

    private void ContextMenu_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ContextMenu contextMenu)
        {
            return;
        }

        var scrollViewer = FindVisualDescendant<ScrollViewer>(contextMenu);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var offsetDelta = e.Delta > 0 ? -FilterMenuWheelScrollStep : FilterMenuWheelScrollStep;
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + offsetDelta);
        e.Handled = true;
    }

    private void MenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
        }
    }

    private void OpenButtonContextMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } contextMenu } button)
        {
            return;
        }

        if (IsOpenMenuButton(button))
        {
            CloseOpenMenu();
            e.Handled = true;
            return;
        }

        OpenContextMenuForButton(button, contextMenu);
        e.Handled = true;
    }

    private void ContextMenu_Closed(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_openContextMenu, sender))
        {
            _openMenuButton = null;
            _openContextMenu = null;
        }
    }

    private void OpenContextMenuForButton(Button button, ContextMenu contextMenu)
    {
        CloseOpenMenu();
        contextMenu.PlacementTarget = button;
        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.Closed -= ContextMenu_Closed;
        contextMenu.Closed += ContextMenu_Closed;
        _openMenuButton = button;
        _openContextMenu = contextMenu;
        contextMenu.IsOpen = true;
        AlignContextMenuToButtonCenter(button, contextMenu);
    }

    private bool IsOpenMenuButton(Button button)
    {
        return ReferenceEquals(_openMenuButton, button)
               && _openContextMenu is not null;
    }

    private void CloseOpenMenu()
    {
        if (_openContextMenu is not null)
        {
            _openContextMenu.IsOpen = false;
        }

        _openMenuButton = null;
        _openContextMenu = null;
    }

    private void AlignContextMenuToButtonCenter(Button button, ContextMenu contextMenu)
    {
        contextMenu.HorizontalOffset = 0;
        contextMenu.VerticalOffset = 4;
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                if (!contextMenu.IsOpen)
                {
                    return;
                }

                contextMenu.HorizontalOffset = Math.Round((button.ActualWidth - contextMenu.ActualWidth) * 0.5);
            },
            DispatcherPriority.Loaded);
    }

    private static bool IsTextInputElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBoxBase
                or PasswordBox
                or ComboBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindVisualDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
