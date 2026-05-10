using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WebDavService : IWebDavService
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<WebDavConnectionTestResult> TestConnectionAsync(
        WebDavConnectionModel connectionModel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionModel.BaseUrl))
        {
            return new WebDavConnectionTestResult
            {
                IsSuccess = false,
                Message = "请先填写 BaseUrl。"
            };
        }

        if (!Uri.TryCreate(connectionModel.BaseUrl.Trim(), UriKind.Absolute, out var targetUri))
        {
            return new WebDavConnectionTestResult
            {
                IsSuccess = false,
                Message = "BaseUrl 不是合法的绝对地址。"
            };
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Options, targetUri, connectionModel);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new WebDavConnectionTestResult
                {
                    IsSuccess = true,
                    StatusCode = (int)response.StatusCode,
                    Message = $"连接测试成功 (HTTP {(int)response.StatusCode})。"
                };
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new WebDavConnectionTestResult
                {
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    Message = $"服务器可达，但认证失败 (HTTP {(int)response.StatusCode})。"
                };
            }

            return new WebDavConnectionTestResult
            {
                IsSuccess = false,
                StatusCode = (int)response.StatusCode,
                Message = $"连接失败 (HTTP {(int)response.StatusCode})。"
            };
        }
        catch (TaskCanceledException)
        {
            return new WebDavConnectionTestResult
            {
                IsSuccess = false,
                Message = "连接测试超时。"
            };
        }
        catch (Exception exception)
        {
            return new WebDavConnectionTestResult
            {
                IsSuccess = false,
                Message = $"连接测试失败：{exception.Message}"
            };
        }
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListDirectoryAsync(
        WebDavConnectionModel connectionModel,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        return await ListDirectoryAsync(connectionModel, directoryPath, null, cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListDirectoryAsync(
        WebDavConnectionModel connectionModel,
        string directoryPath,
        string? directoryUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionModel.BaseUrl))
        {
            throw new InvalidOperationException("WebDAV 连接未配置 BaseUrl。");
        }

        var baseUri = WebDavPathHelper.CreateBaseUri(connectionModel.BaseUrl);
        var normalizedPath = WebDavPathHelper.NormalizeVirtualPath(directoryPath);
        var requestUri = !string.IsNullOrWhiteSpace(directoryUri) && Uri.TryCreate(directoryUri, UriKind.Absolute, out var explicitUri)
            ? explicitUri
            : WebDavPathHelper.BuildDirectoryUri(baseUri, normalizedPath);

        using var request = CreateRequest(PropFindMethod, requestUri, connectionModel);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(
            """
            <?xml version="1.0" encoding="utf-8" ?>
            <d:propfind xmlns:d="DAV:">
              <d:prop>
                <d:displayname />
                <d:getcontentlength />
                <d:getlastmodified />
                <d:resourcetype />
              </d:prop>
            </d:propfind>
            """,
            Encoding.UTF8,
            "application/xml");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"WebDAV 目录读取失败 (HTTP {(int)response.StatusCode})。");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = XDocument.Load(stream);
        XNamespace dav = "DAV:";

        var responseNodes = document
            .Descendants(dav + "response")
            .ToList();

        var responseRootUri = GetResponseRootUri(requestUri, responseNodes, dav);

        var entries = responseNodes
            .Select(responseNode => ParseResponseEntry(responseNode, dav, requestUri, responseRootUri, normalizedPath))
            .Where(entry => entry is not null)
            .Cast<RemoteEntry>()
            .Where(entry => !string.Equals(
                WebDavPathHelper.NormalizeVirtualPath(entry.Path),
                normalizedPath,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return entries;
    }

    private static RemoteEntry? ParseResponseEntry(
        XElement responseNode,
        XNamespace dav,
        Uri requestUri,
        Uri? responseRootUri,
        string requestDirectoryPath)
    {
        var href = responseNode.Element(dav + "href")?.Value;
        if (!TryMapResponseHref(
                requestUri,
                responseRootUri,
                requestDirectoryPath,
                href ?? string.Empty,
                out var virtualPath,
                out var entryUri))
        {
            return null;
        }

        var prop = responseNode
            .Descendants(dav + "prop")
            .FirstOrDefault();

        var isDirectory = prop?.Element(dav + "resourcetype")?.Element(dav + "collection") is not null;
        var name = prop?.Element(dav + "displayname")?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = WebDavPathHelper.GetFileName(virtualPath);
        }

        long? contentLength = null;
        if (long.TryParse(prop?.Element(dav + "getcontentlength")?.Value, out var parsedLength))
        {
            contentLength = parsedLength;
        }

        DateTime? lastModified = null;
        var lastModifiedRaw = prop?.Element(dav + "getlastmodified")?.Value;
        if (DateTime.TryParse(lastModifiedRaw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedDate))
        {
            lastModified = parsedDate;
        }

        return new RemoteEntry
        {
            Name = name ?? string.Empty,
            Path = WebDavPathHelper.NormalizeVirtualPath(virtualPath),
            RemoteUri = entryUri.ToString(),
            IsDirectory = isDirectory,
            ContentLength = contentLength,
            LastModifiedAt = lastModified
        };
    }

    private static Uri? GetResponseRootUri(Uri requestUri, IReadOnlyList<XElement> responseNodes, XNamespace dav)
    {
        var rootHref = responseNodes
            .Select(node => node.Element(dav + "href")?.Value)
            .FirstOrDefault(href => !string.IsNullOrWhiteSpace(href));

        return string.IsNullOrWhiteSpace(rootHref) || !Uri.TryCreate(requestUri, rootHref, out var rootUri)
            ? null
            : rootUri;
    }

    private static bool TryMapResponseHref(
        Uri requestUri,
        Uri? responseRootUri,
        string requestDirectoryPath,
        string href,
        out string virtualPath,
        out Uri entryUri)
    {
        entryUri = requestUri;
        if (WebDavPathHelper.TryGetVirtualPath(requestUri, requestDirectoryPath, href, out virtualPath))
        {
            if (Uri.TryCreate(requestUri, href, out var mappedEntryUri))
            {
                entryUri = mappedEntryUri;
            }

            return true;
        }

        if (responseRootUri is null || !Uri.TryCreate(requestUri, href, out var fallbackEntryUri))
        {
            return false;
        }

        entryUri = fallbackEntryUri;

        if (responseRootUri.IsBaseOf(entryUri))
        {
            var relative = Uri.UnescapeDataString(responseRootUri.MakeRelativeUri(entryUri).ToString());
            virtualPath = WebDavPathHelper.CombineVirtualPath(requestDirectoryPath, relative);
            return true;
        }

        var responseRootPath = responseRootUri.AbsolutePath.TrimEnd('/');
        var entryPath = Uri.UnescapeDataString(entryUri.AbsolutePath);
        if (!entryPath.StartsWith(responseRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = entryPath[responseRootPath.Length..].TrimStart('/');
        virtualPath = WebDavPathHelper.CombineVirtualPath(requestDirectoryPath, relativePath);
        return true;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri requestUri, WebDavConnectionModel connectionModel)
    {
        var request = new HttpRequestMessage(method, requestUri);

        if (!string.IsNullOrWhiteSpace(connectionModel.Username))
        {
            var rawCredential = $"{connectionModel.Username}:{connectionModel.Password}";
            var encodedCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredential));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredential);
        }

        return request;
    }
}
