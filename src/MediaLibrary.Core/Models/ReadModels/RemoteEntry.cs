namespace MediaLibrary.Core.Models.ReadModels;

public sealed class RemoteEntry
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string RemoteUri { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public long? ContentLength { get; set; }

    public DateTime? LastModifiedAt { get; set; }
}
