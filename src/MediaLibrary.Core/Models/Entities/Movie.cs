using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class Movie
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public int? ReleaseYear { get; set; }

    public string? Overview { get; set; }

    public string? PosterLocalPath { get; set; }

    public string? PosterRemoteUrl { get; set; }

    public string? Country { get; set; }

    public string? Language { get; set; }

    public int? RuntimeMinutes { get; set; }

    public int? TmdbId { get; set; }

    public string? ImdbId { get; set; }

    public double? IdentifiedConfidence { get; set; }

    public IdentificationStatus IdentificationStatus { get; set; } = IdentificationStatus.Pending;

    public string? GenresText { get; set; }

    public string? AiTagsText { get; set; }

    public string? EmotionTagsText { get; set; }

    public string? SceneTagsText { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsWatched { get; set; }

    public double? UserRating { get; set; }

    public DateTime? LastPlayedAt { get; set; }

    public DateTime? AutoWatchedBaselineAtUtc { get; set; }

    public int? DefaultMediaFileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public MediaFile? DefaultMediaFile { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public ICollection<RatingSource> RatingSources { get; set; } = new List<RatingSource>();

    public ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();
}
