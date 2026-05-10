using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WebDavRangeDownloadResult
{
    public WebDavRangeDownloadStatus Status { get; init; }

    public int? HttpStatusCode { get; init; }

    public long BytesReceived { get; init; }

    public string? Error { get; init; }
}
