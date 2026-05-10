using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMovieIdentificationService
{
    Task<IdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(
        string query,
        int? releaseYear,
        CancellationToken cancellationToken = default);

    Task<AutoIdentifyResult> AutoIdentifyWithFirstResultAsync(
        int movieId,
        CancellationToken cancellationToken = default);

    Task<int> ApplyManualMatchAsync(
        int movieId,
        int tmdbId,
        CancellationToken cancellationToken = default);
}
