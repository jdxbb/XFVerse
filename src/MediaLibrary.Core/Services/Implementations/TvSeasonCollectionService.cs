using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TvSeasonCollectionService : ITvSeasonCollectionService
{
    public async Task<IReadOnlyList<CollectionMovieItem>> GetCollectionItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var items = await dbContext.UserTvSeasonCollectionItems
            .AsNoTracking()
            .Where(x => x.IsFavorite || x.IsWantToWatch || x.IsNotInterested)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(
                x => new
                {
                    x.Id,
                    x.TvSeasonId,
                    x.TvSeriesId,
                    x.TmdbSeriesId,
                    x.SeasonNumber,
                    x.SeriesTitle,
                    x.OriginalSeriesTitle,
                    x.SeasonTitle,
                    x.FirstAirYear,
                    x.AirDate,
                    x.PosterRemoteUrl,
                    x.Overview,
                    x.GenresText,
                    x.Country,
                    x.Language,
                    x.IsFavorite,
                    x.IsWantToWatch,
                    x.IsNotInterested,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var seasonIds = items
            .Where(x => x.TvSeasonId.HasValue)
            .Select(x => x.TvSeasonId!.Value)
            .Distinct()
            .ToArray();
        var seasonRows = seasonIds.Length == 0
            ? []
            : await LoadSeasonRowsAsync(dbContext, seasonIds, cancellationToken);
        var seasonIndex = seasonRows.ToDictionary(x => x.SeasonId);

        return items
            .Select(
                item =>
                {
                    seasonIndex.TryGetValue(item.TvSeasonId ?? 0, out var season);
                    var title = BuildSeasonTitle(
                        season?.SeriesName ?? item.SeriesTitle,
                        season?.SeasonName ?? item.SeasonTitle,
                        season?.SeasonNumber ?? item.SeasonNumber);
                    var totalEpisodeCount = ResolveTotalEpisodeCount(season?.TotalEpisodeCount, season?.EpisodeCount ?? 0);
                    var watchedEpisodeCount = season?.WatchedEpisodeCount ?? 0;
                    var isWatched = IsAggregateWatched(watchedEpisodeCount, season?.EpisodeCount ?? 0, totalEpisodeCount);
                    var isUnwatched = watchedEpisodeCount == 0;
                    var isFavorite = item.IsFavorite && isWatched;
                    var isWantToWatch = item.IsWantToWatch && isUnwatched;
                    return new CollectionMovieItem
                    {
                        IsTvSeason = true,
                        MovieId = null,
                        TvSeasonId = item.TvSeasonId,
                        TvSeriesId = season?.SeriesId ?? item.TvSeriesId,
                        TmdbId = item.TmdbSeriesId,
                        SeasonNumber = season?.SeasonNumber ?? item.SeasonNumber,
                        Title = title,
                        OriginalTitle = season?.OriginalSeriesName ?? item.OriginalSeriesTitle,
                        ReleaseYear = season?.AirYear ?? item.FirstAirYear,
                        PosterRemoteUrl = FirstNonEmpty(season?.SeasonPosterRemoteUrl, item.PosterRemoteUrl, season?.SeriesPosterRemoteUrl),
                        Overview = FirstNonEmpty(season?.SeasonOverview, item.Overview),
                        GenresText = FirstNonEmpty(season?.GenresText, item.GenresText),
                        Country = item.Country,
                        Language = item.Language,
                        IsLiked = isFavorite,
                        IsWantToWatch = isWantToWatch,
                        IsWatched = isWatched,
                        IsNotInterested = item.IsNotInterested,
                        IsInLibrary = season?.InLibraryEpisodeCount > 0,
                        WatchedEpisodeCount = watchedEpisodeCount,
                        TotalEpisodeCount = totalEpisodeCount,
                        InLibraryEpisodeCount = season?.InLibraryEpisodeCount ?? 0,
                        SourceSummary = FormatSourceSummary(season?.HasLocalSource == true, season?.HasWebDavSource == true),
                        UpdatedAt = item.UpdatedAt
                    };
                })
            .ToList();
    }

    public async Task SetFavoriteAsync(
        int tvSeasonId,
        bool isFavorite,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await SetStateAsync(tvSeasonId, StateFavorite, isFavorite, cancellationToken, changeSource);
    }

    public async Task SetWantToWatchAsync(
        int tvSeasonId,
        bool isWantToWatch,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await SetStateAsync(tvSeasonId, StateWantToWatch, isWantToWatch, cancellationToken, changeSource);
    }

    public async Task SetNotInterestedAsync(
        int tvSeasonId,
        bool isNotInterested,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await SetStateAsync(tvSeasonId, StateNotInterested, isNotInterested, cancellationToken, changeSource);
    }

    public async Task SetWatchedAsync(
        int tvSeasonId,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == tvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("电视剧季不存在。");

        var now = DateTime.UtcNow;
        var episodes = await dbContext.TvEpisodes
            .Include(x => x.MediaFiles)
            .Where(x => x.TvSeasonId == tvSeasonId)
            .ToListAsync(cancellationToken);
        var totalEpisodeCount = ResolveTotalEpisodeCount(season, episodes);
        var oldAggregateWatched = IsAggregateWatched(episodes.Count(x => x.IsWatched), episodes.Count, totalEpisodeCount);
        var oldAggregateUnwatched = IsAggregateUnwatched(episodes);

        foreach (var episode in episodes)
        {
            ApplyEpisodeWatchedState(episode, isWatched, now);
        }

        var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
        var oldFavorite = item?.IsFavorite ?? false;
        var oldWantToWatch = item?.IsWantToWatch ?? false;
        if (item is not null)
        {
            ApplySeasonSnapshot(item, season);
            if (isWatched)
            {
                item.IsWantToWatch = false;
                RestoreAutoVisibilityForPositiveState(item, isPositiveState: true);
            }
            else
            {
                item.IsFavorite = false;
            }

            item.UpdatedAt = now;
        }

        var newAggregateWatched = IsAggregateWatched(episodes.Count(x => x.IsWatched), episodes.Count, totalEpisodeCount);
        var newAggregateUnwatched = IsAggregateUnwatched(episodes);
        season.UpdatedAt = now;
        RecordStateChange(
            dbContext,
            season,
            item,
            StateWatched,
            oldAggregateWatched,
            newAggregateWatched,
            changeSource,
            now);
        if (!isWatched && oldAggregateUnwatched && newAggregateUnwatched)
        {
            RecordStateTouch(dbContext, season, item, StateUnwatched, true, changeSource, now);
        }
        else if (!isWatched)
        {
            RecordStateChange(dbContext, season, item, StateUnwatched, oldAggregateUnwatched, newAggregateUnwatched, changeSource, now);
        }

        if (item is not null)
        {
            RecordStateChange(dbContext, season, item, StateFavorite, oldFavorite, item.IsFavorite, changeSource, now);
            RecordStateChange(dbContext, season, item, StateWantToWatch, oldWantToWatch, item.IsWantToWatch, changeSource, now);
            CleanupCollectionEntityIfEmpty(dbContext, item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEpisodeWatchedAsync(
        int tvEpisodeId,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var targetEpisode = await dbContext.TvEpisodes
            .AsNoTracking()
            .Where(x => x.Id == tvEpisodeId)
            .Select(x => new { x.Id, x.TvSeasonId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("电视剧集不存在。");

        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == targetEpisode.TvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("电视剧季不存在。");
        var episodes = await dbContext.TvEpisodes
            .Include(x => x.MediaFiles)
            .Where(x => x.TvSeasonId == season.Id)
            .ToListAsync(cancellationToken);
        var episode = episodes.FirstOrDefault(x => x.Id == tvEpisodeId)
            ?? throw new InvalidOperationException("电视剧集不存在。");
        var now = DateTime.UtcNow;
        var totalEpisodeCount = ResolveTotalEpisodeCount(season, episodes);
        var oldAggregateWatched = IsAggregateWatched(episodes.Count(x => x.IsWatched), episodes.Count, totalEpisodeCount);
        var oldAggregateUnwatched = IsAggregateUnwatched(episodes);

        ApplyEpisodeWatchedState(episode, isWatched, now);

        var newAggregateWatched = IsAggregateWatched(episodes.Count(x => x.IsWatched), episodes.Count, totalEpisodeCount);
        var newAggregateUnwatched = IsAggregateUnwatched(episodes);
        var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
        var oldFavorite = item?.IsFavorite ?? false;
        var oldWantToWatch = item?.IsWantToWatch ?? false;
        if (item is not null)
        {
            ApplySeasonSnapshot(item, season);
            if (!newAggregateWatched)
            {
                item.IsFavorite = false;
            }

            if (!newAggregateUnwatched)
            {
                item.IsWantToWatch = false;
            }

            RestoreAutoVisibilityForPositiveState(item, isWatched);
            item.UpdatedAt = now;
        }

        season.UpdatedAt = now;
        RecordStateChange(dbContext, season, item, StateWatched, oldAggregateWatched, newAggregateWatched, changeSource, now);
        if (oldAggregateUnwatched != newAggregateUnwatched)
        {
            RecordStateChange(dbContext, season, item, StateUnwatched, oldAggregateUnwatched, newAggregateUnwatched, changeSource, now);
        }

        if (item is not null)
        {
            RecordStateChange(dbContext, season, item, StateFavorite, oldFavorite, item.IsFavorite, changeSource, now);
            RecordStateChange(dbContext, season, item, StateWantToWatch, oldWantToWatch, item.IsWantToWatch, changeSource, now);
            CleanupCollectionEntityIfEmpty(dbContext, item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetEpisodeSourceToUnidentifiedAsync(
        int tvEpisodeId,
        int mediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFile = await dbContext.MediaFiles
            .Include(x => x.Episode)
            .ThenInclude(x => x!.Season)
            .Include(x => x.SourceConnection)
            .FirstOrDefaultAsync(x => x.Id == mediaFileId, cancellationToken);

        if (mediaFile is null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-episode-source-reset-unidentified-rejected episodeId={tvEpisodeId} mediaFileId={mediaFileId} reason=missing-media-file");
            throw new InvalidOperationException("播放源记录不存在。");
        }

        if (mediaFile.EpisodeId != tvEpisodeId || mediaFile.Episode is null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-episode-source-reset-unidentified-rejected episodeId={tvEpisodeId} mediaFileId={mediaFileId} reason=not-current-episode-source actualEpisodeId={mediaFile.EpisodeId?.ToString() ?? "(none)"}");
            throw new InvalidOperationException("该播放源不属于当前剧集。");
        }

        if (mediaFile.MediaType != MediaType.Video)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-episode-source-reset-unidentified-rejected episodeId={tvEpisodeId} mediaFileId={mediaFileId} reason=not-video");
            throw new InvalidOperationException("只能重置视频播放源。");
        }

        if (mediaFile.IsDeleted)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-episode-source-reset-unidentified-rejected episodeId={tvEpisodeId} mediaFileId={mediaFileId} reason=deleted-source");
            throw new InvalidOperationException("该播放源记录已不可用。");
        }

        if (mediaFile.Episode.Season?.IdentificationStatus == IdentificationStatus.Failed)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-episode-source-reset-unidentified-rejected episodeId={tvEpisodeId} mediaFileId={mediaFileId} reason=already-unidentified");
            throw new InvalidOperationException("该剧集已是未识别状态。");
        }

        var now = DateTime.UtcNow;
        var oldEpisodeId = mediaFile.EpisodeId;
        mediaFile.EpisodeId = null;
        mediaFile.MovieId = null;
        mediaFile.UpdatedAt = now;
        mediaFile.Episode.UpdatedAt = now;
        if (mediaFile.Episode.Season is not null)
        {
            mediaFile.Episode.Season.UpdatedAt = now;
        }

        var remainingActiveSourceCount = await dbContext.MediaFiles
            .CountAsync(
                x => x.EpisodeId == tvEpisodeId
                     && x.Id != mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=tv-episode-source-reset-unidentified episodeId={tvEpisodeId} mediaFileId={mediaFileId} oldEpisodeId={oldEpisodeId} protocolType={FormatProtocol(mediaFile.SourceConnection?.ProtocolType)} remainingActiveSourceCount={remainingActiveSourceCount}");

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFromLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == tvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("电视剧季不存在。");
        var now = DateTime.UtcNow;

        var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
        if (item is null)
        {
            item = new UserTvSeasonCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserTvSeasonCollectionItems.Add(item);
        }

        ApplySeasonSnapshot(item, season);
        item.LibraryVisibilityState = LibraryVisibilityState.Hidden;
        item.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSeasonToLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == tvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("鐢佃鍓у涓嶅瓨鍦ㄣ€?");
        var now = DateTime.UtcNow;

        var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
        if (item is null)
        {
            item = new UserTvSeasonCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserTvSeasonCollectionItems.Add(item);
        }

        ApplySeasonSnapshot(item, season);
        item.LibraryVisibilityState = LibraryVisibilityState.Visible;
        item.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSeriesToLibraryAsync(int tvSeriesId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var seasons = await dbContext.TvSeasons
            .Include(x => x.Series)
            .Where(x => x.TvSeriesId == tvSeriesId)
            .OrderBy(x => x.SeasonNumber)
            .ToListAsync(cancellationToken);

        if (seasons.Count == 0)
        {
            var seriesExists = await dbContext.TvSeries.AnyAsync(x => x.Id == tvSeriesId, cancellationToken);
            if (!seriesExists)
            {
                throw new InvalidOperationException("鐢佃鍓т笉瀛樺湪銆?");
            }

            return;
        }

        var now = DateTime.UtcNow;
        foreach (var season in seasons)
        {
            var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
            if (item is null)
            {
                item = new UserTvSeasonCollectionItem
                {
                    CreatedAt = now
                };
                dbContext.UserTvSeasonCollectionItems.Add(item);
            }

            ApplySeasonSnapshot(item, season);
            item.LibraryVisibilityState = LibraryVisibilityState.Visible;
            item.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreSeasonToLibraryAsync(int tvSeasonId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .Include(x => x.Episodes)
            .ThenInclude(x => x.MediaFiles)
            .FirstOrDefaultAsync(x => x.Id == tvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("鐢佃鍓у涓嶅瓨鍦ㄣ€?");

        var now = DateTime.UtcNow;
        var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
        if (item is null)
        {
            item = new UserTvSeasonCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserTvSeasonCollectionItems.Add(item);
        }

        RestoreSeasonToLibrary(dbContext, item, season, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreSeriesToLibraryAsync(int tvSeriesId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var seasons = await dbContext.TvSeasons
            .Include(x => x.Series)
            .Include(x => x.Episodes)
            .ThenInclude(x => x.MediaFiles)
            .Where(x => x.TvSeriesId == tvSeriesId)
            .OrderBy(x => x.SeasonNumber)
            .ToListAsync(cancellationToken);

        if (seasons.Count == 0)
        {
            var seriesExists = await dbContext.TvSeries.AnyAsync(x => x.Id == tvSeriesId, cancellationToken);
            if (!seriesExists)
            {
                throw new InvalidOperationException("閻絻顫嬮崜褌绗夌€涙ê婀妴?");
            }

            return;
        }

        var now = DateTime.UtcNow;
        foreach (var season in seasons)
        {
            var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
            if (item is null)
            {
                item = new UserTvSeasonCollectionItem
                {
                    CreatedAt = now
                };
                dbContext.UserTvSeasonCollectionItems.Add(item);
            }

            RestoreSeasonToLibrary(dbContext, item, season, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSeasonRecordAsync(int tvSeasonId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == tvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("电视剧季不存在。");
        var seriesId = season.TvSeriesId;

        var episodeIds = await dbContext.TvEpisodes
            .Where(x => x.TvSeasonId == tvSeasonId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var mediaFileIds = await dbContext.MediaFiles
            .Where(x => x.EpisodeId.HasValue && episodeIds.Contains(x.EpisodeId.Value))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var mediaFileIdSet = mediaFileIds.ToHashSet();

        var collectionItems = await dbContext.UserTvSeasonCollectionItems
            .Where(x => x.TvSeasonId == tvSeasonId)
            .ToListAsync(cancellationToken);
        var collectionItemIds = collectionItems.Select(x => x.Id).ToHashSet();

        var stateHistories = await dbContext.UserTvSeasonStateChangeHistories
            .Where(
                x => x.TvSeasonId == tvSeasonId
                     || (x.UserTvSeasonCollectionItemId.HasValue
                         && collectionItemIds.Contains(x.UserTvSeasonCollectionItemId.Value)))
            .ToListAsync(cancellationToken);
        dbContext.UserTvSeasonStateChangeHistories.RemoveRange(stateHistories);
        dbContext.UserTvSeasonCollectionItems.RemoveRange(collectionItems);

        if (episodeIds.Count > 0)
        {
            var watchHistories = await dbContext.WatchHistories
                .Where(x => x.EpisodeId.HasValue && episodeIds.Contains(x.EpisodeId.Value))
                .ToListAsync(cancellationToken);
            dbContext.WatchHistories.RemoveRange(watchHistories);
        }

        if (mediaFileIdSet.Count > 0)
        {
            var subtitleBindings = await dbContext.SubtitleBindings
                .Where(x => mediaFileIdSet.Contains(x.MediaFileId) || mediaFileIdSet.Contains(x.SubtitleMediaFileId))
                .ToListAsync(cancellationToken);
            dbContext.SubtitleBindings.RemoveRange(subtitleBindings);

            var mediaFiles = await dbContext.MediaFiles
                .Where(x => mediaFileIdSet.Contains(x.Id))
                .ToListAsync(cancellationToken);
            dbContext.MediaFiles.RemoveRange(mediaFiles);
        }

        var episodes = await dbContext.TvEpisodes
            .Where(x => x.TvSeasonId == tvSeasonId)
            .ToListAsync(cancellationToken);
        dbContext.TvEpisodes.RemoveRange(episodes);
        dbContext.TvSeasons.Remove(season);
        await dbContext.SaveChangesAsync(cancellationToken);

        var seriesHasSeasons = await dbContext.TvSeasons
            .AnyAsync(x => x.TvSeriesId == seriesId, cancellationToken);
        if (!seriesHasSeasons)
        {
            var series = await dbContext.TvSeries.FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken);
            if (series is not null)
            {
                dbContext.TvSeries.Remove(series);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SetStateAsync(
        int tvSeasonId,
        string stateType,
        bool newValue,
        CancellationToken cancellationToken,
        string changeSource)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var season = await dbContext.TvSeasons
            .Include(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == tvSeasonId, cancellationToken)
            ?? throw new InvalidOperationException("电视剧季不存在。");
        var item = await FindCollectionItemAsync(dbContext, season, cancellationToken);
        var now = DateTime.UtcNow;
        var episodes = await dbContext.TvEpisodes
            .AsNoTracking()
            .Where(x => x.TvSeasonId == tvSeasonId)
            .Select(x => new { x.IsWatched })
            .ToListAsync(cancellationToken);
        var totalEpisodeCount = ResolveTotalEpisodeCount(season.TmdbEpisodeCount, episodes.Count);
        var isSeasonWatched = IsAggregateWatched(episodes.Count(x => x.IsWatched), episodes.Count, totalEpisodeCount);
        var isSeasonUnwatched = episodes.All(x => !x.IsWatched);

        if (stateType == StateFavorite && newValue && !isSeasonWatched)
        {
            throw new InvalidOperationException("只有整季已看的电视剧季可以标记喜爱。");
        }

        if (stateType == StateWantToWatch && newValue && !isSeasonUnwatched)
        {
            throw new InvalidOperationException("只有未看的电视剧季可以标记想看。");
        }

        item ??= new UserTvSeasonCollectionItem
        {
            CreatedAt = now,
            IsFavorite = false,
            IsWantToWatch = false,
            IsNotInterested = false
        };
        if (item.Id == 0)
        {
            dbContext.UserTvSeasonCollectionItems.Add(item);
        }

        ApplySeasonSnapshot(item, season);
        var oldFavorite = item.IsFavorite;
        var oldWantToWatch = item.IsWantToWatch;
        var oldNotInterested = item.IsNotInterested;

        switch (stateType)
        {
            case StateFavorite:
                item.IsFavorite = newValue;
                if (newValue)
                {
                    item.IsWantToWatch = false;
                    item.IsNotInterested = false;
                }

                break;
            case StateWantToWatch:
                item.IsWantToWatch = newValue;
                if (newValue)
                {
                    item.IsFavorite = false;
                    item.IsNotInterested = false;
                }

                break;
            case StateNotInterested:
                item.IsNotInterested = newValue;
                if (newValue)
                {
                    item.IsFavorite = false;
                    item.IsWantToWatch = false;
                }

                break;
        }

        RestoreAutoVisibilityForPositiveState(item, newValue);
        item.UpdatedAt = now;
        RecordStateChange(dbContext, season, item, StateFavorite, oldFavorite, item.IsFavorite, changeSource, now);
        RecordStateChange(dbContext, season, item, StateWantToWatch, oldWantToWatch, item.IsWantToWatch, changeSource, now);
        RecordStateChange(dbContext, season, item, StateNotInterested, oldNotInterested, item.IsNotInterested, changeSource, now);
        CleanupCollectionEntityIfEmpty(dbContext, item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<UserTvSeasonCollectionItem?> FindCollectionItemAsync(
        AppDbContext dbContext,
        TvSeason season,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.UserTvSeasonCollectionItems
            .FirstOrDefaultAsync(x => x.TvSeasonId == season.Id, cancellationToken);
        if (item is not null)
        {
            return item;
        }

        if (season.Series?.TmdbSeriesId is > 0)
        {
            item = await dbContext.UserTvSeasonCollectionItems
                .FirstOrDefaultAsync(
                    x => x.TmdbSeriesId == season.Series.TmdbSeriesId
                         && x.SeasonNumber == season.SeasonNumber,
                    cancellationToken);
        }

        return item;
    }

    private static void ApplySeasonSnapshot(UserTvSeasonCollectionItem item, TvSeason season)
    {
        item.TvSeasonId = season.Id;
        item.TvSeriesId = season.TvSeriesId;
        item.TmdbSeriesId = season.Series?.TmdbSeriesId;
        item.TmdbSeasonId = season.TmdbSeasonId;
        item.SeasonNumber = season.SeasonNumber;
        item.SeriesTitle = season.Series?.Name ?? string.Empty;
        item.OriginalSeriesTitle = season.Series?.OriginalName ?? string.Empty;
        item.SeasonTitle = season.Name;
        item.FirstAirYear = season.AirDate?.Year ?? season.Series?.FirstAirYear;
        item.AirDate = season.AirDate;
        item.PosterRemoteUrl = FirstNonEmpty(season.PosterRemoteUrl, season.Series?.PosterRemoteUrl);
        item.Overview = FirstNonEmpty(season.Overview, season.Series?.Overview);
        item.GenresText = season.Series?.GenresText ?? string.Empty;
        item.Country = season.Series?.Country ?? string.Empty;
        item.Language = season.Series?.Language ?? string.Empty;
    }

    private static void RecordStateChange(
        AppDbContext dbContext,
        TvSeason season,
        UserTvSeasonCollectionItem? collectionItem,
        string stateType,
        bool oldValue,
        bool newValue,
        string? source,
        DateTime now)
    {
        if (oldValue == newValue)
        {
            return;
        }

        dbContext.UserTvSeasonStateChangeHistories.Add(
            new UserTvSeasonStateChangeHistory
            {
                TmdbSeriesId = collectionItem?.TmdbSeriesId ?? season.Series?.TmdbSeriesId,
                TmdbSeasonId = collectionItem?.TmdbSeasonId ?? season.TmdbSeasonId,
                TvSeriesId = season.TvSeriesId,
                TvSeasonId = season.Id,
                UserTvSeasonCollectionItemId = collectionItem?.Id > 0 ? collectionItem.Id : null,
                SeasonNumber = season.SeasonNumber,
                SeriesTitle = collectionItem?.SeriesTitle ?? season.Series?.Name,
                SeasonTitle = collectionItem?.SeasonTitle ?? season.Name,
                StateType = stateType,
                OldValue = oldValue,
                NewValue = newValue,
                Source = NormalizeSource(source),
                ChangedAtUtc = now,
                CreatedAtUtc = now
            });
    }

    private static void RecordStateTouch(
        AppDbContext dbContext,
        TvSeason season,
        UserTvSeasonCollectionItem? collectionItem,
        string stateType,
        bool value,
        string? source,
        DateTime now)
    {
        dbContext.UserTvSeasonStateChangeHistories.Add(
            new UserTvSeasonStateChangeHistory
            {
                TmdbSeriesId = collectionItem?.TmdbSeriesId ?? season.Series?.TmdbSeriesId,
                TmdbSeasonId = collectionItem?.TmdbSeasonId ?? season.TmdbSeasonId,
                TvSeriesId = season.TvSeriesId,
                TvSeasonId = season.Id,
                UserTvSeasonCollectionItemId = collectionItem?.Id > 0 ? collectionItem.Id : null,
                SeasonNumber = season.SeasonNumber,
                SeriesTitle = collectionItem?.SeriesTitle ?? season.Series?.Name,
                SeasonTitle = collectionItem?.SeasonTitle ?? season.Name,
                StateType = stateType,
                OldValue = value,
                NewValue = value,
                Source = NormalizeSource(source),
                ChangedAtUtc = now,
                CreatedAtUtc = now
            });
    }

    private static void CleanupCollectionEntityIfEmpty(AppDbContext dbContext, UserTvSeasonCollectionItem entity)
    {
        if (!entity.IsFavorite
            && !entity.IsWantToWatch
            && !entity.IsNotInterested
            && entity.LibraryVisibilityState == LibraryVisibilityState.Auto)
        {
            dbContext.UserTvSeasonCollectionItems.Remove(entity);
        }
    }

    private static void RestoreAutoVisibilityForPositiveState(UserTvSeasonCollectionItem entity, bool isPositiveState)
    {
        if (isPositiveState && entity.LibraryVisibilityState == LibraryVisibilityState.Hidden)
        {
            entity.LibraryVisibilityState = LibraryVisibilityState.Auto;
        }
    }

    private static void RestoreSeasonToLibrary(
        AppDbContext dbContext,
        UserTvSeasonCollectionItem item,
        TvSeason season,
        DateTime now)
    {
        ApplySeasonSnapshot(item, season);
        var hasActiveSource = season.Episodes.Any(
            episode => episode.MediaFiles.Any(file => !file.IsDeleted && file.MediaType == MediaType.Video));
        var hasCurrentState = item.IsFavorite
                              || item.IsWantToWatch
                              || item.IsNotInterested
                              || season.Episodes.Any(episode => episode.IsWatched);

        item.LibraryVisibilityState = ResolveRestoredVisibilityState(hasActiveSource, hasCurrentState);
        item.UpdatedAt = now;
        CleanupCollectionEntityIfEmpty(dbContext, item);
    }

    private static LibraryVisibilityState ResolveRestoredVisibilityState(bool hasActiveSource, bool hasCurrentState)
    {
        return hasActiveSource || hasCurrentState
            ? LibraryVisibilityState.Auto
            : LibraryVisibilityState.Visible;
    }

    private static int ResolveTotalEpisodeCount(TvSeason season, IReadOnlyCollection<TvEpisode> episodes)
    {
        return ResolveTotalEpisodeCount(season.TmdbEpisodeCount, episodes.Count);
    }

    private static int ResolveTotalEpisodeCount(int? tmdbEpisodeCount, int knownEpisodeCount)
    {
        return tmdbEpisodeCount.GetValueOrDefault() > 0
            ? tmdbEpisodeCount!.Value
            : Math.Max(0, knownEpisodeCount);
    }

    private static bool IsAggregateWatched(int watchedEpisodeCount, int knownEpisodeCount, int totalEpisodeCount)
    {
        if (totalEpisodeCount <= 0)
        {
            return knownEpisodeCount > 0 && watchedEpisodeCount >= knownEpisodeCount;
        }

        return knownEpisodeCount >= totalEpisodeCount && watchedEpisodeCount >= totalEpisodeCount;
    }

    private static bool IsAggregateUnwatched(IEnumerable<TvEpisode> episodes)
    {
        return episodes.All(x => !x.IsWatched);
    }

    private static void ApplyEpisodeWatchedState(TvEpisode episode, bool isWatched, DateTime now)
    {
        episode.IsWatched = isWatched;
        episode.UpdatedAt = now;
        if (isWatched)
        {
            episode.LastPlayedAt ??= now;
            var durationSeconds = ResolveEpisodeDurationSeconds(episode);
            if (durationSeconds > 0)
            {
                episode.LastPlayPositionSeconds = durationSeconds;
                episode.DurationWatchedSeconds = Math.Max(episode.DurationWatchedSeconds, durationSeconds);
            }

            return;
        }

        episode.LastPlayedAt = null;
        episode.LastPlayPositionSeconds = 0;
        episode.DurationWatchedSeconds = 0;
    }

    private static int ResolveEpisodeDurationSeconds(TvEpisode episode)
    {
        var mediaDuration = episode.MediaFiles
            .Where(x => !x.IsDeleted && x.MediaType == MediaType.Video)
            .Select(x => x.DurationSeconds)
            .FirstOrDefault(x => x is > 0);
        if (mediaDuration is > 0)
        {
            return mediaDuration.Value;
        }

        return episode.RuntimeMinutes is > 0 ? episode.RuntimeMinutes.Value * 60 : 0;
    }

    private static async Task<IReadOnlyList<SeasonCollectionRow>> LoadSeasonRowsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> seasonIds,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => seasonIds.Contains(x.Id))
            .Select(
                x => new SeasonCollectionRow
                {
                    SeasonId = x.Id,
                    SeriesId = x.TvSeriesId,
                    SeriesName = x.Series!.Name,
                    OriginalSeriesName = x.Series.OriginalName ?? string.Empty,
                    SeasonName = x.Name,
                    SeasonNumber = x.SeasonNumber,
                    SeasonPosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    SeriesPosterRemoteUrl = x.Series.PosterRemoteUrl ?? string.Empty,
                    SeasonOverview = x.Overview ?? string.Empty,
                    GenresText = x.Series.GenresText ?? string.Empty,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : x.Series.FirstAirYear,
                    TotalEpisodeCount = x.TmdbEpisodeCount,
                    EpisodeCount = x.Episodes.Count,
                    WatchedEpisodeCount = x.Episodes.Count(episode => episode.IsWatched),
                    InLibraryEpisodeCount = x.Episodes.Count(
                        episode => episode.MediaFiles.Any(media => !media.IsDeleted && media.MediaType == MediaType.Video)),
                    HasLocalSource = x.Episodes.Any(
                        episode => episode.MediaFiles.Any(
                            media => !media.IsDeleted
                                     && media.MediaType == MediaType.Video
                                     && media.SourceConnection != null
                                     && media.SourceConnection.ProtocolType == ProtocolType.Local)),
                    HasWebDavSource = x.Episodes.Any(
                        episode => episode.MediaFiles.Any(
                            media => !media.IsDeleted
                                     && media.MediaType == MediaType.Video
                                     && media.SourceConnection != null
                                     && media.SourceConnection.ProtocolType == ProtocolType.WebDav))
                })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.TotalEpisodeCount = row.TotalEpisodeCount.GetValueOrDefault() > 0
                ? row.TotalEpisodeCount
                : row.EpisodeCount;
        }

        return rows;
    }

    private static string BuildSeasonTitle(string seriesTitle, string seasonTitle, int seasonNumber)
    {
        var normalizedSeries = string.IsNullOrWhiteSpace(seriesTitle) ? "未命名电视剧" : seriesTitle.Trim();
        var normalizedSeason = string.IsNullOrWhiteSpace(seasonTitle) ? $"S{seasonNumber:D2}" : seasonTitle.Trim();
        return normalizedSeason.Contains(normalizedSeries, StringComparison.OrdinalIgnoreCase)
            ? normalizedSeason
            : $"{normalizedSeries} {normalizedSeason}";
    }

    private static string FormatSourceSummary(bool hasLocal, bool hasWebDav)
    {
        return (hasLocal, hasWebDav) switch
        {
            (true, true) => "本地 + 网盘",
            (true, false) => "本地",
            (false, true) => "网盘",
            _ => "暂无播放源"
        };
    }

    private static string FormatProtocol(ProtocolType? protocolType)
    {
        return protocolType == ProtocolType.Local ? "local" : "webdav";
    }

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return UserMovieStateChangeHistoryRecorder.SourceUnknown;
        }

        var trimmed = source.Trim();
        return trimmed.Length <= 40 ? trimmed : trimmed[..40];
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private const string StateWatched = "Watched";
    private const string StateUnwatched = "Unwatched";
    private const string StateFavorite = "Favorite";
    private const string StateWantToWatch = "WantToWatch";
    private const string StateNotInterested = "NotInterested";

    private sealed class SeasonCollectionRow
    {
        public int SeasonId { get; set; }

        public int SeriesId { get; set; }

        public string SeriesName { get; set; } = string.Empty;

        public string OriginalSeriesName { get; set; } = string.Empty;

        public string SeasonName { get; set; } = string.Empty;

        public int SeasonNumber { get; set; }

        public string SeasonPosterRemoteUrl { get; set; } = string.Empty;

        public string SeriesPosterRemoteUrl { get; set; } = string.Empty;

        public string SeasonOverview { get; set; } = string.Empty;

        public string GenresText { get; set; } = string.Empty;

        public int? AirYear { get; set; }

        public int? TotalEpisodeCount { get; set; }

        public int EpisodeCount { get; set; }

        public int WatchedEpisodeCount { get; set; }

        public int InLibraryEpisodeCount { get; set; }

        public bool HasLocalSource { get; set; }

        public bool HasWebDavSource { get; set; }
    }
}
