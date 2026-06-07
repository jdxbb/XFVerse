using MediaLibrary.App.Models.Settings;

namespace MediaLibrary.App.Services.Interfaces;

public interface IAppBehaviorPreferencesService
{
    Task<AppBehaviorPreferencesModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppBehaviorPreferencesModel preferences, CancellationToken cancellationToken = default);
}
