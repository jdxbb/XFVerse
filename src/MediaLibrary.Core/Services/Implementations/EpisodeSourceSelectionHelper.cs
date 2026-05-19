using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Services.Implementations;

internal static class EpisodeSourceSelectionHelper
{
    public static int? ResolveDefaultMediaFileId<TSource>(
        IReadOnlyList<TSource> sources,
        int? preferredMediaFileId,
        Func<TSource, int> mediaFileIdSelector,
        Func<TSource, ProtocolType> protocolTypeSelector,
        Func<TSource, string> filePathSelector,
        Func<TSource, string> stableNameSelector,
        Func<TSource, DateTime?> lastPlayedAtSelector,
        Func<TSource, int> lastPlayPositionSecondsSelector)
        where TSource : class
    {
        if (sources.Count == 0)
        {
            return null;
        }

        var preferredSource = preferredMediaFileId.HasValue
            ? sources.FirstOrDefault(source => mediaFileIdSelector(source) == preferredMediaFileId.Value)
            : default;
        if (preferredMediaFileId.HasValue && preferredSource is not null)
        {
            return preferredMediaFileId.Value;
        }

        var localSource = StableOrder(sources, stableNameSelector, mediaFileIdSelector)
            .FirstOrDefault(source => protocolTypeSelector(source) == ProtocolType.Local
                                      && IsExistingLocalFile(filePathSelector(source)));
        if (localSource is not null)
        {
            return mediaFileIdSelector(localSource);
        }

        var historySource = StableOrder(sources, stableNameSelector, mediaFileIdSelector)
            .Where(source => IsAvailableForAutomaticSelection(
                                protocolTypeSelector(source),
                                filePathSelector(source))
                             && (lastPlayedAtSelector(source).HasValue
                                 || lastPlayPositionSecondsSelector(source) > 0))
            .OrderByDescending(source => lastPlayedAtSelector(source).HasValue)
            .ThenByDescending(source => lastPlayedAtSelector(source))
            .ThenByDescending(source => lastPlayPositionSecondsSelector(source) > 0)
            .ThenByDescending(source => lastPlayPositionSecondsSelector(source))
            .ThenBy(source => StableName(stableNameSelector(source)), StringComparer.OrdinalIgnoreCase)
            .ThenBy(mediaFileIdSelector)
            .FirstOrDefault();
        if (historySource is not null)
        {
            return mediaFileIdSelector(historySource);
        }

        var remoteSource = StableOrder(sources, stableNameSelector, mediaFileIdSelector)
            .FirstOrDefault(source => protocolTypeSelector(source) != ProtocolType.Local);
        if (remoteSource is not null)
        {
            return mediaFileIdSelector(remoteSource);
        }

        return mediaFileIdSelector(StableOrder(sources, stableNameSelector, mediaFileIdSelector).First());
    }

    public static bool IsAvailableForAutomaticSelection(ProtocolType protocolType, string filePath)
    {
        return protocolType != ProtocolType.Local || IsExistingLocalFile(filePath);
    }

    private static IOrderedEnumerable<TSource> StableOrder<TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, string> stableNameSelector,
        Func<TSource, int> mediaFileIdSelector)
    {
        return sources
            .OrderBy(source => StableName(stableNameSelector(source)), StringComparer.OrdinalIgnoreCase)
            .ThenBy(mediaFileIdSelector);
    }

    private static string StableName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool IsExistingLocalFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            return File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }
}
