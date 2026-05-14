using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvSeasonDetailModel
{
    public int SeasonId { get; set; }

    public int SeriesId { get; set; }

    public int SeasonNumber { get; set; }

    public string SeriesName { get; set; } = string.Empty;

    public string SeriesOriginalName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string PosterLocalPath { get; set; } = string.Empty;

    public string PosterDisplayUrl { get; set; } = string.Empty;

    public DateTime? AirDate { get; set; }

    public int? AirYear { get; set; }

    public string GenreDisplay { get; set; } = string.Empty;

    public string RatingDisplay { get; set; } = "评分将在后续阶段接入";

    public string SourceSummary { get; set; } = string.Empty;

    public int WatchedEpisodeCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public int InLibraryEpisodeCount { get; set; }

    public IdentificationStatus IdentificationStatus { get; set; }

    public string UnidentifiedSummary { get; set; } = string.Empty;

    public IReadOnlyList<TvSeasonEpisodeListItem> Episodes { get; set; } = [];

    public bool IsUnidentified => IdentificationStatus == IdentificationStatus.Failed;

    public string SeasonNumberText => $"S{SeasonNumber:D2}";

    public string AirDateText => AirDate.HasValue
        ? AirDate.Value.ToString("yyyy-MM-dd")
        : AirYear?.ToString() ?? "-";

    public string ProgressText => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount}";

    public string InLibraryText => $"已入库 {InLibraryEpisodeCount} 集";

    public string IdentificationStatusText => TvDetailDisplayText.FormatIdentificationStatus(IdentificationStatus);
}

public sealed class TvSeasonEpisodeListItem
{
    public int EpisodeId { get; set; }

    public int EpisodeNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public bool IsWatched { get; set; }

    public DateTime? LastPlayedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public int DurationWatchedSeconds { get; set; }

    public string StillRemoteUrl { get; set; } = string.Empty;

    public string SourceSummary { get; set; } = string.Empty;

    public bool HasPlayableSource { get; set; }

    public int ActiveSourceCount { get; set; }

    public string EpisodeNumberText => $"E{EpisodeNumber:D2}";

    public string RuntimeText => RuntimeMinutes.HasValue && RuntimeMinutes.Value > 0
        ? $"{RuntimeMinutes.Value} 分钟"
        : "-";

    public string WatchedText => IsWatched ? "已看" : "未看";

    public string LastPlayedText => LastPlayedAt.HasValue
        ? LastPlayedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "-";

    public string PositionText => LastPlayPositionSeconds > 0
        ? TimeSpan.FromSeconds(LastPlayPositionSeconds).ToString(@"hh\:mm\:ss")
        : "-";

    public string ProgressText => LastPlayPositionSeconds > 0
        ? $"进度 {PositionText}"
        : "暂无进度";

    public string PlayButtonText => HasPlayableSource ? "播放" : "暂无播放源";
}

internal static class TvDetailDisplayText
{
    public static string FormatIdentificationStatus(IdentificationStatus status)
    {
        return status switch
        {
            IdentificationStatus.Matched => "自动匹配",
            IdentificationStatus.NeedsReview => "待人工确认",
            IdentificationStatus.ManualConfirmed => "人工确认",
            IdentificationStatus.Failed => "未识别电视剧季",
            _ => "待识别"
        };
    }

    public static string FormatSourceSummary(IReadOnlyCollection<ProtocolType> protocols)
    {
        if (protocols.Count == 0)
        {
            return "暂无播放源";
        }

        var hasLocal = protocols.Contains(ProtocolType.Local);
        var hasWebDav = protocols.Contains(ProtocolType.WebDav);
        return (hasLocal, hasWebDav) switch
        {
            (true, true) => "本地 + 网盘",
            (true, false) => "本地",
            (false, true) => "网盘",
            _ => "暂无播放源"
        };
    }
}
