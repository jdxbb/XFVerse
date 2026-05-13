using MediaLibrary.App.Models.Caches;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.Services;

public sealed class SoftwareCacheManagementService : ISoftwareCacheManagementService
{
    private readonly IPosterCacheService _posterCacheService;
    private readonly IExternalMetadataCacheMaintenanceService _externalMetadataCacheMaintenanceService;

    public SoftwareCacheManagementService(
        IPosterCacheService posterCacheService,
        IExternalMetadataCacheMaintenanceService externalMetadataCacheMaintenanceService)
    {
        _posterCacheService = posterCacheService;
        _externalMetadataCacheMaintenanceService = externalMetadataCacheMaintenanceService;
    }

    public async Task<SoftwareCacheOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var posterUsageTask = _posterCacheService.GetUsageAsync(cancellationToken);
        var posterSettingsTask = _posterCacheService.GetSettingsAsync(cancellationToken);
        var externalUsageTask = _externalMetadataCacheMaintenanceService.GetUsageAsync(cancellationToken);

        await Task.WhenAll(posterUsageTask, posterSettingsTask, externalUsageTask);

        return BuildOverview(
            posterUsageTask.Result,
            posterSettingsTask.Result,
            externalUsageTask.Result.ManagedEntryCount,
            externalUsageTask.Result.EstimatedBytes);
    }

    public async Task<SoftwareCacheOverview> SavePosterCacheLimitAsync(
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        await _posterCacheService.SaveSettingsAsync(
            new PosterCacheSettings { MaxBytes = maxBytes },
            cancellationToken);
        return await GetOverviewAsync(cancellationToken);
    }

    public async Task<SoftwareCacheClearResult> ClearAsync(
        SoftwareCacheCategoryKind kind,
        CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            SoftwareCacheCategoryKind.PosterCache => await ClearPosterCacheAsync(cancellationToken),
            SoftwareCacheCategoryKind.OtherCache => await ClearOtherCacheAsync(cancellationToken),
            _ => new SoftwareCacheClearResult
            {
                Kind = kind,
                Succeeded = false,
                Error = "UnsupportedCacheKind"
            }
        };
    }

    private static SoftwareCacheOverview BuildOverview(
        PosterCacheUsage posterUsage,
        PosterCacheSettings posterSettings,
        int externalEntryCount,
        long externalEstimatedBytes)
    {
        return new SoftwareCacheOverview
        {
            PosterCache = new SoftwareCacheCategoryModel
            {
                Kind = SoftwareCacheCategoryKind.PosterCache,
                Name = "海报缓存",
                Description = "缓存远程影片海报，清理后会按需重新生成。",
                UsedBytes = posterUsage.UsedBytes,
                ItemCount = posterUsage.FileCount,
                IsClearable = posterUsage.FileCount > 0,
                ClearUnavailableReason = posterUsage.FileCount > 0 ? string.Empty : "当前没有可清理的海报缓存。"
            },
            OtherCache = new SoftwareCacheCategoryModel
            {
                Kind = SoftwareCacheCategoryKind.OtherCache,
                Name = "其他缓存",
                Description = "仅包含可再生成的 TMDB / OMDb 外部元数据缓存，不包含用户状态或偏好。",
                UsedBytes = externalEstimatedBytes,
                ItemCount = externalEntryCount,
                IsClearable = externalEntryCount > 0,
                ClearUnavailableReason = externalEntryCount > 0 ? string.Empty : "当前没有可清理的其他缓存。"
            },
            PosterCacheMaxBytes = posterSettings.MaxBytes
        };
    }

    private async Task<SoftwareCacheClearResult> ClearPosterCacheAsync(CancellationToken cancellationToken)
    {
        var result = await _posterCacheService.ClearAsync(cancellationToken);
        return new SoftwareCacheClearResult
        {
            Kind = SoftwareCacheCategoryKind.PosterCache,
            Succeeded = result.Succeeded,
            DeletedItemCount = result.DeletedFileCount,
            FreedBytes = result.FreedBytes,
            Error = result.Error
        };
    }

    private async Task<SoftwareCacheClearResult> ClearOtherCacheAsync(CancellationToken cancellationToken)
    {
        var result = await _externalMetadataCacheMaintenanceService.ClearManagedAsync(cancellationToken);
        return new SoftwareCacheClearResult
        {
            Kind = SoftwareCacheCategoryKind.OtherCache,
            Succeeded = result.Succeeded,
            DeletedItemCount = result.DeletedEntryCount,
            FreedBytes = result.EstimatedFreedBytes,
            Error = result.Error
        };
    }
}
