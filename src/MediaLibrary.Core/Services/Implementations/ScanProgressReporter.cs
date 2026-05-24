using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Implementations;

internal sealed class ScanProgressReporter
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(250);

    private readonly IProgress<ScanProgressUpdate>? _progress;
    private DateTime _lastReportedAtUtc = DateTime.MinValue;
    private string _lastStageKey = string.Empty;
    private string _lastCurrentItemName = string.Empty;

    public ScanProgressReporter(IProgress<ScanProgressUpdate>? progress)
    {
        _progress = progress;
    }

    public void Report(
        string stageKey,
        string stageText,
        string currentItemName = "",
        int scannedCount = 0,
        int newFileCount = 0,
        int updatedFileCount = 0,
        int ignoredFileCount = 0,
        int errorCount = 0,
        bool force = false)
    {
        if (_progress is null)
        {
            return;
        }

        var safeStageKey = string.IsNullOrWhiteSpace(stageKey) ? "unknown" : stageKey.Trim();
        var safeStageText = string.IsNullOrWhiteSpace(stageText) ? "处理中" : stageText.Trim();
        var safeItemName = SanitizeItemName(currentItemName);
        var now = DateTime.UtcNow;
        var stageChanged = !string.Equals(_lastStageKey, safeStageKey, StringComparison.Ordinal);
        var itemChanged = !string.Equals(_lastCurrentItemName, safeItemName, StringComparison.Ordinal);
        if (!force && !stageChanged && (!itemChanged || now - _lastReportedAtUtc < MinimumInterval))
        {
            return;
        }

        _lastReportedAtUtc = now;
        _lastStageKey = safeStageKey;
        _lastCurrentItemName = safeItemName;
        _progress.Report(
            new ScanProgressUpdate
            {
                StageKey = safeStageKey,
                StageText = safeStageText,
                CurrentItemName = safeItemName,
                ScannedCount = scannedCount,
                NewFileCount = newFileCount,
                UpdatedFileCount = updatedFileCount,
                IgnoredFileCount = ignoredFileCount,
                ErrorCount = errorCount
            });
    }

    private static string SanitizeItemName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace('\\', '/').Trim();
        var fileName = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(fileName)
            ? string.Empty
            : fileName;
    }
}
