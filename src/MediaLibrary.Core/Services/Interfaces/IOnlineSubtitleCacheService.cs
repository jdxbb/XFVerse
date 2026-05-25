using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IOnlineSubtitleCacheService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    Task<OnlineSubtitleCacheSaveResult> SaveAsync(
        string provider,
        string providerFileId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<OnlineSubtitleCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default);

    Task<OnlineSubtitleCacheClearResult> ClearAsync(CancellationToken cancellationToken = default);

    string GetAbsolutePath(string relativePath);
}
