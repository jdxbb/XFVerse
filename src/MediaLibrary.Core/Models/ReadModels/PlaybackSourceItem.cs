using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class PlaybackSourceItem
{
    public int MediaFileId { get; set; }

    public int SourceConnectionId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string? RemoteUri { get; set; }

    public string PlaybackUrl { get; set; } = string.Empty;

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

    public MediaProbeStatus MediaProbeStatus { get; set; } = MediaProbeStatus.NotProbed;

    public string? MediaProbeError { get; set; }

    public DateTime? MediaProbedAt { get; set; }

    public ProtocolType ProtocolType { get; set; } = ProtocolType.WebDav;

    public DateTime? LastPlayedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public int ResumePositionSeconds { get; set; }

    public IReadOnlyList<PlaybackSubtitleItem> Subtitles { get; set; } = [];

    public VideoCacheStatus VideoCacheStatus { get; set; } = VideoCacheStatus.NotCached;

    public double VideoCacheProgressPercent { get; set; }

    public string? VideoCacheError { get; set; }

    public string SourceTypeText => MediaSourceDisplayText.FormatSourceType(ProtocolType);

    public string VideoCacheStatusText => MediaSourceDisplayText.FormatVideoCacheStatus(VideoCacheStatus, VideoCacheProgressPercent);

    public string VideoCacheSummaryText => ProtocolType == ProtocolType.WebDav ? VideoCacheStatusText : string.Empty;

    public string FormattedFileSize => MediaSourceDisplayText.FormatFileSize(FileSize);

    public string DurationText => MediaSourceDisplayText.FormatDuration(DurationSeconds);

    public string ResolutionText => MediaSourceDisplayText.FormatResolution(ResolutionWidth, ResolutionHeight);

    public string ResolutionShortText => MediaSourceDisplayText.FormatResolutionShortLabel(ResolutionWidth, ResolutionHeight);

    public string VideoCodecText => MediaSourceDisplayText.FormatVideoCodec(VideoCodec);

    public string AudioText => MediaSourceDisplayText.FormatAudio(AudioCodec, AudioChannels);

    public string BitrateText => MediaSourceDisplayText.FormatBitrate(
        MediaSourceDisplayText.SelectDisplayBitrateKbps(OverallBitrateKbps, VideoBitrateKbps, AudioBitrateKbps));

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

    public string LastPlayedText => MediaSourceDisplayText.FormatDateTime(LastPlayedAt);

    public string LastPlayPositionText => MediaSourceDisplayText.FormatDuration(LastPlayPositionSeconds);

    public string DefaultSourceText => IsDefault ? "默认源" : "播放源";

    public string ResumeText
    {
        get
        {
            var position = ResumePositionSeconds > 0 ? ResumePositionSeconds : LastPlayPositionSeconds;
            return position > 0 ? $"续播 {MediaSourceDisplayText.FormatDuration(position)}" : string.Empty;
        }
    }

    public string SourceSummaryText
    {
        get
        {
            var parts = new[]
                {
                    IsDefault ? DefaultSourceText : string.Empty,
                    SourceTypeText,
                    VideoCacheSummaryText,
                    TechnicalSummary,
                    ResumeText
                }
                .Where(part => !string.IsNullOrWhiteSpace(part) && part != MediaSourceDisplayText.Unknown);

            return string.Join(" · ", parts);
        }
    }

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
