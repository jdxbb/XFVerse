namespace MediaLibrary.Core.Services.Interfaces;

public interface IMediaProbeService
{
    Task EnqueueMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task EnqueueMovieSourcesAsync(
        int movieId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task ProbeMediaFileAsync(
        int mediaFileId,
        bool force = false,
        CancellationToken cancellationToken = default);
}
