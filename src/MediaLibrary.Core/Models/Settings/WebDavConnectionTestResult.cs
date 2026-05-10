namespace MediaLibrary.Core.Models.Settings;

public sealed class WebDavConnectionTestResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public int? StatusCode { get; init; }
}
