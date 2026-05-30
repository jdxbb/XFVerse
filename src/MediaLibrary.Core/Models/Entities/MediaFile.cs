using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class MediaFile
{
    public int Id { get; set; }

    public int SourceConnectionId { get; set; }

    public int? ScanPathId { get; set; }

    public int? MovieId { get; set; }

    public int? EpisodeId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string? RemoteUri { get; set; }

    public string Extension { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime? LastModifiedAt { get; set; }

    public MediaType MediaType { get; set; } = MediaType.Other;

    public int? DurationSeconds { get; set; }

    public int? ResolutionWidth { get; set; }

    public int? ResolutionHeight { get; set; }

    public string? HashValue { get; set; }

    public string? CodecInfo { get; set; }

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

    public int MediaProbeAttemptCount { get; set; }

    public DateTime? MediaProbedAt { get; set; }

    public long? MediaProbeFileSize { get; set; }

    public DateTime? MediaProbeLastModifiedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SourceConnection? SourceConnection { get; set; }

    public ScanPath? ScanPath { get; set; }

    public Movie? Movie { get; set; }

    public TvEpisode? Episode { get; set; }

    public Movie? DefaultForMovie { get; set; }

    public TvEpisode? DefaultForEpisode { get; set; }

    public ICollection<SubtitleBinding> SubtitleBindings { get; set; } = new List<SubtitleBinding>();

    public ICollection<SubtitleBinding> SubtitleBindingsAsSubtitle { get; set; } = new List<SubtitleBinding>();

    public ICollection<OnlineSubtitleBinding> OnlineSubtitleBindings { get; set; } = new List<OnlineSubtitleBinding>();

    public ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();
}
