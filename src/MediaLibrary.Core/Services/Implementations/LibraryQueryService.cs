using System.Diagnostics;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
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
        var totalStopwatch = Stopwatch.StartNew();
        var movieStopwatch = Stopwatch.StartNew();
        var movies = await GetLibraryMoviesAsync(cancellationToken);
        movieStopwatch.Stop();
        IReadOnlyList<LibraryMovieListItem> tvItems;
        var tvStopwatch = Stopwatch.StartNew();
        if (expandSeriesToSeasons)
        {
            tvItems = await GetTvSeasonLibraryItemsAsync(cancellationToken);
        }
        else
        {
            tvItems = await GetTvSeriesLibraryItemsAsync(includeUnknownSeriesAsOther: true, cancellationToken);
        }
        tvStopwatch.Stop();

        var sortStopwatch = Stopwatch.StartNew();
        var items = movies
            .Concat(tvItems)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Title)
            .ToList();
        sortStopwatch.Stop();
        LogLibraryContentCategorySummary(items);
        totalStopwatch.Stop();
        WriteLibraryQueryPerfEvent(
            $"event=library-query-completed expandSeriesToSeasons={FormatBool(expandSeriesToSeasons)} movieMs={movieStopwatch.ElapsedMilliseconds} tvMs={tvStopwatch.ElapsedMilliseconds} sortMs={sortStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} movieItems={movies.Count} tvItems={tvItems.Count} resultItems={items.Count} movie={items.Count(x => x.IsMovie)} tv={items.Count(x => x.IsSeries || x.IsSeason)} other={items.Count(x => x.IsOther)}");

        return items;
    }

    public async Task<IReadOnlyList<LibraryMovieListItem>> GetHiddenLibraryItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movieStopwatch = Stopwatch.StartNew();
        var movies = await GetHiddenMovieItemsAsync(dbContext, cancellationToken);
        movieStopwatch.Stop();
        var seasonStopwatch = Stopwatch.StartNew();
        var seasons = await GetHiddenSeasonItemsAsync(dbContext, cancellationToken);
        seasonStopwatch.Stop();

        var sortStopwatch = Stopwatch.StartNew();
        var items = movies
            .Concat(seasons)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Title)
            .ToList();
        sortStopwatch.Stop();
        totalStopwatch.Stop();
        WriteLibraryQueryPerfEvent(
            $"event=library-hidden-query-completed movieMs={movieStopwatch.ElapsedMilliseconds} seasonMs={seasonStopwatch.ElapsedMilliseconds} sortMs={sortStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} movieItems={movies.Count} seasonItems={seasons.Count} resultItems={items.Count}");
        return items;
    }

    public async Task<IReadOnlyList<LibraryMovieListItem>> GetLibraryMoviesAsync(
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var collectionStopwatch = Stopwatch.StartNew();
        var collectionStates = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested || x.LibraryVisibilityState != LibraryVisibilityState.Auto)
            .Select(
                x => new LibraryCollectionState(
                    x.MovieId,
                    x.TmdbId,
                    x.ImdbId,
                    x.Title,
                    x.ReleaseYear,
                    x.IsWatched,
                    x.IsWantToWatch,
                    x.IsNotInterested,
                    x.LibraryVisibilityState,
                    x.UpdatedAt))
            .ToListAsync(cancellationToken);
        collectionStopwatch.Stop();
        var collectionMovieIds = collectionStates
            .Where(x => x.MovieId is > 0)
            .Select(x => x.MovieId!.Value)
            .Distinct()
            .ToArray();
        var watchedIndex = LibraryCollectionIdentityIndex.Create(collectionStates.Where(x => x.IsWatched));
        var wantToWatchIndex = LibraryCollectionIdentityIndex.Create(collectionStates.Where(x => x.IsWantToWatch));
        var notInterestedIndex = LibraryCollectionIdentityIndex.Create(collectionStates.Where(x => x.IsNotInterested));

        var movieRowsStopwatch = Stopwatch.StartNew();
        var movies = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.MediaFiles.Any(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        || x.IsWatched
                        || x.IsFavorite
                        || x.UserRating.HasValue
                        || x.WatchHistories.Any()
                        || collectionMovieIds.Contains(x.Id)
                        || (x.TmdbId.HasValue && x.IdentificationStatus != IdentificationStatus.Failed))
            .Select(
                x => new
                {
                    x.Id,
                    x.TmdbId,
                    x.Title,
                    x.OriginalTitle,
                    x.ReleaseYear,
                    x.ReleaseDate,
                    x.PosterRemoteUrl,
                    x.GenresText,
                    x.AiTagsText,
                    x.EmotionTagsText,
                    x.SceneTagsText,
                    x.Overview,
                    x.Country,
                    x.Language,
                    x.DirectorText,
                    x.ActorsText,
                    x.RuntimeMinutes,
                    x.ImdbId,
                    x.IdentificationStatus,
                    x.IdentifiedConfidence,
                    x.IsFavorite,
                    x.IsWatched,
                    x.UserRating,
                    x.CreatedAt,
                    x.UpdatedAt,
                    LatestSourceUpdatedAt = x.MediaFiles
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .Select(mediaFile => (DateTime?)mediaFile.UpdatedAt)
                        .Max(),
                    DefaultMediaFileName = x.MediaFiles
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .OrderBy(mediaFile => mediaFile.FileName)
                        .Select(mediaFile => mediaFile.FileName)
                        .FirstOrDefault(),
                    HasWatchHistory = x.WatchHistories.Any()
                                      || x.MediaFiles.Any(
                                          mediaFile => !mediaFile.IsDeleted
                                                       && mediaFile.MediaType == MediaType.Video
                                                       && mediaFile.WatchHistories.Any()),
                    LatestHistoryLastPlayPositionSeconds = x.MediaFiles
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .SelectMany(mediaFile => mediaFile.WatchHistories)
                        .OrderByDescending(history => history.StartedAt)
                        .Select(history => (int?)history.LastPlayPositionSeconds)
                        .FirstOrDefault(),
                    LatestHistoryDurationSeconds = x.MediaFiles
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .SelectMany(mediaFile => mediaFile.WatchHistories)
                        .OrderByDescending(history => history.StartedAt)
                        .Select(history => history.MediaFile == null ? null : history.MediaFile.DurationSeconds)
                        .FirstOrDefault(),
                    LatestHistoryIsCompleted = x.MediaFiles
                        .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                        .SelectMany(mediaFile => mediaFile.WatchHistories)
                        .OrderByDescending(history => history.StartedAt)
                        .Select(history => (bool?)history.IsCompleted)
                        .FirstOrDefault(),
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
        movieRowsStopwatch.Stop();

        var orphanStopwatch = Stopwatch.StartNew();
        var orphanItems = await GetOrphanMediaFileLibraryItemsAsync(dbContext, cancellationToken);
        orphanStopwatch.Stop();
        var externalStopwatch = Stopwatch.StartNew();
        var externalItems = await GetExternalCollectionMoviesAsync(dbContext, cancellationToken);
        externalStopwatch.Stop();
        var projectionStopwatch = Stopwatch.StartNew();
        var items = movies
            .Select(
                x =>
                {
                    var tmdbRating = x.Ratings.FirstOrDefault(rating => string.Equals(rating.SourceName, "TMDB", StringComparison.OrdinalIgnoreCase));
                    var omdbRating = x.Ratings.FirstOrDefault(rating => string.Equals(rating.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase));
                    var primaryRating = BuildMoviePrimaryRating(tmdbRating, omdbRating);
                    var matchingStates = collectionStates
                        .Where(state => MatchesCollectionIdentity(state, x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear))
                        .ToList();
                    var collectionUpdatedAt = matchingStates.Count == 0
                        ? DateTime.MinValue
                        : matchingStates.Max(state => state.UpdatedAt);
                    var visibilityState = ResolveLibraryVisibilityState(matchingStates);
                    var isWatched = x.IsWatched || watchedIndex.Contains(x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear);
                    var isWantToWatch = wantToWatchIndex.Contains(x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear);
                    var isNotInterested = notInterestedIndex.Contains(x.Id, x.TmdbId, x.ImdbId, x.Title, x.ReleaseYear);
                    var hasActiveSource = x.SourceCount > 0;
                    var hasLocalRecognizedMovie = x.TmdbId.HasValue
                                                  && x.IdentificationStatus != IdentificationStatus.Failed;
                    var hasUserState = x.IsFavorite
                                       || isWatched
                                       || x.UserRating.HasValue
                                       || x.HasWatchHistory
                                       || isWantToWatch
                                       || isNotInterested;
                    var progressPercent = ResolvePlaybackProgressPercent(
                        x.LatestHistoryLastPlayPositionSeconds.GetValueOrDefault(),
                        x.LatestHistoryDurationSeconds ?? ResolveRuntimeDurationSeconds(x.RuntimeMinutes));
                    var isVisibleInLibrary = ResolveIsVisibleInLibrary(
                        hasActiveSource,
                        visibilityState,
                        hasUserState || hasLocalRecognizedMovie);

                    return new LibraryMovieListItem
                    {
                        ItemKind = ResolveMovieItemKind(x.IdentificationStatus),
                        MovieId = x.Id,
                        TmdbId = x.TmdbId,
                        Title = ResolveUnidentifiedMovieDisplayTitle(x.IdentificationStatus, x.Title, x.DefaultMediaFileName),
                        OriginalTitle = x.OriginalTitle ?? string.Empty,
                        ReleaseYear = x.ReleaseYear,
                        ReleaseDate = x.ReleaseDate,
                        PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                        GenresText = x.GenresText ?? string.Empty,
                        AiTagsText = x.AiTagsText ?? string.Empty,
                        EmotionTagsText = x.EmotionTagsText ?? string.Empty,
                        SceneTagsText = x.SceneTagsText ?? string.Empty,
                        Overview = x.Overview ?? string.Empty,
                        Country = x.Country ?? string.Empty,
                        Language = x.Language ?? string.Empty,
                        DirectorText = x.DirectorText ?? string.Empty,
                        ActorsText = x.ActorsText ?? string.Empty,
                        RuntimeMinutes = x.RuntimeMinutes,
                        ImdbId = x.ImdbId ?? string.Empty,
                        IdentificationStatus = x.IdentificationStatus,
                        IdentifiedConfidence = x.IdentifiedConfidence,
                        PrimaryRatingSourceName = primaryRating.SourceName,
                        PrimaryRatingValue = primaryRating.Value,
                        PrimaryRatingScale = primaryRating.Scale,
                        PrimaryRatingVoteCount = primaryRating.VoteCount,
                        TmdbRating = tmdbRating?.ScoreValue,
                        TmdbVoteCount = tmdbRating?.VoteCount,
                        OmdbScoreValue = omdbRating?.ScoreValue,
                        OmdbScoreScale = omdbRating?.ScoreScale,
                        OmdbVoteCount = omdbRating?.VoteCount,
                        OmdbSourceUrl = omdbRating?.SourceUrl ?? string.Empty,
                        OmdbLastUpdatedAt = omdbRating?.LastUpdatedAt,
                        SourceCount = x.SourceCount,
                        ActiveSourceCount = x.SourceCount,
                        HasActiveSource = hasActiveSource,
                        HasLocalSource = x.HasLocalSource,
                        HasWebDavSource = x.HasWebDavSource,
                        IsVisibleInLibrary = isVisibleInLibrary,
                        LibraryVisibilityState = visibilityState,
                        HasLibraryContext = isVisibleInLibrary,
                        IsInLibrary = hasActiveSource,
                        IsFavorite = x.IsFavorite,
                        IsWatched = isWatched,
                        IsWantToWatch = isWantToWatch,
                        IsNotInterested = isNotInterested,
                        HasUserState = hasUserState,
                        HasWatchHistory = x.HasWatchHistory,
                        ProgressPercent = progressPercent,
                        UpdatedAt = MaxDate(collectionUpdatedAt, x.LatestSourceUpdatedAt, x.CreatedAt)
                    };
            })
            .Where(x => x.IsVisibleInLibrary)
            .Concat(orphanItems)
            .Concat(externalItems)
            .GroupBy(BuildLibraryItemKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.HasActiveSource).ThenByDescending(x => x.IsVisibleInLibrary).ThenByDescending(x => x.UpdatedAt).First())
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
        projectionStopwatch.Stop();
        totalStopwatch.Stop();
        WriteLibraryQueryPerfEvent(
            $"event=library-query-movie-completed collectionMs={collectionStopwatch.ElapsedMilliseconds} movieRowsMs={movieRowsStopwatch.ElapsedMilliseconds} orphanMs={orphanStopwatch.ElapsedMilliseconds} externalMs={externalStopwatch.ElapsedMilliseconds} projectionMs={projectionStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} collectionRows={collectionStates.Count} movieRows={movies.Count} orphanItems={orphanItems.Count} externalItems={externalItems.Count} resultItems={items.Count}");

        return items;
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetOrphanMediaFileLibraryItemsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var queryStopwatch = Stopwatch.StartNew();
        var rows = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => !x.IsDeleted
                     && x.MediaType == MediaType.Video
                     && !x.MovieId.HasValue
                     && !x.EpisodeId.HasValue)
            .Select(
                x => new
                {
                    x.Id,
                    x.FileName,
                    x.FilePath,
                    x.Extension,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.LastSeenAt,
                    ProtocolType = x.SourceConnection != null
                        ? (ProtocolType?)x.SourceConnection.ProtocolType
                        : null
                })
            .ToListAsync(cancellationToken);
        queryStopwatch.Stop();

        var projectionStopwatch = Stopwatch.StartNew();
        var items = rows
            .Select(
                x =>
                {
                    var parentPath = MoviePlaceholderGroupingHelper.GetDirectParentPath(x.FilePath);
                    var parentDisplay = MoviePlaceholderGroupingHelper.GetParentFolderDisplay(parentPath);
                    return new LibraryMovieListItem
                    {
                        ItemKind = LibraryMediaItemKind.Other,
                        OrphanMediaFileId = x.Id,
                        GroupedRangeKey = $"orphan-media-file:{x.Id}",
                        GroupedRangeMediaFileIds = [x.Id],
                        GroupedRangeParentDisplay = parentDisplay,
                        GroupedRangeSampleFilesText = x.FileName,
                        GroupedRangeReasonTagsText = "orphan-media-file",
                        Title = string.IsNullOrWhiteSpace(x.FileName) ? "-" : x.FileName.Trim(),
                        OriginalTitle = string.Empty,
                        Overview = string.IsNullOrWhiteSpace(parentDisplay) ? string.Empty : parentDisplay,
                        IdentificationStatus = IdentificationStatus.Failed,
                        SourceCount = 1,
                        ActiveSourceCount = 1,
                        HasActiveSource = true,
                        HasLocalSource = x.ProtocolType == ProtocolType.Local,
                        HasWebDavSource = x.ProtocolType == ProtocolType.WebDav,
                        IsVisibleInLibrary = true,
                        HasLibraryContext = true,
                        IsInLibrary = true,
                        UpdatedAt = x.UpdatedAt == default ? x.CreatedAt : x.UpdatedAt
                    };
            })
            .ToList();
        projectionStopwatch.Stop();
        totalStopwatch.Stop();

        ScanIdentificationDiagnostics.Write(
            $"event=library-orphan-media-projection orphanMediaFilesProjectedToOther={items.Count} orphanMediaFilesHiddenBecauseGrouped=0 orphanMediaFilesWithoutProjection=0 unknownFileItemsCount={items.Count}");
        WriteLibraryQueryPerfEvent(
            $"event=library-query-orphan-completed queryMs={queryStopwatch.ElapsedMilliseconds} projectionMs={projectionStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} rows={rows.Count} resultItems={items.Count}");
        return items;
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetHiddenMovieItemsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(
                x => new
                {
                    x.MovieId,
                    x.TmdbId,
                    x.Title,
                    x.OriginalTitle,
                    x.ReleaseYear,
                    x.ReleaseDate,
                    x.PosterRemoteUrl,
                    x.Overview,
                    x.GenresText,
                    x.Country,
                    x.Language,
                    x.RuntimeMinutes,
                    x.ImdbId,
                    x.TmdbRating,
                    x.TmdbVoteCount,
                    x.OmdbScoreValue,
                    x.OmdbScoreScale,
                    x.OmdbVoteCount,
                    x.OmdbSourceUrl,
                    x.OmdbLastUpdatedAt,
                    x.IsWatched,
                    x.IsWantToWatch,
                    x.IsNotInterested,
                    x.IsInLibrary,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var movieIds = rows
            .Where(x => x.MovieId is > 0)
            .Select(x => x.MovieId!.Value)
            .Distinct()
            .ToArray();
        var movieRows = movieIds.Length == 0
            ? []
            : await dbContext.Movies
                .AsNoTracking()
                .Where(x => movieIds.Contains(x.Id))
                .Select(
                    x => new
                    {
                        x.Id,
                        x.TmdbId,
                        x.Title,
                        x.OriginalTitle,
                        x.ReleaseYear,
                        x.ReleaseDate,
                        x.PosterRemoteUrl,
                        x.Overview,
                        x.GenresText,
                        x.AiTagsText,
                        x.EmotionTagsText,
                        x.SceneTagsText,
                        x.Country,
                        x.Language,
                        x.RuntimeMinutes,
                        x.ImdbId,
                        x.IdentificationStatus,
                        x.IdentifiedConfidence,
                        x.IsFavorite,
                        x.IsWatched,
                        x.UpdatedAt,
                        DefaultMediaFileName = x.MediaFiles
                            .Where(mediaFile => !mediaFile.IsDeleted && mediaFile.MediaType == MediaType.Video)
                            .OrderBy(mediaFile => mediaFile.FileName)
                            .Select(mediaFile => mediaFile.FileName)
                            .FirstOrDefault(),
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
                                         && mediaFile.SourceConnection.ProtocolType == ProtocolType.WebDav)
                    })
                .ToListAsync(cancellationToken);
        var movieById = movieRows.ToDictionary(x => x.Id);

        return rows
            .Select(
                row =>
                {
                    movieById.TryGetValue(row.MovieId.GetValueOrDefault(), out var movie);
                    var sourceCount = movie?.SourceCount ?? 0;
                    var primaryRating = BuildMoviePrimaryRating(
                        row.TmdbRating,
                        row.TmdbVoteCount,
                        row.OmdbScoreValue,
                        row.OmdbScoreScale,
                        row.OmdbVoteCount);
                    return new LibraryMovieListItem
                    {
                        ItemKind = movie is null
                            ? LibraryMediaItemKind.Movie
                            : ResolveMovieItemKind(movie.IdentificationStatus),
                        MovieId = movie?.Id ?? 0,
                        TmdbId = movie?.TmdbId ?? row.TmdbId,
                        Title = movie is null
                            ? row.Title
                            : ResolveUnidentifiedMovieDisplayTitle(
                                movie.IdentificationStatus,
                                FirstNonEmpty(movie.Title, row.Title),
                                movie.DefaultMediaFileName),
                        OriginalTitle = FirstNonEmpty(movie?.OriginalTitle, row.OriginalTitle),
                        ReleaseYear = movie?.ReleaseYear ?? row.ReleaseYear,
                        ReleaseDate = movie?.ReleaseDate ?? row.ReleaseDate,
                        PosterRemoteUrl = FirstNonEmpty(movie?.PosterRemoteUrl, row.PosterRemoteUrl),
                        GenresText = FirstNonEmpty(movie?.GenresText, row.GenresText),
                        AiTagsText = movie?.AiTagsText ?? string.Empty,
                        EmotionTagsText = movie?.EmotionTagsText ?? string.Empty,
                        SceneTagsText = movie?.SceneTagsText ?? string.Empty,
                        Overview = FirstNonEmpty(movie?.Overview, row.Overview),
                        Country = FirstNonEmpty(movie?.Country, row.Country),
                        Language = FirstNonEmpty(movie?.Language, row.Language),
                        RuntimeMinutes = movie?.RuntimeMinutes ?? row.RuntimeMinutes,
                        ImdbId = FirstNonEmpty(movie?.ImdbId, row.ImdbId),
                        IdentificationStatus = movie?.IdentificationStatus ?? IdentificationStatus.ManualConfirmed,
                        IdentifiedConfidence = movie?.IdentifiedConfidence,
                        PrimaryRatingSourceName = primaryRating.SourceName,
                        PrimaryRatingValue = primaryRating.Value,
                        PrimaryRatingScale = primaryRating.Scale,
                        PrimaryRatingVoteCount = primaryRating.VoteCount,
                        TmdbRating = row.TmdbRating,
                        TmdbVoteCount = row.TmdbVoteCount,
                        OmdbScoreValue = row.OmdbScoreValue,
                        OmdbScoreScale = row.OmdbScoreScale,
                        OmdbVoteCount = row.OmdbVoteCount,
                        OmdbSourceUrl = row.OmdbSourceUrl,
                        OmdbLastUpdatedAt = row.OmdbLastUpdatedAt,
                        SourceCount = sourceCount,
                        ActiveSourceCount = sourceCount,
                        HasActiveSource = sourceCount > 0,
                        HasLocalSource = movie?.HasLocalSource == true,
                        HasWebDavSource = movie?.HasWebDavSource == true,
                        IsVisibleInLibrary = false,
                        LibraryVisibilityState = LibraryVisibilityState.Hidden,
                        HasLibraryContext = true,
                        HasUserState = movie?.IsFavorite == true || movie?.IsWatched == true || row.IsWatched || row.IsWantToWatch || row.IsNotInterested,
                        IsInLibrary = sourceCount > 0 || row.IsInLibrary,
                        IsFavorite = movie?.IsFavorite == true,
                        IsWatched = movie?.IsWatched == true || row.IsWatched,
                        IsWantToWatch = row.IsWantToWatch,
                        IsNotInterested = row.IsNotInterested,
                        ProgressPercent = 0d,
                        UpdatedAt = row.UpdatedAt
                    };
                })
            .GroupBy(BuildLibraryItemKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.HasActiveSource).ThenByDescending(x => x.UpdatedAt).First())
            .ToList();
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetHiddenSeasonItemsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var stateRows = await dbContext.UserTvSeasonCollectionItems
            .AsNoTracking()
            .Where(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden && x.TvSeasonId.HasValue)
            .Select(
                x => new
                {
                    TvSeasonId = x.TvSeasonId!.Value,
                    x.IsFavorite,
                    x.IsWantToWatch,
                    x.IsNotInterested,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        if (stateRows.Count == 0)
        {
            return [];
        }

        var stateBySeason = stateRows
            .GroupBy(x => x.TvSeasonId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt).First());
        var seasonIds = stateBySeason.Keys.ToArray();
        var seasonRows = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => seasonIds.Contains(x.Id))
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
                    DirectorText = x.Series.DirectorText ?? string.Empty,
                    ActorsText = x.Series.ActorsText ?? string.Empty,
                    TmdbRating = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    TmdbVoteCount = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    OmdbScoreValue = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    OmdbScoreScale = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreScale)
                        .FirstOrDefault(),
                    OmdbVoteCount = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    OmdbSourceUrl = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.SourceUrl ?? string.Empty)
                        .FirstOrDefault() ?? string.Empty,
                    OmdbLastUpdatedAt = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.LastUpdatedAt ?? rating.CreatedAt)
                        .FirstOrDefault(),
                    AirDate = x.AirDate,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : x.Series.FirstAirYear,
                    TotalEpisodeCount = x.TmdbEpisodeCount,
                    IdentificationStatus = x.IdentificationStatus,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        var episodeAggregateRows = await LoadTvEpisodeSeasonAggregateRowsAsync(dbContext, seasonIds, cancellationToken);
        var sourceAggregateRows = await LoadTvSourceSeasonAggregateRowsAsync(dbContext, seasonIds, cancellationToken);
        var episodeAggregatesBySeason = episodeAggregateRows.ToDictionary(x => x.SeasonId);
        var sourceAggregatesBySeason = sourceAggregateRows.ToDictionary(x => x.SeasonId);
        var metricsBySeason = seasonRows.ToDictionary(
            x => x.SeasonId,
            x => BuildTvSeasonProjectionMetrics(x, episodeAggregatesBySeason, sourceAggregatesBySeason));

        return seasonRows
            .Select(
                season =>
                {
                    stateBySeason.TryGetValue(season.SeasonId, out var state);
                    var metrics = metricsBySeason[season.SeasonId];
                    var watchedEpisodeCount = metrics.WatchedEpisodeCount;
                    var totalEpisodeCount = metrics.TotalEpisodeCount;
                    var isWatched = IsAggregateWatched(watchedEpisodeCount, metrics.CountableEpisodeCount, totalEpisodeCount);
                    return new LibraryMovieListItem
                    {
                        ItemKind = ResolveTvSeasonItemKind(season.IdentificationStatus),
                        SeriesId = season.SeriesId,
                        SeasonId = season.SeasonId,
                        SeasonNumber = season.SeasonNumber,
                        TmdbId = season.TmdbSeriesId,
                        Title = BuildSeasonTitle(season.SeriesName, season.Name, season.SeasonNumber),
                        SeriesTitle = season.SeriesName,
                        OriginalTitle = season.OriginalSeriesName,
                        ReleaseYear = season.AirYear,
                        ReleaseDate = season.AirDate,
                        PosterRemoteUrl = FirstNonEmpty(season.PosterRemoteUrl, season.SeriesPosterRemoteUrl),
                        GenresText = season.GenresText,
                        Overview = season.Overview,
                        Country = season.Country,
                        Language = season.Language,
                        DirectorText = season.DirectorText,
                        ActorsText = season.ActorsText,
                        IdentificationStatus = season.IdentificationStatus,
                        SourceCount = metrics.SourceCount,
                        ActiveSourceCount = metrics.SourceCount,
                        HasActiveSource = metrics.SourceCount > 0,
                        HasLocalSource = metrics.HasLocalSource,
                        HasWebDavSource = metrics.HasWebDavSource,
                        IsVisibleInLibrary = false,
                        LibraryVisibilityState = LibraryVisibilityState.Hidden,
                        HasLibraryContext = true,
                        HasUserState = state?.IsFavorite == true || state?.IsWantToWatch == true || state?.IsNotInterested == true || watchedEpisodeCount > 0,
                        IsInLibrary = metrics.SourceCount > 0,
                        IsFavorite = state?.IsFavorite == true && isWatched,
                        IsWantToWatch = state?.IsWantToWatch == true && !isWatched,
                        IsNotInterested = state?.IsNotInterested == true,
                        IsWatched = isWatched,
                        WatchedEpisodeCount = watchedEpisodeCount,
                        InLibraryEpisodeCount = metrics.InLibraryEpisodeCount,
                        TotalEpisodeCount = totalEpisodeCount,
                        ProgressPercent = ResolveEpisodeProgressPercent(watchedEpisodeCount, totalEpisodeCount, isWatched),
                        UpdatedAt = state?.UpdatedAt > season.UpdatedAt ? state.UpdatedAt : season.UpdatedAt
                    };
                })
            .Where(ShouldShowTvSeasonLibraryItem)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetExternalCollectionMoviesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var queryStopwatch = Stopwatch.StartNew();
        var rows = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(
                x => (!x.IsInLibrary || !x.MovieId.HasValue)
                     && (x.IsWatched
                         || x.IsWantToWatch
                         || x.IsNotInterested
                         || x.LibraryVisibilityState != LibraryVisibilityState.Auto))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(
                x => new
                {
                    x.MovieId,
                    x.TmdbId,
                    x.Title,
                    x.OriginalTitle,
                    x.ReleaseYear,
                    x.ReleaseDate,
                    x.PosterRemoteUrl,
                    x.GenresText,
                    x.Overview,
                    x.Country,
                    x.Language,
                    x.RuntimeMinutes,
                    x.ImdbId,
                    x.TmdbRating,
                    x.TmdbVoteCount,
                    x.OmdbScoreValue,
                    x.OmdbScoreScale,
                    x.OmdbVoteCount,
                    x.OmdbSourceUrl,
                    x.OmdbLastUpdatedAt,
                    x.IsWatched,
                    x.IsWantToWatch,
                    x.IsNotInterested,
                    x.LibraryVisibilityState,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        queryStopwatch.Stop();

        var projectionStopwatch = Stopwatch.StartNew();
        var items = rows
            .Select(
                x =>
                {
                    var hasUserState = x.IsWatched || x.IsWantToWatch || x.IsNotInterested;
                    var isVisibleInLibrary = ResolveIsVisibleInLibrary(false, x.LibraryVisibilityState, hasUserState);
                    var primaryRating = BuildMoviePrimaryRating(
                        x.TmdbRating,
                        x.TmdbVoteCount,
                        x.OmdbScoreValue,
                        x.OmdbScoreScale,
                        x.OmdbVoteCount);

                    return new LibraryMovieListItem
                    {
                        MovieId = x.MovieId.GetValueOrDefault(),
                        TmdbId = x.TmdbId,
                        Title = x.Title,
                        OriginalTitle = x.OriginalTitle,
                        ReleaseYear = x.ReleaseYear,
                        ReleaseDate = x.ReleaseDate,
                        PosterRemoteUrl = x.PosterRemoteUrl,
                        GenresText = x.GenresText,
                        Overview = x.Overview,
                        Country = x.Country,
                        Language = x.Language,
                        RuntimeMinutes = x.RuntimeMinutes,
                        ImdbId = x.ImdbId,
                        IdentificationStatus = IdentificationStatus.Pending,
                        PrimaryRatingSourceName = primaryRating.SourceName,
                        PrimaryRatingValue = primaryRating.Value,
                        PrimaryRatingScale = primaryRating.Scale,
                        PrimaryRatingVoteCount = primaryRating.VoteCount,
                        TmdbRating = x.TmdbRating,
                        TmdbVoteCount = x.TmdbVoteCount,
                        OmdbScoreValue = x.OmdbScoreValue,
                        OmdbScoreScale = x.OmdbScoreScale,
                        OmdbVoteCount = x.OmdbVoteCount,
                        OmdbSourceUrl = x.OmdbSourceUrl,
                        OmdbLastUpdatedAt = x.OmdbLastUpdatedAt,
                        SourceCount = 0,
                        ActiveSourceCount = 0,
                        HasActiveSource = false,
                        IsVisibleInLibrary = isVisibleInLibrary,
                        LibraryVisibilityState = x.LibraryVisibilityState,
                        HasLibraryContext = isVisibleInLibrary,
                        HasUserState = hasUserState,
                        IsInLibrary = false,
                        IsWatched = x.IsWatched,
                        IsWantToWatch = x.IsWantToWatch,
                        IsNotInterested = x.IsNotInterested,
                        HasWatchHistory = false,
                        ProgressPercent = 0d,
                        UpdatedAt = x.UpdatedAt
                    };
            })
            .Where(x => x.IsVisibleInLibrary)
            .ToList();
        projectionStopwatch.Stop();
        totalStopwatch.Stop();
        WriteLibraryQueryPerfEvent(
            $"event=library-query-external-movie-completed queryMs={queryStopwatch.ElapsedMilliseconds} projectionMs={projectionStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} rows={rows.Count} resultItems={items.Count}");
        return items;
    }

    private static string ResolveUnidentifiedMovieDisplayTitle(
        IdentificationStatus identificationStatus,
        string storedTitle,
        string? defaultMediaFileName)
    {
        if (identificationStatus == IdentificationStatus.Failed
            && !string.IsNullOrWhiteSpace(defaultMediaFileName))
        {
            return defaultMediaFileName.Trim();
        }

        return storedTitle;
    }

    private static LibraryMediaItemKind ResolveMovieItemKind(IdentificationStatus identificationStatus)
    {
        return identificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed
            ? LibraryMediaItemKind.Movie
            : LibraryMediaItemKind.Other;
    }

    private static LibraryMediaItemKind ResolveTvSeasonItemKind(IdentificationStatus identificationStatus)
    {
        return identificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed
            ? LibraryMediaItemKind.Season
            : LibraryMediaItemKind.Other;
    }

    private static void LogLibraryContentCategorySummary(IReadOnlyCollection<LibraryMovieListItem> items)
    {
        var movieCount = items.Count(x => x.IsMovie);
        var tvCount = items.Count(x => x.IsSeries || x.IsSeason);
        var otherCount = items.Count(x => x.IsOther);
        var groupedCount = items.Count(x => x.IsOther
                                            && x.OrphanMediaFileId == 0
                                            && (x.SeasonId > 0 && x.IdentificationStatus == IdentificationStatus.Failed
                                                || !string.IsNullOrWhiteSpace(x.GroupedRangeKey)));
        var unknownMoviePlaceholders = items.Count(x => x.IsOther && x.MovieId > 0 && string.IsNullOrWhiteSpace(x.GroupedRangeKey));
        var unknownFileItems = items.Count(x => x.IsOther && x.OrphanMediaFileId > 0);
        ScanIdentificationDiagnostics.Write(
            $"event=library-content-category-summary movie={movieCount} tv={tvCount} other={otherCount} groupedTvLikePlaceholders={groupedCount} unknownMoviePlaceholders={unknownMoviePlaceholders} unknownFileItemsCount={unknownFileItems} otherCategoryCounts={otherCount} groupedRangesSelectable={groupedCount} groupedRangeDetailNavigationAvailable=true recognitionStatusFilterUiVisible=false");
    }

    private static void WriteLibraryQueryPerfEvent(string message)
    {
        AiPerfDiagnostics.WriteEvent(message);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string BuildLibraryItemKey(LibraryMovieListItem item)
    {
        if (item.IsOther && !string.IsNullOrWhiteSpace(item.GroupedRangeKey))
        {
            return $"other:{item.GroupedRangeKey}";
        }

        if (item.SeasonId > 0)
        {
            return $"season:{item.SeasonId}";
        }

        if (item.SeriesId > 0 && item.SeasonId == 0)
        {
            return $"series:{item.SeriesId}";
        }

        if (item.HasActiveSource && item.MovieId > 0)
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

    private static PrimaryRatingPresentation BuildMoviePrimaryRating(
        MovieRatingItem? tmdbRating,
        MovieRatingItem? omdbRating)
    {
        var ratings = new[]
            {
                tmdbRating,
                omdbRating
            }
            .Where(rating => rating is not null && IsValidRating(rating))
            .Select(rating => rating!)
            .ToList();
        if (ratings.Count == 0)
        {
            return PrimaryRatingPresentation.Empty;
        }

        var weightedRating = BuildWeightedRating(ratings);
        var sourceName = ratings.Count > 1
            ? "TMDB/IMDb"
            : GetDisplayRatingSourceName(ratings[0].SourceName);
        var voteCount = ratings.Sum(rating => Math.Max(rating.VoteCount ?? 0, 0));
        return new PrimaryRatingPresentation(sourceName, weightedRating, 10d, voteCount);
    }

    private static PrimaryRatingPresentation BuildMoviePrimaryRating(
        double? tmdbScore,
        int? tmdbVotes,
        double? omdbScore,
        double? omdbScale,
        int? omdbVotes)
    {
        return BuildMoviePrimaryRating(
            BuildRatingItem("TMDB", tmdbScore, 10d, tmdbVotes),
            BuildRatingItem("OMDb", omdbScore, omdbScale ?? 10d, omdbVotes));
    }

    private static MovieRatingItem? BuildRatingItem(
        string sourceName,
        double? scoreValue,
        double scoreScale,
        int? voteCount)
    {
        return scoreValue.HasValue
            ? new MovieRatingItem
            {
                SourceName = sourceName,
                ScoreValue = scoreValue.Value,
                ScoreScale = scoreScale,
                VoteCount = voteCount
            }
            : null;
    }

    private static double? BuildWeightedRating(IEnumerable<MovieRatingItem> ratings)
    {
        const int voteWeightCap = 100_000;
        var normalizedRatings = ratings
            .Where(IsValidRating)
            .Select(
                rating => new
                {
                    Rating = NormalizeRatingToTen(rating.ScoreValue, rating.ScoreScale),
                    Votes = Math.Min(Math.Max(rating.VoteCount ?? 0, 0), voteWeightCap)
                })
            .ToList();
        if (normalizedRatings.Count == 0)
        {
            return null;
        }

        if (normalizedRatings.Count == 1)
        {
            return normalizedRatings[0].Rating;
        }

        var totalVotes = normalizedRatings.Sum(x => x.Votes);
        return totalVotes > 0
            ? normalizedRatings.Sum(x => x.Rating * x.Votes) / totalVotes
            : normalizedRatings.Average(x => x.Rating);
    }

    private static bool IsValidRating(MovieRatingItem? rating)
    {
        return rating is { ScoreValue: > 0d, ScoreScale: > 0d };
    }

    private static double NormalizeRatingToTen(double scoreValue, double scoreScale)
    {
        return Math.Clamp(scoreValue / scoreScale * 10d, 0d, 10d);
    }

    private static string GetDisplayRatingSourceName(string sourceName)
    {
        return string.Equals(sourceName, "OMDb", StringComparison.OrdinalIgnoreCase)
            ? "IMDb"
            : sourceName;
    }

    private sealed record PrimaryRatingPresentation(
        string SourceName,
        double? Value,
        double? Scale,
        int? VoteCount)
    {
        public static PrimaryRatingPresentation Empty { get; } = new(string.Empty, null, null, null);
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetTvSeriesLibraryItemsAsync(
        bool includeUnknownSeriesAsOther,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var seriesRowsStopwatch = Stopwatch.StartNew();
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
                    x.FirstAirDate,
                    x.FirstAirYear,
                    x.GenresText,
                    x.Country,
                    x.Language,
                    x.DirectorText,
                    x.ActorsText,
                    TmdbRating = x.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    TmdbVoteCount = x.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    OmdbScoreValue = x.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    OmdbScoreScale = x.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreScale)
                        .FirstOrDefault(),
                    OmdbVoteCount = x.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    OmdbSourceUrl = x.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.SourceUrl ?? string.Empty)
                        .FirstOrDefault() ?? string.Empty,
                    OmdbLastUpdatedAt = x.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.LastUpdatedAt ?? rating.CreatedAt)
                        .FirstOrDefault(),
                    x.CreatedAt,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        seriesRowsStopwatch.Stop();
        var seriesIds = seriesRows.Select(x => x.Id).ToArray();
        if (seriesIds.Length == 0)
        {
            totalStopwatch.Stop();
            WriteLibraryQueryPerfEvent(
                $"event=library-query-tv-series-completed includeUnknownSeriesAsOther={FormatBool(includeUnknownSeriesAsOther)} seriesRowsMs={seriesRowsStopwatch.ElapsedMilliseconds} seasonRowsMs=0 episodeAggregateRowsMs=0 sourceAggregateRowsMs=0 stateRowsMs=0 projectionMs=0 totalMs={totalStopwatch.ElapsedMilliseconds} seriesRows=0 seasonRows=0 episodeRows=0 episodeAggregateRows=0 sourceRows=0 sourceAggregateRows=0 stateRows=0 resultItems=0");
            return [];
        }

        var seasonRowsStopwatch = Stopwatch.StartNew();
        var seasonRows = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => seriesIds.Contains(x.TvSeriesId))
            .Select(
                x => new TvSeasonLibraryRow
                {
                    SeasonId = x.Id,
                    SeriesId = x.TvSeriesId,
                    TmdbSeriesId = x.Series!.TmdbSeriesId,
                    TmdbSeasonId = x.TmdbSeasonId,
                    SeasonNumber = x.SeasonNumber,
                    Name = x.Name,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    AirDate = x.AirDate,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : (int?)null,
                    TotalEpisodeCount = x.TmdbEpisodeCount,
                    IdentificationStatus = x.IdentificationStatus,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        seasonRowsStopwatch.Stop();
        var seasonIds = seasonRows.Select(x => x.SeasonId).ToArray();
        var episodeAggregateRowsStopwatch = Stopwatch.StartNew();
        var episodeAggregateRows = await LoadTvEpisodeSeasonAggregateRowsAsync(dbContext, seasonIds, cancellationToken);
        episodeAggregateRowsStopwatch.Stop();
        var sourceAggregateRowsStopwatch = Stopwatch.StartNew();
        var sourceAggregateRows = await LoadTvSourceSeasonAggregateRowsAsync(dbContext, seasonIds, cancellationToken);
        sourceAggregateRowsStopwatch.Stop();
        var stateRowsStopwatch = Stopwatch.StartNew();
        var stateRows = await LoadTvCollectionStateRowsAsync(dbContext, seasonIds, cancellationToken);
        stateRowsStopwatch.Stop();

        var seasonsBySeries = seasonRows
            .GroupBy(x => x.SeriesId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var episodeAggregatesBySeason = episodeAggregateRows.ToDictionary(x => x.SeasonId);
        var sourceAggregatesBySeason = sourceAggregateRows.ToDictionary(x => x.SeasonId);
        var metricsBySeason = seasonRows.ToDictionary(
            x => x.SeasonId,
            x => BuildTvSeasonProjectionMetrics(x, episodeAggregatesBySeason, sourceAggregatesBySeason));
        var stateBySeason = stateRows
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt).First());

        var projectionStopwatch = Stopwatch.StartNew();
        var items = seriesRows
            .Select(
                series =>
                {
                    var seasons = seasonsBySeries.GetValueOrDefault(series.Id) ?? [];
                    var hasRecognizedSeason = seasons.Any(
                        season => season.IdentificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed);
                    var isUnknownSeriesProjection = includeUnknownSeriesAsOther
                                                    && !series.TmdbSeriesId.HasValue
                                                    && !hasRecognizedSeason
                                                    && seasons.Any(season => season.IdentificationStatus == IdentificationStatus.Failed);
                    var visibleSeasonCount = 0;
                    var visibleSeasonIds = new HashSet<int>();
                    var displaySeasonIds = new HashSet<int>();
                    var hasState = false;
                    foreach (var season in seasons)
                    {
                        stateBySeason.TryGetValue(season.SeasonId, out var state);
                        var metrics = metricsBySeason[season.SeasonId];
                        var watchedInSeason = metrics.WatchedEpisodeCount;
                        var seasonHasCurrentState = state?.HasUserState == true || watchedInSeason > 0;
                        var seasonHasActiveSource = metrics.SourceCount > 0;
                        var seasonInLibraryEpisodeCount = metrics.InLibraryEpisodeCount;
                        var visibilityState = state?.LibraryVisibilityState ?? LibraryVisibilityState.Auto;
                        hasState |= seasonHasCurrentState;
                        var shouldShowSeason = ShouldShowSeasonInSeriesOverviewProjection(
                            series.TmdbSeriesId,
                            season,
                            seasonInLibraryEpisodeCount,
                            visibilityState);
                        if (shouldShowSeason)
                        {
                            displaySeasonIds.Add(season.SeasonId);
                        }

                        if (shouldShowSeason
                            && ResolveIsVisibleInLibrary(
                                seasonHasActiveSource,
                                visibilityState,
                                seasonHasCurrentState))
                        {
                            visibleSeasonCount++;
                            visibleSeasonIds.Add(season.SeasonId);
                        }
                    }

                    var displaySeasons = seasons
                        .Where(season => displaySeasonIds.Contains(season.SeasonId))
                        .ToList();
                    var visibleMetrics = seasons
                        .Where(season => visibleSeasonIds.Contains(season.SeasonId))
                        .Select(season => metricsBySeason[season.SeasonId])
                        .ToList();
                    var visibleCollectionStates = seasons
                        .Where(season => visibleSeasonIds.Contains(season.SeasonId))
                        .Select(season => stateBySeason.TryGetValue(season.SeasonId, out var state) ? state : null)
                        .Where(state => state is not null)
                        .ToList();
                    var displayMetrics = displaySeasons
                        .Select(season => metricsBySeason[season.SeasonId])
                        .ToList();
                    var sourceCount = visibleMetrics.Sum(x => x.SourceCount);
                    var hasActiveSource = sourceCount > 0;
                    var isVisibleInLibrary = visibleSeasonCount > 0
                                             && (hasRecognizedSeason || isUnknownSeriesProjection);
                    var inLibraryEpisodeCount = visibleMetrics.Sum(x => x.InLibraryEpisodeCount);
                    var watchedEpisodeCount = displayMetrics.Sum(x => x.WatchedEpisodeCount);
                    var hasWatchedEpisodeState = watchedEpisodeCount > 0;
                    var totalEpisodeCount = displayMetrics.Sum(x => x.TotalEpisodeCount);
                    var knownEpisodeCount = displayMetrics.Sum(x => x.CountableEpisodeCount);
                    var watchedSeasonCount = displayMetrics.Count(x => IsAggregateWatched(x.WatchedEpisodeCount, x.CountableEpisodeCount, x.TotalEpisodeCount));
                    var isWatched = IsAggregateWatched(watchedEpisodeCount, knownEpisodeCount, totalEpisodeCount);
                    var stateUpdatedAt = seasons
                        .Select(season => stateBySeason.TryGetValue(season.SeasonId, out var state) ? state.UpdatedAt : DateTime.MinValue)
                        .DefaultIfEmpty(DateTime.MinValue)
                        .Max();
                    var latestSourceUpdatedAt = displayMetrics
                        .Select(x => x.LatestSourceUpdatedAt)
                        .Where(x => x.HasValue)
                        .DefaultIfEmpty()
                        .Max();
                    var latestSeasonCreatedAt = displaySeasons
                        .Select(x => (DateTime?)x.CreatedAt)
                        .DefaultIfEmpty()
                        .Max();
                    var latestSeasonPoster = displaySeasons
                        .OrderByDescending(x => x.SeasonNumber)
                        .Select(x => x.PosterRemoteUrl)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                        ?? string.Empty;
                    var primaryRating = BuildMoviePrimaryRating(
                        series.TmdbRating,
                        series.TmdbVoteCount,
                        series.OmdbScoreValue,
                        series.OmdbScoreScale,
                        series.OmdbVoteCount);
                    return new LibraryMovieListItem
                    {
                        ItemKind = isUnknownSeriesProjection ? LibraryMediaItemKind.Other : LibraryMediaItemKind.Series,
                        SeriesId = series.Id,
                        TmdbId = series.TmdbSeriesId,
                        Title = series.Name,
                        OriginalTitle = series.OriginalName ?? string.Empty,
                        ReleaseYear = series.FirstAirYear,
                        ReleaseDate = series.FirstAirDate,
                        PosterRemoteUrl = FirstNonEmpty(series.PosterRemoteUrl, latestSeasonPoster),
                        GenresText = series.GenresText ?? string.Empty,
                        Overview = series.Overview ?? string.Empty,
                        Country = series.Country ?? string.Empty,
                        Language = series.Language ?? string.Empty,
                        DirectorText = series.DirectorText ?? string.Empty,
                        ActorsText = series.ActorsText ?? string.Empty,
                        IdentificationStatus = seasons.Any(x => x.IdentificationStatus == IdentificationStatus.Failed)
                            ? IdentificationStatus.Failed
                            : IdentificationStatus.Matched,
                        SourceCount = sourceCount,
                        ActiveSourceCount = sourceCount,
                        HasActiveSource = hasActiveSource,
                        HasLocalSource = visibleMetrics.Any(x => x.HasLocalSource),
                        HasWebDavSource = visibleMetrics.Any(x => x.HasWebDavSource),
                        IsVisibleInLibrary = isVisibleInLibrary,
                        LibraryVisibilityState = LibraryVisibilityState.Auto,
                        HasLibraryContext = isVisibleInLibrary,
                        HasUserState = hasState || hasWatchedEpisodeState,
                        IsInLibrary = hasActiveSource,
                        IsFavorite = visibleCollectionStates.Any(state => state!.IsFavorite),
                        IsWantToWatch = visibleCollectionStates.Any(state => state!.IsWantToWatch),
                        IsNotInterested = visibleCollectionStates.Any(state => state!.IsNotInterested),
                        IsWatched = isWatched,
                        PrimaryRatingSourceName = primaryRating.SourceName,
                        PrimaryRatingValue = primaryRating.Value,
                        PrimaryRatingScale = primaryRating.Scale,
                        PrimaryRatingVoteCount = primaryRating.VoteCount,
                        SeriesPrimaryRatingValue = primaryRating.Value,
                        TmdbRating = series.TmdbRating,
                        TmdbVoteCount = series.TmdbVoteCount,
                        OmdbScoreValue = series.OmdbScoreValue,
                        OmdbScoreScale = series.OmdbScoreScale,
                        OmdbVoteCount = series.OmdbVoteCount,
                        OmdbSourceUrl = series.OmdbSourceUrl,
                        OmdbLastUpdatedAt = series.OmdbLastUpdatedAt,
                        SeasonCount = displaySeasons.Count,
                        WatchedSeasonCount = watchedSeasonCount,
                        InLibraryEpisodeCount = inLibraryEpisodeCount,
                        WatchedEpisodeCount = watchedEpisodeCount,
                        TotalEpisodeCount = totalEpisodeCount,
                        ProgressPercent = ResolveEpisodeProgressPercent(watchedSeasonCount, displaySeasons.Count, isWatched),
                        UpdatedAt = MaxDate(stateUpdatedAt, latestSourceUpdatedAt, latestSeasonCreatedAt, series.CreatedAt)
                    };
                })
            .Where(x => x.IsVisibleInLibrary)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
        projectionStopwatch.Stop();
        totalStopwatch.Stop();
        WriteLibraryQueryPerfEvent(
            $"event=library-query-tv-series-completed includeUnknownSeriesAsOther={FormatBool(includeUnknownSeriesAsOther)} seriesRowsMs={seriesRowsStopwatch.ElapsedMilliseconds} seasonRowsMs={seasonRowsStopwatch.ElapsedMilliseconds} episodeAggregateRowsMs={episodeAggregateRowsStopwatch.ElapsedMilliseconds} sourceAggregateRowsMs={sourceAggregateRowsStopwatch.ElapsedMilliseconds} stateRowsMs={stateRowsStopwatch.ElapsedMilliseconds} projectionMs={projectionStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} seriesRows={seriesRows.Count} seasonRows={seasonRows.Count} episodeRows={episodeAggregateRows.Sum(x => x.EpisodeCount)} episodeAggregateRows={episodeAggregateRows.Count} sourceRows={sourceAggregateRows.Sum(x => x.SourceCount)} sourceAggregateRows={sourceAggregateRows.Count} stateRows={stateRows.Count} resultItems={items.Count}");
        return items;
    }

    private static async Task<IReadOnlyList<LibraryMovieListItem>> GetTvSeasonLibraryItemsAsync(
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var seasonRowsStopwatch = Stopwatch.StartNew();
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
                    DirectorText = x.Series.DirectorText ?? string.Empty,
                    ActorsText = x.Series.ActorsText ?? string.Empty,
                    SeriesTmdbRating = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    SeriesTmdbVoteCount = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "TMDB")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    SeriesOmdbScoreValue = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreValue)
                        .FirstOrDefault(),
                    SeriesOmdbScoreScale = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => (double?)rating.ScoreScale)
                        .FirstOrDefault(),
                    SeriesOmdbVoteCount = x.Series.RatingSources
                        .Where(rating => rating.SourceName == "OMDb")
                        .Select(rating => rating.VoteCount)
                        .FirstOrDefault(),
                    AirDate = x.AirDate,
                    AirYear = x.AirDate.HasValue ? x.AirDate.Value.Year : x.Series.FirstAirYear,
                    TotalEpisodeCount = x.TmdbEpisodeCount,
                    IdentificationStatus = x.IdentificationStatus,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
        seasonRowsStopwatch.Stop();
        var seasonIds = seasonRows.Select(x => x.SeasonId).ToArray();
        if (seasonIds.Length == 0)
        {
            totalStopwatch.Stop();
            WriteLibraryQueryPerfEvent(
                $"event=library-query-tv-season-completed seasonRowsMs={seasonRowsStopwatch.ElapsedMilliseconds} episodeAggregateRowsMs=0 sourceAggregateRowsMs=0 stateRowsMs=0 projectionMs=0 totalMs={totalStopwatch.ElapsedMilliseconds} seasonRows=0 episodeRows=0 episodeAggregateRows=0 sourceRows=0 sourceAggregateRows=0 stateRows=0 resultItems=0");
            return [];
        }

        var episodeAggregateRowsStopwatch = Stopwatch.StartNew();
        var episodeAggregateRows = await LoadTvEpisodeSeasonAggregateRowsAsync(dbContext, seasonIds, cancellationToken);
        episodeAggregateRowsStopwatch.Stop();
        var sourceAggregateRowsStopwatch = Stopwatch.StartNew();
        var sourceAggregateRows = await LoadTvSourceSeasonAggregateRowsAsync(dbContext, seasonIds, cancellationToken);
        sourceAggregateRowsStopwatch.Stop();
        var stateRowsStopwatch = Stopwatch.StartNew();
        var stateRows = await LoadTvCollectionStateRowsAsync(dbContext, seasonIds, cancellationToken);
        stateRowsStopwatch.Stop();
        var stateBySeason = stateRows
            .GroupBy(x => x.SeasonId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt).First());
        var episodeAggregatesBySeason = episodeAggregateRows.ToDictionary(x => x.SeasonId);
        var sourceAggregatesBySeason = sourceAggregateRows.ToDictionary(x => x.SeasonId);
        var metricsBySeason = seasonRows.ToDictionary(
            x => x.SeasonId,
            x => BuildTvSeasonProjectionMetrics(x, episodeAggregatesBySeason, sourceAggregatesBySeason));
        var projectionStopwatch = Stopwatch.StartNew();
        var items = seasonRows
            .Select(
                season =>
                {
                    stateBySeason.TryGetValue(season.SeasonId, out var state);
                    var metrics = metricsBySeason[season.SeasonId];
                    var countableEpisodeCount = metrics.CountableEpisodeCount;
                    var totalEpisodeCount = metrics.TotalEpisodeCount;
                    var watchedEpisodeCount = metrics.WatchedEpisodeCount;
                    var isWatched = IsAggregateWatched(watchedEpisodeCount, countableEpisodeCount, totalEpisodeCount);
                    var isUnwatched = !isWatched;
                    var hasUserState = state?.HasUserState == true || watchedEpisodeCount > 0;
                    var hasActiveSource = metrics.SourceCount > 0;
                    var visibilityState = state?.LibraryVisibilityState ?? LibraryVisibilityState.Auto;
                    var isVisibleInLibrary = ResolveIsVisibleInLibrary(hasActiveSource, visibilityState, hasUserState);
                    var primaryRating = BuildMoviePrimaryRating(
                        season.TmdbRating,
                        season.TmdbVoteCount,
                        season.OmdbScoreValue,
                        season.OmdbScoreScale,
                        season.OmdbVoteCount);
                    var seriesPrimaryRating = BuildMoviePrimaryRating(
                        season.SeriesTmdbRating,
                        season.SeriesTmdbVoteCount,
                        season.SeriesOmdbScoreValue,
                        season.SeriesOmdbScoreScale,
                        season.SeriesOmdbVoteCount);
                    return new LibraryMovieListItem
                    {
                        ItemKind = ResolveTvSeasonItemKind(season.IdentificationStatus),
                        SeriesId = season.SeriesId,
                        SeasonId = season.SeasonId,
                        SeasonNumber = season.SeasonNumber,
                        TmdbId = season.TmdbSeriesId,
                        Title = BuildSeasonTitle(season.SeriesName, season.Name, season.SeasonNumber),
                        SeriesTitle = season.SeriesName,
                        OriginalTitle = season.OriginalSeriesName,
                        ReleaseYear = season.AirYear,
                        ReleaseDate = season.AirDate,
                        PosterRemoteUrl = FirstNonEmpty(season.PosterRemoteUrl, season.SeriesPosterRemoteUrl),
                        GenresText = season.GenresText,
                        Overview = season.Overview,
                        Country = season.Country,
                        Language = season.Language,
                        DirectorText = season.DirectorText,
                        ActorsText = season.ActorsText,
                        IdentificationStatus = season.IdentificationStatus,
                        SourceCount = metrics.SourceCount,
                        ActiveSourceCount = metrics.SourceCount,
                        HasActiveSource = hasActiveSource,
                        HasLocalSource = metrics.HasLocalSource,
                        HasWebDavSource = metrics.HasWebDavSource,
                        IsVisibleInLibrary = isVisibleInLibrary,
                        LibraryVisibilityState = visibilityState,
                        HasLibraryContext = isVisibleInLibrary,
                        HasUserState = hasUserState,
                        IsInLibrary = hasActiveSource,
                        IsFavorite = state?.IsFavorite == true && isWatched,
                        IsWantToWatch = state?.IsWantToWatch == true && isUnwatched,
                        IsNotInterested = state?.IsNotInterested == true,
                        IsWatched = isWatched,
                        PrimaryRatingSourceName = primaryRating.SourceName,
                        PrimaryRatingValue = primaryRating.Value,
                        PrimaryRatingScale = primaryRating.Scale,
                        PrimaryRatingVoteCount = primaryRating.VoteCount,
                        SeriesPrimaryRatingValue = seriesPrimaryRating.Value,
                        TmdbRating = season.TmdbRating,
                        TmdbVoteCount = season.TmdbVoteCount,
                        OmdbScoreValue = season.OmdbScoreValue,
                        OmdbScoreScale = season.OmdbScoreScale,
                        OmdbVoteCount = season.OmdbVoteCount,
                        OmdbSourceUrl = season.OmdbSourceUrl,
                        OmdbLastUpdatedAt = season.OmdbLastUpdatedAt,
                        WatchedEpisodeCount = watchedEpisodeCount,
                        InLibraryEpisodeCount = metrics.InLibraryEpisodeCount,
                        TotalEpisodeCount = totalEpisodeCount,
                        ProgressPercent = ResolveEpisodeProgressPercent(watchedEpisodeCount, totalEpisodeCount, isWatched),
                        UpdatedAt = MaxDate(state?.UpdatedAt, metrics.LatestSourceUpdatedAt, season.CreatedAt)
                    };
                })
            .Where(x => x.IsVisibleInLibrary)
            .Where(ShouldShowTvSeasonLibraryItem)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
        projectionStopwatch.Stop();
        totalStopwatch.Stop();
        WriteLibraryQueryPerfEvent(
            $"event=library-query-tv-season-completed seasonRowsMs={seasonRowsStopwatch.ElapsedMilliseconds} episodeAggregateRowsMs={episodeAggregateRowsStopwatch.ElapsedMilliseconds} sourceAggregateRowsMs={sourceAggregateRowsStopwatch.ElapsedMilliseconds} stateRowsMs={stateRowsStopwatch.ElapsedMilliseconds} projectionMs={projectionStopwatch.ElapsedMilliseconds} totalMs={totalStopwatch.ElapsedMilliseconds} seasonRows={seasonRows.Count} episodeRows={episodeAggregateRows.Sum(x => x.EpisodeCount)} episodeAggregateRows={episodeAggregateRows.Count} sourceRows={sourceAggregateRows.Sum(x => x.SourceCount)} sourceAggregateRows={sourceAggregateRows.Count} stateRows={stateRows.Count} resultItems={items.Count}");
        return items;
    }

    private static async Task<IReadOnlyList<TvEpisodeSeasonAggregateRow>> LoadTvEpisodeSeasonAggregateRowsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> seasonIds,
        CancellationToken cancellationToken)
    {
        if (seasonIds.Count == 0)
        {
            return [];
        }

        return await dbContext.TvEpisodes
            .AsNoTracking()
            .Where(x => seasonIds.Contains(x.TvSeasonId))
            .GroupBy(x => x.TvSeasonId)
            .Select(
                group => new TvEpisodeSeasonAggregateRow
                {
                    SeasonId = group.Key,
                    EpisodeCount = group.Count(),
                    WatchedEpisodeCount = group.Count(x => x.IsWatched)
                })
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<TvSourceSeasonAggregateRow>> LoadTvSourceSeasonAggregateRowsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> seasonIds,
        CancellationToken cancellationToken)
    {
        if (seasonIds.Count == 0)
        {
            return [];
        }

        var sourceRows = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => x.EpisodeId.HasValue
                     && x.Episode != null
                     && seasonIds.Contains(x.Episode.TvSeasonId)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted)
            .Select(
                x => new TvSourceSeasonFlatRow
                {
                    SeasonId = x.Episode!.TvSeasonId,
                    EpisodeId = x.EpisodeId!.Value,
                    IsWatched = x.Episode.IsWatched,
                    UpdatedAt = x.UpdatedAt,
                    ProtocolType = x.SourceConnection == null ? null : x.SourceConnection.ProtocolType
                })
            .ToListAsync(cancellationToken);

        return sourceRows
            .GroupBy(x => x.SeasonId)
            .Select(
                group => new TvSourceSeasonAggregateRow
                {
                    SeasonId = group.Key,
                    SourceCount = group.Count(),
                    InLibraryEpisodeCount = group.Select(x => x.EpisodeId).Distinct().Count(),
                    WatchedSourceEpisodeCount = group
                        .Where(x => x.IsWatched)
                        .Select(x => x.EpisodeId)
                        .Distinct()
                        .Count(),
                    LatestSourceUpdatedAt = group.Max(x => x.UpdatedAt),
                    HasLocalSource = group.Any(x => x.ProtocolType == ProtocolType.Local),
                    HasWebDavSource = group.Any(x => x.ProtocolType == ProtocolType.WebDav)
                })
            .ToList();
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

        var collectionRows = await dbContext.UserTvSeasonCollectionItems
            .AsNoTracking()
            .Where(
                x => x.TvSeasonId.HasValue
                     && seasonIds.Contains(x.TvSeasonId.Value)
                     && (x.IsFavorite
                         || x.IsWantToWatch
                         || x.IsNotInterested
                         || x.LibraryVisibilityState != LibraryVisibilityState.Auto))
            .Select(
                x => new TvSeasonStateLibraryRow
                {
                    SeasonId = x.TvSeasonId!.Value,
                    SeriesId = x.TvSeriesId,
                    IsFavorite = x.IsFavorite,
                    IsWantToWatch = x.IsWantToWatch,
                    IsNotInterested = x.IsNotInterested,
                    LibraryVisibilityState = x.LibraryVisibilityState,
                    HasUserState = x.IsFavorite || x.IsWantToWatch || x.IsNotInterested,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        return collectionRows
            .GroupBy(x => x.SeasonId)
            .Select(
                group => new TvSeasonStateLibraryRow
                {
                    SeasonId = group.Key,
                    SeriesId = group.Where(x => x.SeriesId.HasValue).Select(x => x.SeriesId).FirstOrDefault(),
                    IsFavorite = group.Any(x => x.IsFavorite),
                    IsWantToWatch = group.Any(x => x.IsWantToWatch),
                    IsNotInterested = group.Any(x => x.IsNotInterested),
                    LibraryVisibilityState = ResolveLibraryVisibilityState(group),
                    HasUserState = group.Any(x => x.HasUserState),
                    UpdatedAt = group.Max(x => x.UpdatedAt)
                })
            .ToList();
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

    private static DateTime MaxDate(params DateTime?[] values)
    {
        return values
            .Where(x => x.HasValue && x.Value != default)
            .Select(x => x!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
    }

    private static bool IsAggregateWatched(int watchedEpisodeCount, int knownEpisodeCount, int totalEpisodeCount)
    {
        if (totalEpisodeCount <= 0)
        {
            return knownEpisodeCount > 0 && watchedEpisodeCount >= knownEpisodeCount;
        }

        return knownEpisodeCount >= totalEpisodeCount && watchedEpisodeCount >= totalEpisodeCount;
    }

    private static int? ResolveRuntimeDurationSeconds(int? runtimeMinutes)
    {
        return runtimeMinutes is > 0 ? runtimeMinutes.Value * 60 : null;
    }

    private static double? ResolvePlaybackProgressPercent(
        int positionSeconds,
        int? durationSeconds)
    {
        if (!durationSeconds.HasValue || durationSeconds.Value <= 0 || positionSeconds <= 0)
        {
            return 0d;
        }

        return Math.Clamp(Math.Round(positionSeconds * 100d / durationSeconds.Value, 1), 0d, 100d);
    }

    private static double? ResolveEpisodeProgressPercent(
        int watchedEpisodeCount,
        int totalEpisodeCount,
        bool isCompleted)
    {
        if (isCompleted)
        {
            return 100d;
        }

        if (totalEpisodeCount <= 0 || watchedEpisodeCount <= 0)
        {
            return 0d;
        }

        return Math.Clamp(Math.Round(watchedEpisodeCount * 100d / totalEpisodeCount, 1), 0d, 100d);
    }

    private static int ResolveSeasonProgressTotalEpisodeCount(TvSeasonLibraryRow season, int countableEpisodeCount)
    {
        if (IsNoTmdbFailedUnknownSeason(season))
        {
            return countableEpisodeCount;
        }

        return season.TotalEpisodeCount.GetValueOrDefault() > 0
            ? season.TotalEpisodeCount!.Value
            : countableEpisodeCount;
    }

    private static TvSeasonProjectionMetrics BuildTvSeasonProjectionMetrics(
        TvSeasonLibraryRow season,
        IReadOnlyDictionary<int, TvEpisodeSeasonAggregateRow> episodeAggregatesBySeason,
        IReadOnlyDictionary<int, TvSourceSeasonAggregateRow> sourceAggregatesBySeason)
    {
        episodeAggregatesBySeason.TryGetValue(season.SeasonId, out var episodeAggregate);
        sourceAggregatesBySeason.TryGetValue(season.SeasonId, out var sourceAggregate);
        var countableEpisodeCount = IsNoTmdbFailedUnknownSeason(season)
            ? sourceAggregate?.InLibraryEpisodeCount ?? 0
            : episodeAggregate?.EpisodeCount ?? 0;
        var watchedEpisodeCount = IsNoTmdbFailedUnknownSeason(season)
            ? sourceAggregate?.WatchedSourceEpisodeCount ?? 0
            : episodeAggregate?.WatchedEpisodeCount ?? 0;
        var totalEpisodeCount = ResolveSeasonProgressTotalEpisodeCount(season, countableEpisodeCount);

        return new TvSeasonProjectionMetrics(
            countableEpisodeCount,
            watchedEpisodeCount,
            totalEpisodeCount,
            sourceAggregate?.SourceCount ?? 0,
            sourceAggregate?.InLibraryEpisodeCount ?? 0,
            sourceAggregate?.LatestSourceUpdatedAt,
            sourceAggregate?.HasLocalSource == true,
            sourceAggregate?.HasWebDavSource == true);
    }

    private static bool ShouldShowTvSeasonLibraryItem(LibraryMovieListItem item)
    {
        return item.TmdbId.HasValue
               || item.IdentificationStatus != IdentificationStatus.Failed
               || item.InLibraryEpisodeCount > 0;
    }

    private static bool ShouldShowSeasonInSeriesOverviewProjection(
        int? tmdbSeriesId,
        TvSeasonLibraryRow season,
        int inLibraryEpisodeCount,
        LibraryVisibilityState libraryVisibilityState)
    {
        if (tmdbSeriesId.HasValue
            && !season.TmdbSeasonId.HasValue
            && inLibraryEpisodeCount <= 0
            && libraryVisibilityState != LibraryVisibilityState.Visible)
        {
            return false;
        }

        if (IsNoTmdbFailedUnknownSeason(season)
            && inLibraryEpisodeCount <= 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsNoTmdbFailedUnknownSeason(TvSeasonLibraryRow season)
    {
        return season.TmdbSeriesId is null && season.IdentificationStatus == IdentificationStatus.Failed;
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

    private static LibraryVisibilityState ResolveLibraryVisibilityState(IEnumerable<LibraryCollectionState> states)
    {
        return states
            .OrderByDescending(x => x.LibraryVisibilityState != LibraryVisibilityState.Auto)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => x.LibraryVisibilityState)
            .FirstOrDefault();
    }

    private static LibraryVisibilityState ResolveLibraryVisibilityState(IEnumerable<TvSeasonStateLibraryRow> states)
    {
        return states
            .OrderByDescending(x => x.LibraryVisibilityState != LibraryVisibilityState.Auto)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => x.LibraryVisibilityState)
            .FirstOrDefault();
    }

    private static bool MatchesCollectionIdentity(
        LibraryCollectionIdentity identity,
        int? movieId,
        int? tmdbId,
        string? imdbId,
        string title,
        int? releaseYear)
    {
        if (identity.MovieId is > 0 && movieId == identity.MovieId)
        {
            return true;
        }

        if (identity.TmdbId is > 0 && tmdbId == identity.TmdbId)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(identity.ImdbId)
            && !string.IsNullOrWhiteSpace(imdbId)
            && string.Equals(NormalizeImdbId(identity.ImdbId), NormalizeImdbId(imdbId), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(identity.Title)
               && !string.IsNullOrWhiteSpace(title)
               && identity.ReleaseYear.HasValue
               && releaseYear.HasValue
               && string.Equals(BuildTitleYearKey(identity.Title, identity.ReleaseYear), BuildTitleYearKey(title, releaseYear), StringComparison.OrdinalIgnoreCase);
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
        bool IsNotInterested,
        LibraryVisibilityState LibraryVisibilityState,
        DateTime UpdatedAt)
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

        public int? TmdbSeasonId { get; set; }

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

        public string DirectorText { get; set; } = string.Empty;

        public string ActorsText { get; set; } = string.Empty;

        public double? TmdbRating { get; set; }

        public int? TmdbVoteCount { get; set; }

        public double? OmdbScoreValue { get; set; }

        public double? OmdbScoreScale { get; set; }

        public int? OmdbVoteCount { get; set; }

        public double? SeriesTmdbRating { get; set; }

        public int? SeriesTmdbVoteCount { get; set; }

        public double? SeriesOmdbScoreValue { get; set; }

        public double? SeriesOmdbScoreScale { get; set; }

        public int? SeriesOmdbVoteCount { get; set; }

        public string OmdbSourceUrl { get; set; } = string.Empty;

        public DateTime? OmdbLastUpdatedAt { get; set; }

        public DateTime? AirDate { get; set; }

        public int? AirYear { get; set; }

        public int? TotalEpisodeCount { get; set; }

        public IdentificationStatus IdentificationStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    private sealed class TvEpisodeSeasonAggregateRow
    {
        public int SeasonId { get; set; }

        public int EpisodeCount { get; set; }

        public int WatchedEpisodeCount { get; set; }
    }

    private sealed class TvSourceSeasonAggregateRow
    {
        public int SeasonId { get; set; }

        public int SourceCount { get; set; }

        public int InLibraryEpisodeCount { get; set; }

        public int WatchedSourceEpisodeCount { get; set; }

        public DateTime? LatestSourceUpdatedAt { get; set; }

        public bool HasLocalSource { get; set; }

        public bool HasWebDavSource { get; set; }
    }

    private sealed class TvSourceSeasonFlatRow
    {
        public int SeasonId { get; set; }

        public int EpisodeId { get; set; }

        public bool IsWatched { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ProtocolType? ProtocolType { get; set; }
    }

    private readonly record struct TvSeasonProjectionMetrics(
        int CountableEpisodeCount,
        int WatchedEpisodeCount,
        int TotalEpisodeCount,
        int SourceCount,
        int InLibraryEpisodeCount,
        DateTime? LatestSourceUpdatedAt,
        bool HasLocalSource,
        bool HasWebDavSource);

    private sealed class TvSeasonStateLibraryRow
    {
        public int SeasonId { get; set; }

        public int? SeriesId { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsWantToWatch { get; set; }

        public bool IsNotInterested { get; set; }

        public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

        public bool HasUserState { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

}
