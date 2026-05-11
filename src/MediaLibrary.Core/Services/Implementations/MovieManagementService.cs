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
            foreach (var item in collectionItems.Where(x => x.IsWantToWatch || x.IsWatched != isWatched))
            {
                var oldWantToWatch = item.IsWantToWatch;
                var oldWatched = item.IsWatched;
                item.IsWantToWatch = false;
                item.IsWatched = true;
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
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var now = DateTime.UtcNow;
        var activeVideoFiles = movie.MediaFiles
            .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted)
            .ToList();
        var collectionItems = await FindCollectionItemsForMovieAsync(dbContext, movie, cancellationToken);
        var hasWatchHistory = await dbContext.WatchHistories
            .AsNoTracking()
            .AnyAsync(x => x.MovieId == movieId, cancellationToken);
        var preserveState = ShouldPreserveRemovedLibraryState(movie, collectionItems, hasWatchHistory);

        if (activeVideoFiles.Count == 0)
        {
            if (preserveState)
            {
                PreserveRemovedLibraryState(dbContext, movie, collectionItems, hasWatchHistory, now);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var deletedMediaFileIds = activeVideoFiles.Select(x => x.Id).ToHashSet();
        foreach (var mediaFile in activeVideoFiles)
        {
            mediaFile.IsDeleted = true;
            mediaFile.UpdatedAt = now;
        }

        if (movie.DefaultMediaFileId.HasValue && deletedMediaFileIds.Contains(movie.DefaultMediaFileId.Value))
        {
            movie.DefaultMediaFileId = movie.MediaFiles
                .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted && !deletedMediaFileIds.Contains(x.Id))
                .OrderBy(x => x.FileName)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
        }

        movie.UpdatedAt = now;

        if (preserveState)
        {
            PreserveRemovedLibraryState(dbContext, movie, collectionItems, hasWatchHistory, now);
        }
        else
        {
            foreach (var item in collectionItems.Where(x => x.MovieId == movieId && x.IsInLibrary))
            {
                item.IsInLibrary = false;
                item.UpdatedAt = now;
                CleanupCollectionEntityIfEmpty(dbContext, item);
            }

            AiPerfDiagnostics.WriteEvent($"event=library-remove-from-library-no-state movieId={movie.Id} preserveCollection=false");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool ShouldPreserveRemovedLibraryState(
        Movie movie,
        IReadOnlyCollection<UserMovieCollectionItem> collectionItems,
        bool hasWatchHistory)
    {
        return movie.IsWatched
               || movie.IsFavorite
               || movie.UserRating.HasValue
               || hasWatchHistory
               || collectionItems.Any(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested);
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
        var shouldKeepCollectionState = movie.IsWatched || collectionWatched || collectionWantToWatch || collectionNotInterested;
        UserMovieCollectionItem? entity = null;
        var collectionCreated = false;

        if (shouldKeepCollectionState)
        {
            entity = collectionItems
                .OrderByDescending(x => x.MovieId == movie.Id)
                .ThenByDescending(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested)
                .ThenByDescending(x => x.UpdatedAt)
                .FirstOrDefault();
            collectionCreated = entity is null;

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
            entity.IsWatched = movie.IsWatched || collectionWatched;
            entity.IsWantToWatch = !entity.IsWatched && collectionWantToWatch;
            entity.IsNotInterested = collectionNotInterested;
            entity.UpdatedAt = now;
        }

        foreach (var item in collectionItems.Where(x => (entity is null || x.Id != entity.Id) && x.MovieId == movie.Id && x.IsInLibrary))
        {
            item.IsInLibrary = false;
            item.UpdatedAt = now;
            CleanupCollectionEntityIfEmpty(dbContext, item);
        }

        AiPerfDiagnostics.WriteEvent(
            $"event=library-remove-from-library-preserve-state movieId={movie.Id} collectionCreated={collectionCreated} "
            + $"watched={entity?.IsWatched == true} favorite={movie.IsFavorite} want={entity?.IsWantToWatch == true} "
            + $"notInterested={entity?.IsNotInterested == true} hasHistory={hasWatchHistory}");
        AiPerfDiagnostics.WriteEvent(
            $"event=library-remove-from-library-state-visible movieId={movie.Id} isInLibrary=false "
            + $"visibleInLibrary=true visibleInCollection={(movie.IsFavorite || entity?.IsWantToWatch == true)}");
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
        var placeholderTitle = string.IsNullOrWhiteSpace(parsedName.CleanTitle)
            ? Path.GetFileNameWithoutExtension(mediaFile.FileName)
            : parsedName.CleanTitle;
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
            $"media-identification-reset-resume-cleared mediaFileId={mediaFile.Id} reason=reset-to-unidentified");

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

        if (movie.MediaFiles.Count == 0
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

    private static void CleanupCollectionEntityIfEmpty(AppDbContext dbContext, UserMovieCollectionItem entity)
    {
        if (!entity.IsWatched && !entity.IsWantToWatch && !entity.IsNotInterested)
        {
            dbContext.UserMovieCollectionItems.Remove(entity);
        }
    }
}
