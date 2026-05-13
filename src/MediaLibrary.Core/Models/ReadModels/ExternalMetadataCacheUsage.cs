namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ExternalMetadataCacheUsage
{
    public int ManagedEntryCount { get; init; }

    public long EstimatedBytes { get; init; }
}

public sealed class ExternalMetadataCacheClearResult
{
    public bool Succeeded { get; init; }

    public int DeletedEntryCount { get; init; }

    public long EstimatedFreedBytes { get; init; }

    public string? Error { get; init; }
}
