using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ITvMetadataHydrationService
{
    Task<TvMetadataHydrationResult> EnsureSeriesSummaryAsync(
        int tmdbSeriesId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<TvMetadataHydrationResult> EnsureSeriesSummaryBySeriesIdAsync(
        int tvSeriesId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<TvMetadataHydrationResult> HydrateSeriesAsync(
        int tmdbSeriesId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<TvMetadataHydrationResult> EnsureHydratedBySeriesIdAsync(
        int tvSeriesId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<TvMetadataHydrationResult> EnsureSeasonEpisodesAsync(
        int tvSeasonId,
        bool force = false,
        CancellationToken cancellationToken = default);
}
