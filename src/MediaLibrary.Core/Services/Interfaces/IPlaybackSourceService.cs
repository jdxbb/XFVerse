using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IPlaybackSourceService
{
    Task<PlaybackSessionModel?> GetPlaybackSessionAsync(
        int movieId,
        int? preferredMediaFileId = null,
        CancellationToken cancellationToken = default);

    Task<PlaybackSessionModel?> GetEpisodePlaybackSessionAsync(
        int episodeId,
        int? preferredMediaFileId = null,
        CancellationToken cancellationToken = default);

    Task SetPreferredSubtitleAsync(
        int mediaFileId,
        int? subtitleMediaFileId,
        CancellationToken cancellationToken = default);
}
