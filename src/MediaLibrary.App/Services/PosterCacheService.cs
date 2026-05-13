using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.App.Models.Caches;
using MediaLibrary.Core.Helpers;

namespace MediaLibrary.App.Services;

public sealed class PosterCacheService : IPosterCacheService, IDisposable
{
    private const int MaxConcurrentDownloads = 4;
    private const int BufferSize = 1024 * 64;
    private const string ItemsDirectoryName = "items";
    private const string SettingsFileName = "settings.json";
    private const string TemporaryExtension = ".tmp";
    private const string DefaultImageExtension = ".img";
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".img",
        ".jpeg",
        ".jpg",
        ".png",
        ".webp"
    };

    private readonly string _cacheRoot;
    private readonly string _itemsRoot;
    private readonly string _settingsPath;
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly SemaphoreSlim _downloadSemaphore = new(MaxConcurrentDownloads, MaxConcurrentDownloads);
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _downloadsByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _failureCooldownUntilUtcByHash = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;

    public PosterCacheService()
    {
        _cacheRoot = AppPaths.GetPosterCacheDirectory();
        _itemsRoot = Path.Combine(_cacheRoot, ItemsDirectoryName);
        _settingsPath = Path.Combine(_cacheRoot, SettingsFileName);
        Directory.CreateDirectory(_itemsRoot);
    }

    public Task<string> GetCachedOrFallbackAsync(
        string? source,
        CancellationToken cancellationToken = default)
    {
        return GetCachedOrFallbackCoreAsync(source, forceRefresh: false, cancellationToken);
    }

    public Task<string> RefreshAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        return GetCachedOrFallbackCoreAsync(source, forceRefresh: true, cancellationToken);
    }

    public Task<PosterCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = EnumerateManagedCacheFiles().ToList();
        return Task.FromResult(
            new PosterCacheUsage
            {
                UsedBytes = files.Sum(file => file.Length),
                FileCount = files.Count
            });
    }

    public async Task<PosterCacheSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new PosterCacheSettings();
            }

            await using var stream = new FileStream(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);
            var settings = await JsonSerializer.DeserializeAsync<PosterCacheSettingsDocument>(
                stream,
                JsonOptions,
                cancellationToken);
            return ToSettings(settings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new PosterCacheSettings();
        }
    }

    public async Task SaveSettingsAsync(
        PosterCacheSettings settings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeSettings(settings);
        Directory.CreateDirectory(_cacheRoot);
        var tempPath = _settingsPath + ".tmp";

        await using (var stream = new FileStream(
                         tempPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         BufferSize,
                         useAsync: true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new PosterCacheSettingsDocument { MaxBytes = normalized.MaxBytes },
                JsonOptions,
                cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, _settingsPath, overwrite: true);
        await TrimToLimitAsync(normalized.MaxBytes, cancellationToken);
    }

    public async Task<PosterCacheClearResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        var acquiredPermits = 0;
        try
        {
            for (var index = 0; index < MaxConcurrentDownloads; index++)
            {
                await _downloadSemaphore.WaitAsync(cancellationToken);
                acquiredPermits++;
            }

            var files = EnumerateFilesUnderItemsRoot(includeTemporaryFiles: true).ToList();
            var freedBytes = 0L;
            var deletedCount = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsPathUnderItemsRoot(file))
                {
                    continue;
                }

                long length;
                try
                {
                    length = new FileInfo(file).Length;
                }
                catch
                {
                    length = 0;
                }

                if (TryDeleteFile(file))
                {
                    deletedCount++;
                    freedBytes += length;
                }
            }

            DeleteEmptyDirectoriesUnderItemsRoot();
            return new PosterCacheClearResult
            {
                Succeeded = true,
                DeletedFileCount = deletedCount,
                FreedBytes = freedBytes
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new PosterCacheClearResult
            {
                Succeeded = false,
                Error = exception.GetType().Name
            };
        }
        finally
        {
            for (var index = 0; index < acquiredPermits; index++)
            {
                _downloadSemaphore.Release();
            }
        }
    }

    public Task<PosterCacheTrimResult> TrimToLimitAsync(
        long? maxBytes = null,
        CancellationToken cancellationToken = default)
    {
        return TrimToLimitCoreAsync(maxBytes, preservePath: null, cancellationToken);
    }

    private async Task<string> GetCachedOrFallbackCoreAsync(
        string? source,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var normalizedSource = NormalizeSource(source);
        if (!TryCreateRemoteImageUri(normalizedSource, out var remoteUri))
        {
            return normalizedSource;
        }

        var hash = HashString(remoteUri.AbsoluteUri);
        var existingCachePath = FindExistingCacheFile(hash);
        if (!forceRefresh && !string.IsNullOrWhiteSpace(existingCachePath))
        {
            Touch(existingCachePath);
            return existingCachePath;
        }

        if (!forceRefresh && IsInFailureCooldown(hash))
        {
            return normalizedSource;
        }

        var fallback = forceRefresh && !string.IsNullOrWhiteSpace(existingCachePath)
            ? existingCachePath
            : normalizedSource;
        var downloadKey = forceRefresh ? $"{hash}:force" : hash;
        var downloadTask = _downloadsByKey.GetOrAdd(
            downloadKey,
            _downloadKey => new Lazy<Task<string>>(
                () =>
                {
                    var task = DownloadAndCacheAsync(
                        remoteUri,
                        hash,
                        fallback,
                        forceRefresh,
                        _disposeCts.Token);
                    _ = task.ContinueWith(
                        _ =>
                        {
                            _downloadsByKey.TryRemove(downloadKey, out var removedDownload);
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    return task;
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        return await downloadTask.Value.WaitAsync(cancellationToken);
    }

    private async Task<string> DownloadAndCacheAsync(
        Uri remoteUri,
        string hash,
        string fallback,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var tempPath = string.Empty;
        try
        {
            await _downloadSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh)
                {
                    var existingCachePath = FindExistingCacheFile(hash);
                    if (!string.IsNullOrWhiteSpace(existingCachePath))
                    {
                        Touch(existingCachePath);
                        return existingCachePath;
                    }

                    if (IsInFailureCooldown(hash))
                    {
                        return fallback;
                    }
                }

                var itemDirectory = GetItemDirectory(hash);
                Directory.CreateDirectory(itemDirectory);
                tempPath = Path.Combine(itemDirectory, $"{hash}.{Guid.NewGuid():N}{TemporaryExtension}");

                using var request = new HttpRequestMessage(HttpMethod.Get, remoteUri);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    MarkFailure(hash);
                    return fallback;
                }

                var contentType = response.Content.Headers.ContentType;
                if (!IsImageContentType(contentType))
                {
                    MarkFailure(hash);
                    return fallback;
                }

                await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var localStream = new FileStream(
                                 tempPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 BufferSize,
                                 FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await remoteStream.CopyToAsync(localStream, BufferSize, cancellationToken);
                    await localStream.FlushAsync(cancellationToken);
                }

                if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                {
                    MarkFailure(hash);
                    return fallback;
                }

                var extension = ResolveImageExtension(remoteUri, contentType);
                var contentPath = Path.Combine(itemDirectory, $"{hash}{extension}");
                File.Move(tempPath, contentPath, overwrite: true);
                tempPath = string.Empty;
                DeleteSiblingCacheFiles(hash, contentPath);
                _failureCooldownUntilUtcByHash.TryRemove(hash, out _);
                Touch(contentPath);
                await TrimToConfiguredLimitBestEffortAsync(contentPath, cancellationToken);
                return contentPath;
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return fallback;
        }
        catch
        {
            MarkFailure(hash);
            return fallback;
        }
        finally
        {
            DeleteFileIfExists(tempPath);
        }
    }

    private static string NormalizeSource(string? source)
    {
        return string.IsNullOrWhiteSpace(source) ? string.Empty : source.Trim();
    }

    private static bool TryCreateRemoteImageUri(string source, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(source)
            || !Uri.TryCreate(source, UriKind.Absolute, out var parsedUri)
            || parsedUri is null)
        {
            return false;
        }

        uri = parsedUri;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool IsImageContentType(MediaTypeHeaderValue? contentType)
    {
        var mediaType = contentType?.MediaType;
        return string.IsNullOrWhiteSpace(mediaType)
               || mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveImageExtension(Uri remoteUri, MediaTypeHeaderValue? contentType)
    {
        var uriExtension = NormalizeExtension(Path.GetExtension(remoteUri.AbsolutePath));
        if (uriExtension is not null)
        {
            return uriExtension;
        }

        return contentType?.MediaType?.ToLowerInvariant() switch
        {
            "image/bmp" => ".bmp",
            "image/gif" => ".gif",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => DefaultImageExtension
        };
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return AllowedImageExtensions.Contains(normalized) && normalized != TemporaryExtension
            ? normalized
            : null;
    }

    private string? FindExistingCacheFile(string hash)
    {
        var itemDirectory = GetItemDirectory(hash);
        if (!Directory.Exists(itemDirectory))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(itemDirectory, $"{hash}.*", SearchOption.TopDirectoryOnly)
                .Where(IsValidCacheFile)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidCacheFile(string path)
    {
        try
        {
            return File.Exists(path)
                   && new FileInfo(path).Length > 0
                   && NormalizeExtension(Path.GetExtension(path)) is not null;
        }
        catch
        {
            return false;
        }
    }

    private void DeleteSiblingCacheFiles(string hash, string contentPath)
    {
        var itemDirectory = GetItemDirectory(hash);
        if (!Directory.Exists(itemDirectory))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(itemDirectory, $"{hash}.*", SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(path, contentPath, StringComparison.OrdinalIgnoreCase)
                    && NormalizeExtension(Path.GetExtension(path)) is not null)
                {
                    DeleteFileIfExists(path);
                }
            }
        }
        catch
        {
            // Cache sibling cleanup is best-effort.
        }
    }

    private bool IsInFailureCooldown(string hash)
    {
        if (!_failureCooldownUntilUtcByHash.TryGetValue(hash, out var untilUtc))
        {
            return false;
        }

        if (untilUtc > DateTime.UtcNow)
        {
            return true;
        }

        _failureCooldownUntilUtcByHash.TryRemove(hash, out _);
        return false;
    }

    private void MarkFailure(string hash)
    {
        _failureCooldownUntilUtcByHash[hash] = DateTime.UtcNow.Add(FailureCooldown);
    }

    private string GetItemDirectory(string hash)
    {
        var shard = hash.Length >= 2 ? hash[..2] : "00";
        return Path.Combine(_itemsRoot, shard);
    }

    private static string HashString(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void Touch(string path)
    {
        try
        {
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
        }
        catch
        {
            // Last access time is only a cache hint.
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cache temp cleanup is best-effort.
        }
    }

    private async Task TrimToConfiguredLimitBestEffortAsync(
        string preservePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await GetSettingsAsync(cancellationToken);
            await TrimToLimitCoreAsync(settings.MaxBytes, preservePath, cancellationToken);
        }
        catch
        {
            // Capacity trimming must never block poster display.
        }
    }

    private Task<PosterCacheTrimResult> TrimToLimitCoreAsync(
        long? maxBytes,
        string? preservePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var limit = maxBytes is > 0 ? maxBytes.Value : PosterCacheDefaults.DefaultMaxBytes;
            var normalizedPreservePath = string.IsNullOrWhiteSpace(preservePath)
                ? null
                : Path.GetFullPath(preservePath);
            var files = EnumerateManagedCacheFiles()
                .OrderBy(file => GetLastUsedTimeUtc(file.FullName))
                .ToList();
            var usedBytes = files.Sum(file => file.Length);
            var freedBytes = 0L;
            var deletedCount = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (usedBytes <= limit)
                {
                    break;
                }

                if (normalizedPreservePath is not null
                    && string.Equals(
                        Path.GetFullPath(file.FullName),
                        normalizedPreservePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsPathUnderItemsRoot(file.FullName))
                {
                    continue;
                }

                var length = file.Length;
                if (TryDeleteFile(file.FullName))
                {
                    deletedCount++;
                    freedBytes += length;
                    usedBytes = Math.Max(0, usedBytes - length);
                }
            }

            DeleteEmptyDirectoriesUnderItemsRoot();
            return Task.FromResult(
                new PosterCacheTrimResult
                {
                    Succeeded = true,
                    DeletedFileCount = deletedCount,
                    FreedBytes = freedBytes,
                    UsedBytesAfterTrim = usedBytes
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Task.FromResult(
                new PosterCacheTrimResult
                {
                    Succeeded = false,
                    Error = exception.GetType().Name
                });
        }
    }

    private IEnumerable<FileInfo> EnumerateManagedCacheFiles()
    {
        foreach (var file in EnumerateFilesUnderItemsRoot(includeTemporaryFiles: false))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(file);
            }
            catch
            {
                continue;
            }

            if (info.Exists && info.Length > 0 && IsValidCacheFile(info.FullName))
            {
                yield return info;
            }
        }
    }

    private IEnumerable<string> EnumerateFilesUnderItemsRoot(bool includeTemporaryFiles)
    {
        if (!Directory.Exists(_itemsRoot))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(_itemsRoot, "*", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (!IsPathUnderItemsRoot(file))
            {
                continue;
            }

            if (includeTemporaryFiles || IsValidCacheFile(file))
            {
                yield return file;
            }
        }
    }

    private void DeleteEmptyDirectoriesUnderItemsRoot()
    {
        if (!Directory.Exists(_itemsRoot))
        {
            return;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(_itemsRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.Length)
                .ToList();
        }
        catch
        {
            return;
        }

        foreach (var directory in directories)
        {
            try
            {
                if (IsPathUnderItemsRoot(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Empty directory cleanup is best-effort.
            }
        }
    }

    private bool IsPathUnderItemsRoot(string path)
    {
        try
        {
            var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(_itemsRoot));
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
               || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static DateTime GetLastUsedTimeUtc(string path)
    {
        try
        {
            var lastAccess = File.GetLastAccessTimeUtc(path);
            if (lastAccess > DateTime.MinValue.AddYears(1))
            {
                return lastAccess;
            }
        }
        catch
        {
            // Fall back to last write time below.
        }

        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
                return !File.Exists(path);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static PosterCacheSettings NormalizeSettings(PosterCacheSettings? settings)
    {
        return new PosterCacheSettings
        {
            MaxBytes = (settings?.MaxBytes ?? 0) > 0
                ? settings!.MaxBytes
                : PosterCacheDefaults.DefaultMaxBytes
        };
    }

    private static PosterCacheSettings ToSettings(PosterCacheSettingsDocument? document)
    {
        return NormalizeSettings(
            new PosterCacheSettings
            {
                MaxBytes = document?.MaxBytes ?? PosterCacheDefaults.DefaultMaxBytes
            });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _downloadSemaphore.Dispose();
        _httpClient.Dispose();
    }

    private sealed class PosterCacheSettingsDocument
    {
        public long MaxBytes { get; set; } = PosterCacheDefaults.DefaultMaxBytes;
    }
}
