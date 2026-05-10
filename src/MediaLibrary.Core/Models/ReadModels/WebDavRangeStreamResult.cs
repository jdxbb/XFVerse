using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WebDavRangeStreamResult : IDisposable
{
    public WebDavRangeDownloadStatus Status { get; init; }

    public int? HttpStatusCode { get; init; }

    public Stream? ContentStream { get; init; }

    public long? ContentLength { get; init; }

    public string? Error { get; init; }

    internal HttpResponseMessage? Response { get; init; }

    public void Dispose()
    {
        ContentStream?.Dispose();
        Response?.Dispose();
    }
}
