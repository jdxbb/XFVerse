namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanOverviewModel
{
    public bool HasConnection { get; set; }

    public string ConnectionName { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public DateTime? LastScanAt { get; set; }

    public IReadOnlyList<ScanPathSummaryItem> EnabledScanPaths { get; set; } = [];

    public IReadOnlyList<ScanTaskLogItem> RecentLogs { get; set; } = [];
}
