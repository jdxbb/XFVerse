using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMovieIdentificationService
{
    Task<IdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task<IdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        ScanTmdbSearchCache? tmdbSearchCache,
        CancellationToken cancellationToken = default);

    Task<MoviePlaceholderGroupingRunResult> AggregateUnidentifiedMediaFilesAsync(
        IReadOnlyCollection<int> scanPathIds,
        CancellationToken cancellationToken = default,
        string sourceKind = "unknown");

    Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(
        string query,
        int? releaseYear,
        CancellationToken cancellationToken = default);

    Task<bool> EnsureMovieCreditsAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task<AutoIdentifyResult> AutoIdentifyWithFirstResultAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task<int> ApplyManualMatchAsync(
        int movieId,
        int tmdbId,
        CancellationToken cancellationToken = default);

    Task<int> ApplyManualMediaFileMatchAsync(
        int mediaFileId,
        int tmdbId,
        CancellationToken cancellationToken = default);
}
