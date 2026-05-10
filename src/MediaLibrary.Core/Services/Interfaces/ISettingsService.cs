using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Settings;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ISettingsService
{
    Task<ApplicationSettingModel> GetApplicationSettingAsync(CancellationToken cancellationToken = default);

    Task<ApplicationSettingModel> SaveApplicationSettingAsync(
        ApplicationSettingModel settings,
        CancellationToken cancellationToken = default);

    Task<WebDavConnectionModel> GetPrimaryConnectionAsync(CancellationToken cancellationToken = default);

    Task<WebDavConnectionModel> SaveConnectionAsync(
        WebDavConnectionModel connectionModel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanPath>> GetScanPathsAsync(
        int sourceConnectionId,
        CancellationToken cancellationToken = default);

    Task<ScanPath> SaveScanPathAsync(ScanPath scanPath, CancellationToken cancellationToken = default);

    Task DeleteScanPathAsync(int scanPathId, CancellationToken cancellationToken = default);

    Task SetScanPathEnabledAsync(int scanPathId, bool isEnabled, CancellationToken cancellationToken = default);
}
