using MediaLibrary.App.Models.Caches;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.Services;

public sealed class SoftwareCacheManagementService : ISoftwareCacheManagementService
{
    private readonly IPosterCacheService _posterCacheService;
    private readonly IExternalMetadataCacheMaintenanceService _externalMetadataCacheMaintenanceService;
    private readonly IOnlineSubtitleCacheService _onlineSubtitleCacheService;

    public SoftwareCacheManagementService(
        IPosterCacheService posterCacheService,
        IExternalMetadataCacheMaintenanceService externalMetadataCacheMaintenanceService,
        IOnlineSubtitleCacheService onlineSubtitleCacheService)
    {
        _posterCacheService = posterCacheService;
        _externalMetadataCacheMaintenanceService = externalMetadataCacheMaintenanceService;
        _onlineSubtitleCacheService = onlineSubtitleCacheService;
    }

    public async Task<SoftwareCacheOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var posterUsageTask = _posterCacheService.GetUsageAsync(cancellationToken);
        var posterSettingsTask = _posterCacheService.GetSettingsAsync(cancellationToken);
        var externalUsageTask = _externalMetadataCacheMaintenanceService.GetUsageAsync(cancellationToken);
        var subtitleUsageTask = _onlineSubtitleCacheService.GetUsageAsync(cancellationToken);

        await Task.WhenAll(posterUsageTask, posterSettingsTask, externalUsageTask, subtitleUsageTask);

        return BuildOverview(
            posterUsageTask.Result,
            posterSettingsTask.Result,
            externalUsageTask.Result.ManagedEntryCount,
            externalUsageTask.Result.EstimatedBytes,
            subtitleUsageTask.Result);
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
            SoftwareCacheCategoryKind.SubtitleCache => await ClearSubtitleCacheAsync(cancellationToken),
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
        long externalEstimatedBytes,
        OnlineSubtitleCacheUsage subtitleUsage)
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
                ClearableBytes = posterUsage.UsedBytes,
                ClearableItemCount = posterUsage.FileCount,
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
                ClearableBytes = externalEstimatedBytes,
                ClearableItemCount = externalEntryCount,
                IsClearable = externalEntryCount > 0,
                ClearUnavailableReason = externalEntryCount > 0 ? string.Empty : "当前没有可清理的其他缓存。"
            },
            SubtitleCache = new SoftwareCacheCategoryModel
            {
                Kind = SoftwareCacheCategoryKind.SubtitleCache,
                Name = "在线字幕缓存",
                Description = "下载的在线字幕缓存文件。删除播放器绑定不会物理删除缓存，孤立文件可在这里清理。",
                UsedBytes = subtitleUsage.UsedBytes,
                ItemCount = subtitleUsage.FileCount,
                ClearableBytes = subtitleUsage.OrphanBytes,
                ClearableItemCount = subtitleUsage.OrphanFileCount,
                DetailText = BuildSubtitleCacheDetailText(subtitleUsage),
                IsClearable = subtitleUsage.ReferenceScanSucceeded && subtitleUsage.OrphanFileCount > 0,
                ClearUnavailableReason = BuildSubtitleCacheClearUnavailableReason(subtitleUsage)
            },
            PosterCacheMaxBytes = posterSettings.MaxBytes
        };
    }

    private static string BuildSubtitleCacheDetailText(OnlineSubtitleCacheUsage subtitleUsage)
    {
        if (!subtitleUsage.ReferenceScanSucceeded)
        {
            return "无法读取在线字幕绑定引用，暂不允许清理，以避免误删仍被使用的字幕缓存。";
        }

        return $"绑定保护 {subtitleUsage.ReferencedFileCount} 个文件，孤立可清理 {subtitleUsage.OrphanFileCount} 个文件。";
    }

    private static string BuildSubtitleCacheClearUnavailableReason(OnlineSubtitleCacheUsage subtitleUsage)
    {
        if (!subtitleUsage.ReferenceScanSucceeded)
        {
            return "无法读取在线字幕绑定引用，已禁用清理。";
        }

        return subtitleUsage.OrphanFileCount > 0
            ? string.Empty
            : "当前没有可清理的在线字幕孤立缓存。";
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

    private async Task<SoftwareCacheClearResult> ClearSubtitleCacheAsync(CancellationToken cancellationToken)
    {
        var result = await _onlineSubtitleCacheService.ClearAsync(cancellationToken);
        return new SoftwareCacheClearResult
        {
            Kind = SoftwareCacheCategoryKind.SubtitleCache,
            Succeeded = result.Succeeded,
            DeletedItemCount = result.DeletedFileCount,
            FreedBytes = result.FreedBytes,
            Error = result.Error
        };
    }
}
