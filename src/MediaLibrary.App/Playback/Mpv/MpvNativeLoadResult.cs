namespace MediaLibrary.App.Playback.Mpv;

public sealed class MpvNativeLoadResult
{
    public bool Succeeded { get; init; }

    public string NativeDirectory { get; init; } = string.Empty;

    public string LibraryPath { get; init; } = string.Empty;

    public string? Error { get; init; }

    public string? ErrorType { get; init; }
}
