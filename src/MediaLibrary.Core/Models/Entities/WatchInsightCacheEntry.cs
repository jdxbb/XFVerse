namespace MediaLibrary.Core.Models.Entities;

public sealed class WatchInsightCacheEntry
{
    public int Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string ScopeKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string SourceFingerprint { get; set; } = string.Empty;

    public DateTime RefreshedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsStale { get; set; }

    public string? LastError { get; set; }

    public DateTime? LastAutoRefreshAtUtc { get; set; }

    public DateTime? LastManualRefreshAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
