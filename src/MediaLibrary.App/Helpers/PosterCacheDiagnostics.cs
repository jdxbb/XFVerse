using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.App.Helpers;

public static class PosterCacheDiagnostics
{
    private static readonly bool IsEnabled =
        string.Equals(
            Environment.GetEnvironmentVariable("XFVERSE_POSTER_CACHE_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
    private static readonly object SyncRoot = new();
    private static string? _logPath;

    public static string LogPath => _logPath ??= ResolveLogPath();

    public static void Write(string eventName, string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            var safeEventName = DiagnosticLogSanitizer.Sanitize(eventName);
            var safeMessage = DiagnosticLogSanitizer.Sanitize(message);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [POSTER-CACHE] event={safeEventName} {safeMessage}";
            Debug.WriteLine(line);

            var logPath = LogPath;
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (SyncRoot)
            {
                File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Poster diagnostics must never affect UI rendering.
        }
    }

    public static string SourceId(string? source)
    {
        if (!IsEnabled)
        {
            return "disabled";
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return "empty";
        }

        var normalized = source.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant()[..10];
        return $"{SourceKind(normalized)}:{hash}";
    }

    public static string SourceKind(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "empty";
        }

        if (Uri.TryCreate(source.Trim(), UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return "file";
            }

            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                return "remote";
            }

            return "absolute";
        }

        return Path.IsPathRooted(source) ? "path" : "relative";
    }

    private static string ResolveLogPath()
    {
        return DiagnosticLogPathResolver.Resolve("poster-cache-debug.log");
    }
}
