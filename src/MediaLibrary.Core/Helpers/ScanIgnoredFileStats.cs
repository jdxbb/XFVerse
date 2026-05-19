using System.Globalization;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.Core.Helpers;

public sealed class ScanIgnoredFileStats
{
    private const int MaxSamplesPerExtension = 3;

    private readonly Dictionary<string, int> _reasonCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _extensionCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _samplesByExtension = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _reasonCounts.Values.Sum();

    public int UnsupportedExtensionIgnoredCount => GetReasonCount("unsupported-extension");

    public int DuplicatePathIgnoredCount => GetReasonCount("duplicate-path");

    public void Add(string reason, string fileNameOrPath)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "other" : reason.Trim();
        _reasonCounts[normalizedReason] = GetReasonCount(normalizedReason) + 1;

        var extension = GetExtensionKey(fileNameOrPath);
        _extensionCounts[extension] = _extensionCounts.GetValueOrDefault(extension) + 1;
        if (!_samplesByExtension.TryGetValue(extension, out var samples))
        {
            samples = [];
            _samplesByExtension[extension] = samples;
        }

        if (samples.Count < MaxSamplesPerExtension)
        {
            samples.Add(Path.GetFileName(fileNameOrPath.Replace('\\', '/')));
        }
    }

    public void WriteDiagnostics(string source, int ignoredFileCount)
    {
        var ignoredByReason = FormatCounts(_reasonCounts);
        var ignoredByExtension = FormatCounts(_extensionCounts);
        var samples = FormatSamples();
        var videoWhitelist = string.Join('|', MediaFileRules.VideoExtensions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var subtitleWhitelist = string.Join('|', MediaFileRules.SubtitleExtensions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        ScanIdentificationDiagnostics.Write(
            $"event=scan-ignored-files-summary source={ScanIdentificationDiagnostics.FormatValue(source)} ignoredFileCount={ignoredFileCount} ignoredByReason={ScanIdentificationDiagnostics.FormatValue(ignoredByReason, 360)} ignoredByExtension={ScanIdentificationDiagnostics.FormatValue(ignoredByExtension, 500)} ignoredSampleNamesByExtension={ScanIdentificationDiagnostics.FormatValue(samples, 800)} duplicatePathIgnoredCount={DuplicatePathIgnoredCount} unsupportedExtensionIgnoredCount={UnsupportedExtensionIgnoredCount} videoWhitelist={ScanIdentificationDiagnostics.FormatValue(videoWhitelist)} subtitleWhitelist={ScanIdentificationDiagnostics.FormatValue(subtitleWhitelist)}");
    }

    private int GetReasonCount(string reason)
    {
        return _reasonCounts.TryGetValue(reason, out var count) ? count : 0;
    }

    private static string GetExtensionKey(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return "(none)";
        }

        var extension = Path.GetExtension(fileNameOrPath);
        return string.IsNullOrWhiteSpace(extension) ? "(none)" : extension.ToLowerInvariant();
    }

    private static string FormatCounts(Dictionary<string, int> counts)
    {
        return string.Join(
            '|',
            counts
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Key}={x.Value.ToString(CultureInfo.InvariantCulture)}"));
    }

    private string FormatSamples()
    {
        return string.Join(
            '|',
            _samplesByExtension
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Key}:{string.Join(',', x.Value.Select(ScanIdentificationDiagnostics.FormatFileNameFingerprint))}"));
    }
}
