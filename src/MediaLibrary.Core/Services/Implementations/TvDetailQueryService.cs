using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TvDetailQueryService : ITvDetailQueryService
{
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

        var seasonItems = seasons
            .Select(
                season =>
                {
                    var seasonEpisodes = episodeRows.Where(x => x.TvSeasonId == season.Id).ToList();
                    var totalEpisodeCount = season.TmdbEpisodeCount.GetValueOrDefault() > 0
                        ? season.TmdbEpisodeCount!.Value
                        : seasonEpisodes.Count;
                    var protocols = sourceProtocolsBySeason.GetValueOrDefault(season.Id) ?? [];
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
                        InLibraryEpisodeCount = inLibraryEpisodeCountsBySeason.GetValueOrDefault(season.Id),
                        SourceSummary = TvDetailDisplayText.FormatSourceSummary(protocols),
                        IdentificationStatus = season.IdentificationStatus
                    };
                })
            .ToList();

        var posterFallback = seasonItems
            .OrderByDescending(x => x.SeasonNumber)
            .Select(x => x.PosterRemoteUrl)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;
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
            PosterDisplayUrl = string.IsNullOrWhiteSpace(series.PosterRemoteUrl) ? posterFallback : series.PosterRemoteUrl,
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
                x.IsNotInterested
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

        return new TvSeasonDetailModel
        {
            SeasonId = season.Id,
            SeriesId = season.TvSeriesId,
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

    private sealed class SourceRow
    {
        public int EpisodeId { get; set; }

        public ProtocolType ProtocolType { get; set; }
    }
}
