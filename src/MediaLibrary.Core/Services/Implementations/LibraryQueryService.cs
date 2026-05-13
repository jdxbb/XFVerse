using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class LibraryQueryService : ILibraryQueryService
{
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
}
