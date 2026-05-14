using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class LibraryQueryService : ILibraryQueryService
{
    public async Task<IReadOnlyList<LibraryMovieListItem>> GetLibraryItemsAsync(
        bool expandSeriesToSeasons,
        CancellationToken cancellationToken = default)
    {
        var movies = await GetLibraryMoviesAsync(cancellationToken);
        var tvItems = expandSeriesToSeasons
            ? await GetTvSeasonLibraryItemsAsync(cancellationToken)
            : await GetTvSeriesLibraryItemsAsync(cancellationToken);

        return movies
            .Concat(tvItems)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Title)
            .ToList();
    }

    public async Task<IReadOnlyList<LibraryMovieListItem>> GetLibraryMoviesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var collectionStates = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested)
            .Select(
                x => new LibraryCollectionState(
                    x.MovieId,
                    x.TmdbId,
                    x.ImdbId,
                    x.Title,
                    x.ReleaseYear,
                    x.IsWatched,
                    x.IsWantToWatch,
                    x.IsNotInterested))
            .ToListAsync(cancellationToken);
        var collectionMovieIds = collectionStates
            .Where(x => x.MovieId is > 0)
            .Select(x => x.MovieId!.Value)
            .Distinct()
            .ToArray();
        var watchedIndex = LibraryCollectionIdentityIndex.Create(collectionStates.Where(x => x.IsWatched));
        var wantToWatchIndex = LibraryCollectionIdentityIndex.Create(collectionStates.Where(x => x.IsWantToWatch));
        var notInterestedIndex = LibraryCollectionIdentityIndex.Create(collectionStates.Where(x => x.IsNotInterested));

        var movies = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.MediaFiles.Any(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        || x.IsWatched
                        || x.IsFavorite
                        || x.UserRating.HasValue
                        || x.WatchHistories.Any()
                        || collectionMovieIds.Contains(x.Id))
            .Select(
                x => new
                {
                    x.Id,
                    x.TmdbId,
                    x.Title,
                    x.OriginalTitle,
                    x.ReleaseYear,
                    x.PosterRemoteUrl,
                    x.GenresText,
                    x.AiTagsText,
                    x.EmotionTagsText,
                    x.SceneTagsText,
                    x.Overview,
                    x.Country,
                    x.Language,
                    x.RuntimeMinutes,
                    x.ImdbId,
                    x.IdentificationStatus,
                    x.IdentifiedConfidence,
                    x.IsFavorite,
                    x.IsWatched,
                    x.UpdatedAt,
                    HasWatchHistory = x.WatchHistories.Any(),
                    SourceCount = x.MediaFiles.Count(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video),
                    HasLocalSource = x.MediaFiles.Any(
                        mediaFile => !mediaFile.IsDeleted
                                     && mediaFile.MediaType == MediaType.Video
                                     && mediaFile.SourceConnection != null
                                     && mediaFile.SourceConnection.ProtocolType == ProtocolType.Local),
                    HasWebDavSource = x.MediaFiles.Any(
                        mediaFile => !mediaFile.IsDeleted
                                     && mediaFile.MediaType == MediaType.Video
                                     && mediaFile.SourceConnection != null
                                     && mediaFile.SourceConnection.ProtocolType == ProtocolType.WebDav),
                    Ratings = x.RatingSources
                        .Select(
                            rating => new MovieRatingItem
                            {
                                SourceName = rating.SourceName,
                                ScoreValue = rating.ScoreValue,
                                ScoreScale = rating.ScoreScale,
                                VoteCount = rating.VoteCount,
                                SourceUrl = rating.SourceUrl ?? string.Empty,
                                LastUpdatedAt = rating.LastUpdatedAt
                            })
                        .ToList()
                })
            .ToListAsync(cancellationToken);

        return movies
            .Select(
                x =>
                {
                    var primaryRating = x.Ratings
                        .OrderByDescending(rating => string.Equals(rating.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase))
                        .ThenByDescending(rating => rating.LastUpdatedAt)
                        .FirstOrDefault();
                    var tmdbRating = x.Ratings.FirstOrDefault(rating => string.Equals(rating.SourceName, "TMDB", StringComparison.OrdinalIgnoreCase));
                    var omdbRating = x.Ratings.FirstOrDefault(rating => string.Equals(rating.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase));

                    return new LibraryMovieListItem
                    {
                        MovieId = x.Id,
                        TmdbId = x.TmdbId,
                        Title = x.Title,
                        OriginalTitle = x.OriginalTitle ?? string.Empty,
                        ReleaseYear = x.ReleaseYear,
                        PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                        GenresText = x.GenresText ?? string.Empty,
                        AiTagsText = x.AiTagsText ?? string.Empty,
                        EmotionTagsText = x.EmotionTagsText ?? string.Empty,
                        SceneTagsText = x.SceneTagsText ?? string.Empty,
                        Overview = x.Overview ?? string.Empty,
                        Country = x.Country ?? string.Empty,
                        Language = x.Language ?? string.Empty,
                        RuntimeMinutes = x.RuntimeMinutes,
                        ImdbId = x.ImdbId ?? string.Empty,
                        IdentificationStatus = x.IdentificationStatus,
                        IdentifiedConfidence = x.IdentifiedConfidence,
                        PrimaryRatingSourceName = GetDisplayRatingSourceName(primaryRating?.SourceName ?? string.Empty),
                        PrimaryRatingValue = primaryRating?.ScoreValue,
                        PrimaryRatingScale = primaryRating?.ScoreScale,
                        PrimaryRatingVoteCount = primaryRating?.VoteCount,
                        TmdbRating = tmdbRating?.ScoreValue,
                        TmdbVoteCount = tmdbRating?.VoteCount,
                        OmdbScoreValue = omdbRating?.ScoreValue,
                        OmdbScoreScale = omdbRating?.ScoreScale,
                        OmdbVoteCount = omdbRating?.VoteCount,
                        OmdbSourceUrl = omdbRating?.SourceUrl ?? string.Empty,
                        OmdbLastUpdatedAt = omdbRating?.LastUpdatedAt,
                        SourceCount = x.SourceCount,
                        HasLocalSource = x.HasLocalSource,
                        HasWebDavSource = x.HasWebDavSource,
                        IsInLibrary = x.SourceCount > 0,
                        IsFavorite = x.IsFavorite,
                        IsWatched = x.IsWatched || watchedIndex.Contains(x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear),
                        IsWantToWatch = wantToWatchIndex.Contains(x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear),
                        IsNotInterested = notInterestedIndex.Contains(x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear),
                        HasWatchHistory = x.HasWatchHistory,
                        UpdatedAt = x.UpdatedAt
                    };
                })
            .Concat(await GetExternalCollectionMoviesAsync(dbContext, cancellationToken))
            .GroupBy(BuildLibraryItemKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.IsInLibrary).ThenByDescending(x => x.UpdatedAt).First())
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetExternalCollectionMoviesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => !x.IsInLibrary && (x.IsWatched || x.IsWantToWatch || x.IsNotInterested))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(
                x => new LibraryMovieListItem
                {
                    MovieId = 0,
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle,
                    ReleaseYear = x.ReleaseYear,
                    PosterRemoteUrl = x.PosterRemoteUrl,
                    GenresText = x.GenresText,
                    Overview = x.Overview,
                    Country = x.Country,
                    Language = x.Language,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId,
                    IdentificationStatus = IdentificationStatus.Pending,
                    PrimaryRatingSourceName = x.OmdbScoreValue.HasValue ? "IMDb" : x.TmdbRating.HasValue ? "TMDB" : string.Empty,
                    PrimaryRatingValue = x.OmdbScoreValue ?? x.TmdbRating,
                    PrimaryRatingScale = x.OmdbScoreScale ?? (x.TmdbRating.HasValue ? 10d : null),
                    PrimaryRatingVoteCount = x.OmdbVoteCount ?? x.TmdbVoteCount,
                    TmdbRating = x.TmdbRating,
                    TmdbVoteCount = x.TmdbVoteCount,
                    OmdbScoreValue = x.OmdbScoreValue,
                    OmdbScoreScale = x.OmdbScoreScale,
                    OmdbVoteCount = x.OmdbVoteCount,
                    OmdbSourceUrl = x.OmdbSourceUrl,
                    OmdbLastUpdatedAt = x.OmdbLastUpdatedAt,
                    SourceCount = 0,
                    IsInLibrary = false,
                    IsWatched = x.IsWatched,
                    IsWantToWatch = x.IsWantToWatch,
                    IsNotInterested = x.IsNotInterested,
                    HasWatchHistory = false,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
    }

    private static string BuildLibraryItemKey(LibraryMovieListItem item)
    {
        if (item.IsInLibrary && item.MovieId > 0)
        {
            if (item.TmdbId.HasValue)
            {
                return $"tmdb:{item.TmdbId.Value}";
            }

            if (!string.IsNullOrWhiteSpace(item.ImdbId))
            {
                return $"imdb:{NormalizeImdbId(item.ImdbId)}";
            }

            return $"movie:{item.MovieId}";
        }

        if (item.TmdbId.HasValue)
        {
            return $"tmdb:{item.TmdbId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(item.ImdbId))
        {
            return $"imdb:{NormalizeImdbId(item.ImdbId)}";
        }

        if (!string.IsNullOrWhiteSpace(item.Title) && item.ReleaseYear.HasValue)
        {
            return BuildTitleYearKey(item.Title, item.ReleaseYear);
        }

        return $"external:{NormalizeTitle(item.Title)}:{item.ReleaseYear?.ToString() ?? string.Empty}";
    }

    private static string BuildTitleYearKey(string title, int? releaseYear)
    {
        return $"title:{NormalizeTitle(title)}:{releaseYear?.ToString() ?? string.Empty}";
    }

    private static string NormalizeTitle(string title)
    {
        return title.Trim().ToLowerInvariant();
    }

    private static string NormalizeImdbId(string imdbId)
    {
        return imdbId.Trim().ToLowerInvariant();
    }

    private static string GetDisplayRatingSourceName(string sourceName)
    {
        return string.Equals(sourceName, "OMDb", StringComparison.OrdinalIgnoreCase)
            ? "IMDb"
            : sourceName;
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetTvSeriesLibraryItemsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var seriesRows = await dbContext.TvSeries
            .AsNoTracking()
            .Select(
                x => new
                {
                    x.Id,
                    x.TmdbSeriesId,
                    x.Name,
                    x.OriginalName,
                    x.Overview,
                    x.PosterRemoteUrl,
                    x.FirstAirYear,
                    x.GenresText,
                    x.Country,
                    x.Language,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        var seriesIds = seriesRows.Select(x => x.Id).ToArray();
        if (seriesIds.Length == 0)
        {
            return [];
        }

        var seasonRows = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => seriesIds.Contains(x.TvSeriesId))
            .Select(
                x => new TvSeasonLibraryRow
                {
                    SeasonId = x.Id,
                    SeriesId = x.TvSeriesId,
                    SeasonNumber = x.SeasonNumber,
                    Name = x.Name,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : (int?)null,
                    TotalEpisodeCount = x.TmdbEpisodeCount,
                    IdentificationStatus = x.IdentificationStatus,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        var seasonIds = seasonRows.Select(x => x.SeasonId).ToArray();
        var episodeRows = seasonIds.Length == 0
            ? []
            : await dbContext.TvEpisodes
                .AsNoTracking()
                .Where(x => seasonIds.Contains(x.TvSeasonId))
                .Select(
                    x => new TvEpisodeLibraryRow
                    {
                        EpisodeId = x.Id,
                        SeasonId = x.TvSeasonId,
                        IsWatched = x.IsWatched
                    })
                .ToListAsync(cancellationToken);
        var sourceRows = await LoadTvSourceRowsAsync(dbContext, episodeRows.Select(x => x.EpisodeId).ToArray(), cancellationToken);
        var stateRows = await LoadTvCollectionStateRowsAsync(dbContext, seasonIds, cancellationToken);

        var seasonsBySeries = seasonRows
            .GroupBy(x => x.SeriesId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var episodesBySeason = episodeRows
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var sourcesBySeason = sourceRows
            .Join(
                episodeRows,
                source => source.EpisodeId,
                episode => episode.EpisodeId,
                (source, episode) => new { episode.SeasonId, episode.EpisodeId, source.ProtocolType })
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var stateSeasonIds = stateRows.Select(x => x.SeasonId).ToHashSet();

        return seriesRows
            .Select(
                series =>
                {
                    var seasons = seasonsBySeries.GetValueOrDefault(series.Id) ?? [];
                    var seasonIdsForSeries = seasons.Select(x => x.SeasonId).ToHashSet();
                    var sources = seasons
                        .SelectMany(season => sourcesBySeason.GetValueOrDefault(season.SeasonId) ?? [])
                        .ToList();
                    var hasState = stateSeasonIds.Overlaps(seasonIdsForSeries);
                    var inLibraryEpisodeCount = sources.Select(x => x.EpisodeId).Distinct().Count();
                    var watchedEpisodeCount = seasons
                        .SelectMany(season => episodesBySeason.GetValueOrDefault(season.SeasonId) ?? [])
                        .Count(x => x.IsWatched);
                    var totalEpisodeCount = seasons.Sum(
                        season => season.TotalEpisodeCount.GetValueOrDefault() > 0
                            ? season.TotalEpisodeCount!.Value
                            : episodesBySeason.GetValueOrDefault(season.SeasonId)?.Count ?? 0);
                    var knownEpisodeCount = seasons.Sum(season => episodesBySeason.GetValueOrDefault(season.SeasonId)?.Count ?? 0);
                    var latestSeasonPoster = seasons
                        .OrderByDescending(x => x.SeasonNumber)
                        .Select(x => x.PosterRemoteUrl)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                        ?? string.Empty;
                    return new LibraryMovieListItem
                    {
                        ItemKind = LibraryMediaItemKind.Series,
                        SeriesId = series.Id,
                        TmdbId = series.TmdbSeriesId,
                        Title = series.Name,
                        OriginalTitle = series.OriginalName ?? string.Empty,
                        ReleaseYear = series.FirstAirYear,
                        PosterRemoteUrl = FirstNonEmpty(series.PosterRemoteUrl, latestSeasonPoster),
                        GenresText = series.GenresText ?? string.Empty,
                        Overview = series.Overview ?? string.Empty,
                        Country = series.Country ?? string.Empty,
                        Language = series.Language ?? string.Empty,
                        IdentificationStatus = seasons.Any(x => x.IdentificationStatus == IdentificationStatus.Failed)
                            ? IdentificationStatus.Failed
                            : IdentificationStatus.Matched,
                        SourceCount = sources.Count,
                        HasLocalSource = sources.Any(x => x.ProtocolType == ProtocolType.Local),
                        HasWebDavSource = sources.Any(x => x.ProtocolType == ProtocolType.WebDav),
                        IsInLibrary = sources.Count > 0,
                        IsWatched = IsAggregateWatched(watchedEpisodeCount, knownEpisodeCount, totalEpisodeCount),
                        SeasonCount = seasons.Count,
                        InLibraryEpisodeCount = inLibraryEpisodeCount,
                        WatchedEpisodeCount = watchedEpisodeCount,
                        TotalEpisodeCount = totalEpisodeCount,
                        UpdatedAt = seasons.Select(x => x.UpdatedAt).Append(series.UpdatedAt).Max()
                    };
                })
            .Where(x => x.IsInLibrary || stateRows.Any(state => state.SeriesId == x.SeriesId))
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetTvSeasonLibraryItemsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var seasonRows = await dbContext.TvSeasons
            .AsNoTracking()
            .Select(
                x => new TvSeasonLibraryRow
                {
                    SeasonId = x.Id,
                    SeriesId = x.TvSeriesId,
                    TmdbSeriesId = x.Series!.TmdbSeriesId,
                    SeasonNumber = x.SeasonNumber,
                    Name = x.Name,
                    SeriesName = x.Series.Name,
                    OriginalSeriesName = x.Series.OriginalName ?? string.Empty,
                    Overview = x.Overview ?? x.Series.Overview ?? string.Empty,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    SeriesPosterRemoteUrl = x.Series.PosterRemoteUrl ?? string.Empty,
                    GenresText = x.Series.GenresText ?? string.Empty,
                    Country = x.Series.Country ?? string.Empty,
                    Language = x.Series.Language ?? string.Empty,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : x.Series.FirstAirYear,
                    TotalEpisodeCount = x.TmdbEpisodeCount,
                    IdentificationStatus = x.IdentificationStatus,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        var seasonIds = seasonRows.Select(x => x.SeasonId).ToArray();
        if (seasonIds.Length == 0)
        {
            return [];
        }

        var episodeRows = await dbContext.TvEpisodes
            .AsNoTracking()
            .Where(x => seasonIds.Contains(x.TvSeasonId))
            .Select(
                x => new TvEpisodeLibraryRow
                {
                    EpisodeId = x.Id,
                    SeasonId = x.TvSeasonId,
                    IsWatched = x.IsWatched
                })
            .ToListAsync(cancellationToken);
        var sourceRows = await LoadTvSourceRowsAsync(dbContext, episodeRows.Select(x => x.EpisodeId).ToArray(), cancellationToken);
        var stateRows = await LoadTvCollectionStateRowsAsync(dbContext, seasonIds, cancellationToken);
        var stateBySeason = stateRows
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt).First());
        var episodesBySeason = episodeRows
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var sourcesBySeason = sourceRows
            .Join(
                episodeRows,
                source => source.EpisodeId,
                episode => episode.EpisodeId,
                (source, episode) => new { episode.SeasonId, episode.EpisodeId, source.ProtocolType })
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return seasonRows
            .Select(
                season =>
                {
                    var episodes = episodesBySeason.GetValueOrDefault(season.SeasonId) ?? [];
                    var sources = sourcesBySeason.GetValueOrDefault(season.SeasonId) ?? [];
                    stateBySeason.TryGetValue(season.SeasonId, out var state);
                    var inLibraryEpisodeCount = sources.Select(x => x.EpisodeId).Distinct().Count();
                    var totalEpisodeCount = season.TotalEpisodeCount.GetValueOrDefault() > 0
                        ? season.TotalEpisodeCount!.Value
                        : episodes.Count;
                    var watchedEpisodeCount = episodes.Count(x => x.IsWatched);
                    var isWatched = IsAggregateWatched(watchedEpisodeCount, episodes.Count, totalEpisodeCount);
                    var isUnwatched = watchedEpisodeCount == 0;
                    return new LibraryMovieListItem
                    {
                        ItemKind = LibraryMediaItemKind.Season,
                        SeriesId = season.SeriesId,
                        SeasonId = season.SeasonId,
                        SeasonNumber = season.SeasonNumber,
                        TmdbId = season.TmdbSeriesId,
                        Title = BuildSeasonTitle(season.SeriesName, season.Name, season.SeasonNumber),
                        OriginalTitle = season.OriginalSeriesName,
                        ReleaseYear = season.AirYear,
                        PosterRemoteUrl = FirstNonEmpty(season.PosterRemoteUrl, season.SeriesPosterRemoteUrl),
                        GenresText = season.GenresText,
                        Overview = season.Overview,
                        Country = season.Country,
                        Language = season.Language,
                        IdentificationStatus = season.IdentificationStatus,
                        SourceCount = sources.Count,
                        HasLocalSource = sources.Any(x => x.ProtocolType == ProtocolType.Local),
                        HasWebDavSource = sources.Any(x => x.ProtocolType == ProtocolType.WebDav),
                        IsInLibrary = sources.Count > 0,
                        IsFavorite = state?.IsFavorite == true && isWatched,
                        IsWantToWatch = state?.IsWantToWatch == true && isUnwatched,
                        IsNotInterested = state?.IsNotInterested == true,
                        IsWatched = isWatched,
                        WatchedEpisodeCount = watchedEpisodeCount,
                        InLibraryEpisodeCount = inLibraryEpisodeCount,
                        TotalEpisodeCount = totalEpisodeCount,
                        UpdatedAt = state?.UpdatedAt > season.UpdatedAt ? state.UpdatedAt : season.UpdatedAt
                    };
                })
            .Where(x => x.IsInLibrary || x.IsFavorite || x.IsWantToWatch || x.IsNotInterested)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    private static async Task<IReadOnlyList<TvSourceLibraryRow>> LoadTvSourceRowsAsync(
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
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted)
            .Select(
                x => new TvSourceLibraryRow
                {
                    EpisodeId = x.EpisodeId!.Value,
                    ProtocolType = x.SourceConnection!.ProtocolType
                })
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<TvSeasonStateLibraryRow>> LoadTvCollectionStateRowsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> seasonIds,
        CancellationToken cancellationToken)
    {
        if (seasonIds.Count == 0)
        {
            return [];
        }

        return await dbContext.UserTvSeasonCollectionItems
            .AsNoTracking()
            .Where(
                x => x.TvSeasonId.HasValue
                     && seasonIds.Contains(x.TvSeasonId.Value)
                     && (x.IsFavorite || x.IsWantToWatch || x.IsNotInterested))
            .Select(
                x => new TvSeasonStateLibraryRow
                {
                    SeasonId = x.TvSeasonId!.Value,
                    SeriesId = x.TvSeriesId,
                    IsFavorite = x.IsFavorite,
                    IsWantToWatch = x.IsWantToWatch,
                    IsNotInterested = x.IsNotInterested,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
    }

    private static string BuildSeasonTitle(string seriesTitle, string seasonTitle, int seasonNumber)
    {
        var normalizedSeries = string.IsNullOrWhiteSpace(seriesTitle) ? "未命名电视剧" : seriesTitle.Trim();
        var normalizedSeason = string.IsNullOrWhiteSpace(seasonTitle) ? $"S{seasonNumber:D2}" : seasonTitle.Trim();
        return normalizedSeason.Contains(normalizedSeries, StringComparison.OrdinalIgnoreCase)
            ? normalizedSeason
            : $"{normalizedSeries} {normalizedSeason}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static bool IsAggregateWatched(int watchedEpisodeCount, int knownEpisodeCount, int totalEpisodeCount)
    {
        if (totalEpisodeCount <= 0)
        {
            return knownEpisodeCount > 0 && watchedEpisodeCount >= knownEpisodeCount;
        }

        return knownEpisodeCount >= totalEpisodeCount && watchedEpisodeCount >= totalEpisodeCount;
    }

    private record LibraryCollectionIdentity(
        int? MovieId,
        int? TmdbId,
        string ImdbId,
        string Title,
        int? ReleaseYear);

    private sealed record LibraryCollectionState(
        int? MovieId,
        int? TmdbId,
        string ImdbId,
        string Title,
        int? ReleaseYear,
        bool IsWatched,
        bool IsWantToWatch,
        bool IsNotInterested)
        : LibraryCollectionIdentity(MovieId, TmdbId, ImdbId, Title, ReleaseYear);

    private sealed class LibraryCollectionIdentityIndex
    {
        private readonly HashSet<int> _movieIds = [];
        private readonly HashSet<int> _tmdbIds = [];
        private readonly HashSet<string> _imdbIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _titleYearKeys = new(StringComparer.OrdinalIgnoreCase);

        private LibraryCollectionIdentityIndex()
        {
        }

        public static LibraryCollectionIdentityIndex Create(IEnumerable<LibraryCollectionIdentity> identities)
        {
            var index = new LibraryCollectionIdentityIndex();
            foreach (var identity in identities)
            {
                if (identity.MovieId is > 0)
                {
                    index._movieIds.Add(identity.MovieId.Value);
                }

                if (identity.TmdbId is > 0)
                {
                    index._tmdbIds.Add(identity.TmdbId.Value);
                }

                if (!string.IsNullOrWhiteSpace(identity.ImdbId))
                {
                    index._imdbIds.Add(NormalizeImdbId(identity.ImdbId));
                }

                if (!string.IsNullOrWhiteSpace(identity.Title) && identity.ReleaseYear.HasValue)
                {
                    index._titleYearKeys.Add(BuildTitleYearKey(identity.Title, identity.ReleaseYear));
                }
            }

            return index;
        }

        public bool Contains(
            int? movieId,
            int? tmdbId,
            string? imdbId,
            string title,
            int? releaseYear)
        {
            if (movieId is > 0 && _movieIds.Contains(movieId.Value))
            {
                return true;
            }

            if (tmdbId is > 0 && _tmdbIds.Contains(tmdbId.Value))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(imdbId) && _imdbIds.Contains(NormalizeImdbId(imdbId)))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(title)
                   && releaseYear.HasValue
                   && _titleYearKeys.Contains(BuildTitleYearKey(title, releaseYear));
        }
    }

    private sealed class TvSeasonLibraryRow
    {
        public int SeasonId { get; set; }

        public int SeriesId { get; set; }

        public int? TmdbSeriesId { get; set; }

        public int SeasonNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public string SeriesName { get; set; } = string.Empty;

        public string OriginalSeriesName { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public string PosterRemoteUrl { get; set; } = string.Empty;

        public string SeriesPosterRemoteUrl { get; set; } = string.Empty;

        public string GenresText { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public int? AirYear { get; set; }

        public int? TotalEpisodeCount { get; set; }

        public IdentificationStatus IdentificationStatus { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    private sealed class TvEpisodeLibraryRow
    {
        public int EpisodeId { get; set; }

        public int SeasonId { get; set; }

        public bool IsWatched { get; set; }
    }

    private sealed class TvSourceLibraryRow
    {
        public int EpisodeId { get; set; }

        public ProtocolType ProtocolType { get; set; }
    }

    private sealed class TvSeasonStateLibraryRow
    {
        public int SeasonId { get; set; }

        public int? SeriesId { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsWantToWatch { get; set; }

        public bool IsNotInterested { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
