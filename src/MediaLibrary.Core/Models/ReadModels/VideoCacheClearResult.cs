namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCacheClearResult
{
    public bool Succeeded { get; init; }

    public bool BlockedByActiveLease { get; init; }

    public int DeletedCount { get; init; }

    public int FailedCount { get; init; }

    public int SkippedActiveCount { get; init; }

    public int DeletedFullFileCount { get; init; }

    public int DeletedMpvSessionCount { get; init; }

    public int DeletedLegacyCount { get; init; }

    public long FreedBytes { get; init; }

    public long SkippedActiveBytes { get; init; }

    public string? Error { get; init; }
}
