using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Entities;

public sealed class SourceConnection
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ProtocolType ProtocolType { get; set; } = ProtocolType.WebDav;

    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordEncrypted { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastConnectedAt { get; set; }

    public DateTime? LastScanAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ScanPath> ScanPaths { get; set; } = new List<ScanPath>();

    public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public ICollection<ScanTaskLog> ScanTaskLogs { get; set; } = new List<ScanTaskLog>();
}
