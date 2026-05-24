using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ILocalMediaScanService
{
    Task<ScanOverviewModel> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<ScanExecutionResult> RunScanAsync(
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressUpdate>? progress = null);

    Task<ScanExecutionResult> RunScanPathAsync(
        int scanPathId,
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressUpdate>? progress = null);
}
