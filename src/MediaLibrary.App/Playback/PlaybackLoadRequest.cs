using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.App.Playback;

public sealed class PlaybackLoadRequest
{
    public int MediaFileId { get; init; }

    public int SourceConnectionId { get; init; }

    public string PlaybackUrl { get; init; } = string.Empty;

    public ProtocolType ProtocolType { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public bool IsLocalFile { get; init; }

    public int StartPositionSeconds { get; init; }

    public long FileSize { get; init; }

    public DateTime? LastModifiedAt { get; init; }

    public string? VideoCodec { get; init; }

    public int? ResolutionWidth { get; init; }

    public int? ResolutionHeight { get; init; }
}
