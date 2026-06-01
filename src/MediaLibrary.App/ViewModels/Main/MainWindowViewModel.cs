using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.Views.Dialogs;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Main;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const double SidebarExpandedWidth = 220;
    private const double SidebarCollapsedWidth = 64;

    private readonly Dictionary<NavigationPageKey, NavigationItemViewModel> _routeMap;
    private readonly Dictionary<NavigationPageKey, NavigationItemViewModel> _visibleNavigationMap;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private NavigationItemViewModel? _selectedNavigationItem;
    private PageViewModelBase? _currentPageViewModel;
    private CancellationTokenSource? _pageActivationCancellation;
    private int _pageActivationVersion;
    private string _currentPageTitle = string.Empty;
    private string _currentPageSubtitle = string.Empty;
    private bool _isHomePageActive;
    private bool _isDetailRouteActive;
    private bool _isSidebarExpanded = true;
    private bool _isUserMenuOpen;
    private string _userMenuStatusMessage = string.Empty;
    private string _themeToggleIcon = "☀";
    private string _themeToggleToolTip = "当前浅色主题，切换到深色主题";

    public MainWindowViewModel(
        INavigationStateService navigationStateService,
        ISettingsService settingsService,
        IThemeService themeService,
        HomeViewModel homeViewModel,
        LibraryViewModel libraryViewModel,
        MovieDiscoveryViewModel movieDiscoveryViewModel,
        WatchHistoryViewModel watchHistoryViewModel,
        MovieDetailViewModel movieDetailViewModel,
        SeriesOverviewViewModel seriesOverviewViewModel,
        TvSeasonDetailViewModel tvSeasonDetailViewModel,
        EpisodeDetailViewModel episodeDetailViewModel,
        ScanTasksViewModel scanTasksViewModel,
        RecommendationsViewModel recommendationsViewModel,
        WatchInsightsViewModel watchInsightsViewModel,
        FavoritesViewModel favoritesViewModel,
        SettingsViewModel settingsViewModel)
    {
        NavigationStateService = navigationStateService;
        _settingsService = settingsService;
        _themeService = themeService;

        VisibleNavigationItems =
        [
            new NavigationItemViewModel(NavigationPageKey.Home, "首页", homeViewModel, "⌂"),
            new NavigationItemViewModel(NavigationPageKey.Library, "媒体库", libraryViewModel, "▦"),
            new NavigationItemViewModel(NavigationPageKey.MovieDiscovery, "影片发现", movieDiscoveryViewModel, "✦"),
            new NavigationItemViewModel(NavigationPageKey.WatchHistory, "观影历史", watchHistoryViewModel, "◷"),
            new NavigationItemViewModel(NavigationPageKey.Favorites, "收藏夹", favoritesViewModel, "♡"),
            new NavigationItemViewModel(NavigationPageKey.WatchInsights, "观影洞察", watchInsightsViewModel, "◎")
        ];

        var hiddenRouteItems = new[]
        {
            new NavigationItemViewModel(NavigationPageKey.SeriesOverview, "电视剧", seriesOverviewViewModel),
            new NavigationItemViewModel(NavigationPageKey.TvSeasonDetail, "电视剧季", tvSeasonDetailViewModel),
            new NavigationItemViewModel(NavigationPageKey.EpisodeDetail, "剧集详情", episodeDetailViewModel),
            new NavigationItemViewModel(NavigationPageKey.MovieDetail, "详情", movieDetailViewModel),
            new NavigationItemViewModel(NavigationPageKey.ScanTasks, "扫描任务", scanTasksViewModel),
            new NavigationItemViewModel(NavigationPageKey.Recommendations, "AI 推荐", recommendationsViewModel),
            new NavigationItemViewModel(NavigationPageKey.Settings, "设置", settingsViewModel)
        };

        _visibleNavigationMap = VisibleNavigationItems.ToDictionary(item => item.PageKey);
        _routeMap = VisibleNavigationItems.Concat(hiddenRouteItems).ToDictionary(item => item.PageKey);
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        ToggleUserMenuCommand = new RelayCommand(ToggleUserMenu);
        OpenUserProfileCommand = new RelayCommand(OpenUserProfile);
        OpenScanTasksFromMenuCommand = new RelayCommand(() => NavigateFromUserMenu(NavigationPageKey.ScanTasks));
        OpenSettingsFromMenuCommand = new RelayCommand(() => NavigateFromUserMenu(NavigationPageKey.Settings));
        LogoutCommand = new RelayCommand(ShowLogoutPlaceholder);

        NavigationStateService.NavigationRequested += OnNavigationRequested;
        _ = InitializeThemePresentationAsync();
        SelectedNavigationItem = VisibleNavigationItems.First();
    }

    private INavigationStateService NavigationStateService { get; }

    public ObservableCollection<NavigationItemViewModel> VisibleNavigationItems { get; }

    public AsyncRelayCommand ToggleThemeCommand { get; }

    public RelayCommand ToggleSidebarCommand { get; }

    public RelayCommand ToggleUserMenuCommand { get; }

    public RelayCommand OpenUserProfileCommand { get; }

    public RelayCommand OpenScanTasksFromMenuCommand { get; }

    public RelayCommand OpenSettingsFromMenuCommand { get; }

    public RelayCommand LogoutCommand { get; }

    public string UserDisplayName => "James";

    public string UserAvatarInitial => "J";

    public string ThemeToggleIcon
    {
        get => _themeToggleIcon;
        private set => SetProperty(ref _themeToggleIcon, value);
    }

    public string ThemeToggleToolTip
    {
        get => _themeToggleToolTip;
        private set => SetProperty(ref _themeToggleToolTip, value);
    }

    public GridLength SidebarColumnWidth => IsSidebarExpanded
        ? new GridLength(IsDetailRouteActive ? 0 : SidebarExpandedWidth)
        : new GridLength(IsDetailRouteActive ? 0 : SidebarCollapsedWidth);

    public bool IsSidebarCollapsed => !IsSidebarExpanded;

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(IsSidebarCollapsed));
                OnPropertyChanged(nameof(SidebarColumnWidth));
                OnPropertyChanged(nameof(ShellPageTitle));
            }
        }
    }

    public bool IsUserMenuOpen
    {
        get => _isUserMenuOpen;
        set => SetProperty(ref _isUserMenuOpen, value);
    }

    public string UserMenuStatusMessage
    {
        get => _userMenuStatusMessage;
        private set
        {
            if (SetProperty(ref _userMenuStatusMessage, value))
            {
                OnPropertyChanged(nameof(HasUserMenuStatusMessage));
            }
        }
    }

    public bool HasUserMenuStatusMessage => !string.IsNullOrWhiteSpace(UserMenuStatusMessage);

    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (!SetProperty(ref _selectedNavigationItem, value) || value is null)
            {
                return;
            }

            _ = NavigateToAsync(new NavigationRequest(value.PageKey), syncVisibleSelection: false);
        }
    }

    public PageViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set
        {
            if (ReferenceEquals(_currentPageViewModel, value))
            {
                return;
            }

            if (_currentPageViewModel is not null)
            {
                _currentPageViewModel.PropertyChanged -= OnCurrentPagePropertyChanged;
            }

            SetProperty(ref _currentPageViewModel, value);
            if (_currentPageViewModel is not null)
            {
                _currentPageViewModel.PropertyChanged += OnCurrentPagePropertyChanged;
            }

            OnPropertyChanged(nameof(DetailBackdropSource));
        }
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set
        {
            if (SetProperty(ref _currentPageTitle, value))
            {
                OnPropertyChanged(nameof(ShellPageTitle));
            }
        }
    }

    public string CurrentPageSubtitle
    {
        get => _currentPageSubtitle;
        private set => SetProperty(ref _currentPageSubtitle, value);
    }

    public bool IsHomePageActive
    {
        get => _isHomePageActive;
        private set
        {
            if (SetProperty(ref _isHomePageActive, value))
            {
                OnPropertyChanged(nameof(ShellPageTitle));
            }
        }
    }

    public bool IsDetailRouteActive
    {
        get => _isDetailRouteActive;
        private set
        {
            if (SetProperty(ref _isDetailRouteActive, value))
            {
                OnPropertyChanged(nameof(SidebarColumnWidth));
                OnPropertyChanged(nameof(IsShellChromeVisible));
            }
        }
    }

    public bool IsShellChromeVisible => !IsDetailRouteActive;

    public string DetailBackdropSource => CurrentPageViewModel switch
    {
        MovieDetailViewModel movieDetail => movieDetail.PosterDisplayUrl,
        SeriesOverviewViewModel seriesOverview => seriesOverview.PosterDisplayUrl,
        TvSeasonDetailViewModel seasonDetail => seasonDetail.PosterDisplayUrl,
        EpisodeDetailViewModel episodeDetail => episodeDetail.StillDisplayUrl,
        _ => string.Empty
    };

    public string ShellPageTitle => IsHomePageActive && IsSidebarExpanded
        ? $"欢迎回来，{UserDisplayName} 👋"
        : CurrentPageTitle;

    private void OnCurrentPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MovieDetailViewModel.PosterDisplayUrl)
            or nameof(SeriesOverviewViewModel.PosterDisplayUrl)
            or nameof(TvSeasonDetailViewModel.PosterDisplayUrl)
            or nameof(EpisodeDetailViewModel.StillDisplayUrl))
        {
            OnPropertyChanged(nameof(DetailBackdropSource));
        }
    }

    private async void OnNavigationRequested(object? sender, NavigationRequest request)
    {
        await NavigateToAsync(request, syncVisibleSelection: true);
    }

    private Task NavigateToAsync(NavigationRequest request, bool syncVisibleSelection)
    {
        var pageKey = request.PageKey;
        if (!_routeMap.TryGetValue(pageKey, out var navigationItem))
        {
            return Task.CompletedTask;
        }

        if (syncVisibleSelection)
        {
            var nextSelectedItem = _visibleNavigationMap.GetValueOrDefault(pageKey);
            SetProperty(ref _selectedNavigationItem, nextSelectedItem, nameof(SelectedNavigationItem));
        }

        NavigationStateService.NotifyPageActivated(request);
        ActivatePage(navigationItem.PageViewModel);
        return Task.CompletedTask;
    }

    private void ActivatePage(PageViewModelBase pageViewModel)
    {
        if (!ReferenceEquals(CurrentPageViewModel, pageViewModel))
        {
            CurrentPageViewModel?.Deactivate();
        }

        if (pageViewModel is MovieDetailViewModel movieDetail)
        {
            movieDetail.PrepareForActivation();
        }

        _pageActivationCancellation?.Cancel();
        _pageActivationCancellation?.Dispose();
        _pageActivationCancellation = new CancellationTokenSource();
        var activationToken = _pageActivationCancellation.Token;
        var activationVersion = ++_pageActivationVersion;

        CurrentPageViewModel = pageViewModel;
        IsHomePageActive = pageViewModel is HomeViewModel;
        IsDetailRouteActive = IsDetailPageViewModel(pageViewModel);
        CurrentPageTitle = pageViewModel.Title;
        CurrentPageSubtitle = pageViewModel.Subtitle;
        _ = ActivatePageContentAsync(pageViewModel, activationVersion, activationToken);
    }

    private static bool IsDetailPageViewModel(PageViewModelBase pageViewModel)
    {
        return pageViewModel is MovieDetailViewModel
            or SeriesOverviewViewModel
            or TvSeasonDetailViewModel
            or EpisodeDetailViewModel;
    }

    private async Task ActivatePageContentAsync(
        PageViewModelBase pageViewModel,
        int activationVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (cancellationToken.IsCancellationRequested ||
                activationVersion != _pageActivationVersion ||
                !ReferenceEquals(CurrentPageViewModel, pageViewModel))
            {
                return;
            }

            await pageViewModel.ActivateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        if (!IsSidebarExpanded)
        {
            IsUserMenuOpen = false;
        }
    }

    private void ToggleUserMenu()
    {
        UserMenuStatusMessage = string.Empty;
        IsUserMenuOpen = !IsUserMenuOpen;
    }

    private void OpenUserProfile()
    {
        IsUserMenuOpen = false;
        var dialog = new UserProfileDialogWindow();
        var owner = Application.Current?.MainWindow;
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    private void NavigateFromUserMenu(NavigationPageKey pageKey)
    {
        IsUserMenuOpen = false;
        _ = NavigateToAsync(new NavigationRequest(pageKey), syncVisibleSelection: true);
    }

    private void ShowLogoutPlaceholder()
    {
        UserMenuStatusMessage = "退出登录功能尚未接入。";
    }

    private async Task ToggleThemeAsync()
    {
        var settings = await _settingsService.GetApplicationSettingAsync();
        var nextTheme = string.Equals(settings.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        await _themeService.ApplyAndSaveAsync(nextTheme);
        UpdateThemePresentation(nextTheme);
    }

    private async Task InitializeThemePresentationAsync()
    {
        try
        {
            var settings = await _settingsService.GetApplicationSettingAsync();
            UpdateThemePresentation(settings.ThemeMode);
        }
        catch
        {
            UpdateThemePresentation("Light");
        }
    }

    private void UpdateThemePresentation(string? themeMode)
    {
        if (string.Equals(themeMode, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            ThemeToggleIcon = "☾";
            ThemeToggleToolTip = "当前深色主题，切换到浅色主题";
            return;
        }

        ThemeToggleIcon = "☀";
        ThemeToggleToolTip = "当前浅色主题，切换到深色主题";
    }
}
