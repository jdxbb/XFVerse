using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ILibraryQueryService
{
    Task<IReadOnlyList<LibraryMovieListItem>> GetLibraryMoviesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryMovieListItem>> GetLibraryItemsAsync(
        bool expandSeriesToSeasons,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryMovieListItem>> GetHiddenLibraryItemsAsync(
        CancellationToken cancellationToken = default);
}
