using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCacheStatusResult
{
    public VideoCacheStatus Status { get; init; } = VideoCacheStatus.NotCached;

    public double ProgressPercent { get; init; }

    public string? LocalFilePath { get; init; }

    public string? Error { get; init; }
}
