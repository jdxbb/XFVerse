using System.Diagnostics;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;

namespace MediaLibrary.Core.Services.Implementations;

internal static class UserMovieStateChangeHistoryRecorder
{
    internal const string StateWatched = "Watched";
    internal const string StateFavorite = "Favorite";
    internal const string StateWantToWatch = "WantToWatch";
    internal const string StateNotInterested = "NotInterested";

    internal const string SourceManual = "Manual";
    internal const string SourceAutoWatched = "AutoWatched";
    internal const string SourceBatch = "Batch";
    internal const string SourceRecommendation = "Recommendation";
    internal const string SourceCollection = "Collection";
    internal const string SourceIdentification = "Identification";
    internal const string SourceUnknown = "Unknown";

    internal static void RecordIfChanged(
        AppDbContext dbContext,
        int? tmdbId,
        int? movieId,
        int? collectionItemId,
        string? title,
        string stateType,
        bool oldValue,
        bool newValue,
        string? source,
        DateTime changedAtUtc)
    {
        if (oldValue == newValue)
        {
            return;
        }

        if (!tmdbId.HasValue || tmdbId.Value <= 0)
        {
            Debug.WriteLine(
                "[STATE-HISTORY] state-change-history-skip reason=no-tmdb "
                + $"movieId={movieId?.ToString() ?? "null"} collectionItemId={collectionItemId?.ToString() ?? "null"} stateType={stateType}");
            return;
        }

        dbContext.UserMovieStateChangeHistories.Add(
            new UserMovieStateChangeHistory
            {
                TmdbId = tmdbId.Value,
                MovieId = movieId,
                UserMovieCollectionItemId = collectionItemId,
                Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                StateType = stateType,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedAtUtc = changedAtUtc,
                Source = NormalizeSource(source),
                CreatedAtUtc = changedAtUtc
            });
    }

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return SourceUnknown;
        }

        var trimmed = source.Trim();
        return trimmed.Length <= 40 ? trimmed : trimmed[..40];
    }
}
