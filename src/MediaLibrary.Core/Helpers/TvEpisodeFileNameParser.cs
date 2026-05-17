using System.Text.RegularExpressions;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Helpers;

public static partial class TvEpisodeFileNameParser
{
    public static TvEpisodeFileNameParseResult Parse(
        string fileName,
        bool allowSeasonContextOnly = false,
        int? seasonNumberHint = null,
        bool allowStrongContextFallbacks = false)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
        {
            return new TvEpisodeFileNameParseResult();
        }

        var multiEpisodeMatch = FindMultiEpisodeMatch(nameWithoutExtension);
        if (multiEpisodeMatch is not null)
        {
            return BuildResult(nameWithoutExtension, multiEpisodeMatch.Value, "MultiEpisode", isMultiEpisode: true);
        }

        var explicitMatch = FindExplicitEpisodeMatch(nameWithoutExtension);
        if (explicitMatch is not null)
        {
            return BuildResult(nameWithoutExtension, explicitMatch.Value, explicitMatch.Value.Kind, isMultiEpisode: false);
        }

        if (!allowSeasonContextOnly)
        {
            return new TvEpisodeFileNameParseResult();
        }

        var contextMatch = FindContextEpisodeMatch(
            nameWithoutExtension,
            seasonNumberHint,
            allowStrongContextFallbacks);
        return contextMatch is null
            ? new TvEpisodeFileNameParseResult()
            : BuildResult(
                nameWithoutExtension,
                contextMatch.Value,
                contextMatch.Value.Kind,
                isMultiEpisode: false,
                isSeasonContextOnly: true);
    }

    public static int? TryParseSeasonNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var regex in new[]
                 {
                     SeasonTokenRegex(),
                     ChineseSeasonTokenRegex(),
                     EnglishSeasonTokenRegex()
                 })
        {
            var match = regex.Match(value);
            if (match.Success && TryReadSeasonToken(match.Groups["season"].Value, out var seasonNumber))
            {
                return Math.Max(1, seasonNumber);
            }
        }

        return null;
    }

    public static bool IsSeasonFolderName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Path.GetFileNameWithoutExtension(value).Trim();
        return SeasonTokenRegex().IsMatch(normalized)
               || ChineseSeasonTokenRegex().IsMatch(normalized)
               || EnglishSeasonTokenRegex().IsMatch(normalized);
    }

    public static bool IsUsableSeriesSearchQuery(string value)
    {
        return GetSeriesSearchQueryRejectReason(value) is null;
    }

    public static string? GetSeriesSearchQueryRejectReason(string value)
    {
        var normalized = CleanSeriesNameCandidate(value);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length <= 1)
        {
            return "empty-or-too-short-query";
        }

        if (GenericSeasonOnlyQueryRegex().IsMatch(normalized)
            || GenericChineseCountOnlyQueryRegex().IsMatch(normalized)
            || GenericChineseSeasonRangeOnlyQueryRegex().IsMatch(normalized))
        {
            return "generic-season-or-count-query";
        }

        if (QualityOnlyQueryRegex().IsMatch(normalized))
        {
            return "quality-only-query";
        }

        if (CodecOnlyQueryRegex().IsMatch(normalized))
        {
            return "codec-only-query";
        }

        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 0)
        {
            var metadataTokenCount = tokens.Count(IsReleaseMetadataQueryToken);
            var titleTokenCount = tokens.Length - metadataTokenCount;
            if (titleTokenCount == 0)
            {
                return "release-metadata-only-query";
            }

            if (tokens.Length >= 3 && titleTokenCount <= 1 && metadataTokenCount >= 2)
            {
                return "dirty-query";
            }
        }

        return normalized.Any(char.IsLetter) || normalized.Any(IsCjkLetter)
            ? null
            : "no-title-token";
    }

    private static bool IsReleaseMetadataQueryToken(string token)
    {
        var normalized = token.Trim();
        return !string.IsNullOrWhiteSpace(normalized)
               && ReleaseMetadataQueryTokenRegex().IsMatch(normalized);
    }

    public static bool IsBareNumberEpisodeFileName(string value)
    {
        var normalized = Path.GetFileNameWithoutExtension(value).Trim();
        return BareNumberEpisodeRegex().IsMatch(normalized);
    }

    public static bool IsTitleNumberEpisodeFileName(string value)
    {
        var normalized = Path.GetFileNameWithoutExtension(value).Trim();
        return !BareNumberEpisodeRegex().IsMatch(normalized)
               && TitleNumberEpisodeRegex().IsMatch(normalized);
    }

    public static bool HasChineseSeasonHint(string value)
    {
        return ChineseSeasonTokenRegex().IsMatch(value);
    }

    public static bool HasChineseEpisodeHint(string value)
    {
        return ChineseContextEpisodeRegex().IsMatch(value)
               || ChineseSeasonEpisodeRegex().IsMatch(value)
               || ChineseMultiEpisodeRegex().IsMatch(value);
    }

    public static bool HasChineseCountHint(string value)
    {
        return ChineseTotalCountRegex().IsMatch(value)
               || ChineseSeasonRangeRegex().IsMatch(value);
    }

    public static string CleanSeriesNameCandidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = Path.GetFileNameWithoutExtension(value).Trim();
        normalized = BracketedContentRegex().Replace(normalized, " ");
        normalized = MultiEpisodeTokenRegex().Replace(normalized, " ");
        normalized = MultiEpisodeCompactRegex().Replace(normalized, " ");
        normalized = ChineseMultiEpisodeRegex().Replace(normalized, " ");
        normalized = ChineseTotalCountRegex().Replace(normalized, " ");
        normalized = ChineseSeasonRangeRegex().Replace(normalized, " ");
        normalized = ExplicitSeasonEpisodeRegex().Replace(normalized, " ");
        normalized = XEpisodeRegex().Replace(normalized, " ");
        normalized = ChineseSeasonEpisodeRegex().Replace(normalized, " ");
        normalized = EnglishSeasonEpisodeRegex().Replace(normalized, " ");
        normalized = SeasonTokenRegex().Replace(normalized, " ");
        normalized = ChineseSeasonTokenRegex().Replace(normalized, " ");
        normalized = EnglishSeasonTokenRegex().Replace(normalized, " ");
        normalized = ContextEpisodeRegex().Replace(normalized, " ");
        normalized = EnglishContextEpisodeRegex().Replace(normalized, " ");
        normalized = ChineseContextEpisodeRegex().Replace(normalized, " ");
        normalized = QualityNoiseTokenRegex().Replace(normalized, " ");
        normalized = ReleaseNoiseTokenRegex().Replace(normalized, " ");
        normalized = SeparatorsRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized;
    }

    private static EpisodeMatch? FindMultiEpisodeMatch(string value)
    {
        foreach (var regex in new[] { MultiEpisodeTokenRegex(), MultiEpisodeCompactRegex(), ChineseMultiEpisodeRegex() })
        {
            var match = regex.Match(value);
            if (!match.Success)
            {
                continue;
            }

            var seasonNumber = ReadOptionalInt(match.Groups["season"].Value) ?? TryParseSeasonNumber(value) ?? 1;
            var episodeNumber = ReadOptionalInt(match.Groups["episode"].Value) ?? 0;
            return new EpisodeMatch(match, Math.Max(1, seasonNumber), episodeNumber, "MultiEpisode");
        }

        return null;
    }

    private static EpisodeMatch? FindExplicitEpisodeMatch(string value)
    {
        var ordered = new[]
        {
            (Regex: ExplicitSeasonEpisodeRegex(), Kind: "SxxExx"),
            (Regex: XEpisodeRegex(), Kind: "NxN"),
            (Regex: ChineseSeasonEpisodeRegex(), Kind: "ChineseSeasonEpisode"),
            (Regex: EnglishSeasonEpisodeRegex(), Kind: "EnglishSeasonEpisode")
        };

        foreach (var entry in ordered)
        {
            var match = entry.Regex.Match(value);
            if (!match.Success)
            {
                continue;
            }

            if (!TryReadSeasonToken(match.Groups["season"].Value, out var seasonNumber)
                || !TryReadInt(match.Groups["episode"].Value, out var episodeNumber))
            {
                continue;
            }

            return new EpisodeMatch(match, Math.Max(1, seasonNumber), episodeNumber, entry.Kind);
        }

        return null;
    }

    private static EpisodeMatch? FindContextEpisodeMatch(
        string value,
        int? seasonNumberHint,
        bool allowStrongContextFallbacks)
    {
        var ordered = new List<(Regex Regex, string Kind)>
        {
            (ContextEpisodeRegex(), "ContextEpisode"),
            (EnglishContextEpisodeRegex(), "EnglishContextEpisode"),
            (ChineseContextEpisodeRegex(), "ChineseContextEpisode")
        };

        if (allowStrongContextFallbacks)
        {
            ordered.Add((BareNumberEpisodeRegex(), "StrongContextBareNumber"));
            ordered.Add((TitleNumberEpisodeRegex(), "StrongContextTitleNumber"));
        }

        foreach (var entry in ordered)
        {
            var match = entry.Regex.Match(value);
            if (!match.Success || !TryReadInt(match.Groups["episode"].Value, out var episodeNumber))
            {
                continue;
            }

            return new EpisodeMatch(match, seasonNumberHint ?? TryParseSeasonNumber(value) ?? 1, episodeNumber, entry.Kind);
        }

        return null;
    }

    private static TvEpisodeFileNameParseResult BuildResult(
        string nameWithoutExtension,
        EpisodeMatch episodeMatch,
        string matchKind,
        bool isMultiEpisode,
        bool isSeasonContextOnly = false)
    {
        var seriesCandidate = CleanSeriesNameCandidate(nameWithoutExtension[..episodeMatch.Match.Index]);
        var episodeTitleCandidate = string.Empty;
        var titleStart = episodeMatch.Match.Index + episodeMatch.Match.Length;
        if (titleStart < nameWithoutExtension.Length)
        {
            episodeTitleCandidate = SeparatorsRegex().Replace(nameWithoutExtension[titleStart..], " ");
            episodeTitleCandidate = WhitespaceRegex().Replace(episodeTitleCandidate, " ").Trim();
        }

        return new TvEpisodeFileNameParseResult
        {
            IsEpisodeLike = true,
            IsSeasonContextOnly = isSeasonContextOnly,
            IsMultiEpisode = isMultiEpisode,
            SeasonNumber = Math.Max(1, episodeMatch.SeasonNumber),
            EpisodeNumber = Math.Max(0, episodeMatch.EpisodeNumber),
            SeriesNameCandidate = seriesCandidate,
            EpisodeTitleCandidate = episodeTitleCandidate,
            MatchKind = matchKind
        };
    }

    private static int? ReadOptionalInt(string value)
    {
        return TryReadInt(value, out var number) ? number : null;
    }

    private static bool TryReadInt(string value, out int number)
    {
        return int.TryParse(value, out number) && number > 0;
    }

    private static bool TryReadSeasonToken(string value, out int number)
    {
        if (TryReadInt(value, out number))
        {
            return true;
        }

        number = ParseChineseNumber(value);
        return number > 0;
    }

    private static int ParseChineseNumber(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        var total = 0;
        var current = 0;
        foreach (var ch in normalized)
        {
            var digit = ch switch
            {
                '\u96f6' => 0,
                '\u4e00' => 1,
                '\u4e8c' or '\u4e24' => 2,
                '\u4e09' => 3,
                '\u56db' => 4,
                '\u4e94' => 5,
                '\u516d' => 6,
                '\u4e03' => 7,
                '\u516b' => 8,
                '\u4e5d' => 9,
                _ => -1
            };
            if (digit >= 0)
            {
                current = digit;
                continue;
            }

            if (ch == '\u5341')
            {
                total += (current == 0 ? 1 : current) * 10;
                current = 0;
                continue;
            }

            return 0;
        }

        return total + current;
    }

    private static bool IsCjkLetter(char ch)
    {
        return ch is >= '\u4e00' and <= '\u9fff';
    }

    private readonly record struct EpisodeMatch(Match Match, int SeasonNumber, int EpisodeNumber, string Kind);

    [GeneratedRegex(@"[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,3})\s*(?:-|~|to)\s*(?:[Ee])?\d{1,3}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiEpisodeTokenRegex();

    [GeneratedRegex(@"[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,3})[\s._-]*[Ee]\d{1,3}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiEpisodeCompactRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<season>[0-9一二三四五六七八九十两]{1,4})?\s*\u5b63?\s*\u7b2c?\s*(?<episode>\d{1,3})\s*(?:-|~|\u81f3|\u5230)\s*\d{1,3}\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseMultiEpisodeRegex();

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitSeasonEpisodeRegex();

    [GeneratedRegex(@"\b(?<season>\d{1,2})x(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XEpisodeRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<season>[0-9一二三四五六七八九十两]{1,4})\s*\u5b63\s*\u7b2c?\s*(?<episode>\d{1,3})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseSeasonEpisodeRegex();

    [GeneratedRegex(@"\bSeason\s*(?<season>\d{1,2})\s*Episode\s*(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishSeasonEpisodeRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:E|EP)(?<episode>\d{1,3})(?:$|[\s._\-\]\)])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContextEpisodeRegex();

    [GeneratedRegex(@"\bEpisode\s*(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishContextEpisodeRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<episode>\d{1,3})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseContextEpisodeRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,3})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberEpisodeRegex();

    [GeneratedRegex(@"^(?=.*[\p{L}\u4e00-\u9fff])(?<title>.+?)(?<episode>\d{1,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex TitleNumberEpisodeRegex();

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonTokenRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<season>[0-9一二三四五六七八九十两]{1,4})\s*\u5b63(?:\s*\u5168\s*\d{1,3}\s*\u96c6)?", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseSeasonTokenRegex();

    [GeneratedRegex(@"\u5168\s*(?<count>[0-9\u4e00\u4e8c\u4e24\u4e09\u56db\u4e94\u516d\u4e03\u516b\u4e5d\u5341]{1,4})\s*[\u96c6\u8bdd\u5b63]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseTotalCountRegex();

    [GeneratedRegex(@"(?<start>[0-9\u4e00\u4e8c\u4e24\u4e09\u56db\u4e94\u516d\u4e03\u516b\u4e5d\u5341]{1,4})\s*(?:-|~|\u2013|\u2014|\u81f3|\u5230)\s*(?<end>[0-9\u4e00\u4e8c\u4e24\u4e09\u56db\u4e94\u516d\u4e03\u516b\u4e5d\u5341]{1,4})\s*\u5b63", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseSeasonRangeRegex();

    [GeneratedRegex(@"\bSeason\s*(?<season>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishSeasonTokenRegex();

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedContentRegex();

    [GeneratedRegex(@"[._\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(?:4K|8K|1080P|2160P|720P|HDR|DV|WEB[-\s]?DL|WEBRIP|BDRIP|BLURAY|HEVC|H\.?265|H\.?264|X264|X265|AAC|DDP?|ATMOS|REMUX)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualityNoiseTokenRegex();

    [GeneratedRegex(@"(?:字幕组|小组|压制|双语|中字|内封|外挂)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseNoiseTokenRegex();

    [GeneratedRegex(@"^(?:\u7b2c\s*)?[0-9一二三四五六七八九十两]{1,4}\s*\u5b63(?:\s*\u5168\s*\d{1,3}\s*\u96c6)?$|^(?:Season\s*)?\d{1,2}$|^[Ss]\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GenericSeasonOnlyQueryRegex();

    [GeneratedRegex(@"^\u5168\s*[0-9\u4e00\u4e8c\u4e24\u4e09\u56db\u4e94\u516d\u4e03\u516b\u4e5d\u5341]{1,4}\s*[\u96c6\u8bdd\u5b63]$", RegexOptions.CultureInvariant)]
    private static partial Regex GenericChineseCountOnlyQueryRegex();

    [GeneratedRegex(@"^[0-9\u4e00\u4e8c\u4e24\u4e09\u56db\u4e94\u516d\u4e03\u516b\u4e5d\u5341]{1,4}\s*(?:-|~|\u2013|\u2014|\u81f3|\u5230)\s*[0-9\u4e00\u4e8c\u4e24\u4e09\u56db\u4e94\u516d\u4e03\u516b\u4e5d\u5341]{1,4}\s*\u5b63$", RegexOptions.CultureInvariant)]
    private static partial Regex GenericChineseSeasonRangeOnlyQueryRegex();

    [GeneratedRegex(@"^(?:4K|8K|1080P|2160P|720P|HDR|DV|DX|WEB[-\s]?DL|WEBRIP|BDRIP|BLURAY|HEVC|H\.?265|H\.?264|X264|X265|\s)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualityOnlyQueryRegex();

    [GeneratedRegex(@"^(?:DX|AAC|DDP?|ATMOS|REMUX|HEVC|X264|X265|H\.?264|H\.?265)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodecOnlyQueryRegex();

    [GeneratedRegex(@"^(?:\d{1,4}|19\d{2}|20\d{2}|4K|8K|1080P|2160P|720P|480P|UHD|FHD|HD|SD|HDR|HDR10|DV|DOLBY|VISION|WEB|DL|WEBDL|WEBRIP|BDRIP|BRRIP|BLURAY|HDTV|REMUX|PROPER|REPACK|EXTENDED|LIMITED|IMAX|HEVC|H\.?265|H\.?264|X264|X265|AV1|AAC|AC3|EAC3|DDP\d?|DTS|TRUEHD|ATMOS|FLAC|LPCM|PCM|MA|JAPANESE|ENGLISH|CHINESE|MANDARIN|CANTONESE|KOREAN|MULTI|SUBS?|SUBBED|DUBBED|DUAL|AUDIO|AMZN|NF|DSNP|HMAX|ITUNES)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseMetadataQueryTokenRegex();
}
