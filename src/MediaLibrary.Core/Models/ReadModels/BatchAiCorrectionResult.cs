namespace MediaLibrary.Core.Models.ReadModels;

public sealed class BatchAiCorrectionRunResult
{
    public IReadOnlyList<BatchAiCorrectionUnitResult> UnitResults { get; set; } = [];

    public int TotalCount { get; set; }

    public int SuccessCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public int CancelledCount { get; set; }
}

public sealed class BatchAiCorrectionUnitResult
{
    public string SelectionKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string UnitKind { get; set; } = string.Empty;

    public string TargetKind { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int? MediaFileId { get; set; }

    public int? SeasonId { get; set; }
}

public sealed class BatchAiCorrectionProgress
{
    public int TotalCount { get; set; }

    public int ProcessedCount { get; set; }

    public int SuccessCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public int CancelledCount { get; set; }

    public string CurrentTitle { get; set; } = string.Empty;
}
