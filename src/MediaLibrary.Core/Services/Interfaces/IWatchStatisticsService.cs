using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IWatchStatisticsService
{
    Task<WatchStatisticsSnapshot> GetStatisticsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    Task<WatchStatisticsSnapshot> GetStatisticsAsync(
        WatchStatisticsTimeRange timeRange,
        DateTime? calendarMonth = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
