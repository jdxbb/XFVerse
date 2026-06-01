using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvEpisodeDetailModel
{
    public int EpisodeId { get; set; }

    public int SeasonId { get; set; }

    public int SeriesId { get; set; }

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public string SeriesName { get; set; } = string.Empty;

    public string SeriesOriginalName { get; set; } = string.Empty;

    public string SeriesCountry { get; set; } = string.Empty;

    public string SeriesLanguage { get; set; } = string.Empty;

    public string SeriesDirectorText { get; set; } = string.Empty;

    public string SeriesWriterText { get; set; } = string.Empty;

    public string SeriesActorsText { get; set; } = string.Empty;

    public string SeriesNetworksText { get; set; } = string.Empty;

    public string SeriesProductionCompaniesText { get; set; } = string.Empty;

    public string GenreDisplay { get; set; } = string.Empty;

    public string SeasonName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string FallbackTitle { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public DateTime? AirDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public bool IsWatched { get; set; }

    public DateTime? LastPlayedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public int DurationWatchedSeconds { get; set; }

    public string StillDisplayUrl { get; set; } = string.Empty;

    public int ActiveSourceCount { get; set; }

    public string SourceSummary { get; set; } = string.Empty;

    public int? DefaultMediaFileId { get; set; }

    public IReadOnlyList<TvEpisodeSourceItem> Sources { get; set; } = [];

    public IdentificationStatus SeasonIdentificationStatus { get; set; }

    public bool IsUnidentified => SeasonIdentificationStatus == IdentificationStatus.Failed;

    public string SeasonNumberText => SeasonNumber == 0 ? "特别篇" : $"S{SeasonNumber:D2}";

    public string EpisodeNumberText => EpisodeNumber > 0 ? $"E{EpisodeNumber:D2}" : "E--";

    public string DisplayTitle => TvDetailDisplayText.FormatEpisodeTitle(SeasonNumber, EpisodeNumber, Title, FallbackTitle);

    public string DisplayOverview => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public string AirDateText => AirDate.HasValue ? AirDate.Value.ToString("yyyy-MM-dd") : "-";

    public string RuntimeText => RuntimeMinutes.HasValue && RuntimeMinutes.Value > 0
        ? TimeSpan.FromMinutes(RuntimeMinutes.Value).ToString(@"hh\:mm\:ss")
        : "-";

    public string WatchedText => IsWatched ? "已看" : "未看";

    public string IdentificationStatusText => IsUnidentified
        ? "未识别 / 待修正"
        : TvDetailDisplayText.FormatIdentificationStatus(SeasonIdentificationStatus);

    public string LastPlayedText => LastPlayedAt.HasValue
        ? LastPlayedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "-";

    public string PositionText => LastPlayPositionSeconds > 0
        ? TimeSpan.FromSeconds(LastPlayPositionSeconds).ToString(@"hh\:mm\:ss")
        : "-";

    public string ProgressText => IsWatched
        ? "已看"
        : LastPlayPositionSeconds > 0
            ? $"进度 {PositionText}"
            : "暂无进度";

    public string SourceCountText => ActiveSourceCount > 0
        ? $"播放源：{ActiveSourceCount}"
        : "暂无播放源";

    public bool HasSources => ActiveSourceCount > 0;

    public bool HasNoSources => !HasSources;

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "-";
    }
}

public sealed class TvEpisodeSourceItem
{
    public int MediaFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string RemoteUri { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public int? ResolutionWidth { get; set; }

    public int? ResolutionHeight { get; set; }

    public string? VideoCodec { get; set; }

    public string? AudioCodec { get; set; }

    public int? AudioChannels { get; set; }

    public int? AudioSampleRate { get; set; }

    public int? OverallBitrateKbps { get; set; }

    public int? VideoBitrateKbps { get; set; }

    public int? AudioBitrateKbps { get; set; }

    public double? VideoFrameRate { get; set; }

    public MediaProbeStatus MediaProbeStatus { get; set; } = MediaProbeStatus.NotProbed;

    public string? MediaProbeError { get; set; }

    public DateTime? MediaProbedAt { get; set; }

    public ProtocolType ProtocolType { get; set; } = ProtocolType.WebDav;

    public DateTime? LastPlayedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public bool IsDefault { get; set; }

    public string DisplayFileName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                return "-";
            }

            var fileName = Path.GetFileName(FileName.Trim());
            return string.IsNullOrWhiteSpace(fileName) ? FileName.Trim() : fileName;
        }
    }

    public string SourceTypeText => MediaSourceDisplayText.FormatSourceType(ProtocolType);

    public string LocationText => MediaSourceDisplayText.FormatSafeLocation(ProtocolType, FilePath, RemoteUri);

    public string FormattedFileSize => MediaSourceDisplayText.FormatFileSize(FileSize);

    public string DurationText => MediaSourceDisplayText.FormatDuration(DurationSeconds);

    public string ResolutionText => MediaSourceDisplayText.FormatResolution(ResolutionWidth, ResolutionHeight);

    public string ResolutionShortText => MediaSourceDisplayText.FormatResolutionShortLabel(ResolutionWidth, ResolutionHeight);

    public string ResolutionRawText => MediaSourceDisplayText.FormatRawResolution(ResolutionWidth, ResolutionHeight);

    public string ExtensionDisplayText => string.IsNullOrWhiteSpace(Extension)
        ? MediaSourceDisplayText.Unknown
        : Extension.TrimStart('.');

    public string VideoCodecText => MediaSourceDisplayText.FormatVideoCodec(VideoCodec);

    public string AudioText => MediaSourceDisplayText.FormatAudio(AudioCodec, AudioChannels);

    public string BitrateText => MediaSourceDisplayText.FormatBitrate(
        MediaSourceDisplayText.SelectDisplayBitrateKbps(OverallBitrateKbps, VideoBitrateKbps, AudioBitrateKbps));

    public string VideoBitrateText => MediaSourceDisplayText.FormatBitrate(VideoBitrateKbps);

    public string FrameRateText => MediaSourceDisplayText.FormatFrameRate(VideoFrameRate);

    public string TechnicalSummary => MediaSourceDisplayText.BuildTechnicalSummary(
        ResolutionWidth,
        ResolutionHeight,
        VideoCodec,
        AudioCodec,
        AudioChannels,
        OverallBitrateKbps,
        VideoBitrateKbps,
        AudioBitrateKbps);

    public string ProbeStatusText => MediaSourceDisplayText.FormatProbeStatus(
        MediaProbeStatus,
        MediaSourceDisplayText.HasProbeTechnicalInfo(
            DurationSeconds,
            ResolutionWidth,
            ResolutionHeight,
            VideoCodec,
            AudioCodec,
            AudioChannels,
            OverallBitrateKbps,
            VideoBitrateKbps,
            AudioBitrateKbps));

    public string ProbeErrorText => MediaSourceDisplayText.FormatProbeError(MediaProbeError);

    public string ProbeStatusShortText => MediaSourceDisplayText.FormatProbeStatusShort(MediaProbeStatus);

    public string LastPlayedText => MediaSourceDisplayText.FormatDateTime(LastPlayedAt);

    public string LastPlayPositionText => MediaSourceDisplayText.FormatDuration(LastPlayPositionSeconds);

    public string LastPlayPositionProgressText =>
        $"{MediaSourceDisplayText.FormatDuration(LastPlayPositionSeconds)} / {DurationText}";

    public string DefaultSourceText => IsDefault ? "默认源" : "播放源";

    public string PlaybackHistoryText
    {
        get
        {
            if (!LastPlayedAt.HasValue && LastPlayPositionSeconds <= 0)
            {
                return "暂无播放记录";
            }

            return $"最近播放：{LastPlayedText} · 位置：{LastPlayPositionText}";
        }
    }

}
