using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class OnlineSubtitleBindingQueryService : IOnlineSubtitleBindingQueryService
{
    public async Task<IReadOnlyList<OnlineSubtitleBindingListItem>> GetActiveBindingsAsync(
        int? movieId,
        int? episodeId,
        CancellationToken cancellationToken = default)
    {
        if ((movieId is null || movieId <= 0) && (episodeId is null || episodeId <= 0))
        {
            return [];
        }

        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var query = dbContext.OnlineSubtitleBindings
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            query = episodeId is > 0
                ? query.Where(x => x.EpisodeId == episodeId.Value)
                : query.Where(x => x.MovieId == movieId!.Value);

            var rows = await query
                .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Select(
                    x => new OnlineSubtitleBindingListItem
                    {
                        Id = x.Id,
                        MovieId = x.MovieId,
                        EpisodeId = x.EpisodeId,
                        Provider = x.Provider,
                        ProviderSubtitleId = x.ProviderSubtitleId,
                        ProviderFileId = x.ProviderFileId,
                        LanguageCode = x.LanguageCode,
                        LanguageName = x.LanguageName,
                        DisplayName = x.DisplayName,
                        ReleaseName = x.ReleaseName,
                        FileName = x.FileName,
                        CacheRelativePath = x.CacheRelativePath,
                        Format = x.Format,
                        Extension = x.Extension,
                        CreatedAt = x.CreatedAt
                    })
                .ToListAsync(cancellationToken);

            var cacheRoot = Path.GetFullPath(AppPaths.GetOnlineSubtitleCacheDirectory());
            foreach (var row in rows)
            {
                row.HasCacheFile = HasManagedCacheFile(cacheRoot, row.CacheRelativePath);
            }

            return rows;
        }
        catch (SqliteException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private static bool HasManagedCacheFile(string cacheRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            var combined = Path.GetFullPath(Path.Combine(cacheRoot, relativePath));
            return combined.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase)
                   && File.Exists(combined);
        }
        catch
        {
            return false;
        }
    }
}
