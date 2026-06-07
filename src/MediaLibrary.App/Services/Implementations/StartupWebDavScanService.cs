using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.Services.Implementations;

public sealed class StartupWebDavScanService : IStartupWebDavScanService
{
    private readonly IAppBehaviorPreferencesService _appBehaviorPreferencesService;
    private readonly IMediaScanService _mediaScanService;
    private readonly IDataRefreshService _dataRefreshService;
    private int _hasQueued;

    public StartupWebDavScanService(
        IAppBehaviorPreferencesService appBehaviorPreferencesService,
        IMediaScanService mediaScanService,
        IDataRefreshService dataRefreshService)
    {
        _appBehaviorPreferencesService = appBehaviorPreferencesService;
        _mediaScanService = mediaScanService;
        _dataRefreshService = dataRefreshService;
    }

    public void QueueStartupScan()
    {
        if (Interlocked.Exchange(ref _hasQueued, 1) != 0)
        {
            return;
        }

        _ = Task.Run(RunStartupScanAsync);
    }

    private async Task RunStartupScanAsync()
    {
        try
        {
            var preferences = await _appBehaviorPreferencesService.LoadAsync();
            if (!preferences.AutoScanWebDavOnStartup)
            {
                return;
            }

            AiPerfDiagnostics.WriteEvent("auto-webdav-scan-started");
            var result = await _mediaScanService.RunScanAsync();
            _dataRefreshService.NotifyScanChanged();
            AiPerfDiagnostics.WriteEvent(
                $"auto-webdav-scan-completed scanned={result.TotalScannedCount} new={result.NewFileCount} updated={result.UpdatedFileCount} errors={result.ErrorCount}");
        }
        catch (Exception exception)
        {
            AiPerfDiagnostics.WriteEvent(
                $"auto-webdav-scan-failed errorType={exception.GetType().Name}");
        }
    }
}
