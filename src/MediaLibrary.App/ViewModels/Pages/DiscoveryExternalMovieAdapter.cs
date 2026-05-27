using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

internal static class DiscoveryExternalMovieAdapter
{
    public static AiRecommendationItem ToRecommendation(DiscoveryMovieCardViewModel movie)
    {
        return new AiRecommendationItem
        {
            MovieId = movie.MovieId ?? 0,
            TmdbId = movie.TmdbId,
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle,
            ReleaseYear = movie.ReleaseYear,
            ReleaseDate = ParseReleaseDate(movie.ReleaseDate),
            PosterRemoteUrl = movie.PosterRemoteUrl,
            Overview = movie.Overview,
            Country = movie.Country,
            Language = movie.Language,
            RuntimeMinutes = movie.RuntimeMinutes,
            ImdbId = movie.ImdbId,
            TmdbRating = movie.TmdbRating,
            TmdbVoteCount = movie.TmdbVoteCount,
            OmdbRating = movie.OmdbRating,
            Tags = movie.DisplayTags,
            EmotionTagsText = movie.EmotionTagsText,
            SceneTagsText = movie.SceneTagsText,
            IsInLibrary = movie.IsInLibrary,
            IsVisibleInLibrary = movie.IsVisibleInLibrary,
            LibraryVisibilityState = movie.LibraryVisibilityState,
            IsWatched = movie.IsWatched,
            IsWantToWatch = movie.IsWantToWatch,
            IsNotInterested = movie.IsNotInterested,
            ScopeText = "影片发现",
            AvailabilityText = movie.AvailabilityText,
            WatchStateText = movie.WatchStateText,
            Reason = string.IsNullOrWhiteSpace(movie.Overview) ? "来自 TMDB 影片搜索结果。" : movie.Overview
        };
    }

    private static DateTime? ParseReleaseDate(string value)
    {
        return DateTime.TryParse(value, out var date) ? date.Date : null;
    }
}
