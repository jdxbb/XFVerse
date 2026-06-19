using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class ScanTaskLog
{
    public int Id { get; set; }

    public int SourceConnectionId { get; set; }

    public int? ScanPathId { get; set; }

    public ScanTaskType TaskType { get; set; } = ScanTaskType.IncrementalScan;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public ScanTaskStatus Status { get; set; } = ScanTaskStatus.Pending;

    public int ScannedCount { get; set; }

    public int NewFileCount { get; set; }

    public int UpdatedFileCount { get; set; }

    public int IgnoredFileCount { get; set; }

    public int ErrorCount { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SourceBaseUrlSnapshot { get; set; }

    public string? SourceUsernameSnapshot { get; set; }

    public string? ScanPathSnapshot { get; set; }

    public string? ScanPathDisplayNameSnapshot { get; set; }

    public string? ReasonSummaryJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SourceConnection? SourceConnection { get; set; }

    public ScanPath? ScanPath { get; set; }
}
