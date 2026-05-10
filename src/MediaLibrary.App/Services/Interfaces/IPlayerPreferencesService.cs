using MediaLibrary.App.Models.Player;

namespace MediaLibrary.App.Services.Interfaces;

public interface IPlayerPreferencesService
{
    Task<PlayerPreferencesModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(PlayerPreferencesModel preferences, CancellationToken cancellationToken = default);
}
