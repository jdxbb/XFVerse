using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IHomeDashboardQueryService
{
    Task<HomeDashboardModel> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<HomeDashboardModel> GetLibraryOverviewAsync(CancellationToken cancellationToken = default);

    Task<HomeDashboardModel> GetScanOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HomeMovieItem>> GetRecentlyPlayedAsync(CancellationToken cancellationToken = default);

    Task<HomeDashboardModel> GetCollectionPreviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiRecommendationItem>> GetRecommendationsPreviewAsync(CancellationToken cancellationToken = default);

    Task<AiRecommendationPreviewState> GetRecommendationsPreviewStateAsync(CancellationToken cancellationToken = default);
}
