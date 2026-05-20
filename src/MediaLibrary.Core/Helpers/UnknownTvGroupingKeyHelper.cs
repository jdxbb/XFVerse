using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Helpers;

internal static partial class UnknownTvGroupingKeyHelper
{
    public static bool TryBuildContext(
        MediaFile mediaFile,
        out UnknownTvGroupingContext context,
        out string skippedReason)
    {
        context = UnknownTvGroupingContext.Empty;
        skippedReason = string.Empty;

        if (mediaFile.SourceConnectionId <= 0)
        {
            skippedReason = "missing-source-connection";
            return false;
        }

        var directory = NormalizePath(MoviePlaceholderGroupingHelper.GetDirectParentPath(mediaFile.FilePath));
        if (string.IsNullOrWhiteSpace(directory))
        {
            skippedReason = "missing-parent-directory";
            return false;
        }

        context = BuildContext(
            mediaFile.SourceConnectionId,
            mediaFile.ScanPathId,
            directory,
            seasonRange: null);
        return true;
    }

    public static bool TryBuildContext(
        IReadOnlyCollection<MediaFile> mediaFiles,
        string parentPath,
        int startNumber,
        int endNumber,
        out UnknownTvGroupingContext context,
        out string skippedReason)
    {
        context = UnknownTvGroupingContext.Empty;
        skippedReason = string.Empty;

        var activeFiles = mediaFiles
            .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted)
            .ToArray();
        if (activeFiles.Length == 0)
        {
            skippedReason = "no-active-video-sources";
            return false;
        }

        var sourceConnectionIds = activeFiles
            .Select(x => x.SourceConnectionId)
            .Distinct()
            .ToArray();
        if (sourceConnectionIds.Length != 1 || sourceConnectionIds[0] <= 0)
        {
            skippedReason = "mixed-source-connections";
            return false;
        }

        var scanPathIds = activeFiles
            .Select(x => x.ScanPathId)
            .Distinct()
            .ToArray();
        if (scanPathIds.Length != 1)
        {
            skippedReason = "mixed-scan-paths";
            return false;
        }

        var normalizedParent = NormalizePath(parentPath);
        if (string.IsNullOrWhiteSpace(normalizedParent))
        {
            normalizedParent = NormalizePath(MoviePlaceholderGroupingHelper.GetDirectParentPath(activeFiles[0].FilePath));
        }

        if (string.IsNullOrWhiteSpace(normalizedParent))
        {
            skippedReason = "missing-parent-directory";
            return false;
        }

        var seasonRange = startNumber > 0 && endNumber >= startNumber
            ? $"{startNumber.ToString(CultureInfo.InvariantCulture)}-{endNumber.ToString(CultureInfo.InvariantCulture)}"
            : null;
        context = BuildContext(sourceConnectionIds[0], scanPathIds[0], normalizedParent, seasonRange);
        return true;
    }

    public static bool IsCompatibleSeries(
        TvSeries series,
        UnknownTvGroupingContext context,
        out string skippedReason)
    {
        skippedReason = string.Empty;
        if (series.TmdbSeriesId.HasValue)
        {
            skippedReason = "recognized-series";
            return false;
        }

        if (!TryGetUniqueSeriesGroupingKey(series, out var existingSeriesGroupingKey, out skippedReason))
        {
            return false;
        }

        if (!string.Equals(existingSeriesGroupingKey, context.SeriesGroupingKey, StringComparison.OrdinalIgnoreCase))
        {
            skippedReason = "strict-series-key-mismatch";
            return false;
        }

        return true;
    }

    public static bool IsCompatibleSeason(
        TvSeason season,
        UnknownTvGroupingContext context,
        IReadOnlyCollection<int> candidateEpisodeNumbers,
        out string skippedReason)
    {
        skippedReason = string.Empty;
        if (!IsUnknownSeason(season) || season.Series?.TmdbSeriesId.HasValue == true)
        {
            skippedReason = "not-compatible-unknown-season";
            return false;
        }

        if (season.Series is null
            || !TryGetUniqueSeriesGroupingKey(season.Series, out var seriesStrictGroupingKey, out skippedReason))
        {
            return false;
        }

        if (!string.Equals(seriesStrictGroupingKey, context.SeriesGroupingKey, StringComparison.OrdinalIgnoreCase))
        {
            skippedReason = "strict-series-key-mismatch";
            return false;
        }

        if (!TryGetUniqueSeasonGroupingKey(
                season,
                out var existingSeriesGroupingKey,
                out var existingSeasonGroupingKey,
                out skippedReason))
        {
            return false;
        }

        if (!string.Equals(existingSeriesGroupingKey, context.SeriesGroupingKey, StringComparison.OrdinalIgnoreCase))
        {
            skippedReason = "strict-series-key-mismatch";
            return false;
        }

        if (!string.Equals(existingSeasonGroupingKey, context.SeasonGroupingKey, StringComparison.OrdinalIgnoreCase))
        {
            skippedReason = "strict-season-key-mismatch";
            return false;
        }

        _ = candidateEpisodeNumbers;
        return true;
    }

    public static bool HasSpecialDirectoryToken(UnknownTvGroupingContext context)
    {
        if (ContainsSpecialDirectoryToken(context.SeasonDisplayTitle)
            || ContainsSpecialDirectoryToken(context.NormalizedSeasonTitle)
            || ContainsSpecialDirectoryToken(GetDirectoryName(context.SeasonDirectory)))
        {
            return true;
        }

        return !context.HasExplicitSeriesRoot
               && (ContainsSpecialDirectoryToken(context.SeriesDisplayTitle)
                   || ContainsSpecialDirectoryToken(context.NormalizedSeriesTitle)
                   || ContainsSpecialDirectoryToken(GetDirectoryName(context.SeriesRootDirectory)));
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    public static string GetDirectoryPath(string? path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex <= 0 ? string.Empty : normalized[..lastSeparatorIndex];
    }

    public static string HashKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())))
            .ToLowerInvariant()[..12];
    }

    public static bool ContainsSpecialEpisodeToken(string fileName)
    {
        var text = NormalizeTokenText(fileName);
        var tokens = SplitTokens(text);
        return text.Contains("ova", StringComparison.Ordinal)
               || text.Contains("oad", StringComparison.Ordinal)
               || tokens.Contains("sp", StringComparer.OrdinalIgnoreCase)
               || text.Contains("special", StringComparison.Ordinal)
               || text.Contains("\u5267\u573a\u7248", StringComparison.Ordinal)
               || text.Contains("\u5287\u5834\u7248", StringComparison.Ordinal)
               || text.Contains("\u7279\u522b\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u7279\u5225\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u603b\u96c6\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u7e3d\u96c6\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u91cd\u5236\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u91cd\u88fd\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u756a\u5916", StringComparison.Ordinal)
               || text.Contains("\u5916\u4f20", StringComparison.Ordinal)
               || text.Contains("\u5916\u50b3", StringComparison.Ordinal)
               || text.Contains("\u8bfe\u7a0b", StringComparison.Ordinal)
               || text.Contains("\u8ab2\u7a0b", StringComparison.Ordinal)
               || text.Contains("\u5408\u96c6", StringComparison.Ordinal)
               || text.Contains("the movie", StringComparison.Ordinal)
               || text.Contains("themovie", StringComparison.Ordinal)
               || tokens.Contains("movie", StringComparer.OrdinalIgnoreCase);
    }

    public static bool ContainsStructuralNonEpisodeToken(string fileName)
    {
        var tokens = SplitTokens(NormalizeTokenText(fileName));
        return tokens.Contains("part", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("pt", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("cd", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("disc", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("disk", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("sample", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("trailer", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("teaser", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("preview", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("extra", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("extras", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("bonus", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("featurette", StringComparer.OrdinalIgnoreCase);
    }

    public static string BuildEpisodeTitle(string fileName, int episodeNumber)
    {
        return string.IsNullOrWhiteSpace(fileName)
            ? $"Episode {episodeNumber.ToString(CultureInfo.InvariantCulture)}"
            : fileName.Trim();
    }

    private static bool TryGetUniqueSeriesGroupingKey(
        TvSeries series,
        out string seriesGroupingKey,
        out string skippedReason)
    {
        seriesGroupingKey = string.Empty;
        skippedReason = string.Empty;

        var keys = series.Seasons
            .Where(IsUnknownSeason)
            .SelectMany(x => x.Episodes)
            .SelectMany(x => x.MediaFiles)
            .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted)
            .Select(source => TryBuildContext(source, out var sourceContext, out _)
                ? sourceContext.SeriesGroupingKey
                : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            skippedReason = "no-existing-unknown-series-context";
            return false;
        }

        if (keys.Length > 1)
        {
            skippedReason = "ambiguous-existing-unknown-series-context";
            return false;
        }

        seriesGroupingKey = keys[0];
        return true;
    }

    private static bool TryGetUniqueSeasonGroupingKey(
        TvSeason season,
        out string seriesGroupingKey,
        out string seasonGroupingKey,
        out string skippedReason)
    {
        seriesGroupingKey = string.Empty;
        seasonGroupingKey = string.Empty;
        skippedReason = string.Empty;

        var contexts = season.Episodes
            .SelectMany(x => x.MediaFiles)
            .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted)
            .Select(source => TryBuildContext(source, out var sourceContext, out _)
                ? sourceContext
                : null)
            .Where(x => x is not null)
            .Cast<UnknownTvGroupingContext>()
            .ToArray();

        if (contexts.Length == 0)
        {
            skippedReason = "no-existing-unknown-season-context";
            return false;
        }

        var seriesKeys = contexts
            .Select(x => x.SeriesGroupingKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var seasonKeys = contexts
            .Select(x => x.SeasonGroupingKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (seriesKeys.Length != 1 || seasonKeys.Length != 1)
        {
            skippedReason = "ambiguous-existing-unknown-season-context";
            return false;
        }

        seriesGroupingKey = seriesKeys[0];
        seasonGroupingKey = seasonKeys[0];
        return true;
    }

    private static UnknownTvGroupingContext BuildContext(
        int sourceConnectionId,
        int? scanPathId,
        string mediaDirectory,
        string? seasonRange)
    {
        var directoryName = GetDirectoryName(mediaDirectory);
        var hasExplicitSeriesRoot = IsSeasonIdentityDirectory(directoryName);
        var seriesRootDirectory = hasExplicitSeriesRoot
            ? GetDirectoryPath(mediaDirectory)
            : mediaDirectory;
        if (string.IsNullOrWhiteSpace(seriesRootDirectory))
        {
            seriesRootDirectory = mediaDirectory;
            hasExplicitSeriesRoot = false;
        }

        var seriesFolderName = GetDirectoryName(seriesRootDirectory);
        var normalizedSeriesTitle = NormalizeTitle(seriesFolderName);
        if (string.IsNullOrWhiteSpace(normalizedSeriesTitle))
        {
            normalizedSeriesTitle = NormalizeTitle(directoryName);
        }

        var normalizedSeasonTitle = NormalizeTitle(directoryName);
        if (string.IsNullOrWhiteSpace(normalizedSeasonTitle))
        {
            normalizedSeasonTitle = NormalizeTitle(seasonRange ?? string.Empty);
        }

        var seriesDisplayTitle = string.IsNullOrWhiteSpace(seriesFolderName)
            ? directoryName
            : seriesFolderName;
        if (string.IsNullOrWhiteSpace(seriesDisplayTitle))
        {
            seriesDisplayTitle = normalizedSeriesTitle;
        }

        var seasonDisplayTitle = hasExplicitSeriesRoot && !string.IsNullOrWhiteSpace(directoryName)
            ? directoryName
            : seasonRange ?? directoryName;
        if (string.IsNullOrWhiteSpace(seasonDisplayTitle))
        {
            seasonDisplayTitle = normalizedSeasonTitle;
        }

        var sourceKey = sourceConnectionId.ToString(CultureInfo.InvariantCulture);
        var scanKey = scanPathId?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var seriesGroupingKey = string.Join(
            '|',
            sourceKey,
            scanKey,
            NormalizeKeyPath(seriesRootDirectory),
            normalizedSeriesTitle.ToLowerInvariant());
        var seasonGroupingKey = string.Join(
            '|',
            seriesGroupingKey,
            NormalizeKeyPath(mediaDirectory),
            normalizedSeasonTitle.ToLowerInvariant());

        return new UnknownTvGroupingContext(
            sourceConnectionId,
            scanPathId,
            mediaDirectory,
            seriesRootDirectory,
            mediaDirectory,
            normalizedSeriesTitle,
            normalizedSeasonTitle,
            string.IsNullOrWhiteSpace(seriesDisplayTitle) ? normalizedSeriesTitle : seriesDisplayTitle.Trim(),
            string.IsNullOrWhiteSpace(seasonDisplayTitle) ? normalizedSeasonTitle : seasonDisplayTitle.Trim(),
            seasonRange ?? string.Empty,
            hasExplicitSeriesRoot,
            seriesGroupingKey,
            seasonGroupingKey,
            HashKey(seriesRootDirectory),
            HashKey(seriesGroupingKey),
            HashKey(seasonGroupingKey));
    }

    private static bool IsSameSourceRoot(UnknownTvGroupingContext left, UnknownTvGroupingContext right)
    {
        return left.SourceConnectionId == right.SourceConnectionId
               && Nullable.Equals(left.ScanPathId, right.ScanPathId)
               && string.Equals(left.SeriesRootDirectory, right.SeriesRootDirectory, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.NormalizedSeriesTitle, right.NormalizedSeriesTitle, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnknownSeason(TvSeason season)
    {
        return !season.TmdbSeasonId.HasValue
               && season.IdentificationStatus == IdentificationStatus.Failed;
    }

    private static bool IsSeasonIdentityDirectory(string value)
    {
        return TvEpisodeFileNameParser.IsSeasonFolderName(value)
               || RangeFolderRegex().IsMatch(value);
    }

    private static bool IsRangeOnlyTitle(string value)
    {
        return RangeFolderRegex().IsMatch(value);
    }

    private static bool ContainsSpecialDirectoryToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = NormalizeDirectoryTokenText(value);
        var tokens = SplitTokens(text);
        return text.Contains("\u7279\u522b\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u7279\u5225\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u603b\u96c6\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u7e3d\u96c6\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u91cd\u5236\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u91cd\u88fd\u7bc7", StringComparison.Ordinal)
               || text.Contains("\u5267\u573a\u7248", StringComparison.Ordinal)
               || text.Contains("\u5287\u5834\u7248", StringComparison.Ordinal)
               || text.Contains("\u756a\u5916", StringComparison.Ordinal)
               || text.Contains("\u5916\u4f20", StringComparison.Ordinal)
               || text.Contains("\u5916\u50b3", StringComparison.Ordinal)
               || text.Contains("\u8bfe\u7a0b", StringComparison.Ordinal)
               || text.Contains("\u8ab2\u7a0b", StringComparison.Ordinal)
               || text.Contains("\u5408\u96c6", StringComparison.Ordinal)
               || text.Contains("the movie", StringComparison.Ordinal)
               || text.Contains("themovie", StringComparison.Ordinal)
               || tokens.Contains("sp", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("special", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("specials", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("ova", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("oad", StringComparer.OrdinalIgnoreCase)
               || tokens.Contains("movie", StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeTitle(string value)
    {
        var cleaned = TvEpisodeFileNameParser.CleanSeriesNameCandidate(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = value ?? string.Empty;
        }

        cleaned = cleaned.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? string.Empty : cleaned.ToLowerInvariant();
    }

    private static string GetDirectoryName(string path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex >= 0 ? normalized[(lastSeparatorIndex + 1)..].Trim() : normalized.Trim();
    }

    private static string NormalizeKeyPath(string value)
    {
        return NormalizePath(value).ToLowerInvariant();
    }

    private static string NormalizeTokenText(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName)
            .ToLowerInvariant()
            .Replace('.', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace('[', ' ')
            .Replace(']', ' ')
            .Replace('(', ' ')
            .Replace(')', ' ')
            .Replace('\uFF08', ' ')
            .Replace('\uFF09', ' ');
    }

    private static string NormalizeDirectoryTokenText(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace('.', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace('[', ' ')
            .Replace(']', ' ')
            .Replace('【', ' ')
            .Replace('】', ' ')
            .Replace('(', ' ')
            .Replace(')', ' ')
            .Replace('\uFF08', ' ')
            .Replace('\uFF09', ' ');
    }

    private static string[] SplitTokens(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    [GeneratedRegex(@"^\s*\d{1,4}\s*(?:\p{Pd}|_|~|\u5230|\u81f3)+\s*\d{1,4}\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeFolderRegex();
}

internal sealed record UnknownTvGroupingContext(
    int SourceConnectionId,
    int? ScanPathId,
    string MediaDirectory,
    string SeriesRootDirectory,
    string SeasonDirectory,
    string NormalizedSeriesTitle,
    string NormalizedSeasonTitle,
    string SeriesDisplayTitle,
    string SeasonDisplayTitle,
    string SeasonRange,
    bool HasExplicitSeriesRoot,
    string SeriesGroupingKey,
    string SeasonGroupingKey,
    string ParentDirectoryHash,
    string SeriesGroupingKeyHash,
    string SeasonGroupingKeyHash)
{
    public static UnknownTvGroupingContext Empty { get; } = new(
        0,
        null,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        false,
        string.Empty,
        string.Empty,
        "none",
        "none",
        "none");
}
