using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class MovieSourceItem
{
    public int MediaFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

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

    public IReadOnlyList<SubtitleBindingItem> SubtitleBindings { get; set; } = [];

    public string SourceTypeText => MediaSourceDisplayText.FormatSourceType(ProtocolType);

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

    public string ProbeStatusShortText => MediaSourceDisplayText.FormatProbeStatusShort(MediaProbeStatus);

    public string ProbeErrorText => MediaSourceDisplayText.FormatProbeError(MediaProbeError);

    public string LastPlayedText => MediaSourceDisplayText.FormatDateTime(LastPlayedAt);

    public string LastPlayPositionText => MediaSourceDisplayText.FormatDuration(LastPlayPositionSeconds);

    public string LastPlayPositionProgressText =>
        $"{MediaSourceDisplayText.FormatDuration(LastPlayPositionSeconds)} / {DurationText}";

    public string DefaultSourceText => IsDefault ? "默认源" : "播放源";

    public string CorrectionSourceDisplayText => string.IsNullOrWhiteSpace(FilePath) ? FileName : FilePath;

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
