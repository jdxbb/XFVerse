using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class WatchHistoryPage : UserControl
{
    private WatchHistoryViewModel? _subscribedViewModel;

    public WatchHistoryPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null && DataContext is WatchHistoryViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            _subscribedViewModel.TargetDateLocated += OnTargetDateLocated;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetDateLocated -= OnTargetDateLocated;
        }

        _subscribedViewModel = e.NewValue as WatchHistoryViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetDateLocated += OnTargetDateLocated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetDateLocated -= OnTargetDateLocated;
            _subscribedViewModel = null;
        }
    }

    private void OnTargetDateLocated(object? sender, WatchHistoryViewModel.WatchHistoryTargetDateLocatedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => ScrollToTargetDate(e.TargetDate), DispatcherPriority.ContextIdle);
    }

    private void ScrollToTargetDate(DateTime targetDate)
    {
        if (DataContext is not WatchHistoryViewModel viewModel)
        {
            return;
        }

        var targetGroup = viewModel.DayGroups.FirstOrDefault(group => group.Date == targetDate.Date);
        if (targetGroup is null)
        {
            return;
        }

        HistoryGroupsItemsControl.UpdateLayout();
        var container = HistoryGroupsItemsControl.ItemContainerGenerator.ContainerFromItem(targetGroup) as FrameworkElement;
        if (container is not null)
        {
            container.BringIntoView();
            return;
        }

        HistoryScrollViewer.ScrollToTop();
    }
}
