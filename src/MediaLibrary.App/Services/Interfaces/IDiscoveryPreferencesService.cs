using MediaLibrary.App.Models.Discovery;

namespace MediaLibrary.App.Services.Interfaces;

public interface IDiscoveryPreferencesService
{
    Task<DiscoveryPreferencesModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DiscoveryPreferencesModel preferences, CancellationToken cancellationToken = default);
}
