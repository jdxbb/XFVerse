namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvScanDirectoryAnalysisResult
{
    private readonly Dictionary<int, TvScanFileHint> _fileHints = [];
    private readonly List<TvScanAiCandidateRange> _aiCandidateRanges = [];

    public bool AiAttempted { get; set; }

    public bool AiSucceeded { get; set; }

    public string Message { get; set; } = string.Empty;

    public IReadOnlyDictionary<int, TvScanFileHint> FileHints => _fileHints;

    public IReadOnlyList<TvScanAiCandidateRange> AiCandidateRanges => _aiCandidateRanges;

    public int AiCandidateRangeMergedCount { get; private set; }

    public int AiCandidateRangeDeduplicatedEntryCount { get; private set; }

    public IReadOnlyCollection<int> StrongTvMediaFileIds => _fileHints
        .Where(x => x.Value.IsStrongTvContext)
        .Select(x => x.Key)
        .ToArray();

    public IReadOnlyCollection<int> MovieFallbackBlockedMediaFileIds => _fileHints
        .Where(x => x.Value.BlocksMovieFallback)
        .Select(x => x.Key)
        .ToArray();

    public void AddOrUpdateHint(TvScanFileHint hint)
    {
        if (hint.MediaFileId <= 0)
        {
            return;
        }

        if (_fileHints.TryGetValue(hint.MediaFileId, out var existing))
        {
            _fileHints[hint.MediaFileId] = new TvScanFileHint
            {
                MediaFileId = hint.MediaFileId,
                SeriesTitleHint = FirstNonEmpty(hint.SeriesTitleHint, existing.SeriesTitleHint),
                LocalizedTitleHint = FirstNonEmpty(hint.LocalizedTitleHint, existing.LocalizedTitleHint),
                OriginalTitleHint = FirstNonEmpty(hint.OriginalTitleHint, existing.OriginalTitleHint),
                OriginalLanguageTitle = FirstNonEmpty(hint.OriginalLanguageTitle, existing.OriginalLanguageTitle),
                EnglishTitleHint = FirstNonEmpty(hint.EnglishTitleHint, existing.EnglishTitleHint),
                SearchTitle = FirstNonEmpty(hint.SearchTitle, existing.SearchTitle),
                SearchTitleSource = FirstNonEmpty(hint.SearchTitleSource, existing.SearchTitleSource),
                YearHint = hint.YearHint ?? existing.YearHint,
                SeriesYearHint = hint.SeriesYearHint ?? existing.SeriesYearHint,
                SeasonYearHint = hint.SeasonYearHint ?? existing.SeasonYearHint,
                SeasonNumberHint = hint.SeasonNumberHint ?? existing.SeasonNumberHint,
                EpisodeNumberHint = hint.EpisodeNumberHint ?? existing.EpisodeNumberHint,
                Confidence = FirstNonEmpty(hint.Confidence, existing.Confidence),
                Source = FirstNonEmpty(hint.Source, existing.Source),
                Reason = FirstNonEmpty(hint.Reason, existing.Reason),
                Evidence = FirstNonEmpty(hint.Evidence, existing.Evidence),
                IsStrongTvContext = existing.IsStrongTvContext || hint.IsStrongTvContext,
                BlocksMovieFallback = existing.BlocksMovieFallback || hint.BlocksMovieFallback
            };
            return;
        }

        _fileHints[hint.MediaFileId] = hint;
    }

    public TvScanFileHint? GetHint(int mediaFileId)
    {
        return _fileHints.TryGetValue(mediaFileId, out var hint) ? hint : null;
    }

    public bool IsStrongTvFile(int mediaFileId)
    {
        return GetHint(mediaFileId)?.IsStrongTvContext == true;
    }

    public bool BlocksMovieFallback(int mediaFileId)
    {
        return GetHint(mediaFileId)?.BlocksMovieFallback == true;
    }

    public void AddAiCandidateRange(TvScanAiCandidateRange range)
    {
        if (string.IsNullOrWhiteSpace(range.SanitizedPath))
        {
            return;
        }

        var existingIndex = _aiCandidateRanges.FindIndex(
            x => string.Equals(x.SanitizedPath, range.SanitizedPath, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            AiCandidateRangeMergedCount++;
            AiCandidateRangeDeduplicatedEntryCount++;
            var existing = _aiCandidateRanges[existingIndex];
            _aiCandidateRanges[existingIndex] = new TvScanAiCandidateRange
            {
                SanitizedPath = existing.SanitizedPath,
                RangeType = MergeRangeType(existing.RangeType, range.RangeType),
                RiskTags = MergeDistinct(existing.RiskTags, range.RiskTags),
                SourceFileCount = Math.Max(existing.SourceFileCount, range.SourceFileCount),
                DirectVideoCount = Math.Max(existing.DirectVideoCount, range.DirectVideoCount),
                ChildFolderCount = Math.Max(existing.ChildFolderCount, range.ChildFolderCount),
                SampleDirectVideoFiles = MergeDistinct(existing.SampleDirectVideoFiles, range.SampleDirectVideoFiles, maxCount: 8),
                SuspectedSeriesFolder = FirstNonEmpty(existing.SuspectedSeriesFolder, range.SuspectedSeriesFolder),
                SuspectedSeasonFolder = FirstNonEmpty(existing.SuspectedSeasonFolder, range.SuspectedSeasonFolder),
                CandidateQueries = MergeDistinct(existing.CandidateQueries, range.CandidateQueries, maxCount: 8),
                UsableCandidateQueries = MergeDistinct(existing.UsableCandidateQueries, range.UsableCandidateQueries, maxCount: 8),
                RejectedCandidateQueries = MergeDistinct(existing.RejectedCandidateQueries, range.RejectedCandidateQueries, maxCount: 8),
                NoisyCandidateQueries = MergeDistinct(existing.NoisyCandidateQueries, range.NoisyCandidateQueries, maxCount: 8),
                BlockedMovieFallbackCount = Math.Max(existing.BlockedMovieFallbackCount, range.BlockedMovieFallbackCount),
                CandidateConflictsCount = Math.Max(existing.CandidateConflictsCount, range.CandidateConflictsCount),
                ChineseStructureHints = MergeDistinct(existing.ChineseStructureHints, range.ChineseStructureHints),
                MediaFileIds = MergeDistinct(existing.MediaFileIds, range.MediaFileIds)
            };
            return;
        }

        _aiCandidateRanges.Add(range);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> MergeDistinct(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        int maxCount = 24)
    {
        return left
            .Concat(right)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToArray();
    }

    private static IReadOnlyList<int> MergeDistinct(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        return left
            .Concat(right)
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    private static string MergeRangeType(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return left;
        }

        if (string.Equals(left, "candidate-conflict", StringComparison.OrdinalIgnoreCase)
            || string.Equals(right, "candidate-conflict", StringComparison.OrdinalIgnoreCase))
        {
            return "candidate-conflict";
        }

        if (string.Equals(left, "placeholder-needed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(right, "placeholder-needed", StringComparison.OrdinalIgnoreCase))
        {
            return "placeholder-needed";
        }

        return "tv-like-uncertain";
    }
}

public sealed class TvScanFileHint
{
    public int MediaFileId { get; set; }

    public string SeriesTitleHint { get; set; } = string.Empty;

    public string LocalizedTitleHint { get; set; } = string.Empty;

    public string OriginalTitleHint { get; set; } = string.Empty;

    public string OriginalLanguageTitle { get; set; } = string.Empty;

    public string EnglishTitleHint { get; set; } = string.Empty;

    public string SearchTitle { get; set; } = string.Empty;

    public string SearchTitleSource { get; set; } = string.Empty;

    public int? YearHint { get; set; }

    public int? SeriesYearHint { get; set; }

    public int? SeasonYearHint { get; set; }

    public int? SeasonNumberHint { get; set; }

    public int? EpisodeNumberHint { get; set; }

    public string Confidence { get; set; } = "medium";

    public string Source { get; set; } = "local";

    public string Reason { get; set; } = string.Empty;

    public string Evidence { get; set; } = string.Empty;

    public bool IsStrongTvContext { get; set; }

    public bool BlocksMovieFallback { get; set; }
}

public sealed class TvScanAiCandidateRange
{
    public string SanitizedPath { get; set; } = string.Empty;

    public string RangeType { get; set; } = "tv-like-uncertain";

    public IReadOnlyList<string> RiskTags { get; set; } = [];

    public int SourceFileCount { get; set; }

    public int DirectVideoCount { get; set; }

    public int ChildFolderCount { get; set; }

    public IReadOnlyList<string> SampleDirectVideoFiles { get; set; } = [];

    public string SuspectedSeriesFolder { get; set; } = string.Empty;

    public string SuspectedSeasonFolder { get; set; } = string.Empty;

    public IReadOnlyList<string> CandidateQueries { get; set; } = [];

    public IReadOnlyList<string> UsableCandidateQueries { get; set; } = [];

    public IReadOnlyList<string> RejectedCandidateQueries { get; set; } = [];

    public IReadOnlyList<string> NoisyCandidateQueries { get; set; } = [];

    public int BlockedMovieFallbackCount { get; set; }

    public int CandidateConflictsCount { get; set; }

    public IReadOnlyList<string> ChineseStructureHints { get; set; } = [];

    public IReadOnlyList<int> MediaFileIds { get; set; } = [];
}
