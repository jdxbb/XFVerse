namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanProgressUpdate
{
    public string StageKey { get; set; } = string.Empty;

    public string StageText { get; set; } = string.Empty;

    public string CurrentItemName { get; set; } = string.Empty;

    public int ScannedCount { get; set; }

    public int NewFileCount { get; set; }

    public int UpdatedFileCount { get; set; }

    public int IgnoredFileCount { get; set; }

    public int ErrorCount { get; set; }
}
