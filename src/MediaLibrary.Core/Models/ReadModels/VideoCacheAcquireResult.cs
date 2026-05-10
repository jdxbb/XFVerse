namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCacheAcquireResult
{
    public static VideoCacheAcquireResult Miss { get; } = new();

    public bool IsHit => Lease is not null && !string.IsNullOrWhiteSpace(LocalFilePath);

    public string? LocalFilePath { get; init; }

    public VideoCachePlaybackLease? Lease { get; init; }
}
