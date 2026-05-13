using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class LibraryMovieListItem
{
    public int MovieId { get; set; }

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string AiTagsText { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public IdentificationStatus IdentificationStatus { get; set; }

    public double? IdentifiedConfidence { get; set; }

    public string PrimaryRatingSourceName { get; set; } = string.Empty;

    public double? PrimaryRatingValue { get; set; }

    public double? PrimaryRatingScale { get; set; }

    public int? PrimaryRatingVoteCount { get; set; }

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public double? OmdbScoreValue { get; set; }

    public double? OmdbScoreScale { get; set; }

    public int? OmdbVoteCount { get; set; }

    public string OmdbSourceUrl { get; set; } = string.Empty;

    public DateTime? OmdbLastUpdatedAt { get; set; }

    public int SourceCount { get; set; }

    public bool HasLocalSource { get; set; }

    public bool HasWebDavSource { get; set; }

    public string SourceSummary
    {
        get
        {
            if (!IsInLibrary)
            {
                return "未入库";
            }

            return (HasLocalSource, HasWebDavSource) switch
            {
                (true, true) => "本地 + 网盘",
                (true, false) => "本地",
                (false, true) => "网盘",
                _ => "无播放源"
            };
        }
    }

    public bool IsInLibrary { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsWatched { get; set; }

    public bool IsWantToWatch { get; set; }

    public bool IsNotInterested { get; set; }

    public bool HasWatchHistory { get; set; }

    public DateTime UpdatedAt { get; set; }
}
