namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCacheUsage
{
    public string CacheDirectory { get; init; } = string.Empty;

    public long UsedBytes { get; init; }

    public long FullFileBytes { get; init; }

    public long MpvSessionBytes { get; init; }

    public long LegacyBytes { get; init; }

    public long DownloadingBytes { get; init; }

    public long MaxBytes { get; init; }

    public int FullFileItemCount { get; init; }

    public int MpvSessionDirectoryCount { get; init; }

    public int LegacyItemCount { get; init; }
}
