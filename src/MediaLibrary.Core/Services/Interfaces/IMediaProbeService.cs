using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IMediaProbeService
{
    event EventHandler<MediaProbeStatusChangedEventArgs>? ProbeStatusChanged;

    Task<MediaProbeDetailLazyResult> EnqueueDetailSourcesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        string contentKind,
        int contentId,
        int limit = 10,
        CancellationToken cancellationToken = default);

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

public sealed record MediaProbeDetailLazyResult(
    int SourceCount,
    int CandidateCount,
    int QueuedCount,
    int SkippedCount,
    int Limit,
    IReadOnlyList<int> ProbeMediaFileIds);

public sealed record MediaProbeStatusChangedEventArgs(
    int MediaFileId,
    MediaProbeStatus Status,
    string MediaFileKind,
    ProtocolType ProtocolType);
