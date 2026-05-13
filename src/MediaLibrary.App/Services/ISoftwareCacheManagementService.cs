using MediaLibrary.App.Models.Caches;

namespace MediaLibrary.App.Services;

public interface ISoftwareCacheManagementService
{
    Task<SoftwareCacheOverview> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<SoftwareCacheOverview> SavePosterCacheLimitAsync(
        long maxBytes,
        CancellationToken cancellationToken = default);

    Task<SoftwareCacheClearResult> ClearAsync(
        SoftwareCacheCategoryKind kind,
        CancellationToken cancellationToken = default);
}
