using System.Globalization;
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

        var multiEpisodeFalsePositiveAvoided = HasRejectedMultiEpisodeCandidate(nameWithoutExtension);
        var explicitMatch = FindExplicitEpisodeMatch(nameWithoutExtension);
        if (explicitMatch is not null)
        {
            return BuildResult(
                nameWithoutExtension,
                explicitMatch.Value,
                explicitMatch.Value.Kind,
                isMultiEpisode: false,
                multiEpisodeFalsePositiveAvoided: multiEpisodeFalsePositiveAvoided);
        }

        if (!allowSeasonContextOnly)
        {
            return new TvEpisodeFileNameParseResult
            {
                MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided
            };
        }

        var contextMatch = FindContextEpisodeMatch(
            nameWithoutExtension,
            seasonNumberHint,
            allowStrongContextFallbacks);
        if (contextMatch is not null)
        {
            return BuildResult(
                nameWithoutExtension,
                contextMatch.Value,
                contextMatch.Value.Kind,
                isMultiEpisode: false,
                isSeasonContextOnly: true,
                multiEpisodeFalsePositiveAvoided: multiEpisodeFalsePositiveAvoided);
        }

        if (TryStripTrailingDuplicateCopySuffix(nameWithoutExtension, out var duplicateBaseName))
        {
            contextMatch = FindContextEpisodeMatch(
                duplicateBaseName,
                seasonNumberHint,
                allowStrongContextFallbacks);
            if (contextMatch is not null)
            {
                return BuildResult(
                    duplicateBaseName,
                    contextMatch.Value,
                    contextMatch.Value.Kind,
                    isMultiEpisode: false,
                    isSeasonContextOnly: true,
                    multiEpisodeFalsePositiveAvoided: multiEpisodeFalsePositiveAvoided);
            }
        }

        return new TvEpisodeFileNameParseResult
        {
            MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided
        };
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

        var normalized = RemoveKnownFileExtension(value).Trim();
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

        if (IsStructuralPartOnlyQuery(normalized))
        {
            return "structural-part-query";
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

    private static bool IsStructuralPartOnlyQuery(string value)
    {
        var tokens = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 0
               && tokens.Any(IsStructuralPartToken)
               && tokens.All(IsStructuralPartQueryToken);
    }

    private static bool IsStructuralPartQueryToken(string token)
    {
        return IsStructuralPartToken(token)
               || StructuralSeasonTokenRegex().IsMatch(token)
               || int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsStructuralPartToken(string token)
    {
        return StructuralPartTokenRegex().IsMatch(token);
    }

    public static bool IsBareNumberEpisodeFileName(string value)
    {
        var normalized = RemoveKnownFileExtension(value).Trim();
        if (BareNumberEpisodeRegex().IsMatch(normalized))
        {
            return true;
        }

        return TryStripTrailingDuplicateCopySuffix(normalized, out var duplicateBaseName)
               && BareNumberEpisodeRegex().IsMatch(duplicateBaseName);
    }

    public static bool IsTitleNumberEpisodeFileName(string value)
    {
        return TryParseEpisodeSequencePattern(value, out var pattern)
               && IsTitleLikeSequencePattern(pattern.Pattern);
    }

    public static bool IsVerifiedTitleNumberSequenceMember(string fileName, TvEpisodeSequenceAnalysis sequence)
    {
        return sequence.IsSequence
               && TryParseEpisodeSequencePattern(fileName, out var pattern)
               && string.Equals(pattern.PatternKey, sequence.PatternKey, StringComparison.OrdinalIgnoreCase)
               && pattern.Number >= sequence.StartNumber
               && pattern.Number <= sequence.EndNumber;
    }

    public static TvEpisodeFileNameParseResult ParseVerifiedTitleNumberSequence(
        string fileName,
        TvEpisodeSequenceAnalysis sequence,
        int? seasonNumberHint = null)
    {
        var nameWithoutExtension = GetBaseNameFromRawFileName(fileName);
        var multiEpisodeFalsePositiveAvoided = HasRejectedMultiEpisodeCandidate(nameWithoutExtension);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension)
            || !IsVerifiedTitleNumberSequenceMember(fileName, sequence))
        {
            return new TvEpisodeFileNameParseResult
            {
                MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided
            };
        }

        if (!TryParseEpisodeSequencePatternFromBaseName(nameWithoutExtension, out var pattern)
            || !string.Equals(pattern.PatternKey, sequence.PatternKey, StringComparison.OrdinalIgnoreCase)
            || pattern.Number < sequence.StartNumber
            || pattern.Number > sequence.EndNumber)
        {
            return new TvEpisodeFileNameParseResult
            {
                VerifiedTitleNumberSequenceContext = true,
                MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided,
                MatchKind = "VerifiedTitleNumberSequenceFailed"
            };
        }

        var explicitSeasonNumber = pattern.SeasonNumber ?? TryParseSeasonNumber(nameWithoutExtension);
        if (pattern.PartHintDetected)
        {
            return new TvEpisodeFileNameParseResult
            {
                IsEpisodeLike = false,
                IsSeasonContextOnly = false,
                IsMultiEpisode = false,
                MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided,
                SeasonNumber = Math.Max(1, explicitSeasonNumber ?? seasonNumberHint ?? 1),
                EpisodeNumber = 0,
                SeriesNameCandidate = CleanSeriesNameCandidate(pattern.PrefixKey),
                EpisodeTitleCandidate = string.Empty,
                MatchKind = $"VerifiedEpisodeSequence:{pattern.Pattern}",
                VerifiedTitleNumberSequenceContext = true,
                PartHintDetected = true,
                PartHint = pattern.PartNumber,
                EpisodeInPart = pattern.EpisodeInPart ?? pattern.Number,
                EpisodeOffset = null,
                EpisodeOffsetApplied = false,
                EpisodeOffsetSkippedReason = "not-evaluated",
                EpisodeOffsetSource = string.Empty
            };
        }

        return new TvEpisodeFileNameParseResult
        {
            IsEpisodeLike = true,
            IsSeasonContextOnly = !explicitSeasonNumber.HasValue,
            IsMultiEpisode = false,
            MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided,
            SeasonNumber = Math.Max(1, explicitSeasonNumber ?? seasonNumberHint ?? 1),
            EpisodeNumber = pattern.Number,
            SeriesNameCandidate = CleanSeriesNameCandidate(pattern.PrefixKey),
            EpisodeTitleCandidate = string.Empty,
            MatchKind = $"VerifiedEpisodeSequence:{pattern.Pattern}",
            VerifiedTitleNumberSequenceContext = true
        };
    }

    public static bool TryAnalyzeTitleNumberSequence(
        IEnumerable<string> fileNames,
        out TvEpisodeSequenceAnalysis sequence)
    {
        var patterns = fileNames
            .Select(
                x => TryParseEpisodeSequencePattern(x, out var pattern)
                    && IsTitleLikeSequencePattern(pattern.Pattern)
                        ? pattern
                        : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();
        return TryBuildStrictSequence(patterns, out sequence);
    }

    public static bool TryAnalyzeEpisodeSequence(
        IEnumerable<string> fileNames,
        out TvEpisodeSequenceAnalysis sequence)
    {
        var patterns = fileNames
            .Select(x => TryParseEpisodeSequencePattern(x, out var pattern) ? pattern : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();
        return TryBuildStrictSequence(patterns, out sequence);
    }

    public static bool TryParseEpisodeSequencePattern(string fileName, out TvEpisodeSequencePattern pattern)
    {
        return TryParseEpisodeSequencePatternFromBaseName(GetBaseNameFromRawFileName(fileName), out pattern);
    }

    public static bool TryStripTrailingDuplicateCopySuffix(string value, out string baseName)
    {
        baseName = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = RemoveKnownFileExtension(value).Trim();
        var match = DuplicateCopySuffixRegex().Match(normalized);
        if (!match.Success)
        {
            return false;
        }

        baseName = match.Groups["base"].Value.Trim();
        return !string.IsNullOrWhiteSpace(baseName);
    }

    private static bool TryParseEpisodeSequencePatternFromBaseName(string baseName, out TvEpisodeSequencePattern pattern)
    {
        pattern = new TvEpisodeSequencePattern(string.Empty, string.Empty, string.Empty, 0);
        var name = baseName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (TryStripTrailingDuplicateCopySuffix(name, out var duplicateBaseName))
        {
            name = duplicateBaseName;
        }

        if (TryParseTitleSeasonPartEpisodeSequencePattern(name, out pattern))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(name) || ExcludedEpisodeSequenceTokenRegex().IsMatch(name))
        {
            return false;
        }

        foreach (var regex in new[] { ExplicitSeasonEpisodeRegex(), ContextEpisodeRegex(), EnglishContextEpisodeRegex() })
        {
            var match = regex.Match(name);
            if (match.Success && TryReadInt(match.Groups["episode"].Value, out var episodeNumber))
            {
                pattern = new TvEpisodeSequencePattern("episode-marker", "episode-marker", string.Empty, episodeNumber);
                return true;
            }
        }

        var chineseMatch = ChineseContextEpisodeRegex().Match(name);
        if (chineseMatch.Success && TryReadInt(chineseMatch.Groups["episode"].Value, out var chineseEpisodeNumber))
        {
            pattern = new TvEpisodeSequencePattern("chinese-episode-marker", "chinese-episode-marker", string.Empty, chineseEpisodeNumber);
            return true;
        }

        if (TryParseBracketEpisodeSequencePattern(name, out pattern))
        {
            return true;
        }

        if (TryParseLeadingNumberTitleSequencePattern(name, out pattern))
        {
            return true;
        }

        var normalized = NormalizeTitleNumberSequenceName(name);
        var titleNumberMatch = TitleNumberEpisodeRegex().Match(normalized);
        if (titleNumberMatch.Success
            && TryReadInt(titleNumberMatch.Groups["episode"].Value, out var titleNumber)
            && TryNormalizeSequenceTitlePrefix(titleNumberMatch.Groups["title"].Value, out var prefixKey))
        {
            pattern = new TvEpisodeSequencePattern($"title-number:{prefixKey}", "title-number", prefixKey, titleNumber);
            return true;
        }

        return false;
    }

    private static string GetBaseNameFromRawFileName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName).Trim();
    }

    private static string RemoveKnownFileExtension(string value)
    {
        var normalized = value.Replace('\\', '/').Trim();
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        if (lastSeparatorIndex >= 0)
        {
            normalized = normalized[(lastSeparatorIndex + 1)..];
        }

        var extension = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return normalized;
        }

        var extensionBody = extension.TrimStart('.');
        return extensionBody.Length is >= 1 and <= 8
               && extensionBody.All(char.IsLetterOrDigit)
            ? normalized[..^extension.Length]
            : normalized;
    }

    private static bool TryParseTitleSeasonPartEpisodeSequencePattern(
        string name,
        out TvEpisodeSequencePattern pattern)
    {
        pattern = new TvEpisodeSequencePattern(string.Empty, string.Empty, string.Empty, 0);
        var normalized = NormalizeTitleNumberSequenceName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var match in new[]
                 {
                     TitleSeasonPartEpisodeSequenceRegex().Match(normalized),
                     TitlePartEpisodeSequenceRegex().Match(normalized)
                 })
        {
            if (!match.Success
                || !TryReadInt(match.Groups["part"].Value, out var partNumber)
                || !TryReadInt(match.Groups["episode"].Value, out var episodeInPart)
                || !TryNormalizeSequenceTitlePrefix(match.Groups["title"].Value, out var prefixKey)
                || IsGenericPartSequencePrefix(prefixKey))
            {
                continue;
            }

            int? seasonNumber = null;
            if (match.Groups["season"].Success
                && TryReadSeasonToken(match.Groups["season"].Value, out var parsedSeasonNumber))
            {
                seasonNumber = Math.Max(1, parsedSeasonNumber);
            }

            var keySeason = seasonNumber.HasValue
                ? $"s{seasonNumber.Value}:"
                : string.Empty;
            pattern = new TvEpisodeSequencePattern(
                $"title-part-number:{prefixKey}:{keySeason}p{partNumber}",
                "title-part-number",
                prefixKey,
                episodeInPart,
                seasonNumber,
                partNumber,
                episodeInPart,
                true);
            return true;
        }

        return false;
    }

    private static bool IsGenericPartSequencePrefix(string prefixKey)
    {
        return string.Equals(prefixKey, "part", StringComparison.OrdinalIgnoreCase)
               || string.Equals(prefixKey, "pt", StringComparison.OrdinalIgnoreCase)
               || string.Equals(prefixKey, "season", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBracketEpisodeSequencePattern(
        string name,
        out TvEpisodeSequencePattern pattern)
    {
        pattern = new TvEpisodeSequencePattern(string.Empty, string.Empty, string.Empty, 0);
        var bracketParts = BracketedContentRegex()
            .Matches(name)
            .Select(match => new BracketPart(TrimBracketContent(match.Value), match.Index, match.Length))
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .ToArray();
        if (bracketParts.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < bracketParts.Length; index++)
        {
            if (!TryParseBracketEpisodeNumber(bracketParts[index].Value, out var episodeNumber))
            {
                continue;
            }

            var titleFromText = ExtractTextBeforeBracket(name, bracketParts[index]);
            if (TryNormalizeSequenceTitlePrefix(titleFromText, out var textPrefixKey))
            {
                pattern = new TvEpisodeSequencePattern(
                    $"fansub-bracket-number:{textPrefixKey}",
                    "fansub-bracket-episode",
                    textPrefixKey,
                    episodeNumber);
                return true;
            }

            for (var titleIndex = index - 1; titleIndex >= 0; titleIndex--)
            {
                if (TryNormalizeSequenceTitlePrefix(bracketParts[titleIndex].Value, out var prefixKey))
                {
                    pattern = new TvEpisodeSequencePattern(
                        $"bracket-title-number:{prefixKey}",
                        "bracket-episode-segment",
                        prefixKey,
                        episodeNumber);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseLeadingNumberTitleSequencePattern(
        string name,
        out TvEpisodeSequencePattern pattern)
    {
        pattern = new TvEpisodeSequencePattern(string.Empty, string.Empty, string.Empty, 0);
        var match = LeadingNumberTitleSequenceRegex().Match(name);
        if (!match.Success
            || !TryReadInt(match.Groups["episode"].Value, out var episodeNumber)
            || !TryReadSeasonToken(match.Groups["season"].Value, out var seasonNumber)
            || !TryNormalizeSequenceTitlePrefix(match.Groups["title"].Value, out var prefixKey))
        {
            return false;
        }

        pattern = new TvEpisodeSequencePattern(
            $"leading-number-title:{prefixKey}:s{seasonNumber}",
            "leading-number-title",
            prefixKey,
            episodeNumber,
            Math.Max(1, seasonNumber));
        return true;
    }

    private static string ExtractTextBeforeBracket(string name, BracketPart episodeBracket)
    {
        if (episodeBracket.Index <= 0)
        {
            return string.Empty;
        }

        var prefix = name[..episodeBracket.Index];
        prefix = BracketedContentRegex().Replace(prefix, " ");
        prefix = TitleNumberReleaseTailRegex().Replace(prefix, " ");
        prefix = SeparatorsRegex().Replace(prefix, " ");
        return WhitespaceRegex().Replace(prefix, " ").Trim();
    }

    private static bool TryParseBracketEpisodeNumber(string value, out int episodeNumber)
    {
        episodeNumber = 0;
        var segmentMatch = BracketEpisodeSegmentRegex().Match(value);
        if (!segmentMatch.Success || !TryReadInt(segmentMatch.Groups["episode"].Value, out episodeNumber))
        {
            return false;
        }

        var hasExplicitEpisodeMarker = BracketEpisodeExplicitMarkerRegex().IsMatch(value);
        return hasExplicitEpisodeMarker || !IsReleaseNumber(episodeNumber);
    }

    private static string TrimBracketContent(string value)
    {
        return value.Trim().Trim('[', ']', '(', ')', '{', '}', '\u3010', '\u3011').Trim();
    }

    private static string NormalizeTitleNumberSequenceName(string value)
    {
        var normalized = BracketedContentRegex().Replace(value, " ");
        normalized = TitleNumberReleaseTailRegex().Replace(normalized, " ");
        normalized = SeparatorsRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized;
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
        if (TryStripTrailingDuplicateCopySuffix(normalized, out var duplicateBaseName))
        {
            normalized = duplicateBaseName;
        }

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
            var endEpisodeNumber = ReadOptionalInt(match.Groups["endEpisode"].Value) ?? 0;
            if (!IsPlausibleMultiEpisodeRange(episodeNumber, endEpisodeNumber))
            {
                continue;
            }

            return new EpisodeMatch(match, Math.Max(1, seasonNumber), episodeNumber, "MultiEpisode", endEpisodeNumber);
        }

        return null;
    }

    private static bool HasRejectedMultiEpisodeCandidate(string value)
    {
        foreach (var regex in new[] { MultiEpisodeTokenRegex(), MultiEpisodeCompactRegex(), ChineseMultiEpisodeRegex() })
        {
            var match = regex.Match(value);
            if (!match.Success)
            {
                continue;
            }

            var episodeNumber = ReadOptionalInt(match.Groups["episode"].Value) ?? 0;
            var endEpisodeNumber = ReadOptionalInt(match.Groups["endEpisode"].Value) ?? 0;
            if (!IsPlausibleMultiEpisodeRange(episodeNumber, endEpisodeNumber))
            {
                return true;
            }
        }

        return false;
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

            return new EpisodeMatch(match, Math.Max(1, seasonNumber), episodeNumber, entry.Kind, null);
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

            return new EpisodeMatch(match, seasonNumberHint ?? TryParseSeasonNumber(value) ?? 1, episodeNumber, entry.Kind, null);
        }

        return null;
    }

    private static TvEpisodeFileNameParseResult BuildResult(
        string nameWithoutExtension,
        EpisodeMatch episodeMatch,
        string matchKind,
        bool isMultiEpisode,
        bool isSeasonContextOnly = false,
        bool multiEpisodeFalsePositiveAvoided = false)
    {
        var seriesCandidate = episodeMatch.Match.Groups["title"].Success
            ? CleanSeriesNameCandidate(episodeMatch.Match.Groups["title"].Value)
            : CleanSeriesNameCandidate(nameWithoutExtension[..episodeMatch.Match.Index]);
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
            MultiEpisodeFalsePositiveAvoided = multiEpisodeFalsePositiveAvoided,
            MultiEpisodeEndNumber = isMultiEpisode ? episodeMatch.MultiEpisodeEndNumber : null,
            MultiEpisodePattern = isMultiEpisode ? episodeMatch.Kind : string.Empty,
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

    private static bool IsPlausibleMultiEpisodeRange(int startEpisode, int endEpisode)
    {
        return startEpisode > 0
               && endEpisode > startEpisode
               && endEpisode - startEpisode <= 20
               && !IsReleaseNumber(endEpisode);
    }

    private static bool IsReleaseNumber(int value)
    {
        return value is 480 or 720 or 1080 or 2160 or 4320
               || value is >= 1900 and <= 2099;
    }

    private static bool TryNormalizeSequenceTitlePrefix(string value, out string prefixKey)
    {
        prefixKey = string.Empty;
        var normalized = CleanSeriesNameCandidate(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var meaningful = string.Join(
            ' ',
            normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !ReleaseMetadataQueryTokenRegex().IsMatch(token)));
        if (string.IsNullOrWhiteSpace(meaningful))
        {
            return false;
        }

        if (!meaningful.Any(IsCjkLetter) && meaningful.Count(char.IsLetter) < 3)
        {
            return false;
        }

        prefixKey = meaningful.ToLowerInvariant();
        return true;
    }

    private static bool IsTitleLikeSequencePattern(string pattern)
    {
        return string.Equals(pattern, "title-number", StringComparison.OrdinalIgnoreCase)
               || string.Equals(pattern, "title-part-number", StringComparison.OrdinalIgnoreCase)
               || string.Equals(pattern, "bracket-episode-segment", StringComparison.OrdinalIgnoreCase)
               || string.Equals(pattern, "fansub-bracket-episode", StringComparison.OrdinalIgnoreCase)
               || string.Equals(pattern, "leading-number-title", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryBuildStrictSequence(
        IReadOnlyList<TvEpisodeSequencePattern> patterns,
        out TvEpisodeSequenceAnalysis sequence)
    {
        sequence = TvEpisodeSequenceAnalysis.Empty;
        foreach (var group in patterns.GroupBy(x => x.PatternKey, StringComparer.OrdinalIgnoreCase))
        {
            var distinctByNumber = group
                .GroupBy(x => x.Number)
                .Select(x => x.First())
                .OrderBy(x => x.Number)
                .ToArray();
            var current = new List<TvEpisodeSequencePattern>();
            foreach (var item in distinctByNumber)
            {
                if (current.Count == 0 || item.Number == current[^1].Number + 1)
                {
                    current.Add(item);
                    continue;
                }

                if (TryBuildSequenceFromRun(current, out sequence))
                {
                    return true;
                }

                current = [item];
            }

            if (TryBuildSequenceFromRun(current, out sequence))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildSequenceFromRun(
        IReadOnlyList<TvEpisodeSequencePattern> run,
        out TvEpisodeSequenceAnalysis sequence)
    {
        sequence = TvEpisodeSequenceAnalysis.Empty;
        if (run.Count < 3)
        {
            return false;
        }

        sequence = new TvEpisodeSequenceAnalysis(
            true,
            run[0].PatternKey,
            run[0].Pattern,
            run[0].PrefixKey,
            run[0].Number,
            run[^1].Number,
            run.Count);
        return true;
    }

    private readonly record struct EpisodeMatch(Match Match, int SeasonNumber, int EpisodeNumber, string Kind, int? MultiEpisodeEndNumber);

    private readonly record struct BracketPart(string Value, int Index, int Length);

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,4})\s*(?:-|~|to)\s*(?:[Ee])?(?<endEpisode>\d{1,3})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiEpisodeTokenRegex();

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,4})[\s._-]*[Ee](?<endEpisode>\d{1,4})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiEpisodeCompactRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<season>[0-9一二三四五六七八九十两]{1,4})?\s*\u5b63?\s*\u7b2c?\s*(?<episode>\d{1,4})\s*(?:-|~|\u81f3|\u5230)\s*(?:\u7b2c)?(?<endEpisode>\d{1,3})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseMultiEpisodeRegex();

    [GeneratedRegex(@"\b[Ss](?<season>\d{1,2})[\s._-]*[Ee](?<episode>\d{1,4})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitSeasonEpisodeRegex();

    [GeneratedRegex(@"\b(?<season>\d{1,2})x(?<episode>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex XEpisodeRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<season>[0-9一二三四五六七八九十两]{1,4})\s*\u5b63\s*\u7b2c?\s*(?<episode>\d{1,4})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseSeasonEpisodeRegex();

    [GeneratedRegex(@"\bSeason\s*(?<season>\d{1,2})\s*Episode\s*(?<episode>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishSeasonEpisodeRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:E|EP)(?<episode>\d{1,4})(?:$|[\s._\-\]\)])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContextEpisodeRegex();

    [GeneratedRegex(@"\bEpisode\s*(?<episode>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishContextEpisodeRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<episode>\d{1,4})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseContextEpisodeRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,3})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberEpisodeRegex();

    [GeneratedRegex(@"^(?=.*[\p{L}\u4e00-\u9fff])(?<title>.+?)(?:[\s._-]+)?(?<episode>\d{1,4})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TitleNumberEpisodeRegex();

    [GeneratedRegex(@"^(?=.*[\p{L}\u4e00-\u9fff])(?<title>.+?)\s+(?:[Ss](?<season>\d{1,2})|Season\s*(?<season>\d{1,2}))\s+(?:Pt|Part)\s*(?<part>\d{1,2})\s+(?<episode>\d{1,4})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleSeasonPartEpisodeSequenceRegex();

    [GeneratedRegex(@"^(?=.*[\p{L}\u4e00-\u9fff])(?<title>.+?)\s+(?:Pt|Part)\s*(?<part>\d{1,2})\s+(?<episode>\d{1,4})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitlePartEpisodeSequenceRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,4})[\s._-]+(?<title>.+?)[\s._-]+(?:[Ss](?<season>\d{1,2})|Season[\s._-]*(?<season>\d{1,2}))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumberTitleSequenceRegex();

    [GeneratedRegex(@"\b(?:4K|8K|1080P|2160P|720P|480P|UHD|FHD|HDR|HDR10|DV|WEB[-\s]?DL|WEBRIP|BDRIP|BLURAY|HEVC|H\.?265|H\.?264|X264|X265|AAC|AC3|EAC3|DDP\d?|DTS|TRUEHD|ATMOS|FLAC|REMUX|10BIT|8BIT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleNumberReleaseTailRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:cd|disc|disk|part|sample|trailer|teaser|preview|extras?|bonus|featurette)\s*\d*(?:$|[\s._\-\]\)])|\u82b1\u7d6e|\u9884\u544a|\u7279\u5178|\u5e55\u540e|\u8bbf\u8c08|\u6837\u7247|\u7247\u6bb5|(?:^|[\s._\-\[\(])[\u4e0a\u4e0b](?:$|[\s._\-\]\)])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExcludedEpisodeSequenceTokenRegex();

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

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}|\u3010[^\u3011]+\u3011|\uFF08[^\uFF09]+\uFF09", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedContentRegex();

    [GeneratedRegex(@"^(?<base>.+?)\s*(?:\((?<copy>[1-9]\d{0,2})\)|\uFF08(?<copy>[1-9]\d{0,2})\uFF09)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex DuplicateCopySuffixRegex();

    [GeneratedRegex(@"^\s*(?:E|EP|Episode\s*)?(?<episode>\d{1,4})\s*(?:[-:：].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketEpisodeSegmentRegex();

    [GeneratedRegex(@"^\s*(?:E|EP|Episode)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketEpisodeExplicitMarkerRegex();

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

    [GeneratedRegex(@"^(?:part|pt)(?:\d{1,2})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StructuralPartTokenRegex();

    [GeneratedRegex(@"^(?:s(?:\d{1,2})?|season(?:\d{1,2})?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StructuralSeasonTokenRegex();
}

public sealed record TvEpisodeSequencePattern(
    string PatternKey,
    string Pattern,
    string PrefixKey,
    int Number,
    int? SeasonNumber = null,
    int? PartNumber = null,
    int? EpisodeInPart = null,
    bool PartHintDetected = false);

public sealed record TvEpisodeSequenceAnalysis(
    bool IsSequence,
    string PatternKey,
    string Pattern,
    string PrefixKey,
    int StartNumber,
    int EndNumber,
    int FileCount)
{
    public static TvEpisodeSequenceAnalysis Empty { get; } =
        new(false, string.Empty, string.Empty, string.Empty, 0, 0, 0);
}
