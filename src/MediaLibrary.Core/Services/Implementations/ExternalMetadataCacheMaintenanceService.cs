using System.Text;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class ExternalMetadataCacheMaintenanceService : IExternalMetadataCacheMaintenanceService
{
    public async Task<ExternalMetadataCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entries = await ApplyManagedScopeFilter(dbContext.ExternalMetadataCaches)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new ExternalMetadataCacheUsage
        {
            ManagedEntryCount = entries.Count,
            EstimatedBytes = entries.Sum(EstimateEntryBytes)
        };
    }

    public async Task<ExternalMetadataCacheClearResult> ClearManagedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var entries = await ApplyManagedScopeFilter(dbContext.ExternalMetadataCaches)
                .ToListAsync(cancellationToken);
            var estimatedBytes = entries.Sum(EstimateEntryBytes);

            dbContext.ExternalMetadataCaches.RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ExternalMetadataCacheClearResult
            {
                Succeeded = true,
                DeletedEntryCount = entries.Count,
                EstimatedFreedBytes = estimatedBytes
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ExternalMetadataCacheClearResult
            {
                Succeeded = false,
                Error = exception.GetType().Name
            };
        }
    }

    private static IQueryable<ExternalMetadataCache> ApplyManagedScopeFilter(IQueryable<ExternalMetadataCache> query)
    {
        return query.Where(
            entry => (entry.Provider == "TMDB"
                      && (entry.CacheType == "Search"
                          || entry.CacheType == "Detail"
                          || entry.CacheType == "ExternalIds"))
                     || (entry.Provider == "OMDb" && entry.CacheType == "Rating"));
    }

    private static long EstimateEntryBytes(ExternalMetadataCache entry)
    {
        return EstimateStringBytes(entry.Provider)
               + EstimateStringBytes(entry.CacheType)
               + EstimateStringBytes(entry.CacheKey)
               + EstimateStringBytes(entry.PayloadJson);
    }

    private static long EstimateStringBytes(string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
    }
}
