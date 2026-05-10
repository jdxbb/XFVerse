using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IWatchProfileInputService
{
    Task<WatchProfileInputSnapshot> BuildProfileInputAsync(
        CancellationToken cancellationToken = default);
}
