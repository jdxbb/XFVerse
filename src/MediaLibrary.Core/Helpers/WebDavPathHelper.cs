namespace MediaLibrary.Core.Helpers;

public static class WebDavPathHelper
{
    public static Uri CreateBaseUri(string baseUrl)
    {
        return new Uri($"{baseUrl.Trim().TrimEnd('/')}/", UriKind.Absolute);
    }

    public static string NormalizeVirtualPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    public static Uri BuildDirectoryUri(Uri baseUri, string directoryPath)
    {
        var normalizedPath = NormalizeVirtualPath(directoryPath);
        if (normalizedPath == "/")
        {
            return baseUri;
        }

        var baseAbsolutePath = baseUri.AbsolutePath.TrimEnd('/');
        if (baseAbsolutePath.EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return baseUri;
        }

        return new Uri(baseUri, $"{normalizedPath.TrimStart('/')}/");
    }

    public static bool TryGetVirtualPath(
        Uri requestDirectoryUri,
        string requestDirectoryPath,
        string href,
        out string virtualPath)
    {
        virtualPath = "/";
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        if (!Uri.TryCreate(requestDirectoryUri, href, out var entryUri))
        {
            return false;
        }

        var normalizedDirectoryPath = NormalizeVirtualPath(requestDirectoryPath);
        if (requestDirectoryUri.IsBaseOf(entryUri))
        {
            var relative = Uri.UnescapeDataString(requestDirectoryUri.MakeRelativeUri(entryUri).ToString());
            virtualPath = CombineVirtualPath(normalizedDirectoryPath, relative);
            return true;
        }

        var requestAbsolutePath = requestDirectoryUri.AbsolutePath.TrimEnd('/');
        var entryPath = Uri.UnescapeDataString(entryUri.AbsolutePath);
        if (!entryPath.StartsWith(requestAbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = entryPath[requestAbsolutePath.Length..].TrimStart('/');
        virtualPath = CombineVirtualPath(normalizedDirectoryPath, relativePath);
        return true;
    }

    public static string GetFileName(string virtualPath)
    {
        var normalized = NormalizeVirtualPath(virtualPath);
        return normalized == "/" ? string.Empty : normalized.Split('/').Last();
    }

    public static bool TryGetVirtualPath(Uri baseUri, string href, out string virtualPath)
    {
        virtualPath = "/";
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        if (!Uri.TryCreate(baseUri, href, out var entryUri))
        {
            return false;
        }

        if (baseUri.IsBaseOf(entryUri))
        {
            var relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(entryUri).ToString());
            virtualPath = NormalizeVirtualPath(relative);
            return true;
        }

        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        var entryPath = Uri.UnescapeDataString(entryUri.AbsolutePath);
        if (entryPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            virtualPath = NormalizeVirtualPath(entryPath[basePath.Length..]);
            return true;
        }

        return false;
    }

    public static int MatchPathDepth(string virtualPath)
    {
        return NormalizeVirtualPath(virtualPath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    public static string CombineVirtualPath(string directoryPath, string relativePath)
    {
        var normalizedDirectoryPath = NormalizeVirtualPath(directoryPath);
        var normalizedRelativePath = Uri.UnescapeDataString(relativePath)
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return normalizedDirectoryPath;
        }

        return normalizedDirectoryPath == "/"
            ? NormalizeVirtualPath(normalizedRelativePath)
            : NormalizeVirtualPath($"{normalizedDirectoryPath}/{normalizedRelativePath}");
    }

    public static string BuildPlaybackUrl(string baseUrl, string filePath, string? remoteUri)
    {
        var baseUri = CreateBaseUri(baseUrl);
        if (!string.IsNullOrWhiteSpace(remoteUri)
            && Uri.TryCreate(remoteUri, UriKind.Absolute, out var absoluteRemoteUri))
        {
            if (IsUnderCurrentBaseUri(baseUri, absoluteRemoteUri))
            {
                return absoluteRemoteUri.AbsoluteUri;
            }
        }

        var normalizedFilePath = NormalizeVirtualPath(filePath);
        var baseSegments = baseUri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileSegments = normalizedFilePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        var overlap = 0;
        var maxOverlap = Math.Min(baseSegments.Length, fileSegments.Length);
        for (var count = 1; count <= maxOverlap; count++)
        {
            var baseSuffix = baseSegments.Skip(baseSegments.Length - count);
            var filePrefix = fileSegments.Take(count);
            if (baseSuffix.SequenceEqual(filePrefix, StringComparer.OrdinalIgnoreCase))
            {
                overlap = count;
            }
        }

        var remaining = fileSegments.Skip(overlap).Select(Uri.EscapeDataString);
        return new Uri(baseUri, string.Join("/", remaining)).AbsoluteUri;
    }

    private static bool IsUnderCurrentBaseUri(Uri baseUri, Uri remoteUri)
    {
        if (!string.Equals(baseUri.Scheme, remoteUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(baseUri.Host, remoteUri.Host, StringComparison.OrdinalIgnoreCase)
            || baseUri.Port != remoteUri.Port)
        {
            return false;
        }

        var basePath = Uri.UnescapeDataString(baseUri.AbsolutePath).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return true;
        }

        var remotePath = Uri.UnescapeDataString(remoteUri.AbsolutePath).TrimEnd('/');
        return remotePath.Equals(basePath, StringComparison.OrdinalIgnoreCase)
               || remotePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase);
    }
}
