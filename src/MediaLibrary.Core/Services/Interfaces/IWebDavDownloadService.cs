using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IWebDavDownloadService
{
    Task DownloadAsync(
        WebDavDownloadRequest request,
        IProgress<VideoCacheDownloadProgress>? progress,
        CancellationToken cancellationToken = default);

    Task<WebDavRangeDownloadResult> DownloadRangeAsync(
        WebDavDownloadRequest request,
        long start,
        long end,
        CancellationToken cancellationToken = default);

    Task<WebDavRangeStreamResult> OpenRangeStreamAsync(
        WebDavDownloadRequest request,
        long start,
        long end,
        CancellationToken cancellationToken = default);
}
