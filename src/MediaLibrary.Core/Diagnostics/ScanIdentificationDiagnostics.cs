using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Diagnostics;

public static partial class ScanIdentificationDiagnostics
{
    private static readonly object SyncRoot = new();
    private static string? _logPath;

    public static void Write(string message)
    {
        if (!DiagnosticMessageFilter.ShouldWrite(message, "XFVERSE_SCAN_IDENTIFICATION_DIAGNOSTICS"))
        {
            return;
        }

        try
        {
            var safeMessage = DiagnosticLogSanitizer.Sanitize(message);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [SCAN-ID] {safeMessage}";
            Debug.WriteLine(line);

            var logPath = _logPath ??= ResolveLogPath();
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
            // Identification diagnostics must never affect scan or metadata binding.
        }
    }

    public static string FormatValue(string? value, int maxLength = 180)
    {
        var sanitized = SanitizeText(value, maxLength);
        return string.IsNullOrWhiteSpace(sanitized)
            ? "(none)"
            : $"\"{sanitized.Replace("\"", "'", StringComparison.Ordinal)}\"";
    }

    public static string FormatPath(string? path, int keepSegments = 5)
    {
        var sanitized = SanitizePath(path, keepSegments);
        return FormatValue(sanitized, 260);
    }

    public static string FormatFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "(none)";
        }

        var normalized = fileName.Replace('\\', '/').Trim();
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return FormatValue(lastSeparatorIndex >= 0 ? normalized[(lastSeparatorIndex + 1)..] : normalized, 180);
    }

    public static string FormatFileNameFingerprint(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "(none)";
        }

        var normalized = fileName.Replace('\\', '/').Trim();
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        var name = lastSeparatorIndex >= 0 ? normalized[(lastSeparatorIndex + 1)..] : normalized;
        var extension = Path.GetExtension(name);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant())))
            .ToLowerInvariant()[..10];
        var extensionText = string.IsNullOrWhiteSpace(extension) ? "(none)" : extension.ToLowerInvariant();
        return FormatValue($"ext={extensionText} hash={hash}", 80);
    }

    public static string FormatNullable(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "(none)";
    }

    public static string FormatConfidence(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.000", CultureInfo.InvariantCulture) : "(none)";
    }

    public static void WriteFinalAiCandidateRanges(string source, TvScanDirectoryAnalysisResult result)
    {
        var rangesWithFiles = result.AiCandidateRanges.Count(x => x.MediaFileIds.Count > 0);
        Write(
            $"event=scan-final-ai-candidate-ranges source={source} finalAiCandidateRangesCount={result.AiCandidateRanges.Count} uniqueAiCandidateDirs={result.AiCandidateRanges.Select(x => x.SanitizedPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()} mergedRangeCount={result.AiCandidateRangeMergedCount} deduplicatedEntryCount={result.AiCandidateRangeDeduplicatedEntryCount} rangesWithFiles={rangesWithFiles} rangesWithoutFiles={Math.Max(0, result.AiCandidateRanges.Count - rangesWithFiles)} fullAiRangeAnalysis=disabled reason=deferred-to-ai-on-uncertain");

        foreach (var range in result.AiCandidateRanges.OrderBy(x => x.SanitizedPath, StringComparer.OrdinalIgnoreCase))
        {
            Write(
                $"event=scan-final-ai-candidate-range source={source} directory={FormatValue(range.SanitizedPath, 260)} rangeType={FormatValue(range.RangeType)} riskTags={FormatValue(string.Join('|', range.RiskTags))} sourceFiles={range.SourceFileCount} directVideoCount={range.DirectVideoCount} childFolderCount={range.ChildFolderCount} rangeMediaFileCount={range.MediaFileIds.Count} rangeHasMediaFiles={(range.MediaFileIds.Count > 0).ToString().ToLowerInvariant()} sampleDirectVideoFiles={FormatValue(string.Join('|', range.SampleDirectVideoFiles))} suspectedSeriesFolder={FormatValue(range.SuspectedSeriesFolder, 260)} suspectedSeasonFolder={FormatValue(range.SuspectedSeasonFolder, 260)} usableCandidateQueries={FormatValue(string.Join('|', range.UsableCandidateQueries), 300)} noisyCandidateQueries={FormatValue(string.Join('|', range.NoisyCandidateQueries), 300)} rejectedCandidateQueries={FormatValue(string.Join('|', range.RejectedCandidateQueries), 300)} candidateQueries={FormatValue(string.Join('|', range.CandidateQueries), 300)} blockedMovieFallbackCount={range.BlockedMovieFallbackCount} candidateConflictsCount={range.CandidateConflictsCount} chineseStructureHints={FormatValue(string.Join('|', range.ChineseStructureHints))} finalDecision=ai-candidate");
        }
    }

    private static string ResolveLogPath()
    {
        return DiagnosticLogPathResolver.Resolve("scan-identification-debug.log");
    }

    private static string SanitizePath(string? path, int keepSegments)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        normalized = QueryStringRegex().Replace(normalized, string.Empty);
        normalized = UrlAuthorityRegex().Replace(normalized, "/");
        normalized = WindowsDriveRegex().Replace(normalized, "/");
        normalized = SecretAssignmentRegex().Replace(normalized, "$1=<redacted>");
        normalized = normalized.Trim('/');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "/";
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (segments.Length == 0)
        {
            return "/";
        }

        var effectiveKeepSegments = Math.Clamp(keepSegments, 1, 8);
        var tail = segments.Skip(Math.Max(0, segments.Length - effectiveKeepSegments)).ToArray();
        return segments.Length > effectiveKeepSegments
            ? "/.../" + string.Join("/", tail)
            : "/" + string.Join("/", tail);
    }

    private static string SanitizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        sanitized = SecretAssignmentRegex().Replace(sanitized, "$1=<redacted>");
        sanitized = QueryStringRegex().Replace(sanitized, string.Empty);
        sanitized = WhitespaceRegex().Replace(sanitized, " ").Trim();

        var effectiveMaxLength = Math.Max(16, maxLength);
        return sanitized.Length <= effectiveMaxLength ? sanitized : sanitized[..effectiveMaxLength] + "...";
    }

    [GeneratedRegex(@"\?.*$", RegexOptions.CultureInvariant)]
    private static partial Regex QueryStringRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*://[^/]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlAuthorityRegex();

    [GeneratedRegex(@"^[A-Za-z]:", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDriveRegex();

    [GeneratedRegex(@"(api[_-]?key|access[_-]?token|authorization|bearer|password|pwd|token)\s*[:=]\s*[^\s&]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
