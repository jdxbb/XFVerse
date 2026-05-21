using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ISingleSourceCorrectionService
{
    Task<SingleSourceCorrectionPreview> PreviewMovieCorrectionAsync(
        int mediaFileId,
        int tmdbMovieId,
        CancellationToken cancellationToken = default);

    Task<SingleSourceCorrectionApplyResult> ApplyMovieCorrectionAsync(
        int mediaFileId,
        int tmdbMovieId,
        CancellationToken cancellationToken = default);

    Task<SingleSourceCorrectionPreview> PreviewTvEpisodeCorrectionAsync(
        int mediaFileId,
        int seriesTmdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);

    Task<SingleSourceCorrectionApplyResult> ApplyTvEpisodeCorrectionAsync(
        int mediaFileId,
        int seriesTmdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnknownTvSeasonCorrectionTargetItem>> SearchUnknownSeasonTargetsAsync(
        string? query = null,
        CancellationToken cancellationToken = default);

    Task<SingleSourceCorrectionApplyResult> ApplyUnknownSeasonEpisodeCorrectionAsync(
        int mediaFileId,
        int targetSeasonId,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
