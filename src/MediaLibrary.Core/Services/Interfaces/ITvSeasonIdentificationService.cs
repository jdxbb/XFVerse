using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITvSeasonIdentificationService
{
    Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult? directoryAnalysis,
        CancellationToken cancellationToken = default);

    Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult? directoryAnalysis,
        ScanTmdbSearchCache? tmdbSearchCache,
        CancellationToken cancellationToken = default);

    Task<int> ApplyManualMediaFileMatchAsync(
        int mediaFileId,
        int seriesTmdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
