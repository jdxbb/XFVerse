using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WatchInsightCacheService : IWatchInsightCacheService
{
    public async Task<WatchInsightCacheSnapshot?> GetAsync(
        string kind,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = NormalizeRequired(kind, nameof(kind));
        var normalizedScopeKey = NormalizeRequired(scopeKey, nameof(scopeKey));

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entry = await dbContext.WatchInsightCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Kind == normalizedKind && x.ScopeKey == normalizedScopeKey,
                cancellationToken);

        return entry is null ? null : ToSnapshot(entry);
    }

    public async Task<WatchInsightCacheSnapshot> UpsertAsync(
        string kind,
        string scopeKey,
        string payloadJson,
        string sourceFingerprint,
        DateTime? expiresAtUtc,
        bool isManualRefresh,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = NormalizeRequired(kind, nameof(kind));
        var normalizedScopeKey = NormalizeRequired(scopeKey, nameof(scopeKey));
        var now = DateTime.UtcNow;

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entry = await dbContext.WatchInsightCacheEntries
            .FirstOrDefaultAsync(
                x => x.Kind == normalizedKind && x.ScopeKey == normalizedScopeKey,
                cancellationToken);

        if (entry is null)
        {
            entry = new WatchInsightCacheEntry
            {
                Kind = normalizedKind,
                ScopeKey = normalizedScopeKey,
                CreatedAtUtc = now
            };
            dbContext.WatchInsightCacheEntries.Add(entry);
        }

        entry.PayloadJson = payloadJson ?? string.Empty;
        entry.SourceFingerprint = sourceFingerprint ?? string.Empty;
        entry.RefreshedAtUtc = now;
        entry.ExpiresAtUtc = expiresAtUtc;
        entry.IsStale = false;
        entry.LastError = null;
        entry.UpdatedAtUtc = now;

        if (isManualRefresh)
        {
            entry.LastManualRefreshAtUtc = now;
        }
        else
        {
            entry.LastAutoRefreshAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSnapshot(entry);
    }

    public async Task MarkStaleAsync(
        string kind,
        string? scopeKey = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = NormalizeRequired(kind, nameof(kind));
        var normalizedScopeKey = NormalizeOptional(scopeKey);
        var now = DateTime.UtcNow;

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var query = dbContext.WatchInsightCacheEntries
            .Where(x => x.Kind == normalizedKind);

        if (!string.IsNullOrWhiteSpace(normalizedScopeKey))
        {
            query = query.Where(x => x.ScopeKey == normalizedScopeKey);
        }

        var entries = await query.ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.IsStale = true;
            entry.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetErrorAsync(
        string kind,
        string scopeKey,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = NormalizeRequired(kind, nameof(kind));
        var normalizedScopeKey = NormalizeRequired(scopeKey, nameof(scopeKey));
        var now = DateTime.UtcNow;

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entry = await dbContext.WatchInsightCacheEntries
            .FirstOrDefaultAsync(
                x => x.Kind == normalizedKind && x.ScopeKey == normalizedScopeKey,
                cancellationToken);

        if (entry is null)
        {
            return;
        }

        entry.LastError = string.IsNullOrWhiteSpace(lastError) ? null : lastError.Trim();
        entry.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ClearAsync(
        string? kind = null,
        string? scopeKey = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = NormalizeOptional(kind);
        var normalizedScopeKey = NormalizeOptional(scopeKey);

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var query = dbContext.WatchInsightCacheEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedKind))
        {
            query = query.Where(x => x.Kind == normalizedKind);
        }

        if (!string.IsNullOrWhiteSpace(normalizedScopeKey))
        {
            query = query.Where(x => x.ScopeKey == normalizedScopeKey);
        }

        var entries = await query.ToListAsync(cancellationToken);
        dbContext.WatchInsightCacheEntries.RemoveRange(entries);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    private static WatchInsightCacheSnapshot ToSnapshot(WatchInsightCacheEntry entry)
    {
        return new WatchInsightCacheSnapshot
        {
            Kind = entry.Kind,
            ScopeKey = entry.ScopeKey,
            PayloadJson = entry.PayloadJson,
            SourceFingerprint = entry.SourceFingerprint,
            RefreshedAtUtc = entry.RefreshedAtUtc,
            ExpiresAtUtc = entry.ExpiresAtUtc,
            IsStale = entry.IsStale,
            LastError = entry.LastError,
            LastAutoRefreshAtUtc = entry.LastAutoRefreshAtUtc,
            LastManualRefreshAtUtc = entry.LastManualRefreshAtUtc
        };
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
