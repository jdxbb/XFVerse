using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class UserMovieCollectionItem
{
    public int Id { get; set; }

    public int? MovieId { get; set; }

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public double? OmdbScoreValue { get; set; }

    public double? OmdbScoreScale { get; set; }

    public int? OmdbVoteCount { get; set; }

    public string OmdbSourceUrl { get; set; } = string.Empty;

    public DateTime? OmdbLastUpdatedAt { get; set; }

    public bool IsWantToWatch { get; set; } = true;

    public bool IsWatched { get; set; }

    public bool IsNotInterested { get; set; }

    public bool IsInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
