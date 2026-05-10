using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class VideoCacheService : IVideoCacheService, IDisposable
{
    private const long DefaultMaxBytes = 50L * 1024L * 1024L * 1024L;
    private const string CacheModeComplete = "complete";
    private const string SettingsFileName = "settings.json";
    private const string ManifestFileName = "manifest.json";
    private const string PartialFileName = "content.partial";
    private const string MpvSessionRootName = "mpv-session";
    private const string MpvSessionActiveMarkerFileName = ".active.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IWebDavDownloadService _webDavDownloadService;
    private readonly string _cacheRoot;
    private readonly string _itemsRoot;
    private readonly string _mpvSessionRoot;
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<int, DownloadState> _downloadsByMediaFileId = new();
    private readonly ConcurrentDictionary<int, VideoCacheStatusResult> _lastStatuses = new();
    private readonly Dictionary<string, int> _activeLeaseCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _leaseLock = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;

    public VideoCacheService(IWebDavDownloadService webDavDownloadService)
    {
        _webDavDownloadService = webDavDownloadService;
        _cacheRoot = AppPaths.GetVideoCacheDirectory();
        _itemsRoot = Path.Combine(_cacheRoot, "items");
        _mpvSessionRoot = Path.Combine(_cacheRoot, MpvSessionRootName);
        Directory.CreateDirectory(_itemsRoot);
        EnsureSettingsFile();
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await CleanupAsync(_disposeCts.Token);
                }
                catch
                {
                    // Startup cache cleanup must never block application startup.
                }
            });
    }

    public event EventHandler<VideoCacheChangedEventArgs>? StatusChanged;

    public async Task<VideoCacheStatusResult> GetStatusAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        if (!IsCacheCandidate(source))
        {
            return new VideoCacheStatusResult
            {
                Status = VideoCacheStatus.NotCacheable,
                Error = "Only WebDAV video sources can be cached."
            };
        }

        if (_downloadsByMediaFileId.TryGetValue(source.MediaFileId, out var downloadState))
        {
            return new VideoCacheStatusResult
            {
                Status = VideoCacheStatus.Downloading,
                ProgressPercent = downloadState.ProgressPercent
            };
        }

        if (TryGetValidCacheItem(source, out var cacheItem))
        {
            var isInUse = IsLeaseActive(cacheItem.CacheKey);
            return new VideoCacheStatusResult
            {
                Status = isInUse ? VideoCacheStatus.InUse : VideoCacheStatus.Cached,
                ProgressPercent = 100d,
                LocalFilePath = cacheItem.ContentPath,
                Error = isInUse ? "正在播放，停止后可删除。" : null
            };
        }

        if (_lastStatuses.TryGetValue(source.MediaFileId, out var lastStatus)
            && lastStatus.Status == VideoCacheStatus.NotCacheable
            && !string.Equals(lastStatus.Error, "File exceeds video cache capacity.", StringComparison.Ordinal))
        {
            return lastStatus;
        }

        return new VideoCacheStatusResult { Status = VideoCacheStatus.NotCached };
    }

    public Task<VideoCacheAcquireResult> AcquirePlaybackAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCacheCandidate(source) || !TryGetValidCacheItem(source, out var cacheItem))
        {
            return Task.FromResult(VideoCacheAcquireResult.Miss);
        }

        AddLease(cacheItem.CacheKey);
        UpdateLastAccessed(cacheItem);
        SetStatus(
            source,
            new VideoCacheStatusResult
            {
                Status = VideoCacheStatus.InUse,
                ProgressPercent = 100d,
                LocalFilePath = cacheItem.ContentPath
            });

        return Task.FromResult(
            new VideoCacheAcquireResult
            {
                LocalFilePath = cacheItem.ContentPath,
                Lease = new VideoCachePlaybackLease(cacheItem.ContentPath, () => ReleaseLease(cacheItem.CacheKey))
            });
    }

    public async Task EnqueueDownloadAsync(
        PlaybackSourceItem source,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCacheCandidate(source))
        {
            SetStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.NotCacheable,
                    Error = "Only WebDAV video sources can be cached."
                });
            return;
        }

        var settings = await ReadSettingsAsync(cancellationToken);
        if (source.FileSize <= 0 || source.FileSize > settings.MaxBytes)
        {
            SetStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.NotCacheable,
                    Error = "File exceeds video cache capacity."
                });
            return;
        }

        if (!force && TryGetValidCacheItem(source, out var existingItem))
        {
            SetStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.Cached,
                    ProgressPercent = 100d,
                    LocalFilePath = existingItem.ContentPath
                });
            return;
        }

        if (_downloadsByMediaFileId.ContainsKey(source.MediaFileId))
        {
            return;
        }

        var cacheKey = BuildCacheKey(source);
        var state = new DownloadState(source.MediaFileId, source.SourceConnectionId, cacheKey);
        if (!_downloadsByMediaFileId.TryAdd(source.MediaFileId, state))
        {
            return;
        }

        SetStatus(source, new VideoCacheStatusResult { Status = VideoCacheStatus.Downloading });
        _ = Task.Run(() => RunDownloadAsync(CloneSource(source), state, force), CancellationToken.None);
    }

    public Task CancelDownloadAsync(int mediaFileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_downloadsByMediaFileId.TryGetValue(mediaFileId, out var state))
        {
            state.CancellationTokenSource.Cancel();
        }

        return Task.CompletedTask;
    }

    public async Task TryEnqueueAutoDownloadAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCacheCandidate(source))
        {
            return;
        }

        var settings = await ReadSettingsAsync(cancellationToken);
        if (source.FileSize <= 0)
        {
            return;
        }

        if (source.FileSize > settings.MaxBytes)
        {
            SafeTrace(
                $"complete-cache-skipped mediaFileId={source.MediaFileId} cacheKey={CacheKeyPrefix(BuildCacheKey(source))} reason=file-size-exceeds-maxBytes");
            return;
        }

        if (_downloadsByMediaFileId.ContainsKey(source.MediaFileId)
            || TryGetValidCacheItem(source, out _))
        {
            return;
        }

        await EnqueueDownloadAsync(source, force: false, cancellationToken);
    }

    public async Task<VideoCacheStatusResult> DeleteCacheAsync(
        PlaybackSourceItem source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCacheCandidate(source))
        {
            var notCacheable = new VideoCacheStatusResult
            {
                Status = VideoCacheStatus.NotCacheable,
                Error = "Only WebDAV video sources can be cached."
            };
            SetStatus(source, notCacheable);
            return notCacheable;
        }

        await CancelDownloadAsync(source.MediaFileId, cancellationToken);

        var cacheKey = BuildCacheKey(source);
        if (IsLeaseActive(cacheKey))
        {
            var inUse = new VideoCacheStatusResult
            {
                Status = VideoCacheStatus.InUse,
                ProgressPercent = 100d,
                Error = "正在播放，停止后可删除。"
            };
            SetStatus(source, inUse);
            return inUse;
        }

        var itemDirectory = GetCacheItemDirectory(cacheKey);
        if (!Directory.Exists(itemDirectory))
        {
            var notCached = new VideoCacheStatusResult { Status = VideoCacheStatus.NotCached };
            SetStatus(source, notCached);
            return notCached;
        }

        if (!TryDeleteCacheItemDirectory(cacheKey, itemDirectory, out var deleteError))
        {
            var failed = TryGetValidCacheItem(source, out var cacheItem)
                ? new VideoCacheStatusResult
                {
                    Status = IsLeaseActive(cacheKey) ? VideoCacheStatus.InUse : VideoCacheStatus.Cached,
                    ProgressPercent = 100d,
                    LocalFilePath = cacheItem.ContentPath,
                    Error = deleteError ?? "删除本地缓存失败。"
                }
                : new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.Failed,
                    Error = deleteError ?? "删除本地缓存失败。"
                };
            SetStatus(source, failed);
            return failed;
        }

        var deleted = new VideoCacheStatusResult { Status = VideoCacheStatus.NotCached };
        SetStatus(source, deleted);
        return deleted;
    }

    public async Task<VideoCacheSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        return ToSettingsModel(settings);
    }

    public async Task SaveSettingsAsync(
        VideoCacheSettingsModel settings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeSettings(
            new VideoCacheSettings
            {
                MaxBytes = settings.MaxBytes
            });

        Directory.CreateDirectory(_cacheRoot);
        var settingsPath = Path.Combine(_cacheRoot, SettingsFileName);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(settingsPath, json, Encoding.UTF8, cancellationToken);
        await CleanupAsync(cancellationToken);
    }

    public async Task<VideoCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        var cleanedEmptySessionDirectories = CleanupEmptyInactiveMpvSessionDirectories(cancellationToken);
        var usageBytes = CalculateUsageBytes();
        if (cleanedEmptySessionDirectories > 0)
        {
            SafeTrace($"video-cache-r6-lite-empty-session-dir-cleaned count={cleanedEmptySessionDirectories}");
        }

        if (usageBytes.MpvSessionMarkerBytes > 0)
        {
            SafeTrace(
                $"video-cache-r6-lite-usage-marker-excluded count={usageBytes.MpvSessionMarkerCount} bytes={usageBytes.MpvSessionMarkerBytes}");
        }

        return new VideoCacheUsage
        {
            CacheDirectory = _cacheRoot,
            UsedBytes = usageBytes.TotalBytes,
            FullFileBytes = usageBytes.FullFileBytes,
            MpvSessionBytes = usageBytes.MpvSessionBytes,
            LegacyBytes = usageBytes.LegacyBytes,
            DownloadingBytes = usageBytes.PartialBytes,
            MaxBytes = settings.MaxBytes,
            FullFileItemCount = usageBytes.FullFileItemCount,
            MpvSessionDirectoryCount = usageBytes.MpvSessionDirectoryCount,
            LegacyItemCount = usageBytes.LegacyItemCount
        };
    }

    public async Task<VideoCacheClearResult> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_downloadsByMediaFileId.IsEmpty && HasActiveLease())
        {
            return new VideoCacheClearResult
            {
                Succeeded = false,
                BlockedByActiveLease = true,
                Error = "请先关闭播放器或停止播放后再清空视频缓存。"
            };
        }

        var usedBefore = CalculateUsageBytes().TotalBytes;
        foreach (var state in _downloadsByMediaFileId.Values)
        {
            state.CancellationTokenSource.Cancel();
        }

        if (!await WaitForDownloadsToStopAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            return new VideoCacheClearResult
            {
                Succeeded = false,
                Error = "仍有缓存下载任务正在停止，请稍后重试。"
            };
        }

        var deletedCount = 0;
        var failedCount = 0;
        var skippedActiveCount = 0;
        long skippedActiveBytes = 0;
        var deletedFullFileCount = 0;
        foreach (var itemDirectory in EnumerateCacheItemDirectories().ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cacheKey = Path.GetFileName(itemDirectory);
            if (IsLeaseActive(cacheKey) || IsDownloadActive(cacheKey))
            {
                skippedActiveCount++;
                skippedActiveBytes += CalculateDirectoryUsage(itemDirectory).TotalBytes;
                continue;
            }

            if (TryDeleteCacheItemDirectory(cacheKey, itemDirectory, out _))
            {
                deletedCount++;
                deletedFullFileCount++;
            }
            else
            {
                failedCount++;
            }
        }

        DeleteOrphanPartialFiles();
        DeleteEmptyShardDirectories();
        var mpvSessionClear = DeleteInactiveMpvSessionCacheDirectories(cancellationToken);
        var legacyClear = DeleteLegacySegmentResidue(cancellationToken);
        deletedCount += mpvSessionClear.DeletedCount + legacyClear.DeletedCount;
        failedCount += mpvSessionClear.FailedCount + legacyClear.FailedCount;
        skippedActiveCount += mpvSessionClear.SkippedActiveCount;
        skippedActiveBytes += mpvSessionClear.SkippedActiveBytes;

        var usedAfter = CalculateUsageBytes().TotalBytes;
        return new VideoCacheClearResult
        {
            Succeeded = failedCount == 0,
            BlockedByActiveLease = skippedActiveCount > 0,
            DeletedCount = deletedCount,
            FailedCount = failedCount,
            SkippedActiveCount = skippedActiveCount,
            DeletedFullFileCount = deletedFullFileCount,
            DeletedMpvSessionCount = mpvSessionClear.DeletedCount,
            DeletedLegacyCount = legacyClear.DeletedCount,
            FreedBytes = Math.Max(0, usedBefore - usedAfter),
            SkippedActiveBytes = skippedActiveBytes,
            Error = failedCount == 0 ? null : "部分视频缓存删除失败。"
        };
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteStalePartialFiles();
        DeleteInvalidOrOrphanItems();
        var cleanedEmptySessionDirectories = CleanupEmptyInactiveMpvSessionDirectories(cancellationToken);
        if (cleanedEmptySessionDirectories > 0)
        {
            SafeTrace($"video-cache-r6-lite-empty-session-dir-cleaned count={cleanedEmptySessionDirectories}");
        }

        var settings = await ReadSettingsAsync(cancellationToken);
        await EnsureCapacityAsync(0, null, settings.MaxBytes, cancellationToken);
    }

    private async Task RunDownloadAsync(PlaybackSourceItem source, DownloadState state, bool force)
    {
        try
        {
            await _downloadSemaphore.WaitAsync(state.CancellationTokenSource.Token);
            try
            {
                using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    state.CancellationTokenSource.Token,
                    _disposeCts.Token);

                await DownloadCoreAsync(source, state, force, linkedToken.Token);
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            DeletePartialFile(state.CacheKey);
            SetStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.Canceled,
                    Error = "Download canceled."
                });
        }
        catch (Exception exception)
        {
            DeletePartialFile(state.CacheKey);
            SetStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.Failed,
                    Error = SanitizeError(exception.Message)
                });
        }
        finally
        {
            _downloadsByMediaFileId.TryRemove(source.MediaFileId, out _);
            state.CancellationTokenSource.Dispose();
        }
    }

    private async Task DownloadCoreAsync(
        PlaybackSourceItem source,
        DownloadState state,
        bool force,
        CancellationToken cancellationToken)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        if (source.FileSize > settings.MaxBytes)
        {
            SetStatus(
                source,
                new VideoCacheStatusResult
                {
                    Status = VideoCacheStatus.NotCacheable,
                    Error = "File exceeds video cache capacity."
                });
            return;
        }

        await EnsureCapacityAsync(source.FileSize, state.CacheKey, settings.MaxBytes, cancellationToken);

        var itemDirectory = GetCacheItemDirectory(state.CacheKey);
        Directory.CreateDirectory(itemDirectory);

        if (force && !IsLeaseActive(state.CacheKey))
        {
            DeleteCacheContentFiles(itemDirectory);
        }

        var partialPath = Path.Combine(itemDirectory, PartialFileName);
        DeleteFileIfExists(partialPath);

        var progress = new Progress<VideoCacheDownloadProgress>(
            value =>
            {
                state.ProgressPercent = value.Percent;
                SetStatus(
                    source,
                    new VideoCacheStatusResult
                    {
                        Status = VideoCacheStatus.Downloading,
                        ProgressPercent = value.Percent
                    });
            });

        await _webDavDownloadService.DownloadAsync(
            new WebDavDownloadRequest
            {
                DownloadUrl = source.PlaybackUrl,
                Username = source.Username,
                Password = source.Password,
                DestinationPath = partialPath,
                ExpectedBytes = source.FileSize > 0 ? source.FileSize : null
            },
            progress,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var partialLength = new FileInfo(partialPath).Length;
        if (source.FileSize > 0 && partialLength != source.FileSize)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Downloaded length mismatch ({partialLength}/{source.FileSize})."));
        }

        var contentPath = GetContentPath(itemDirectory, source.Extension, source.FileName);
        DeleteFileIfExists(contentPath);
        File.Move(partialPath, contentPath);

        var manifest = CreateManifest(source, state.CacheKey);
        await WriteManifestAsync(itemDirectory, manifest, cancellationToken);

        SetStatus(
            source,
            new VideoCacheStatusResult
            {
                Status = VideoCacheStatus.Cached,
                ProgressPercent = 100d,
                LocalFilePath = contentPath
            });
    }

    private async Task EnsureCapacityAsync(
        long requiredBytes,
        string? protectedCacheKey,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (CalculateUsedBytes() + requiredBytes <= maxBytes)
        {
            return;
        }

        var candidates = ReadCacheItems()
            .Where(x => !string.Equals(x.CacheKey, protectedCacheKey, StringComparison.OrdinalIgnoreCase))
            .Where(x => !IsLeaseActive(x.CacheKey))
            .Where(x => !IsDownloadActive(x.CacheKey))
            .OrderBy(x => x.LastAccessedAtUtc ?? x.CompletedAtUtc ?? DateTime.MinValue)
            .ToList();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = TryDeleteCacheItemDirectory(candidate.CacheKey, candidate.ItemDirectory, out _);
            if (CalculateUsedBytes() + requiredBytes <= maxBytes)
            {
                return;
            }

            await Task.Yield();
        }
    }

    private void DeleteStalePartialFiles()
    {
        if (!Directory.Exists(_itemsRoot))
        {
            return;
        }

        foreach (var partialPath in Directory.EnumerateFiles(_itemsRoot, PartialFileName, SearchOption.AllDirectories))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(partialPath) > TimeSpan.FromHours(24))
                {
                    DeleteFileIfExists(partialPath);
                }
            }
            catch
            {
                // Cleanup is best-effort.
            }
        }
    }

    private void DeleteInvalidOrOrphanItems()
    {
        foreach (var itemDirectory in EnumerateCacheItemDirectories())
        {
            var cacheKey = Path.GetFileName(itemDirectory);
            if (IsLeaseActive(cacheKey)
                || IsDownloadActive(cacheKey))
            {
                continue;
            }

            var manifestPath = Path.Combine(itemDirectory, ManifestFileName);
            if (!File.Exists(manifestPath) || !TryReadManifest(manifestPath, out var manifest))
            {
                _ = TryDeleteCacheItemDirectory(cacheKey, itemDirectory, out _);
                continue;
            }

            if (string.Equals(manifest.CacheMode, CacheModeComplete, StringComparison.OrdinalIgnoreCase))
            {
                var contentPath = GetContentPath(itemDirectory, manifest.Extension, manifest.FileName);
                if (!File.Exists(contentPath))
                {
                    _ = TryDeleteCacheItemDirectory(cacheKey, itemDirectory, out _);
                }

                continue;
            }

            if (!File.Exists(Path.Combine(itemDirectory, PartialFileName)))
            {
                _ = TryDeleteCacheItemDirectory(cacheKey, itemDirectory, out _);
            }
        }
    }

    private async Task<bool> WaitForDownloadsToStopAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!_downloadsByMediaFileId.IsEmpty && DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return _downloadsByMediaFileId.IsEmpty;
    }

    private void DeleteOrphanPartialFiles()
    {
        if (!Directory.Exists(_itemsRoot))
        {
            return;
        }

        foreach (var partialPath in Directory.EnumerateFiles(_itemsRoot, PartialFileName, SearchOption.AllDirectories))
        {
            if (IsPathUnderItemsRoot(partialPath))
            {
                DeleteFileIfExists(partialPath);
            }
        }
    }

    private void DeleteEmptyShardDirectories()
    {
        if (!Directory.Exists(_itemsRoot))
        {
            return;
        }

        foreach (var shardDirectory in Directory.EnumerateDirectories(_itemsRoot))
        {
            try
            {
                var name = Path.GetFileName(shardDirectory);
                if (!Regex.IsMatch(name, "^[a-f0-9]{2}$", RegexOptions.IgnoreCase)
                    || Directory.EnumerateFileSystemEntries(shardDirectory).Any())
                {
                    continue;
                }

                Directory.Delete(shardDirectory);
            }
            catch
            {
                // Empty shard cleanup is best-effort.
            }
        }
    }

    private bool TryGetValidCacheItem(PlaybackSourceItem source, out CacheItem cacheItem)
    {
        cacheItem = default;
        var cacheKey = BuildCacheKey(source);
        var itemDirectory = GetCacheItemDirectory(cacheKey);
        var manifestPath = Path.Combine(itemDirectory, ManifestFileName);
        if (!File.Exists(manifestPath) || !TryReadManifest(manifestPath, out var manifest))
        {
            return false;
        }

        if (!string.Equals(manifest.CacheMode, CacheModeComplete, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.CacheKey, cacheKey, StringComparison.OrdinalIgnoreCase)
            || manifest.MediaFileId != source.MediaFileId
            || manifest.SourceConnectionId != source.SourceConnectionId
            || manifest.FileSize != source.FileSize
            || GetUtcTicks(manifest.LastModifiedAtUtc) != GetUtcTicks(source.LastModifiedAt))
        {
            return false;
        }

        var contentPath = GetContentPath(itemDirectory, source.Extension, source.FileName);
        if (!File.Exists(contentPath))
        {
            contentPath = GetContentPath(itemDirectory, manifest.Extension, manifest.FileName);
        }

        if (!File.Exists(contentPath))
        {
            return false;
        }

        var contentLength = new FileInfo(contentPath).Length;
        if (source.FileSize > 0 && contentLength != source.FileSize)
        {
            return false;
        }

        cacheItem = new CacheItem(cacheKey, itemDirectory, contentPath, manifest);
        return true;
    }

    private IReadOnlyList<CacheItem> ReadCacheItems()
    {
        var result = new List<CacheItem>();
        foreach (var itemDirectory in EnumerateCacheItemDirectories())
        {
            var manifestPath = Path.Combine(itemDirectory, ManifestFileName);
            if (!TryReadManifest(manifestPath, out var manifest))
            {
                continue;
            }

            var contentPath = GetContentPath(itemDirectory, manifest.Extension, manifest.FileName);
            if (string.Equals(manifest.CacheMode, CacheModeComplete, StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(contentPath))
                {
                    continue;
                }

                result.Add(new CacheItem(manifest.CacheKey, itemDirectory, contentPath, manifest));
                continue;
            }

        }

        return result;
    }

    private IEnumerable<string> EnumerateCacheItemDirectories()
    {
        if (!Directory.Exists(_itemsRoot))
        {
            yield break;
        }

        foreach (var shardDirectory in Directory.EnumerateDirectories(_itemsRoot))
        {
            foreach (var itemDirectory in Directory.EnumerateDirectories(shardDirectory))
            {
                if (IsSafeCacheItemDirectory(itemDirectory))
                {
                    yield return itemDirectory;
                }
            }
        }
    }

    private long CalculateUsedBytes()
    {
        return CalculateUsageBytes().FullFileBytes;
    }

    private (
        long TotalBytes,
        long FullFileBytes,
        long MpvSessionBytes,
        long LegacyBytes,
        long PartialBytes,
        int FullFileItemCount,
        int MpvSessionDirectoryCount,
        int MpvSessionMarkerCount,
        long MpvSessionMarkerBytes,
        int LegacyItemCount) CalculateUsageBytes()
    {
        long fullFileBytes = 0;
        long partialBytes = 0;
        var fullFileItemCount = 0;
        foreach (var itemDirectory in EnumerateCacheItemDirectories())
        {
            var hasContent = false;
            foreach (var file in Directory.EnumerateFiles(itemDirectory, "content.*"))
            {
                try
                {
                    var length = new FileInfo(file).Length;
                    fullFileBytes += length;
                    hasContent = true;
                    if (string.Equals(Path.GetFileName(file), PartialFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        partialBytes += length;
                    }
                }
                catch
                {
                    // Ignore files that disappear during cleanup or download cancellation.
                }
            }

            if (hasContent)
            {
                fullFileItemCount++;
            }
        }

        var mpvSessionUsage = CalculateMpvSessionUsage();
        var legacyUsage = CalculateLegacySegmentUsage();
        return (
            fullFileBytes + mpvSessionUsage.TotalBytes + legacyUsage.TotalBytes,
            fullFileBytes,
            mpvSessionUsage.TotalBytes,
            legacyUsage.TotalBytes,
            partialBytes,
            fullFileItemCount,
            mpvSessionUsage.DirectoryCount,
            mpvSessionUsage.MarkerCount,
            mpvSessionUsage.MarkerBytes,
            legacyUsage.ItemCount);
    }

    private (long TotalBytes, int DirectoryCount, int MarkerCount, long MarkerBytes) CalculateMpvSessionUsage()
    {
        if (!Directory.Exists(_mpvSessionRoot))
        {
            return (0, 0, 0, 0);
        }

        long totalBytes = 0;
        var markerCount = 0;
        long markerBytes = 0;
        var directoriesWithCacheData = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_mpvSessionRoot, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var length = new FileInfo(file).Length;
                    if (string.Equals(Path.GetFileName(file), MpvSessionActiveMarkerFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        markerCount++;
                        markerBytes += length;
                        continue;
                    }

                    totalBytes += length;
                    var sessionDirectory = FindMpvSessionChildDirectory(file);
                    if (!string.IsNullOrWhiteSpace(sessionDirectory))
                    {
                        directoriesWithCacheData.Add(sessionDirectory);
                    }
                }
                catch
                {
                    // Ignore files that disappear during active playback or cleanup.
                }
            }
        }
        catch
        {
            // Usage is advisory; failed directory scans should not block settings.
        }

        return (totalBytes, directoriesWithCacheData.Count, markerCount, markerBytes);
    }

    private static long CalculateMpvSessionDirectoryDataBytes(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        long totalBytes = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(file), MpvSessionActiveMarkerFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch
                {
                    // Ignore files that disappear during active playback or cleanup.
                }
            }
        }
        catch
        {
            // Usage is advisory; failed directory scans should not block settings.
        }

        return totalBytes;
    }

    private static (long TotalBytes, int FileCount, int DirectoryCount) CalculateDirectoryUsage(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return (0, 0, 0);
        }

        long totalBytes = 0;
        var fileCount = 0;
        var directoryCount = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                    fileCount++;
                }
                catch
                {
                    // Ignore files that disappear during active playback or cleanup.
                }
            }

            directoryCount = Directory.EnumerateDirectories(directory).Count();
        }
        catch
        {
            // Usage is advisory; failed directory scans should not block settings.
        }

        return (totalBytes, fileCount, directoryCount);
    }

    private (long TotalBytes, int ItemCount) CalculateLegacySegmentUsage()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            return (0, 0);
        }

        var countedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        var itemCount = 0;

        foreach (var indexPath in EnumerateFilesSafely(_cacheRoot, "segments.index.json"))
        {
            if (IsPathUnderManagedCacheRoot(indexPath))
            {
                continue;
            }

            var itemDirectory = Path.GetDirectoryName(indexPath);
            if (string.IsNullOrWhiteSpace(itemDirectory)
                || !countedDirectories.Add(Path.GetFullPath(itemDirectory)))
            {
                continue;
            }

            totalBytes += CalculateDirectoryUsage(itemDirectory).TotalBytes;
            itemCount++;
        }

        foreach (var segmentsDirectory in EnumerateDirectoriesSafely(_cacheRoot, "segments"))
        {
            if (IsPathUnderManagedCacheRoot(segmentsDirectory)
                || !countedDirectories.Add(Path.GetFullPath(segmentsDirectory)))
            {
                continue;
            }

            totalBytes += CalculateDirectoryUsage(segmentsDirectory).TotalBytes;
            itemCount++;
        }

        foreach (var segmentFile in EnumerateFilesSafely(_cacheRoot, "*.seg"))
        {
            if (IsPathUnderManagedCacheRoot(segmentFile)
                || IsPathUnderAnyRoot(segmentFile, countedDirectories))
            {
                continue;
            }

            try
            {
                totalBytes += new FileInfo(segmentFile).Length;
                itemCount++;
            }
            catch
            {
                // Ignore files that disappear during cleanup.
            }
        }

        return (totalBytes, itemCount);
    }

    private CacheClearCategoryResult DeleteInactiveMpvSessionCacheDirectories(CancellationToken cancellationToken)
    {
        var result = new CacheClearCategoryResult();
        if (!Directory.Exists(_mpvSessionRoot))
        {
            return result;
        }

        foreach (var sessionDirectory in EnumerateImmediateDirectoriesSafely(_mpvSessionRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsActiveMpvSessionCacheDirectory(sessionDirectory))
            {
                result.SkippedActiveCount++;
                result.SkippedActiveBytes += CalculateMpvSessionDirectoryDataBytes(sessionDirectory);
                continue;
            }

            if (TryDeleteDirectoryUnderRoot(sessionDirectory, _mpvSessionRoot))
            {
                result.DeletedCount++;
            }
            else
            {
                result.FailedCount++;
            }
        }

        return result;
    }

    private int CleanupEmptyInactiveMpvSessionDirectories(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_mpvSessionRoot))
        {
            return 0;
        }

        var deletedCount = 0;
        foreach (var sessionDirectory in EnumerateImmediateDirectoriesSafely(_mpvSessionRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsActiveMpvSessionCacheDirectory(sessionDirectory) || !IsDirectoryEmpty(sessionDirectory))
            {
                continue;
            }

            if (TryDeleteDirectoryUnderRoot(sessionDirectory, _mpvSessionRoot))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private CacheClearCategoryResult DeleteLegacySegmentResidue(CancellationToken cancellationToken)
    {
        var result = new CacheClearCategoryResult();
        if (!Directory.Exists(_cacheRoot))
        {
            return result;
        }

        var deletedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cacheRootFullPath = Path.GetFullPath(_cacheRoot);

        foreach (var indexPath in EnumerateFilesSafely(_cacheRoot, "segments.index.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsPathUnderManagedCacheRoot(indexPath))
            {
                continue;
            }

            var itemDirectory = Path.GetDirectoryName(indexPath);
            if (string.IsNullOrWhiteSpace(itemDirectory)
                || string.Equals(Path.GetFullPath(itemDirectory), cacheRootFullPath, StringComparison.OrdinalIgnoreCase)
                || !deletedRoots.Add(Path.GetFullPath(itemDirectory)))
            {
                continue;
            }

            if (TryDeleteDirectoryUnderRoot(itemDirectory, _cacheRoot))
            {
                result.DeletedCount++;
            }
            else
            {
                result.FailedCount++;
            }
        }

        foreach (var segmentsDirectory in EnumerateDirectoriesSafely(_cacheRoot, "segments"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsPathUnderManagedCacheRoot(segmentsDirectory)
                || !deletedRoots.Add(Path.GetFullPath(segmentsDirectory)))
            {
                continue;
            }

            if (TryDeleteDirectoryUnderRoot(segmentsDirectory, _cacheRoot))
            {
                result.DeletedCount++;
            }
            else
            {
                result.FailedCount++;
            }
        }

        foreach (var segmentFile in EnumerateFilesSafely(_cacheRoot, "*.seg"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsPathUnderManagedCacheRoot(segmentFile)
                || IsPathUnderAnyRoot(segmentFile, deletedRoots))
            {
                continue;
            }

            try
            {
                File.Delete(segmentFile);
                result.DeletedCount++;
            }
            catch
            {
                result.FailedCount++;
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateImmediateDirectoriesSafely(string directory)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory).ToList();
        }
        catch
        {
            yield break;
        }

        foreach (var item in directories)
        {
            yield return item;
        }
    }

    private static bool IsActiveMpvSessionCacheDirectory(string directory)
    {
        try
        {
            return File.Exists(Path.Combine(directory, MpvSessionActiveMarkerFileName));
        }
        catch
        {
            return true;
        }
    }

    private static bool TryDeleteDirectoryUnderRoot(string directory, string root)
    {
        try
        {
            if (!IsPathUnderRoot(directory, root) || !Directory.Exists(directory))
            {
                return false;
            }

            Directory.Delete(directory, recursive: true);
            return !Directory.Exists(directory);
        }
        catch
        {
            return false;
        }
    }

    private string? FindMpvSessionChildDirectory(string path)
    {
        try
        {
            var relative = Path.GetRelativePath(_mpvSessionRoot, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                return null;
            }

            var firstSeparator = relative.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            var childName = firstSeparator >= 0 ? relative[..firstSeparator] : relative;
            return string.IsNullOrWhiteSpace(childName) ? null : Path.Combine(_mpvSessionRoot, childName);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDirectoryEmpty(string directory)
    {
        try
        {
            return Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any();
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories).ToList();
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafely(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory, searchPattern, SearchOption.AllDirectories).ToList();
        }
        catch
        {
            yield break;
        }

        foreach (var item in directories)
        {
            yield return item;
        }
    }

    private bool IsPathUnderManagedCacheRoot(string path)
    {
        return IsPathUnderRoot(path, _itemsRoot) || IsPathUnderRoot(path, _mpvSessionRoot);
    }

    private static bool IsPathUnderAnyRoot(string path, IEnumerable<string> roots)
    {
        return roots.Any(root => IsPathUnderRoot(path, root));
    }

    private static bool IsPathUnderRoot(string path, string root)
    {
        try
        {
            if (!Directory.Exists(root) && !File.Exists(root))
            {
                return false;
            }

            var relative = Path.GetRelativePath(root, path);
            return !relative.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relative);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateLastAccessed(CacheItem cacheItem)
    {
        try
        {
            cacheItem.Manifest.LastAccessedAtUtc = DateTime.UtcNow;
            File.WriteAllText(
                Path.Combine(cacheItem.ItemDirectory, ManifestFileName),
                JsonSerializer.Serialize(cacheItem.Manifest, JsonOptions),
                Encoding.UTF8);
        }
        catch
        {
            // Last access time is a cache hint; playback should not depend on it.
        }
    }

    private static VideoCacheManifest CreateManifest(PlaybackSourceItem source, string cacheKey)
    {
        return new VideoCacheManifest
        {
            Version = 1,
            CacheMode = CacheModeComplete,
            CacheKey = cacheKey,
            MediaFileId = source.MediaFileId,
            SourceConnectionId = source.SourceConnectionId,
            FileName = source.FileName,
            Extension = NormalizeExtension(source.Extension, source.FileName),
            FileSize = source.FileSize,
            LastModifiedAtUtc = ToUtc(source.LastModifiedAt),
            CompletedAtUtc = DateTime.UtcNow,
            LastAccessedAtUtc = DateTime.UtcNow
        };
    }

    private static async Task WriteManifestAsync(
        string itemDirectory,
        VideoCacheManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(itemDirectory, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, Encoding.UTF8, cancellationToken);
    }

    private bool TryReadManifest(string manifestPath, out VideoCacheManifest manifest)
    {
        manifest = new VideoCacheManifest();
        try
        {
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            var json = File.ReadAllText(manifestPath, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<VideoCacheManifest>(json, JsonOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.CacheKey))
            {
                return false;
            }

            manifest = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<VideoCacheSettings> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        EnsureSettingsFile();
        try
        {
            var settingsPath = Path.Combine(_cacheRoot, SettingsFileName);
            var json = await File.ReadAllTextAsync(settingsPath, Encoding.UTF8, cancellationToken);
            var settings = JsonSerializer.Deserialize<VideoCacheSettings>(json, JsonOptions);
            if (settings is not null)
            {
                return NormalizeSettings(settings);
            }
        }
        catch
        {
            // Corrupt settings fall back to the default limit.
        }

        return new VideoCacheSettings();
    }

    private static VideoCacheSettings NormalizeSettings(VideoCacheSettings settings)
    {
        return new VideoCacheSettings
        {
            MaxBytes = settings.MaxBytes > 0 ? settings.MaxBytes : DefaultMaxBytes
        };
    }

    private static VideoCacheSettingsModel ToSettingsModel(VideoCacheSettings settings)
    {
        return new VideoCacheSettingsModel
        {
            MaxBytes = settings.MaxBytes
        };
    }

    private void EnsureSettingsFile()
    {
        Directory.CreateDirectory(_cacheRoot);
        var settingsPath = Path.Combine(_cacheRoot, SettingsFileName);
        if (File.Exists(settingsPath))
        {
            return;
        }

        var json = JsonSerializer.Serialize(new VideoCacheSettings(), JsonOptions);
        File.WriteAllText(settingsPath, json, Encoding.UTF8);
    }

    private void SetStatus(PlaybackSourceItem source, VideoCacheStatusResult status)
    {
        _lastStatuses[source.MediaFileId] = status;
        try
        {
            StatusChanged?.Invoke(
                this,
                new VideoCacheChangedEventArgs
                {
                    MediaFileId = source.MediaFileId,
                    SourceConnectionId = source.SourceConnectionId,
                    Status = status.Status,
                    ProgressPercent = status.ProgressPercent,
                    Error = status.Error
                });
        }
        catch
        {
            // Cache status notifications must not terminate background download or cleanup paths.
        }
    }

    private void AddLease(string cacheKey)
    {
        lock (_leaseLock)
        {
            _activeLeaseCounts.TryGetValue(cacheKey, out var count);
            _activeLeaseCounts[cacheKey] = count + 1;
        }
    }

    private void ReleaseLease(string cacheKey)
    {
        lock (_leaseLock)
        {
            if (!_activeLeaseCounts.TryGetValue(cacheKey, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _activeLeaseCounts.Remove(cacheKey);
                return;
            }

            _activeLeaseCounts[cacheKey] = count - 1;
        }
    }

    private bool IsLeaseActive(string cacheKey)
    {
        lock (_leaseLock)
        {
            return _activeLeaseCounts.TryGetValue(cacheKey, out var count) && count > 0;
        }
    }

    private bool HasActiveLease()
    {
        lock (_leaseLock)
        {
            return _activeLeaseCounts.Values.Any(count => count > 0);
        }
    }

    private void AddActivity(Dictionary<string, int> activityCounts, string cacheKey)
    {
        lock (_leaseLock)
        {
            activityCounts.TryGetValue(cacheKey, out var count);
            activityCounts[cacheKey] = count + 1;
        }
    }

    private bool IsDownloadActive(string cacheKey)
    {
        return _downloadsByMediaFileId.Values.Any(
            x => string.Equals(x.CacheKey, cacheKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCacheCandidate(PlaybackSourceItem source)
    {
        return source.ProtocolType == ProtocolType.WebDav
               && source.MediaFileId > 0
               && source.SourceConnectionId > 0
               && !string.IsNullOrWhiteSpace(source.PlaybackUrl)
               && source.FileSize > 0;
    }

    private static PlaybackSourceItem CloneSource(PlaybackSourceItem source)
    {
        return new PlaybackSourceItem
        {
            MediaFileId = source.MediaFileId,
            SourceConnectionId = source.SourceConnectionId,
            FileName = source.FileName,
            FilePath = source.FilePath,
            RemoteUri = source.RemoteUri,
            PlaybackUrl = source.PlaybackUrl,
            Extension = source.Extension,
            FileSize = source.FileSize,
            LastModifiedAt = source.LastModifiedAt,
            ProtocolType = source.ProtocolType,
            Username = source.Username,
            Password = source.Password
        };
    }

    private string GetCacheItemDirectory(string cacheKey)
    {
        var shard = cacheKey.Length >= 2 ? cacheKey[..2] : "00";
        return Path.Combine(_itemsRoot, shard, cacheKey);
    }

    private static string GetContentPath(string itemDirectory, string? extension, string? fileName)
    {
        return Path.Combine(itemDirectory, $"content{NormalizeExtension(extension, fileName)}");
    }

    private static string NormalizeExtension(string? extension, string? fileName)
    {
        var value = string.IsNullOrWhiteSpace(extension)
            ? Path.GetExtension(fileName ?? string.Empty)
            : extension.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return ".bin";
        }

        if (!value.StartsWith(".", StringComparison.Ordinal))
        {
            value = "." + value;
        }

        return Regex.IsMatch(value, @"^\.[A-Za-z0-9]{1,16}$")
            ? value.ToLowerInvariant()
            : ".bin";
    }

    private string BuildCacheKey(PlaybackSourceItem source)
    {
        var normalizedPath = WebDavPathHelper.NormalizeVirtualPath(source.FilePath);
        var remoteUriHash = HashString(source.RemoteUri ?? source.PlaybackUrl ?? string.Empty);
        var lastModifiedTicks = GetUtcTicks(source.LastModifiedAt);
        var input = string.Create(
            CultureInfo.InvariantCulture,
            $"v1|{source.SourceConnectionId}|{source.MediaFileId}|{normalizedPath}|{remoteUriHash}|{source.FileSize}|{lastModifiedTicks}");
        return HashString(input);
    }

    private static string HashString(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private bool IsSafeCacheItemDirectory(string itemDirectory)
    {
        try
        {
            var relative = Path.GetRelativePath(_itemsRoot, itemDirectory);
            if (relative.StartsWith("..", StringComparison.Ordinal)
                || Path.IsPathRooted(relative))
            {
                return false;
            }

            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length == 2
                   && Regex.IsMatch(parts[0], "^[a-f0-9]{2}$", RegexOptions.IgnoreCase)
                   && Regex.IsMatch(parts[1], "^[a-f0-9]{64}$", RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private bool IsPathUnderItemsRoot(string path)
    {
        try
        {
            var relative = Path.GetRelativePath(_itemsRoot, path);
            return !relative.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relative);
        }
        catch
        {
            return false;
        }
    }

    private bool TryDeleteCacheItemDirectory(string cacheKey, string itemDirectory, out string? error)
    {
        error = null;
        try
        {
            if (!IsSafeCacheItemDirectory(itemDirectory))
            {
                error = "缓存目录不合法，已拒绝删除。";
                return false;
            }

            if (IsLeaseActive(cacheKey))
            {
                error = "正在播放，停止后可删除。";
                return false;
            }

            if (IsDownloadActive(cacheKey))
            {
                error = "正在缓存，取消后可删除。";
                return false;
            }

            if (!Directory.Exists(itemDirectory))
            {
                return true;
            }

            Directory.Delete(itemDirectory, recursive: true);
            if (Directory.Exists(itemDirectory))
            {
                error = "缓存目录仍然存在。";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = SanitizeError(exception.Message);
            return false;
        }
    }

    private void DeleteCacheContentFiles(string itemDirectory)
    {
        try
        {
            if (!IsSafeCacheItemDirectory(itemDirectory) || !Directory.Exists(itemDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(itemDirectory, "content.*"))
            {
                DeleteFileIfExists(file);
            }

            DeleteFileIfExists(Path.Combine(itemDirectory, ManifestFileName));
            DeleteFileIfExists(Path.Combine(itemDirectory, PartialFileName));
        }
        catch
        {
        }
    }

    private void DeletePartialFile(string cacheKey)
    {
        DeleteFileIfExists(Path.Combine(GetCacheItemDirectory(cacheKey), PartialFileName));
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string SanitizeError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Video cache operation failed.";
        }

        var sanitized = Regex.Replace(value, @"https?://\S+", "[redacted-url]", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)(authorization|basic|bearer|token|password|passwd)\S*", "[redacted]");
        sanitized = sanitized.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 180 ? sanitized : sanitized[..180];
    }

    private static string SanitizeReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var sanitized = Regex.Replace(value, @"[^A-Za-z0-9_.-]", "-");
        return sanitized.Length <= 80 ? sanitized : sanitized[..80];
    }

    private static string CacheKeyPrefix(string cacheKey)
    {
        return cacheKey.Length <= 8 ? cacheKey : cacheKey[..8];
    }

    private static void SafeTrace(string message)
    {
        try
        {
            Debug.WriteLine("[VIDEO-CACHE] " + message);
        }
        catch
        {
        }
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
    }

    private static long GetUtcTicks(DateTime? value)
    {
        return ToUtc(value)?.Ticks ?? 0L;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();
        foreach (var state in _downloadsByMediaFileId.Values)
        {
            state.CancellationTokenSource.Cancel();
        }

        _disposeCts.Dispose();
        _downloadSemaphore.Dispose();
    }

    private sealed class VideoCacheSettings
    {
        public long MaxBytes { get; set; } = DefaultMaxBytes;
    }

    private sealed class VideoCacheManifest
    {
        public int Version { get; set; }

        public string CacheMode { get; set; } = CacheModeComplete;

        public string CacheKey { get; set; } = string.Empty;

        public int MediaFileId { get; set; }

        public int SourceConnectionId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string Extension { get; set; } = ".bin";

        public long FileSize { get; set; }

        public DateTime? LastModifiedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? LastAccessedAtUtc { get; set; }
    }

    private sealed class VideoCacheActivityLease : IDisposable
    {
        private readonly Action _release;
        private int _disposed;

        public VideoCacheActivityLease(Action release)
        {
            _release = release;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _release();
        }
    }

    private sealed class CacheClearCategoryResult
    {
        public int DeletedCount { get; set; }

        public int FailedCount { get; set; }

        public int SkippedActiveCount { get; set; }

        public long SkippedActiveBytes { get; set; }
    }

    private readonly record struct CacheItem(
        string CacheKey,
        string ItemDirectory,
        string ContentPath,
        VideoCacheManifest Manifest)
    {
        public DateTime? LastAccessedAtUtc => Manifest.LastAccessedAtUtc;

        public DateTime? CompletedAtUtc => Manifest.CompletedAtUtc;
    }

    private sealed class DownloadState
    {
        public DownloadState(int mediaFileId, int sourceConnectionId, string cacheKey)
        {
            MediaFileId = mediaFileId;
            SourceConnectionId = sourceConnectionId;
            CacheKey = cacheKey;
        }

        public int MediaFileId { get; }

        public int SourceConnectionId { get; }

        public string CacheKey { get; }

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        public double ProgressPercent { get; set; }
    }
}
