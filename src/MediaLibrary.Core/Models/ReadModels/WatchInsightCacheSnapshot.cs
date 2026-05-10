namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WatchInsightCacheSnapshot
{
    public string Kind { get; set; } = string.Empty;

    public string ScopeKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string SourceFingerprint { get; set; } = string.Empty;

    public DateTime RefreshedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsStale { get; set; }

    public string? LastError { get; set; }

    public DateTime? LastAutoRefreshAtUtc { get; set; }

    public DateTime? LastManualRefreshAtUtc { get; set; }
}
