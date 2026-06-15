using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MediaLibrary.Core.Helpers;

public static partial class AiFailureMessageFormatter
{
    public static string Build(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (TryFindStatusCode(exception, out var statusCode))
        {
            return BuildForStatusCode(statusCode);
        }

        if (exception is TimeoutException or TaskCanceledException)
        {
            return "AI 服务响应超时，请稍后重试。";
        }

        if (exception is JsonException)
        {
            return "AI 返回内容格式异常，请重试。";
        }

        return Build(exception.Message);
    }

    public static string Build(string? message)
    {
        var value = (message ?? string.Empty).Trim();
        if (TryReadStatusCode(value, out var statusCode))
        {
            return BuildForStatusCode(statusCode);
        }

        if (value.Contains("额度不足", StringComparison.OrdinalIgnoreCase)
            || value.Contains("计费状态", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Payment Required", StringComparison.OrdinalIgnoreCase))
        {
            return BuildForStatusCode(HttpStatusCode.PaymentRequired);
        }

        if (value.Contains("请求过于频繁", StringComparison.OrdinalIgnoreCase)
            || value.Contains("额度已达上限", StringComparison.OrdinalIgnoreCase)
            || value.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return BuildForStatusCode(HttpStatusCode.TooManyRequests);
        }

        if (value.Contains("认证失败", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return BuildForStatusCode(HttpStatusCode.Unauthorized);
        }

        if (value.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return BuildForStatusCode(HttpStatusCode.Forbidden);
        }

        if (value.Contains("超时", StringComparison.OrdinalIgnoreCase)
            || value.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || value.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "AI 服务响应超时，请稍后重试。";
        }

        if (value.Contains("未返回可解析", StringComparison.OrdinalIgnoreCase)
            || value.Contains("does not contain a JSON object", StringComparison.OrdinalIgnoreCase)
            || value.Contains("response was empty", StringComparison.OrdinalIgnoreCase)
            || value.Contains("could not be parsed", StringComparison.OrdinalIgnoreCase))
        {
            return "AI 返回内容格式异常，请重试。";
        }

        return "AI 请求失败，请稍后重试。";
    }

    private static bool TryFindStatusCode(Exception exception, out HttpStatusCode statusCode)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: { } currentStatusCode })
            {
                statusCode = currentStatusCode;
                return true;
            }
        }

        statusCode = default;
        return false;
    }

    private static bool TryReadStatusCode(string value, out HttpStatusCode statusCode)
    {
        var match = HttpStatusCodeRegex().Match(value);
        if (match.Success
            && int.TryParse(match.Groups[1].Value, out var numericStatusCode)
            && numericStatusCode is >= 400 and <= 599)
        {
            statusCode = (HttpStatusCode)numericStatusCode;
            return true;
        }

        statusCode = default;
        return false;
    }

    private static string BuildForStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "AI 请求或模型配置不受服务端支持，请检查 AI 模型配置后重试。",
            HttpStatusCode.Unauthorized => "AI 服务认证失败，请检查 API Key 后重试。",
            HttpStatusCode.PaymentRequired => "AI 服务额度不足或计费状态异常，请检查服务商账户后重试。",
            HttpStatusCode.Forbidden => "AI 服务拒绝访问，请检查 API 或模型权限后重试。",
            HttpStatusCode.NotFound => "AI 服务地址或模型不可用，请检查 AI 接口与模型配置。",
            HttpStatusCode.RequestTimeout => "AI 服务响应超时，请稍后重试。",
            HttpStatusCode.TooManyRequests => "AI 服务请求过于频繁或额度已达上限，请稍后重试。",
            HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout => $"AI 服务暂时不可用（HTTP {(int)statusCode}），请稍后重试。",
            _ => $"AI 服务请求失败（HTTP {(int)statusCode}），请稍后重试。"
        };
    }

    [GeneratedRegex(@"(?<!\d)(?:HTTP\s*)?([45]\d{2})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpStatusCodeRegex();
}
