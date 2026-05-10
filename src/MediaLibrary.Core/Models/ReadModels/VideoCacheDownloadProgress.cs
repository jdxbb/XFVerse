namespace MediaLibrary.Core.Models.ReadModels;

public sealed class VideoCacheDownloadProgress
{
    public long BytesReceived { get; init; }

    public long? TotalBytes { get; init; }

    public double Percent => TotalBytes.GetValueOrDefault() > 0
        ? Math.Clamp(BytesReceived * 100d / TotalBytes!.Value, 0d, 100d)
        : 0d;
}
