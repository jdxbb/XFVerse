using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IWatchProfileService
{
    Task<WatchProfileSnapshot> GetProfileAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
