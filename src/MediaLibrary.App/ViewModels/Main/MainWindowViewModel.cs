using System.Collections.ObjectModel;
using System.Windows;
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
    private readonly Dictionary<NavigationPageKey, NavigationItemViewModel> _routeMap;
    private readonly Dictionary<NavigationPageKey, NavigationItemViewModel> _visibleNavigationMap;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private NavigationItemViewModel? _selectedNavigationItem;
    private PageViewModelBase? _currentPageViewModel;
    private string _currentPageTitle = string.Empty;
    private string _currentPageSubtitle = string.Empty;
    private bool _isSidebarExpanded = true;
    private bool _isUserMenuOpen;
    private string _userMenuStatusMessage = string.Empty;

    public MainWindowViewModel(
        INavigationStateService navigationStateService,
        ISettingsService settingsService,
        IThemeService themeService,
        HomeViewModel homeViewModel,
        LibraryViewModel libraryViewModel,
        MovieDiscoveryViewModel movieDiscoveryViewModel,
        WatchHistoryViewModel watchHistoryViewModel,
        MovieDetailViewModel movieDetailViewModel,
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
            new NavigationItemViewModel(NavigationPageKey.Home, "首页", homeViewModel),
            new NavigationItemViewModel(NavigationPageKey.Library, "媒体库", libraryViewModel),
            new NavigationItemViewModel(NavigationPageKey.MovieDiscovery, "影片发现", movieDiscoveryViewModel),
            new NavigationItemViewModel(NavigationPageKey.WatchHistory, "观影历史", watchHistoryViewModel),
            new NavigationItemViewModel(NavigationPageKey.Favorites, "收藏夹", favoritesViewModel),
            new NavigationItemViewModel(NavigationPageKey.WatchInsights, "观影洞察", watchInsightsViewModel)
        ];

        var hiddenRouteItems = new[]
        {
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

    public GridLength SidebarColumnWidth => IsSidebarExpanded ? new GridLength(248) : new GridLength(0);

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

            _ = NavigateToAsync(value.PageKey, syncVisibleSelection: false);
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

            SetProperty(ref _currentPageViewModel, value);
        }
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    public string CurrentPageSubtitle
    {
        get => _currentPageSubtitle;
        private set => SetProperty(ref _currentPageSubtitle, value);
    }

    private async void OnNavigationRequested(object? sender, NavigationRequest request)
    {
        await NavigateToAsync(request.PageKey, syncVisibleSelection: true);
    }

    private async Task NavigateToAsync(NavigationPageKey pageKey, bool syncVisibleSelection)
    {
        if (!_routeMap.TryGetValue(pageKey, out var navigationItem))
        {
            return;
        }

        if (syncVisibleSelection)
        {
            var nextSelectedItem = _visibleNavigationMap.GetValueOrDefault(pageKey);
            SetProperty(ref _selectedNavigationItem, nextSelectedItem, nameof(SelectedNavigationItem));
        }

        await ActivatePageAsync(navigationItem.PageViewModel);
    }

    private async Task ActivatePageAsync(PageViewModelBase pageViewModel)
    {
        if (!ReferenceEquals(CurrentPageViewModel, pageViewModel))
        {
            CurrentPageViewModel?.Deactivate();
        }

        CurrentPageViewModel = pageViewModel;
        CurrentPageTitle = pageViewModel.Title;
        CurrentPageSubtitle = pageViewModel.Subtitle;
        await pageViewModel.ActivateAsync();
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
        _ = NavigateToAsync(pageKey, syncVisibleSelection: true);
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
    }
}
