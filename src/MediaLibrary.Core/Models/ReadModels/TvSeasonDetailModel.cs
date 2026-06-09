using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvSeasonDetailModel
{
    public int SeasonId { get; set; }

    public int SeriesId { get; set; }

    public int? TmdbSeriesId { get; set; }

    public int? TmdbSeasonId { get; set; }

    public int SeasonNumber { get; set; }

    public string SeriesName { get; set; } = string.Empty;

    public string SeriesOriginalName { get; set; } = string.Empty;

    public string SeriesCountry { get; set; } = string.Empty;

    public string SeriesLanguage { get; set; } = string.Empty;

    public string SeriesDirectorText { get; set; } = string.Empty;

    public string SeriesWriterText { get; set; } = string.Empty;

    public string SeriesActorsText { get; set; } = string.Empty;

    public string SeriesNetworksText { get; set; } = string.Empty;

    public string SeriesProductionCompaniesText { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string PosterLocalPath { get; set; } = string.Empty;

    public string PosterDisplayUrl { get; set; } = string.Empty;

    public DateTime? AirDate { get; set; }

    public int? AirYear { get; set; }

    public string GenreDisplay { get; set; } = string.Empty;

    public string RatingDisplay { get; set; } = "暂无评分";

    public string SourceSummary { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public bool IsWantToWatch { get; set; }

    public bool IsNotInterested { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public int WatchedEpisodeCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public int InLibraryEpisodeCount { get; set; }

    public IdentificationStatus IdentificationStatus { get; set; }

    public string UnidentifiedSummary { get; set; } = string.Empty;

    public IReadOnlyList<TvSeasonEpisodeListItem> Episodes { get; set; } = [];

    public IReadOnlyList<TvSeasonCorrectionSourceItem> CorrectionSources { get; set; } = [];

    public bool IsUnidentified => IdentificationStatus == IdentificationStatus.Failed || !TmdbSeasonId.HasValue;

    public string SeasonNumberText => SeasonNumber == 0 ? "特别篇" : $"S{SeasonNumber:D2}";

    public string SeasonTitleText => TvDetailDisplayText.FormatSeasonTitle(SeasonNumber, Name, IsUnidentified);

    public string AirDateText => AirDate.HasValue
        ? AirDate.Value.ToString("yyyy-MM-dd")
        : AirYear?.ToString() ?? "-";

    public string ProgressText => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount}";

    public string InLibraryText => InLibraryEpisodeCount > 0 ? $"有播放源 {InLibraryEpisodeCount} 集" : "暂无播放源";

    public string EpisodeCountText => $"{InLibraryEpisodeCount} / {TotalEpisodeCount} 集有播放源";

    public string WatchedCountText => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount} 集";

    public string IdentificationStatusText => TvDetailDisplayText.FormatIdentificationStatus(IdentificationStatus);

    public bool IsSeasonWatched => TotalEpisodeCount > 0
        ? Episodes.Count >= TotalEpisodeCount && WatchedEpisodeCount >= TotalEpisodeCount
        : Episodes.Count > 0 && WatchedEpisodeCount >= Episodes.Count;

    public bool IsSeasonUnwatched => !IsSeasonWatched;
}

public sealed class TvSeasonEpisodeListItem : INotifyPropertyChanged
{
    private bool _isWatched;

    public event PropertyChangedEventHandler? PropertyChanged;
    public int EpisodeId { get; set; }

    public int EpisodeNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string OverviewDisplayText => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public DateTime? AirDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public bool IsWatched
    {
        get => _isWatched;
        set
        {
            if (_isWatched == value)
            {
                return;
            }

            _isWatched = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WatchedText));
            OnPropertyChanged(nameof(WatchedActionText));
            OnPropertyChanged(nameof(UnwatchedActionText));
        }
    }

    public DateTime? LastPlayedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public int DurationWatchedSeconds { get; set; }

    public string StillRemoteUrl { get; set; } = string.Empty;

    public string SourceSummary { get; set; } = string.Empty;

    public bool HasPlayableSource { get; set; }

    public int ActiveSourceCount { get; set; }

    public string EpisodeNumberText => $"E{EpisodeNumber:D2}";

    public string AirDateText => AirDate.HasValue ? AirDate.Value.ToString("yyyy-MM-dd") : "-";

    public string RuntimeText => RuntimeMinutes.HasValue && RuntimeMinutes.Value > 0
        ? $"{RuntimeMinutes.Value} 分钟"
        : "-";

    public string RuntimeClockText => RuntimeMinutes.HasValue && RuntimeMinutes.Value > 0
        ? TimeSpan.FromMinutes(RuntimeMinutes.Value).ToString(@"hh\:mm\:ss")
        : "-";

    public string WatchedText => IsWatched ? "已看" : "未看";

    public string LastPlayedText => LastPlayedAt.HasValue
        ? LastPlayedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "-";

    public string SourceCountText => $"播放源{ActiveSourceCount}个";

    public string PositionText => LastPlayPositionSeconds > 0
        ? TimeSpan.FromSeconds(LastPlayPositionSeconds).ToString(@"hh\:mm\:ss")
        : "-";

    public string ProgressText => LastPlayPositionSeconds > 0
        ? $"进度 {PositionText}"
        : "暂无进度";

    public string PlayButtonText => HasPlayableSource ? "播放" : "暂无播放源";

    public string WatchedActionText => IsWatched ? "已看" : "标记已看";

    public string UnwatchedActionText => IsWatched ? "标记未看" : "未看";

    public bool IsTargetEpisode { get; set; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class TvSeasonCorrectionSourceItem
{
    public int MediaFileId { get; set; }

    public int EpisodeId { get; set; }

    public int EpisodeNumber { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string SourceSummary { get; set; } = string.Empty;

    public IReadOnlyList<TvSeasonCorrectionPlaybackSourceItem> SourceOptions { get; set; } = [];

    public string EpisodeNumberText => $"E{EpisodeNumber:D2}";

    public string SafeFileName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FileName) ? string.Empty : Path.GetFileName(FileName.Trim());
            return string.IsNullOrWhiteSpace(name) ? $"MediaFile {MediaFileId}" : name;
        }
    }

    public string SafeFilePath => string.IsNullOrWhiteSpace(FilePath) ? SafeFileName : FilePath;
}

public sealed class TvSeasonCorrectionPlaybackSourceItem
{
    public int MediaFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public ProtocolType ProtocolType { get; set; }

    public bool IsDefault { get; set; }

    public string SourceTypeText => MediaSourceDisplayText.FormatSourceType(ProtocolType);

    public string SafeFileName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FileName) ? string.Empty : Path.GetFileName(FileName.Trim());
            return string.IsNullOrWhiteSpace(name) ? $"MediaFile {MediaFileId}" : name;
        }
    }

    public string SafeFilePath => string.IsNullOrWhiteSpace(FilePath) ? SafeFileName : FilePath;

    public string DisplayText => IsDefault
        ? $"默认源 · {SourceTypeText} · {SafeFilePath}"
        : $"{SourceTypeText} · {SafeFilePath}";
}

internal static class TvDetailDisplayText
{
    private static readonly Regex GenericSeasonNameRegex = new(@"^S\d{1,4}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GenericEpisodeNameRegex = new(@"^(E\d{1,4}|第\d{1,4}集)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string FormatSeasonTitle(int seasonNumber, string? name, bool isUnidentified = false)
    {
        var label = seasonNumber == 0 ? "特别篇" : $"第{seasonNumber}季";
        var unidentifiedSuffix = isUnidentified ? "（未识别）" : string.Empty;
        return ShouldHideSeasonName(label, name)
            ? $"{label}{unidentifiedSuffix}"
            : $"{label}  {name!.Trim()}{unidentifiedSuffix}";
    }

    public static string FormatEpisodeTitle(int seasonNumber, int episodeNumber, string? title, string? fallbackTitle)
    {
        var prefix = $"S{seasonNumber:D2}E{episodeNumber:D2}";
        var name = FirstNonEmpty(title, fallbackTitle);
        return ShouldHideEpisodeName(name) ? prefix : $"{prefix}  {name}";
    }

    public static bool ShouldHideSeasonName(string label, string? name)
    {
        var normalizedName = Normalize(name);
        return string.IsNullOrEmpty(normalizedName)
               || string.Equals(Normalize(label), normalizedName, StringComparison.OrdinalIgnoreCase)
               || GenericSeasonNameRegex.IsMatch(normalizedName);
    }

    private static bool ShouldHideEpisodeName(string? name)
    {
        var normalizedName = Normalize(name);
        return string.IsNullOrEmpty(normalizedName) || GenericEpisodeNameRegex.IsMatch(normalizedName);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
    }

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
            (true, true) => "本地/网盘",
            (true, false) => "本地",
            (false, true) => "网盘",
            _ => "暂无播放源"
        };
    }
}
