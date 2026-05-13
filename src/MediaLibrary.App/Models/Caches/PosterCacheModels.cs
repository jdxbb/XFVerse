namespace MediaLibrary.App.Models.Caches;

public static class PosterCacheDefaults
{
    public const int DefaultMaxMegabytes = 512;
    public const long DefaultMaxBytes = DefaultMaxMegabytes * 1024L * 1024L;
}

public sealed class PosterCacheSettings
{
    public long MaxBytes { get; init; } = PosterCacheDefaults.DefaultMaxBytes;
}

public sealed class PosterCacheUsage
{
    public long UsedBytes { get; init; }

    public int FileCount { get; init; }
}

public sealed class PosterCacheClearResult
{
    public bool Succeeded { get; init; }

    public int DeletedFileCount { get; init; }

    public long FreedBytes { get; init; }

    public string? Error { get; init; }
}

public sealed class PosterCacheTrimResult
{
    public bool Succeeded { get; init; }

    public int DeletedFileCount { get; init; }

    public long FreedBytes { get; init; }

    public long UsedBytesAfterTrim { get; init; }

    public string? Error { get; init; }
}
