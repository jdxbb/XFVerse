namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanPathSummaryItem
{
    public int Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool IsRecursive { get; set; }
}
