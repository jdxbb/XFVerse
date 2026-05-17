using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITvScanDirectoryAnalysisService
{
    Task<TvScanDirectoryAnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task<int> ApplyAiOnUncertainAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult analysis,
        CancellationToken cancellationToken = default);
}
