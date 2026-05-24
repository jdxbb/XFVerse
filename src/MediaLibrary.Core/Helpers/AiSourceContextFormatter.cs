using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MediaLibrary.Core.Helpers;

public static partial class AiSourceContextFormatter
{
    public static string BuildPathHint(string? filePath, string? remoteUri = null, int keepSegments = 6)
    {
        var value = !string.IsNullOrWhiteSpace(filePath) ? filePath : remoteUri;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace('\\', '/').Trim();
        normalized = QueryStringRegex().Replace(normalized, string.Empty);
        normalized = UrlAuthorityRegex().Replace(normalized, "/");
        normalized = WindowsDriveRegex().Replace(normalized, "/");
        normalized = SecretAssignmentRegex().Replace(normalized, "$1=<redacted>");
        normalized = normalized.Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Where(segment => !segment.Contains(':', StringComparison.Ordinal))
            .ToArray();
        if (segments.Length == 0)
        {
            return ShortHash(normalized);
        }

        var effectiveKeepSegments = Math.Clamp(keepSegments, 2, 10);
        var tail = segments.Skip(Math.Max(0, segments.Length - effectiveKeepSegments)).ToArray();
        return segments.Length > effectiveKeepSegments
            ? ".../" + string.Join("/", tail)
            : string.Join("/", tail);
    }

    public static string BuildSourceLine(
        int? episodeNumber,
        string? fileName,
        string? filePath,
        string? remoteUri = null)
    {
        var episodeText = episodeNumber.HasValue && episodeNumber.Value > 0
            ? $"E{episodeNumber.Value}"
            : "E?";
        var safeFileName = Path.GetFileName(fileName ?? string.Empty);
        var pathHint = BuildPathHint(filePath, remoteUri);
        return string.IsNullOrWhiteSpace(pathHint)
            ? $"{episodeText} file={safeFileName}"
            : $"{episodeText} file={safeFileName} pathHint={pathHint}";
    }

    private static string ShortHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant())))
            .ToLowerInvariant()[..10];
    }

    [GeneratedRegex(@"\?.*$", RegexOptions.CultureInvariant)]
    private static partial Regex QueryStringRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*://[^/]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlAuthorityRegex();

    [GeneratedRegex(@"^[A-Za-z]:", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDriveRegex();

    [GeneratedRegex(@"(api[_-]?key|access[_-]?token|authorization|bearer|password|pwd|token)\s*[:=]\s*[^\s&]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();
}
