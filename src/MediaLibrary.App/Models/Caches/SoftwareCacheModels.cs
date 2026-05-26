namespace MediaLibrary.App.Models.Caches;

public enum SoftwareCacheCategoryKind
{
    PosterCache = 0,
    OtherCache = 1,
    SubtitleCache = 2
}

public sealed class SoftwareCacheCategoryModel
{
    public SoftwareCacheCategoryKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public long UsedBytes { get; init; }

    public int ItemCount { get; init; }

    public long ClearableBytes { get; init; }

    public int ClearableItemCount { get; init; }

    public string DetailText { get; init; } = string.Empty;

    public bool IsClearable { get; init; }

    public string ClearUnavailableReason { get; init; } = string.Empty;
}

public sealed class SoftwareCacheOverview
{
    public SoftwareCacheCategoryModel PosterCache { get; init; } = new();

    public SoftwareCacheCategoryModel OtherCache { get; init; } = new();

    public SoftwareCacheCategoryModel SubtitleCache { get; init; } = new();

    public long PosterCacheMaxBytes { get; init; } = PosterCacheDefaults.DefaultMaxBytes;
}

public sealed class SoftwareCacheClearResult
{
    public SoftwareCacheCategoryKind Kind { get; init; }

    public bool Succeeded { get; init; }

    public int DeletedItemCount { get; init; }

    public long FreedBytes { get; init; }

    public string? Error { get; init; }
}
