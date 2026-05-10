namespace MediaLibrary.Core.Models.Entities;

public sealed class ExternalMetadataCache
{
    public int Id { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string CacheType { get; set; } = string.Empty;

    public string CacheKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? LastHitAtUtc { get; set; }

    public int HitCount { get; set; }
}
