using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.Playback;
using MediaLibrary.App.Playback.Mpv;
using MediaLibrary.App.ViewModels.Main;
using MediaLibrary.App.ViewModels.Pages;
using MediaLibrary.Core.Services.Implementations;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MediaLibrary.App.Services;

public static class AppServiceProvider
{
    private static ServiceProvider? _serviceProvider;

    public static void Initialize()
    {
        if (_serviceProvider is not null)
        {
            return;
        }

        var services = new ServiceCollection();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IWebDavService, WebDavService>();
        services.AddSingleton<IWebDavDownloadService, WebDavDownloadService>();
        services.AddSingleton<IVideoCacheService, VideoCacheService>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ITmdbService, TmdbService>();
        services.AddSingleton<IOmdbService, OmdbService>();
        services.AddSingleton<IOpenSubtitlesClientService, OpenSubtitlesClientService>();
        services.AddSingleton<OnlineSubtitleBindingQueryService>();
        services.AddSingleton<IOnlineSubtitleBindingQueryService>(provider => provider.GetRequiredService<OnlineSubtitleBindingQueryService>());
        services.AddSingleton<IOnlineSubtitleBindingService>(provider => provider.GetRequiredService<OnlineSubtitleBindingQueryService>());
        services.AddSingleton<ISubtitleBindingService, SubtitleBindingService>();
        services.AddSingleton<ITvMetadataHydrationService, TvMetadataHydrationService>();
        services.AddSingleton<ITvScanDirectoryAnalysisService, TvScanDirectoryAnalysisService>();
        services.AddSingleton<ITvSeasonIdentificationService, TvSeasonIdentificationService>();
        services.AddSingleton<IMovieIdentificationService, MovieIdentificationService>();
        services.AddSingleton<ISingleSourceCorrectionService, SingleSourceCorrectionService>();
        services.AddSingleton<IBatchAiCorrectionService, BatchAiCorrectionService>();
        services.AddSingleton<IManualUnknownSeasonAggregationService, ManualUnknownSeasonAggregationService>();
        services.AddSingleton<IUnknownSeasonCorrectionService, UnknownSeasonCorrectionService>();
        services.AddSingleton<IRescanReattachService, RescanReattachService>();
        services.AddSingleton<IUnknownTvSeasonAppendService, UnknownTvSeasonAppendService>();
        services.AddSingleton<IMediaProbeService, MediaProbeService>();
        services.AddSingleton<IMediaScanService, MediaScanService>();
        services.AddSingleton<ILocalMediaScanService, LocalMediaScanService>();
        services.AddSingleton<ILibraryQueryService, LibraryQueryService>();
        services.AddSingleton<IMovieDetailQueryService, MovieDetailQueryService>();
        services.AddSingleton<IMovieMetadataRefreshService, MovieMetadataRefreshService>();
        services.AddSingleton<ITvDetailQueryService, TvDetailQueryService>();
        services.AddSingleton<IMovieManagementService, MovieManagementService>();
        services.AddSingleton<IPlaybackSourceService, PlaybackSourceService>();
        services.AddSingleton<IWatchHistoryService, WatchHistoryService>();
        services.AddSingleton<IHomeDashboardQueryService, HomeDashboardQueryService>();
        services.AddSingleton<IAiService, AiService>();
        services.AddSingleton<IAiClassificationService, AiClassificationService>();
        services.AddSingleton<IRecommendationPreferenceService, RecommendationPreferenceService>();
        services.AddSingleton<IRecommendationService, RecommendationService>();
        services.AddSingleton<IUserCollectionService, UserCollectionService>();
        services.AddSingleton<ITvSeasonCollectionService, TvSeasonCollectionService>();
        services.AddSingleton<IDiscoveryMovieStatusResolver, DiscoveryMovieStatusResolver>();
        services.AddSingleton<IDiscoveryTvSeriesStatusResolver, DiscoveryTvSeriesStatusResolver>();
        services.AddSingleton<IDiscoveryRatingRefreshService, DiscoveryRatingRefreshService>();
        services.AddSingleton<IWatchInsightCacheService, WatchInsightCacheService>();
        services.AddSingleton<IWatchStatisticsService, WatchStatisticsService>();
        services.AddSingleton<IWatchProfileInputService, WatchProfileInputService>();
        services.AddSingleton<IWatchProfileService, WatchProfileService>();
        services.AddSingleton<IExternalMetadataCacheMaintenanceService, ExternalMetadataCacheMaintenanceService>();
        services.AddSingleton<IOnlineSubtitleCacheService, OnlineSubtitleCacheService>();

        services.AddSingleton<INavigationStateService, NavigationStateService>();
        services.AddSingleton<IDataRefreshService, DataRefreshService>();
        services.AddSingleton<IConfirmationDialogService, ConfirmationDialogService>();
        services.AddSingleton<IScanPathPickerService, ScanPathPickerService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAppBehaviorPreferencesService, AppBehaviorPreferencesService>();
        services.AddSingleton<IStartupWebDavScanService, StartupWebDavScanService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IPlayerPreferencesService, PlayerPreferencesService>();
        services.AddSingleton<ILibraryPreferencesService, LibraryPreferencesService>();
        services.AddSingleton<IDiscoveryPreferencesService, DiscoveryPreferencesService>();
        services.AddSingleton<IUserProfileService, UserProfileService>();
        services.AddSingleton<IPlayerWindowService, PlayerWindowService>();
        services.AddSingleton<IPosterCacheService, PosterCacheService>();
        services.AddSingleton<ISoftwareCacheManagementService, SoftwareCacheManagementService>();
        services.AddSingleton<IPlaybackEngineFactory, MpvPlaybackEngineFactory>();

        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<MovieDiscoveryViewModel>();
        services.AddSingleton<WatchHistoryViewModel>();
        services.AddSingleton<MovieDetailViewModel>();
        services.AddSingleton<SeriesOverviewViewModel>();
        services.AddSingleton<TvSeasonDetailViewModel>();
        services.AddSingleton<EpisodeDetailViewModel>();
        services.AddSingleton<ScanTasksViewModel>();
        services.AddSingleton<RecommendationsViewModel>();
        services.AddSingleton<WatchInsightsViewModel>();
        services.AddSingleton<FavoritesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<ViewModels.Player.PlayerWindowViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public static T GetRequiredService<T>() where T : notnull
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Application service provider has not been initialized.");
        }

        return _serviceProvider.GetRequiredService<T>();
    }

    public static void Dispose()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}
