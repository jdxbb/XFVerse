namespace MediaLibrary.Core.Models.ReadModels;

public sealed class OnlineSubtitleCacheSaveResult
{
    public string RelativePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public long Bytes { get; init; }
}

public sealed class OnlineSubtitleCacheUsage
{
    public long UsedBytes { get; init; }

    public int FileCount { get; init; }

    public long ReferencedBytes { get; init; }

    public int ReferencedFileCount { get; init; }

    public long OrphanBytes { get; init; }

    public int OrphanFileCount { get; init; }

    public int UnknownFileCount { get; init; }

    public bool ReferenceScanSucceeded { get; init; } = true;
}

public sealed class OnlineSubtitleCacheClearResult
{
    public bool Succeeded { get; init; }

    public int DeletedFileCount { get; init; }

    public long FreedBytes { get; init; }

    public int FailedFileCount { get; init; }

    public string Error { get; init; } = string.Empty;
}
