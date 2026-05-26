using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class MovieManagementService : IMovieManagementService
{
    private const int MovieTitleMaxLength = 300;

    public async Task SetDefaultMediaFileAsync(
        int movieId,
        int mediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var mediaFile = await dbContext.MediaFiles
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MovieId == movieId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("默认播放源必须是该影片下的有效视频资源。");

        movie.DefaultMediaFileId = mediaFile.Id;
        movie.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> EnsureUnidentifiedMoviePlaceholderForMediaFileAsync(
        int mediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var mediaFile = await dbContext.MediaFiles
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("未识别文件不存在，或已不在媒体库中。");

        if (mediaFile.MovieId.HasValue)
        {
            return mediaFile.MovieId.Value;
        }

        if (mediaFile.EpisodeId.HasValue)
        {
            throw new InvalidOperationException("该文件已属于电视剧集，不能作为未识别电影详情打开。");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var parsedName = MovieFileNameParser.Parse(mediaFile.FileName);
        var now = DateTime.UtcNow;
        var placeholderMovie = new Movie
        {
            Title = BuildUnidentifiedMovieTitle(mediaFile.FileName),
            ReleaseYear = parsedName.ReleaseYear,
            IdentificationStatus = IdentificationStatus.Failed,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Movies.Add(placeholderMovie);
        await dbContext.SaveChangesAsync(cancellationToken);

        mediaFile.MovieId = placeholderMovie.Id;
        mediaFile.UpdatedAt = now;
        placeholderMovie.DefaultMediaFileId = mediaFile.Id;
        placeholderMovie.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return placeholderMovie.Id;
    }

    public async Task SetFavoriteAsync(
        int movieId,
        bool isFavorite,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        if (isFavorite && !movie.IsWatched)
        {
            throw new InvalidOperationException("只有已看影片可以标记喜爱。");
        }

        var now = DateTime.UtcNow;
        var oldFavorite = movie.IsFavorite;
        movie.IsFavorite = isFavorite;
        movie.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            movie.TmdbId,
            movie.Id,
            collectionItemId: null,
            movie.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldFavorite,
            movie.IsFavorite,
            changeSource,
            now);

        if (isFavorite)
        {
            var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
            foreach (var item in collectionItems)
            {
                var oldNotInterested = item.IsNotInterested;
                item.IsNotInterested = false;
                RestoreAutoVisibilityForPositiveState(item, isPositiveState: true);
                item.UpdatedAt = now;
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    item.TmdbId ?? movie.TmdbId,
                    movie.Id,
                    item.Id == 0 ? null : item.Id,
                    item.Title,
                    UserMovieStateChangeHistoryRecorder.StateNotInterested,
                    oldNotInterested,
                    item.IsNotInterested,
                    changeSource,
                    now);
                CleanupCollectionEntityIfEmpty(dbContext, item);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetWatchedAsync(
        int movieId,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var now = DateTime.UtcNow;
        var oldMovieWatched = movie.IsWatched;
        var oldMovieFavorite = movie.IsFavorite;
        movie.IsWatched = isWatched;
        if (!isWatched)
        {
            movie.IsFavorite = false;
            movie.AutoWatchedBaselineAtUtc = now;
            WatchCompletionDiagnostics.Write(
                $"watch-completion-baseline-set movieId={movie.Id} baselineUtc={movie.AutoWatchedBaselineAtUtc:O}");
        }

        movie.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            movie.TmdbId,
            movie.Id,
            collectionItemId: null,
            movie.Title,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            oldMovieWatched,
            movie.IsWatched,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            movie.TmdbId,
            movie.Id,
            collectionItemId: null,
            movie.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldMovieFavorite,
            movie.IsFavorite,
            changeSource,
            now);

        if (isWatched)
        {
            var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
            foreach (var item in collectionItems.Where(x => x.IsWantToWatch || x.IsWatched != isWatched || x.LibraryVisibilityState == LibraryVisibilityState.Hidden))
            {
                var oldWantToWatch = item.IsWantToWatch;
                var oldWatched = item.IsWatched;
                item.IsWantToWatch = false;
                item.IsWatched = true;
                RestoreAutoVisibilityForPositiveState(item, isPositiveState: true);
                item.UpdatedAt = now;
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    item.TmdbId ?? movie.TmdbId,
                    movie.Id,
                    item.Id == 0 ? null : item.Id,
                    item.Title,
                    UserMovieStateChangeHistoryRecorder.StateWantToWatch,
                    oldWantToWatch,
                    item.IsWantToWatch,
                    changeSource,
                    now);
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    item.TmdbId ?? movie.TmdbId,
                    movie.Id,
                    item.Id == 0 ? null : item.Id,
                    item.Title,
                    UserMovieStateChangeHistoryRecorder.StateWatched,
                    oldWatched,
                    item.IsWatched,
                    changeSource,
                    now);
            }
        }
        else
        {
            var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
            foreach (var item in collectionItems.Where(x => x.IsWatched))
            {
                var oldWatched = item.IsWatched;
                item.IsWatched = false;
                item.UpdatedAt = now;
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    item.TmdbId ?? movie.TmdbId,
                    movie.Id,
                    item.Id == 0 ? null : item.Id,
                    item.Title,
                    UserMovieStateChangeHistoryRecorder.StateWatched,
                    oldWatched,
                    item.IsWatched,
                    changeSource,
                    now);
                CleanupCollectionEntityIfEmpty(dbContext, item);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFromLibraryAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies
            .Include(x => x.RatingSources)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var now = DateTime.UtcNow;
        var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
        var hasWatchHistory = await dbContext.WatchHistories
            .AsNoTracking()
            .AnyAsync(x => x.MovieId == movieId, cancellationToken);

        PreserveRemovedLibraryState(dbContext, movie, collectionItems, hasWatchHistory, now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddToLibraryAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("褰辩墖涓嶅瓨鍦ㄣ€?");

        var now = DateTime.UtcNow;
        var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
        var entity = collectionItems
            .OrderByDescending(x => x.MovieId == movie.Id)
            .ThenByDescending(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now,
                IsWantToWatch = false,
                IsWatched = false,
                IsNotInterested = false
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        ApplyRemovedLibrarySnapshot(entity, movie);
        entity.IsInLibrary = movie.MediaFiles.Any(x => !x.IsDeleted && x.MediaType == MediaType.Video);
        entity.LibraryVisibilityState = LibraryVisibilityState.Visible;
        entity.UpdatedAt = now;

        foreach (var item in collectionItems.Where(x => x.Id != entity.Id && x.LibraryVisibilityState == LibraryVisibilityState.Hidden))
        {
            item.LibraryVisibilityState = LibraryVisibilityState.Visible;
            item.UpdatedAt = now;
            CleanupCollectionEntityIfEmpty(dbContext, item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreToLibraryAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("褰辩墖涓嶅瓨鍦ㄣ€?");

        var now = DateTime.UtcNow;
        var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
        var entity = collectionItems
            .OrderByDescending(x => x.MovieId == movie.Id)
            .ThenByDescending(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now,
                IsWantToWatch = false
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        var hasActiveSource = movie.MediaFiles.Any(x => !x.IsDeleted && x.MediaType == MediaType.Video);
        var hasCurrentState = movie.IsFavorite
                              || movie.IsWatched
                              || movie.UserRating.HasValue
                              || collectionItems.Any(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested);
        var restoredVisibilityState = ResolveRestoredVisibilityState(hasActiveSource, hasCurrentState);

        ApplyRemovedLibrarySnapshot(entity, movie);
        entity.IsInLibrary = hasActiveSource;
        entity.LibraryVisibilityState = restoredVisibilityState;
        entity.UpdatedAt = now;
        CleanupCollectionEntityIfEmpty(dbContext, entity);

        foreach (var item in collectionItems.Where(x => x.Id != entity.Id && x.LibraryVisibilityState == LibraryVisibilityState.Hidden))
        {
            item.LibraryVisibilityState = restoredVisibilityState;
            item.UpdatedAt = now;
            CleanupCollectionEntityIfEmpty(dbContext, item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveGroupedPlaceholderRangeFromLibraryAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var movieIds = await ResolveMovieIdsForMediaFilesAsync(mediaFileIds, cancellationToken);
        foreach (var movieId in movieIds)
        {
            await RemoveFromLibraryAsync(movieId, cancellationToken);
        }

        var orphanHideResult = await HideUnassociatedMediaFilesAsMoviePlaceholdersAsync(mediaFileIds, cancellationToken);
        AiPerfDiagnostics.WriteEvent(
            $"event=library-grouped-placeholder-remove-from-library mediaFiles={mediaFileIds.Count} movies={movieIds.Count} orphanMediaFiles={orphanHideResult.HiddenMediaFileCount} createdHiddenPlaceholders={orphanHideResult.CreatedPlaceholderCount} hideOnly=true");
    }

    public async Task RestoreGroupedPlaceholderRangeToLibraryAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var movieIds = await ResolveMovieIdsForMediaFilesAsync(mediaFileIds, cancellationToken);
        foreach (var movieId in movieIds)
        {
            await RestoreToLibraryAsync(movieId, cancellationToken);
        }

        AiPerfDiagnostics.WriteEvent(
            $"event=library-grouped-placeholder-restore-to-library mediaFiles={mediaFileIds.Count} movies={movieIds.Count} orphanMediaFiles=0");
    }

    public async Task SetGroupedPlaceholderRangeWatchedAsync(
        IReadOnlyCollection<int> mediaFileIds,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        var movieIds = await ResolveMovieIdsForMediaFilesAsync(mediaFileIds, cancellationToken);
        foreach (var movieId in movieIds)
        {
            await SetWatchedAsync(movieId, isWatched, cancellationToken, changeSource);
        }

        AiPerfDiagnostics.WriteEvent(
            $"event=library-grouped-placeholder-set-watched mediaFiles={mediaFileIds.Count} movies={movieIds.Count} isWatched={isWatched.ToString().ToLowerInvariant()}");
    }

    private static void PreserveRemovedLibraryState(
        AppDbContext dbContext,
        Movie movie,
        IReadOnlyCollection<UserMovieCollectionItem> collectionItems,
        bool hasWatchHistory,
        DateTime now)
    {
        var collectionWatched = collectionItems.Any(x => x.IsWatched);
        var collectionWantToWatch = collectionItems.Any(x => x.IsWantToWatch);
        var collectionNotInterested = collectionItems.Any(x => x.IsNotInterested);
        var entity = collectionItems
            .OrderByDescending(x => x.MovieId == movie.Id)
            .ThenByDescending(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested || x.LibraryVisibilityState != LibraryVisibilityState.Auto)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();
        var collectionCreated = entity is null;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        ApplyRemovedLibrarySnapshot(entity, movie);
        entity.IsInLibrary = false;
        entity.LibraryVisibilityState = LibraryVisibilityState.Hidden;
        entity.IsWatched = movie.IsWatched || collectionWatched;
        entity.IsWantToWatch = !entity.IsWatched && collectionWantToWatch;
        entity.IsNotInterested = collectionNotInterested;
        entity.UpdatedAt = now;

        foreach (var item in collectionItems.Where(x => x.Id != entity.Id))
        {
            item.IsInLibrary = false;
            item.LibraryVisibilityState = LibraryVisibilityState.Hidden;
            item.UpdatedAt = now;
            CleanupCollectionEntityIfEmpty(dbContext, item);
        }

        AiPerfDiagnostics.WriteEvent(
            $"event=library-remove-from-library-preserve-state movieId={movie.Id} collectionCreated={collectionCreated} "
            + $"watched={entity.IsWatched} favorite={movie.IsFavorite} want={entity.IsWantToWatch} "
            + $"notInterested={entity.IsNotInterested} hasHistory={hasWatchHistory} visibility=hidden");
        AiPerfDiagnostics.WriteEvent(
            $"event=library-remove-from-library-state-visible movieId={movie.Id} isInLibrary=false "
            + $"visibleInLibrary=false visibleInCollection={(movie.IsFavorite || entity.IsWantToWatch)}");
    }

    private static void ApplyRemovedLibrarySnapshot(UserMovieCollectionItem entity, Movie movie)
    {
        var tmdbRating = movie.RatingSources
            .FirstOrDefault(x => string.Equals(x.SourceName, "TMDB", StringComparison.OrdinalIgnoreCase));
        var omdbRating = movie.RatingSources
            .FirstOrDefault(x => string.Equals(x.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase));

        entity.MovieId = movie.Id;
        entity.TmdbId = movie.TmdbId;
        entity.Title = movie.Title;
        entity.OriginalTitle = movie.OriginalTitle ?? string.Empty;
        entity.ReleaseYear = movie.ReleaseYear;
        entity.PosterRemoteUrl = movie.PosterRemoteUrl ?? string.Empty;
        entity.Overview = movie.Overview ?? string.Empty;
        entity.GenresText = movie.GenresText ?? string.Empty;
        entity.Country = movie.Country ?? string.Empty;
        entity.Language = movie.Language ?? string.Empty;
        entity.RuntimeMinutes = movie.RuntimeMinutes;
        entity.ImdbId = movie.ImdbId ?? string.Empty;
        entity.TmdbRating = tmdbRating?.ScoreValue;
        entity.TmdbVoteCount = tmdbRating?.VoteCount;
        entity.OmdbScoreValue = omdbRating?.ScoreValue;
        entity.OmdbScoreScale = omdbRating?.ScoreScale;
        entity.OmdbVoteCount = omdbRating?.VoteCount;
        entity.OmdbSourceUrl = omdbRating?.SourceUrl ?? string.Empty;
        entity.OmdbLastUpdatedAt = omdbRating?.LastUpdatedAt;
    }

    public async Task DeleteMovieRecordAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var now = DateTime.UtcNow;
        var mediaFileIds = await dbContext.MediaFiles
            .Where(x => x.MovieId == movieId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var mediaFileIdSet = mediaFileIds.ToHashSet();

        var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
        var collectionItemIds = collectionItems.Select(x => x.Id).ToHashSet();
        var stateHistories = await dbContext.UserMovieStateChangeHistories
            .Where(x => x.MovieId == movieId
                || (x.UserMovieCollectionItemId.HasValue
                    && collectionItemIds.Contains(x.UserMovieCollectionItemId.Value)))
            .ToListAsync(cancellationToken);
        if (stateHistories.Count > 0)
        {
            dbContext.UserMovieStateChangeHistories.RemoveRange(stateHistories);
        }

        if (collectionItems.Count > 0)
        {
            dbContext.UserMovieCollectionItems.RemoveRange(collectionItems);
        }

        var watchHistories = await dbContext.WatchHistories
            .Where(x => x.MovieId == movieId)
            .ToListAsync(cancellationToken);
        if (watchHistories.Count > 0)
        {
            dbContext.WatchHistories.RemoveRange(watchHistories);
        }

        var ratingSources = await dbContext.RatingSources
            .Where(x => x.MovieId == movieId)
            .ToListAsync(cancellationToken);
        if (ratingSources.Count > 0)
        {
            dbContext.RatingSources.RemoveRange(ratingSources);
        }

        if (mediaFileIdSet.Count > 0)
        {
            var subtitleBindings = await dbContext.SubtitleBindings
                .Where(x => mediaFileIdSet.Contains(x.MediaFileId)
                            || mediaFileIdSet.Contains(x.SubtitleMediaFileId))
                .ToListAsync(cancellationToken);
            if (subtitleBindings.Count > 0)
            {
                dbContext.SubtitleBindings.RemoveRange(subtitleBindings);
            }

            var defaultOwners = await dbContext.Movies
                .Where(x => x.DefaultMediaFileId.HasValue
                            && mediaFileIdSet.Contains(x.DefaultMediaFileId.Value))
                .ToListAsync(cancellationToken);
            foreach (var defaultOwner in defaultOwners)
            {
                defaultOwner.DefaultMediaFileId = null;
                defaultOwner.UpdatedAt = now;
            }
        }

        var onlineSubtitleBindings = await dbContext.OnlineSubtitleBindings
            .Where(x => !x.IsDeleted
                        && (x.MovieId == movieId
                            || (x.MediaFileId.HasValue && mediaFileIdSet.Contains(x.MediaFileId.Value))))
            .ToListAsync(cancellationToken);
        foreach (var binding in onlineSubtitleBindings)
        {
            binding.IsDeleted = true;
            binding.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (mediaFileIdSet.Count > 0)
        {
            var retainedMediaFileIds = await dbContext.WatchHistories
                .Where(x => mediaFileIdSet.Contains(x.MediaFileId))
                .Select(x => x.MediaFileId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var retainedMediaFileIdSet = retainedMediaFileIds.ToHashSet();
            var mediaFiles = await dbContext.MediaFiles
                .Where(x => mediaFileIdSet.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var mediaFile in mediaFiles)
            {
                if (retainedMediaFileIdSet.Contains(mediaFile.Id))
                {
                    mediaFile.MovieId = null;
                    mediaFile.IsDeleted = true;
                    mediaFile.UpdatedAt = now;
                    continue;
                }

                dbContext.MediaFiles.Remove(mediaFile);
            }
        }

        dbContext.Movies.Remove(movie);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteGroupedPlaceholderRangeRecordAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var movieIds = await ResolveMovieIdsForMediaFilesAsync(mediaFileIds, cancellationToken);
        foreach (var movieId in movieIds)
        {
            await DeleteMovieRecordAsync(movieId, cancellationToken);
        }

        var orphanMediaFiles = await DeleteUnassociatedMediaFileRecordsAsync(mediaFileIds, cancellationToken);
        AiPerfDiagnostics.WriteEvent(
            $"event=library-grouped-placeholder-delete-record mediaFiles={mediaFileIds.Count} movies={movieIds.Count} orphanMediaFiles={orphanMediaFiles}");
    }

    public async Task<ResetSourceResult> ResetMediaFileToUnidentifiedAsync(
        int movieId,
        int mediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var mediaFile = await dbContext.MediaFiles
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MovieId == movieId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("要重置的播放源不存在，或不属于当前影片。");

        var parsedName = MovieFileNameParser.Parse(mediaFile.FileName);
        if (movie.IdentificationStatus == IdentificationStatus.Failed)
        {
            throw new InvalidOperationException("该播放源已在未识别承接中，无需重复重置。");
        }

        var placeholderTitle = BuildUnidentifiedMovieTitle(mediaFile.FileName);
        var now = DateTime.UtcNow;
        var remainingSourceCount = movie.MediaFiles
            .Count(x => x.Id != mediaFile.Id && x.MediaType == MediaType.Video && !x.IsDeleted);
        var nextDefaultMediaFileId = movie.MediaFiles
            .Where(x => x.Id != mediaFile.Id && x.MediaType == MediaType.Video && !x.IsDeleted)
            .OrderBy(x => x.FileName)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

        var staleDefaultOwners = await dbContext.Movies
            .Where(x => x.Id != movie.Id && x.DefaultMediaFileId == mediaFile.Id)
            .ToListAsync(cancellationToken);

        foreach (var staleDefaultOwner in staleDefaultOwners)
        {
            staleDefaultOwner.DefaultMediaFileId = null;
            staleDefaultOwner.UpdatedAt = now;
        }

        if (movie.DefaultMediaFileId == mediaFile.Id)
        {
            movie.DefaultMediaFileId = nextDefaultMediaFileId;
            movie.UpdatedAt = now;
        }

        if (staleDefaultOwners.Count > 0 || movie.DefaultMediaFileId == nextDefaultMediaFileId)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var placeholderMovie = new Movie
        {
            Title = placeholderTitle,
            ReleaseYear = parsedName.ReleaseYear,
            IdentificationStatus = IdentificationStatus.Failed,
            AiTagsText = null,
            EmotionTagsText = null,
            SceneTagsText = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Movies.Add(placeholderMovie);
        await dbContext.SaveChangesAsync(cancellationToken);

        mediaFile.MovieId = placeholderMovie.Id;
        mediaFile.UpdatedAt = now;

        movie.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        WatchCompletionDiagnostics.Write(
            $"media-identification-reset-boundary mediaFileId={mediaFile.Id} oldMovieId={movie.Id} "
            + $"newMovieId={placeholderMovie.Id} movedWatchHistory=false");
        WatchCompletionDiagnostics.Write(
            $"media-identification-reset-history-retained mediaFileId={mediaFile.Id} reason=split-from-current-movie");

        placeholderMovie.DefaultMediaFileId = mediaFile.Id;
        placeholderMovie.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await CleanupMovieIfOrphanedAsync(dbContext, movie.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResetSourceResult
        {
            OriginalMovieId = movie.Id,
            PlaceholderMovieId = placeholderMovie.Id,
            RemainingLibrarySourceCount = remainingSourceCount,
            DetailMovieId = remainingSourceCount > 0 ? movie.Id : placeholderMovie.Id
        };
    }

    private static string BuildUnidentifiedMovieTitle(string fileName)
    {
        var title = string.IsNullOrWhiteSpace(fileName)
            ? "-"
            : fileName.Trim();
        return title.Length <= MovieTitleMaxLength ? title : title[..MovieTitleMaxLength];
    }

    private static async Task CleanupMovieIfOrphanedAsync(
        AppDbContext dbContext,
        int movieId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);

        if (movie is null)
        {
            return;
        }

        var isFailedPlaceholder = !movie.TmdbId.HasValue
                                  && movie.IdentificationStatus == IdentificationStatus.Failed;

        if (isFailedPlaceholder
            && movie.MediaFiles.Count == 0
            && movie.WatchHistories.Count == 0
            && !movie.IsFavorite
            && !movie.IsWatched)
        {
            if (movie.RatingSources.Count > 0)
            {
                dbContext.RatingSources.RemoveRange(movie.RatingSources);
            }

            dbContext.Movies.Remove(movie);
        }
    }

    private static async Task<List<UserMovieCollectionItem>> FindCollectionItemsForMovieAsync(
        AppDbContext dbContext,
        Movie movie,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.UserMovieCollectionItems
            .Where(x => x.MovieId == movie.Id
                        || (movie.TmdbId.HasValue && x.TmdbId == movie.TmdbId.Value)
                        || (!string.IsNullOrWhiteSpace(movie.ImdbId) && x.ImdbId == movie.ImdbId)
                        || (x.Title == movie.Title && x.ReleaseYear == movie.ReleaseYear))
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(x => x.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static async Task<IReadOnlyList<int>> ResolveMovieIdsForMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        return await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.MovieId.HasValue)
            .Select(x => x.MovieId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async Task<OrphanHideOnlyResult> HideUnassociatedMediaFilesAsMoviePlaceholdersAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return OrphanHideOnlyResult.Empty;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var mediaFiles = await dbContext.MediaFiles
            .Where(
                x => ids.Contains(x.Id)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.MovieId.HasValue
                     && !x.EpisodeId.HasValue)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (mediaFiles.Count == 0)
        {
            return OrphanHideOnlyResult.Empty;
        }

        var placeholders = new List<Movie>(mediaFiles.Count);
        foreach (var mediaFile in mediaFiles)
        {
            var parsedName = MovieFileNameParser.Parse(mediaFile.FileName);
            var placeholderMovie = new Movie
            {
                Title = BuildUnidentifiedMovieTitle(mediaFile.FileName),
                ReleaseYear = parsedName.ReleaseYear,
                IdentificationStatus = IdentificationStatus.Failed,
                CreatedAt = now,
                UpdatedAt = now
            };
            placeholders.Add(placeholderMovie);
            dbContext.Movies.Add(placeholderMovie);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < mediaFiles.Count; index++)
        {
            var mediaFile = mediaFiles[index];
            var placeholderMovie = placeholders[index];
            mediaFile.MovieId = placeholderMovie.Id;
            mediaFile.UpdatedAt = now;
            placeholderMovie.DefaultMediaFileId = mediaFile.Id;
            placeholderMovie.UpdatedAt = now;
            PreserveRemovedLibraryState(dbContext, placeholderMovie, Array.Empty<UserMovieCollectionItem>(), false, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        AiPerfDiagnostics.WriteEvent(
            $"event=orphan-remove-hide-only hiddenOrphanCount={mediaFiles.Count} createdPlaceholderCount={placeholders.Count} mediaFileIsDeleted=false");
        AiPerfDiagnostics.WriteEvent(
            $"event=orphan-remove-created-hidden-placeholder hiddenOrphanCount={mediaFiles.Count} createdPlaceholderCount={placeholders.Count}");
        return new OrphanHideOnlyResult(mediaFiles.Count, placeholders.Count);
    }

    private static async Task<int> DeleteUnassociatedMediaFileRecordsAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var now = DateTime.UtcNow;
        var mediaFiles = await dbContext.MediaFiles
            .Where(
                x => ids.Contains(x.Id)
                     && x.MediaType == MediaType.Video
                     && !x.MovieId.HasValue
                     && !x.EpisodeId.HasValue)
            .ToListAsync(cancellationToken);
        if (mediaFiles.Count == 0)
        {
            return 0;
        }

        var mediaFileIdSet = mediaFiles.Select(x => x.Id).ToHashSet();
        var subtitleBindings = await dbContext.SubtitleBindings
            .Where(x => mediaFileIdSet.Contains(x.MediaFileId)
                        || mediaFileIdSet.Contains(x.SubtitleMediaFileId))
            .ToListAsync(cancellationToken);
        if (subtitleBindings.Count > 0)
        {
            dbContext.SubtitleBindings.RemoveRange(subtitleBindings);
        }

        var onlineSubtitleBindings = await dbContext.OnlineSubtitleBindings
            .Where(x => !x.IsDeleted && x.MediaFileId.HasValue && mediaFileIdSet.Contains(x.MediaFileId.Value))
            .ToListAsync(cancellationToken);
        foreach (var binding in onlineSubtitleBindings)
        {
            binding.IsDeleted = true;
            binding.UpdatedAt = now;
        }

        var retainedMediaFileIds = await dbContext.WatchHistories
            .Where(x => mediaFileIdSet.Contains(x.MediaFileId))
            .Select(x => x.MediaFileId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var retainedMediaFileIdSet = retainedMediaFileIds.ToHashSet();
        foreach (var mediaFile in mediaFiles)
        {
            if (retainedMediaFileIdSet.Contains(mediaFile.Id))
            {
                mediaFile.IsDeleted = true;
                mediaFile.UpdatedAt = now;
                continue;
            }

            dbContext.MediaFiles.Remove(mediaFile);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return mediaFiles.Count;
    }

    private static void CleanupCollectionEntityIfEmpty(AppDbContext dbContext, UserMovieCollectionItem entity)
    {
        if (!entity.IsWatched
            && !entity.IsWantToWatch
            && !entity.IsNotInterested
            && entity.LibraryVisibilityState == LibraryVisibilityState.Auto)
        {
            dbContext.UserMovieCollectionItems.Remove(entity);
        }
    }

    private static void RestoreAutoVisibilityForPositiveState(UserMovieCollectionItem entity, bool isPositiveState)
    {
        if (isPositiveState && entity.LibraryVisibilityState == LibraryVisibilityState.Hidden)
        {
            entity.LibraryVisibilityState = LibraryVisibilityState.Auto;
        }
    }

    private static LibraryVisibilityState ResolveRestoredVisibilityState(bool hasActiveSource, bool hasCurrentState)
    {
        return hasActiveSource || hasCurrentState
            ? LibraryVisibilityState.Auto
            : LibraryVisibilityState.Visible;
    }

    private sealed record OrphanHideOnlyResult(int HiddenMediaFileCount, int CreatedPlaceholderCount)
    {
        public static OrphanHideOnlyResult Empty { get; } = new(0, 0);
    }
}
