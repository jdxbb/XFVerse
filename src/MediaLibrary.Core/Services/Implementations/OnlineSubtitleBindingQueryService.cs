using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class OnlineSubtitleBindingQueryService : IOnlineSubtitleBindingService
{
    private const string DefaultProvider = "OpenSubtitles";
    private const int MetadataJsonMaxLength = 8000;

    public async Task<IReadOnlyList<OnlineSubtitleBindingListItem>> GetActiveBindingsAsync(
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken = default)
    {
        if ((movieId is null || movieId <= 0)
            && (episodeId is null || episodeId <= 0)
            && (mediaFileId is null || mediaFileId <= 0))
        {
            return [];
        }

        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var query = dbContext.OnlineSubtitleBindings
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            query = query.Where(
                x => (movieId.HasValue && x.MovieId == movieId.Value)
                     || (episodeId.HasValue && x.EpisodeId == episodeId.Value)
                     || (mediaFileId.HasValue && x.MediaFileId == mediaFileId.Value));

            var rows = await query
                .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Select(
                    x => new OnlineSubtitleBindingListItem
                    {
                        Id = x.Id,
                        MovieId = x.MovieId,
                        EpisodeId = x.EpisodeId,
                        MediaFileId = x.MediaFileId,
                        TargetKind = x.MovieId.HasValue
                            ? "movie"
                            : x.EpisodeId.HasValue
                                ? "episode"
                                : "mediaFile",
                        Provider = x.Provider,
                        ProviderSubtitleId = x.ProviderSubtitleId,
                        ProviderFileId = x.ProviderFileId,
                        LanguageCode = x.LanguageCode,
                        LanguageName = x.LanguageName,
                        DisplayName = x.DisplayName,
                        ReleaseName = x.ReleaseName,
                        FileName = x.FileName,
                        CacheRelativePath = x.CacheRelativePath,
                        CacheHash = x.CacheHash,
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

            return DeduplicateRows(rows, movieId, episodeId, mediaFileId);
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

    public async Task<OnlineSubtitleBindingListItem> UpsertBindingAsync(
        OnlineSubtitleBindingUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(request.MovieId, request.EpisodeId, request.MediaFileId);
        if (string.IsNullOrWhiteSpace(request.ProviderFileId))
        {
            throw new InvalidOperationException("OnlineSubtitleProviderFileIdRequired");
        }

        if (string.IsNullOrWhiteSpace(request.CacheRelativePath) || string.IsNullOrWhiteSpace(request.CacheHash))
        {
            throw new InvalidOperationException("OnlineSubtitleCacheRequired");
        }

        var provider = NormalizeRequired(request.Provider, DefaultProvider, 64);
        var providerFileId = NormalizeRequired(request.ProviderFileId, "file", 128);
        var now = DateTime.UtcNow;

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var binding = await FindExistingBindingAsync(
            dbContext,
            request.MovieId,
            request.EpisodeId,
            request.MediaFileId,
            provider,
            providerFileId,
            request.ProviderSubtitleId,
            request.CacheHash,
            cancellationToken);

        if (binding is null)
        {
            binding = new OnlineSubtitleBinding
            {
                MovieId = request.MovieId,
                EpisodeId = request.EpisodeId,
                MediaFileId = request.MediaFileId,
                Provider = provider,
                ProviderFileId = providerFileId,
                CreatedAt = now
            };
            dbContext.OnlineSubtitleBindings.Add(binding);
        }

        binding.ProviderSubtitleId = NormalizeOptional(request.ProviderSubtitleId, 128);
        binding.LanguageCode = NormalizeRequired(request.LanguageCode, "unknown", 32);
        binding.LanguageName = NormalizeOptional(request.LanguageName, 120);
        binding.DisplayName = NormalizeOptional(request.DisplayName, 500);
        binding.ReleaseName = NormalizeOptional(request.ReleaseName, 500);
        binding.FileName = NormalizeOptional(request.FileName, 260);
        binding.CacheRelativePath = NormalizeRequired(request.CacheRelativePath, "items", 800);
        binding.CacheHash = NormalizeRequired(request.CacheHash, "hash", 128);
        binding.Format = NormalizeOptional(request.Format, 32);
        binding.Extension = NormalizeRequired(request.Extension, ".srt", 16);
        binding.DownloadCount = request.DownloadCount;
        binding.Rating = request.Rating;
        binding.Votes = request.Votes;
        binding.IsHearingImpaired = request.IsHearingImpaired;
        binding.IsMachineTranslated = request.IsMachineTranslated;
        binding.IsAiTranslated = request.IsAiTranslated;
        binding.IsTrustedUploader = request.IsTrustedUploader;
        binding.Fps = request.Fps;
        binding.UploadedAt = request.UploadedAt;
        binding.MetadataJson = NormalizeOptional(request.MetadataJson, MetadataJsonMaxLength);
        binding.IsDeleted = false;
        binding.UpdatedAt = now;
        binding.LastUsedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToListItem(binding);
    }

    public async Task<bool> MarkUsedAsync(
        int bindingId,
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken = default)
    {
        var binding = await FindMutableTargetBindingAsync(bindingId, movieId, episodeId, mediaFileId, cancellationToken);
        if (binding is null)
        {
            return false;
        }

        binding.Entity.LastUsedAt = DateTime.UtcNow;
        binding.Entity.UpdatedAt = binding.Entity.LastUsedAt.Value;
        await binding.Context.SaveChangesAsync(cancellationToken);
        await binding.Context.DisposeAsync();
        return true;
    }

    public async Task<bool> SoftDeleteAsync(
        int bindingId,
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken = default)
    {
        var binding = await FindMutableTargetBindingAsync(bindingId, movieId, episodeId, mediaFileId, cancellationToken);
        if (binding is null)
        {
            return false;
        }

        binding.Entity.IsDeleted = true;
        binding.Entity.UpdatedAt = DateTime.UtcNow;
        await binding.Context.SaveChangesAsync(cancellationToken);
        await binding.Context.DisposeAsync();
        return true;
    }

    public async Task<int> SoftDeleteForMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var now = DateTime.UtcNow;
        var bindings = await dbContext.OnlineSubtitleBindings
            .Where(x => !x.IsDeleted && x.MediaFileId.HasValue && ids.Contains(x.MediaFileId.Value))
            .ToListAsync(cancellationToken);
        foreach (var binding in bindings)
        {
            binding.IsDeleted = true;
            binding.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return bindings.Count;
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

    private static IReadOnlyList<OnlineSubtitleBindingListItem> DeduplicateRows(
        IReadOnlyList<OnlineSubtitleBindingListItem> rows,
        int? movieId,
        int? episodeId,
        int? mediaFileId)
    {
        var result = new List<OnlineSubtitleBindingListItem>(rows.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows
                     .OrderBy(x => GetTargetPriority(x, movieId, episodeId, mediaFileId))
                     .ThenByDescending(x => x.CreatedAt))
        {
            var keys = BuildDeduplicationKeys(row);
            if (keys.Count == 0 || !keys.Any(seen.Contains))
            {
                result.Add(row);
                foreach (var key in keys)
                {
                    seen.Add(key);
                }
            }
        }

        return result;
    }

    private static int GetTargetPriority(
        OnlineSubtitleBindingListItem row,
        int? movieId,
        int? episodeId,
        int? mediaFileId)
    {
        if (episodeId is > 0 && row.EpisodeId == episodeId.Value)
        {
            return 0;
        }

        if (movieId is > 0 && row.MovieId == movieId.Value)
        {
            return 0;
        }

        if (mediaFileId is > 0 && row.MediaFileId == mediaFileId.Value)
        {
            return 1;
        }

        return 2;
    }

    private static IReadOnlyList<string> BuildDeduplicationKeys(OnlineSubtitleBindingListItem row)
    {
        var keys = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(row.ProviderFileId))
        {
            keys.Add($"file:{row.Provider}:{row.ProviderFileId}");
        }

        if (!string.IsNullOrWhiteSpace(row.ProviderSubtitleId))
        {
            keys.Add($"subtitle:{row.Provider}:{row.ProviderSubtitleId}");
        }

        if (!string.IsNullOrWhiteSpace(row.CacheHash))
        {
            keys.Add($"cache:{row.CacheHash}");
        }

        return keys;
    }

    private static async Task<OnlineSubtitleBinding?> FindExistingBindingAsync(
        AppDbContext dbContext,
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        string provider,
        string providerFileId,
        string providerSubtitleId,
        string cacheHash,
        CancellationToken cancellationToken)
    {
        var query = dbContext.OnlineSubtitleBindings
            .Where(x => x.MovieId == movieId
                        && x.EpisodeId == episodeId
                        && x.MediaFileId == mediaFileId
                        && x.Provider == provider);

        var normalizedSubtitleId = NormalizeOptional(providerSubtitleId, 128);
        var normalizedCacheHash = NormalizeOptional(cacheHash, 128);

        return await query
            .Where(x => x.ProviderFileId == providerFileId
                        || (!string.IsNullOrWhiteSpace(normalizedSubtitleId) && x.ProviderSubtitleId == normalizedSubtitleId)
                        || (!string.IsNullOrWhiteSpace(normalizedCacheHash) && x.CacheHash == normalizedCacheHash))
            .OrderBy(x => x.IsDeleted)
            .ThenByDescending(x => x.LastUsedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<MutableBinding?> FindMutableTargetBindingAsync(
        int bindingId,
        int? movieId,
        int? episodeId,
        int? mediaFileId,
        CancellationToken cancellationToken)
    {
        if (bindingId <= 0)
        {
            return null;
        }

        var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var query = dbContext.OnlineSubtitleBindings
            .Where(x => x.Id == bindingId && !x.IsDeleted);

        if (episodeId is > 0)
        {
            query = query.Where(x => x.EpisodeId == episodeId.Value);
        }
        else if (movieId is > 0)
        {
            query = query.Where(x => x.MovieId == movieId.Value);
        }
        else if (mediaFileId is > 0)
        {
            query = query.Where(x => x.MediaFileId == mediaFileId.Value);
        }
        else
        {
            await dbContext.DisposeAsync();
            return null;
        }

        var entity = await query.FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            await dbContext.DisposeAsync();
            return null;
        }

        return new MutableBinding(dbContext, entity);
    }

    private static OnlineSubtitleBindingListItem ToListItem(OnlineSubtitleBinding binding)
    {
        var cacheRoot = Path.GetFullPath(AppPaths.GetOnlineSubtitleCacheDirectory());
        return new OnlineSubtitleBindingListItem
        {
            Id = binding.Id,
            MovieId = binding.MovieId,
            EpisodeId = binding.EpisodeId,
            MediaFileId = binding.MediaFileId,
            TargetKind = binding.MovieId.HasValue
                ? "movie"
                : binding.EpisodeId.HasValue
                    ? "episode"
                    : "mediaFile",
            Provider = binding.Provider,
            ProviderSubtitleId = binding.ProviderSubtitleId,
            ProviderFileId = binding.ProviderFileId,
            LanguageCode = binding.LanguageCode,
            LanguageName = binding.LanguageName,
            DisplayName = binding.DisplayName,
            ReleaseName = binding.ReleaseName,
            FileName = binding.FileName,
            CacheRelativePath = binding.CacheRelativePath,
            CacheHash = binding.CacheHash,
            Format = binding.Format,
            Extension = binding.Extension,
            HasCacheFile = HasManagedCacheFile(cacheRoot, binding.CacheRelativePath),
            CreatedAt = binding.CreatedAt
        };
    }

    private static void ValidateTarget(int? movieId, int? episodeId, int? mediaFileId)
    {
        var hasMovie = movieId is > 0;
        var hasEpisode = episodeId is > 0;
        var hasMediaFile = mediaFileId is > 0;
        if ((hasMovie ? 1 : 0) + (hasEpisode ? 1 : 0) + (hasMediaFile ? 1 : 0) != 1)
        {
            throw new InvalidOperationException("OnlineSubtitleTargetMustBeMovieEpisodeOrMediaFile");
        }
    }

    private static string NormalizeRequired(string value, string fallback, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);
        return string.IsNullOrWhiteSpace(normalized) ? fallback[..Math.Min(fallback.Length, maxLength)] : normalized;
    }

    private static string NormalizeOptional(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record MutableBinding(AppDbContext Context, OnlineSubtitleBinding Entity);
}
