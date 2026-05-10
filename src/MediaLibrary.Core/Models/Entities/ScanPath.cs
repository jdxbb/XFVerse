namespace MediaLibrary.Core.Models.Entities;

public sealed class ScanPath
{
    public int Id { get; set; }

    public int SourceConnectionId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool IsRecursive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SourceConnection? SourceConnection { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public ICollection<ScanTaskLog> ScanTaskLogs { get; set; } = new List<ScanTaskLog>();
}
