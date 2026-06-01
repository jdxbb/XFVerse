using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class MovieDetailModel
{
    public int MovieId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string PosterLocalPath { get; set; } = string.Empty;

    public string PosterDisplayUrl => string.IsNullOrWhiteSpace(PosterRemoteUrl) ? PosterLocalPath : PosterRemoteUrl;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string DirectorText { get; set; } = string.Empty;

    public string WriterText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public string ProductionCompanyText { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string GenresText { get; set; } = string.Empty;

    public string AiTagsText { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

    public int? TmdbId { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public IdentificationStatus IdentificationStatus { get; set; }

    public double? IdentifiedConfidence { get; set; }

    public int? DefaultMediaFileId { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsWatched { get; set; }

    public bool IsWantToWatch { get; set; }

    public bool IsNotInterested { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public IReadOnlyList<MovieRatingItem> Ratings { get; set; } = [];

    public IReadOnlyList<MovieSourceItem> Sources { get; set; } = [];
}
