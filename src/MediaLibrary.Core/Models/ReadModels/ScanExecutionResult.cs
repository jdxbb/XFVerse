namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanExecutionResult
{
    public string StatusMessage { get; set; } = string.Empty;

    public int ProcessedPathCount { get; set; }

    public int TotalScannedCount { get; set; }

    public int NewFileCount { get; set; }

    public int UpdatedFileCount { get; set; }

    public int IgnoredFileCount { get; set; }

    public int ErrorCount { get; set; }
}
