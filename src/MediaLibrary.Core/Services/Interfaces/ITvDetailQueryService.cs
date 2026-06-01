using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITvDetailQueryService
{
    Task<TvSeriesOverviewModel?> GetSeriesOverviewAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    Task<TvSeasonDetailModel?> GetSeasonDetailAsync(
        int seasonId,
        CancellationToken cancellationToken = default);

    Task<TvEpisodeDetailModel?> GetEpisodeDetailAsync(
        int episodeId,
        int? preferredMediaFileId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecognizedTvSeasonCorrectionTargetItem>> GetRecognizedSeasonCorrectionTargetsAsync(
        CancellationToken cancellationToken = default);

    Task<string> GetSeasonTmdbRatingDisplayAsync(
        int seasonId,
        CancellationToken cancellationToken = default);

    Task<string> GetSeasonImdbSeriesRatingDisplayAsync(
        int seasonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovieRatingItem>> GetSeriesRatingsAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    Task<MovieRatingItem> GetSeasonTmdbRatingAsync(
        int seasonId,
        CancellationToken cancellationToken = default);

    Task<MovieRatingItem> GetEpisodeTmdbRatingAsync(
        int episodeId,
        CancellationToken cancellationToken = default);
}
