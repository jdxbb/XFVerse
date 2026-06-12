using MediaLibrary.App.Models.Profile;

namespace MediaLibrary.App.Services.Interfaces;

public interface IUserProfileService
{
    event EventHandler<UserProfileModel>? ProfileChanged;

    Task<UserProfileModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserProfileModel profile, CancellationToken cancellationToken = default);
}
