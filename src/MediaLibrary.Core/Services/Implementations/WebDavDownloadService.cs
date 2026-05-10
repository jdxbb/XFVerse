using System.Net.Http.Headers;
using System.Text;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WebDavDownloadService : IWebDavDownloadService, IDisposable
{
    private const int BufferSize = 1024 * 256;

    private readonly HttpClient _httpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public async Task DownloadAsync(
        WebDavDownloadRequest request,
        IProgress<VideoCacheDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(request.DownloadUrl, UriKind.Absolute, out var requestUri))
        {
            throw new InvalidOperationException("WebDAV download URL is invalid.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath) ?? string.Empty);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var rawCredential = $"{request.Username}:{request.Password}";
            var encodedCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredential));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredential);
        }

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
        }

        var totalBytes = request.ExpectedBytes.GetValueOrDefault() > 0
            ? request.ExpectedBytes
            : response.Content.Headers.ContentLength;

        await using var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var localStream = new FileStream(
            request.DestinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[BufferSize];
        long bytesReceived = 0;
        while (true)
        {
            var read = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await localStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesReceived += read;
            progress?.Report(
                new VideoCacheDownloadProgress
                {
                    BytesReceived = bytesReceived,
                    TotalBytes = totalBytes
                });
        }

        await localStream.FlushAsync(cancellationToken);
    }

    public async Task<WebDavRangeDownloadResult> DownloadRangeAsync(
        WebDavDownloadRequest request,
        long start,
        long end,
        CancellationToken cancellationToken = default)
    {
        if (start < 0 || end < start)
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.RangeNotSatisfiable,
                Error = "Invalid range."
            };
        }

        if (!Uri.TryCreate(request.DownloadUrl, UriKind.Absolute, out var requestUri))
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.Failed,
                Error = "WebDAV download URL is invalid."
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath) ?? string.Empty);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        httpRequest.Headers.Range = new RangeHeaderValue(start, end);
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var rawCredential = $"{request.Username}:{request.Password}";
            var encodedCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredential));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredential);
        }

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var statusCode = (int)response.StatusCode;
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.RangeNotSupported,
                HttpStatusCode = statusCode,
                Error = "Range requests are not supported."
            };
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.Unauthorized,
                HttpStatusCode = statusCode,
                Error = $"HTTP {statusCode}"
            };
        }

        if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.RangeNotSatisfiable,
                HttpStatusCode = statusCode,
                Error = $"HTTP {statusCode}"
            };
        }

        if (response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.Failed,
                HttpStatusCode = statusCode,
                Error = $"HTTP {statusCode}"
            };
        }

        await using var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var localStream = new FileStream(
            request.DestinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var expectedBytes = end - start + 1;
        var buffer = new byte[BufferSize];
        long bytesReceived = 0;
        while (true)
        {
            var read = await remoteStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await localStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesReceived += read;
        }

        await localStream.FlushAsync(cancellationToken);
        if (bytesReceived != expectedBytes)
        {
            return new WebDavRangeDownloadResult
            {
                Status = WebDavRangeDownloadStatus.Failed,
                HttpStatusCode = statusCode,
                BytesReceived = bytesReceived,
                Error = $"Range length mismatch ({bytesReceived}/{expectedBytes})."
            };
        }

        return new WebDavRangeDownloadResult
        {
            Status = WebDavRangeDownloadStatus.Success,
            HttpStatusCode = statusCode,
            BytesReceived = bytesReceived
        };
    }

    public async Task<WebDavRangeStreamResult> OpenRangeStreamAsync(
        WebDavDownloadRequest request,
        long start,
        long end,
        CancellationToken cancellationToken = default)
    {
        if (start < 0 || end < start)
        {
            return new WebDavRangeStreamResult
            {
                Status = WebDavRangeDownloadStatus.RangeNotSatisfiable,
                Error = "Invalid range."
            };
        }

        if (!Uri.TryCreate(request.DownloadUrl, UriKind.Absolute, out var requestUri))
        {
            return new WebDavRangeStreamResult
            {
                Status = WebDavRangeDownloadStatus.Failed,
                Error = "WebDAV download URL is invalid."
            };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        httpRequest.Headers.Range = new RangeHeaderValue(start, end);
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var rawCredential = $"{request.Username}:{request.Password}";
            var encodedCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredential));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredential);
        }

        var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var statusCode = (int)response.StatusCode;
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            response.Dispose();
            return new WebDavRangeStreamResult
            {
                Status = WebDavRangeDownloadStatus.RangeNotSupported,
                HttpStatusCode = statusCode,
                Error = "Range requests are not supported."
            };
        }

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            response.Dispose();
            return new WebDavRangeStreamResult
            {
                Status = WebDavRangeDownloadStatus.Unauthorized,
                HttpStatusCode = statusCode,
                Error = $"HTTP {statusCode}"
            };
        }

        if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            response.Dispose();
            return new WebDavRangeStreamResult
            {
                Status = WebDavRangeDownloadStatus.RangeNotSatisfiable,
                HttpStatusCode = statusCode,
                Error = $"HTTP {statusCode}"
            };
        }

        if (response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            response.Dispose();
            return new WebDavRangeStreamResult
            {
                Status = WebDavRangeDownloadStatus.Failed,
                HttpStatusCode = statusCode,
                Error = $"HTTP {statusCode}"
            };
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new WebDavRangeStreamResult
        {
            Status = WebDavRangeDownloadStatus.Success,
            HttpStatusCode = statusCode,
            ContentStream = stream,
            ContentLength = response.Content.Headers.ContentLength,
            Response = response
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
