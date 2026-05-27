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
        return await GetUsageAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExternalMetadataCacheClearResult> ClearManagedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var usage = await GetUsageAsync(dbContext, cancellationToken).ConfigureAwait(false);
            var deletedCount = usage.ManagedEntryCount == 0
                ? 0
                : await ApplyManagedScopeFilter(dbContext.ExternalMetadataCaches)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);


            return new ExternalMetadataCacheClearResult
            {
                Succeeded = true,
                DeletedEntryCount = deletedCount,
                EstimatedFreedBytes = usage.EstimatedBytes
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

    private static async Task<ExternalMetadataCacheUsage> GetUsageAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var summary = await ApplyManagedScopeFilter(dbContext.ExternalMetadataCaches)
            .AsNoTracking()
            .Select(
                entry => new
                {
                    EstimatedBytes =
                        (long)entry.Provider.Length
                        + entry.CacheType.Length
                        + entry.CacheKey.Length
                        + entry.PayloadJson.Length
                })
            .GroupBy(_ => 1)
            .Select(
                group => new
                {
                    Count = group.Count(),
                    EstimatedBytes = group.Sum(entry => entry.EstimatedBytes)
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ExternalMetadataCacheUsage
        {
            ManagedEntryCount = summary?.Count ?? 0,
            EstimatedBytes = summary?.EstimatedBytes ?? 0
        };
    }
}
