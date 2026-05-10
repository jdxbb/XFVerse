using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Helpers;

public static class MediaFileRules
{
    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".avi",
        ".mov"
    };

    public static readonly IReadOnlySet<string> SubtitleExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".srt",
        ".ass",
        ".ssa"
    };

    public static MediaType GetMediaType(string fileNameOrPath)
    {
        var extension = Path.GetExtension(fileNameOrPath);
        if (VideoExtensions.Contains(extension))
        {
            return MediaType.Video;
        }

        if (SubtitleExtensions.Contains(extension))
        {
            return MediaType.Subtitle;
        }

        return MediaType.Other;
    }
}
