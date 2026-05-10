using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class SettingsService : ISettingsService
{
    public async Task<ApplicationSettingModel> GetApplicationSettingAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var entity = await dbContext.ApplicationSettings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null
            ? new ApplicationSettingModel()
            : new ApplicationSettingModel
            {
                Id = entity.Id,
                TmdbReadAccessToken = entity.TmdbReadAccessToken,
                TmdbApiKey = entity.TmdbApiKey,
                OmdbApiKey = entity.OmdbApiKey,
                ThemeMode = string.IsNullOrWhiteSpace(entity.ThemeMode) ? "Light" : entity.ThemeMode,
                AiBaseUrl = entity.AiBaseUrl,
                AiApiKey = entity.AiApiKey,
                AiModel = entity.AiModel,
                RecentAiRecommendationsJson = entity.RecentAiRecommendationsJson,
                CurrentAiRecommendationsJson = entity.CurrentAiRecommendationsJson,
                AiRecommendationLibraryFingerprint = entity.AiRecommendationLibraryFingerprint,
                TmdbBaseUrl = string.IsNullOrWhiteSpace(entity.TmdbBaseUrl) ? "https://api.tmdb.org/3/" : entity.TmdbBaseUrl
            };
    }

    public async Task<ApplicationSettingModel> SaveApplicationSettingAsync(
        ApplicationSettingModel settings,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        ApplicationSetting entity;
        if (settings.Id.HasValue)
        {
            entity = await dbContext.ApplicationSettings.FirstOrDefaultAsync(
                         x => x.Id == settings.Id.Value,
                         cancellationToken)
                     ?? new ApplicationSetting
                     {
                         CreatedAt = DateTime.UtcNow
                     };

            if (entity.Id == 0)
            {
                dbContext.ApplicationSettings.Add(entity);
            }
        }
        else
        {
            entity = await dbContext.ApplicationSettings
                         .OrderByDescending(x => x.UpdatedAt)
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? new ApplicationSetting
                     {
                         CreatedAt = DateTime.UtcNow
                     };

            if (entity.Id == 0)
            {
                dbContext.ApplicationSettings.Add(entity);
            }
        }

        entity.TmdbReadAccessToken = settings.TmdbReadAccessToken.Trim();
        entity.TmdbApiKey = settings.TmdbApiKey.Trim();
        entity.OmdbApiKey = settings.OmdbApiKey.Trim();
        entity.ThemeMode = string.IsNullOrWhiteSpace(settings.ThemeMode) ? "Light" : settings.ThemeMode.Trim();
        entity.AiBaseUrl = settings.AiBaseUrl.Trim();
        entity.AiApiKey = settings.AiApiKey.Trim();
        entity.AiModel = settings.AiModel.Trim();
        entity.RecentAiRecommendationsJson = settings.RecentAiRecommendationsJson;
        entity.CurrentAiRecommendationsJson = settings.CurrentAiRecommendationsJson;
        entity.AiRecommendationLibraryFingerprint = settings.AiRecommendationLibraryFingerprint;
        entity.TmdbBaseUrl = settings.TmdbBaseUrl.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApplicationSettingModel
        {
            Id = entity.Id,
            TmdbReadAccessToken = entity.TmdbReadAccessToken,
            TmdbApiKey = entity.TmdbApiKey,
            OmdbApiKey = entity.OmdbApiKey,
            ThemeMode = entity.ThemeMode,
            AiBaseUrl = entity.AiBaseUrl,
            AiApiKey = entity.AiApiKey,
            AiModel = entity.AiModel,
            RecentAiRecommendationsJson = entity.RecentAiRecommendationsJson,
            CurrentAiRecommendationsJson = entity.CurrentAiRecommendationsJson,
            AiRecommendationLibraryFingerprint = entity.AiRecommendationLibraryFingerprint,
            TmdbBaseUrl = string.IsNullOrWhiteSpace(entity.TmdbBaseUrl) ? "https://api.tmdb.org/3/" : entity.TmdbBaseUrl
        };
    }

    public async Task<WebDavConnectionModel> GetPrimaryConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var connection = await dbContext.SourceConnections
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return connection is null
            ? new WebDavConnectionModel()
            : MapToModel(connection);
    }

    public async Task<WebDavConnectionModel> SaveConnectionAsync(
        WebDavConnectionModel connectionModel,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = connectionModel.Name.Trim();
        var normalizedBaseUrl = connectionModel.BaseUrl.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("连接名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            throw new InvalidOperationException("BaseUrl 不能为空。");
        }

        if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("BaseUrl 必须是有效的绝对地址。");
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        SourceConnection entity;
        if (connectionModel.Id.HasValue)
        {
            entity = await dbContext.SourceConnections.FirstOrDefaultAsync(
                         x => x.Id == connectionModel.Id.Value,
                         cancellationToken)
                     ?? new SourceConnection
                     {
                         CreatedAt = DateTime.UtcNow
                     };

            if (entity.Id == 0)
            {
                dbContext.SourceConnections.Add(entity);
            }
        }
        else
        {
            entity = new SourceConnection
            {
                CreatedAt = DateTime.UtcNow
            };
            dbContext.SourceConnections.Add(entity);
        }

        entity.Name = normalizedName;
        entity.BaseUrl = normalizedBaseUrl;
        entity.Username = connectionModel.Username.Trim();
        entity.PasswordEncrypted = SecretProtector.Protect(connectionModel.Password);
        entity.IsEnabled = connectionModel.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToModel(entity);
    }

    public async Task<IReadOnlyList<ScanPath>> GetScanPathsAsync(
        int sourceConnectionId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        return await dbContext.ScanPaths
            .AsNoTracking()
            .Where(x => x.SourceConnectionId == sourceConnectionId)
            .OrderByDescending(x => x.IsEnabled)
            .ThenBy(x => x.DisplayName)
            .ThenBy(x => x.Path)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScanPath> SaveScanPathAsync(ScanPath scanPath, CancellationToken cancellationToken = default)
    {
        if (scanPath.SourceConnectionId <= 0)
        {
            throw new InvalidOperationException("扫描路径必须绑定到已保存的连接。");
        }

        var normalizedPath = NormalizePath(scanPath.Path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new InvalidOperationException("扫描路径不能为空。");
        }

        var normalizedDisplayName = string.IsNullOrWhiteSpace(scanPath.DisplayName)
            ? normalizedPath
            : scanPath.DisplayName.Trim();

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var duplicateExists = await dbContext.ScanPaths
            .AnyAsync(
                x => x.SourceConnectionId == scanPath.SourceConnectionId
                     && x.Id != scanPath.Id
                     && x.Path.ToLower() == normalizedPath.ToLower(),
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("同一连接下，相同扫描路径不能重复添加。");
        }

        ScanPath entity;
        if (scanPath.Id > 0)
        {
            entity = await dbContext.ScanPaths.FirstOrDefaultAsync(x => x.Id == scanPath.Id, cancellationToken)
                     ?? throw new InvalidOperationException("要编辑的扫描路径不存在。");
        }
        else
        {
            entity = new ScanPath
            {
                SourceConnectionId = scanPath.SourceConnectionId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ScanPaths.Add(entity);
        }

        entity.Path = normalizedPath;
        entity.DisplayName = normalizedDisplayName;
        entity.IsEnabled = scanPath.IsEnabled;
        entity.IsRecursive = scanPath.IsRecursive;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScanPath
        {
            Id = entity.Id,
            SourceConnectionId = entity.SourceConnectionId,
            Path = entity.Path,
            DisplayName = entity.DisplayName,
            IsEnabled = entity.IsEnabled,
            IsRecursive = entity.IsRecursive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task DeleteScanPathAsync(int scanPathId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await dbContext.ScanPaths.FirstOrDefaultAsync(x => x.Id == scanPathId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.ScanPaths.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetScanPathEnabledAsync(int scanPathId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await dbContext.ScanPaths.FirstOrDefaultAsync(x => x.Id == scanPathId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.IsEnabled = isEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static WebDavConnectionModel MapToModel(SourceConnection connection)
    {
        return new WebDavConnectionModel
        {
            Id = connection.Id,
            Name = connection.Name,
            ProtocolType = connection.ProtocolType,
            BaseUrl = connection.BaseUrl,
            Username = connection.Username,
            Password = SecretProtector.Unprotect(connection.PasswordEncrypted),
            IsEnabled = connection.IsEnabled,
            LastConnectedAt = connection.LastConnectedAt,
            LastScanAt = connection.LastScanAt
        };
    }

    private static string NormalizePath(string path)
    {
        return WebDavPathHelper.NormalizeVirtualPath(path);
    }
}
