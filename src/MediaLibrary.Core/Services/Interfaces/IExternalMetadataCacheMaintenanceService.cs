using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IExternalMetadataCacheMaintenanceService
{
    Task<ExternalMetadataCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default);

    Task<ExternalMetadataCacheClearResult> ClearManagedAsync(CancellationToken cancellationToken = default);
}
