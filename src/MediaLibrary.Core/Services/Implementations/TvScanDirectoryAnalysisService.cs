using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed partial class TvScanDirectoryAnalysisService : ITvScanDirectoryAnalysisService
{
    private static readonly bool EnableFullAiRangeAnalysisByDefault = false;
    private const int MaxAiDirectoryLines = 140;
    private const int MaxAiSampleFilesPerDirectory = 5;
    private const int MaxAiChildFolderNamesPerDirectory = 8;
    private static readonly TimeSpan AiRangeTimeout = TimeSpan.FromSeconds(18);
    private const int MaxAiOnUncertainRanges = 80;
    private const int MaxAiOnUncertainSamplesPerRange = 5;
    private const int MaxAiOnUncertainQueriesPerRange = 5;
    private static readonly TimeSpan AiOnUncertainTimeout = TimeSpan.FromSeconds(120);

    private readonly IAiService _aiService;

    public TvScanDirectoryAnalysisService(IAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task<TvScanDirectoryAnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var result = new TvScanDirectoryAnalysisResult();
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return result;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var files = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => ids.Contains(x.Id)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted)
            .Select(
                x => new ScanFile(
                    x.Id,
                    x.SourceConnectionId,
                    x.FileName,
                    x.FilePath))
            .ToListAsync(cancellationToken);

        ApplyLocalHints(files, result);
        if (EnableFullAiRangeAnalysisByDefault)
        {
            await TryApplyAiHintsAsync(files, result, cancellationToken);
        }
        else
        {
            LogFullAiRangeDisabled(files, result);
        }

        ScanIdentificationDiagnostics.Write(
            $"event=tv-range-analysis-complete requested={ids.Length} files={files.Count} strongTvFiles={result.StrongTvMediaFileIds.Count} movieFallbackBlockedByTvRisk={result.MovieFallbackBlockedMediaFileIds.Count} aiCandidateRanges={result.AiCandidateRanges.Count} aiAttempted={result.AiAttempted.ToString().ToLowerInvariant()} aiSucceeded={result.AiSucceeded.ToString().ToLowerInvariant()} message={ScanIdentificationDiagnostics.FormatValue(result.Message, 180)}");
        return result;
    }

    public async Task<TvScanAiOnUncertainApplyResult> ApplyAiOnUncertainAsync(
        IReadOnlyCollection<int> mediaFileIds,
        TvScanDirectoryAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var ranges = analysis.AiCandidateRanges
            .Where(x => !string.IsNullOrWhiteSpace(x.SanitizedPath))
            .OrderBy(x => x.SanitizedPath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxAiOnUncertainRanges)
            .ToList();
        if (ranges.Count == 0)
        {
            ScanIdentificationDiagnostics.Write(
                "event=ai-on-uncertain-skipped aiOnUncertainAttempted=false aiOnUncertainCandidateRanges=0 aiOnUncertainSkippedReason=no-ai-candidates aiAutoApply=false fallback=local-placeholders-preserved");
            return new TvScanAiOnUncertainApplyResult();
        }

        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=ai-on-uncertain-skipped aiOnUncertainAttempted=false aiOnUncertainCandidateRanges={ranges.Count} aiOnUncertainSkippedReason=no-media-file-ids aiAutoApply=false fallback=local-placeholders-preserved");
            return new TvScanAiOnUncertainApplyResult();
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var files = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => ids.Contains(x.Id)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted)
            .Select(
                x => new ScanFile(
                    x.Id,
                    x.SourceConnectionId,
                    x.FileName,
                    x.FilePath))
            .ToListAsync(cancellationToken);
        if (files.Count == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=ai-on-uncertain-skipped aiOnUncertainAttempted=false aiOnUncertainCandidateRanges={ranges.Count} aiOnUncertainSkippedReason=no-active-video-files aiAutoApply=false fallback=local-placeholders-preserved");
            return new TvScanAiOnUncertainApplyResult();
        }

        var inputRanges = ranges
            .Select((range, index) => BuildAiOnUncertainInputRange(index + 1, range))
            .ToList();
        var rangesWithFiles = inputRanges.Count(x => x.MediaFileIds.Count > 0);
        var rangesWithoutFiles = Math.Max(0, inputRanges.Count - rangesWithFiles);
        var prompt = BuildAiOnUncertainPrompt(inputRanges);
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();
        var responseChars = 0;
        var jsonParseSucceeded = false;
        var promptTokenEstimate = EstimateTokenCount(prompt);
        var pathItems = files
            .OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SanitizedPathItem(x.Id, x.FilePath, SanitizePathForPrompt(x.FilePath)))
            .ToList();
        var pathItemsById = pathItems
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First());

        ScanIdentificationDiagnostics.Write(
            $"event=ai-on-uncertain-request-start requestId={requestId} aiOnUncertainAttempted=true aiOnUncertainCandidateRanges={inputRanges.Count} aiOnUncertainRangesWithFiles={rangesWithFiles} aiOnUncertainRangesWithoutFiles={rangesWithoutFiles} omittedCandidateRanges={Math.Max(0, analysis.AiCandidateRanges.Count - inputRanges.Count)} aiOnUncertainInputChars={prompt.Length} aiOnUncertainInputTokenEstimate={promptTokenEstimate} timeoutMs={(int)AiOnUncertainTimeout.TotalMilliseconds} fullAiRangeAnalysis=disabled aiAutoApply=false schema=ai-original-language-title-v1");
        try
        {
            var response = await _aiService.GenerateTextAsync(
                "You review uncertain TV scan directory ranges. Return JSON hints only. Never return episodeFiles.",
                prompt,
                new AiRequestOptions
                {
                    Temperature = 0.1,
                    Timeout = AiOnUncertainTimeout
                },
                cancellationToken);
            stopwatch.Stop();
            responseChars = response?.Length ?? 0;
            ScanIdentificationDiagnostics.Write(
                $"event=ai-on-uncertain-response requestId={requestId} aiOnUncertainDurationMs={stopwatch.ElapsedMilliseconds} responseChars={responseChars} assistantContentChars={responseChars} empty={string.IsNullOrWhiteSpace(response).ToString().ToLowerInvariant()} aiAutoApply=false schema=ai-original-language-title-v1");
            if (string.IsNullOrWhiteSpace(response))
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=ai-on-uncertain-complete requestId={requestId} aiOnUncertainSucceeded=false aiOnUncertainFailureReason=empty-or-not-configured fallback=local-placeholders-preserved aiAutoApply=false");
                return new TvScanAiOnUncertainApplyResult();
            }

            var parsed = ParseAiOnUncertainResponse(response);
            jsonParseSucceeded = true;
            var runResult = new TvScanAiOnUncertainApplyResult();
            var inputById = inputRanges.ToDictionary(x => x.InputRangeId, StringComparer.OrdinalIgnoreCase);
            var appliedHints = 0;
            var appliedFiles = 0;
            var ignoredHints = 0;
            var parsedHints = 0;
            var appliedByMediaFileIds = 0;
            var appliedByPathFallback = 0;
            var ignoredNoMediaFileIds = 0;
            var ignoredNoFilesInRange = 0;
            var skippedNoOriginalOrSearchTitle = 0;
            foreach (var hint in parsed.Ranges)
            {
                parsedHints++;
                var titleSelection = SelectAiOnUncertainSearchTitle(hint);
                if (!TryResolveAiOnUncertainInputRange(hint, inputById, inputRanges, out var inputRange, out var appliedBy))
                {
                    ignoredHints++;
                    ScanIdentificationDiagnostics.Write(
                        $"event=ai-on-uncertain-hint-ignored requestId={requestId} inputRangeId={ScanIdentificationDiagnostics.FormatValue(hint.InputRangeId)} reason=no-range-match ignoredReason=no-range-match aiHintApplied=false aiAutoApply=false");
                    continue;
                }

                var applyResult = ApplyAiOnUncertainHint(hint, inputRange, pathItems, pathItemsById, analysis);
                if (applyResult.AppliedFiles == 0)
                {
                    ignoredHints++;
                    if (string.Equals(applyResult.IgnoredReason, "no-media-file-ids", StringComparison.OrdinalIgnoreCase))
                    {
                        ignoredNoMediaFileIds++;
                    }
                    else if (string.Equals(applyResult.IgnoredReason, "no-files-in-input-range", StringComparison.OrdinalIgnoreCase))
                    {
                        ignoredNoFilesInRange++;
                    }
                    else if (string.Equals(applyResult.IgnoredReason, "no-original-or-search-title", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedNoOriginalOrSearchTitle++;
                    }

                    ScanIdentificationDiagnostics.Write(
                        $"event=ai-on-uncertain-hint-ignored requestId={requestId} inputRangeId={ScanIdentificationDiagnostics.FormatValue(hint.InputRangeId)} aiRefinedTitleParsed={!string.IsNullOrWhiteSpace(titleSelection.Title)} aiRefinedTitle={ScanIdentificationDiagnostics.FormatValue(titleSelection.Title)} aiOriginalLanguageTitle={ScanIdentificationDiagnostics.FormatValue(titleSelection.OriginalLanguageTitle)} aiEnglishTitleHint={ScanIdentificationDiagnostics.FormatValue(hint.EnglishTitleHint)} aiLocalizedTitleHint={ScanIdentificationDiagnostics.FormatValue(hint.LocalizedTitleHint)} aiOriginalTitleHint={ScanIdentificationDiagnostics.FormatValue(hint.OriginalTitleHint)} aiSearchTitle={ScanIdentificationDiagnostics.FormatValue(titleSelection.Title)} aiSearchTitleSource={ScanIdentificationDiagnostics.FormatValue(titleSelection.Source)} originalLanguageTitleMissing={titleSelection.OriginalLanguageTitleMissing.ToString().ToLowerInvariant()} fallbackToEnglishTitle={titleSelection.FallbackToEnglishTitle.ToString().ToLowerInvariant()} fallbackToLocalizedTitle={titleSelection.FallbackToLocalizedTitle.ToString().ToLowerInvariant()} aiSeriesYearHint={ScanIdentificationDiagnostics.FormatNullable(hint.SeriesYearHint ?? hint.YearHint)} aiSeasonYearHint={ScanIdentificationDiagnostics.FormatNullable(hint.SeasonYearHint)} aiSeasonNumberHint={ScanIdentificationDiagnostics.FormatNullable(hint.SeasonNumberHint)} confidence={ScanIdentificationDiagnostics.FormatValue(hint.Confidence)} aiConfidence={ScanIdentificationDiagnostics.FormatValue(hint.Confidence)} aiNeedsReview={hint.NeedsReview.ToString().ToLowerInvariant()} evidence={ScanIdentificationDiagnostics.FormatValue(FormatEvidence(hint.Evidence))} reason={ScanIdentificationDiagnostics.FormatValue(applyResult.IgnoredReason)} ignoredReason={ScanIdentificationDiagnostics.FormatValue(applyResult.IgnoredReason)} aiRefinedLookupAttempted=false aiRefinedLookupSkippedReason={ScanIdentificationDiagnostics.FormatValue(applyResult.IgnoredReason)} rangeMediaFileCount={applyResult.RangeMediaFileCount} filesResolvedCount={applyResult.FilesResolvedCount} filesResolvedBy={ScanIdentificationDiagnostics.FormatValue(applyResult.FilesResolvedBy)} aiHintApplied=false aiHintAppliedBy={appliedBy} directoryHintMismatch={applyResult.DirectoryHintMismatch.ToString().ToLowerInvariant()} aiAutoApply=false finalDecisionAfterAiHint=placeholder-preserved finalDecisionAfterAiRefinedLookup=placeholder-preserved");
                    continue;
                }

                appliedHints++;
                appliedFiles += applyResult.AppliedFiles;
                runResult.AppliedFiles += applyResult.AppliedFiles;
                runResult.AddAffectedMediaFiles(applyResult.AppliedMediaFileIds);
                if (string.Equals(applyResult.FilesResolvedBy, "mediaFileIds", StringComparison.OrdinalIgnoreCase))
                {
                    appliedByMediaFileIds++;
                }
                else if (string.Equals(applyResult.FilesResolvedBy, "sanitizedPathFallback", StringComparison.OrdinalIgnoreCase))
                {
                    appliedByPathFallback++;
                }

                ScanIdentificationDiagnostics.Write(
                    $"event=ai-on-uncertain-hint-applied requestId={requestId} inputRangeId={ScanIdentificationDiagnostics.FormatValue(hint.InputRangeId)} aiRefinedTitleParsed={!string.IsNullOrWhiteSpace(titleSelection.Title)} aiRefinedTitle={ScanIdentificationDiagnostics.FormatValue(titleSelection.Title)} aiOriginalLanguageTitle={ScanIdentificationDiagnostics.FormatValue(titleSelection.OriginalLanguageTitle)} aiEnglishTitleHint={ScanIdentificationDiagnostics.FormatValue(hint.EnglishTitleHint)} aiLocalizedTitleHint={ScanIdentificationDiagnostics.FormatValue(hint.LocalizedTitleHint)} aiOriginalTitleHint={ScanIdentificationDiagnostics.FormatValue(hint.OriginalTitleHint)} aiSearchTitle={ScanIdentificationDiagnostics.FormatValue(titleSelection.Title)} aiSearchTitleSource={ScanIdentificationDiagnostics.FormatValue(titleSelection.Source)} originalLanguageTitleMissing={titleSelection.OriginalLanguageTitleMissing.ToString().ToLowerInvariant()} fallbackToEnglishTitle={titleSelection.FallbackToEnglishTitle.ToString().ToLowerInvariant()} fallbackToLocalizedTitle={titleSelection.FallbackToLocalizedTitle.ToString().ToLowerInvariant()} aiSeriesYearHint={ScanIdentificationDiagnostics.FormatNullable(hint.SeriesYearHint ?? hint.YearHint)} aiSeasonYearHint={ScanIdentificationDiagnostics.FormatNullable(hint.SeasonYearHint)} aiSeasonNumberHint={ScanIdentificationDiagnostics.FormatNullable(hint.SeasonNumberHint)} confidence={ScanIdentificationDiagnostics.FormatValue(hint.Confidence)} aiConfidence={ScanIdentificationDiagnostics.FormatValue(hint.Confidence)} aiNeedsReview={hint.NeedsReview.ToString().ToLowerInvariant()} evidence={ScanIdentificationDiagnostics.FormatValue(FormatEvidence(hint.Evidence))} aiEvidence={ScanIdentificationDiagnostics.FormatValue(FormatEvidence(hint.Evidence))} seriesTitleHint={ScanIdentificationDiagnostics.FormatValue(titleSelection.Title)} rangeMediaFileCount={applyResult.RangeMediaFileCount} filesResolvedCount={applyResult.FilesResolvedCount} filesResolvedBy={ScanIdentificationDiagnostics.FormatValue(applyResult.FilesResolvedBy)} appliedFiles={applyResult.AppliedFiles} aiHintApplied=true aiHintAppliedBy={appliedBy} aiHintQuerySource=ai-refined-title directoryHintMismatch={applyResult.DirectoryHintMismatch.ToString().ToLowerInvariant()} directoryHintMatchCount={applyResult.DirectoryHintMatchCount} tmdbValidationAfterAiHint=pending aiRefinedLookupAttempted=true aiRefinedLookupPending=true aiAutoApply=false finalDecisionAfterAiHint=tmdb-validation-pending finalDecisionAfterAiRefinedLookup=tmdb-validation-pending");
            }

            ScanIdentificationDiagnostics.Write(
                $"event=ai-on-uncertain-complete requestId={requestId} aiOnUncertainSucceeded=true aiOnUncertainDurationMs={stopwatch.ElapsedMilliseconds} aiOnUncertainResponseRanges={parsed.Ranges.Count} aiOnUncertainParsedHints={parsedHints} aiOnUncertainAppliedHints={appliedHints} aiOnUncertainIgnoredHints={ignoredHints} aiOnUncertainRangesWithFiles={rangesWithFiles} aiOnUncertainRangesWithoutFiles={rangesWithoutFiles} aiOnUncertainAppliedByMediaFileIds={appliedByMediaFileIds} aiOnUncertainAppliedByPathFallback={appliedByPathFallback} aiOnUncertainIgnoredNoMediaFileIds={ignoredNoMediaFileIds} aiOnUncertainIgnoredNoFilesInRange={ignoredNoFilesInRange} skippedNoOriginalOrSearchTitle={skippedNoOriginalOrSearchTitle} aiOnUncertainJsonParseSucceeded={jsonParseSucceeded.ToString().ToLowerInvariant()} aiHintApplied={(appliedHints > 0).ToString().ToLowerInvariant()} appliedHints={appliedHints} appliedFiles={appliedFiles} aiAffectedMediaFiles={runResult.AffectedMediaFileIds.Count} aiAutoApply=false fallback={(appliedHints > 0 ? "tmdb-validation-required" : "local-placeholders-preserved")}");
            return runResult;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var isCancellation = exception is OperationCanceledException;
            var cancellationOrigin = isCancellation
                ? cancellationToken.IsCancellationRequested
                    ? "scan-token"
                    : stopwatch.Elapsed >= AiOnUncertainTimeout.Subtract(TimeSpan.FromMilliseconds(500))
                        ? "request-timeout-or-provider-timeout"
                        : "unknown-cancellation"
                : "not-cancellation";
            ScanIdentificationDiagnostics.Write(
                $"event=ai-on-uncertain-error requestId={requestId} aiOnUncertainSucceeded=false aiOnUncertainDurationMs={stopwatch.ElapsedMilliseconds} timeoutMs={(int)AiOnUncertainTimeout.TotalMilliseconds} exceptionType={ScanIdentificationDiagnostics.FormatValue(exception.GetType().Name)} innerExceptionType={ScanIdentificationDiagnostics.FormatValue(exception.InnerException?.GetType().Name)} cancellationOrigin={ScanIdentificationDiagnostics.FormatValue(cancellationOrigin)} responseChars={responseChars} assistantContentChars={responseChars} aiOnUncertainJsonParseSucceeded={jsonParseSucceeded.ToString().ToLowerInvariant()} aiOnUncertainFailureReason=ai-failed fallback=local-placeholders-preserved aiAutoApply=false error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 180)}");
            return new TvScanAiOnUncertainApplyResult();
        }
    }

    private static void ApplyLocalHints(IReadOnlyList<ScanFile> files, TvScanDirectoryAnalysisResult result)
    {
        var directoryGroups = files
            .GroupBy(x => new
            {
                x.SourceConnectionId,
                DirectoryPath = GetDirectoryPath(x.FilePath)
            })
            .ToList();
        var siblingSeasonDirectories = directoryGroups
            .GroupBy(x => new
            {
                x.Key.SourceConnectionId,
                ParentPath = GetDirectoryPath(x.Key.DirectoryPath)
            })
            .Where(x => x.Count(y => TvEpisodeFileNameParser.IsSeasonFolderName(GetFolderName(y.Key.DirectoryPath))) >= 2)
            .SelectMany(x => x.Select(y => BuildDirectoryKey(y.Key.SourceConnectionId, y.Key.DirectoryPath)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allDirectoryPaths = directoryGroups
            .Select(x => x.Key.DirectoryPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var group in directoryGroups)
        {
            var directoryPath = group.Key.DirectoryPath;
            var folderName = GetFolderName(directoryPath);
            var folderSeasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(folderName);
            var isSeasonFolder = TvEpisodeFileNameParser.IsSeasonFolderName(folderName);
            var hasSiblingSeasonDirectory = siblingSeasonDirectories.Contains(BuildDirectoryKey(group.Key.SourceConnectionId, directoryPath));
            var titleHint = BuildSeriesTitleHint(directoryPath, folderName);
            var groupFiles = group.ToList();
            var directoryContext = AnalyzeLocalDirectoryContext(
                folderName,
                isSeasonFolder,
                hasSiblingSeasonDirectory,
                groupFiles);
            var parsedFiles = groupFiles
                .Select(
                    file => new
                    {
                        File = file,
                        Parse = TvEpisodeFileNameParser.Parse(
                            file.FileName,
                            allowSeasonContextOnly: true,
                            seasonNumberHint: folderSeasonNumber,
                            allowStrongContextFallbacks: directoryContext.IsStrong)
                    })
                .ToList();
            var validEpisodeCount = parsedFiles.Count(x => x.Parse.IsEpisodeLike && !x.Parse.IsMultiEpisode && x.Parse.EpisodeNumber > 0);
            if (!directoryContext.BlocksMovieFallback && validEpisodeCount < 2)
            {
                continue;
            }

            foreach (var item in parsedFiles.Where(x => x.Parse.IsEpisodeLike && !x.Parse.IsMultiEpisode && x.Parse.EpisodeNumber > 0))
            {
                result.AddOrUpdateHint(
                    new TvScanFileHint
                    {
                        MediaFileId = item.File.Id,
                        SeriesTitleHint = titleHint,
                        SeasonNumberHint = Math.Max(1, item.Parse.SeasonNumber),
                        EpisodeNumberHint = item.Parse.EpisodeNumber,
                        Confidence = directoryContext.IsStrong ? "high" : "medium",
                        Source = "local",
                        Reason = directoryContext.IsStrong ? "strong-tv-directory" : "episode-pattern",
                        Evidence = directoryContext.EvidenceText,
                        IsStrongTvContext = directoryContext.IsStrong,
                        BlocksMovieFallback = directoryContext.BlocksMovieFallback
                    });
            }

            if (ShouldEmitAiCandidateRange(directoryContext))
            {
                if (directoryContext.BlocksMovieFallback)
                {
                    foreach (var item in parsedFiles.Where(x => !x.Parse.IsEpisodeLike || x.Parse.IsMultiEpisode || x.Parse.EpisodeNumber <= 0))
                    {
                        result.AddOrUpdateHint(
                            new TvScanFileHint
                            {
                                MediaFileId = item.File.Id,
                                SeriesTitleHint = titleHint,
                                SeasonNumberHint = folderSeasonNumber,
                                Confidence = directoryContext.IsStrong ? "high" : "medium",
                                Source = "local",
                                Reason = directoryContext.IsStrong ? "strong-tv-directory" : "movie-fallback-blocked-by-tv-risk",
                                Evidence = directoryContext.EvidenceText,
                                IsStrongTvContext = directoryContext.IsStrong,
                                BlocksMovieFallback = true
                            });
                    }
                }

                var aiCandidateRange = BuildAiCandidateRange(
                    directoryPath,
                    groupFiles,
                    titleHint,
                    directoryContext,
                    allDirectoryPaths);
                result.AddAiCandidateRange(aiCandidateRange);
                ScanIdentificationDiagnostics.Write(
                    $"event=ai-candidate-range directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} rangeType={ScanIdentificationDiagnostics.FormatValue(aiCandidateRange.RangeType)} riskTags={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.RiskTags))} sourceFiles={aiCandidateRange.SourceFileCount} directVideoCount={aiCandidateRange.DirectVideoCount} childFolderCount={aiCandidateRange.ChildFolderCount} rangeMediaFileCount={aiCandidateRange.MediaFileIds.Count} rangeHasMediaFiles={(aiCandidateRange.MediaFileIds.Count > 0).ToString().ToLowerInvariant()} sampleDirectVideoFiles={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.SampleDirectVideoFiles))} suspectedSeriesFolder={ScanIdentificationDiagnostics.FormatValue(aiCandidateRange.SuspectedSeriesFolder)} suspectedSeasonFolder={ScanIdentificationDiagnostics.FormatValue(aiCandidateRange.SuspectedSeasonFolder)} usableCandidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.UsableCandidateQueries))} rejectedCandidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.RejectedCandidateQueries))} noisyCandidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.NoisyCandidateQueries))} candidateQueries={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.CandidateQueries))} blockedMovieFallbackCount={aiCandidateRange.BlockedMovieFallbackCount} candidateConflictsCount={aiCandidateRange.CandidateConflictsCount} chineseStructureHints={ScanIdentificationDiagnostics.FormatValue(string.Join('|', aiCandidateRange.ChineseStructureHints))} titleNumberSequenceCandidate={(directoryContext.TitleNumberSequence.IsSequence && string.Equals(directoryContext.TitleNumberSequence.Pattern, "title-number", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} bracketEpisodeSegmentCandidate={(directoryContext.TitleNumberSequence.IsSequence && string.Equals(directoryContext.TitleNumberSequence.Pattern, "bracket-episode-segment", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} bracketEpisodeSegmentAddedToAiCandidateRanges={(directoryContext.TitleNumberSequence.IsSequence && string.Equals(directoryContext.TitleNumberSequence.Pattern, "bracket-episode-segment", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} titleNumberSequenceCandidateRange={(directoryContext.TitleNumberSequence.IsSequence ? $"{directoryContext.TitleNumberSequence.StartNumber}-{directoryContext.TitleNumberSequence.EndNumber}" : "(none)")} titleNumberSequencePrefix={ScanIdentificationDiagnostics.FormatValue(directoryContext.TitleNumberSequence.PrefixKey)} titleNumberSequenceStart={directoryContext.TitleNumberSequence.StartNumber} titleNumberSequenceEnd={directoryContext.TitleNumberSequence.EndNumber} titleNumberSequenceFiles={directoryContext.TitleNumberSequence.FileCount} addedToAiCandidateRanges=true aiCandidateRangeReason={(directoryContext.TitleNumberSequence.IsSequence ? directoryContext.TitleNumberSequence.Pattern : "local-tv-risk")}");
            }
            else if (directoryContext.BlocksMovieFallback)
            {
                foreach (var item in parsedFiles.Where(x => !x.Parse.IsEpisodeLike || x.Parse.IsMultiEpisode || x.Parse.EpisodeNumber <= 0))
                {
                    result.AddOrUpdateHint(
                        new TvScanFileHint
                        {
                            MediaFileId = item.File.Id,
                            SeriesTitleHint = titleHint,
                            SeasonNumberHint = folderSeasonNumber,
                            Confidence = directoryContext.IsStrong ? "high" : "medium",
                            Source = "local",
                            Reason = directoryContext.IsStrong ? "strong-tv-directory" : "movie-fallback-blocked-by-tv-risk",
                            Evidence = directoryContext.EvidenceText,
                            IsStrongTvContext = directoryContext.IsStrong,
                            BlocksMovieFallback = true
                        });
                }
            }

            ScanIdentificationDiagnostics.Write(
                $"event=tv-range-local directory={ScanIdentificationDiagnostics.FormatPath(directoryPath)} titleHint={ScanIdentificationDiagnostics.FormatValue(titleHint)} strong={directoryContext.IsStrong.ToString().ToLowerInvariant()} blocksMovieFallback={directoryContext.BlocksMovieFallback.ToString().ToLowerInvariant()} seasonFolder={isSeasonFolder.ToString().ToLowerInvariant()} siblingSeason={hasSiblingSeasonDirectory.ToString().ToLowerInvariant()} explicitEpisodes={directoryContext.ExplicitEpisodeCount} contextEpisodes={directoryContext.ContextEpisodeCount} strongFallbackEpisodes={directoryContext.StrongFallbackEpisodeCount} validEpisodes={validEpisodeCount} titleNumberSequenceCandidate={(directoryContext.TitleNumberSequence.IsSequence && string.Equals(directoryContext.TitleNumberSequence.Pattern, "title-number", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} bracketEpisodeSegmentCandidate={(directoryContext.TitleNumberSequence.IsSequence && string.Equals(directoryContext.TitleNumberSequence.Pattern, "bracket-episode-segment", StringComparison.OrdinalIgnoreCase)).ToString().ToLowerInvariant()} titleNumberSequencePrefix={ScanIdentificationDiagnostics.FormatValue(directoryContext.TitleNumberSequence.PrefixKey)} titleNumberSequenceStart={directoryContext.TitleNumberSequence.StartNumber} titleNumberSequenceEnd={directoryContext.TitleNumberSequence.EndNumber} titleNumberSequenceFiles={directoryContext.TitleNumberSequence.FileCount} addedToAiCandidateRanges={(ShouldEmitAiCandidateRange(directoryContext)).ToString().ToLowerInvariant()} aiCandidateRangeReason={(directoryContext.TitleNumberSequence.IsSequence ? directoryContext.TitleNumberSequence.Pattern : "(none)")} strongTvEvidenceCount={directoryContext.StrongEvidence.Count} strongTvEvidence={ScanIdentificationDiagnostics.FormatValue(directoryContext.EvidenceText)} weakTvContextReason={ScanIdentificationDiagnostics.FormatValue(directoryContext.WeakReasonText)}");
        }
    }

    private static LocalTvDirectoryContext AnalyzeLocalDirectoryContext(
        string folderName,
        bool isSeasonFolder,
        bool hasSiblingSeasonDirectory,
        IReadOnlyList<ScanFile> groupFiles)
    {
        var folderSeasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(folderName);
        var explicitEpisodeCount = groupFiles.Count(x => TvEpisodeFileNameParser.Parse(x.FileName).IsEpisodeLike);
        var contextEpisodeCount = groupFiles.Count(
            x => TvEpisodeFileNameParser.Parse(
                    x.FileName,
                    allowSeasonContextOnly: true,
                    seasonNumberHint: folderSeasonNumber,
                    allowStrongContextFallbacks: false)
                .IsEpisodeLike);
        var strongFallbackEpisodeCount = groupFiles.Count(
            x => TvEpisodeFileNameParser.Parse(
                    x.FileName,
                    allowSeasonContextOnly: true,
                    seasonNumberHint: folderSeasonNumber,
                    allowStrongContextFallbacks: true)
                .IsEpisodeLike);
        var bareNumberCount = groupFiles.Count(x => TvEpisodeFileNameParser.IsBareNumberEpisodeFileName(x.FileName));
        var titleNumberCount = groupFiles.Count(x => TvEpisodeFileNameParser.IsTitleNumberEpisodeFileName(x.FileName));
        var hasTitleNumberSequence = TvEpisodeFileNameParser.TryAnalyzeTitleNumberSequence(
            groupFiles.Select(x => x.FileName),
            out var titleNumberSequence);
        var hasEpisodeSequence = TvEpisodeFileNameParser.TryAnalyzeEpisodeSequence(
            groupFiles.Select(x => x.FileName),
            out var episodeSequence);
        var hasBracketEpisodeSequence = hasEpisodeSequence
                                        && string.Equals(
                                            episodeSequence.Pattern,
                                            "bracket-episode-segment",
                                            StringComparison.OrdinalIgnoreCase);
        var sequentialEpisodeDirectory = LooksLikeSequentialEpisodeDirectory(groupFiles);
        var hasChineseSeasonHint = TvEpisodeFileNameParser.HasChineseSeasonHint(folderName);
        var hasChineseCountHint = TvEpisodeFileNameParser.HasChineseCountHint(folderName);
        var hasCountSequentialRisk = hasChineseCountHint && (sequentialEpisodeDirectory || bareNumberCount >= 2);

        var strongEvidence = new List<string>();
        var weakReasons = new List<string>();
        if (isSeasonFolder)
        {
            strongEvidence.Add("season-folder");
        }

        if (hasSiblingSeasonDirectory)
        {
            strongEvidence.Add("sibling-season-folders");
        }

        AddCountEvidence(strongEvidence, weakReasons, explicitEpisodeCount, "explicit-episode-files", "single-explicit-episode");
        AddCountEvidence(strongEvidence, weakReasons, contextEpisodeCount, "context-episode-files", "single-context-episode");
        if (sequentialEpisodeDirectory && (isSeasonFolder || hasSiblingSeasonDirectory))
        {
            strongEvidence.Add("sequential-files");
        }
        else if (sequentialEpisodeDirectory)
        {
            weakReasons.Add("sequential-files-without-season-context");
        }

        if (bareNumberCount >= 2 && (isSeasonFolder || hasSiblingSeasonDirectory))
        {
            strongEvidence.Add("numeric-files");
        }
        else if (bareNumberCount > 0)
        {
            weakReasons.Add("weak-numeric-files");
        }

        if (titleNumberCount >= 2 && (isSeasonFolder || hasSiblingSeasonDirectory))
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

        if (hasBracketEpisodeSequence)
        {
            weakReasons.Add("bracket-episode-segment");
        }

        if (hasChineseSeasonHint)
        {
            weakReasons.Add("chinese-season-hint");
        }

        if (hasChineseCountHint)
        {
            weakReasons.Add("chinese-count-hint");
        }

        if (hasCountSequentialRisk)
        {
            weakReasons.Add("count-hint-sequential-files");
        }

        var hasEpisodeEvidence = explicitEpisodeCount >= 2
                                 || contextEpisodeCount >= 2
                                 || (sequentialEpisodeDirectory && (isSeasonFolder || hasSiblingSeasonDirectory))
                                 || (bareNumberCount >= 2 && (isSeasonFolder || hasSiblingSeasonDirectory))
                                 || (titleNumberCount >= 2 && (isSeasonFolder || hasSiblingSeasonDirectory));
        var isStrong = strongEvidence.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2 && hasEpisodeEvidence;
        var blocksMovieFallback = isStrong
                                  || hasCountSequentialRisk
                                  || ((isSeasonFolder || hasSiblingSeasonDirectory)
                                      && (explicitEpisodeCount + contextEpisodeCount + bareNumberCount + titleNumberCount > 0 || groupFiles.Count >= 2))
                                  || explicitEpisodeCount >= 2
                                  || contextEpisodeCount >= 2
                                  || hasTitleNumberSequence
                                  || hasBracketEpisodeSequence;
        var verifiedSequence = hasTitleNumberSequence
            ? titleNumberSequence
            : hasBracketEpisodeSequence
                ? episodeSequence
                : TvEpisodeSequenceAnalysis.Empty;

        return new LocalTvDirectoryContext(
            isStrong,
            blocksMovieFallback,
            strongEvidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            weakReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            explicitEpisodeCount,
            contextEpisodeCount,
            strongFallbackEpisodeCount,
            verifiedSequence);
    }

    private static bool ShouldEmitAiCandidateRange(LocalTvDirectoryContext directoryContext)
    {
        if (!directoryContext.BlocksMovieFallback)
        {
            return directoryContext.WeakReasons.Count > 0;
        }

        if (!directoryContext.IsStrong)
        {
            return true;
        }

        return directoryContext.WeakReasons.Any(
            x => string.Equals(x, "count-hint-sequential-files", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(x, "chinese-count-hint", StringComparison.OrdinalIgnoreCase));
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

    private static TvScanAiCandidateRange BuildAiCandidateRange(
        string directoryPath,
        IReadOnlyList<ScanFile> groupFiles,
        string titleHint,
        LocalTvDirectoryContext directoryContext,
        IReadOnlyList<string> allDirectoryPaths)
    {
        var sanitizedPath = SanitizePathForPrompt(directoryPath);
        var folderName = GetFolderName(directoryPath);
        var childFolderNames = SelectChildFolderNames(sanitizedPath, allDirectoryPaths.Select(SanitizePathForPrompt).ToArray());
        var sampleItems = groupFiles
            .OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SanitizedPathItem(x.Id, x.FilePath, SanitizePathForPrompt(x.FilePath)))
            .ToList();
        var sampleFiles = SelectRepresentativeSamples(
            sampleItems,
            CalculateSampleCount(groupFiles.Count));
        var riskTags = BuildAiCandidateRiskTags(titleHint, directoryContext);
        var queryRejectReason = TvEpisodeFileNameParser.GetSeriesSearchQueryRejectReason(titleHint);
        IReadOnlyList<string> candidateQueries = string.IsNullOrWhiteSpace(queryRejectReason)
            ? new[] { titleHint }
            : [];
        IReadOnlyList<string> rejectedCandidateQueries = string.IsNullOrWhiteSpace(queryRejectReason) || string.IsNullOrWhiteSpace(titleHint)
            ? []
            : new[] { $"{titleHint} ({queryRejectReason})" };
        IReadOnlyList<string> noisyCandidateQueries = IsNoisyCandidateQuery(queryRejectReason)
            ? rejectedCandidateQueries
            : [];

        return new TvScanAiCandidateRange
        {
            SanitizedPath = sanitizedPath,
            RangeType = directoryContext.IsStrong ? "local-tv-risk" : "tv-like-uncertain",
            RiskTags = riskTags,
            SourceFileCount = groupFiles.Count,
            DirectVideoCount = groupFiles.Count,
            ChildFolderCount = childFolderNames.Count,
            SampleDirectVideoFiles = sampleFiles,
            SuspectedSeriesFolder = TvEpisodeFileNameParser.IsSeasonFolderName(folderName)
                ? SanitizePathForPrompt(GetDirectoryPath(directoryPath))
                : sanitizedPath,
            SuspectedSeasonFolder = TvEpisodeFileNameParser.IsSeasonFolderName(folderName)
                ? sanitizedPath
                : string.Empty,
            CandidateQueries = candidateQueries,
            UsableCandidateQueries = candidateQueries,
            RejectedCandidateQueries = rejectedCandidateQueries,
            NoisyCandidateQueries = noisyCandidateQueries,
            BlockedMovieFallbackCount = directoryContext.BlocksMovieFallback ? groupFiles.Count : 0,
            CandidateConflictsCount = 0,
            ChineseStructureHints = BuildChineseStructureHints(folderName, groupFiles)
                .ToArray(),
            MediaFileIds = groupFiles
                .Select(x => x.Id)
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToArray()
        };
    }

    private static bool IsNoisyCandidateQuery(string? rejectReason)
    {
        return string.Equals(rejectReason, "quality-only-query", StringComparison.OrdinalIgnoreCase)
               || string.Equals(rejectReason, "codec-only-query", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildAiCandidateRiskTags(
        string titleHint,
        LocalTvDirectoryContext directoryContext)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evidence in directoryContext.StrongEvidence)
        {
            tags.Add(evidence);
        }

        foreach (var reason in directoryContext.WeakReasons)
        {
            tags.Add(reason);
        }

        if (directoryContext.BlocksMovieFallback)
        {
            tags.Add("movie-fallback-blocked-by-tv-risk");
        }

        var queryRejectReason = TvEpisodeFileNameParser.GetSeriesSearchQueryRejectReason(titleHint);
        if (!string.IsNullOrWhiteSpace(queryRejectReason))
        {
            tags.Add(queryRejectReason);
        }

        return tags
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildChineseStructureHints(
        string folderName,
        IReadOnlyList<ScanFile> files)
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddChineseStructureHints(folderName, hints);
        foreach (var file in files.Take(16))
        {
            AddChineseStructureHints(file.FileName, hints);
        }

        return hints
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static void LogFullAiRangeDisabled(
        IReadOnlyList<ScanFile> files,
        TvScanDirectoryAnalysisResult result)
    {
        result.AiAttempted = false;
        result.AiSucceeded = false;
        result.Message = "full-ai-range-disabled";

        var sourceDirectoryCount = files
            .Select(x => GetDirectoryPath(x.FilePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var rangesWithFiles = result.AiCandidateRanges.Count(x => x.MediaFileIds.Count > 0);
        ScanIdentificationDiagnostics.Write(
            $"event=tv-range-ai-disabled fullAiRangeAnalysis=disabled reason=deferred-to-ai-on-uncertain sourceFiles={files.Count} sourceDirs={sourceDirectoryCount} aiCandidateRanges={result.AiCandidateRanges.Count} rangesWithFiles={rangesWithFiles} rangesWithoutFiles={Math.Max(0, result.AiCandidateRanges.Count - rangesWithFiles)} uniqueAiCandidateDirs={result.AiCandidateRanges.Count} mergedRangeCount={result.AiCandidateRangeMergedCount} deduplicatedEntryCount={result.AiCandidateRangeDeduplicatedEntryCount}");
        ScanIdentificationDiagnostics.Write(
            $"event=ai-candidate-ranges-summary count={result.AiCandidateRanges.Count} uniqueAiCandidateDirs={result.AiCandidateRanges.Count} mergedRangeCount={result.AiCandidateRangeMergedCount} deduplicatedEntryCount={result.AiCandidateRangeDeduplicatedEntryCount} rangesWithFiles={rangesWithFiles} rangesWithoutFiles={Math.Max(0, result.AiCandidateRanges.Count - rangesWithFiles)} blockedMovieFallbackFiles={result.MovieFallbackBlockedMediaFileIds.Count} sourceFiles={files.Count}");
    }

    private async Task TryApplyAiHintsAsync(
        IReadOnlyList<ScanFile> files,
        TvScanDirectoryAnalysisResult result,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        result.AiAttempted = true;
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();
        var jsonParseSucceeded = false;
        var responseChars = 0;
        var directoryItems = BuildSanitizedDirectoryItems(files);
        var pathItems = directoryItems.SelectMany(x => x.Files).ToList();
        var prompt = BuildAiPrompt(directoryItems);
        var sourceDirectoryCount = files
            .Select(x => GetDirectoryPath(x.FilePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var promptDirectoryCount = directoryItems.Count;
        var promptSampleFileCount = directoryItems.Sum(x => x.SampleFileNames.Count);
        var representedFileCount = directoryItems.Sum(x => x.Files.Count);
        var promptTokenEstimate = EstimateTokenCount(prompt);
        ScanIdentificationDiagnostics.Write(
            $"event=tv-range-ai-request-start requestId={requestId} sourceFiles={files.Count} representedFiles={representedFileCount} omittedFiles={Math.Max(0, files.Count - representedFileCount)} sourceDirs={sourceDirectoryCount} promptDirs={promptDirectoryCount} directorySummaryCount={promptDirectoryCount} sampleFiles={promptSampleFileCount} maxSamplesPerDirectory={MaxAiSampleFilesPerDirectory} promptChars={prompt.Length} promptTokenEstimate={promptTokenEstimate} timeoutMs={(int)AiRangeTimeout.TotalMilliseconds} scanTokenCanBeCanceled={cancellationToken.CanBeCanceled.ToString().ToLowerInvariant()} scanTokenCanceledBefore={cancellationToken.IsCancellationRequested.ToString().ToLowerInvariant()} schema=directory-ranges-v1");
        try
        {
            var response = await _aiService.GenerateTextAsync(
                "You identify likely TV-series path ranges from a sanitized media tree. Return JSON only.",
                prompt,
                new AiRequestOptions
                {
                    Temperature = 0.1,
                    Timeout = AiRangeTimeout
                },
                cancellationToken);
            stopwatch.Stop();
            responseChars = response?.Length ?? 0;
            ScanIdentificationDiagnostics.Write(
                $"event=tv-range-ai-response requestId={requestId} durationMs={stopwatch.ElapsedMilliseconds} responseChars={responseChars} assistantContentChars={responseChars} empty={string.IsNullOrWhiteSpace(response).ToString().ToLowerInvariant()} scanTokenCanceledAfter={cancellationToken.IsCancellationRequested.ToString().ToLowerInvariant()} schema=directory-ranges-v1");
            if (string.IsNullOrWhiteSpace(response))
            {
                result.Message = "ai-empty-or-not-configured";
                ScanIdentificationDiagnostics.Write($"event=tv-range-ai-skipped requestId={requestId} reason=empty-or-not-configured");
                return;
            }

            var parsed = ParseAiResponse(response);
            jsonParseSucceeded = true;
            ScanIdentificationDiagnostics.Write(
                $"event=tv-range-ai-json requestId={requestId} succeeded=true ranges={parsed.TvRanges.Count} responseChars={responseChars} assistantContentChars={responseChars} schema=directory-ranges-v1");
            if (parsed.TvRanges.Count == 0)
            {
                result.AiSucceeded = true;
                result.Message = "ai-no-tv-ranges";
                ScanIdentificationDiagnostics.Write($"event=tv-range-ai-complete requestId={requestId} ranges=0");
                return;
            }

            var appliedRanges = 0;
            var appliedFiles = 0;
            var ignoredLowConfidenceRanges = 0;
            var evidenceKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var range in parsed.TvRanges)
            {
                foreach (var evidence in NormalizeEvidence(range.Evidence))
                {
                    evidenceKinds.Add(evidence);
                }

                if (!IsAcceptedConfidence(range.Confidence))
                {
                    ignoredLowConfidenceRanges++;
                    ScanIdentificationDiagnostics.Write(
                        $"event=tv-range-ai-rejected requestId={requestId} seriesFolder={ScanIdentificationDiagnostics.FormatValue(range.SeriesFolder)} confidence={ScanIdentificationDiagnostics.FormatValue(range.Confidence)} evidence={ScanIdentificationDiagnostics.FormatValue(FormatEvidence(range.Evidence))} reason=ai-low-confidence");
                    continue;
                }

                var rangeAppliedFiles = ApplyAiDirectoryRange(range, pathItems, result);
                if (rangeAppliedFiles == 0)
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=tv-range-ai-rejected requestId={requestId} seriesFolder={ScanIdentificationDiagnostics.FormatValue(range.SeriesFolder)} confidence={ScanIdentificationDiagnostics.FormatValue(range.Confidence)} evidence={ScanIdentificationDiagnostics.FormatValue(FormatEvidence(range.Evidence))} reason=no-matching-sanitized-directory");
                    continue;
                }

                appliedRanges++;
                appliedFiles += rangeAppliedFiles;
            }

            result.AiSucceeded = true;
            result.Message = $"ai-applied-ranges-{appliedRanges}-files-{appliedFiles}";
            ScanIdentificationDiagnostics.Write(
                $"event=tv-range-ai-complete requestId={requestId} ranges={parsed.TvRanges.Count} appliedTvRanges={appliedRanges} appliedFiles={appliedFiles} ignoredLowConfidenceRanges={ignoredLowConfidenceRanges} evidenceCount={evidenceKinds.Count} evidenceKinds={ScanIdentificationDiagnostics.FormatValue(string.Join('|', evidenceKinds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)))} schema=directory-ranges-v1");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            result.Message = "ai-failed";
            var isCancellation = exception is OperationCanceledException;
            var cancellationOrigin = isCancellation
                ? cancellationToken.IsCancellationRequested
                    ? "scan-token"
                    : stopwatch.Elapsed >= AiRangeTimeout.Subtract(TimeSpan.FromMilliseconds(500))
                        ? "request-timeout-or-provider-timeout"
                        : "unknown-cancellation"
                : "not-cancellation";
            ScanIdentificationDiagnostics.Write(
                $"event=tv-range-ai-error requestId={requestId} durationMs={stopwatch.ElapsedMilliseconds} timeoutMs={(int)AiRangeTimeout.TotalMilliseconds} exceptionType={ScanIdentificationDiagnostics.FormatValue(exception.GetType().Name)} innerExceptionType={ScanIdentificationDiagnostics.FormatValue(exception.InnerException?.GetType().Name)} cancellationOrigin={ScanIdentificationDiagnostics.FormatValue(cancellationOrigin)} scanTokenCanceledAfter={cancellationToken.IsCancellationRequested.ToString().ToLowerInvariant()} responseChars={responseChars} assistantContentChars={responseChars} jsonParseSucceeded={jsonParseSucceeded.ToString().ToLowerInvariant()} schema=directory-ranges-v1 fallbackReason=ai-failed error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 180)}");
        }
    }

    private static AiOnUncertainInputRange BuildAiOnUncertainInputRange(
        int index,
        TvScanAiCandidateRange range)
    {
        return new AiOnUncertainInputRange(
            $"r{index:D3}",
            range.SanitizedPath,
            range.RangeType,
            range.RiskTags.Take(10).ToArray(),
            range.SourceFileCount,
            range.DirectVideoCount,
            range.ChildFolderCount,
            range.SampleDirectVideoFiles.Take(MaxAiOnUncertainSamplesPerRange).ToArray(),
            range.SuspectedSeriesFolder,
            range.SuspectedSeasonFolder,
            range.UsableCandidateQueries.Take(MaxAiOnUncertainQueriesPerRange).Select(StripDiagnosticQuerySuffix).ToArray(),
            range.NoisyCandidateQueries.Take(MaxAiOnUncertainQueriesPerRange).ToArray(),
            range.RejectedCandidateQueries.Take(MaxAiOnUncertainQueriesPerRange).ToArray(),
            range.BlockedMovieFallbackCount,
            range.CandidateConflictsCount,
            range.ChineseStructureHints.Take(8).ToArray(),
            range.MediaFileIds.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray());
    }

    private static string BuildAiOnUncertainPrompt(IReadOnlyList<AiOnUncertainInputRange> ranges)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Input is sanitized. Review only the listed uncertain ranges, not a full media tree.");
        builder.AppendLine("These ranges are already uncertain. Your task is not to decide whether the app can auto-write metadata; your task is to provide the best title hints for a local TMDB TV search.");
        builder.AppendLine("Return strict JSON: {\"ranges\":[{\"inputRangeId\":\"r001\",\"originalLanguageTitle\":\"original-language title or null\",\"englishTitleHint\":\"English/international title or null\",\"localizedTitleHint\":\"Chinese/localized title or null\",\"searchTitle\":\"best TMDB search title, usually identical to originalLanguageTitle\",\"refinedSeriesTitle\":\"legacy fallback title or null\",\"seriesYearHint\":2023,\"seasonYearHint\":2024,\"seasonNumberHint\":1,\"confidence\":\"high|medium|low\",\"needsReview\":false,\"evidence\":[\"original-title-from-filename\"]}]}");
        builder.AppendLine("originalLanguageTitle is the primary output and must be the official native/original title, not a translated, localized, international, romanized, or English marketing title.");
        builder.AppendLine("For Korean series, originalLanguageTitle must be Hangul when known. For Japanese series, use Japanese Kanji/Kana when known. For Chinese series, use Chinese characters when known. For Spanish, French, German, and other Latin-script original languages, use the actual original local-language title, not an English translation.");
        builder.AppendLine("Never copy englishTitleHint into originalLanguageTitle unless the series original language is English. If the only title you know is English/international but the range appears to be non-English, set originalLanguageTitle to null and put that value only in englishTitleHint.");
        builder.AppendLine("searchTitle must prefer originalLanguageTitle. For non-English series, do not set searchTitle to an English/international title when originalLanguageTitle is unknown; leave searchTitle null instead.");
        builder.AppendLine("If you can provide a title hint, return it even when uncertain and express uncertainty with confidence. Do not set needsReview=true only because the input says candidate-conflict, unresolved, dirty-query, or low-confidence.");
        builder.AppendLine("Use needsReview=true only when you cannot provide any credible single title, the range does not look like a TV series, or multiple different series are mixed and cannot be reduced to one title.");
        builder.AppendLine("Only return directory/title/season hints for TV lookup. Do not return episodeFiles. Do not map individual files to season or episode numbers. Do not request TMDB. Do not choose a TMDB candidate. Do not force guesses.");
        builder.AppendLine("Evidence must be short values such as original-title-from-filename, localized-title-from-folder, english-title-from-file, season-folder, numeric-episodes, conflict.");
        builder.AppendLine("Ranges:");
        foreach (var range in ranges)
        {
            builder
                .Append("- id=")
                .Append(range.InputRangeId)
                .Append(" path=")
                .Append(range.SanitizedPath)
                .Append(" type=")
                .Append(range.RangeType)
                .Append(" riskTags=[")
                .Append(string.Join(", ", range.RiskTags.Select(FormatPromptSampleFile)))
                .Append("] sourceFiles=")
                .Append(range.SourceFileCount)
                .Append(" directVideoCount=")
                .Append(range.DirectVideoCount)
                .Append(" childFolderCount=")
                .Append(range.ChildFolderCount)
                .Append(" samples=[")
                .Append(string.Join(", ", range.SampleDirectVideoFiles.Select(FormatPromptSampleFile)))
                .Append("] suspectedSeriesFolder=")
                .Append(FormatPromptSampleFile(range.SuspectedSeriesFolder))
                .Append(" suspectedSeasonFolder=")
                .Append(FormatPromptSampleFile(range.SuspectedSeasonFolder))
                .Append(" usableQueries=[")
                .Append(string.Join(", ", range.UsableCandidateQueries.Select(FormatPromptSampleFile)))
                .Append("] noisyQueries=[")
                .Append(string.Join(", ", range.NoisyCandidateQueries.Select(FormatPromptSampleFile)))
                .Append("] rejectedQueries=[")
                .Append(string.Join(", ", range.RejectedCandidateQueries.Select(FormatPromptSampleFile)))
                .Append("] blockedMovieFallback=")
                .Append(range.BlockedMovieFallbackCount)
                .Append(" candidateConflicts=")
                .Append(range.CandidateConflictsCount)
                .Append(" chineseHints=[")
                .Append(string.Join(", ", range.ChineseStructureHints.Select(FormatPromptSampleFile)))
                .AppendLine("]");
        }

        return builder.ToString();
    }

    private static AiOnUncertainResponse ParseAiOnUncertainResponse(string response)
    {
        var json = ExtractJsonObject(response);
        return JsonSerializer.Deserialize<AiOnUncertainResponse>(
                   json,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   })
               ?? new AiOnUncertainResponse();
    }

    private static AiOnUncertainHintApplyResult ApplyAiOnUncertainHint(
        AiOnUncertainRange hint,
        AiOnUncertainInputRange inputRange,
        IReadOnlyList<SanitizedPathItem> pathItems,
        IReadOnlyDictionary<int, SanitizedPathItem> pathItemsById,
        TvScanDirectoryAnalysisResult result)
    {
        var folderHints = BuildAiOnUncertainFolderHints(hint, inputRange);
        var titleSelection = SelectAiOnUncertainSearchTitle(hint);
        if (string.IsNullOrWhiteSpace(titleSelection.Title))
        {
            return new AiOnUncertainHintApplyResult(0, [], true, 0, "not-resolved", inputRange.MediaFileIds.Count, 0, "no-original-or-search-title");
        }

        var evidence = FirstNonEmpty(FormatEvidence(hint.Evidence), "ai-on-uncertain");
        var rangeMediaFileIds = inputRange.MediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var inputRangeFiles = rangeMediaFileIds
            .Select(x => pathItemsById.TryGetValue(x, out var pathItem) ? pathItem : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
        var filesResolvedBy = inputRangeFiles.Count > 0 ? "mediaFileIds" : "sanitizedPathFallback";
        if (inputRangeFiles.Count == 0)
        {
            inputRangeFiles = pathItems
                .Where(x => IsSameOrDescendantPath(x.SanitizedPath, inputRange.SanitizedPath))
                .ToList();
        }

        if (inputRangeFiles.Count == 0)
        {
            var ignoredReason = rangeMediaFileIds.Length == 0
                ? "no-media-file-ids"
                : "no-files-in-input-range";
            return new AiOnUncertainHintApplyResult(0, [], true, 0, filesResolvedBy, rangeMediaFileIds.Length, 0, ignoredReason);
        }

        var seasonNumberByFileId = new Dictionary<int, int?>();
        var directoryHintMatchCount = 0;
        foreach (var folderHint in folderHints)
        {
            var matchingFiles = inputRangeFiles
                .Where(x => IsSameOrDescendantPath(x.SanitizedPath, folderHint.Path))
                .ToList();
            if (matchingFiles.Count == 0)
            {
                continue;
            }

            directoryHintMatchCount += matchingFiles.Count;
            var seasonNumberHint = folderHint.SeasonNumberHint
                                   ?? TvEpisodeFileNameParser.TryParseSeasonNumber(GetFolderName(folderHint.Path));
            foreach (var pathItem in matchingFiles)
            {
                seasonNumberByFileId[pathItem.Id] = seasonNumberHint;
            }
        }

        var fallbackSeasonNumberHint = ResolveAiOnUncertainFallbackSeasonNumber(hint, inputRange);
        var directoryHintMismatch = folderHints.Count > 0 && directoryHintMatchCount == 0;
        var applied = 0;
        var seenFileIds = new HashSet<int>();
        var appliedMediaFileIds = new List<int>();
        foreach (var pathItem in inputRangeFiles)
        {
            if (!seenFileIds.Add(pathItem.Id))
            {
                continue;
            }

            var seasonNumberHint = seasonNumberByFileId.GetValueOrDefault(pathItem.Id)
                                   ?? fallbackSeasonNumberHint;
            result.AddOrUpdateHint(
                new TvScanFileHint
                {
                    MediaFileId = pathItem.Id,
                    SeriesTitleHint = titleSelection.Title,
                    LocalizedTitleHint = hint.LocalizedTitleHint,
                    OriginalTitleHint = hint.OriginalTitleHint,
                    OriginalLanguageTitle = titleSelection.OriginalLanguageTitle,
                    EnglishTitleHint = hint.EnglishTitleHint,
                    SearchTitle = titleSelection.Title,
                    SearchTitleSource = titleSelection.Source,
                    YearHint = hint.SeriesYearHint ?? hint.YearHint,
                    SeriesYearHint = hint.SeriesYearHint ?? hint.YearHint,
                    SeasonYearHint = hint.SeasonYearHint,
                    SeasonNumberHint = seasonNumberHint,
                    EpisodeNumberHint = null,
                    Confidence = hint.Confidence,
                    Source = "ai-refined-title",
                    Reason = evidence,
                    Evidence = evidence,
                    IsStrongTvContext = true,
                    BlocksMovieFallback = true
                });
            applied++;
            appliedMediaFileIds.Add(pathItem.Id);
        }

        return new AiOnUncertainHintApplyResult(applied, appliedMediaFileIds, directoryHintMismatch, directoryHintMatchCount, filesResolvedBy, rangeMediaFileIds.Length, inputRangeFiles.Count, string.Empty);
    }

    private static AiSearchTitleSelection SelectAiOnUncertainSearchTitle(AiOnUncertainRange hint)
    {
        var originalLanguageTitle = FirstNonEmpty(
            hint.OriginalLanguageTitle,
            hint.OriginalTitleHint);
        if (!string.IsNullOrWhiteSpace(originalLanguageTitle))
        {
            return new AiSearchTitleSelection(
                originalLanguageTitle,
                "original-language",
                originalLanguageTitle,
                OriginalLanguageTitleMissing: false,
                FallbackToEnglishTitle: false,
                FallbackToLocalizedTitle: false);
        }

        var searchTitle = FirstNonEmpty(hint.SearchTitle);
        if (!string.IsNullOrWhiteSpace(searchTitle))
        {
            return new AiSearchTitleSelection(
                searchTitle,
                "search-title",
                string.Empty,
                OriginalLanguageTitleMissing: true,
                FallbackToEnglishTitle: false,
                FallbackToLocalizedTitle: false);
        }

        var englishTitle = FirstNonEmpty(hint.EnglishTitleHint);
        if (!string.IsNullOrWhiteSpace(englishTitle))
        {
            return new AiSearchTitleSelection(
                englishTitle,
                "english-title",
                string.Empty,
                OriginalLanguageTitleMissing: true,
                FallbackToEnglishTitle: true,
                FallbackToLocalizedTitle: false);
        }

        var localizedOrLegacyTitle = FirstNonEmpty(
            hint.LocalizedTitleHint,
            hint.RefinedSeriesTitle,
            hint.SeriesTitleHint);
        if (!string.IsNullOrWhiteSpace(localizedOrLegacyTitle))
        {
            var source = !string.IsNullOrWhiteSpace(hint.LocalizedTitleHint)
                ? "localized-title"
                : "legacy-refined-title";
            return new AiSearchTitleSelection(
                localizedOrLegacyTitle,
                source,
                string.Empty,
                OriginalLanguageTitleMissing: true,
                FallbackToEnglishTitle: false,
                FallbackToLocalizedTitle: string.Equals(source, "localized-title", StringComparison.OrdinalIgnoreCase));
        }

        return new AiSearchTitleSelection(
            string.Empty,
            "none",
            string.Empty,
            OriginalLanguageTitleMissing: true,
            FallbackToEnglishTitle: false,
            FallbackToLocalizedTitle: false);
    }

    private static bool TryResolveAiOnUncertainInputRange(
        AiOnUncertainRange hint,
        IReadOnlyDictionary<string, AiOnUncertainInputRange> inputById,
        IReadOnlyList<AiOnUncertainInputRange> inputRanges,
        out AiOnUncertainInputRange inputRange,
        out string appliedBy)
    {
        if (!string.IsNullOrWhiteSpace(hint.InputRangeId)
            && inputById.TryGetValue(hint.InputRangeId.Trim(), out inputRange!))
        {
            appliedBy = "inputRangeId";
            return true;
        }

        foreach (var path in GetAiOnUncertainHintPaths(hint))
        {
            var normalizedPath = NormalizeAiReturnedPath(path);
            var match = inputRanges.FirstOrDefault(
                x => IsSameOrDescendantPath(x.SanitizedPath, normalizedPath)
                     || IsSameOrDescendantPath(normalizedPath, x.SanitizedPath));
            if (match is not null)
            {
                inputRange = match;
                appliedBy = "fuzzySanitizedPath";
                return true;
            }
        }

        inputRange = null!;
        appliedBy = "none";
        return false;
    }

    private static IEnumerable<string> GetAiOnUncertainHintPaths(AiOnUncertainRange hint)
    {
        if (!string.IsNullOrWhiteSpace(hint.SeriesFolderHint))
        {
            yield return hint.SeriesFolderHint;
        }

        foreach (var seasonHint in hint.SeasonHints.Concat(hint.SeasonFolders))
        {
            var path = FirstNonEmpty(seasonHint.SeasonFolderHint, seasonHint.Path);
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static int? ResolveAiOnUncertainFallbackSeasonNumber(
        AiOnUncertainRange hint,
        AiOnUncertainInputRange inputRange)
    {
        if (hint.SeasonNumberHint.HasValue)
        {
            return hint.SeasonNumberHint;
        }

        var localSeasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(GetFolderName(inputRange.SuspectedSeasonFolder))
                                ?? TvEpisodeFileNameParser.TryParseSeasonNumber(GetFolderName(inputRange.SanitizedPath));
        if (localSeasonNumber.HasValue)
        {
            return localSeasonNumber;
        }

        var distinctAiSeasonNumbers = hint.SeasonHints
            .Concat(hint.SeasonFolders)
            .Select(x => x.SeasonNumberHint ?? x.SeasonNumber)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .Take(2)
            .ToArray();
        return distinctAiSeasonNumbers.Length == 1
            ? distinctAiSeasonNumbers[0]
            : null;
    }

    private static IReadOnlyList<AiDirectoryFolderHint> BuildAiOnUncertainFolderHints(
        AiOnUncertainRange hint,
        AiOnUncertainInputRange inputRange)
    {
        var folderHints = hint.SeasonHints
            .Concat(hint.SeasonFolders)
            .Where(x => !string.IsNullOrWhiteSpace(FirstNonEmpty(x.SeasonFolderHint, x.Path)))
            .Select(
                x => new AiDirectoryFolderHint(
                    NormalizeAiReturnedPath(FirstNonEmpty(x.SeasonFolderHint, x.Path)),
                    x.SeasonNumberHint ?? x.SeasonNumber))
            .ToList();
        if (folderHints.Count > 0)
        {
            return folderHints;
        }

        var seriesFolder = FirstNonEmpty(hint.SeriesFolderHint, inputRange.SuspectedSeasonFolder, inputRange.SanitizedPath);
        return [new AiDirectoryFolderHint(NormalizeAiReturnedPath(seriesFolder), null)];
    }

    private static string StripDiagnosticQuerySuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var bracketIndex = value.LastIndexOf(" [", StringComparison.Ordinal);
        return bracketIndex > 0 ? value[..bracketIndex].Trim() : value.Trim();
    }

    private static IReadOnlyList<SanitizedDirectoryItem> BuildSanitizedDirectoryItems(IReadOnlyList<ScanFile> files)
    {
        var groupedDirectories = files
            .OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SanitizedPathItem(x.Id, x.FilePath, SanitizePathForPrompt(x.FilePath)))
            .GroupBy(x => GetDirectoryPath(x.SanitizedPath), StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var allDirectoryPaths = groupedDirectories
            .Select(x => x.Key)
            .ToArray();

        return groupedDirectories
            .Take(MaxAiDirectoryLines)
            .Select(
                x =>
                {
                    var groupedFiles = x
                        .OrderBy(y => y.SanitizedPath, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var directVideoCount = groupedFiles.Count;
                    var sampleCount = CalculateSampleCount(directVideoCount);
                    var sampleFiles = SelectRepresentativeSamples(groupedFiles, sampleCount);
                    var childFolderNames = SelectChildFolderNames(x.Key, allDirectoryPaths);
                    var episodeLikeSampleCount = groupedFiles.Count(IsEpisodeLikeForSummary);
                    return new SanitizedDirectoryItem(
                        x.Key,
                        groupedFiles,
                        directVideoCount,
                        childFolderNames,
                        sampleFiles,
                        episodeLikeSampleCount);
                })
            .ToList();
    }

    private static string BuildAiPrompt(IReadOnlyList<SanitizedDirectoryItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Input is sanitized. Do not infer exact private paths.");
        builder.AppendLine("Return strict JSON: {\"tvRanges\":[{\"seriesFolder\":\"/path\",\"seriesTitleHint\":\"title\",\"confidence\":\"high|medium|low\",\"evidence\":[\"season-folders\"],\"seasonFolders\":[{\"path\":\"/path\",\"seasonNumberHint\":1}]}]}");
        builder.AppendLine("Only identify directory ranges that look like TV series. Do not classify movies. Do not return episodeFiles. Do not map files to episode numbers.");
        builder.AppendLine("Do not write natural-language reasons. Use short evidence values only: season-folders, episode-like-files, sequential-files, numeric-files, title-folder.");
        builder.AppendLine("Use high or medium only when directory paths clearly form a series/season/episode structure. Use low for weak guesses.");
        builder.AppendLine("Directories:");
        foreach (var item in items)
        {
            builder
                .Append("- dir=")
                .Append(item.SanitizedDirectoryPath)
                .Append(" directVideoCount=")
                .Append(item.DirectVideoCount)
                .Append(" childFolderCount=")
                .Append(item.ChildFolderNames.Count)
                .Append(" childFolders=[")
                .Append(string.Join(", ", item.ChildFolderNames.Select(FormatPromptSampleFile)))
                .Append("] episodeLikeSampleCount=")
                .Append(item.EpisodeLikeFileCount)
                .Append(" sampleVideoFiles=[")
                .Append(string.Join(", ", item.SampleFileNames.Select(FormatPromptSampleFile)))
                .AppendLine("]");
        }

        return builder.ToString();
    }

    private static AiTvRangeResponse ParseAiResponse(string response)
    {
        var json = ExtractJsonObject(response);
        return JsonSerializer.Deserialize<AiTvRangeResponse>(
                   json,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   })
               ?? new AiTvRangeResponse();
    }

    private static string ExtractJsonObject(string value)
    {
        var trimmed = value.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new JsonException("AI response did not contain a JSON object.");
        }

        return trimmed[start..(end + 1)];
    }

    private static int ApplyAiDirectoryRange(
        AiTvRange range,
        IReadOnlyList<SanitizedPathItem> pathItems,
        TvScanDirectoryAnalysisResult result)
    {
        var folders = range.SeasonFolders.Count > 0
            ? range.SeasonFolders
                .Where(x => !string.IsNullOrWhiteSpace(x.Path))
                .Select(
                    x => new AiDirectoryFolderHint(
                        NormalizeAiReturnedPath(x.Path),
                        x.SeasonNumberHint ?? x.SeasonNumber))
                .ToList()
            : [];
        if (folders.Count == 0 && !string.IsNullOrWhiteSpace(range.SeriesFolder))
        {
            folders.Add(new AiDirectoryFolderHint(NormalizeAiReturnedPath(range.SeriesFolder), null));
        }

        var applied = 0;
        var seenFileIds = new HashSet<int>();
        foreach (var folder in folders)
        {
            var matchingFiles = pathItems
                .Where(x => IsSameOrDescendantPath(x.SanitizedPath, folder.Path))
                .ToList();
            if (matchingFiles.Count == 0)
            {
                continue;
            }

            var seasonNumberHint = folder.SeasonNumberHint
                                   ?? TvEpisodeFileNameParser.TryParseSeasonNumber(GetFolderName(folder.Path));
            foreach (var pathItem in matchingFiles)
            {
                if (!seenFileIds.Add(pathItem.Id))
                {
                    continue;
                }

                var originalDirectoryPath = GetDirectoryPath(pathItem.OriginalPath);
                result.AddOrUpdateHint(
                    new TvScanFileHint
                    {
                        MediaFileId = pathItem.Id,
                        SeriesTitleHint = FirstNonEmpty(
                            range.SeriesTitleHint,
                            BuildSeriesTitleHint(originalDirectoryPath, GetFolderName(originalDirectoryPath))),
                        SeasonNumberHint = seasonNumberHint,
                        EpisodeNumberHint = null,
                        Confidence = range.Confidence,
                        Source = "ai",
                        Reason = FirstNonEmpty(FormatEvidence(range.Evidence), "ai-directory-range"),
                        Evidence = FirstNonEmpty(FormatEvidence(range.Evidence), "ai-directory-range"),
                        IsStrongTvContext = true,
                        BlocksMovieFallback = true
                    });
                applied++;
            }
        }

        return applied;
    }

    private static int CalculateSampleCount(int directVideoCount)
    {
        if (directVideoCount <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(directVideoCount * 0.10d), 1, MaxAiSampleFilesPerDirectory);
    }

    private static IReadOnlyList<string> SelectRepresentativeSamples(
        IReadOnlyList<SanitizedPathItem> files,
        int sampleCount)
    {
        if (sampleCount <= 0 || files.Count == 0)
        {
            return [];
        }

        var ordered = files
            .OrderBy(x => x.SanitizedPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selected = new List<SanitizedPathItem>();

        AddSpreadSamples(ordered.Where(IsExplicitEpisodeLikeForSummary).ToList(), selected, sampleCount);
        AddSpreadSamples(ordered.Where(IsBareNumberLikeForSummary).ToList(), selected, sampleCount);
        AddSpreadSamples(ordered.Where(IsTitleNumberLikeForSummary).ToList(), selected, sampleCount);
        AddSpreadSamples(ordered, selected, sampleCount);

        return selected
            .Take(sampleCount)
            .Select(x => GetFolderName(x.SanitizedPath))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static void AddSpreadSamples(
        IReadOnlyList<SanitizedPathItem> candidates,
        List<SanitizedPathItem> selected,
        int sampleCount)
    {
        if (selected.Count >= sampleCount || candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in PickSpread(candidates))
        {
            if (selected.Count >= sampleCount)
            {
                return;
            }

            if (selected.All(x => x.Id != candidate.Id))
            {
                selected.Add(candidate);
            }
        }
    }

    private static IEnumerable<SanitizedPathItem> PickSpread(IReadOnlyList<SanitizedPathItem> candidates)
    {
        if (candidates.Count == 0)
        {
            yield break;
        }

        var indexes = new[]
            {
                0,
                candidates.Count / 2,
                candidates.Count - 1,
                candidates.Count / 4,
                candidates.Count * 3 / 4
            }
            .Where(x => x >= 0 && x < candidates.Count)
            .Distinct()
            .ToArray();
        foreach (var index in indexes)
        {
            yield return candidates[index];
        }
    }

    private static IReadOnlyList<string> SelectChildFolderNames(
        string directoryPath,
        IReadOnlyList<string> allDirectoryPaths)
    {
        var normalizedDirectoryPath = NormalizeAiReturnedPath(directoryPath);
        return allDirectoryPaths
            .Select(x => GetImmediateChildFolderName(normalizedDirectoryPath, x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(MaxAiChildFolderNamesPerDirectory)
            .ToArray();
    }

    private static string GetImmediateChildFolderName(string parentDirectoryPath, string candidateDirectoryPath)
    {
        var normalizedCandidatePath = NormalizeAiReturnedPath(candidateDirectoryPath);
        if (!normalizedCandidatePath.StartsWith(parentDirectoryPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var remaining = normalizedCandidatePath[(parentDirectoryPath.Length + 1)..];
        if (string.IsNullOrWhiteSpace(remaining) || remaining.Contains('/'))
        {
            return string.Empty;
        }

        return remaining;
    }

    private static bool IsEpisodeLikeForSummary(SanitizedPathItem item)
    {
        return TvEpisodeFileNameParser.Parse(
                GetFolderName(item.SanitizedPath),
                allowSeasonContextOnly: true,
                allowStrongContextFallbacks: true)
            .IsEpisodeLike;
    }

    private static bool IsExplicitEpisodeLikeForSummary(SanitizedPathItem item)
    {
        var parse = TvEpisodeFileNameParser.Parse(GetFolderName(item.SanitizedPath));
        return parse.IsEpisodeLike;
    }

    private static bool IsBareNumberLikeForSummary(SanitizedPathItem item)
    {
        return BareNumberFileNameRegex().IsMatch(Path.GetFileNameWithoutExtension(GetFolderName(item.SanitizedPath)));
    }

    private static bool IsTitleNumberLikeForSummary(SanitizedPathItem item)
    {
        var fileName = Path.GetFileNameWithoutExtension(GetFolderName(item.SanitizedPath));
        return !BareNumberFileNameRegex().IsMatch(fileName)
               && TitleTrailingNumberFileNameRegex().IsMatch(fileName);
    }

    private static bool LooksLikeSequentialEpisodeDirectory(IReadOnlyList<ScanFile> files)
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

    private static string BuildSeriesTitleHint(string directoryPath, string folderName)
    {
        if (TvEpisodeFileNameParser.IsSeasonFolderName(folderName))
        {
            return TvEpisodeFileNameParser.CleanSeriesNameCandidate(GetFolderName(GetDirectoryPath(directoryPath)));
        }

        var cleanedFolder = TvEpisodeFileNameParser.CleanSeriesNameCandidate(folderName);
        return string.IsNullOrWhiteSpace(cleanedFolder)
            ? TvEpisodeFileNameParser.CleanSeriesNameCandidate(GetFolderName(GetDirectoryPath(directoryPath)))
            : cleanedFolder;
    }

    private static bool IsAcceptedConfidence(string? confidence)
    {
        return string.Equals(confidence, "high", StringComparison.OrdinalIgnoreCase)
               || string.Equals(confidence, "medium", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> NormalizeEvidence(IReadOnlyList<string>? evidence)
    {
        if (evidence is null || evidence.Count == 0)
        {
            return [];
        }

        return evidence
            .Select(x => x?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static string FormatEvidence(IReadOnlyList<string>? evidence)
    {
        return string.Join("+", NormalizeEvidence(evidence));
    }

    private static string FormatPromptSampleFile(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string NormalizeAiReturnedPath(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path)
            ? "/"
            : path.Replace('\\', '/').Trim();
        normalized = QueryStringRegex().Replace(normalized, string.Empty);
        normalized = normalized.StartsWith("/", StringComparison.Ordinal)
            ? normalized
            : "/" + normalized;
        normalized = DuplicateSlashRegex().Replace(normalized, "/");
        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : normalized;
    }

    private static bool IsSameOrDescendantPath(string filePath, string directoryPath)
    {
        var normalizedFilePath = NormalizeAiReturnedPath(filePath);
        var normalizedDirectoryPath = NormalizeAiReturnedPath(directoryPath);
        return normalizedFilePath.Equals(normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase)
               || normalizedFilePath.StartsWith(normalizedDirectoryPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizePathForPrompt(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Replace('\\', '/').Trim();
        normalized = QueryStringRegex().Replace(normalized, string.Empty);
        normalized = UrlAuthorityRegex().Replace(normalized, "/");
        normalized = WindowsDriveRegex().Replace(normalized, "/");
        normalized = SecretAssignmentRegex().Replace(normalized, "$1=<redacted>");
        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (segments.Length == 0)
        {
            return "/";
        }

        var tail = segments.Skip(Math.Max(0, segments.Length - 5)).ToArray();
        return segments.Length > 5
            ? "/.../" + string.Join("/", tail)
            : "/" + string.Join("/", tail);
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

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "unknown" : message.Trim();
    }

    private static int EstimateTokenCount(string value)
    {
        return string.IsNullOrEmpty(value)
            ? 0
            : (int)Math.Ceiling(value.Length / 4d);
    }

    private sealed record ScanFile(int Id, int SourceConnectionId, string FileName, string FilePath);

    private sealed record SanitizedPathItem(int Id, string OriginalPath, string SanitizedPath);

    private sealed record SanitizedDirectoryItem(
        string SanitizedDirectoryPath,
        IReadOnlyList<SanitizedPathItem> Files,
        int DirectVideoCount,
        IReadOnlyList<string> ChildFolderNames,
        IReadOnlyList<string> SampleFileNames,
        int EpisodeLikeFileCount);

    private sealed record AiOnUncertainInputRange(
        string InputRangeId,
        string SanitizedPath,
        string RangeType,
        IReadOnlyList<string> RiskTags,
        int SourceFileCount,
        int DirectVideoCount,
        int ChildFolderCount,
        IReadOnlyList<string> SampleDirectVideoFiles,
        string SuspectedSeriesFolder,
        string SuspectedSeasonFolder,
        IReadOnlyList<string> UsableCandidateQueries,
        IReadOnlyList<string> NoisyCandidateQueries,
        IReadOnlyList<string> RejectedCandidateQueries,
        int BlockedMovieFallbackCount,
        int CandidateConflictsCount,
        IReadOnlyList<string> ChineseStructureHints,
        IReadOnlyList<int> MediaFileIds);

    private sealed record LocalTvDirectoryContext(
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

    private sealed record AiDirectoryFolderHint(string Path, int? SeasonNumberHint);

    private sealed record AiOnUncertainHintApplyResult(
        int AppliedFiles,
        IReadOnlyCollection<int> AppliedMediaFileIds,
        bool DirectoryHintMismatch,
        int DirectoryHintMatchCount,
        string FilesResolvedBy,
        int RangeMediaFileCount,
        int FilesResolvedCount,
        string IgnoredReason);

    private sealed record AiSearchTitleSelection(
        string Title,
        string Source,
        string OriginalLanguageTitle,
        bool OriginalLanguageTitleMissing,
        bool FallbackToEnglishTitle,
        bool FallbackToLocalizedTitle);

    private sealed class AiTvRangeResponse
    {
        public List<AiTvRange> TvRanges { get; set; } = [];
    }

    private sealed class AiTvRange
    {
        public string SeriesFolder { get; set; } = string.Empty;

        public string SeriesTitleHint { get; set; } = string.Empty;

        public string Confidence { get; set; } = "low";

        public List<string> Evidence { get; set; } = [];

        public List<AiTvSeasonFolderHint> SeasonFolders { get; set; } = [];
    }

    private sealed class AiTvSeasonFolderHint
    {
        public string Path { get; set; } = string.Empty;

        public int? SeasonNumberHint { get; set; }

        public int? SeasonNumber { get; set; }
    }

    private sealed class AiOnUncertainResponse
    {
        public List<AiOnUncertainRange> Ranges { get; set; } = [];
    }

    private sealed class AiOnUncertainRange
    {
        public string InputRangeId { get; set; } = string.Empty;

        public string SeriesFolderHint { get; set; } = string.Empty;

        public string SeriesTitleHint { get; set; } = string.Empty;

        public string RefinedSeriesTitle { get; set; } = string.Empty;

        public string OriginalLanguageTitle { get; set; } = string.Empty;

        public string EnglishTitleHint { get; set; } = string.Empty;

        public string LocalizedTitleHint { get; set; } = string.Empty;

        public string OriginalTitleHint { get; set; } = string.Empty;

        public string SearchTitle { get; set; } = string.Empty;

        public int? YearHint { get; set; }

        public int? SeriesYearHint { get; set; }

        public int? SeasonYearHint { get; set; }

        public int? SeasonNumberHint { get; set; }

        public string Confidence { get; set; } = "low";

        public List<string> Evidence { get; set; } = [];

        public bool NeedsReview { get; set; }

        public List<AiOnUncertainSeasonHint> SeasonHints { get; set; } = [];

        public List<AiOnUncertainSeasonHint> SeasonFolders { get; set; } = [];
    }

    private sealed class AiOnUncertainSeasonHint
    {
        public string SeasonFolderHint { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public int? SeasonNumberHint { get; set; }

        public int? SeasonNumber { get; set; }
    }

    [GeneratedRegex(@"\?.*$", RegexOptions.CultureInvariant)]
    private static partial Regex QueryStringRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*://[^/]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlAuthorityRegex();

    [GeneratedRegex(@"/+", RegexOptions.CultureInvariant)]
    private static partial Regex DuplicateSlashRegex();

    [GeneratedRegex(@"^\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberFileNameRegex();

    [GeneratedRegex(@"[\p{L}][\p{L}\p{M}\p{N}\s._-]*\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TitleTrailingNumberFileNameRegex();

    [GeneratedRegex(@"^[A-Za-z]:", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDriveRegex();

    [GeneratedRegex(@"(api[_-]?key|access[_-]?token|authorization|bearer|password|pwd|token)\s*[:=]\s*[^\s&]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();
}
