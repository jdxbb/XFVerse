using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class DiscoveryMovieStatusResolver : IDiscoveryMovieStatusResolver
{
    public async Task<IReadOnlyDictionary<int, DiscoveryMovieStatus>> ResolveAsync(
        IEnumerable<int> tmdbIds,
        CancellationToken cancellationToken = default)
    {
        var ids = tmdbIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, DiscoveryMovieStatus>();
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movieRows = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.TmdbId.HasValue && ids.Contains(x.TmdbId.Value))
            .Select(
                x => new
                {
                    x.Id,
                    TmdbId = x.TmdbId!.Value,
                    x.Title,
                    x.OriginalTitle,
                    x.ReleaseYear,
                    x.ReleaseDate,
                    x.PosterRemoteUrl,
                    x.Overview,
                    x.GenresText,
                    x.Country,
                    x.Language,
                    x.DirectorText,
                    x.ActorsText,
                    x.RuntimeMinutes,
                    x.ImdbId,
                    x.IsFavorite,
                    x.IsWatched,
                    x.IdentificationStatus,
                    x.UpdatedAt,
                    SourceCount = x.MediaFiles.Count(media => !media.IsDeleted && media.MediaType == MediaType.Video),
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

        var collectionRows = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.TmdbId.HasValue && ids.Contains(x.TmdbId.Value))
            .Select(
                x => new
                {
                    x.Id,
                    TmdbId = x.TmdbId!.Value,
                    x.MovieId,
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
                    x.IsWantToWatch,
                    x.IsWatched,
                    x.IsNotInterested,
                    x.IsInLibrary,
                    x.LibraryVisibilityState,
                    x.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, DiscoveryMovieStatus>();

        foreach (var group in movieRows.GroupBy(x => x.TmdbId))
        {
            var movie = group
                .OrderByDescending(x => x.SourceCount > 0)
                .ThenByDescending(x => x.UpdatedAt)
                .First();
            var hasLocalRecognizedMovie = movie.TmdbId > 0
                                          && movie.IdentificationStatus != IdentificationStatus.Failed;

            result[movie.TmdbId] = new DiscoveryMovieStatus
            {
                TmdbId = movie.TmdbId,
                MovieId = movie.Id,
                ActiveSourceCount = movie.SourceCount,
                IsInLibrary = movie.SourceCount > 0,
                IsVisibleInLibrary = ResolveIsVisibleInLibrary(
                    movie.SourceCount > 0,
                    LibraryVisibilityState.Auto,
                    hasLocalRecognizedMovie || group.Any(x => x.IsFavorite || x.IsWatched)),
                LibraryVisibilityState = LibraryVisibilityState.Auto,
                IsWatched = group.Any(x => x.IsWatched),
                IsFavorite = group.Any(x => x.IsFavorite),
                Title = movie.Title,
                OriginalTitle = movie.OriginalTitle ?? string.Empty,
                ReleaseYear = movie.ReleaseYear,
                ReleaseDate = movie.ReleaseDate,
                PosterRemoteUrl = movie.PosterRemoteUrl ?? string.Empty,
                Overview = movie.Overview ?? string.Empty,
                GenresText = movie.GenresText ?? string.Empty,
                Country = movie.Country ?? string.Empty,
                Language = movie.Language ?? string.Empty,
                DirectorText = movie.DirectorText ?? string.Empty,
                ActorsText = movie.ActorsText ?? string.Empty,
                RuntimeMinutes = movie.RuntimeMinutes,
                ImdbId = movie.ImdbId ?? string.Empty,
                TmdbRating = movie.TmdbRating,
                TmdbVoteCount = movie.TmdbVoteCount,
                OmdbScoreValue = movie.OmdbScoreValue,
                OmdbScoreScale = movie.OmdbScoreScale,
                OmdbVoteCount = movie.OmdbVoteCount,
                OmdbSourceUrl = movie.OmdbSourceUrl,
                OmdbLastUpdatedAt = movie.OmdbLastUpdatedAt
            };
        }

        foreach (var group in collectionRows.GroupBy(x => x.TmdbId))
        {
            var collection = group.OrderByDescending(x => x.UpdatedAt).First();
            if (!result.TryGetValue(group.Key, out var status))
            {
                var hasState = group.Any(x => x.IsWatched || x.IsWantToWatch || x.IsNotInterested);
                status = new DiscoveryMovieStatus
                {
                    TmdbId = group.Key,
                    MovieId = collection.MovieId,
                    IsInLibrary = collection.IsInLibrary,
                    IsVisibleInLibrary = ResolveIsVisibleInLibrary(collection.IsInLibrary, collection.LibraryVisibilityState, hasState),
                    LibraryVisibilityState = collection.LibraryVisibilityState,
                    Title = collection.Title,
                    OriginalTitle = collection.OriginalTitle,
                    ReleaseYear = collection.ReleaseYear,
                    ReleaseDate = collection.ReleaseDate,
                    PosterRemoteUrl = collection.PosterRemoteUrl,
                    Overview = collection.Overview,
                    GenresText = collection.GenresText,
                    Country = collection.Country,
                    Language = collection.Language,
                    RuntimeMinutes = collection.RuntimeMinutes,
                    ImdbId = collection.ImdbId,
                    TmdbRating = collection.TmdbRating,
                    TmdbVoteCount = collection.TmdbVoteCount,
                    OmdbScoreValue = collection.OmdbScoreValue,
                    OmdbScoreScale = collection.OmdbScoreScale,
                    OmdbVoteCount = collection.OmdbVoteCount,
                    OmdbSourceUrl = collection.OmdbSourceUrl,
                    OmdbLastUpdatedAt = collection.OmdbLastUpdatedAt
                };
                result[group.Key] = status;
            }

            status.IsWantToWatch = group.Any(x => x.IsWantToWatch);
            status.IsWatched |= group.Any(x => x.IsWatched);
            status.IsNotInterested = group.Any(x => x.IsNotInterested);
            status.IsInLibrary = status.ActiveSourceCount > 0;
            status.LibraryVisibilityState = ResolveLibraryVisibilityState(group.Select(x => new CollectionVisibilityRow(x.LibraryVisibilityState, x.UpdatedAt)));
            status.IsVisibleInLibrary = ResolveIsVisibleInLibrary(
                status.IsInLibrary,
                status.LibraryVisibilityState,
                status.HasLocalMovie || status.IsWatched || status.IsWantToWatch || status.IsFavorite || status.IsNotInterested);
            status.OmdbScoreValue ??= collection.OmdbScoreValue;
            status.OmdbScoreScale ??= collection.OmdbScoreScale;
            status.OmdbVoteCount ??= collection.OmdbVoteCount;
            status.OmdbSourceUrl = string.IsNullOrWhiteSpace(status.OmdbSourceUrl) ? collection.OmdbSourceUrl : status.OmdbSourceUrl;
            status.OmdbLastUpdatedAt ??= collection.OmdbLastUpdatedAt;
            status.TmdbRating ??= collection.TmdbRating;
            status.TmdbVoteCount ??= collection.TmdbVoteCount;
            status.ReleaseDate ??= collection.ReleaseDate;
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

    private static LibraryVisibilityState ResolveLibraryVisibilityState(IEnumerable<CollectionVisibilityRow> rows)
    {
        return rows
            .OrderByDescending(x => x.LibraryVisibilityState != LibraryVisibilityState.Auto)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => x.LibraryVisibilityState)
            .FirstOrDefault();
    }

    private sealed record CollectionVisibilityRow(LibraryVisibilityState LibraryVisibilityState, DateTime UpdatedAt);
}
