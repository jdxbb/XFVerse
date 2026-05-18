using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace MediaLibrary.Core.Helpers;

public static partial class MoviePlaceholderGroupingHelper
{
    public static MoviePlaceholderGroupingResult BuildRanges(IEnumerable<MoviePlaceholderGroupingInput> placeholders)
    {
        var placeholderList = placeholders.ToList();
        if (placeholderList.Count == 0)
        {
            return new MoviePlaceholderGroupingResult([], 0, 0, 0, EmptySkippedReasons);
        }

        var parsedCandidates = new List<MoviePlaceholderGroupingParsedCandidate>();
        var skippedReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var placeholder in placeholderList)
        {
            if (TryParseEpisodePattern(placeholder.FileName, out var pattern, out var skippedReason))
            {
                parsedCandidates.Add(new MoviePlaceholderGroupingParsedCandidate(placeholder, pattern));
            }
            else
            {
                AddCount(skippedReasons, skippedReason);
            }
        }

        var ranges = new List<MoviePlaceholderGroupingRange>();
        var skippedRunReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var consumedMediaFileIds = new HashSet<int>();
        foreach (var parentGroup in parsedCandidates.GroupBy(x => x.Placeholder.ParentPath))
        {
            var parentItems = parentGroup.ToList();
            if (parentItems.Select(x => x.Pattern.PatternKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 1)
            {
                continue;
            }

            if (parentItems.GroupBy(x => x.Pattern.Number).Any(x => x.Count() > 1))
            {
                AddCount(skippedRunReasons, "mixed-duplicate-episode-number");
                continue;
            }

            foreach (var run in BuildStrictContinuousRuns(parentItems.OrderBy(x => x.Pattern.Number).ToList()))
            {
                if (run.Count < 3)
                {
                    AddCount(skippedRunReasons, "mixed-run-too-short");
                    continue;
                }

                if (run.Select(x => x.Pattern.PatternKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 1)
                {
                    continue;
                }

                var first = run[0];
                var last = run[^1];
                foreach (var item in run)
                {
                    consumedMediaFileIds.Add(item.Placeholder.MediaFileId);
                }

                ranges.Add(
                    new MoviePlaceholderGroupingRange(
                        first.Placeholder.ParentPath,
                        "mixed-episode-sequence",
                        "mixed-pattern",
                        first.Pattern.Number,
                        last.Pattern.Number,
                        run.Select(x => x.Placeholder).ToArray()));
            }
        }

        foreach (var group in parsedCandidates
                     .Where(x => !consumedMediaFileIds.Contains(x.Placeholder.MediaFileId))
                     .GroupBy(x => (x.Placeholder.ParentPath, x.Pattern.PatternKey)))
        {
            if (group.GroupBy(x => x.Pattern.Number).Any(x => x.Count() > 1))
            {
                AddCount(skippedRunReasons, "duplicate-episode-number");
                continue;
            }

            var ordered = group.OrderBy(x => x.Pattern.Number).ToList();
            foreach (var run in BuildStrictContinuousRuns(ordered))
            {
                if (run.Count < 3)
                {
                    AddCount(skippedRunReasons, "run-too-short");
                    continue;
                }

                var first = run[0];
                var last = run[^1];
                ranges.Add(
                    new MoviePlaceholderGroupingRange(
                        first.Placeholder.ParentPath,
                        first.Pattern.PatternKey,
                        first.Pattern.Pattern,
                        first.Pattern.Number,
                        last.Pattern.Number,
                        run.Select(x => x.Placeholder).ToArray()));
            }
        }

        var combinedSkippedReasons = skippedReasons
            .Concat(skippedRunReasons)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Value), StringComparer.OrdinalIgnoreCase);

        return new MoviePlaceholderGroupingResult(
            ranges,
            placeholderList.Count,
            parsedCandidates.Count,
            ranges.Sum(x => x.FileCount),
            combinedSkippedReasons);
    }

    public static string GetDirectParentPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        var normalized = filePath.Replace('\\', '/').TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index > 0 ? normalized[..index] : string.Empty;
    }

    public static string GetParentFolderDisplay(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return "未识别剧集候选";
        }

        var normalized = parentPath.Replace('\\', '/').TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        var display = index >= 0 ? normalized[(index + 1)..] : normalized;
        return string.IsNullOrWhiteSpace(display) ? "未识别剧集候选" : display.Trim();
    }

    private static IReadOnlyList<List<MoviePlaceholderGroupingParsedCandidate>> BuildStrictContinuousRuns(
        IReadOnlyList<MoviePlaceholderGroupingParsedCandidate> ordered)
    {
        var runs = new List<List<MoviePlaceholderGroupingParsedCandidate>>();
        var current = new List<MoviePlaceholderGroupingParsedCandidate>();
        foreach (var item in ordered)
        {
            if (current.Count == 0 || item.Pattern.Number == current[^1].Pattern.Number + 1)
            {
                current.Add(item);
                continue;
            }

            runs.Add(current);
            current = [item];
        }

        if (current.Count > 0)
        {
            runs.Add(current);
        }

        return runs;
    }

    private static bool TryParseEpisodePattern(
        string fileName,
        out MoviePlaceholderEpisodePattern pattern,
        out string skippedReason)
    {
        pattern = new MoviePlaceholderEpisodePattern(string.Empty, string.Empty, 0);
        skippedReason = string.Empty;

        var name = WebUtility.HtmlDecode(Path.GetFileNameWithoutExtension(fileName)).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            skippedReason = "empty-name";
            return false;
        }

        if (ExcludedTokenRegex().IsMatch(name))
        {
            skippedReason = "excluded-non-episode-token";
            return false;
        }

        var bareNumberMatch = BareNumberRegex().Match(name);
        if (bareNumberMatch.Success && TryReadPositiveEpisodeNumber(bareNumberMatch.Groups["episode"].Value, out var bareNumber))
        {
            pattern = new MoviePlaceholderEpisodePattern("bare-number", "bare-number", bareNumber);
            return true;
        }

        var bareNumberQualityMatch = BareNumberQualityTailRegex().Match(name);
        if (bareNumberQualityMatch.Success
            && TryReadPositiveEpisodeNumber(bareNumberQualityMatch.Groups["episode"].Value, out var bareNumberQuality))
        {
            pattern = new MoviePlaceholderEpisodePattern("bare-number-quality", "bare-number-quality", bareNumberQuality);
            return true;
        }

        var markerMatch = MarkerEpisodeRegex().Match(name);
        if (markerMatch.Success && TryReadPositiveEpisodeNumber(markerMatch.Groups["episode"].Value, out var markerNumber))
        {
            pattern = new MoviePlaceholderEpisodePattern("episode-marker", "episode-marker", markerNumber);
            return true;
        }

        var chineseMatch = ChineseEpisodeRegex().Match(name);
        if (chineseMatch.Success && TryReadPositiveEpisodeNumber(chineseMatch.Groups["episode"].Value, out var chineseNumber))
        {
            pattern = new MoviePlaceholderEpisodePattern("chinese-episode-marker", "chinese-episode-marker", chineseNumber);
            return true;
        }

        if (TryParseBracketedEpisodePattern(name, out pattern))
        {
            return true;
        }

        var titleNumberName = WhitespaceRegex()
            .Replace(
                SeparatorRegex().Replace(
                    BracketedContentRegex().Replace(name, " "),
                    " "),
                " ")
            .Trim();
        var titleNumberMatch = TitleNumberRegex().Match(titleNumberName);
        if (titleNumberMatch.Success
            && TryReadPositiveEpisodeNumber(titleNumberMatch.Groups["episode"].Value, out var titleNumber)
            && TryNormalizeTitlePrefix(titleNumberMatch.Groups["prefix"].Value, out var prefixKey))
        {
            pattern = new MoviePlaceholderEpisodePattern($"title-number:{prefixKey}", "title-number", titleNumber);
            return true;
        }

        skippedReason = "no-supported-episode-number";
        return false;
    }

    private static bool TryParseBracketedEpisodePattern(string name, out MoviePlaceholderEpisodePattern pattern)
    {
        pattern = new MoviePlaceholderEpisodePattern(string.Empty, string.Empty, 0);
        var bracketValues = BracketedContentRegex()
            .Matches(name)
            .Select(match => TrimBracketContent(match.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (bracketValues.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < bracketValues.Length; index++)
        {
            if (!TryParseBracketedEpisodeSegment(bracketValues[index], out var number))
            {
                continue;
            }

            var titleKey = FindNearestBracketTitleKey(bracketValues, index);
            var patternKey = string.IsNullOrWhiteSpace(titleKey)
                ? "bracket-episode-segment"
                : $"bracket-title-number:{titleKey}";
            pattern = new MoviePlaceholderEpisodePattern(patternKey, "bracket-episode-segment", number);
            return true;
        }

        return false;
    }

    private static string TrimBracketContent(string value)
    {
        return value.Trim().Trim('[', ']', '(', ')', '{', '}').Trim();
    }

    private static bool TryParseBracketedEpisodeSegment(string value, out int number)
    {
        number = 0;
        var markerMatch = BracketedEpisodeMarkerOnlyRegex().Match(value);
        if (markerMatch.Success)
        {
            return TryReadPositiveEpisodeNumber(markerMatch.Groups["episode"].Value, out number);
        }

        var segmentMatch = BracketedEpisodeSegmentRegex().Match(value);
        return segmentMatch.Success
               && TryReadPositiveEpisodeNumber(segmentMatch.Groups["episode"].Value, out number);
    }

    private static string FindNearestBracketTitleKey(IReadOnlyList<string> bracketValues, int episodeSegmentIndex)
    {
        for (var index = episodeSegmentIndex - 1; index >= 0; index--)
        {
            var candidate = bracketValues[index];
            if (BracketTitleNoiseRegex().IsMatch(candidate))
            {
                continue;
            }

            if (TryNormalizeTitlePrefix(candidate, out var titleKey))
            {
                return titleKey;
            }
        }

        return string.Empty;
    }

    private static bool TryNormalizeTitlePrefix(string value, out string prefixKey)
    {
        prefixKey = string.Empty;
        var normalized = NormalizeQueryToken(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var meaningfulTokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !LowInformationTokenRegex().IsMatch(token))
            .ToArray();
        if (meaningfulTokens.Length == 0)
        {
            return false;
        }

        var meaningfulText = string.Join(' ', meaningfulTokens);
        if (!meaningfulText.Any(IsCjk) && meaningfulText.Count(char.IsLetter) < 3)
        {
            return false;
        }

        prefixKey = meaningfulText.ToLowerInvariant();
        return true;
    }

    private static string NormalizeQueryToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = BracketedContentRegex().Replace(value, " ");
        normalized = SeparatorRegex().Replace(normalized, " ");
        return WhitespaceRegex().Replace(normalized, " ").Trim();
    }

    private static bool TryReadPositiveEpisodeNumber(string value, out int number)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number > 0;
    }

    private static bool IsCjk(char ch)
    {
        return ch >= 0x4E00 && ch <= 0x9FFF;
    }

    private static void AddCount(IDictionary<string, int> counts, string key)
    {
        key = string.IsNullOrWhiteSpace(key) ? "unknown" : key;
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }

    private static IReadOnlyDictionary<string, int> EmptySkippedReasons { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedContentRegex();

    [GeneratedRegex(@"[._\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,4})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,4})(?:[\s._-]+(?:4K|8K|1080P|2160P|720P|480P|UHD|FHD|HDR|HDR10|DV|WEB[-\s]?DL|WEBRIP|BDRIP|BLURAY|HEVC|H\.?265|H\.?264|X264|X265|AAC|AC3|EAC3|DDP\d?|DTS|TRUEHD|ATMOS|FLAC|REMUX|10BIT|8BIT))+\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberQualityTailRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:E|EP)(?<episode>\d{1,4})(?:$|[\s._\-\]\)])|\bEpisode\s*(?<episode>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkerEpisodeRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<episode>\d{1,4})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseEpisodeRegex();

    [GeneratedRegex(@"^\s*(?:E|EP|Episode)\s*(?<episode>\d{1,4})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketedEpisodeMarkerOnlyRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,4})\s*[-–—_:：.]\s*\S.+$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedEpisodeSegmentRegex();

    [GeneratedRegex(@"^(?<prefix>.*?[\p{L}\u4e00-\u9fff].*?)[\s._-]*(?<episode>\d{1,4})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TitleNumberRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:cd|disc|disk|part|sample|trailer|teaser|preview|extras?|bonus|featurette)\s*\d*(?:$|[\s._\-\]\)])|\u82b1\u7d6e|\u9884\u544a|\u7279\u5178|\u5e55\u540e|\u8bbf\u8c08|\u6837\u7247|\u7247\u6bb5|(?:^|[\s._\-\[\(])[\u4e0a\u4e0b](?:$|[\s._\-\]\)])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExcludedTokenRegex();

    [GeneratedRegex(@"^(?:\d{1,4}|19\d{2}|20\d{2}|part|pt|disc|disk|cd|sample|trailer|teaser|preview|extras?|bonus|4k|8k|1080p|2160p|720p|480p|uhd|fhd|sd|hq|hdr|hdr10|dv|x264|x265|h264|h265|hevc|av1|bluray|blu|ray|brrip|webrip|webdl|web|dl|hdrip|dvdrip|bdrip|hdtv|remux|aac|ac3|eac3|dts|truehd|atmos|ddp\d?|flac|lpcm|pcm|ma|hd|10bit|8bit|proper|repack|extended|limited|multi|subs?|subbed|dubbed|dual|audio|japanese|english|chinese|mandarin|cantonese|korean|amzn|nf|dsnp|hmax|itunes|group|team)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LowInformationTokenRegex();

    [GeneratedRegex(@"\b(?:1080p|2160p|720p|480p|4k|8k|uhd|fhd|hdr|hdr10|dv|x264|x265|h264|h265|hevc|av1|bluray|bdrip|webrip|webdl|web|dl|hdtv|remux|aac|ac3|eac3|dts|truehd|atmos|flac|lpcm|10bit|8bit|sub|subs|subbed|dubbed|group|team)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketTitleNoiseRegex();
}

public sealed record MoviePlaceholderGroupingInput(
    int MediaFileId,
    string FileName,
    string FilePath,
    string ParentPath,
    string CandidateTitle,
    string PlaceholderReason);

public sealed record MoviePlaceholderGroupingRange(
    string ParentPath,
    string PatternKey,
    string Pattern,
    int StartNumber,
    int EndNumber,
    IReadOnlyList<MoviePlaceholderGroupingInput> Items)
{
    public int FileCount => Items.Count;

    public IReadOnlyList<int> MediaFileIds => Items.Select(x => x.MediaFileId).Distinct().ToArray();

    public IReadOnlyList<string> PlaceholderReasons =>
        Items.Select(x => x.PlaceholderReason).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> SampleFileNames => Items.Select(x => x.FileName).Take(5).ToArray();
}

public sealed record MoviePlaceholderGroupingResult(
    IReadOnlyList<MoviePlaceholderGroupingRange> Ranges,
    int CandidateFiles,
    int ParsedEpisodeLikeFiles,
    int GroupedMoviePlaceholdersCount,
    IReadOnlyDictionary<string, int> SkippedReasons);

internal sealed record MoviePlaceholderEpisodePattern(string PatternKey, string Pattern, int Number);

internal sealed record MoviePlaceholderGroupingParsedCandidate(
    MoviePlaceholderGroupingInput Placeholder,
    MoviePlaceholderEpisodePattern Pattern);
