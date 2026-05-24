using System.Net;

namespace MediaLibrary.Core.Services.Implementations;

internal sealed class AiRequestException : HttpRequestException
{
    public AiRequestException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        TimeSpan? retryAfter)
        : base($"AI request failed with HTTP {(int)statusCode} {reasonPhrase}".Trim(), null, statusCode)
    {
        AiStatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode AiStatusCode { get; }

    public TimeSpan? RetryAfter { get; }
}
