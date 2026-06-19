using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanTaskLogItem
{
    public int Id { get; set; }

    public int? ScanPathId { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string ScanPathDisplayName { get; set; } = string.Empty;

    public string ScanPath { get; set; } = string.Empty;

    public ScanTaskType TaskType { get; set; }

    public ScanTaskStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int ScannedCount { get; set; }

    public int NewFileCount { get; set; }

    public int UpdatedFileCount { get; set; }

    public int IgnoredFileCount { get; set; }

    public int ErrorCount { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public string ReasonSummaryJson { get; set; } = string.Empty;

    public string ReasonSummaryText { get; set; } = string.Empty;

    public string TopReasonSummaryText { get; set; } = string.Empty;
}
