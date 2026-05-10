using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCacheChangedEventArgs : EventArgs
{
    public int MediaFileId { get; init; }

    public int SourceConnectionId { get; init; }

    public VideoCacheStatus Status { get; init; }

    public double ProgressPercent { get; init; }

    public string? Error { get; init; }
}
