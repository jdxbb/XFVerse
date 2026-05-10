namespace MediaLibrary.Core.Models.ReadModels;

public sealed class WebDavDownloadRequest
{
    public required string DownloadUrl { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public required string DestinationPath { get; init; }

    public long? ExpectedBytes { get; init; }
}
