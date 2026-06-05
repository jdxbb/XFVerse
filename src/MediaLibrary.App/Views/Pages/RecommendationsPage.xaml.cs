using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Views.Pages;

public partial class RecommendationsPage : UserControl
{
    private Button? _openMenuButton;
    private ContextMenu? _openContextMenu;

    public RecommendationsPage()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseOpenMenu();
    }

    private void MenuButton_PreviewMouseLeftButtonDown(object sender, RoutedEventArgs e)
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

        CloseOpenMenu();
        contextMenu.PlacementTarget = button;
        contextMenu.Placement = PlacementMode.Bottom;
        contextMenu.VerticalOffset = 4;
        contextMenu.Closed -= ContextMenu_Closed;
        contextMenu.Closed += ContextMenu_Closed;
        _openMenuButton = button;
        _openContextMenu = contextMenu;
        contextMenu.IsOpen = true;

        _ = Dispatcher.BeginInvoke(
            () =>
            {
                if (contextMenu.IsOpen)
                {
                    contextMenu.HorizontalOffset = Math.Round((button.ActualWidth - contextMenu.ActualWidth) * 0.5);
                }
            },
            DispatcherPriority.Loaded);
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

    private void RecommendationPoster_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AiRecommendationItem item }
            || IsWithinButton(e.OriginalSource))
        {
            return;
        }

        if (DataContext is RecommendationsViewModel viewModel
            && viewModel.OpenMovieCommand.CanExecute(item))
        {
            viewModel.OpenMovieCommand.Execute(item);
            e.Handled = true;
        }
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

    private static bool IsWithinButton(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = GetParent(current))
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject source)
    {
        if (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return LogicalTreeHelper.GetParent(source);
    }
}
