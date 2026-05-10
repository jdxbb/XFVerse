using System.Text.Json;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

internal static class ExternalMetadataPersistentCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<PersistentCacheReadResult<T>> TryGetAsync<T>(
        string provider,
        string cacheType,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(cacheType)
            || string.IsNullOrWhiteSpace(cacheKey))
        {
            return PersistentCacheReadResult<T>.Miss();
        }

        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var entry = await dbContext.ExternalMetadataCaches
                .FirstOrDefaultAsync(
                    x => x.Provider == provider
                         && x.CacheType == cacheType
                         && x.CacheKey == cacheKey,
                    cancellationToken);

            if (entry is null)
            {
                return PersistentCacheReadResult<T>.Miss();
            }

            var now = DateTime.UtcNow;
            if (entry.ExpiresAtUtc <= now)
            {
                await TryRemoveAsync(dbContext, entry, cancellationToken);
                return PersistentCacheReadResult<T>.Miss();
            }

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(entry.PayloadJson, JsonOptions);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                await TryRemoveAsync(dbContext, entry, cancellationToken);
                return PersistentCacheReadResult<T>.Miss();
            }

            if (value is null)
            {
                await TryRemoveAsync(dbContext, entry, cancellationToken);
                return PersistentCacheReadResult<T>.Miss();
            }

            entry.LastHitAtUtc = now;
            entry.HitCount++;
            await TrySaveChangesAsync(dbContext, cancellationToken);
            return PersistentCacheReadResult<T>.Hit(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return PersistentCacheReadResult<T>.Miss();
        }
    }

    public static async Task SetAsync<T>(
        string provider,
        string cacheType,
        string cacheKey,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            || string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(cacheType)
            || string.IsNullOrWhiteSpace(cacheKey)
            || ttl <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var payloadJson = JsonSerializer.Serialize(value, JsonOptions);
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var entry = await dbContext.ExternalMetadataCaches
                .FirstOrDefaultAsync(
                    x => x.Provider == provider
                         && x.CacheType == cacheType
                         && x.CacheKey == cacheKey,
                    cancellationToken);

            if (entry is null)
            {
                entry = new ExternalMetadataCache
                {
                    Provider = provider,
                    CacheType = cacheType,
                    CacheKey = cacheKey
                };
                dbContext.ExternalMetadataCaches.Add(entry);
            }

            entry.PayloadJson = payloadJson;
            entry.CreatedAtUtc = now;
            entry.ExpiresAtUtc = now.Add(ttl);
            entry.LastHitAtUtc = null;
            entry.HitCount = 0;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Persistent cache writes must never block metadata resolution.
        }
    }

    private static async Task TryRemoveAsync(
        AppDbContext dbContext,
        ExternalMetadataCache entry,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.ExternalMetadataCaches.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Stale or corrupt cache cleanup is best-effort only.
        }
    }

    private static async Task TrySaveChangesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Hit counters are diagnostic-only and must not affect cache hits.
        }
    }
}

internal sealed record PersistentCacheReadResult<T>(bool IsHit, T? Value)
{
    public static PersistentCacheReadResult<T> Hit(T value)
    {
        return new PersistentCacheReadResult<T>(true, value);
    }

    public static PersistentCacheReadResult<T> Miss()
    {
        return new PersistentCacheReadResult<T>(false, default);
    }
}
