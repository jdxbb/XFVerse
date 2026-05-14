using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IDiscoveryTvSeriesStatusResolver
{
    Task<IReadOnlyDictionary<int, DiscoveryTvSeriesStatus>> ResolveAsync(
        IEnumerable<int> tmdbSeriesIds,
        CancellationToken cancellationToken = default);
}
