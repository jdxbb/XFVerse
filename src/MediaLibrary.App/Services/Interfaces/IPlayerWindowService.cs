namespace MediaLibrary.App.Services.Interfaces;

public interface IPlayerWindowService
{
    event EventHandler? PlayerWindowClosed;

    bool IsPlayerOpen { get; }

    int? ActiveMovieId { get; }

    int? ActiveEpisodeId { get; }

    int? ActiveMediaFileId { get; }

    Task OpenAsync(int movieId, int? mediaFileId = null, CancellationToken cancellationToken = default);

    Task OpenEpisodeAsync(int episodeId, int? mediaFileId = null, CancellationToken cancellationToken = default);
}
