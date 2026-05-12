using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IDiscoveryMovieStatusResolver
{
    Task<IReadOnlyDictionary<int, DiscoveryMovieStatus>> ResolveAsync(
        IEnumerable<int> tmdbIds,
        CancellationToken cancellationToken = default);
}
