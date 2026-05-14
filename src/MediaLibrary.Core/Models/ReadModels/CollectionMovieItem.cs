namespace MediaLibrary.Core.Models.ReadModels;

public sealed class CollectionMovieItem
{
    public bool IsTvSeason { get; set; }

    public int? MovieId { get; set; }

    public int? TvSeasonId { get; set; }

    public int? TvSeriesId { get; set; }

    public int SeasonNumber { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public int InLibraryEpisodeCount { get; set; }

    public string SourceSummary { get; set; } = string.Empty;

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string AiTagsText { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

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

    public bool IsLiked { get; set; }

    public bool IsWantToWatch { get; set; }

    public bool IsWatched { get; set; }

    public bool IsNotInterested { get; set; }

    public bool IsInLibrary { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string CollectionTypeText => IsLiked && IsWantToWatch
        ? "喜爱 / 想看"
        : IsLiked
            ? "喜爱"
            : IsWantToWatch
                ? "想看"
                : IsWatched
                    ? "已看"
                    : "收藏";

    public string AvailabilityText => IsTvSeason
        ? InLibraryEpisodeCount > 0 ? $"已入库 {InLibraryEpisodeCount} 集" : "暂无播放源"
        : IsInLibrary ? "已入库" : "未入库";

    public string WatchStateText => IsTvSeason
        ? $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount}"
        : IsWatched ? "已看" : "未看";

    public string DetailButtonText => IsTvSeason
        ? "查看季详情"
        : IsInLibrary ? "查看详情并播放" : "查看详情（未入库）";
}
