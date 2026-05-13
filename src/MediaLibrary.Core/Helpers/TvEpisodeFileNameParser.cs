using System.Text.RegularExpressions;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Helpers;

public static partial class TvEpisodeFileNameParser
{
    public static TvEpisodeFileNameParseResult Parse(
        string fileName,
        bool allowSeasonContextOnly = false)
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

        var contextMatch = FindContextEpisodeMatch(nameWithoutExtension);
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

        foreach (var regex in new[] { SeasonTokenRegex(), ChineseSeasonTokenRegex(), EnglishSeasonTokenRegex() })
        {
            var match = regex.Match(value);
            if (match.Success && TryReadInt(match.Groups["season"].Value, out var seasonNumber))
            {
                return Math.Max(1, seasonNumber);
            }
        }

        return null;
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
        normalized = ExplicitSeasonEpisodeRegex().Replace(normalized, " ");
        normalized = XEpisodeRegex().Replace(normalized, " ");
        normalized = ChineseSeasonEpisodeRegex().Replace(normalized, " ");
        normalized = EnglishSeasonEpisodeRegex().Replace(normalized, " ");
        normalized = SeasonTokenRegex().Replace(normalized, " ");
        normalized = ChineseSeasonTokenRegex().Replace(normalized, " ");
        normalized = EnglishSeasonTokenRegex().Replace(normalized, " ");
        normalized = ContextEpisodeRegex().Replace(normalized, " ");
        normalized = ChineseContextEpisodeRegex().Replace(normalized, " ");
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

            if (!TryReadInt(match.Groups["season"].Value, out var seasonNumber)
                || !TryReadInt(match.Groups["episode"].Value, out var episodeNumber))
            {
                continue;
            }

            return new EpisodeMatch(match, Math.Max(1, seasonNumber), episodeNumber, entry.Kind);
        }

        return null;
    }

    private static EpisodeMatch? FindContextEpisodeMatch(string value)
    {
        var ordered = new[]
        {
            (Regex: ContextEpisodeRegex(), Kind: "ContextEpisode"),
            (Regex: EnglishContextEpisodeRegex(), Kind: "EnglishContextEpisode"),
            (Regex: ChineseContextEpisodeRegex(), Kind: "ChineseContextEpisode")
        };

        foreach (var entry in ordered)
        {
            var match = entry.Regex.Match(value);
            if (!match.Success || !TryReadInt(match.Groups["episode"].Value, out var episodeNumber))
            {
                continue;
            }

            return new EpisodeMatch(match, TryParseSeasonNumber(value) ?? 1, episodeNumber, entry.Kind);
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

    private readonly record struct EpisodeMatch(Match Match, int SeasonNumber, int EpisodeNumber, string Kind);

    [GeneratedRegex(@"[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,3})\s*(?:-|~|to)\s*(?:[Ee])?\d{1,3}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiEpisodeTokenRegex();

    [GeneratedRegex(@"[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,3})[\s._-]*[Ee]\d{1,3}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiEpisodeCompactRegex();

    [GeneratedRegex(@"第\s*(?<season>\d{1,2})?\s*季?\s*第?\s*(?<episode>\d{1,3})\s*(?:-|~|至|到)\s*\d{1,3}\s*集", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseMultiEpisodeRegex();

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitSeasonEpisodeRegex();

    [GeneratedRegex(@"\b(?<season>\d{1,2})x(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XEpisodeRegex();

    [GeneratedRegex(@"第\s*(?<season>\d{1,2})\s*季\s*第?\s*(?<episode>\d{1,3})\s*集", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseSeasonEpisodeRegex();

    [GeneratedRegex(@"\bSeason\s*(?<season>\d{1,2})\s*Episode\s*(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishSeasonEpisodeRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:E|EP)(?<episode>\d{1,3})(?:$|[\s._\-\]\)])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContextEpisodeRegex();

    [GeneratedRegex(@"\bEpisode\s*(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishContextEpisodeRegex();

    [GeneratedRegex(@"第\s*(?<episode>\d{1,3})\s*集", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseContextEpisodeRegex();

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonTokenRegex();

    [GeneratedRegex(@"第\s*(?<season>\d{1,2})\s*季", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseSeasonTokenRegex();

    [GeneratedRegex(@"\bSeason\s*(?<season>\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishSeasonTokenRegex();

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedContentRegex();

    [GeneratedRegex(@"[._\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
