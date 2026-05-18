using System.Globalization;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TvSeasonIdentificationService : ITvSeasonIdentificationService
{
    private const double MinimumAutoMatchConfidence = 0.55d;
    private const double MatchedConfidence = 0.80d;
    private const string UnidentifiedSeasonTitle = "未识别电视剧季";

    private readonly ISettingsService _settingsService;
    private readonly ITmdbService _tmdbService;
    private readonly ITvMetadataHydrationService _metadataHydrationService;

    public TvSeasonIdentificationService(
        ISettingsService settingsService,
        ITmdbService tmdbService,
        ITvMetadataHydrationService metadataHydrationService)
    {
        _settingsService = settingsService;
        _tmdbService = tmdbService;
        _metadataHydrationService = metadataHydrationService;
    }

    public async Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        return await IdentifyMediaFilesAsync(mediaFileIds, directoryAnalysis: null, tmdbSearchCache: null, cancellationToken);
    }

    public async Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult? directoryAnalysis,
        CancellationToken cancellationToken = default)
    {
        return await IdentifyMediaFilesAsync(mediaFileIds, directoryAnalysis, tmdbSearchCache: null, cancellationToken);
    }

    public async Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult? directoryAnalysis,
        ScanTmdbSearchCache? tmdbSearchCache,
        CancellationToken cancellationToken = default)
    {
        var result = new TvSeasonIdentificationRunResult();
        var distinctIds = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        ScanIdentificationDiagnostics.Write($"event=tv-identify-start requested={distinctIds.Length}");
        if (distinctIds.Length == 0)
        {
            ScanIdentificationDiagnostics.Write("event=tv-identify-complete requested=0 reason=no-media-file-ids");
            return result;
        }

        var candidates = await BuildCandidatesAsync(distinctIds, directoryAnalysis, cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=tv-identify-candidates requested={distinctIds.Length} candidateCount={candidates.Count}");
        if (candidates.Count == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-identify-complete requested={distinctIds.Length} candidateCount=0 reason=no-tv-candidates");
            return result;
        }

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        var hasTmdbCredential = !string.IsNullOrWhiteSpace(settings.TmdbReadAccessToken)
                                || !string.IsNullOrWhiteSpace(settings.TmdbApiKey);

        foreach (var candidate in candidates)
        {
            result.Summary.AttemptedCount++;
            result.AddHandledMediaFiles(candidate.Files.Select(x => x.MediaFileId));
            result.AddHandledMediaFiles(candidate.UnsupportedFiles.Select(x => x.MediaFileId));
            ScanIdentificationDiagnostics.Write(
                $"event=tv-candidate-start directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} folder={ScanIdentificationDiagnostics.FormatValue(candidate.FolderName)} candidate={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateName)} commonPrefix={ScanIdentificationDiagnostics.FormatValue(candidate.CommonPrefix)} season={candidate.SeasonNumber} files={candidate.Files.Count} unsupported={candidate.UnsupportedFiles.Count} candidateSource={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateSource)} strongTvContext={candidate.IsStrongTvContext.ToString().ToLowerInvariant()} strongTvEvidenceCount={candidate.StrongTvEvidence.Count} strongTvEvidence={ScanIdentificationDiagnostics.FormatValue(candidate.StrongTvEvidenceText)} weakTvContextReason={ScanIdentificationDiagnostics.FormatValue(candidate.WeakTvReasonText)}");

            if (candidate.UnsupportedFiles.Count > 0)
            {
                var hasMultiEpisodeUnsupported = candidate.UnsupportedFiles.Any(x => x.ParseResult.IsMultiEpisode);
                var hasVerifiedTitleNumberContext = candidate.UnsupportedFiles.Any(x => x.ParseResult.VerifiedTitleNumberSequenceContext);
                var unsupportedReason = hasMultiEpisodeUnsupported
                    ? "multi-episode-not-supported"
                    : hasVerifiedTitleNumberContext
                        ? "title-number-sequence-parse-failed"
                        : candidate.WeakTvReasons.Any(x => string.Equals(x, "title-number-sequence", StringComparison.OrdinalIgnoreCase))
                            ? "title-number-sequence-not-applied"
                            : "episode-parse-failed";
                var unsupportedSamples = string.Join(
                    '|',
                    candidate.UnsupportedFiles
                        .Take(5)
                        .Select(x => ScanIdentificationDiagnostics.FormatFileName(x.FileName)));
                var unsupportedKinds = string.Join(
                    '|',
                    candidate.UnsupportedFiles
                        .Select(x => x.ParseResult.MatchKind)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(5));
                var unsupportedPatterns = string.Join(
                    '|',
                    candidate.UnsupportedFiles
                        .Select(x => x.ParseResult.MultiEpisodePattern)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(5));
                var detectedStarts = string.Join(
                    '|',
                    candidate.UnsupportedFiles
                        .Where(x => x.ParseResult.IsMultiEpisode)
                        .Select(x => x.ParseResult.EpisodeNumber)
                        .Where(x => x > 0)
                        .Distinct()
                        .OrderBy(x => x)
                        .Take(5));
                var detectedEnds = string.Join(
                    '|',
                    candidate.UnsupportedFiles
                        .Where(x => x.ParseResult.IsMultiEpisode)
                        .Select(x => x.ParseResult.MultiEpisodeEndNumber)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .Distinct()
                        .OrderBy(x => x)
                        .Take(5));
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-candidate-unsupported directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} season={candidate.SeasonNumber} unsupported={candidate.UnsupportedFiles.Count} reason={unsupportedReason} unsupportedReason={unsupportedReason} unsupportedSampleNames={ScanIdentificationDiagnostics.FormatValue(unsupportedSamples)} unsupportedMatchKind={ScanIdentificationDiagnostics.FormatValue(unsupportedKinds)} detectedMultiEpisodeStart={ScanIdentificationDiagnostics.FormatValue(detectedStarts)} detectedMultiEpisodeEnd={ScanIdentificationDiagnostics.FormatValue(detectedEnds)} detectedMultiEpisodePattern={ScanIdentificationDiagnostics.FormatValue(unsupportedPatterns)} verifiedTitleNumberSequenceContext={hasVerifiedTitleNumberContext.ToString().ToLowerInvariant()}");
                result.Summary.AddWarning(
                    "TV.Parse",
                    hasMultiEpisodeUnsupported
                        ? "multi-episode-not-supported"
                        : "\u90e8\u5206\u5267\u96c6\u6587\u4ef6\u6682\u65e0\u6cd5\u89e3\u6790\uff0c\u5df2\u4fdd\u7559\u4e3a\u672a\u8bc6\u522b/\u5f85\u4fee\u6b63\u3002");
            }

            if (candidate.Files.Count == 0)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-candidate-skip directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} season={candidate.SeasonNumber} reason=no-supported-files");
                if (candidate.StrongTvEvidence.Count > 0 || candidate.WeakTvReasons.Count > 0)
                {
                    LogAiCandidateRange(
                        directoryAnalysis,
                        candidate,
                        "tv-like-uncertain",
                        candidate.WeakTvReasons.Count > 0 ? candidate.WeakTvReasons : candidate.StrongTvEvidence,
                        candidateConflictsCount: 0);
                }

                if (candidate.IsStrongTvContext)
                {
                    await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                    result.Summary.PlaceholderCount++;
                    ScanIdentificationDiagnostics.Write(
                        $"event=tv-candidate-placeholder directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} candidate={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateName)} season={candidate.SeasonNumber} reason=strong-tv-context-no-supported-files rejectReason=tv-context-no-movie-fallback");
                }

                continue;
            }

            if (!hasTmdbCredential || candidate.SearchQueries.Count == 0)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-candidate-placeholder directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} candidate={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateName)} season={candidate.SeasonNumber} reason={(hasTmdbCredential ? "no-usable-query" : "missing-tmdb-credential")} rejectReason={(hasTmdbCredential ? "generic-or-quality-only-query" : "missing-tmdb-credential")}");
                LogAiCandidateRange(
                    directoryAnalysis,
                    candidate,
                    hasTmdbCredential ? "placeholder-needed" : "missing-tmdb-credential",
                    hasTmdbCredential ? ["generic-or-quality-only-query"] : ["missing-tmdb-credential"],
                    candidateConflictsCount: 0);
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                if (!hasTmdbCredential)
                {
                    result.Summary.AddWarning("TMDB.Auth", "TMDB 认证未配置，电视剧季已保留为未识别。");
                }

                continue;
            }

            TvSearchCandidate? bestCandidate = null;
            var candidateConflictCount = 0;
            var candidateConflictReasons = new List<string>();
            var aiRefinedYearGateBlockedCandidate = false;
            var aiRefinedYearGateBlockedReason = string.Empty;
            foreach (var queryAttempt in candidate.SearchQueries)
            {
                var query = queryAttempt.Value;
                TmdbTvSeriesSearchPage searchPage;
                try
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=tv-search-start query={ScanIdentificationDiagnostics.FormatValue(query)} querySource={ScanIdentificationDiagnostics.FormatValue(queryAttempt.Source)} season={candidate.SeasonNumber} files={candidate.Files.Count} candidateSource={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateSource)}");
                    searchPage = await SearchTvSeriesAsync(query, 1, "zh-CN", tmdbSearchCache, cancellationToken);
                }
                catch (Exception exception)
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=tv-search-error query={ScanIdentificationDiagnostics.FormatValue(query)} season={candidate.SeasonNumber} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
                    result.Summary.AddError("TV.Search", TrimMessage(exception.Message));
                    continue;
                }

                var queryCandidates = searchPage.Results
                    .Select(item => new TvSearchCandidate(
                        item,
                        CalculateSeriesConfidence(query, item),
                        query,
                        queryAttempt.Source,
                        queryAttempt.LocalizedTitleHint,
                        queryAttempt.OriginalTitleHint,
                        queryAttempt.YearHint,
                        queryAttempt.SeriesYearHint,
                        queryAttempt.SeasonYearHint,
                        queryAttempt.SeasonNumberHint))
                    .OrderByDescending(x => x.Confidence)
                    .ToList();
                var queryBestCandidate = queryCandidates.FirstOrDefault();
                var nextBestCandidate = queryCandidates.Skip(1).FirstOrDefault();
                var conflictReason = GetCandidateConflictReason(queryAttempt, queryBestCandidate, nextBestCandidate);
                var localizedTitleExactMatch = HasLocalizedTitleExactMatch(query, queryBestCandidate?.Item);
                var originalTitleConflict = HasOriginalTitleConflict(query, queryBestCandidate?.Item);
                var isAiRefinedLookup = IsAiRefinedTitleQuery(queryAttempt.Source);
                var aiRefinedYearGate = isAiRefinedLookup
                    ? GetAiRefinedYearGateResult(queryAttempt, queryBestCandidate)
                    : AiRefinedYearGateResult.NotChecked("not-ai-refined-lookup");
                var aiRefinedTop1Accepted = isAiRefinedLookup
                                               && queryBestCandidate is not null
                                               && !aiRefinedYearGate.Blocked;
                var aiRefinedTop1RejectedReason = isAiRefinedLookup && queryBestCandidate is null
                    ? "no-tmdb-result"
                    : aiRefinedYearGate.Blocked
                        ? aiRefinedYearGate.Reason
                    : string.Empty;
                var aiRefinedSafetyGateReason = isAiRefinedLookup
                    ? GetAiRefinedSafetyGateReason(queryAttempt, queryBestCandidate, nextBestCandidate)
                    : string.Empty;
                var aiRefinedSafetyGatePassed = aiRefinedTop1Accepted;
                var autoApply = queryBestCandidate is not null
                                && string.IsNullOrWhiteSpace(conflictReason)
                                && (isAiRefinedLookup
                                    ? aiRefinedTop1Accepted
                                    : queryBestCandidate.Confidence >= MatchedConfidence);
                var autoApplyBlockedReason = autoApply
                    ? string.Empty
                    : isAiRefinedLookup
                        ? FirstNonEmpty(aiRefinedTop1RejectedReason, aiRefinedSafetyGateReason, "no-tmdb-result")
                        : GetTvAutoApplyBlockedReason(queryBestCandidate, conflictReason);
                var searchDecision = isAiRefinedLookup
                    ? aiRefinedTop1Accepted
                        ? "match-ai-refined-top1"
                        : aiRefinedYearGate.Blocked
                            ? "placeholder-year-conflict"
                            : "placeholder-no-result"
                    : GetTvSearchDecision(queryBestCandidate, conflictReason);

                ScanIdentificationDiagnostics.Write(
                    $"event=tv-search-complete query={ScanIdentificationDiagnostics.FormatValue(query)} querySource={ScanIdentificationDiagnostics.FormatValue(queryAttempt.Source)} resultCount={searchPage.Results.Count} topTitle={ScanIdentificationDiagnostics.FormatValue(queryBestCandidate?.Item.Name)} topOriginal={ScanIdentificationDiagnostics.FormatValue(queryBestCandidate?.Item.OriginalName)} topTmdbId={ScanIdentificationDiagnostics.FormatNullable(queryBestCandidate?.Item.TmdbId)} topConfidence={ScanIdentificationDiagnostics.FormatConfidence(queryBestCandidate?.Confidence)} secondConfidence={ScanIdentificationDiagnostics.FormatConfidence(nextBestCandidate?.Confidence)} tvLocalizedTitleExactMatch={localizedTitleExactMatch.ToString().ToLowerInvariant()} tvOriginalTitleConflict={originalTitleConflict.ToString().ToLowerInvariant()} tvCandidateConflictReason={ScanIdentificationDiagnostics.FormatValue(conflictReason)} conflictReason={ScanIdentificationDiagnostics.FormatValue(conflictReason)} tvAutoApply={autoApply.ToString().ToLowerInvariant()} tvAutoApplyBlockedReason={ScanIdentificationDiagnostics.FormatValue(autoApplyBlockedReason)} decision={ScanIdentificationDiagnostics.FormatValue(searchDecision)} aiRefinedLookup={isAiRefinedLookup.ToString().ToLowerInvariant()} aiRefinedTitleParsed={isAiRefinedLookup.ToString().ToLowerInvariant()} aiRefinedTitle={ScanIdentificationDiagnostics.FormatValue(isAiRefinedLookup ? query : string.Empty)} aiOriginalLanguageTitle={ScanIdentificationDiagnostics.FormatValue(queryAttempt.OriginalLanguageTitle)} aiEnglishTitleHint={ScanIdentificationDiagnostics.FormatValue(queryAttempt.EnglishTitleHint)} aiLocalizedTitleHint={ScanIdentificationDiagnostics.FormatValue(queryAttempt.LocalizedTitleHint)} aiOriginalTitleHint={ScanIdentificationDiagnostics.FormatValue(queryAttempt.OriginalTitleHint)} aiSearchTitle={ScanIdentificationDiagnostics.FormatValue(isAiRefinedLookup ? queryAttempt.SearchTitle : string.Empty)} aiSearchTitleSource={ScanIdentificationDiagnostics.FormatValue(isAiRefinedLookup ? queryAttempt.SearchTitleSource : string.Empty)} originalLanguageTitleMissing={(isAiRefinedLookup && string.IsNullOrWhiteSpace(queryAttempt.OriginalLanguageTitle)).ToString().ToLowerInvariant()} fallbackToEnglishTitle={(isAiRefinedLookup && string.Equals(queryAttempt.SearchTitleSource, "english-title", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} fallbackToLocalizedTitle={(isAiRefinedLookup && string.Equals(queryAttempt.SearchTitleSource, "localized-title", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} aiSeriesYearHint={ScanIdentificationDiagnostics.FormatNullable(queryAttempt.SeriesYearHint)} aiSeasonYearHint={ScanIdentificationDiagnostics.FormatNullable(queryAttempt.SeasonYearHint)} aiSeasonNumberHint={ScanIdentificationDiagnostics.FormatNullable(queryAttempt.SeasonNumberHint)} aiRefinedTmdbSearchQuery={ScanIdentificationDiagnostics.FormatValue(isAiRefinedLookup ? query : string.Empty)} aiRefinedTmdbSearchQuerySource={ScanIdentificationDiagnostics.FormatValue(isAiRefinedLookup ? queryAttempt.SearchTitleSource : string.Empty)} aiRefinedTmdbTop1Title={ScanIdentificationDiagnostics.FormatValue(queryBestCandidate?.Item.Name)} aiRefinedTmdbTop1OriginalTitle={ScanIdentificationDiagnostics.FormatValue(queryBestCandidate?.Item.OriginalName)} aiRefinedTmdbTop1Year={ScanIdentificationDiagnostics.FormatNullable(queryBestCandidate?.Item.FirstAirYear)} aiRefinedTmdbTop1Id={ScanIdentificationDiagnostics.FormatNullable(queryBestCandidate?.Item.TmdbId)} aiRefinedTmdbResultCount={searchPage.Results.Count} aiRefinedTmdbLookupSucceeded={(isAiRefinedLookup && queryBestCandidate is not null).ToString().ToLowerInvariant()} aiRefinedYearGateChecked={aiRefinedYearGate.Checked.ToString().ToLowerInvariant()} aiRefinedYearDiff={ScanIdentificationDiagnostics.FormatNullable(aiRefinedYearGate.YearDiff)} aiRefinedYearGateBlocked={aiRefinedYearGate.Blocked.ToString().ToLowerInvariant()} aiRefinedYearGateReason={ScanIdentificationDiagnostics.FormatValue(aiRefinedYearGate.Reason)} aiRefinedSafetyGatePassed={aiRefinedSafetyGatePassed.ToString().ToLowerInvariant()} aiRefinedSafetyGateReason={ScanIdentificationDiagnostics.FormatValue(aiRefinedSafetyGateReason)} aiRefinedTop1Accepted={aiRefinedTop1Accepted.ToString().ToLowerInvariant()} aiRefinedTop1RejectedReason={ScanIdentificationDiagnostics.FormatValue(aiRefinedTop1RejectedReason)} aiRefinedAutoApply={(isAiRefinedLookup && autoApply).ToString().ToLowerInvariant()} finalDecisionAfterAiRefinedLookup={ScanIdentificationDiagnostics.FormatValue(isAiRefinedLookup ? searchDecision : string.Empty)}");
                if (aiRefinedYearGate.Blocked)
                {
                    aiRefinedYearGateBlockedCandidate = true;
                    aiRefinedYearGateBlockedReason = aiRefinedYearGate.Reason;
                    break;
                }

                if (!string.IsNullOrWhiteSpace(conflictReason))
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=tv-candidate-conflict query={ScanIdentificationDiagnostics.FormatValue(query)} querySource={ScanIdentificationDiagnostics.FormatValue(queryAttempt.Source)} topTmdbId={ScanIdentificationDiagnostics.FormatNullable(queryBestCandidate?.Item.TmdbId)} topTitle={ScanIdentificationDiagnostics.FormatValue(queryBestCandidate?.Item.Name)} secondTmdbId={ScanIdentificationDiagnostics.FormatNullable(nextBestCandidate?.Item.TmdbId)} secondTitle={ScanIdentificationDiagnostics.FormatValue(nextBestCandidate?.Item.Name)} localizedTitleExactMatch={localizedTitleExactMatch.ToString().ToLowerInvariant()} originalTitleConflict={originalTitleConflict.ToString().ToLowerInvariant()} tvCandidateConflictReason={ScanIdentificationDiagnostics.FormatValue(conflictReason)} conflictReason={ScanIdentificationDiagnostics.FormatValue(conflictReason)} tvAutoApply=false tvAutoApplyBlockedReason={ScanIdentificationDiagnostics.FormatValue(conflictReason)} whyNeedsReview=candidate-conflict");
                    candidateConflictCount++;
                    candidateConflictReasons.Add(conflictReason);
                    continue;
                }

                if (isAiRefinedLookup && aiRefinedTop1Accepted && queryBestCandidate is not null)
                {
                    bestCandidate = queryBestCandidate;
                    break;
                }

                if (queryBestCandidate is not null
                    && (bestCandidate is null || queryBestCandidate.Confidence > bestCandidate.Confidence))
                {
                    bestCandidate = queryBestCandidate;
                }

                if (bestCandidate?.Confidence >= MatchedConfidence)
                {
                    break;
                }
            }

            var bestCandidateIsAiRefinedTop1 = IsAiRefinedTitleQuery(bestCandidate?.QuerySource);
            if (aiRefinedYearGateBlockedCandidate)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-candidate-placeholder directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} candidate={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateName)} season={candidate.SeasonNumber} reason=ai-refined-year-conflict rejectReason={ScanIdentificationDiagnostics.FormatValue(aiRefinedYearGateBlockedReason)} aiRefinedYearGateBlocked=true tvAutoApply=false tvAutoApplyBlockedReason={ScanIdentificationDiagnostics.FormatValue(aiRefinedYearGateBlockedReason)} finalDecision=tv-placeholder");
                LogAiCandidateRange(
                    directoryAnalysis,
                    candidate,
                    "ai-refined-year-conflict",
                    ["ai-refined-year-conflict", aiRefinedYearGateBlockedReason],
                    candidateConflictsCount: candidateConflictCount);
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                continue;
            }

            if (bestCandidate is null || (!bestCandidateIsAiRefinedTop1 && bestCandidate.Confidence < MinimumAutoMatchConfidence))
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-candidate-placeholder directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} candidate={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateName)} season={candidate.SeasonNumber} reason={(bestCandidate is null ? "no-result" : "below-threshold")} rejectReason={(bestCandidate is null ? "query-no-result" : "below-threshold")} tvAutoApply=false tvAutoApplyBlockedReason={ScanIdentificationDiagnostics.FormatValue(GetTvAutoApplyBlockedReason(bestCandidate, string.Empty))} finalDecision=tv-placeholder");
                var rangeType = candidateConflictCount > 0 ? "candidate-conflict" : "placeholder-needed";
                var riskTags = candidateConflictCount > 0
                    ? new[] { "candidate-conflict" }.Concat(candidateConflictReasons).ToArray()
                    : bestCandidate is null ? ["query-no-result"] : ["low-confidence-tv-match"];
                LogAiCandidateRange(
                    directoryAnalysis,
                    candidate,
                    rangeType,
                    riskTags,
                    candidateConflictsCount: candidateConflictCount);
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                continue;
            }

            if (!bestCandidateIsAiRefinedTop1 && bestCandidate.Confidence < MatchedConfidence)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-candidate-placeholder directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} candidate={ScanIdentificationDiagnostics.FormatValue(candidate.CandidateName)} season={candidate.SeasonNumber} topTmdbId={bestCandidate.Item.TmdbId} topTitle={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Item.Name)} topConfidence={ScanIdentificationDiagnostics.FormatConfidence(bestCandidate.Confidence)} reason=needs-review-not-auto-applied rejectReason=needs-review-not-auto-applied tvAutoApply=false tvAutoApplyBlockedReason=needs-review-not-auto-applied finalDecision=tv-placeholder");
                LogAiCandidateRange(
                    directoryAnalysis,
                    candidate,
                    candidateConflictCount > 0 ? "candidate-conflict" : "placeholder-needed",
                    candidateConflictCount > 0
                        ? new[] { "candidate-conflict", "needs-review-not-auto-applied" }.Concat(candidateConflictReasons).ToArray()
                        : ["needs-review-not-auto-applied"],
                    candidateConflictsCount: candidateConflictCount);
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                continue;
            }

            TmdbTvSeriesDetailResult? seriesDetails = null;
            TmdbTvSeasonDetailResult? seasonDetails = null;
            try
            {
                seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                    bestCandidate.Item.TmdbId,
                    cancellationToken: cancellationToken);
                seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
                    bestCandidate.Item.TmdbId,
                    candidate.SeasonNumber,
                    cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-detail-error tmdbId={bestCandidate.Item.TmdbId} season={candidate.SeasonNumber} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
                result.Summary.AddWarning("TV.Detail", TrimMessage(exception.Message));
            }

            try
            {
                await UpsertMatchedSeasonAsync(
                    candidate,
                    bestCandidate.Item,
                    bestCandidate.Confidence,
                    seriesDetails,
                    seasonDetails,
                    IdentificationStatus.Matched,
                    cancellationToken);
                await _metadataHydrationService.HydrateSeriesAsync(
                    bestCandidate.Item.TmdbId,
                    cancellationToken: cancellationToken);
                result.Summary.BoundCount++;
                var matchedByAiRefinedTop1 = IsAiRefinedTitleQuery(bestCandidate.QuerySource);
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-apply-complete tmdbId={bestCandidate.Item.TmdbId} title={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Item.Name)} season={candidate.SeasonNumber} files={candidate.Files.Count} selectedQuery={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Query)} querySource={ScanIdentificationDiagnostics.FormatValue(bestCandidate.QuerySource)} status=success tvAutoApply=true tvAutoApplyBlockedReason=(none) matchedByAiRefinedTop1={matchedByAiRefinedTop1.ToString().ToLowerInvariant()} finalDecisionAfterAiRefinedLookup={ScanIdentificationDiagnostics.FormatValue(matchedByAiRefinedTop1 ? "match-ai-refined-top1" : string.Empty)}");
            }
            catch (Exception exception)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-apply-error tmdbId={bestCandidate.Item.TmdbId} title={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Item.Name)} season={candidate.SeasonNumber} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                result.Summary.AddError("TV.Apply", TrimMessage(exception.Message));
            }
        }

        ScanIdentificationDiagnostics.Write(
            $"event=tv-identify-complete requested={distinctIds.Length} candidates={candidates.Count} handled={result.HandledMediaFileIds.Count} attempted={result.Summary.AttemptedCount} bound={result.Summary.BoundCount} placeholders={result.Summary.PlaceholderCount} warnings={result.Summary.WarningCount} errors={result.Summary.ErrorCount}");
        return result;
    }

    public async Task<int> ApplyManualMediaFileMatchAsync(
        int mediaFileId,
        int seriesTmdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaFileId));
        }

        if (seriesTmdbId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seriesTmdbId));
        }

        if (seasonNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonNumber));
        }

        if (episodeNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(episodeNumber));
        }

        var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                                seriesTmdbId,
                                cancellationToken: cancellationToken)
                            ?? throw new InvalidOperationException("无法读取 TMDB 电视剧详情。");
        var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
                                seriesTmdbId,
                                seasonNumber,
                                cancellationToken: cancellationToken)
                            ?? throw new InvalidOperationException("无法读取 TMDB 电视剧季详情。");
        var episodeMetadata = seasonDetails.Episodes.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var mediaFile = await dbContext.MediaFiles
            .Include(x => x.Movie)
            .Include(x => x.Episode)
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("待修正的播放源不存在。");

        var previousMovieId = mediaFile.MovieId;
        var tvSeries = await UpsertSeriesAsync(dbContext, seriesDetails, null, cancellationToken);
        var tvSeason = await UpsertSeasonAsync(
            dbContext,
            tvSeries,
            seasonNumber,
            1d,
            IdentificationStatus.ManualConfirmed,
            seriesDetails,
            seasonDetails,
            cancellationToken);
        await UpsertSeasonMetadataEpisodesAsync(dbContext, tvSeason, seasonDetails, cancellationToken);
        var tvEpisode = await UpsertEpisodeAsync(
            dbContext,
            tvSeason,
            episodeNumber,
            episodeMetadata,
            null,
            cancellationToken);

        mediaFile.MovieId = null;
        mediaFile.Movie = null;
        mediaFile.EpisodeId = tvEpisode.Id;
        mediaFile.Episode = tvEpisode;
        mediaFile.UpdatedAt = DateTime.UtcNow;

        if (previousMovieId.HasValue)
        {
            await ReconcileMovieAfterSourceMoveAsync(dbContext, previousMovieId.Value, mediaFile.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _metadataHydrationService.HydrateSeriesAsync(seriesTmdbId, force: true, cancellationToken);
        return tvEpisode.Id;
    }

    private async Task<TmdbTvSeriesSearchPage> SearchTvSeriesAsync(
        string query,
        int page,
        string language,
        ScanTmdbSearchCache? tmdbSearchCache,
        CancellationToken cancellationToken)
    {
        if (tmdbSearchCache is not null
            && tmdbSearchCache.TryGetTvSearch(query, page, language, out var cachedResult))
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tmdb-tv-search-cache-hit query={ScanIdentificationDiagnostics.FormatValue(query)} page={page} language={ScanIdentificationDiagnostics.FormatValue(language)} tmdbTvSearchCacheHit={tmdbSearchCache.TvSearchCacheHits} tmdbTvSearchCacheMiss={tmdbSearchCache.TvSearchCacheMisses} tmdbSearchCacheEntries={tmdbSearchCache.TvSearchCacheEntries} duplicateSearchAvoided={tmdbSearchCache.DuplicateSearchAvoided}");
            return cachedResult;
        }

        if (tmdbSearchCache is not null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tmdb-tv-search-cache-miss query={ScanIdentificationDiagnostics.FormatValue(query)} page={page} language={ScanIdentificationDiagnostics.FormatValue(language)} tmdbTvSearchCacheHit={tmdbSearchCache.TvSearchCacheHits} tmdbTvSearchCacheMiss={tmdbSearchCache.TvSearchCacheMisses} tmdbSearchCacheEntries={tmdbSearchCache.TvSearchCacheEntries}");
        }

        var result = await _tmdbService.SearchTvSeriesAsync(
            query,
            page,
            language,
            cancellationToken: cancellationToken);
        tmdbSearchCache?.SetTvSearch(query, page, language, result);
        return result;
    }

    private static void LogAiCandidateRange(
        TvScanDirectoryAnalysisResult? directoryAnalysis,
        TvSeasonCandidate candidate,
        string rangeType,
        IReadOnlyList<string> riskTags,
        int candidateConflictsCount)
    {
        var sampleFiles = candidate.Files
            .Concat(candidate.UnsupportedFiles)
            .Select(x => x.FileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(x => ScanIdentificationDiagnostics.FormatFileName(x))
            .ToArray();
        var usableCandidateQueries = candidate.SearchQueries
            .Select(FormatQueryForDiagnostics)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        var rejectedCandidateQueries = candidate.RejectedSearchQueries
            .Select(FormatQueryForDiagnostics)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        var noisyCandidateQueries = candidate.NoisySearchQueries
            .Select(FormatQueryForDiagnostics)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        var candidateQueries = usableCandidateQueries
            .Concat(noisyCandidateQueries)
            .Concat(rejectedCandidateQueries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        var allRiskTags = riskTags
            .Concat(candidate.StrongTvEvidence)
            .Concat(candidate.WeakTvReasons)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var mediaFileIds = candidate.Files
            .Concat(candidate.UnsupportedFiles)
            .Select(x => x.MediaFileId)
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        var range = new TvScanAiCandidateRange
        {
            SanitizedPath = BuildSanitizedDiagnosticPath(candidate.DirectoryPath),
            RangeType = rangeType,
            RiskTags = allRiskTags,
            SourceFileCount = candidate.Files.Count + candidate.UnsupportedFiles.Count,
            DirectVideoCount = candidate.Files.Count + candidate.UnsupportedFiles.Count,
            ChildFolderCount = 0,
            SampleDirectVideoFiles = sampleFiles.Select(UnquoteDiagnosticValue).ToArray(),
            SuspectedSeriesFolder = BuildSanitizedDiagnosticPath(GetDirectoryPath(candidate.DirectoryPath)),
            SuspectedSeasonFolder = BuildSanitizedDiagnosticPath(candidate.DirectoryPath),
            CandidateQueries = candidateQueries,
            UsableCandidateQueries = usableCandidateQueries,
            RejectedCandidateQueries = rejectedCandidateQueries,
            NoisyCandidateQueries = noisyCandidateQueries,
            BlockedMovieFallbackCount = candidate.WeakTvReasons.Count > 0 || candidate.StrongTvEvidence.Count > 0
                ? candidate.Files.Count + candidate.UnsupportedFiles.Count
                : 0,
            CandidateConflictsCount = candidateConflictsCount,
            ChineseStructureHints = BuildChineseStructureHints(candidate.FolderName, candidate.Files.Concat(candidate.UnsupportedFiles).Select(x => x.FileName)),
            MediaFileIds = mediaFileIds
        };
        directoryAnalysis?.AddAiCandidateRange(range);
        ScanIdentificationDiagnostics.Write(
            $"event=ai-candidate-range directory={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} rangeType={ScanIdentificationDiagnostics.FormatValue(rangeType)} riskTags={ScanIdentificationDiagnostics.FormatValue(string.Join('|', allRiskTags))} sourceFiles={range.SourceFileCount} directVideoCount={range.DirectVideoCount} childFolderCount=0 rangeMediaFileCount={range.MediaFileIds.Count} rangeHasMediaFiles={(range.MediaFileIds.Count > 0).ToString().ToLowerInvariant()} sampleDirectVideoFiles={ScanIdentificationDiagnostics.FormatValue(string.Join('|', sampleFiles))} suspectedSeriesFolder={ScanIdentificationDiagnostics.FormatPath(GetDirectoryPath(candidate.DirectoryPath))} suspectedSeasonFolder={ScanIdentificationDiagnostics.FormatPath(candidate.DirectoryPath)} usableCandidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', usableCandidateQueries))} rejectedCandidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', rejectedCandidateQueries))} noisyCandidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', noisyCandidateQueries))} candidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', candidateQueries))} blockedMovieFallbackCount={range.BlockedMovieFallbackCount} candidateConflictsCount={candidateConflictsCount} chineseStructureHints={ScanIdentificationDiagnostics.FormatValue(string.Join('|', range.ChineseStructureHints))} finalDecision=ai-candidate");
    }

    private static string FormatQueryForDiagnostics(TvSearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Value))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(query.RejectReason)
            ? $"{query.Value} [{query.Source};{query.Quality}]"
            : $"{query.Value} [{query.Source};{query.Quality};{query.RejectReason}]";
    }

    private static string BuildSanitizedDiagnosticPath(string? path)
    {
        return UnquoteDiagnosticValue(ScanIdentificationDiagnostics.FormatPath(path));
    }

    private static string UnquoteDiagnosticValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "(none)", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
    }

    private static IReadOnlyList<string> BuildChineseStructureHints(string folderName, IEnumerable<string> fileNames)
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddChineseStructureHints(folderName, hints);
        foreach (var fileName in fileNames.Take(16))
        {
            AddChineseStructureHints(fileName, hints);
        }

        return hints.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddChineseStructureHints(string value, HashSet<string> hints)
    {
        if (TvEpisodeFileNameParser.HasChineseSeasonHint(value))
        {
            hints.Add("chinese-season-hint");
        }

        if (TvEpisodeFileNameParser.HasChineseEpisodeHint(value))
        {
            hints.Add("chinese-episode-hint");
        }

        if (TvEpisodeFileNameParser.HasChineseCountHint(value))
        {
            hints.Add("chinese-count-hint");
        }
    }

    private async Task<IReadOnlyList<TvSeasonCandidate>> BuildCandidatesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult? directoryAnalysis,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var touchedFiles = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => mediaFileIds.Contains(x.Id)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.MovieId.HasValue)
            .Select(
                x => new CandidateMediaFile
                {
                    Id = x.Id,
                    SourceConnectionId = x.SourceConnectionId,
                    ScanPathId = x.ScanPathId,
                    EpisodeId = x.EpisodeId,
                    FileName = x.FileName,
                    FilePath = x.FilePath
                })
            .ToListAsync(cancellationToken);

        ScanIdentificationDiagnostics.Write(
            $"event=tv-build-touched requested={mediaFileIds.Count} touched={touchedFiles.Count}");
        if (touchedFiles.Count == 0)
        {
            ScanIdentificationDiagnostics.Write("event=tv-build-complete candidateCount=0 reason=no-unbound-video-files");
            return [];
        }

        var touchedDirectoryKeys = touchedFiles
            .Select(x => BuildDirectoryKey(x.SourceConnectionId, GetDirectoryPath(x.FilePath)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceConnectionIds = touchedFiles
            .Select(x => x.SourceConnectionId)
            .Distinct()
            .ToArray();

        var sourceFiles = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => sourceConnectionIds.Contains(x.SourceConnectionId)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.MovieId.HasValue)
            .Select(
                x => new CandidateMediaFile
                {
                    Id = x.Id,
                    SourceConnectionId = x.SourceConnectionId,
                    ScanPathId = x.ScanPathId,
                    EpisodeId = x.EpisodeId,
                    FileName = x.FileName,
                    FilePath = x.FilePath
                })
            .ToListAsync(cancellationToken);

        ScanIdentificationDiagnostics.Write(
            $"event=tv-build-source-context touchedDirectories={touchedDirectoryKeys.Count} sourceFiles={sourceFiles.Count}");
        var candidates = new List<TvSeasonCandidate>();
        foreach (var directoryGroup in sourceFiles
                     .GroupBy(x => new
                     {
                         x.SourceConnectionId,
                         DirectoryPath = GetDirectoryPath(x.FilePath)
                     })
                     .Where(x => touchedDirectoryKeys.Contains(BuildDirectoryKey(x.Key.SourceConnectionId, x.Key.DirectoryPath))))
        {
            var directoryCandidates = BuildCandidatesForDirectory(
                directoryGroup.Key.DirectoryPath,
                directoryGroup.ToList(),
                directoryAnalysis);
            ScanIdentificationDiagnostics.Write(
                $"event=tv-build-directory-complete directory={ScanIdentificationDiagnostics.FormatPath(directoryGroup.Key.DirectoryPath)} files={directoryGroup.Count()} candidates={directoryCandidates.Count}");
            candidates.AddRange(directoryCandidates);
        }

        ScanIdentificationDiagnostics.Write($"event=tv-build-complete candidateCount={candidates.Count}");
        return candidates;
    }

    private static IReadOnlyList<TvSeasonCandidate> BuildCandidatesForDirectory(
        string directoryPath,
        IReadOnlyList<CandidateMediaFile> files,
        TvScanDirectoryAnalysisResult? directoryAnalysis)
    {
        var folderName = GetFolderName(directoryPath);
        var folderSeasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(folderName);
        var directoryContext = AnalyzeDirectoryContext(directoryPath, files, directoryAnalysis);
        var strongDirectoryContext = directoryContext.IsStrong;
        var parsedFiles = files
            .Select(
                file =>
                {
                    var hint = directoryAnalysis?.GetHint(file.Id);
                    var parseResult = TvEpisodeFileNameParser.Parse(
                        file.FileName,
                        allowSeasonContextOnly: true,
                        seasonNumberHint: hint?.SeasonNumberHint ?? folderSeasonNumber,
                        allowStrongContextFallbacks: strongDirectoryContext);
                    if (!parseResult.IsEpisodeLike
                        && TvEpisodeFileNameParser.IsVerifiedTitleNumberSequenceMember(
                            file.FileName,
                            directoryContext.TitleNumberSequence))
                    {
                        parseResult = TvEpisodeFileNameParser.ParseVerifiedTitleNumberSequence(
                            file.FileName,
                            directoryContext.TitleNumberSequence,
                            hint?.SeasonNumberHint ?? folderSeasonNumber);
                    }

                    if (hint?.EpisodeNumberHint is > 0 && !parseResult.IsEpisodeLike)
                    {
                        parseResult = new TvEpisodeFileNameParseResult
                        {
                            IsEpisodeLike = true,
                            IsSeasonContextOnly = true,
                            IsMultiEpisode = false,
                            SeasonNumber = hint.SeasonNumberHint ?? folderSeasonNumber ?? 1,
                            EpisodeNumber = hint.EpisodeNumberHint.Value,
                            SeriesNameCandidate = hint.SeriesTitleHint,
                            MatchKind = "AiRangeEpisodeHint"
                        };
                    }

                    var effectiveSeasonNumber = parseResult.IsSeasonContextOnly && folderSeasonNumber.HasValue
                        ? folderSeasonNumber.Value
                        : parseResult.SeasonNumber;
                    if (hint?.SeasonNumberHint is > 0 && parseResult.IsSeasonContextOnly)
                    {
                        effectiveSeasonNumber = hint.SeasonNumberHint.Value;
                    }

                    return new TvSeasonCandidateFile
                    {
                        MediaFileId = file.Id,
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        EpisodeId = file.EpisodeId,
                        SeasonNumber = effectiveSeasonNumber,
                        ParseResult = parseResult
                    };
                })
            .ToList();

        foreach (var parsedFile in parsedFiles)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-parse directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} folder={ScanIdentificationDiagnostics.FormatValue(folderName)} fileId={parsedFile.MediaFileId} file={ScanIdentificationDiagnostics.FormatFileName(parsedFile.FileName)} directoryRange={(directoryAnalysis?.IsStrongTvFile(parsedFile.MediaFileId) == true).ToString().ToLowerInvariant()} directoryHintSource={ScanIdentificationDiagnostics.FormatValue(directoryAnalysis?.GetHint(parsedFile.MediaFileId)?.Source)} strongContext={strongDirectoryContext.ToString().ToLowerInvariant()} movieFallbackRisk={directoryContext.BlocksMovieFallback.ToString().ToLowerInvariant()} strongTvEvidenceCount={directoryContext.StrongEvidence.Count} strongTvEvidence={ScanIdentificationDiagnostics.FormatValue(directoryContext.EvidenceText)} weakTvContextReason={ScanIdentificationDiagnostics.FormatValue(directoryContext.WeakReasonText)} match={(parsedFile.ParseResult.IsEpisodeLike ? "yes" : "no")} kind={ScanIdentificationDiagnostics.FormatValue(parsedFile.ParseResult.MatchKind)} season={parsedFile.SeasonNumber} episode={parsedFile.ParseResult.EpisodeNumber} seasonContextOnly={parsedFile.ParseResult.IsSeasonContextOnly.ToString().ToLowerInvariant()} multi={parsedFile.ParseResult.IsMultiEpisode.ToString().ToLowerInvariant()} multiEpisodeFalsePositiveAvoided={parsedFile.ParseResult.MultiEpisodeFalsePositiveAvoided.ToString().ToLowerInvariant()} verifiedTitleNumberSequenceContext={parsedFile.ParseResult.VerifiedTitleNumberSequenceContext.ToString().ToLowerInvariant()} seriesCandidate={ScanIdentificationDiagnostics.FormatValue(parsedFile.ParseResult.SeriesNameCandidate)} episodeTitle={ScanIdentificationDiagnostics.FormatValue(parsedFile.ParseResult.EpisodeTitleCandidate)}");
        }

        var unsupportedFiles = parsedFiles
            .Where(x => x.ParseResult.IsMultiEpisode)
            .ToList();
        var validEpisodeFiles = parsedFiles
            .Where(
                x => x.ParseResult.IsEpisodeLike
                     && !x.ParseResult.IsMultiEpisode
                     && x.ParseResult.EpisodeNumber > 0)
            .ToList();

        if (validEpisodeFiles.Count < 2 && !parsedFiles.Any(x => x.EpisodeId.HasValue) && !directoryContext.BlocksMovieFallback)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-directory-rejected directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} folder={ScanIdentificationDiagnostics.FormatValue(folderName)} folderSeason={ScanIdentificationDiagnostics.FormatNullable(folderSeasonNumber)} files={files.Count} validEpisodeFiles={validEpisodeFiles.Count} unsupported={unsupportedFiles.Count} reason=episode-like-count-below-threshold");
            var fallbackSearchQueries = BuildSearchQueries(directoryPath, folderName, validEpisodeFiles, directoryAnalysis);
            return unsupportedFiles.Count == 0
                ? []
                : [
                    new TvSeasonCandidate
                    {
                        SourceConnectionId = files[0].SourceConnectionId,
                        DirectoryPath = directoryPath,
                        FolderName = folderName,
                        CandidateName = BuildCandidateName(directoryPath, folderName, validEpisodeFiles, directoryAnalysis),
                        CommonPrefix = BuildCommonPrefix(validEpisodeFiles),
                        SeasonNumber = folderSeasonNumber ?? 1,
                        SearchQueries = fallbackSearchQueries.Usable,
                        RejectedSearchQueries = fallbackSearchQueries.Rejected,
                        NoisySearchQueries = fallbackSearchQueries.Noisy,
                        UnsupportedFiles = unsupportedFiles
                    }
                ];
        }

        if (validEpisodeFiles.Count == 0 && directoryContext.BlocksMovieFallback)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-directory-rejected directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} folder={ScanIdentificationDiagnostics.FormatValue(folderName)} folderSeason={ScanIdentificationDiagnostics.FormatNullable(folderSeasonNumber)} files={files.Count} validEpisodeFiles=0 unsupported={unsupportedFiles.Count} reason=tv-risk-parser-failed rejectReason=tv-context-no-movie-fallback strongTvEvidence={ScanIdentificationDiagnostics.FormatValue(directoryContext.EvidenceText)} weakTvContextReason={ScanIdentificationDiagnostics.FormatValue(directoryContext.WeakReasonText)} movieFallbackRisk=true finalDecision=ai-candidate");
            var failedSearchQueries = BuildSearchQueries(directoryPath, folderName, validEpisodeFiles, directoryAnalysis);
            return [
                new TvSeasonCandidate
                {
                    SourceConnectionId = files[0].SourceConnectionId,
                    DirectoryPath = directoryPath,
                    FolderName = folderName,
                    CandidateName = BuildCandidateName(directoryPath, folderName, validEpisodeFiles, directoryAnalysis),
                    CommonPrefix = BuildCommonPrefix(validEpisodeFiles),
                    SeasonNumber = folderSeasonNumber ?? 1,
                    SearchQueries = failedSearchQueries.Usable,
                    RejectedSearchQueries = failedSearchQueries.Rejected,
                    NoisySearchQueries = failedSearchQueries.Noisy,
                    IsStrongTvContext = directoryContext.IsStrong,
                    CandidateSource = directoryContext.IsStrong ? "strong-tv-context" : "tv-risk",
                    StrongTvEvidence = directoryContext.StrongEvidence.ToList(),
                    WeakTvReasons = directoryContext.WeakReasons.ToList(),
                    UnsupportedFiles = parsedFiles
                }
            ];
        }

        if (validEpisodeFiles.Count < 2 && !parsedFiles.Any(x => x.EpisodeId.HasValue) && directoryContext.BlocksMovieFallback && !directoryContext.IsStrong)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-directory-rejected directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} folder={ScanIdentificationDiagnostics.FormatValue(folderName)} folderSeason={ScanIdentificationDiagnostics.FormatNullable(folderSeasonNumber)} files={files.Count} validEpisodeFiles={validEpisodeFiles.Count} unsupported={unsupportedFiles.Count} reason=weak-tv-context rejectReason=movie-fallback-blocked-by-tv-risk weakTvContextReason={ScanIdentificationDiagnostics.FormatValue(directoryContext.WeakReasonText)} movieFallbackRisk=true finalDecision=ai-candidate");
            return [
                new TvSeasonCandidate
                {
                    SourceConnectionId = files[0].SourceConnectionId,
                    DirectoryPath = directoryPath,
                    FolderName = folderName,
                    CandidateName = BuildCandidateName(directoryPath, folderName, validEpisodeFiles, directoryAnalysis),
                    CommonPrefix = BuildCommonPrefix(validEpisodeFiles),
                    SeasonNumber = folderSeasonNumber ?? 1,
                    SearchQueries = [],
                    IsStrongTvContext = false,
                    CandidateSource = "weak-tv-context",
                    StrongTvEvidence = directoryContext.StrongEvidence.ToList(),
                    WeakTvReasons = directoryContext.WeakReasons.ToList(),
                    Files = validEpisodeFiles,
                    UnsupportedFiles = parsedFiles.Where(x => !validEpisodeFiles.Any(y => y.MediaFileId == x.MediaFileId)).ToList()
                }
            ];
        }

        var candidates = new List<TvSeasonCandidate>();
        foreach (var seasonGroup in validEpisodeFiles.GroupBy(x => Math.Max(1, x.SeasonNumber)))
        {
            var seasonFiles = seasonGroup
                .GroupBy(x => x.ParseResult.EpisodeNumber)
                .SelectMany(x => x)
                .ToList();
            var seasonNumber = seasonGroup.Key;
            var candidateName = BuildCandidateName(directoryPath, folderName, seasonFiles, directoryAnalysis);
            var searchQueries = BuildSearchQueries(directoryPath, folderName, seasonFiles, directoryAnalysis);
            ScanIdentificationDiagnostics.Write(
                $"event=tv-directory-candidate directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} folder={ScanIdentificationDiagnostics.FormatValue(folderName)} candidate={ScanIdentificationDiagnostics.FormatValue(candidateName)} commonPrefix={ScanIdentificationDiagnostics.FormatValue(BuildCommonPrefix(seasonFiles))} season={seasonNumber} files={seasonFiles.Count} unsupported={unsupportedFiles.Count} strongContext={strongDirectoryContext.ToString().ToLowerInvariant()} movieFallbackRisk={directoryContext.BlocksMovieFallback.ToString().ToLowerInvariant()} strongTvEvidenceCount={directoryContext.StrongEvidence.Count} strongTvEvidence={ScanIdentificationDiagnostics.FormatValue(directoryContext.EvidenceText)} weakTvContextReason={ScanIdentificationDiagnostics.FormatValue(directoryContext.WeakReasonText)} queries={searchQueries.Usable.Count} noisyQueries={searchQueries.Noisy.Count} rejectedQueries={searchQueries.Rejected.Count}");
            candidates.Add(
                new TvSeasonCandidate
                {
                    SourceConnectionId = files[0].SourceConnectionId,
                    DirectoryPath = directoryPath,
                    FolderName = folderName,
                    CandidateName = candidateName,
                    CommonPrefix = BuildCommonPrefix(seasonFiles),
                    SeasonNumber = seasonNumber,
                    SearchQueries = searchQueries.Usable,
                    RejectedSearchQueries = searchQueries.Rejected,
                    NoisySearchQueries = searchQueries.Noisy,
                    IsStrongTvContext = strongDirectoryContext,
                    CandidateSource = directoryContext.IsStrong ? "strong-tv-context" : "local",
                    StrongTvEvidence = directoryContext.StrongEvidence.ToList(),
                    WeakTvReasons = directoryContext.WeakReasons.ToList(),
                    Files = seasonFiles,
                    UnsupportedFiles = unsupportedFiles
                        .Where(x => Math.Max(1, x.SeasonNumber) == seasonNumber || !x.ParseResult.IsEpisodeLike)
                        .ToList()
                });
        }

        return candidates;
    }

    private async Task UpsertMatchedSeasonAsync(
        TvSeasonCandidate candidate,
        TmdbTvSeriesSearchItem searchItem,
        double confidence,
        TmdbTvSeriesDetailResult? seriesDetails,
        TmdbTvSeasonDetailResult? seasonDetails,
        IdentificationStatus status,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tvSeries = await UpsertSeriesAsync(dbContext, seriesDetails, searchItem, cancellationToken);
        var tvSeason = await UpsertSeasonAsync(
            dbContext,
            tvSeries,
            candidate.SeasonNumber,
            confidence,
            status,
            seriesDetails,
            seasonDetails,
            cancellationToken);
        await UpsertSeasonMetadataEpisodesAsync(dbContext, tvSeason, seasonDetails, cancellationToken);

        foreach (var candidateFile in candidate.Files)
        {
            var episodeMetadata = seasonDetails?.Episodes
                .FirstOrDefault(x => x.EpisodeNumber == candidateFile.ParseResult.EpisodeNumber);
            var tvEpisode = await UpsertEpisodeAsync(
                dbContext,
                tvSeason,
                candidateFile.ParseResult.EpisodeNumber,
                episodeMetadata,
                candidateFile,
                cancellationToken);

            await AttachMediaFileToEpisodeAsync(dbContext, candidateFile.MediaFileId, tvEpisode.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpsertUnidentifiedSeasonAsync(
        TvSeasonCandidate candidate,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var seriesName = FirstNonEmpty(candidate.CandidateName, candidate.CommonPrefix, candidate.FolderName, UnidentifiedSeasonTitle);
        var tvSeries = await dbContext.TvSeries
            .FirstOrDefaultAsync(
                x => !x.TmdbSeriesId.HasValue
                     && x.Name == seriesName,
                cancellationToken);
        if (tvSeries is null)
        {
            tvSeries = new TvSeries
            {
                Name = TruncateRequired(seriesName, 300),
                CreatedAt = now
            };
            dbContext.TvSeries.Add(tvSeries);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvSeries.OriginalName = null;
        tvSeries.Overview = null;
        tvSeries.PosterRemoteUrl = null;
        tvSeries.Country = null;
        tvSeries.Language = null;
        tvSeries.FirstAirDate = null;
        tvSeries.FirstAirYear = null;
        tvSeries.GenresText = null;
        tvSeries.UpdatedAt = now;

        var tvSeason = await dbContext.TvSeasons
            .FirstOrDefaultAsync(
                x => x.TvSeriesId == tvSeries.Id
                     && x.SeasonNumber == candidate.SeasonNumber,
                cancellationToken);
        if (tvSeason is null)
        {
            tvSeason = new TvSeason
            {
                TvSeriesId = tvSeries.Id,
                SeasonNumber = candidate.SeasonNumber,
                CreatedAt = now
            };
            dbContext.TvSeasons.Add(tvSeason);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvSeason.TmdbSeasonId = null;
        tvSeason.Name = TruncateRequired($"{UnidentifiedSeasonTitle} S{candidate.SeasonNumber:D2}", 300);
        tvSeason.Overview = null;
        tvSeason.PosterRemoteUrl = null;
        tvSeason.AirDate = null;
        tvSeason.TmdbEpisodeCount = candidate.Files.Count;
        tvSeason.IdentifiedConfidence = null;
        tvSeason.IdentificationStatus = IdentificationStatus.Failed;
        tvSeason.UpdatedAt = now;

        foreach (var candidateFile in candidate.Files)
        {
            var tvEpisode = await UpsertEpisodeAsync(
                dbContext,
                tvSeason,
                candidateFile.ParseResult.EpisodeNumber,
                null,
                candidateFile,
                cancellationToken);
            await AttachMediaFileToEpisodeAsync(dbContext, candidateFile.MediaFileId, tvEpisode.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<TvSeries> UpsertSeriesAsync(
        AppDbContext dbContext,
        TmdbTvSeriesDetailResult? seriesDetails,
        TmdbTvSeriesSearchItem? searchItem,
        CancellationToken cancellationToken)
    {
        var tmdbSeriesId = seriesDetails?.TmdbId ?? searchItem?.TmdbId;
        if (tmdbSeriesId is not > 0)
        {
            throw new InvalidOperationException("TV Series TMDB id 无效。");
        }

        var tvSeries = await dbContext.TvSeries
            .FirstOrDefaultAsync(x => x.TmdbSeriesId == tmdbSeriesId.Value, cancellationToken);
        if (tvSeries is null)
        {
            tvSeries = new TvSeries
            {
                TmdbSeriesId = tmdbSeriesId.Value,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvSeries.Add(tvSeries);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvSeries.Name = TruncateRequired(FirstNonEmpty(seriesDetails?.Name, searchItem?.Name, $"TV {tmdbSeriesId.Value}"), 300);
        tvSeries.OriginalName = Truncate(FirstNonEmpty(seriesDetails?.OriginalName, searchItem?.OriginalName), 300);
        tvSeries.Overview = Truncate(FirstNonEmpty(seriesDetails?.Overview, searchItem?.Overview), 5000);
        tvSeries.PosterRemoteUrl = EmptyToNull(FirstNonEmpty(seriesDetails?.PosterRemoteUrl, searchItem?.PosterRemoteUrl));
        tvSeries.Country = Truncate(string.Join(", ", seriesDetails?.OriginCountries ?? searchItem?.OriginCountries ?? []), 120);
        tvSeries.Language = Truncate(FirstNonEmpty(seriesDetails?.OriginalLanguage, searchItem?.OriginalLanguage), 120);
        tvSeries.FirstAirDate = ParseDate(FirstNonEmpty(seriesDetails?.FirstAirDate, searchItem?.FirstAirDate));
        tvSeries.FirstAirYear = seriesDetails?.FirstAirYear ?? searchItem?.FirstAirYear;
        tvSeries.GenresText = Truncate(seriesDetails?.GenresText ?? string.Empty, 1000);
        tvSeries.UpdatedAt = DateTime.UtcNow;
        return tvSeries;
    }

    private static async Task<TvSeason> UpsertSeasonAsync(
        AppDbContext dbContext,
        TvSeries tvSeries,
        int seasonNumber,
        double confidence,
        IdentificationStatus status,
        TmdbTvSeriesDetailResult? seriesDetails,
        TmdbTvSeasonDetailResult? seasonDetails,
        CancellationToken cancellationToken)
    {
        var tvSeason = await dbContext.TvSeasons
            .FirstOrDefaultAsync(
                x => x.TvSeriesId == tvSeries.Id
                     && x.SeasonNumber == seasonNumber,
                cancellationToken);
        if (tvSeason is null)
        {
            tvSeason = new TvSeason
            {
                TvSeriesId = tvSeries.Id,
                SeasonNumber = seasonNumber,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvSeasons.Add(tvSeason);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = seriesDetails?.Seasons.FirstOrDefault(x => x.SeasonNumber == seasonNumber);
        tvSeason.TmdbSeasonId = PositiveOrNull(seasonDetails?.TmdbId) ?? summary?.TmdbId;
        tvSeason.Name = TruncateRequired(FirstNonEmpty(seasonDetails?.Name, summary?.Name, $"Season {seasonNumber}"), 300);
        tvSeason.Overview = Truncate(FirstNonEmpty(seasonDetails?.Overview, summary?.Overview), 5000);
        tvSeason.PosterRemoteUrl = EmptyToNull(FirstNonEmpty(seasonDetails?.PosterRemoteUrl, summary?.PosterRemoteUrl));
        tvSeason.AirDate = ParseDate(FirstNonEmpty(seasonDetails?.AirDate, summary?.AirDate));
        tvSeason.TmdbEpisodeCount = seasonDetails?.EpisodeCount > 0
            ? seasonDetails.EpisodeCount
            : summary?.EpisodeCount;
        tvSeason.IdentifiedConfidence = confidence;
        tvSeason.IdentificationStatus = status;
        tvSeason.UpdatedAt = DateTime.UtcNow;
        return tvSeason;
    }

    private static async Task UpsertSeasonMetadataEpisodesAsync(
        AppDbContext dbContext,
        TvSeason tvSeason,
        TmdbTvSeasonDetailResult? seasonDetails,
        CancellationToken cancellationToken)
    {
        if (seasonDetails is null || seasonDetails.Episodes.Count == 0)
        {
            return;
        }

        foreach (var metadata in seasonDetails.Episodes
                     .Where(x => x.EpisodeNumber > 0)
                     .GroupBy(x => x.EpisodeNumber)
                     .Select(x => x.OrderByDescending(y => y.TmdbId).First())
                     .OrderBy(x => x.EpisodeNumber))
        {
            await UpsertEpisodeAsync(
                dbContext,
                tvSeason,
                metadata.EpisodeNumber,
                metadata,
                candidateFile: null,
                cancellationToken);
        }
    }

    private static async Task<TvEpisode> UpsertEpisodeAsync(
        AppDbContext dbContext,
        TvSeason tvSeason,
        int episodeNumber,
        TmdbTvEpisodeMetadataItem? metadata,
        TvSeasonCandidateFile? candidateFile,
        CancellationToken cancellationToken)
    {
        var tvEpisode = await dbContext.TvEpisodes
            .FirstOrDefaultAsync(
                x => x.TvSeasonId == tvSeason.Id
                     && x.EpisodeNumber == episodeNumber,
                cancellationToken);
        if (tvEpisode is null)
        {
            tvEpisode = new TvEpisode
            {
                TvSeasonId = tvSeason.Id,
                EpisodeNumber = episodeNumber,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvEpisodes.Add(tvEpisode);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvEpisode.TmdbEpisodeId = PositiveOrNull(metadata?.TmdbId);
        tvEpisode.Title = TruncateRequired(
            FirstNonEmpty(
                metadata?.Name,
                candidateFile?.ParseResult.EpisodeTitleCandidate,
                $"第 {episodeNumber} 集"),
            300);
        tvEpisode.Overview = Truncate(metadata?.Overview ?? string.Empty, 5000);
        tvEpisode.StillRemoteUrl = EmptyToNull(metadata?.StillRemoteUrl);
        tvEpisode.AirDate = ParseDate(metadata?.AirDate ?? string.Empty);
        tvEpisode.RuntimeMinutes = metadata?.RuntimeMinutes;
        tvEpisode.UpdatedAt = DateTime.UtcNow;
        return tvEpisode;
    }

    private static async Task AttachMediaFileToEpisodeAsync(
        AppDbContext dbContext,
        int mediaFileId,
        int episodeId,
        CancellationToken cancellationToken)
    {
        var mediaFile = await dbContext.MediaFiles
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken);
        if (mediaFile is null || mediaFile.MovieId.HasValue)
        {
            return;
        }

        mediaFile.MovieId = null;
        mediaFile.EpisodeId = episodeId;
        mediaFile.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task ReconcileMovieAfterSourceMoveAsync(
        AppDbContext dbContext,
        int movieId,
        int movedMediaFileId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is null || movie.DefaultMediaFileId != movedMediaFileId)
        {
            return;
        }

        movie.DefaultMediaFileId = movie.MediaFiles
            .Where(x => x.Id != movedMediaFileId && x.MediaType == MediaType.Video && !x.IsDeleted)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        movie.UpdatedAt = DateTime.UtcNow;
    }

    private static string BuildCandidateName(
        string directoryPath,
        string folderName,
        IReadOnlyList<TvSeasonCandidateFile> files,
        TvScanDirectoryAnalysisResult? directoryAnalysis)
    {
        return BuildSearchQueryCandidates(directoryPath, folderName, files, directoryAnalysis)
            .Select(x => TvEpisodeFileNameParser.CleanSeriesNameCandidate(x.Value))
            .FirstOrDefault(x => TvEpisodeFileNameParser.IsUsableSeriesSearchQuery(x))
            ?? FirstNonEmpty(
                BuildCommonPrefix(files),
                files.Select(x => x.ParseResult.SeriesNameCandidate).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                TvEpisodeFileNameParser.CleanSeriesNameCandidate(folderName),
                folderName);
    }

    private static TvSearchQuerySet BuildSearchQueries(
        string directoryPath,
        string folderName,
        IReadOnlyList<TvSeasonCandidateFile> files,
        TvScanDirectoryAnalysisResult? directoryAnalysis)
    {
        var rejected = new List<TvSearchQuery>();
        var noisy = new List<TvSearchQuery>();
        var queries = BuildSearchQueryCandidates(directoryPath, folderName, files, directoryAnalysis)
            .Select(x => x with { Value = TvEpisodeFileNameParser.CleanSeriesNameCandidate(x.Value) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Where(
                query =>
                {
                    var rejectReason = TvEpisodeFileNameParser.GetSeriesSearchQueryRejectReason(query.Value);
                    if (!string.IsNullOrWhiteSpace(rejectReason))
                    {
                        if (IsAiRefinedTitleQuery(query.Source))
                        {
                            ScanIdentificationDiagnostics.Write(
                                $"event=tv-query-ai-refined-reject-bypassed directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} query={ScanIdentificationDiagnostics.FormatValue(query.Value)} querySource={ScanIdentificationDiagnostics.FormatValue(query.Source)} badQueryRejectReason={ScanIdentificationDiagnostics.FormatValue(rejectReason)} aiRefinedLookupAttempted=true");
                            return true;
                        }

                        var rejectedQuery = query with
                        {
                            Quality = IsNoisyTvQueryRejectReason(rejectReason) ? "noisy" : "rejected",
                            RejectReason = rejectReason
                        };
                        if (string.Equals(rejectedQuery.Quality, "noisy", StringComparison.OrdinalIgnoreCase))
                        {
                            noisy.Add(rejectedQuery);
                        }
                        else
                        {
                            rejected.Add(rejectedQuery);
                        }
                    }

                    return string.IsNullOrWhiteSpace(rejectReason);
                })
            .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderBy(y => y.Priority).First())
            .OrderBy(x => x.Priority)
            .Take(8)
            .ToList();

        foreach (var rejectedQuery in rejected
                     .Concat(noisy)
                     .GroupBy(x => $"{x.Source}:{x.Value}:{x.RejectReason}", StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.First()))
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tv-query-rejected directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} query={ScanIdentificationDiagnostics.FormatValue(rejectedQuery.Value)} querySource={ScanIdentificationDiagnostics.FormatValue(rejectedQuery.Source)} queryQuality={ScanIdentificationDiagnostics.FormatValue(rejectedQuery.Quality)} badQueryRejectReason={ScanIdentificationDiagnostics.FormatValue(rejectedQuery.RejectReason)}");
        }

        return new TvSearchQuerySet(
            queries,
            noisy
                .GroupBy(x => $"{x.Source}:{x.Value}:{x.RejectReason}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderBy(y => y.Priority).First())
                .OrderBy(x => x.Priority)
                .Take(8)
                .ToList(),
            rejected
                .GroupBy(x => $"{x.Source}:{x.Value}:{x.RejectReason}", StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderBy(y => y.Priority).First())
                .OrderBy(x => x.Priority)
                .Take(8)
                .ToList());
    }

    private static bool IsNoisyTvQueryRejectReason(string rejectReason)
    {
        return string.Equals(rejectReason, "quality-only-query", StringComparison.OrdinalIgnoreCase)
               || string.Equals(rejectReason, "codec-only-query", StringComparison.OrdinalIgnoreCase)
               || string.Equals(rejectReason, "release-metadata-only-query", StringComparison.OrdinalIgnoreCase)
               || string.Equals(rejectReason, "dirty-query", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<TvSearchQuery> BuildSearchQueryCandidates(
        string directoryPath,
        string folderName,
        IReadOnlyList<TvSeasonCandidateFile> files,
        TvScanDirectoryAnalysisResult? directoryAnalysis)
    {
        var values = new List<TvSearchQuery>();
        foreach (var hint in files
                     .Select(x => directoryAnalysis?.GetHint(x.MediaFileId))
                     .Where(x => !string.IsNullOrWhiteSpace(x?.SeriesTitleHint)))
        {
            var source = IsAiRefinedTitleHintSource(hint!.Source)
                ? "ai-refined-title"
                : IsAiDirectoryHintSource(hint.Source)
                    ? "ai-title-hint"
                    : "directory-title-hint";
            values.Add(new TvSearchQuery(
                hint.SeriesTitleHint,
                source,
                10,
                LocalizedTitleHint: hint.LocalizedTitleHint,
                OriginalTitleHint: hint.OriginalTitleHint,
                OriginalLanguageTitle: hint.OriginalLanguageTitle,
                EnglishTitleHint: hint.EnglishTitleHint,
                SearchTitle: FirstNonEmpty(hint.SearchTitle, hint.SeriesTitleHint),
                SearchTitleSource: FirstNonEmpty(hint.SearchTitleSource, "legacy-refined-title"),
                YearHint: hint.SeriesYearHint ?? hint.YearHint,
                SeriesYearHint: hint.SeriesYearHint ?? hint.YearHint,
                SeasonYearHint: hint.SeasonYearHint,
                SeasonNumberHint: hint.SeasonNumberHint));
        }

        var parentFolderName = GetFolderName(GetDirectoryPath(directoryPath));
        if (TvEpisodeFileNameParser.IsSeasonFolderName(folderName))
        {
            values.Add(new TvSearchQuery(parentFolderName, "parent-folder", 20));
        }

        values.Add(new TvSearchQuery(RemoveSeasonSuffix(folderName), "folder-season-stripped", 30));
        values.Add(new TvSearchQuery(folderName, "folder-name", 50));
        values.Add(new TvSearchQuery(BuildCommonPrefix(files), "common-prefix", 60));
        values.AddRange(files.Select(x => new TvSearchQuery(x.ParseResult.SeriesNameCandidate, "parsed-series-candidate", 70)));
        values.AddRange(SplitMixedTitleCandidates(folderName).Select(x => new TvSearchQuery(x, "folder-mixed-title", 80)));
        values.AddRange(SplitMixedTitleCandidates(parentFolderName).Select(x => new TvSearchQuery(x, "parent-mixed-title", 90)));

        return values
            .Select(x => x with { Value = x.Value?.Trim() ?? string.Empty })
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .GroupBy(x => $"{x.Source}:{x.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderBy(y => y.Priority).First())
            .ToList();
    }

    private static string BuildCommonPrefix(IReadOnlyList<TvSeasonCandidateFile> files)
    {
        var candidates = files
            .Select(x => FirstNonEmpty(x.ParseResult.SeriesNameCandidate, TvEpisodeFileNameParser.CleanSeriesNameCandidate(x.FileName)))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => NormalizeTitle(x), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenByDescending(x => x.First().Length)
            .Select(x => x.First())
            .FirstOrDefault();

        return candidates ?? string.Empty;
    }

    private static TvDirectoryContext AnalyzeDirectoryContext(
        string directoryPath,
        IReadOnlyList<CandidateMediaFile> files,
        TvScanDirectoryAnalysisResult? directoryAnalysis)
    {
        var folderName = GetFolderName(directoryPath);
        var folderSeasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(folderName);
        var isSeasonFolder = TvEpisodeFileNameParser.IsSeasonFolderName(folderName);
        var strongDirectoryHints = files
            .Select(x => directoryAnalysis?.GetHint(x.Id))
            .Where(x => x?.IsStrongTvContext == true)
            .ToArray();
        var hasDirectoryStrongHint = strongDirectoryHints.Length > 0;
        var hasAiStrongHint = strongDirectoryHints.Any(x => IsAiDirectoryHintSource(x!.Source));
        var hasFallbackBlockHint = files.Any(x => directoryAnalysis?.BlocksMovieFallback(x.Id) == true);
        var explicitEpisodeCount = files.Count(x => TvEpisodeFileNameParser.Parse(x.FileName).IsEpisodeLike);
        var contextEpisodeCount = files.Count(
            x => TvEpisodeFileNameParser.Parse(
                    x.FileName,
                    allowSeasonContextOnly: true,
                    seasonNumberHint: folderSeasonNumber,
                    allowStrongContextFallbacks: false)
                .IsEpisodeLike);
        var strongFallbackEpisodeCount = files.Count(
            x => TvEpisodeFileNameParser.Parse(
                    x.FileName,
                    allowSeasonContextOnly: true,
                    seasonNumberHint: folderSeasonNumber,
                    allowStrongContextFallbacks: true)
                .IsEpisodeLike);
        var bareNumberCount = files.Count(x => TvEpisodeFileNameParser.IsBareNumberEpisodeFileName(x.FileName));
        var titleNumberCount = files.Count(x => TvEpisodeFileNameParser.IsTitleNumberEpisodeFileName(x.FileName));
        var hasTitleNumberSequence = TvEpisodeFileNameParser.TryAnalyzeTitleNumberSequence(
            files.Select(x => x.FileName),
            out var titleNumberSequence);
        var sequentialEpisodeDirectory = LooksLikeSequentialEpisodeDirectory(files);
        var hasChineseSeasonHint = TvEpisodeFileNameParser.HasChineseSeasonHint(folderName);
        var hasChineseCountHint = TvEpisodeFileNameParser.HasChineseCountHint(folderName);
        var hasCountSequentialRisk = hasChineseCountHint && (sequentialEpisodeDirectory || bareNumberCount >= 2);

        var strongEvidence = new List<string>();
        var weakReasons = new List<string>();
        if (hasDirectoryStrongHint)
        {
            strongEvidence.Add(hasAiStrongHint ? "ai-range" : "local-directory-range");
        }

        if (isSeasonFolder)
        {
            strongEvidence.Add("season-folder");
        }

        AddCountEvidence(strongEvidence, weakReasons, explicitEpisodeCount, "explicit-episode-files", "single-explicit-episode");
        AddCountEvidence(strongEvidence, weakReasons, contextEpisodeCount, "context-episode-files", "single-context-episode");
        if (sequentialEpisodeDirectory && isSeasonFolder)
        {
            strongEvidence.Add("sequential-files");
        }
        else if (sequentialEpisodeDirectory)
        {
            weakReasons.Add("sequential-files-without-season-context");
        }

        if (bareNumberCount >= 2 && isSeasonFolder)
        {
            strongEvidence.Add("numeric-files");
        }
        else if (bareNumberCount > 0)
        {
            weakReasons.Add("weak-numeric-files");
        }

        if (titleNumberCount >= 2 && isSeasonFolder)
        {
            strongEvidence.Add("title-number-files");
        }
        else if (hasTitleNumberSequence)
        {
            weakReasons.Add("title-number-sequence");
        }
        else if (titleNumberCount > 0)
        {
            weakReasons.Add("weak-title-number");
        }

        if (hasChineseSeasonHint)
        {
            weakReasons.Add("chinese-season-hint");
        }

        if (files.Any(x => TvEpisodeFileNameParser.HasChineseEpisodeHint(x.FileName)))
        {
            weakReasons.Add("chinese-episode-hint");
        }

        if (hasChineseCountHint)
        {
            weakReasons.Add("chinese-count-hint");
        }

        if (hasCountSequentialRisk)
        {
            weakReasons.Add("count-hint-sequential-files");
        }

        var hasEpisodeEvidence = hasDirectoryStrongHint
                                 || explicitEpisodeCount >= 2
                                 || contextEpisodeCount >= 2
                                 || (sequentialEpisodeDirectory && isSeasonFolder)
                                 || (bareNumberCount >= 2 && isSeasonFolder)
                                 || (titleNumberCount >= 2 && isSeasonFolder);
        var isStrong = strongEvidence.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2 && hasEpisodeEvidence;
        var blocksMovieFallback = isStrong
                                  || hasFallbackBlockHint
                                  || hasCountSequentialRisk
                                  || (isSeasonFolder && (explicitEpisodeCount + contextEpisodeCount + bareNumberCount + titleNumberCount > 0 || files.Count >= 2))
                                  || explicitEpisodeCount >= 2
                                  || contextEpisodeCount >= 2
                                  || hasTitleNumberSequence;

        return new TvDirectoryContext(
            isStrong,
            blocksMovieFallback,
            strongEvidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            weakReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            explicitEpisodeCount,
            contextEpisodeCount,
            strongFallbackEpisodeCount,
            titleNumberSequence);
    }

    private static void AddCountEvidence(
        List<string> strongEvidence,
        List<string> weakReasons,
        int count,
        string strongKind,
        string weakKind)
    {
        if (count >= 2)
        {
            strongEvidence.Add(strongKind);
        }
        else if (count == 1)
        {
            weakReasons.Add(weakKind);
        }
    }

    private static bool IsAiDirectoryHintSource(string? source)
    {
        return string.Equals(source, "ai", StringComparison.OrdinalIgnoreCase)
               || string.Equals(source, "ai-on-uncertain", StringComparison.OrdinalIgnoreCase)
               || IsAiRefinedTitleHintSource(source);
    }

    private static bool IsAiRefinedTitleHintSource(string? source)
    {
        return string.Equals(source, "ai-refined-title", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAiRefinedTitleQuery(string? source)
    {
        return string.Equals(source, "ai-refined-title", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeSequentialEpisodeDirectory(IReadOnlyList<CandidateMediaFile> files)
    {
        var episodeNumbers = files
            .Select(x => TvEpisodeFileNameParser.Parse(
                x.FileName,
                allowSeasonContextOnly: true,
                allowStrongContextFallbacks: true))
            .Where(x => x.IsEpisodeLike && !x.IsMultiEpisode && x.EpisodeNumber is > 0 and <= 9999)
            .Select(x => x.EpisodeNumber)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        return episodeNumbers.Length >= 2 && episodeNumbers[^1] - episodeNumbers[0] <= Math.Max(12, episodeNumbers.Length + 4);
    }

    private static string GetCandidateConflictReason(
        TvSearchQuery queryAttempt,
        TvSearchCandidate? bestCandidate,
        TvSearchCandidate? nextBestCandidate)
    {
        if (IsAiRefinedTitleQuery(queryAttempt.Source))
        {
            return string.Empty;
        }

        if (bestCandidate is null || bestCandidate.Confidence < MinimumAutoMatchConfidence)
        {
            return string.Empty;
        }

        if (nextBestCandidate is not null
            && bestCandidate.Item.TmdbId != nextBestCandidate.Item.TmdbId
            && bestCandidate.Confidence - nextBestCandidate.Confidence < 0.08d
            && nextBestCandidate.Confidence >= MinimumAutoMatchConfidence)
        {
            return "top-candidates-too-close";
        }

        var query = queryAttempt.Value;
        var queryYear = ExtractYear(query);
        if (queryYear.HasValue
            && bestCandidate.Item.FirstAirYear.HasValue
            && Math.Abs(queryYear.Value - bestCandidate.Item.FirstAirYear.Value) > 1)
        {
            return "year-conflict";
        }

        if (HasLocalizedTitleExactMatch(query, bestCandidate.Item)
            && HasOriginalTitleConflict(query, bestCandidate.Item)
            && HasLocalizedVersionQualifierMismatch(query, bestCandidate.Item.Name))
        {
            return "localized-title-version-conflict";
        }

        var hasEnglishTitleHint = query.Any(char.IsAsciiLetter);
        var originalSimilarity = MovieFileNameParser.CalculateTitleSimilarity(query, bestCandidate.Item.OriginalName);
        var localizedSimilarity = MovieFileNameParser.CalculateTitleSimilarity(query, bestCandidate.Item.Name);
        if (hasEnglishTitleHint
            && localizedSimilarity >= 0.92d
            && originalSimilarity < 0.55d
            && !string.Equals(queryAttempt.Source, "ai-title-hint", StringComparison.OrdinalIgnoreCase))
        {
            return "original-title-conflict";
        }

        return string.Empty;
    }

    private static string GetAiRefinedSafetyGateReason(
        TvSearchQuery queryAttempt,
        TvSearchCandidate? bestCandidate,
        TvSearchCandidate? nextBestCandidate)
    {
        return bestCandidate is null ? "no-tmdb-result" : string.Empty;
    }

    private static AiRefinedYearGateResult GetAiRefinedYearGateResult(
        TvSearchQuery queryAttempt,
        TvSearchCandidate? bestCandidate)
    {
        if (!queryAttempt.SeriesYearHint.HasValue)
        {
            return AiRefinedYearGateResult.NotChecked("missing-series-year-hint");
        }

        if (bestCandidate?.Item.FirstAirYear is not { } firstAirYear)
        {
            return AiRefinedYearGateResult.NotChecked("missing-tmdb-first-air-year");
        }

        var diff = Math.Abs(queryAttempt.SeriesYearHint.Value - firstAirYear);
        return diff > 2
            ? new AiRefinedYearGateResult(true, diff, true, "series-year-conflict")
            : new AiRefinedYearGateResult(true, diff, false, string.Empty);
    }

    private static string GetTvSearchDecision(TvSearchCandidate? candidate, string conflictReason)
    {
        if (candidate is null)
        {
            return "placeholder-no-result";
        }

        if (candidate.Confidence < MinimumAutoMatchConfidence)
        {
            return "placeholder-low-confidence";
        }

        if (!string.IsNullOrWhiteSpace(conflictReason))
        {
            return "placeholder-conflict";
        }

        return candidate.Confidence >= MatchedConfidence
            ? "match"
            : "placeholder-needs-review";
    }

    private static string GetTvAutoApplyBlockedReason(TvSearchCandidate? candidate, string conflictReason)
    {
        if (!string.IsNullOrWhiteSpace(conflictReason))
        {
            return conflictReason;
        }

        if (candidate is null)
        {
            return "no-result";
        }

        if (candidate.Confidence < MinimumAutoMatchConfidence)
        {
            return "low-confidence";
        }

        return candidate.Confidence >= MatchedConfidence
            ? string.Empty
            : "needs-review-not-auto-applied";
    }

    private static bool HasLocalizedTitleExactMatch(string query, TmdbTvSeriesSearchItem? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        var normalizedQuery = NormalizeConflictTitle(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return false;
        }

        return string.Equals(normalizedQuery, NormalizeConflictTitle(candidate.Name), StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalizedQuery, NormalizeConflictTitle(RemoveBracketedQualifiers(candidate.Name)), StringComparison.OrdinalIgnoreCase)
               || MovieFileNameParser.CalculateTitleSimilarity(query, candidate.Name) >= 0.96d;
    }

    private static bool HasOriginalTitleConflict(string query, TmdbTvSeriesSearchItem? candidate)
    {
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.OriginalName))
        {
            return false;
        }

        return HasLocalizedTitleExactMatch(query, candidate)
               && MovieFileNameParser.CalculateTitleSimilarity(query, candidate.OriginalName) < 0.55d;
    }

    private static bool HasLocalizedVersionQualifierMismatch(string query, string localizedTitle)
    {
        var bracketMatches = System.Text.RegularExpressions.Regex
            .Matches(localizedTitle, @"[\(\[\{（【](?<value>[^()\[\]\{\}（）【】]+)[\)\]\}）】]", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(x => NormalizeConflictTitle(x.Groups["value"].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (bracketMatches.Length == 0)
        {
            return false;
        }

        var normalizedQuery = NormalizeConflictTitle(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return false;
        }

        var normalizedTitleWithoutQualifiers = NormalizeConflictTitle(RemoveBracketedQualifiers(localizedTitle));
        if (!string.Equals(normalizedQuery, normalizedTitleWithoutQualifiers, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return bracketMatches.Any(qualifier => !normalizedQuery.Contains(qualifier, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveBracketedQualifiers(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"[\(\[\{（【][^()\[\]\{\}（）【】]+[\)\]\}）】]",
            " ",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string NormalizeConflictTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || (ch is >= '\u4e00' and <= '\u9fff') ? ch : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static int? ExtractYear(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            value,
            @"(?:^|[^\d])(?<year>19\d{2}|20\d{2})(?:[^\d]|$)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["year"].Value, out var year)
            ? year
            : null;
    }

    private static string RemoveSeasonSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"(?:[\s._-]*[Ss]\d{1,2}|[\s._-]*Season\s*\d{1,2}|[\s._-]*\u7b2c\s*[0-9一二三四五六七八九十两]{1,4}\s*\u5b63(?:\s*\u5168\s*\d{1,3}\s*\u96c6)?)",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return TvEpisodeFileNameParser.CleanSeriesNameCandidate(normalized);
    }

    private static IEnumerable<string> SplitMixedTitleCandidates(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var cleaned = TvEpisodeFileNameParser.CleanSeriesNameCandidate(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            yield break;
        }

        var english = new string(cleaned.Select(ch => char.IsAsciiLetter(ch) || ch is ' ' ? ch : ' ').ToArray());
        english = string.Join(' ', english.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(english))
        {
            yield return english;
        }

        var cjk = new string(cleaned.Select(ch => ch is >= '\u4e00' and <= '\u9fff' ? ch : ' ').ToArray());
        cjk = string.Join(' ', cjk.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!string.IsNullOrWhiteSpace(cjk))
        {
            yield return cjk;
        }
    }

    private static double CalculateSeriesConfidence(string expectedTitle, TmdbTvSeriesSearchItem candidate)
    {
        var titleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.Name);
        var originalTitleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.OriginalName);
        return Math.Clamp(Math.Max(titleSimilarity, originalTitleSimilarity), 0d, 1d);
    }

    private static IdentificationStatus IdentificationStatusFromConfidence(double confidence)
    {
        return confidence >= MatchedConfidence
            ? IdentificationStatus.Matched
            : IdentificationStatus.NeedsReview;
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex <= 0
            ? "/"
            : normalized[..lastSeparatorIndex];
    }

    private static string GetFolderName(string directoryPath)
    {
        var normalized = directoryPath.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex < 0
            ? normalized
            : normalized[(lastSeparatorIndex + 1)..];
    }

    private static string BuildDirectoryKey(int sourceConnectionId, string directoryPath)
    {
        return $"{sourceConnectionId}:{directoryPath.Replace('\\', '/').TrimEnd('/').ToUpperInvariant()}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string NormalizeTitle(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date.Date
            : null;
    }

    private static int? PositiveOrNull(int? value)
    {
        return value is > 0 ? value.Value : null;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? UnidentifiedSeasonTitle : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
    }

    private sealed class CandidateMediaFile
    {
        public int Id { get; set; }

        public int SourceConnectionId { get; set; }

        public int? ScanPathId { get; set; }

        public int? EpisodeId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;
    }

    private sealed class TvSeasonCandidate
    {
        public int SourceConnectionId { get; set; }

        public string DirectoryPath { get; set; } = string.Empty;

        public string FolderName { get; set; } = string.Empty;

        public string CandidateName { get; set; } = string.Empty;

        public string CommonPrefix { get; set; } = string.Empty;

        public int SeasonNumber { get; set; } = 1;

        public bool IsStrongTvContext { get; set; }

        public string CandidateSource { get; set; } = "local";

        public List<string> StrongTvEvidence { get; set; } = [];

        public List<string> WeakTvReasons { get; set; } = [];

        public string StrongTvEvidenceText => string.Join('|', StrongTvEvidence);

        public string WeakTvReasonText => string.Join('|', WeakTvReasons);

        public List<TvSearchQuery> SearchQueries { get; set; } = [];

        public List<TvSearchQuery> RejectedSearchQueries { get; set; } = [];

        public List<TvSearchQuery> NoisySearchQueries { get; set; } = [];

        public List<TvSeasonCandidateFile> Files { get; set; } = [];

        public List<TvSeasonCandidateFile> UnsupportedFiles { get; set; } = [];
    }

    private sealed class TvSeasonCandidateFile
    {
        public int MediaFileId { get; set; }

        public int? EpisodeId { get; set; }

        public int SeasonNumber { get; set; } = 1;

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public TvEpisodeFileNameParseResult ParseResult { get; set; } = new();
    }

    private sealed record TvDirectoryContext(
        bool IsStrong,
        bool BlocksMovieFallback,
        IReadOnlyList<string> StrongEvidence,
        IReadOnlyList<string> WeakReasons,
        int ExplicitEpisodeCount,
        int ContextEpisodeCount,
        int StrongFallbackEpisodeCount,
        TvEpisodeSequenceAnalysis TitleNumberSequence)
    {
        public string EvidenceText => string.Join('|', StrongEvidence);

        public string WeakReasonText => string.Join('|', WeakReasons);
    }

    private sealed record TvSearchQuery(
        string Value,
        string Source,
        int Priority,
        string Quality = "usable",
        string RejectReason = "",
        string LocalizedTitleHint = "",
        string OriginalTitleHint = "",
        string OriginalLanguageTitle = "",
        string EnglishTitleHint = "",
        string SearchTitle = "",
        string SearchTitleSource = "",
        int? YearHint = null,
        int? SeriesYearHint = null,
        int? SeasonYearHint = null,
        int? SeasonNumberHint = null);

    private sealed record TvSearchQuerySet(
        List<TvSearchQuery> Usable,
        List<TvSearchQuery> Noisy,
        List<TvSearchQuery> Rejected);

    private sealed record TvSearchCandidate(
        TmdbTvSeriesSearchItem Item,
        double Confidence,
        string Query,
        string QuerySource,
        string LocalizedTitleHint = "",
        string OriginalTitleHint = "",
        int? YearHint = null,
        int? SeriesYearHint = null,
        int? SeasonYearHint = null,
        int? SeasonNumberHint = null);

    private sealed record AiRefinedYearGateResult(bool Checked, int? YearDiff, bool Blocked, string Reason)
    {
        public static AiRefinedYearGateResult NotChecked(string reason) => new(false, null, false, reason);
    }
}
