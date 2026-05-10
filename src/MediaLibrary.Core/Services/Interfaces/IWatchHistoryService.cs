namespace MediaLibrary.Core.Services.Interfaces;

public interface IWatchHistoryService
{
    Task<int> StartAsync(
        int movieId,
        int mediaFileId,
        int initialPositionSeconds = 0,
        CancellationToken cancellationToken = default);

    Task<int> GetResumePositionAsync(int movieId, int mediaFileId, CancellationToken cancellationToken = default);

    Task<bool> SaveProgressAsync(
        int watchHistoryId,
        int positionSeconds,
        int durationWatchedSeconds,
        bool isCompleted,
        int? mediaDurationSeconds = null,
        CancellationToken cancellationToken = default);

    Task DiscardAsync(int watchHistoryId, CancellationToken cancellationToken = default);
}
