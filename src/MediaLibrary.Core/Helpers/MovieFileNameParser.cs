using System.Text;
using System.Text.RegularExpressions;
using System.Net;

namespace MediaLibrary.Core.Helpers;

public static partial class MovieFileNameParser
{
    private static readonly string[] NoiseTokens =
    [
        "1080p",
        "2160p",
        "720p",
        "480p",
        "hq",
        "4k",
        "hdr",
        "hdr10",
        "x264",
        "x265",
        "h264",
        "h265",
        "hevc",
        "av1",
        "bluray",
        "brrip",
        "webrip",
        "web-dl",
        "webdl",
        "hdrip",
        "dvdrip",
        "bdrip",
        "remux",
        "aac",
        "aac2",
        "aac5",
        "dts",
        "truehd",
        "atmos",
        "ddp",
        "ddp5",
        "10bit",
        "8bit",
        "proper",
        "repack",
        "extended",
        "limited",
        "multi",
        "subs",
        "sub",
        "dubbed",
        "dual",
        "audio",
        "国语",
        "国粤",
        "中英字幕",
        "中字",
        "内封",
        "内嵌",
        "简中",
        "繁中"
    ];

    public static ParsedMovieName Parse(string fileName)
    {
        var removedNoiseCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName).Trim();
        var nameWithoutExtension = WebUtility.HtmlDecode(rawNameWithoutExtension).Trim();
        if (!string.Equals(rawNameWithoutExtension, nameWithoutExtension, StringComparison.Ordinal))
        {
            removedNoiseCategories.Add("html-entity");
        }

        var yearMatch = FindLikelyReleaseYearMatch(nameWithoutExtension);
        var year = ExtractYear(yearMatch);

        var normalized = nameWithoutExtension;
        if (yearMatch.Success && HasLikelyTitleBeforeYear(nameWithoutExtension, yearMatch))
        {
            normalized = nameWithoutExtension[..yearMatch.Index];
            removedNoiseCategories.Add("release-year-tail");
        }

        normalized = BracketedContentRegex().Replace(normalized, " ");
        normalized = CleanupSeparatorsRegex().Replace(normalized, " ");
        normalized = AudioCodecPhraseRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("audio-codec");
            return " ";
        });
        normalized = AudioChannelLayoutRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("channel-layout");
            return " ";
        });
        normalized = VideoCodecPhraseRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("video-codec");
            return " ";
        });
        normalized = SourceQualityPhraseRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("release-source");
            return " ";
        });
        normalized = LeadingReleasePrefixRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("release-prefix");
            return string.Empty;
        });
        normalized = TrailingSymbolReleaseTokenRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("release-tail");
            return " ";
        });
        normalized = LanguageSubtitlePhraseRegex().Replace(normalized, match =>
        {
            removedNoiseCategories.Add("language-subtitle");
            return " ";
        });
        normalized = SeasonEpisodeRegex().Replace(normalized, " ");
        normalized = ReleaseGroupRegex().Replace(normalized, " ");

        foreach (var token in NoiseTokens)
        {
            var updated = Regex.Replace(
                normalized,
                $@"\b{Regex.Escape(token)}\b",
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!string.Equals(updated, normalized, StringComparison.Ordinal))
            {
                removedNoiseCategories.Add("release-token");
                normalized = updated;
            }
        }

        if (HasReleaseCleanupContext(removedNoiseCategories))
        {
            normalized = EditionTailRegex().Replace(normalized, match =>
            {
                removedNoiseCategories.Add("edition-tail");
                return " ";
            });
        }

        if (year.HasValue)
        {
            normalized = Regex.Replace(
                normalized,
                $@"\b{year.Value}\b",
                " ",
                RegexOptions.CultureInvariant);
        }

        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        var titleBeforeTrailingCleanup = normalized;
        normalized = TrimTrailingTitleNoise(normalized);
        if (!string.Equals(titleBeforeTrailingCleanup, normalized, StringComparison.Ordinal))
        {
            removedNoiseCategories.Add("trailing-punctuation");
        }

        return new ParsedMovieName
        {
            CleanTitle = normalized,
            ReleaseYear = year,
            RemovedNoiseCategories = removedNoiseCategories
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static double CalculateTitleSimilarity(string left, string right)
    {
        var leftVariants = BuildTitleVariants(left);
        var rightVariants = BuildTitleVariants(right);

        var bestScore = 0d;
        foreach (var leftVariant in leftVariants)
        {
            foreach (var rightVariant in rightVariants)
            {
                bestScore = Math.Max(bestScore, CalculateNormalizedSimilarity(leftVariant, rightVariant));
            }
        }

        return bestScore;
    }

    private static IReadOnlyCollection<string> BuildTitleVariants(string value)
    {
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddVariant(variants, value);

        var cleaned = WhitespaceRegex().Replace(BracketedContentRegex().Replace(CleanupSeparatorsRegex().Replace(value, " "), " "), " ").Trim();
        AddVariant(variants, cleaned);
        AddVariant(variants, YearRegex().Replace(cleaned, " "));

        var cjkBuilder = new StringBuilder(cleaned.Length);
        var latinBuilder = new StringBuilder(cleaned.Length);
        foreach (var ch in cleaned)
        {
            if (IsCjk(ch))
            {
                cjkBuilder.Append(ch);
                latinBuilder.Append(' ');
            }
            else if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                latinBuilder.Append(ch);
                cjkBuilder.Append(' ');
            }
            else
            {
                latinBuilder.Append(' ');
                cjkBuilder.Append(' ');
            }
        }

        AddVariant(variants, cjkBuilder.ToString());
        AddVariant(variants, latinBuilder.ToString());

        foreach (Match match in TitleSegmentRegex().Matches(cleaned))
        {
            AddVariant(variants, match.Value);
        }

        return variants.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    private static void AddVariant(ISet<string> variants, string? value)
    {
        var normalized = NormalizeTitle(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            variants.Add(normalized);
        }
    }

    private static string NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || IsCjk(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static double CalculateNormalizedSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0d;
        }

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return 1d;
        }

        var leftTokens = left
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightTokens = right
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var overlapCount = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var unionCount = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var tokenScore = unionCount == 0 ? 0d : (double)overlapCount / unionCount;

        var charScore = 1d - ((double)LevenshteinDistance(left, right) / Math.Max(left.Length, right.Length));
        var subsetScore = leftTokens.IsSubsetOf(rightTokens) || rightTokens.IsSubsetOf(leftTokens)
            ? (double)Math.Min(leftTokens.Count, rightTokens.Count) / Math.Max(leftTokens.Count, rightTokens.Count)
            : 0d;

        return Math.Clamp(Math.Max(subsetScore, (tokenScore * 0.6d) + (charScore * 0.4d)), 0d, 1d);
    }

    private static Match FindLikelyReleaseYearMatch(string value)
    {
        return YearRegex()
                   .Matches(value)
                   .Cast<Match>()
                   .LastOrDefault()
               ?? Match.Empty;
    }

    private static bool HasLikelyTitleBeforeYear(string value, Match yearMatch)
    {
        if (!yearMatch.Success || yearMatch.Index <= 0)
        {
            return false;
        }

        var prefix = value[..yearMatch.Index];
        var normalized = NormalizeTitle(
            CleanupSeparatorsRegex().Replace(
                BracketedContentRegex().Replace(prefix, " "),
                " "));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Any(IsCjk)
               || normalized.Count(char.IsLetter) >= 3;
    }

    private static int? ExtractYear(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Value, out var year) ? year : null;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var matrix = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }

    private static string TrimTrailingTitleNoise(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex()
            .Replace(TrailingDanglingTitleNoiseRegex().Replace(value.Trim(), " "), " ")
            .Trim();
    }

    private static bool HasReleaseCleanupContext(IReadOnlySet<string> removedNoiseCategories)
    {
        return removedNoiseCategories.Contains("release-year-tail")
               || removedNoiseCategories.Contains("release-source")
               || removedNoiseCategories.Contains("release-tail")
               || removedNoiseCategories.Contains("language-subtitle")
               || removedNoiseCategories.Contains("audio-codec")
               || removedNoiseCategories.Contains("channel-layout")
               || removedNoiseCategories.Contains("video-codec")
               || removedNoiseCategories.Contains("release-token")
               || removedNoiseCategories.Contains("html-entity");
    }

    private static bool IsCjk(char ch)
    {
        return ch >= 0x4E00 && ch <= 0x9FFF;
    }

    [GeneratedRegex(@"\b(19\d{2}|20\d{2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"[.\-_·•]+", RegexOptions.CultureInvariant)]
    private static partial Regex CleanupSeparatorsRegex();

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}|（[^）]+）|【[^】]+】", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedContentRegex();

    [GeneratedRegex(@"\bS\d{1,4}E\d{1,4}\b|\b\d{1,4}x\d{1,4}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonEpisodeRegex();

    [GeneratedRegex(@"-\s*[A-Za-z0-9]+$|\b(?:GROUP|TEAM)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseGroupRegex();

    [GeneratedRegex(@"\b(?:DTS(?:\s+(?:HD|MA|X|ES))*|TRUEHD|ATMOS|DDP|EAC3|AC3|AAC|FLAC|LPCM|PCM)(?:\s+(?:HD|MA|X|ES|ATMOS))*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudioCodecPhraseRegex();

    [GeneratedRegex(@"(?:^|\s)(?:[257]\s+[01]|[257]\s*\.\s*[01])(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudioChannelLayoutRegex();

    [GeneratedRegex(@"\b(?:X\s*26[45]|H\s*\.?\s*26[45]|HEVC|AV1|VC\s*1|MPEG\s*2|10\s*BIT|8\s*BIT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VideoCodecPhraseRegex();

    [GeneratedRegex(@"\b(?:3D|4K|8K|UHD|FHD|HD|SD|HQ|HDR|HDR10|DV|DOLBY\s+VISION|BLU\s*RAY|BLURAY|BD|BD\s*REMUX|BDRIP|BRRIP|WEB(?:\s*[- ]?\s*(?:DL|RIP))?|WEBDL|WEBRIP|HDRIP|DVDRIP|HDTV|REMUX|PROPER|REPACK|EXTENDED|LIMITED|SPECIAL\s+EDITION|ULTIMATE\s+EDITION|COLLECTORS?\s+EDITION|DIRECTORS?\s+CUT|THEATRICAL|IMAX|AMZN|NF|DSNP|HMAX|ITUNES)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceQualityPhraseRegex();

    [GeneratedRegex(@"^\s*(?:3\s*D|4\s*K|8\s*K)\s*(?=[\u4e00-\u9fff])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingReleasePrefixRegex();

    [GeneratedRegex(@"\s*(?:\u5b8c\u7f8e)?(?:\u7ec8\u6781\u7248|\u5b8c\u7f8e\u7248|\u6536\u85cf\u7248|\u5178\u85cf\u7248)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EditionTailRegex();

    [GeneratedRegex(@"\b(?:JAPANESE|ENGLISH|CHINESE|MANDARIN|CANTONESE|KOREAN|FRENCH|GERMAN|SPANISH|MULTI|SUBS?|SUBBED|DUBBED|DUAL\s+AUDIO)\b|(?:\u4e2d\u5b57|\u4e2d\u82f1\u5b57\u5e55|\u5b57\u5e55|\u56fd\u8bed|\u7ca4\u8bed|\u914d\u97f3|\u516c\u6620\u4e2d\u5b57|\u51c0\u7248)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LanguageSubtitlePhraseRegex();

    [GeneratedRegex(@"(?:^|\s)[\p{Sc}\p{P}\p{S}]*[A-Za-z0-9]{2,}[A-Za-z0-9\p{Sc}\p{P}\p{S}]*[\p{Sc}\p{P}\p{S}]+[A-Za-z0-9\p{Sc}\p{P}\p{S}]*\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingSymbolReleaseTokenRegex();

    [GeneratedRegex(@"[\s\(\[\{（【《「『,，.。:：;；_\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDanglingTitleNoiseRegex();

    [GeneratedRegex(@"[\p{IsCJKUnifiedIdeographs}]{2,}|[A-Za-z][A-Za-z0-9'’&\s:]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex TitleSegmentRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
