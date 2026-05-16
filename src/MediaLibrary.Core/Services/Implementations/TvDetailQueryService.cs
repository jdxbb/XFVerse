using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TvDetailQueryService : ITvDetailQueryService
{
    private readonly ITmdbService _tmdbService;
    private readonly IOmdbService _omdbService;

    public TvDetailQueryService(ITmdbService tmdbService, IOmdbService omdbService)
    {
        _tmdbService = tmdbService;
        _omdbService = omdbService;
    }

    public async Task<TvSeriesOverviewModel?> GetSeriesOverviewAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var series = await dbContext.TvSeries
            .AsNoTracking()
            .Where(x => x.Id == seriesId)
            .Select(
                x => new
                {
                    x.Id,
                    x.TmdbSeriesId,
                    x.Name,
                    x.OriginalName,
                    x.Overview,
                    x.PosterRemoteUrl,
                    x.PosterLocalPath,
                    x.FirstAirDate,
                    x.FirstAirYear,
                    x.GenresText
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (series is null)
        {
            return null;
        }

        var seasons = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => x.TvSeriesId == seriesId)
            .OrderBy(x => x.SeasonNumber)
            .Select(
                x => new
                {
                    x.Id,
                    x.SeasonNumber,
                    x.Name,
                    x.PosterRemoteUrl,
                    x.PosterLocalPath,
                    x.AirDate,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : (int?)null,
                    x.TmdbEpisodeCount,
                    x.IdentificationStatus
                })
            .ToListAsync(cancellationToken);

        var seasonIds = seasons.Select(x => x.Id).ToArray();
        var episodeRows = seasonIds.Length == 0
            ? []
            : await dbContext.TvEpisodes
                .AsNoTracking()
                .Where(x => seasonIds.Contains(x.TvSeasonId))
                .Select(
                    x => new
                    {
                        x.Id,
                        x.TvSeasonId,
                        x.IsWatched
                    })
                .ToListAsync(cancellationToken);

        var episodeIds = episodeRows.Select(x => x.Id).ToArray();
        var sourceRows = await LoadSourceRowsAsync(dbContext, episodeIds, cancellationToken);
        var sourceProtocolsBySeason = sourceRows
            .Join(
                episodeRows,
                source => source.EpisodeId,
                episode => episode.Id,
                (source, episode) => new { episode.TvSeasonId, source.ProtocolType })
            .GroupBy(x => x.TvSeasonId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<ProtocolType>)x.Select(y => y.ProtocolType).Distinct().ToArray());
        var inLibraryEpisodeIds = sourceRows.Select(x => x.EpisodeId).Distinct().ToHashSet();
        var inLibraryEpisodeCountsBySeason = episodeRows
            .Where(x => inLibraryEpisodeIds.Contains(x.Id))
            .GroupBy(x => x.TvSeasonId)
            .ToDictionary(x => x.Key, x => x.Count());
        var collectionRows = seasonIds.Length == 0
            ? []
            : await dbContext.UserTvSeasonCollectionItems
                .AsNoTracking()
                .Where(x => x.TvSeasonId.HasValue && seasonIds.Contains(x.TvSeasonId.Value))
                .Select(
                    x => new
                    {
                        TvSeasonId = x.TvSeasonId!.Value,
                        x.IsFavorite,
                        x.IsWantToWatch,
                        x.IsNotInterested,
                        x.LibraryVisibilityState,
                        x.UpdatedAt
                    })
                .ToListAsync(cancellationToken);
        var collectionBySeason = collectionRows
            .GroupBy(x => x.TvSeasonId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.LibraryVisibilityState != LibraryVisibilityState.Auto)
                    .ThenByDescending(y => y.UpdatedAt)
                    .First());

        var seasonItems = seasons
            .Select(
                season =>
                {
                    var seasonEpisodes = episodeRows.Where(x => x.TvSeasonId == season.Id).ToList();
                    var totalEpisodeCount = season.TmdbEpisodeCount.GetValueOrDefault() > 0
                        ? season.TmdbEpisodeCount!.Value
                        : seasonEpisodes.Count;
                    var protocols = sourceProtocolsBySeason.GetValueOrDefault(season.Id) ?? [];
                    collectionBySeason.TryGetValue(season.Id, out var collection);
                    var activeSourceCount = inLibraryEpisodeCountsBySeason.GetValueOrDefault(season.Id);
                    var visibilityState = collection?.LibraryVisibilityState ?? LibraryVisibilityState.Auto;
                    var hasCurrentState = collection?.IsFavorite == true
                                          || collection?.IsWantToWatch == true
                                          || collection?.IsNotInterested == true
                                          || seasonEpisodes.Any(x => x.IsWatched);
                    return new TvSeriesSeasonListItem
                    {
                        SeasonId = season.Id,
                        SeasonNumber = season.SeasonNumber,
                        Name = season.Name,
                        PosterRemoteUrl = season.PosterRemoteUrl ?? string.Empty,
                        PosterLocalPath = season.PosterLocalPath ?? string.Empty,
                        AirDate = season.AirDate,
                        AirYear = season.AirYear,
                        WatchedEpisodeCount = seasonEpisodes.Count(x => x.IsWatched),
                        TotalEpisodeCount = totalEpisodeCount,
                        InLibraryEpisodeCount = activeSourceCount,
                        LibraryVisibilityState = visibilityState,
                        IsVisibleInLibrary = ResolveIsVisibleInLibrary(activeSourceCount > 0, visibilityState, hasCurrentState),
                        SourceSummary = TvDetailDisplayText.FormatSourceSummary(protocols),
                        IdentificationStatus = season.IdentificationStatus
                    };
                })
            .ToList();

        var posterFallback = seasonItems
            .OrderByDescending(x => x.SeasonNumber)
            .Select(x => x.PosterDisplayUrl)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;
        var seriesPoster = string.IsNullOrWhiteSpace(series.PosterRemoteUrl)
            ? series.PosterLocalPath ?? string.Empty
            : series.PosterRemoteUrl;
        var sourceSummary = TvDetailDisplayText.FormatSourceSummary(
            sourceRows.Select(x => x.ProtocolType).Distinct().ToArray());

        return new TvSeriesOverviewModel
        {
            SeriesId = series.Id,
            TmdbSeriesId = series.TmdbSeriesId,
            Name = series.Name,
            OriginalName = series.OriginalName ?? string.Empty,
            Overview = series.Overview ?? string.Empty,
            PosterRemoteUrl = series.PosterRemoteUrl ?? string.Empty,
            PosterLocalPath = series.PosterLocalPath ?? string.Empty,
            PosterDisplayUrl = string.IsNullOrWhiteSpace(seriesPoster) ? posterFallback : seriesPoster,
            FirstAirDate = series.FirstAirDate,
            FirstAirYear = series.FirstAirYear,
            GenresText = series.GenresText ?? string.Empty,
            SourceSummary = sourceSummary,
            TotalSeasonCount = seasons.Count,
            InLibrarySeasonCount = seasonItems.Count(x => x.InLibraryEpisodeCount > 0),
            Seasons = seasonItems
        };
    }

    public async Task<TvSeasonDetailModel?> GetSeasonDetailAsync(
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var season = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => x.Id == seasonId)
            .Select(
                x => new
                {
                    x.Id,
                    x.TvSeriesId,
                    x.SeasonNumber,
                    x.Name,
                    x.Overview,
                    x.PosterRemoteUrl,
                    x.PosterLocalPath,
                    x.AirDate,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : (int?)null,
                    x.TmdbEpisodeCount,
                    x.IdentificationStatus,
                    SeriesName = x.Series!.Name,
                    SeriesOriginalName = x.Series.OriginalName,
                    SeriesTmdbId = x.Series.TmdbSeriesId,
                    SeriesGenresText = x.Series.GenresText,
                    SeriesPosterRemoteUrl = x.Series.PosterRemoteUrl
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (season is null)
        {
            return null;
        }

        var collectionState = await dbContext.UserTvSeasonCollectionItems
            .AsNoTracking()
            .Where(x => x.TvSeasonId == season.Id
                || (season.SeriesTmdbId.HasValue
                    && x.TmdbSeriesId == season.SeriesTmdbId.Value
                    && x.SeasonNumber == season.SeasonNumber))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new
            {
                x.IsFavorite,
                x.IsWantToWatch,
                x.IsNotInterested,
                x.LibraryVisibilityState
            })
            .FirstOrDefaultAsync(cancellationToken);

        var episodes = await dbContext.TvEpisodes
            .AsNoTracking()
            .Where(x => x.TvSeasonId == seasonId)
            .OrderBy(x => x.EpisodeNumber)
            .Select(
                x => new
                {
                    x.Id,
                    x.EpisodeNumber,
                    x.Title,
                    x.Overview,
                    x.AirDate,
                    x.RuntimeMinutes,
                    x.IsWatched,
                    x.LastPlayedAt,
                    x.LastPlayPositionSeconds,
                    x.DurationWatchedSeconds,
                    x.StillRemoteUrl
                })
            .ToListAsync(cancellationToken);

        var episodeIds = episodes.Select(x => x.Id).ToArray();
        var sourceRows = await LoadSourceRowsAsync(dbContext, episodeIds, cancellationToken);
        var sourceRowsByEpisode = sourceRows
            .GroupBy(x => x.EpisodeId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var episodeItems = episodes
            .Select(
                episode =>
                {
                    var sources = sourceRowsByEpisode.GetValueOrDefault(episode.Id) ?? [];
                    var protocols = sources.Select(x => x.ProtocolType).Distinct().ToArray();
                    return new TvSeasonEpisodeListItem
                    {
                        EpisodeId = episode.Id,
                        EpisodeNumber = episode.EpisodeNumber,
                        Name = episode.Title,
                        Overview = episode.Overview ?? string.Empty,
                        AirDate = episode.AirDate,
                        RuntimeMinutes = episode.RuntimeMinutes,
                        IsWatched = episode.IsWatched,
                        LastPlayedAt = episode.LastPlayedAt,
                        LastPlayPositionSeconds = episode.LastPlayPositionSeconds,
                        DurationWatchedSeconds = episode.DurationWatchedSeconds,
                        StillRemoteUrl = episode.StillRemoteUrl ?? string.Empty,
                        SourceSummary = TvDetailDisplayText.FormatSourceSummary(protocols),
                        HasPlayableSource = sources.Count > 0,
                        ActiveSourceCount = sources.Count
                    };
                })
            .ToList();

        var totalEpisodeCount = season.TmdbEpisodeCount.GetValueOrDefault() > 0
            ? season.TmdbEpisodeCount!.Value
            : episodeItems.Count;
        var watchedEpisodeCount = episodeItems.Count(x => x.IsWatched);
        var isSeasonWatched = totalEpisodeCount > 0
            ? episodeItems.Count >= totalEpisodeCount && watchedEpisodeCount >= totalEpisodeCount
            : episodeItems.Count > 0 && watchedEpisodeCount >= episodeItems.Count;
        var isSeasonUnwatched = watchedEpisodeCount == 0;
        var sourceSummary = TvDetailDisplayText.FormatSourceSummary(
            sourceRows.Select(x => x.ProtocolType).Distinct().ToArray());
        var posterDisplayUrl = FirstNonEmpty(season.PosterRemoteUrl, season.SeriesPosterRemoteUrl);
        var visibilityState = collectionState?.LibraryVisibilityState ?? LibraryVisibilityState.Auto;
        var isVisibleInLibrary = ResolveIsVisibleInLibrary(
            episodeItems.Any(x => x.HasPlayableSource),
            visibilityState,
            collectionState?.IsFavorite == true
            || collectionState?.IsWantToWatch == true
            || collectionState?.IsNotInterested == true
            || watchedEpisodeCount > 0);

        return new TvSeasonDetailModel
        {
            SeasonId = season.Id,
            SeriesId = season.TvSeriesId,
            TmdbSeriesId = season.SeriesTmdbId,
            SeasonNumber = season.SeasonNumber,
            SeriesName = season.SeriesName,
            SeriesOriginalName = season.SeriesOriginalName ?? string.Empty,
            Name = season.Name,
            Overview = season.Overview ?? string.Empty,
            PosterRemoteUrl = season.PosterRemoteUrl ?? string.Empty,
            PosterLocalPath = season.PosterLocalPath ?? string.Empty,
            PosterDisplayUrl = posterDisplayUrl,
            AirDate = season.AirDate,
            AirYear = season.AirYear,
            GenreDisplay = season.SeriesGenresText ?? string.Empty,
            SourceSummary = sourceSummary,
            IsFavorite = collectionState?.IsFavorite == true && isSeasonWatched,
            IsWantToWatch = collectionState?.IsWantToWatch == true && isSeasonUnwatched,
            IsNotInterested = collectionState?.IsNotInterested == true,
            IsVisibleInLibrary = isVisibleInLibrary,
            LibraryVisibilityState = visibilityState,
            WatchedEpisodeCount = watchedEpisodeCount,
            TotalEpisodeCount = totalEpisodeCount,
            InLibraryEpisodeCount = episodeItems.Count(x => x.HasPlayableSource),
            IdentificationStatus = season.IdentificationStatus,
            UnidentifiedSummary = season.IdentificationStatus == IdentificationStatus.Failed
                ? "未识别电视剧季。可先查看已解析集数和播放源，修正入口将在后续阶段接入。"
                : string.Empty,
            Episodes = episodeItems
        };
    }

    public async Task<string> GetSeasonTmdbRatingDisplayAsync(
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await LoadSeasonRatingKeyAsync(seasonId, cancellationToken);

        if (season is null)
        {
            return "暂无季评分";
        }

        try
        {
            return await BuildSeasonTmdbRatingDisplayAsync(season.SeriesTmdbId, season.SeasonNumber, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return "暂无季评分";
        }
    }

    public async Task<string> GetSeasonImdbSeriesRatingDisplayAsync(
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        var season = await LoadSeasonRatingKeyAsync(seasonId, cancellationToken);

        if (season is null)
        {
            return string.Empty;
        }

        try
        {
            return await BuildSeriesImdbRatingDisplayAsync(season.SeriesTmdbId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
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

    private static async Task<IReadOnlyList<SourceRow>> LoadSourceRowsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> episodeIds,
        CancellationToken cancellationToken)
    {
        if (episodeIds.Count == 0)
        {
            return [];
        }

        return await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => x.EpisodeId.HasValue
                     && episodeIds.Contains(x.EpisodeId.Value)
                     && !x.IsDeleted
                     && x.MediaType == MediaType.Video)
            .Select(
                x => new SourceRow
                {
                    EpisodeId = x.EpisodeId!.Value,
                    ProtocolType = x.SourceConnection!.ProtocolType
                })
            .ToListAsync(cancellationToken);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static async Task<SeasonRatingKey?> LoadSeasonRatingKeyAsync(
        int seasonId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        return await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => x.Id == seasonId)
            .Select(x => new SeasonRatingKey(x.Series!.TmdbSeriesId, x.SeasonNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> BuildSeasonTmdbRatingDisplayAsync(
        int? seriesTmdbId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        if (seriesTmdbId is not > 0 || seasonNumber < 0)
        {
            return "暂无季评分";
        }

        var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
            seriesTmdbId.Value,
            seasonNumber,
            cancellationToken: cancellationToken);
        if (seasonDetails?.TmdbRating is > 0)
        {
            return FormatRating("TMDB 季评分", seasonDetails.TmdbRating.Value, 10d, seasonDetails.TmdbVoteCount);
        }

        return "暂无季评分";
    }

    private async Task<string> BuildSeriesImdbRatingDisplayAsync(
        int? seriesTmdbId,
        CancellationToken cancellationToken)
    {
        if (seriesTmdbId is not > 0)
        {
            return string.Empty;
        }

        var externalIds = await _tmdbService.GetTvSeriesExternalIdsAsync(seriesTmdbId.Value, cancellationToken);
        if (!string.IsNullOrWhiteSpace(externalIds?.ImdbId))
        {
            var seriesRating = await _omdbService.GetSeriesRatingAsync(externalIds.ImdbId, cancellationToken);
            if (seriesRating is not null && seriesRating.ScoreValue > 0)
            {
                return FormatRating("IMDb 剧集评分", seriesRating.ScoreValue, seriesRating.ScoreScale, seriesRating.VoteCount);
            }
        }

        return string.Empty;
    }

    private static string FormatRating(string label, double scoreValue, double scoreScale, int? voteCount)
    {
        var safeScale = scoreScale > 0 ? scoreScale : 10d;
        var text = $"{label} {scoreValue:0.0} / {safeScale:0.#}";
        return voteCount is > 0
            ? $"{text}（{voteCount.Value.ToString("N0", CultureInfo.CurrentCulture)} 票）"
            : text;
    }

    private sealed class SourceRow
    {
        public int EpisodeId { get; set; }

        public ProtocolType ProtocolType { get; set; }
    }

    private sealed record SeasonRatingKey(int? SeriesTmdbId, int SeasonNumber);
}
