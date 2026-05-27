using MediaLibrary.App.Models.Library;

namespace MediaLibrary.App.Services.Interfaces;

public interface ILibraryPreferencesService
{
    Task<LibraryPreferencesModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LibraryPreferencesModel preferences, CancellationToken cancellationToken = default);
}
