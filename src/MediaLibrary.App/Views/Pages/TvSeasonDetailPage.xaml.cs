using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.Views.Pages;

public partial class TvSeasonDetailPage : UserControl
{
    private TvSeasonDetailViewModel? _subscribedViewModel;

    public TvSeasonDetailPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null && DataContext is TvSeasonDetailViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            _subscribedViewModel.TargetEpisodeLocated += OnTargetEpisodeLocated;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetEpisodeLocated -= OnTargetEpisodeLocated;
        }

        _subscribedViewModel = e.NewValue as TvSeasonDetailViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetEpisodeLocated += OnTargetEpisodeLocated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.TargetEpisodeLocated -= OnTargetEpisodeLocated;
            _subscribedViewModel = null;
        }
    }

    private void OnTargetEpisodeLocated(object? sender, TvSeasonDetailViewModel.TvSeasonTargetEpisodeLocatedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => ScrollToTargetEpisode(e.EpisodeId), DispatcherPriority.ContextIdle);
    }

    private void ScrollToTargetEpisode(int episodeId)
    {
        if (DataContext is not TvSeasonDetailViewModel viewModel)
        {
            return;
        }

        var targetEpisode = viewModel.Episodes.FirstOrDefault(episode => episode.EpisodeId == episodeId);
        if (targetEpisode is null)
        {
            return;
        }

        EpisodeListBox.ScrollIntoView(targetEpisode);
        EpisodeListBox.UpdateLayout();
        var container = EpisodeListBox.ItemContainerGenerator.ContainerFromItem(targetEpisode) as FrameworkElement;
        if (container is not null)
        {
            container.BringIntoView();
            return;
        }

        if (EpisodeListBox.Items.Count > 0)
        {
            EpisodeListBox.ScrollIntoView(EpisodeListBox.Items[0]);
        }
    }
}
