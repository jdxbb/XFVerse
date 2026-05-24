using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

internal static class ScanCandidateVisibilityGuard
{
    public const string HiddenFailedPlaceholderSkipReason = "hidden-placeholder-excluded-from-scan-candidate";

    public static async Task<HashSet<int>> LoadHiddenMovieIdsAsync(
        AppDbContext dbContext,
        IEnumerable<int?> movieIds,
        CancellationToken cancellationToken)
    {
        var ids = movieIds
            .Select(x => x.GetValueOrDefault())
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return (await dbContext.UserMovieCollectionItems
                .AsNoTracking()
                .Where(x => x.MovieId.HasValue
                            && ids.Contains(x.MovieId.Value)
                            && x.LibraryVisibilityState == LibraryVisibilityState.Hidden)
                .Select(x => x.MovieId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    public static bool IsHiddenFailedMoviePlaceholder(MediaFile mediaFile, IReadOnlySet<int> hiddenMovieIds)
    {
        return mediaFile.MovieId.HasValue
               && hiddenMovieIds.Contains(mediaFile.MovieId.Value)
               && mediaFile.Movie?.IdentificationStatus == IdentificationStatus.Failed;
    }
}
