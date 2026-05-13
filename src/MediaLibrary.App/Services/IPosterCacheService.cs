using MediaLibrary.App.Models.Caches;

namespace MediaLibrary.App.Services;

public interface IPosterCacheService
{
    Task<string> GetCachedOrFallbackAsync(
        string? source,
        CancellationToken cancellationToken = default);

    Task<string> RefreshAsync(
        string source,
        CancellationToken cancellationToken = default);

    Task<PosterCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default);

    Task<PosterCacheSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        PosterCacheSettings settings,
        CancellationToken cancellationToken = default);

    Task<PosterCacheClearResult> ClearAsync(CancellationToken cancellationToken = default);

    Task<PosterCacheTrimResult> TrimToLimitAsync(
        long? maxBytes = null,
        CancellationToken cancellationToken = default);
}
