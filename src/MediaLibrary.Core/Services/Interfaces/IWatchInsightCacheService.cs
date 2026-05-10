using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IWatchInsightCacheService
{
    Task<WatchInsightCacheSnapshot?> GetAsync(
        string kind,
        string scopeKey,
        CancellationToken cancellationToken = default);

    Task<WatchInsightCacheSnapshot> UpsertAsync(
        string kind,
        string scopeKey,
        string payloadJson,
        string sourceFingerprint,
        DateTime? expiresAtUtc,
        bool isManualRefresh,
        CancellationToken cancellationToken = default);

    Task MarkStaleAsync(
        string kind,
        string? scopeKey = null,
        CancellationToken cancellationToken = default);

    Task SetErrorAsync(
        string kind,
        string scopeKey,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task<int> ClearAsync(
        string? kind = null,
        string? scopeKey = null,
        CancellationToken cancellationToken = default);
}
