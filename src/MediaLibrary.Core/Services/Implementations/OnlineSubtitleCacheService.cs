using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class OnlineSubtitleCacheService : IOnlineSubtitleCacheService
{
    private const long MaxSubtitleFileBytes = 15L * 1024L * 1024L;
    private const long MaxSubtitlePackageBytes = 50L * 1024L * 1024L;
    private static readonly HashSet<string> ExtensionSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt",
        ".ass",
        ".ssa",
        ".vtt"
    };

    public IReadOnlySet<string> SupportedExtensions => ExtensionSet;

    public async Task<OnlineSubtitleCacheSaveResult> SaveAsync(
        string provider,
        string providerFileId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeOriginalFileName = Path.GetFileName(originalFileName ?? string.Empty);
        var extension = Path.GetExtension(safeOriginalFileName).ToLowerInvariant();
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return await SaveZipAsync(provider, providerFileId, content, cancellationToken);
        }

        if (!ExtensionSet.Contains(extension))
        {
            throw new InvalidOperationException("UnsupportedSubtitleExtension");
        }

        var bytes = await ReadBoundedAsync(content, MaxSubtitleFileBytes, cancellationToken);
        return await WriteSubtitleBytesAsync(
            provider,
            providerFileId,
            safeOriginalFileName,
            extension,
            bytes,
            cancellationToken);
    }

    public Task<OnlineSubtitleCacheUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var itemsDirectory = GetItemsDirectory();
        if (!Directory.Exists(itemsDirectory))
        {
            return Task.FromResult(new OnlineSubtitleCacheUsage());
        }

        long usedBytes = 0;
        var fileCount = 0;
        foreach (var file in Directory.EnumerateFiles(itemsDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                if (!ExtensionSet.Contains(info.Extension))
                {
                    continue;
                }

                usedBytes += info.Length;
                fileCount++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return Task.FromResult(
            new OnlineSubtitleCacheUsage
            {
                UsedBytes = usedBytes,
                FileCount = fileCount
            });
    }

    public Task<OnlineSubtitleCacheClearResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        var itemsDirectory = GetItemsDirectory();
        if (!Directory.Exists(itemsDirectory))
        {
            return Task.FromResult(new OnlineSubtitleCacheClearResult { Succeeded = true });
        }

        var deletedCount = 0;
        long freedBytes = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(itemsDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                if (!ExtensionSet.Contains(info.Extension))
                {
                    continue;
                }

                var length = info.Length;
                File.Delete(file);
                deletedCount++;
                freedBytes += length;
            }

            RemoveEmptyDirectories(itemsDirectory, cancellationToken);
            return Task.FromResult(
                new OnlineSubtitleCacheClearResult
                {
                    Succeeded = true,
                    DeletedFileCount = deletedCount,
                    FreedBytes = freedBytes
                });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(
                new OnlineSubtitleCacheClearResult
                {
                    Succeeded = false,
                    DeletedFileCount = deletedCount,
                    FreedBytes = freedBytes,
                    Error = exception.GetType().Name
                });
        }
    }

    public string GetAbsolutePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("SubtitleCachePathEmpty");
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var root = GetRootDirectory();
        var candidate = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
        if (!IsUnderRoot(root, candidate))
        {
            throw new InvalidOperationException("SubtitleCachePathEscapesRoot");
        }

        return candidate;
    }

    private async Task<OnlineSubtitleCacheSaveResult> SaveZipAsync(
        string provider,
        string providerFileId,
        Stream content,
        CancellationToken cancellationToken)
    {
        var packageBytes = await ReadBoundedAsync(content, MaxSubtitlePackageBytes, cancellationToken);
        await using var packageStream = new MemoryStream(packageBytes, writable: false);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Name) || IsUnsafeZipEntryName(entry.FullName))
            {
                continue;
            }

            var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (!ExtensionSet.Contains(extension))
            {
                continue;
            }

            if (entry.Length > MaxSubtitleFileBytes)
            {
                throw new InvalidOperationException("SubtitleFileTooLarge");
            }

            await using var entryStream = entry.Open();
            var subtitleBytes = await ReadBoundedAsync(entryStream, MaxSubtitleFileBytes, cancellationToken);
            return await WriteSubtitleBytesAsync(
                provider,
                providerFileId,
                entry.Name,
                extension,
                subtitleBytes,
                cancellationToken);
        }

        throw new InvalidOperationException("ZipDoesNotContainSupportedSubtitle");
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var target = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (target.Length + read > maxBytes)
            {
                throw new InvalidOperationException("SubtitleFileTooLarge");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (target.Length == 0)
        {
            throw new InvalidOperationException("SubtitleFileEmpty");
        }

        return target.ToArray();
    }

    private async Task<OnlineSubtitleCacheSaveResult> WriteSubtitleBytesAsync(
        string provider,
        string providerFileId,
        string originalFileName,
        string extension,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var safeProvider = SanitizeToken(provider, "provider");
        var safeFileId = SanitizeToken(providerFileId, "file");
        var fileName = $"{safeProvider}-{safeFileId}-{hash[..16]}{extension}";
        var relativePath = Path.Combine("items", safeProvider, fileName);
        var absolutePath = GetAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllBytesAsync(absolutePath, bytes, cancellationToken);

        return new OnlineSubtitleCacheSaveResult
        {
            RelativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/'),
            FileName = string.IsNullOrWhiteSpace(originalFileName) ? fileName : Path.GetFileName(originalFileName),
            Hash = hash,
            Extension = extension,
            Bytes = bytes.LongLength
        };
    }

    private static string SanitizeToken(string value, string fallback)
    {
        var normalized = Regex.Replace(value ?? string.Empty, "[^A-Za-z0-9._-]+", "-").Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized[..Math.Min(normalized.Length, 80)];
    }

    private static bool IsUnsafeZipEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return true;
        }

        var normalized = entryName.Replace('\\', '/');
        return normalized.StartsWith("/", StringComparison.Ordinal)
               || normalized.Contains("../", StringComparison.Ordinal)
               || normalized.Contains("..\\", StringComparison.Ordinal)
               || Path.IsPathFullyQualified(entryName);
    }

    private static string GetRootDirectory()
    {
        return Path.GetFullPath(AppPaths.GetOnlineSubtitleCacheDirectory());
    }

    private static string GetItemsDirectory()
    {
        var directory = Path.Combine(GetRootDirectory(), "items");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static bool IsUnderRoot(string root, string candidate)
    {
        var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveEmptyDirectories(string root, CancellationToken cancellationToken)
    {
        foreach (var directory in Directory
                     .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }
}
