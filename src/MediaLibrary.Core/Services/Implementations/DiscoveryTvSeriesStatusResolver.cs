using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class DiscoveryTvSeriesStatusResolver : IDiscoveryTvSeriesStatusResolver
{
    public async Task<IReadOnlyDictionary<int, DiscoveryTvSeriesStatus>> ResolveAsync(
        IEnumerable<int> tmdbSeriesIds,
        CancellationToken cancellationToken = default)
    {
        var ids = tmdbSeriesIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, DiscoveryTvSeriesStatus>();
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var seriesRows = await dbContext.TvSeries
            .AsNoTracking()
            .Where(series => series.TmdbSeriesId.HasValue && ids.Contains(series.TmdbSeriesId.Value))
            .Select(
                series => new
                {
                    series.Id,
                    TmdbSeriesId = series.TmdbSeriesId!.Value,
                    series.Name,
                    OriginalName = series.OriginalName ?? string.Empty,
                    Overview = series.Overview ?? string.Empty,
                    PosterRemoteUrl = series.PosterRemoteUrl ?? string.Empty,
                    GenresText = series.GenresText ?? string.Empty,
                    series.FirstAirYear,
                    Country = series.Country ?? string.Empty,
                    Language = series.Language ?? string.Empty,
                    PlayableSeasonCount = series.Seasons.Count(
                        season => season.Episodes.Any(
                            episode => episode.MediaFiles.Any(file => !file.IsDeleted))),
                    series.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var collectionRows = await dbContext.UserTvSeasonCollectionItems
            .AsNoTracking()
            .Where(item => item.TmdbSeriesId.HasValue && ids.Contains(item.TmdbSeriesId.Value))
            .Select(
                item => new
                {
                    TmdbSeriesId = item.TmdbSeriesId!.Value,
                    item.TvSeriesId,
                    item.IsWantToWatch,
                    item.IsFavorite,
                    item.IsNotInterested
                })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, DiscoveryTvSeriesStatus>();
        foreach (var group in seriesRows.GroupBy(row => row.TmdbSeriesId))
        {
            var series = group
                .OrderByDescending(row => row.PlayableSeasonCount)
                .ThenByDescending(row => row.UpdatedAt)
                .First();
            var inLibrarySeasonCount = group.Sum(row => row.PlayableSeasonCount);

            result[group.Key] = new DiscoveryTvSeriesStatus
            {
                TmdbSeriesId = group.Key,
                TvSeriesId = series.Id,
                IsInLibrary = inLibrarySeasonCount > 0,
                InLibrarySeasonCount = inLibrarySeasonCount,
                Name = series.Name,
                OriginalName = series.OriginalName,
                Overview = series.Overview,
                PosterRemoteUrl = series.PosterRemoteUrl,
                GenresText = series.GenresText,
                FirstAirYear = series.FirstAirYear,
                Country = series.Country,
                Language = series.Language
            };
        }

        foreach (var group in collectionRows.GroupBy(row => row.TmdbSeriesId))
        {
            if (!result.TryGetValue(group.Key, out var status))
            {
                status = new DiscoveryTvSeriesStatus
                {
                    TmdbSeriesId = group.Key,
                    TvSeriesId = group.Select(row => row.TvSeriesId).FirstOrDefault(id => id.HasValue),
                    IsInLibrary = false
                };
                result[group.Key] = status;
            }

            status.HasWantToWatchSeason = group.Any(row => row.IsWantToWatch);
            status.HasFavoriteSeason = group.Any(row => row.IsFavorite);
            status.HasNotInterestedSeason = group.Any(row => row.IsNotInterested);
        }

        return result;
    }
}
