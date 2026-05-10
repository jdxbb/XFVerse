using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface ILibraryQueryService
{
    Task<IReadOnlyList<LibraryMovieListItem>> GetLibraryMoviesAsync(
        CancellationToken cancellationToken = default);
}
