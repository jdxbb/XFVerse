using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class LibraryMovieListItem
{
    public LibraryMediaItemKind ItemKind { get; set; } = LibraryMediaItemKind.Movie;

    public int MovieId { get; set; }

    public int SeriesId { get; set; }

    public int SeasonId { get; set; }

    public int SeasonNumber { get; set; }

    public int SeasonCount { get; set; }

    public int WatchedSeasonCount { get; set; }

    public int OrphanMediaFileId { get; set; }

    public string GroupedRangeKey { get; set; } = string.Empty;

    public IReadOnlyList<int> GroupedRangeMediaFileIds { get; set; } = [];

    public int GroupedRangeStartNumber { get; set; }

    public int GroupedRangeEndNumber { get; set; }

    public string GroupedRangeParentDisplay { get; set; } = string.Empty;

    public string GroupedRangeSampleFilesText { get; set; } = string.Empty;

    public string GroupedRangeReasonTagsText { get; set; } = string.Empty;

    public int InLibraryEpisodeCount { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public string SeriesTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string AiTagsText { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string DirectorText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public IdentificationStatus IdentificationStatus { get; set; }

    public double? IdentifiedConfidence { get; set; }

    public string PrimaryRatingSourceName { get; set; } = string.Empty;

    public double? PrimaryRatingValue { get; set; }

    public double? PrimaryRatingScale { get; set; }

    public int? PrimaryRatingVoteCount { get; set; }

    public double? SeriesPrimaryRatingValue { get; set; }

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public double? OmdbScoreValue { get; set; }

    public double? OmdbScoreScale { get; set; }

    public int? OmdbVoteCount { get; set; }

    public string OmdbSourceUrl { get; set; } = string.Empty;

    public DateTime? OmdbLastUpdatedAt { get; set; }

    public int SourceCount { get; set; }

    public int ActiveSourceCount { get; set; }

    public bool HasActiveSource { get; set; }

    public bool HasLocalSource { get; set; }

    public bool HasWebDavSource { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public bool HasLibraryContext { get; set; }

    public bool HasUserState { get; set; }

    public string SourceSummary
    {
        get
        {
            if (!HasActiveSource)
            {
                return "暂无播放源";
            }

            return (HasLocalSource, HasWebDavSource) switch
            {
                (true, true) => "本地/网盘",
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

    public double? ProgressPercent { get; set; } = 0d;

    public double ProgressValue => ProgressPercent.GetValueOrDefault();

    public bool HasProgressPercent => true;

    public bool IsTvLikeUnidentifiedItem => ItemKind == LibraryMediaItemKind.Other
                                            && (SeasonId > 0
                                                || (GroupedRangeStartNumber > 0
                                                    && GroupedRangeEndNumber >= GroupedRangeStartNumber));

    public bool IsTvLikeUnidentifiedSeries => ItemKind == LibraryMediaItemKind.Other
                                              && SeriesId > 0
                                              && SeasonId == 0
                                              && !TmdbId.HasValue
                                              && SeasonCount > 0;

    public string ProgressLabel => ItemKind switch
    {
        LibraryMediaItemKind.Series => $"已看 {WatchedSeasonCount} / {SeasonCount} 季",
        LibraryMediaItemKind.Season => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount} 集",
        LibraryMediaItemKind.Other when IsTvLikeUnidentifiedItem => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount} 集",
        LibraryMediaItemKind.Other when IsTvLikeUnidentifiedSeries => $"已看 {WatchedSeasonCount} / {SeasonCount} 季",
        _ => FormatProgressPercent(ProgressValue)
    };

    private static string FormatProgressPercent(double value)
    {
        return value == 0d ? "0%" : $"{value:0.0}%";
    }

    public DateTime UpdatedAt { get; set; }

    public bool IsMovie => ItemKind == LibraryMediaItemKind.Movie;

    public bool IsSeries => ItemKind == LibraryMediaItemKind.Series;

    public bool IsSeason => ItemKind == LibraryMediaItemKind.Season;

    public bool IsOther => ItemKind == LibraryMediaItemKind.Other;

    public string MediaKindText => ItemKind switch
    {
        LibraryMediaItemKind.Series => "电视剧",
        LibraryMediaItemKind.Season => "电视剧季",
        LibraryMediaItemKind.Other => "其他",
        _ => "电影"
    };

    public string ProgressSummary => ItemKind switch
    {
        LibraryMediaItemKind.Series => SeasonCount > 0
            ? $"已看 {WatchedSeasonCount} / {SeasonCount} 季"
            : $"有播放源 {InLibraryEpisodeCount} 集",
        LibraryMediaItemKind.Season => InLibraryEpisodeCount > 0
            ? $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount} · 有播放源 {InLibraryEpisodeCount} 集"
            : $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount} · 暂无播放源",
        LibraryMediaItemKind.Other when IsTvLikeUnidentifiedSeries => $"已看 {WatchedSeasonCount} / {SeasonCount} 季 · 有播放源 {InLibraryEpisodeCount} 集",
        LibraryMediaItemKind.Other when IsTvLikeUnidentifiedItem => GroupedRangeStartNumber > 0 && GroupedRangeEndNumber >= GroupedRangeStartNumber
            ? $"未识别剧集候选 · 已看 {WatchedEpisodeCount} / {TotalEpisodeCount} 集 · {SourceCount} 个文件 · {GroupedRangeStartNumber}-{GroupedRangeEndNumber}"
            : $"未识别剧集候选 · 已看 {WatchedEpisodeCount} / {TotalEpisodeCount} 集 · {SourceCount} 个文件",
        LibraryMediaItemKind.Other => GroupedRangeStartNumber > 0 && GroupedRangeEndNumber >= GroupedRangeStartNumber
            ? $"未识别剧集候选 · {SourceCount} 个文件 · {GroupedRangeStartNumber}-{GroupedRangeEndNumber}"
            : $"未识别 / 待修正 · {SourceCount} 个文件",
        _ => SourceSummary
    };
}
