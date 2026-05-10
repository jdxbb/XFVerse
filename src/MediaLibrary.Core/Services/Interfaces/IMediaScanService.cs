using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMediaScanService
{
    Task<ScanOverviewModel> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<ScanExecutionResult> RunScanAsync(CancellationToken cancellationToken = default);
}
