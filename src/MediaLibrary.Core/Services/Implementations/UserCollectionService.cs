using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class UserCollectionService : IUserCollectionService
{
    public async Task<IReadOnlyList<CollectionMovieItem>> GetCollectionItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var likedMovies = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.IsFavorite)
            .Select(
                x => new CollectionMovieItem
                {
                    MovieId = x.Id,
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle ?? string.Empty,
                    ReleaseYear = x.ReleaseYear,
                    ReleaseDate = x.ReleaseDate,
                    PosterRemoteUrl = x.PosterRemoteUrl ?? string.Empty,
                    Overview = x.Overview ?? string.Empty,
                    GenresText = x.GenresText ?? string.Empty,
                    AiTagsText = x.AiTagsText ?? string.Empty,
                    EmotionTagsText = x.EmotionTagsText ?? string.Empty,
                    SceneTagsText = x.SceneTagsText ?? string.Empty,
                    Country = x.Country ?? string.Empty,
                    Language = x.Language ?? string.Empty,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId ?? string.Empty,
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
                    IsLiked = true,
                    IsWantToWatch = false,
                    IsWatched = x.IsWatched,
                    IsNotInterested = false,
                    IsInLibrary = x.MediaFiles.Any(media => !media.IsDeleted && media.MediaType == MediaType.Video),
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var favoriteItems = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsFavorite && !x.IsNotInterested)
            .Select(
                x => new CollectionMovieItem
                {
                    MovieId = x.MovieId,
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle,
                    ReleaseYear = x.ReleaseYear,
                    ReleaseDate = x.ReleaseDate,
                    PosterRemoteUrl = x.PosterRemoteUrl,
                    Overview = x.Overview,
                    GenresText = x.GenresText,
                    AiTagsText = x.GenresText,
                    EmotionTagsText = InferEmotionTags(x.Overview),
                    SceneTagsText = "\u72ec\u81ea\u89c2\u770b",
                    Country = x.Country,
                    Language = x.Language,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId,
                    TmdbRating = x.TmdbRating,
                    TmdbVoteCount = x.TmdbVoteCount,
                    OmdbScoreValue = x.OmdbScoreValue,
                    OmdbScoreScale = x.OmdbScoreScale,
                    OmdbVoteCount = x.OmdbVoteCount,
                    OmdbSourceUrl = x.OmdbSourceUrl,
                    OmdbLastUpdatedAt = x.OmdbLastUpdatedAt,
                    IsLiked = true,
                    IsWantToWatch = x.IsWantToWatch,
                    IsWatched = x.IsWatched,
                    IsNotInterested = x.IsNotInterested,
                    IsInLibrary = x.IsInLibrary,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var wantItems = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsWantToWatch && !x.IsFavorite && !x.IsNotInterested)
            .Select(
                x => new CollectionMovieItem
                {
                    MovieId = x.MovieId,
                    TmdbId = x.TmdbId,
                    Title = x.Title,
                    OriginalTitle = x.OriginalTitle,
                    ReleaseYear = x.ReleaseYear,
                    ReleaseDate = x.ReleaseDate,
                    PosterRemoteUrl = x.PosterRemoteUrl,
                    Overview = x.Overview,
                    GenresText = x.GenresText,
                    AiTagsText = x.GenresText,
                    EmotionTagsText = InferEmotionTags(x.Overview),
                    SceneTagsText = "\u72ec\u81ea\u89c2\u770b",
                    Country = x.Country,
                    Language = x.Language,
                    RuntimeMinutes = x.RuntimeMinutes,
                    ImdbId = x.ImdbId,
                    TmdbRating = x.TmdbRating,
                    TmdbVoteCount = x.TmdbVoteCount,
                    OmdbScoreValue = x.OmdbScoreValue,
                    OmdbScoreScale = x.OmdbScoreScale,
                    OmdbVoteCount = x.OmdbVoteCount,
                    OmdbSourceUrl = x.OmdbSourceUrl,
                    OmdbLastUpdatedAt = x.OmdbLastUpdatedAt,
                    IsLiked = false,
                    IsWantToWatch = true,
                    IsWatched = x.IsWatched,
                    IsNotInterested = x.IsNotInterested,
                    IsInLibrary = x.IsInLibrary,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        await HydrateCollectionItemRatingsFromMoviesAsync(
            dbContext,
            favoriteItems.Concat(wantItems),
            cancellationToken);

        NormalizeCollectionTags(likedMovies);
        NormalizeCollectionTags(favoriteItems);
        NormalizeCollectionTags(wantItems);

        var merged = new Dictionary<string, CollectionMovieItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in likedMovies.Concat(favoriteItems).Concat(wantItems))
        {
            var key = BuildKey(item.MovieId, item.TmdbId, item.Title, item.ReleaseYear);
            if (merged.TryGetValue(key, out var existing))
            {
                existing.IsLiked |= item.IsLiked;
                existing.IsWantToWatch |= item.IsWantToWatch;
                existing.IsWatched |= item.IsWatched;
                existing.IsNotInterested |= item.IsNotInterested;
                existing.IsInLibrary |= item.IsInLibrary;
                existing.TmdbRating ??= item.TmdbRating;
                existing.TmdbVoteCount ??= item.TmdbVoteCount;
                existing.OmdbScoreValue ??= item.OmdbScoreValue;
                existing.OmdbScoreScale ??= item.OmdbScoreScale;
                existing.OmdbVoteCount ??= item.OmdbVoteCount;
                existing.OmdbLastUpdatedAt ??= item.OmdbLastUpdatedAt;
                existing.OmdbSourceUrl = string.IsNullOrWhiteSpace(existing.OmdbSourceUrl) ? item.OmdbSourceUrl : existing.OmdbSourceUrl;
                existing.AiTagsText = string.IsNullOrWhiteSpace(existing.AiTagsText) ? item.AiTagsText : existing.AiTagsText;
                existing.EmotionTagsText = string.IsNullOrWhiteSpace(existing.EmotionTagsText) ? item.EmotionTagsText : existing.EmotionTagsText;
                existing.SceneTagsText = string.IsNullOrWhiteSpace(existing.SceneTagsText) ? item.SceneTagsText : existing.SceneTagsText;
                existing.UpdatedAt = existing.UpdatedAt > item.UpdatedAt ? existing.UpdatedAt : item.UpdatedAt;
                continue;
            }

            merged[key] = item;
        }

        return merged.Values
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private static async Task HydrateCollectionItemRatingsFromMoviesAsync(
        AppDbContext dbContext,
        IEnumerable<CollectionMovieItem> items,
        CancellationToken cancellationToken)
    {
        var collectionItems = items
            .Where(x => !x.IsTvSeason && (x.MovieId.HasValue || x.TmdbId.HasValue))
            .ToArray();
        if (collectionItems.Length == 0)
        {
            return;
        }

        var movieIds = collectionItems
            .Select(x => x.MovieId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var tmdbIds = collectionItems
            .Select(x => x.TmdbId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var ratings = await dbContext.Movies
            .AsNoTracking()
            .Where(x => movieIds.Contains(x.Id) || (x.TmdbId.HasValue && tmdbIds.Contains(x.TmdbId.Value)))
            .Select(
                x => new CollectionMovieRatingSnapshot
                {
                    MovieId = x.Id,
                    TmdbId = x.TmdbId,
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
                        .FirstOrDefault()
                })
            .ToListAsync(cancellationToken);

        foreach (var item in collectionItems)
        {
            var rating = item.MovieId.HasValue
                ? ratings.FirstOrDefault(x => x.MovieId == item.MovieId.Value)
                : null;
            rating ??= item.TmdbId.HasValue
                ? ratings.FirstOrDefault(x => x.TmdbId == item.TmdbId.Value)
                : null;
            if (rating is null)
            {
                continue;
            }

            item.TmdbRating ??= rating.TmdbRating;
            item.TmdbVoteCount ??= rating.TmdbVoteCount;
            item.OmdbScoreValue ??= rating.OmdbScoreValue;
            item.OmdbScoreScale ??= rating.OmdbScoreScale;
            item.OmdbVoteCount ??= rating.OmdbVoteCount;
            item.OmdbLastUpdatedAt ??= rating.OmdbLastUpdatedAt;
            item.OmdbSourceUrl = string.IsNullOrWhiteSpace(item.OmdbSourceUrl)
                ? rating.OmdbSourceUrl
                : item.OmdbSourceUrl;
        }
    }

    public async Task AddWantToWatchAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        if (recommendation.IsWatched || entity?.IsWatched == true)
        {
            throw new InvalidOperationException("已看影片不能加入想看。");
        }

        var now = DateTime.UtcNow;
        var oldWantToWatch = entity?.IsWantToWatch ?? false;
        var oldNotInterested = entity?.IsNotInterested ?? false;
        var oldWatched = entity?.IsWatched ?? false;

        entity ??= new UserMovieCollectionItem
        {
            CreatedAt = now
        };

        if (entity.Id == 0)
        {
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        entity.MovieId = recommendation.MovieId > 0 ? recommendation.MovieId : null;
        entity.TmdbId = recommendation.TmdbId;
        entity.Title = recommendation.Title;
        entity.OriginalTitle = recommendation.OriginalTitle;
        entity.ReleaseYear = recommendation.ReleaseYear;
        entity.ReleaseDate = recommendation.ReleaseDate;
        entity.PosterRemoteUrl = recommendation.PosterRemoteUrl;
        entity.Overview = recommendation.Overview;
        entity.GenresText = recommendation.Tags;
        entity.Country = recommendation.Country;
        entity.Language = recommendation.Language;
        entity.RuntimeMinutes = recommendation.RuntimeMinutes;
        entity.ImdbId = recommendation.ImdbId;
        entity.TmdbRating = recommendation.TmdbRating;
        entity.TmdbVoteCount = recommendation.TmdbVoteCount;
        entity.OmdbScoreValue = recommendation.OmdbRating?.ScoreValue;
        entity.OmdbScoreScale = recommendation.OmdbRating?.ScoreScale;
        entity.OmdbVoteCount = recommendation.OmdbRating?.VoteCount;
        entity.OmdbSourceUrl = recommendation.OmdbRating?.SourceUrl ?? string.Empty;
        entity.OmdbLastUpdatedAt = recommendation.OmdbRating?.LastUpdatedAt;
        entity.IsWantToWatch = true;
        entity.IsNotInterested = false;
        entity.IsWatched = recommendation.IsWatched;
        entity.IsInLibrary = recommendation.IsInLibrary;
        RestoreAutoVisibilityForPositiveState(entity, isPositiveState: true);
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            oldNotInterested,
            entity.IsNotInterested,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            oldWatched,
            entity.IsWatched,
            changeSource,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveWantToWatchAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        if (entity is null || !entity.IsWantToWatch)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var oldWantToWatch = entity.IsWantToWatch;
        if (entity.IsWatched)
        {
            entity.IsWantToWatch = false;
            entity.UpdatedAt = now;
        }
        else
        {
            entity.IsWantToWatch = false;
            entity.UpdatedAt = now;
            CleanupCollectionEntityIfEmpty(dbContext, entity);
        }

        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
            changeSource,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetWantToWatchAsync(
        int movieId,
        bool isWantToWatch,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        if (isWantToWatch && movie.IsWatched)
        {
            throw new InvalidOperationException("已看影片不能加入想看。");
        }

        var entity = await FindCollectionEntityForMovieAsync(dbContext, movie, cancellationToken);
        if (entity is null && !isWantToWatch)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var oldWantToWatch = entity?.IsWantToWatch ?? false;
        var oldNotInterested = entity?.IsNotInterested ?? false;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        ApplyMovieSnapshot(entity, movie);
        entity.IsWatched = movie.IsWatched;
        entity.IsWantToWatch = isWantToWatch;
        if (isWantToWatch)
        {
            entity.IsNotInterested = false;
        }

        RestoreAutoVisibilityForPositiveState(entity, isWantToWatch);
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            movie.Id,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            movie.Id,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            oldNotInterested,
            entity.IsNotInterested,
            changeSource,
            now);
        CleanupCollectionEntityIfEmpty(dbContext, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetWatchedAsync(
        AiRecommendationItem recommendation,
        bool isWatched,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        if (entity is null && !isWatched && recommendation.MovieId <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var oldEntityWatched = entity?.IsWatched ?? false;
        var oldEntityFavorite = entity?.IsFavorite ?? false;
        var oldEntityWantToWatch = entity?.IsWantToWatch ?? false;

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

        ApplyRecommendationSnapshot(entity, recommendation);
        entity.IsWatched = isWatched;
        if (isWatched)
        {
            entity.IsWantToWatch = false;
        }
        else
        {
            entity.IsFavorite = false;
        }

        RestoreAutoVisibilityForPositiveState(entity, isWatched);
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            oldEntityWatched,
            entity.IsWatched,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldEntityFavorite,
            entity.IsFavorite,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldEntityWantToWatch,
            entity.IsWantToWatch,
            changeSource,
            now);
        CleanupCollectionEntityIfEmpty(dbContext, entity);

        if (recommendation.MovieId > 0)
        {
            var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == recommendation.MovieId, cancellationToken);
            if (movie is not null)
            {
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
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetFavoriteAsync(
        AiRecommendationItem recommendation,
        bool isFavorite,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        var movie = await FindMovieForRecommendationAsync(dbContext, recommendation, cancellationToken);
        if (entity is null && movie is null && !isFavorite)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (movie is not null)
        {
            var oldMovieWatched = movie.IsWatched;
            var oldMovieFavorite = movie.IsFavorite;
            if (isFavorite && !movie.IsWatched && (entity?.IsWatched == true || recommendation.IsWatched))
            {
                movie.IsWatched = true;
            }

            if (isFavorite && !movie.IsWatched)
            {
                throw new InvalidOperationException("\u53ea\u6709\u5df2\u770b\u5f71\u7247\u53ef\u4ee5\u6807\u8bb0\u559c\u7231\u3002");
            }

            movie.IsFavorite = isFavorite;
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

            if (entity is not null)
            {
                var oldEntityWatched = entity.IsWatched;
                var oldEntityFavorite = entity.IsFavorite;
                var oldEntityWantToWatch = entity.IsWantToWatch;
                var oldEntityNotInterested = entity.IsNotInterested;
                ApplyMovieSnapshot(entity, movie);
                entity.IsWatched = movie.IsWatched;
                entity.IsFavorite = isFavorite;
                if (isFavorite)
                {
                    entity.IsWantToWatch = false;
                    entity.IsNotInterested = false;
                }

                RestoreAutoVisibilityForPositiveState(entity, isFavorite);
                entity.UpdatedAt = now;
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    entity.TmdbId,
                    movie.Id,
                    entity.Id == 0 ? null : entity.Id,
                    entity.Title,
                    UserMovieStateChangeHistoryRecorder.StateWatched,
                    oldEntityWatched,
                    entity.IsWatched,
                    changeSource,
                    now);
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    entity.TmdbId,
                    movie.Id,
                    entity.Id == 0 ? null : entity.Id,
                    entity.Title,
                    UserMovieStateChangeHistoryRecorder.StateFavorite,
                    oldEntityFavorite,
                    entity.IsFavorite,
                    changeSource,
                    now);
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    entity.TmdbId,
                    movie.Id,
                    entity.Id == 0 ? null : entity.Id,
                    entity.Title,
                    UserMovieStateChangeHistoryRecorder.StateWantToWatch,
                    oldEntityWantToWatch,
                    entity.IsWantToWatch,
                    changeSource,
                    now);
                UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                    dbContext,
                    entity.TmdbId,
                    movie.Id,
                    entity.Id == 0 ? null : entity.Id,
                    entity.Title,
                    UserMovieStateChangeHistoryRecorder.StateNotInterested,
                    oldEntityNotInterested,
                    entity.IsNotInterested,
                    changeSource,
                    now);
                CleanupCollectionEntityIfEmpty(dbContext, entity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now,
                IsWantToWatch = false,
                IsWatched = false,
                IsFavorite = false,
                IsNotInterested = false
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        var oldFavorite = entity.IsFavorite;
        var oldWatched = entity.IsWatched;
        var oldWantToWatch = entity.IsWantToWatch;
        var oldNotInterested = entity.IsNotInterested;
        ApplyRecommendationSnapshot(entity, recommendation);
        if (isFavorite && !entity.IsWatched && recommendation.IsWatched)
        {
            entity.IsWatched = true;
        }

        if (isFavorite && !entity.IsWatched)
        {
            throw new InvalidOperationException("\u53ea\u6709\u5df2\u770b\u5f71\u7247\u53ef\u4ee5\u6807\u8bb0\u559c\u7231\u3002");
        }

        entity.IsFavorite = isFavorite;
        if (isFavorite)
        {
            entity.IsWantToWatch = false;
            entity.IsNotInterested = false;
        }

        RestoreAutoVisibilityForPositiveState(entity, isFavorite);
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            oldWatched,
            entity.IsWatched,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldFavorite,
            entity.IsFavorite,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            oldNotInterested,
            entity.IsNotInterested,
            changeSource,
            now);
        CleanupCollectionEntityIfEmpty(dbContext, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetNotInterestedAsync(
        AiRecommendationItem recommendation,
        bool isNotInterested,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        var movie = await FindMovieForRecommendationAsync(dbContext, recommendation, cancellationToken);
        if (entity is null && !isNotInterested)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var oldNotInterested = entity?.IsNotInterested ?? false;
        var oldWantToWatch = entity?.IsWantToWatch ?? false;
        var oldFavorite = entity?.IsFavorite ?? false;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        if (movie is not null)
        {
            var oldMovieFavorite = movie.IsFavorite;
            ApplyMovieSnapshot(entity, movie);
            entity.IsWatched = movie.IsWatched;
            if (isNotInterested)
            {
                movie.IsFavorite = false;
                movie.UpdatedAt = now;
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
            }
        }
        else
        {
            ApplyRecommendationSnapshot(entity, recommendation);
            entity.IsWatched = recommendation.IsWatched;
        }

        entity.IsNotInterested = isNotInterested;
        if (isNotInterested)
        {
            entity.IsWantToWatch = false;
            entity.IsFavorite = false;
        }

        RestoreAutoVisibilityForPositiveState(entity, isNotInterested);
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            oldNotInterested,
            entity.IsNotInterested,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldFavorite,
            entity.IsFavorite,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
            changeSource,
            now);
        CleanupCollectionEntityIfEmpty(dbContext, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteNotInterestedStateLog(isNotInterested ? "recommendation-not-interested-marked" : "recommendation-not-interested-unmarked", entity);
    }

    public async Task SetNotInterestedAsync(
        int movieId,
        bool isNotInterested,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("影片不存在。");

        var entity = await FindCollectionEntityForMovieAsync(dbContext, movie, cancellationToken);
        if (entity is null && !isNotInterested)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var oldNotInterested = entity?.IsNotInterested ?? false;
        var oldWantToWatch = entity?.IsWantToWatch ?? false;
        var oldFavorite = entity?.IsFavorite ?? false;
        var oldMovieFavorite = movie.IsFavorite;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        ApplyMovieSnapshot(entity, movie);
        entity.IsWatched = movie.IsWatched;
        entity.IsNotInterested = isNotInterested;
        if (isNotInterested)
        {
            entity.IsWantToWatch = false;
            entity.IsFavorite = false;
            movie.IsFavorite = false;
            movie.UpdatedAt = now;
        }

        RestoreAutoVisibilityForPositiveState(entity, isNotInterested);
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            movie.Id,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            oldNotInterested,
            entity.IsNotInterested,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            movie.Id,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldFavorite,
            entity.IsFavorite,
            changeSource,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            movie.Id,
            entity.Id == 0 ? null : entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
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
        CleanupCollectionEntityIfEmpty(dbContext, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteNotInterestedStateLog(isNotInterested ? "recommendation-not-interested-marked" : "recommendation-not-interested-unmarked", entity);
    }

    public async Task HideFromLibraryAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        var now = DateTime.UtcNow;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserMovieCollectionItems.Add(entity);
            ApplyRecommendationSnapshot(entity, recommendation);
            entity.IsWatched = recommendation.IsWatched;
            entity.IsWantToWatch = recommendation.IsWantToWatch;
            entity.IsFavorite = recommendation.IsFavorite;
            entity.IsNotInterested = recommendation.IsNotInterested;
        }

        entity.IsInLibrary = false;
        entity.LibraryVisibilityState = LibraryVisibilityState.Hidden;
        entity.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        AiPerfDiagnostics.WriteEvent(
            $"event=library-hide-external-movie title=\"{SanitizeLogText(entity.Title)}\" year={FormatOptional(entity.ReleaseYear)} source={SanitizeLogText(changeSource, 32)}");
    }

    public async Task AddToLibraryAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        var movie = await FindMovieForRecommendationAsync(dbContext, recommendation, cancellationToken);
        var now = DateTime.UtcNow;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        if (movie is not null)
        {
            ApplyMovieSnapshot(entity, movie);
            movie.UpdatedAt = now;
        }
        else
        {
            ApplyRecommendationSnapshot(entity, recommendation);
        }

        entity.LibraryVisibilityState = LibraryVisibilityState.Visible;
        entity.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        AiPerfDiagnostics.WriteEvent(
            $"event=library-add-movie-visible title=\"{SanitizeLogText(entity.Title)}\" year={FormatOptional(entity.ReleaseYear)} source={SanitizeLogText(changeSource, 32)}");
    }

    public async Task RestoreToLibraryAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default,
        string changeSource = "Manual")
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        var movie = await FindMovieForRecommendationAsync(dbContext, recommendation, cancellationToken);
        var now = DateTime.UtcNow;

        if (entity is null)
        {
            entity = new UserMovieCollectionItem
            {
                CreatedAt = now,
                IsWantToWatch = false,
                IsWatched = false,
                IsFavorite = false,
                IsNotInterested = false
            };
            dbContext.UserMovieCollectionItems.Add(entity);
        }

        if (movie is not null)
        {
            ApplyMovieSnapshot(entity, movie);
        }
        else
        {
            ApplyRecommendationSnapshot(entity, recommendation);
        }

        var hasActiveSource = movie?.MediaFiles.Any(media => !media.IsDeleted && media.MediaType == MediaType.Video) == true;
        var hasCurrentState = movie?.IsFavorite == true
                              || movie?.IsWatched == true
                              || movie?.UserRating.HasValue == true
                              || entity.IsWatched
                              || entity.IsFavorite
                              || entity.IsWantToWatch
                              || entity.IsNotInterested
                              || recommendation.IsWatched
                              || recommendation.IsFavorite
                              || recommendation.IsWantToWatch
                              || recommendation.IsNotInterested;
        entity.LibraryVisibilityState = ResolveRestoredVisibilityState(hasActiveSource, hasCurrentState);
        entity.UpdatedAt = now;
        CleanupCollectionEntityIfEmpty(dbContext, entity);

        await dbContext.SaveChangesAsync(cancellationToken);
        AiPerfDiagnostics.WriteEvent(
            $"event=library-restore-movie title=\"{SanitizeLogText(entity.Title)}\" year={FormatOptional(entity.ReleaseYear)} source={SanitizeLogText(changeSource, 32)} visibility={entity.LibraryVisibilityState}");
    }

    public async Task<bool> IsNotInterestedAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        return entity?.IsNotInterested == true;
    }

    public async Task<IReadOnlyList<NotInterestedMovieKey>> GetNotInterestedKeysAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        return await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsNotInterested)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(
                x => new NotInterestedMovieKey
                {
                    MovieId = x.MovieId,
                    TmdbId = x.TmdbId,
                    ImdbId = x.ImdbId,
                    Title = x.Title,
                    ReleaseYear = x.ReleaseYear,
                    GenresText = x.GenresText,
                    UpdatedAt = x.UpdatedAt
                })
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveCollectionRecordAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        if (entity is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var oldWantToWatch = entity.IsWantToWatch;
        var oldWatched = entity.IsWatched;
        var oldFavorite = entity.IsFavorite;
        var oldNotInterested = entity.IsNotInterested;
        entity.IsWantToWatch = false;
        entity.IsWatched = false;
        entity.IsFavorite = false;
        entity.IsNotInterested = false;
        entity.UpdatedAt = now;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWantToWatch,
            oldWantToWatch,
            entity.IsWantToWatch,
            UserMovieStateChangeHistoryRecorder.SourceCollection,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            oldWatched,
            entity.IsWatched,
            UserMovieStateChangeHistoryRecorder.SourceCollection,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldFavorite,
            entity.IsFavorite,
            UserMovieStateChangeHistoryRecorder.SourceCollection,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            entity.TmdbId,
            entity.MovieId,
            entity.Id,
            entity.Title,
            UserMovieStateChangeHistoryRecorder.StateNotInterested,
            oldNotInterested,
            entity.IsNotInterested,
            UserMovieStateChangeHistoryRecorder.SourceCollection,
            now);
        CleanupCollectionEntityIfEmpty(dbContext, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCollectionRecordAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await FindCollectionEntityAsync(dbContext, recommendation, cancellationToken);
        if (entity is null)
        {
            return;
        }

        var stateHistories = await dbContext.UserMovieStateChangeHistories
            .Where(x => x.UserMovieCollectionItemId == entity.Id)
            .ToListAsync(cancellationToken);
        if (stateHistories.Count > 0)
        {
            dbContext.UserMovieStateChangeHistories.RemoveRange(stateHistories);
        }

        dbContext.UserMovieCollectionItems.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Movie?> FindMovieForRecommendationAsync(
        AppDbContext dbContext,
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken)
    {
        if (recommendation.MovieId > 0)
        {
            var movie = await dbContext.Movies
                .Include(x => x.MediaFiles)
                .FirstOrDefaultAsync(x => x.Id == recommendation.MovieId, cancellationToken);
            if (movie is not null)
            {
                return movie;
            }
        }

        if (recommendation.TmdbId.HasValue)
        {
            var movie = await dbContext.Movies
                .Include(x => x.MediaFiles)
                .FirstOrDefaultAsync(x => x.TmdbId == recommendation.TmdbId.Value, cancellationToken);
            if (movie is not null)
            {
                return movie;
            }
        }

        if (!string.IsNullOrWhiteSpace(recommendation.ImdbId))
        {
            var movie = await dbContext.Movies
                .Include(x => x.MediaFiles)
                .FirstOrDefaultAsync(x => x.ImdbId == recommendation.ImdbId, cancellationToken);
            if (movie is not null)
            {
                return movie;
            }
        }

        return await dbContext.Movies
            .Include(x => x.MediaFiles)
            .FirstOrDefaultAsync(
                x => x.Title == recommendation.Title && x.ReleaseYear == recommendation.ReleaseYear,
                cancellationToken);
    }

    private static async Task<UserMovieCollectionItem?> FindCollectionEntityForMovieAsync(
        AppDbContext dbContext,
        Movie movie,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.UserMovieCollectionItems
            .FirstOrDefaultAsync(x => x.MovieId == movie.Id, cancellationToken);
        if (entity is not null)
        {
            return entity;
        }

        if (movie.TmdbId.HasValue)
        {
            entity = await dbContext.UserMovieCollectionItems
                .FirstOrDefaultAsync(x => x.TmdbId == movie.TmdbId.Value, cancellationToken);
            if (entity is not null)
            {
                return entity;
            }
        }

        if (!string.IsNullOrWhiteSpace(movie.ImdbId))
        {
            entity = await dbContext.UserMovieCollectionItems
                .FirstOrDefaultAsync(x => x.ImdbId == movie.ImdbId, cancellationToken);
            if (entity is not null)
            {
                return entity;
            }
        }

        return await dbContext.UserMovieCollectionItems
            .FirstOrDefaultAsync(
                x => x.Title == movie.Title && x.ReleaseYear == movie.ReleaseYear,
                cancellationToken);
    }

    private static async Task<UserMovieCollectionItem?> FindCollectionEntityAsync(
        AppDbContext dbContext,
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken)
    {
        UserMovieCollectionItem? entity = null;
        if (recommendation.MovieId > 0)
        {
            entity = await dbContext.UserMovieCollectionItems
                .FirstOrDefaultAsync(x => x.MovieId == recommendation.MovieId, cancellationToken);
        }

        if (entity is null && recommendation.TmdbId.HasValue)
        {
            entity = await dbContext.UserMovieCollectionItems
                .FirstOrDefaultAsync(x => x.TmdbId == recommendation.TmdbId.Value, cancellationToken);
        }

        if (entity is null && !string.IsNullOrWhiteSpace(recommendation.ImdbId))
        {
            entity = await dbContext.UserMovieCollectionItems
                .FirstOrDefaultAsync(x => x.ImdbId == recommendation.ImdbId, cancellationToken);
        }

        if (entity is null)
        {
            entity = await dbContext.UserMovieCollectionItems
                .FirstOrDefaultAsync(
                    x => x.Title == recommendation.Title && x.ReleaseYear == recommendation.ReleaseYear,
                    cancellationToken);
        }

        return entity;
    }

    private static void ApplyRecommendationSnapshot(
        UserMovieCollectionItem entity,
        AiRecommendationItem recommendation)
    {
        entity.MovieId = recommendation.MovieId > 0 ? recommendation.MovieId : null;
        entity.TmdbId = recommendation.TmdbId;
        entity.Title = recommendation.Title;
        entity.OriginalTitle = recommendation.OriginalTitle;
        entity.ReleaseYear = recommendation.ReleaseYear;
        entity.ReleaseDate = recommendation.ReleaseDate;
        entity.PosterRemoteUrl = recommendation.PosterRemoteUrl;
        entity.Overview = recommendation.Overview;
        entity.GenresText = recommendation.Tags;
        entity.Country = recommendation.Country;
        entity.Language = recommendation.Language;
        entity.RuntimeMinutes = recommendation.RuntimeMinutes;
        entity.ImdbId = recommendation.ImdbId;
        entity.TmdbRating = recommendation.TmdbRating;
        entity.TmdbVoteCount = recommendation.TmdbVoteCount;
        entity.OmdbScoreValue = recommendation.OmdbRating?.ScoreValue;
        entity.OmdbScoreScale = recommendation.OmdbRating?.ScoreScale;
        entity.OmdbVoteCount = recommendation.OmdbRating?.VoteCount;
        entity.OmdbSourceUrl = recommendation.OmdbRating?.SourceUrl ?? string.Empty;
        entity.OmdbLastUpdatedAt = recommendation.OmdbRating?.LastUpdatedAt;
        entity.IsInLibrary = recommendation.IsInLibrary;
    }

    private static void ApplyMovieSnapshot(
        UserMovieCollectionItem entity,
        Movie movie)
    {
        entity.MovieId = movie.Id;
        entity.TmdbId = movie.TmdbId;
        entity.Title = movie.Title;
        entity.OriginalTitle = movie.OriginalTitle ?? string.Empty;
        entity.ReleaseYear = movie.ReleaseYear;
        entity.ReleaseDate = movie.ReleaseDate;
        entity.PosterRemoteUrl = movie.PosterRemoteUrl ?? string.Empty;
        entity.Overview = movie.Overview ?? string.Empty;
        entity.GenresText = movie.GenresText ?? string.Empty;
        entity.Country = movie.Country ?? string.Empty;
        entity.Language = movie.Language ?? string.Empty;
        entity.RuntimeMinutes = movie.RuntimeMinutes;
        entity.ImdbId = movie.ImdbId ?? string.Empty;
        entity.IsInLibrary = movie.MediaFiles.Any(media => !media.IsDeleted && media.MediaType == MediaType.Video);
    }

    private static void CleanupCollectionEntityIfEmpty(AppDbContext dbContext, UserMovieCollectionItem entity)
    {
        if (!entity.IsWatched
            && !entity.IsFavorite
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

    private static void WriteNotInterestedStateLog(string eventName, UserMovieCollectionItem entity)
    {
        AiPerfDiagnostics.WriteEvent(
            $"event={eventName} title=\"{SanitizeLogText(entity.Title)}\" year={FormatOptional(entity.ReleaseYear)} tmdbId={FormatOptional(entity.TmdbId)} imdbId={FormatLogValue(entity.ImdbId)} staleCandidatePool=true");
    }

    private static string FormatOptional(int? value)
    {
        return value?.ToString() ?? "(none)";
    }

    private static string FormatLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(none)"
            : SanitizeLogText(value, 64);
    }

    private static string SanitizeLogText(string? value, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(none)";
        }

        var sanitized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace('"', '\'')
            .Trim();
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    private static string InferEmotionTags(string? overview)
    {
        var text = overview ?? string.Empty;
        if (text.Contains("\u9b54\u6cd5", StringComparison.OrdinalIgnoreCase))
        {
            return "\u6e29\u6696\u3001\u68a6\u5e7b";
        }

        if (text.Contains("\u72af\u7f6a", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\u60ac\u7591", StringComparison.OrdinalIgnoreCase))
        {
            return "\u7d27\u5f20\u3001\u60ac\u7591";
        }

        return "\u601d\u8003\u5411";
    }

    private static void NormalizeCollectionTags(IEnumerable<CollectionMovieItem> items)
    {
        foreach (var item in items)
        {
            item.AiTagsText = AiTagVocabulary.NormalizeText(item.AiTagsText, AiTagVocabulary.TypeTags);
            item.EmotionTagsText = AiTagVocabulary.NormalizeText(item.EmotionTagsText, AiTagVocabulary.EmotionTags);
            item.SceneTagsText = AiTagVocabulary.NormalizeText(item.SceneTagsText, AiTagVocabulary.SceneTags);
        }
    }

    private static string BuildKey(int? movieId, int? tmdbId, string title, int? year)
    {
        if (tmdbId.HasValue)
        {
            return $"tmdb:{tmdbId.Value}";
        }

        if (movieId.HasValue)
        {
            return $"movie:{movieId.Value}";
        }

        return $"title:{title.Trim().ToLowerInvariant()}:{year?.ToString() ?? string.Empty}";
    }

    private sealed class CollectionMovieRatingSnapshot
    {
        public int MovieId { get; init; }

        public int? TmdbId { get; init; }

        public double? TmdbRating { get; init; }

        public int? TmdbVoteCount { get; init; }

        public double? OmdbScoreValue { get; init; }

        public double? OmdbScoreScale { get; init; }

        public int? OmdbVoteCount { get; init; }

        public string OmdbSourceUrl { get; init; } = string.Empty;

        public DateTime? OmdbLastUpdatedAt { get; init; }
    }
}
