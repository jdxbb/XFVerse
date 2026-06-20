using System.Text.RegularExpressions;

namespace MediaLibrary.Core.Diagnostics;

/// <summary>
/// Removes common credentials, URLs and absolute paths before diagnostic text is persisted.
/// </summary>
public static partial class DiagnosticLogSanitizer
{
    private const int MaximumMessageLength = 4000;

    /// <summary>
    /// Produces a single-line diagnostic value that does not expose common private fields.
    /// </summary>
    public static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "event=empty";
        }

        var sanitized = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        sanitized = SecretAssignmentRegex().Replace(sanitized, "$1=<redacted>");
        sanitized = UserNameAssignmentRegex().Replace(sanitized, "$1=<redacted>");
        sanitized = HttpUrlRegex().Replace(sanitized, "<url>");
        sanitized = WindowsAbsolutePathRegex().Replace(sanitized, "<path>");
        sanitized = UncPathRegex().Replace(sanitized, "<unc-path>");
        sanitized = WhitespaceRegex().Replace(sanitized, " ");

        return sanitized.Length <= MaximumMessageLength
            ? sanitized
            : sanitized[..MaximumMessageLength] + "...";
    }

    [GeneratedRegex(
        @"(api[_-]?key|access[_-]?token|refresh[_-]?token|authorization|bearer|password|pwd|token)\s*[:=]\s*[^\s&]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(
        @"(username|user)\s*[:=]\s*[^\s&]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserNameAssignmentRegex();

    [GeneratedRegex(
        @"https?://[^\s""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9])[A-Za-z]:\\[^\s""']+",
        RegexOptions.CultureInvariant)]
    private static partial Regex WindowsAbsolutePathRegex();

    [GeneratedRegex(
        @"\\\\[^\\\s""']+\\[^\s""']+",
        RegexOptions.CultureInvariant)]
    private static partial Regex UncPathRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
