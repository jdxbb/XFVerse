using System.Collections.ObjectModel;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Main;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly Dictionary<NavigationPageKey, NavigationItemViewModel> _navigationMap;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private NavigationItemViewModel? _selectedNavigationItem;
    private PageViewModelBase? _currentPageViewModel;
    private string _currentPageTitle = string.Empty;
    private string _currentPageSubtitle = string.Empty;

    public MainWindowViewModel(
        INavigationStateService navigationStateService,
        ISettingsService settingsService,
        IThemeService themeService,
        HomeViewModel homeViewModel,
        LibraryViewModel libraryViewModel,
        MovieDetailViewModel movieDetailViewModel,
        ScanTasksViewModel scanTasksViewModel,
        DuplicatesViewModel duplicatesViewModel,
        RecommendationsViewModel recommendationsViewModel,
        WatchInsightsViewModel watchInsightsViewModel,
        FavoritesViewModel favoritesViewModel,
        SettingsViewModel settingsViewModel)
    {
        NavigationStateService = navigationStateService;
        _settingsService = settingsService;
        _themeService = themeService;

        NavigationItems =
        [
            new NavigationItemViewModel(NavigationPageKey.Home, "首页", homeViewModel),
            new NavigationItemViewModel(NavigationPageKey.Library, "资源库", libraryViewModel),
            new NavigationItemViewModel(NavigationPageKey.MovieDetail, "详情", movieDetailViewModel),
            new NavigationItemViewModel(NavigationPageKey.ScanTasks, "扫描任务", scanTasksViewModel),
            new NavigationItemViewModel(NavigationPageKey.Duplicates, "重复资源", duplicatesViewModel),
            new NavigationItemViewModel(NavigationPageKey.Recommendations, "AI 推荐", recommendationsViewModel),
            new NavigationItemViewModel(NavigationPageKey.Favorites, "收藏夹", favoritesViewModel),
            new NavigationItemViewModel(NavigationPageKey.Settings, "设置", settingsViewModel)
        ];

        NavigationItems.Insert(6, new NavigationItemViewModel(NavigationPageKey.WatchInsights, "观影洞察", watchInsightsViewModel));

        _navigationMap = NavigationItems.ToDictionary(item => item.PageKey);
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);

        NavigationStateService.NavigationRequested += OnNavigationRequested;
        SelectedNavigationItem = NavigationItems.First();
    }

    private INavigationStateService NavigationStateService { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public AsyncRelayCommand ToggleThemeCommand { get; }

    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (!SetProperty(ref _selectedNavigationItem, value) || value is null)
            {
                return;
            }

            if (!ReferenceEquals(CurrentPageViewModel, value.PageViewModel))
            {
                CurrentPageViewModel?.Deactivate();
            }

            CurrentPageViewModel = value.PageViewModel;
            CurrentPageTitle = value.PageViewModel.Title;
            CurrentPageSubtitle = value.PageViewModel.Subtitle;
            _ = value.PageViewModel.ActivateAsync();
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
        if (!_navigationMap.TryGetValue(request.PageKey, out var navigationItem))
        {
            return;
        }

        if (!ReferenceEquals(SelectedNavigationItem, navigationItem))
        {
            SelectedNavigationItem = navigationItem;
            return;
        }

        await navigationItem.PageViewModel.ActivateAsync();
    }

    private async Task ToggleThemeAsync()
    {
        var settings = await _settingsService.GetApplicationSettingAsync();
        var nextTheme = string.Equals(settings.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        await _themeService.ApplyAndSaveAsync(nextTheme);
    }
}
