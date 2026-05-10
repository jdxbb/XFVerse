using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IVideoCacheService
{
    event EventHandler<VideoCacheChangedEventArgs>? StatusChanged;

    Task<VideoCacheStatusResult> GetStatusAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default);

    Task<VideoCacheAcquireResult> AcquirePlaybackAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default);

    Task EnqueueDownloadAsync(
        PlaybackSourceItem source,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task CancelDownloadAsync(
        int mediaFileId,
        CancellationToken cancellationToken = default);

    Task TryEnqueueAutoDownloadAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default);

    Task<VideoCacheStatusResult> DeleteCacheAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default);

    Task<VideoCacheSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        VideoCacheSettingsModel settings,
        CancellationToken cancellationToken = default);

    Task<VideoCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default);

    Task<VideoCacheClearResult> ClearAllAsync(CancellationToken cancellationToken = default);

    Task CleanupAsync(CancellationToken cancellationToken = default);
}
