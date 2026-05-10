namespace MediaLibrary.App.Playback.Mpv.Core;

public sealed record MpvTrackInfo(
    int Id,
    string Kind,
    string? Title,
    string? Language,
    string? Codec,
    bool IsExternal,
    bool IsSelected,
    bool IsDefault,
    bool IsForced,
    bool IsDependent,
    bool IsVisualImpaired,
    bool IsHearingImpaired);
