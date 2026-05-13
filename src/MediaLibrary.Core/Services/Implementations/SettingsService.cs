using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class SettingsService : ISettingsService
{
    private const string LocalSourceName = "本地媒体";
    private const string LocalSourceBaseUrl = "local://media";

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
            .Where(x => x.ProtocolType == ProtocolType.WebDav)
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
        entity.ProtocolType = ProtocolType.WebDav;
        entity.BaseUrl = normalizedBaseUrl;
        entity.Username = connectionModel.Username.Trim();
        entity.PasswordEncrypted = SecretProtector.Protect(connectionModel.Password);
        entity.IsEnabled = connectionModel.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToModel(entity);
    }

    public async Task<SourceConnection?> GetLocalConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        return await dbContext.SourceConnections
            .AsNoTracking()
            .Where(x => x.ProtocolType == ProtocolType.Local)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SourceConnection> GetOrCreateLocalConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var connection = await dbContext.SourceConnections
            .Where(x => x.ProtocolType == ProtocolType.Local)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is not null)
        {
            return connection;
        }

        var now = DateTime.UtcNow;
        connection = new SourceConnection
        {
            Name = LocalSourceName,
            ProtocolType = ProtocolType.Local,
            BaseUrl = LocalSourceBaseUrl,
            Username = string.Empty,
            PasswordEncrypted = string.Empty,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.SourceConnections.Add(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return connection;
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

    public async Task<IReadOnlyList<ScanPath>> GetLocalScanPathsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var localConnection = await dbContext.SourceConnections
            .AsNoTracking()
            .Where(x => x.ProtocolType == ProtocolType.Local)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (localConnection is null)
        {
            return [];
        }

        return await dbContext.ScanPaths
            .AsNoTracking()
            .Where(x => x.SourceConnectionId == localConnection.Id)
            .OrderByDescending(x => x.IsEnabled)
            .ThenBy(x => x.DisplayName)
            .ThenBy(x => x.Path)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScanPath> SaveLocalScanPathAsync(ScanPath scanPath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeLocalPath(scanPath.Path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new InvalidOperationException("本地目录路径不能为空。");
        }

        var normalizedDisplayName = NormalizeLocalDisplayName(scanPath.DisplayName, normalizedPath);

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var connection = await dbContext.SourceConnections
            .Where(x => x.ProtocolType == ProtocolType.Local)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            var now = DateTime.UtcNow;
            connection = new SourceConnection
            {
                Name = LocalSourceName,
                ProtocolType = ProtocolType.Local,
                BaseUrl = LocalSourceBaseUrl,
                Username = string.Empty,
                PasswordEncrypted = string.Empty,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.SourceConnections.Add(connection);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var duplicateExists = await dbContext.ScanPaths
            .AnyAsync(
                x => x.SourceConnectionId == connection.Id
                     && x.Id != scanPath.Id
                     && x.Path.ToLower() == normalizedPath.ToLower(),
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("相同本地目录不能重复添加。");
        }

        var existingLocalPaths = await dbContext.ScanPaths
            .AsNoTracking()
            .Where(x => x.SourceConnectionId == connection.Id && x.Id != scanPath.Id)
            .Select(x => x.Path)
            .ToListAsync(cancellationToken);

        if (existingLocalPaths.Any(existingPath => LocalPathsOverlap(existingPath, normalizedPath)))
        {
            throw new InvalidOperationException("该目录与已有本地目录存在包含关系。");
        }

        ScanPath entity;
        if (scanPath.Id > 0)
        {
            entity = await dbContext.ScanPaths
                         .Include(x => x.SourceConnection)
                         .FirstOrDefaultAsync(x => x.Id == scanPath.Id, cancellationToken)
                     ?? throw new InvalidOperationException("要编辑的本地目录配置不存在。");

            if (entity.SourceConnection?.ProtocolType != ProtocolType.Local)
            {
                throw new InvalidOperationException("只能编辑本地目录配置。");
            }
        }
        else
        {
            entity = new ScanPath
            {
                SourceConnectionId = connection.Id,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ScanPaths.Add(entity);
        }

        entity.SourceConnectionId = connection.Id;
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

    public async Task DeleteLocalScanPathAsync(int scanPathId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await dbContext.ScanPaths
            .Include(x => x.SourceConnection)
            .FirstOrDefaultAsync(x => x.Id == scanPathId, cancellationToken);

        if (entity is null || entity.SourceConnection?.ProtocolType != ProtocolType.Local)
        {
            return;
        }

        var mediaFiles = await dbContext.MediaFiles
            .Where(x => x.SourceConnectionId == entity.SourceConnectionId
                        && x.ScanPathId == entity.Id
                        && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var mediaFile in mediaFiles)
        {
            mediaFile.IsDeleted = true;
            mediaFile.UpdatedAt = now;
        }

        dbContext.ScanPaths.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetLocalScanPathEnabledAsync(
        int scanPathId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var entity = await dbContext.ScanPaths
            .Include(x => x.SourceConnection)
            .FirstOrDefaultAsync(x => x.Id == scanPathId, cancellationToken);

        if (entity is null || entity.SourceConnection?.ProtocolType != ProtocolType.Local)
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

    private static string NormalizeLocalPath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!Path.IsPathFullyQualified(trimmed))
        {
            throw new InvalidOperationException("本地目录必须使用绝对路径。");
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            var root = Path.GetPathRoot(fullPath);
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            throw new InvalidOperationException("本地目录路径格式无效。");
        }
    }

    private static string NormalizeLocalDisplayName(string displayName, string normalizedPath)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        var directoryName = new DirectoryInfo(normalizedPath).Name;
        return string.IsNullOrWhiteSpace(directoryName) ? "本地目录" : directoryName;
    }

    private static bool LocalPathsOverlap(string existingPath, string candidatePath)
    {
        var normalizedExisting = NormalizeLocalPath(existingPath);
        var normalizedCandidate = NormalizeLocalPath(candidatePath);
        return IsSameOrChildLocalPath(normalizedExisting, normalizedCandidate)
               || IsSameOrChildLocalPath(normalizedCandidate, normalizedExisting);
    }

    private static bool IsSameOrChildLocalPath(string parentPath, string candidatePath)
    {
        if (string.Equals(parentPath, candidatePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentWithSeparator = parentPath.EndsWith(Path.DirectorySeparatorChar)
            || parentPath.EndsWith(Path.AltDirectorySeparatorChar)
                ? parentPath
                : parentPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
