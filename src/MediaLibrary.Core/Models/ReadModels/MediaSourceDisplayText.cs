using System.Globalization;
using System.Net;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

internal static class MediaSourceDisplayText
{
    public const string Unknown = "未知";
    private const string UnknownResolution = "分辨率未知";
    private const string UnknownVideoCodec = "编码未知";
    private const string UnknownAudio = "音频未知";
    private const string UnknownBitrate = "码率未知";

    public static string FormatSourceType(ProtocolType protocolType)
    {
        return protocolType switch
        {
            ProtocolType.Local => "本地",
            ProtocolType.WebDav => "网盘",
            _ => protocolType.ToString()
        };
    }

    public static string FormatSafeLocation(ProtocolType protocolType, string filePath, string? remoteUri = null)
    {
        var location = protocolType == ProtocolType.Local
            ? BuildSafePathLocation(filePath)
            : BuildSafeWebDavLocation(filePath, remoteUri);

        return protocolType == ProtocolType.Local
            ? $"本地位置：{location}"
            : $"网盘位置：{location}";
    }

    public static string FormatFileSize(long fileSize)
    {
        if (fileSize <= 0)
        {
            return Unknown;
        }

        const double bytesPerMegabyte = 1024d * 1024d;
        const double bytesPerGigabyte = bytesPerMegabyte * 1024d;

        return fileSize >= bytesPerGigabyte
            ? string.Create(CultureInfo.InvariantCulture, $"{fileSize / bytesPerGigabyte:0.##} GB")
            : string.Create(CultureInfo.InvariantCulture, $"{fileSize / bytesPerMegabyte:0.##} MB");
    }

    public static string FormatDuration(int? seconds)
    {
        if (!seconds.HasValue || seconds.Value <= 0)
        {
            return Unknown;
        }

        return FormatDuration(seconds.Value);
    }

    public static string FormatDuration(int seconds)
    {
        if (seconds <= 0)
        {
            return Unknown;
        }

        var duration = TimeSpan.FromSeconds(seconds);
        return duration.TotalHours >= 1
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{duration.Minutes:D2}:{duration.Seconds:D2}");
    }

    public static string FormatResolution(int? width, int? height)
    {
        var effectiveWidth = width.GetValueOrDefault();
        var effectiveHeight = height.GetValueOrDefault();
        if (effectiveWidth <= 0 || effectiveHeight <= 0)
        {
            return UnknownResolution;
        }

        var actual = FormatRawResolution(effectiveWidth, effectiveHeight);
        var label = FormatResolutionShortLabel(effectiveWidth, effectiveHeight);
        return string.IsNullOrWhiteSpace(label) || label == Unknown || label == actual
            ? actual
            : $"{label} · {actual}";
    }

    public static string FormatResolutionShortLabel(int? width, int? height)
    {
        var effectiveWidth = width.GetValueOrDefault();
        var effectiveHeight = height.GetValueOrDefault();
        if (effectiveWidth <= 0 || effectiveHeight <= 0)
        {
            return Unknown;
        }

        if (effectiveHeight >= 2160 || effectiveWidth >= 3840)
        {
            return "4K";
        }

        if (effectiveHeight >= 1440)
        {
            return "2K";
        }

        if (effectiveHeight >= 1080)
        {
            return "1080p";
        }

        return effectiveHeight >= 720
            ? "720p"
            : FormatRawResolution(effectiveWidth, effectiveHeight);
    }

    public static string FormatVideoCodec(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return UnknownVideoCodec;
        }

        return codec.Trim().ToLowerInvariant() switch
        {
            "hevc" or "h265" => "HEVC",
            "h264" or "avc" => "H.264",
            "av1" => "AV1",
            "mpeg4" => "MPEG-4",
            "vp9" => "VP9",
            _ => codec.Trim().ToUpperInvariant()
        };
    }

    public static string FormatAudioCodec(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return string.Empty;
        }

        return codec.Trim().ToLowerInvariant() switch
        {
            "aac" => "AAC",
            "ac3" => "AC3",
            "eac3" => "EAC3",
            "dts" => "DTS",
            "truehd" => "TrueHD",
            "flac" => "FLAC",
            "opus" => "Opus",
            _ => codec.Trim().ToUpperInvariant()
        };
    }

    public static string FormatAudioChannels(int? channels)
    {
        return channels.GetValueOrDefault() switch
        {
            1 => "1.0",
            2 => "2.0",
            6 => "5.1",
            8 => "7.1",
            > 0 => string.Create(CultureInfo.InvariantCulture, $"{channels!.Value}ch"),
            _ => string.Empty
        };
    }

    public static string FormatAudio(string? codec, int? channels)
    {
        var codecText = FormatAudioCodec(codec);
        var channelText = FormatAudioChannels(channels);
        if (!string.IsNullOrWhiteSpace(codecText) && !string.IsNullOrWhiteSpace(channelText))
        {
            return $"{codecText} {channelText}";
        }

        if (!string.IsNullOrWhiteSpace(codecText))
        {
            return codecText;
        }

        return !string.IsNullOrWhiteSpace(channelText) ? channelText : UnknownAudio;
    }

    public static string FormatBitrate(int? bitrateKbps)
    {
        if (!bitrateKbps.HasValue || bitrateKbps.Value <= 0)
        {
            return UnknownBitrate;
        }

        return bitrateKbps.Value >= 1000
            ? string.Create(CultureInfo.InvariantCulture, $"{bitrateKbps.Value / 1000d:0.0} Mbps")
            : string.Create(CultureInfo.InvariantCulture, $"{bitrateKbps.Value} Kbps");
    }

    public static int? SelectDisplayBitrateKbps(
        int? overallBitrateKbps,
        int? videoBitrateKbps,
        int? audioBitrateKbps)
    {
        if (overallBitrateKbps.GetValueOrDefault() > 0)
        {
            return overallBitrateKbps;
        }

        var video = videoBitrateKbps.GetValueOrDefault();
        var audio = audioBitrateKbps.GetValueOrDefault();
        if (video > 0 && audio > 0)
        {
            return video + audio;
        }

        if (video > 0)
        {
            return video;
        }

        return audio > 0 ? audio : null;
    }

    public static string BuildTechnicalSummary(
        int? width,
        int? height,
        string? videoCodec,
        string? audioCodec,
        int? audioChannels,
        int? overallBitrateKbps,
        int? videoBitrateKbps,
        int? audioBitrateKbps)
    {
        var resolution = FormatResolution(width, height);
        var video = FormatVideoCodec(videoCodec);
        var audio = FormatAudio(audioCodec, audioChannels);
        var bitrate = FormatBitrate(SelectDisplayBitrateKbps(overallBitrateKbps, videoBitrateKbps, audioBitrateKbps));

        var parts = new[] { resolution, video, audio, bitrate }
            .Where(part => !string.IsNullOrWhiteSpace(part)
                           && part != Unknown
                           && part != UnknownResolution
                           && part != UnknownVideoCodec
                           && part != UnknownAudio
                           && part != UnknownBitrate);

        return string.Join(" · ", parts);
    }

    public static bool HasProbeTechnicalInfo(
        int? durationSeconds,
        int? width,
        int? height,
        string? videoCodec,
        string? audioCodec,
        int? audioChannels,
        int? overallBitrateKbps,
        int? videoBitrateKbps,
        int? audioBitrateKbps)
    {
        return durationSeconds.GetValueOrDefault() > 0
               || (width.GetValueOrDefault() > 0 && height.GetValueOrDefault() > 0)
               || !string.IsNullOrWhiteSpace(videoCodec)
               || !string.IsNullOrWhiteSpace(audioCodec)
               || audioChannels.GetValueOrDefault() > 0
               || SelectDisplayBitrateKbps(overallBitrateKbps, videoBitrateKbps, audioBitrateKbps).GetValueOrDefault() > 0;
    }

    private static string FormatRawResolution(int width, int height)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{width}×{height}");
    }

    public static string FormatProbeStatus(MediaProbeStatus status)
    {
        return FormatProbeStatus(status, hasTechnicalInfo: true);
    }

    public static string FormatProbeStatus(MediaProbeStatus status, bool hasTechnicalInfo)
    {
        return status switch
        {
            MediaProbeStatus.NotProbed => "待探测（等待后台任务）",
            MediaProbeStatus.Pending => "探测中（后台读取媒体信息）",
            MediaProbeStatus.Success => hasTechnicalInfo
                ? "已完成（媒体信息已更新）"
                : "已探测（未读取到媒体信息）",
            MediaProbeStatus.Failed => "失败（暂未取得媒体信息）",
            MediaProbeStatus.Unavailable => "不可用（缺少 ffprobe）",
            MediaProbeStatus.Skipped => "已跳过（非可探测视频）",
            _ => Unknown
        };
    }

    public static string FormatProbeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return string.Empty;
        }

        var normalized = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (normalized.Contains("://", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("bearer", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("basic ", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            return "失败原因：错误信息已脱敏";
        }

        var text = normalized.Length <= 160 ? normalized : normalized[..160];
        return $"失败原因：{text}";
    }

    public static string FormatDateTime(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : Unknown;
    }

    public static string FormatVideoCacheStatus(VideoCacheStatus status, double progressPercent)
    {
        return status switch
        {
            VideoCacheStatus.NotCached => "未缓存",
            VideoCacheStatus.Downloading => string.Create(
                CultureInfo.InvariantCulture,
                $"缓存中 {Math.Clamp(progressPercent, 0d, 100d):0}%"),
            VideoCacheStatus.Cached => "已缓存",
            VideoCacheStatus.Failed => "缓存失败",
            VideoCacheStatus.Canceled => "缓存已取消",
            VideoCacheStatus.NotCacheable => "不可缓存",
            VideoCacheStatus.InUse => "正在使用，停止后可删除",
            _ => Unknown
        };
    }

    private static string BuildSafeWebDavLocation(string filePath, string? remoteUri)
    {
        var primaryPath = !string.IsNullOrWhiteSpace(filePath)
            ? DecodeForDisplay(filePath)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(primaryPath) && primaryPath.Trim() != "/")
        {
            return BuildSafePathLocation(primaryPath);
        }

        return BuildSafePathLocation(remoteUri ?? string.Empty);
    }

    private static string BuildSafePathLocation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var displayPath = ExtractDisplayPath(value);
        var parts = displayPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DecodeForDisplay)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Where(part => !part.Contains(':', StringComparison.Ordinal))
            .TakeLast(2)
            .ToArray();

        return parts.Length == 0 ? DisplayFallback(value) : string.Join(" / ", parts);
    }

    private static string ExtractDisplayPath(string value)
    {
        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.IsFile
                ? uri.LocalPath
                : DecodeForDisplay(uri.AbsolutePath);
        }

        return DecodeForDisplay(trimmed);
    }

    private static string DecodeForDisplay(string value)
    {
        var decoded = WebUtility.HtmlDecode(value.Trim());
        for (var attempt = 0; attempt < 2 && decoded.Contains('%', StringComparison.Ordinal); attempt++)
        {
            try
            {
                var next = Uri.UnescapeDataString(decoded);
                if (string.Equals(next, decoded, StringComparison.Ordinal))
                {
                    break;
                }

                decoded = next;
            }
            catch
            {
                break;
            }
        }

        return decoded;
    }

    private static string DisplayFallback(string value)
    {
        var fileName = Path.GetFileName(DecodeForDisplay(value));
        return string.IsNullOrWhiteSpace(fileName) ? "-" : fileName;
    }
}
