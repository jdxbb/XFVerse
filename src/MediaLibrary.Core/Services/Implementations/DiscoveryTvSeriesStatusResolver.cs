using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
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
                    DirectorText = series.DirectorText ?? string.Empty,
                    ActorsText = series.ActorsText ?? string.Empty,
                    TmdbRating = series.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    TmdbVoteCount = series.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    OmdbScoreValue = series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    OmdbScoreScale = series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreScale)
                        .FirstOrDefault(),
                    OmdbVoteCount = series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    OmdbSourceUrl = series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.SourceUrl ?? string.Empty)
                        .FirstOrDefault() ?? string.Empty,
                    OmdbLastUpdatedAt = series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.LastUpdatedAt ?? rating.CreatedAt)
                        .FirstOrDefault(),
                    EpisodeCount = series.Seasons.Sum(season => season.Episodes.Count),
                    WatchedEpisodeCount = series.Seasons.Sum(season => season.Episodes.Count(episode => episode.IsWatched)),
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
                    item.IsNotInterested,
                    item.LibraryVisibilityState
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
                IsVisibleInLibrary = inLibrarySeasonCount > 0,
                InLibrarySeasonCount = inLibrarySeasonCount,
                IsWatched = group.Sum(row => row.EpisodeCount) > 0
                            && group.Sum(row => row.WatchedEpisodeCount) >= group.Sum(row => row.EpisodeCount),
                Name = series.Name,
                OriginalName = series.OriginalName,
                Overview = series.Overview,
                PosterRemoteUrl = series.PosterRemoteUrl,
                GenresText = series.GenresText,
                FirstAirYear = series.FirstAirYear,
                Country = series.Country,
                Language = series.Language,
                DirectorText = series.DirectorText,
                ActorsText = series.ActorsText,
                TmdbRating = series.TmdbRating,
                TmdbVoteCount = series.TmdbVoteCount,
                OmdbScoreValue = series.OmdbScoreValue,
                OmdbScoreScale = series.OmdbScoreScale,
                OmdbVoteCount = series.OmdbVoteCount,
                OmdbSourceUrl = series.OmdbSourceUrl,
                OmdbLastUpdatedAt = series.OmdbLastUpdatedAt
            };
        }

        foreach (var group in collectionRows.GroupBy(row => row.TmdbSeriesId))
        {
            if (!result.TryGetValue(group.Key, out var status))
            {
                var visibilityState = ResolveLibraryVisibilityState(group.Select(row => row.LibraryVisibilityState));
                var hasState = group.Any(row => row.IsWantToWatch || row.IsFavorite || row.IsNotInterested);
                status = new DiscoveryTvSeriesStatus
                {
                    TmdbSeriesId = group.Key,
                    TvSeriesId = group.Select(row => row.TvSeriesId).FirstOrDefault(id => id.HasValue),
                    IsInLibrary = false,
                    IsVisibleInLibrary = ResolveIsVisibleInLibrary(false, visibilityState, hasState),
                    HasHiddenSeason = group.Any(row => row.LibraryVisibilityState == LibraryVisibilityState.Hidden),
                    LibraryVisibilityState = visibilityState
                };
                result[group.Key] = status;
            }

            status.HasWantToWatchSeason = group.Any(row => row.IsWantToWatch);
            status.HasFavoriteSeason = group.Any(row => row.IsFavorite);
            status.HasNotInterestedSeason = group.Any(row => row.IsNotInterested);
            status.HasHiddenSeason = group.Any(row => row.LibraryVisibilityState == LibraryVisibilityState.Hidden);
            status.LibraryVisibilityState = ResolveLibraryVisibilityState(group.Select(row => row.LibraryVisibilityState));
            status.IsVisibleInLibrary = ResolveIsVisibleInLibrary(
                status.IsInLibrary,
                status.LibraryVisibilityState,
                status.HasWantToWatchSeason || status.HasFavoriteSeason || status.HasNotInterestedSeason);
        }

        return result;
    }

    private static bool ResolveIsVisibleInLibrary(
        bool hasActiveSource,
        LibraryVisibilityState visibilityState,
        bool hasCurrentState)
    {
        return visibilityState switch
        {
            LibraryVisibilityState.Hidden => false,
            LibraryVisibilityState.Visible => true,
            _ => hasActiveSource || hasCurrentState
        };
    }

    private static LibraryVisibilityState ResolveLibraryVisibilityState(IEnumerable<LibraryVisibilityState> states)
    {
        return states
            .OrderByDescending(state => state != LibraryVisibilityState.Auto)
            .FirstOrDefault();
    }
}
