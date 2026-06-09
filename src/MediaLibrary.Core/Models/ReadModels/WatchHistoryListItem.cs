using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WatchHistoryListItem
{
    public int HistoryId { get; set; }

    public int MovieId { get; set; }

    public int? EpisodeId { get; set; }

    public int? TvSeasonId { get; set; }

    public int? TvSeriesId { get; set; }

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public int MediaFileId { get; set; }

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string AiTagsText { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

    public string MediaFileName { get; set; } = string.Empty;

    public DateTime StartedAtLocal { get; set; }

    public DateTime? EndedAtLocal { get; set; }

    public int DurationWatchedSeconds { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public int? TotalDurationSeconds { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsMediaFileDeleted { get; set; }

    public int SourceCount { get; set; }

    public bool HasLocalSource { get; set; }

    public bool HasWebDavSource { get; set; }

    public IdentificationStatus IdentificationStatus { get; set; }

    public double? ProgressPercent { get; set; }

    public bool IsEpisode => EpisodeId.HasValue;

    public bool HasActiveSource => SourceCount > 0;

    public string SourceSummary
    {
        get
        {
            if (IsMediaFileDeleted)
            {
                return "源不可用";
            }

            if (!HasActiveSource)
            {
                return "无播放源";
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
}
