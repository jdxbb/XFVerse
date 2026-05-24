using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class LocalMediaScanService : ILocalMediaScanService
{
    private readonly ISettingsService _settingsService;
    private readonly ITvScanDirectoryAnalysisService _tvScanDirectoryAnalysisService;
    private readonly ITvSeasonIdentificationService _tvSeasonIdentificationService;
    private readonly IMovieIdentificationService _movieIdentificationService;
    private readonly IRescanReattachService _rescanReattachService;
    private readonly IUnknownTvSeasonAppendService _unknownTvSeasonAppendService;
    private readonly ISubtitleBindingService _subtitleBindingService;
    private readonly IAiClassificationService _aiClassificationService;

    public LocalMediaScanService(
        ISettingsService settingsService,
        ITvScanDirectoryAnalysisService tvScanDirectoryAnalysisService,
        ITvSeasonIdentificationService tvSeasonIdentificationService,
        IMovieIdentificationService movieIdentificationService,
        IRescanReattachService rescanReattachService,
        IUnknownTvSeasonAppendService unknownTvSeasonAppendService,
        ISubtitleBindingService subtitleBindingService,
        IAiClassificationService aiClassificationService)
    {
        _settingsService = settingsService;
        _tvScanDirectoryAnalysisService = tvScanDirectoryAnalysisService;
        _tvSeasonIdentificationService = tvSeasonIdentificationService;
        _movieIdentificationService = movieIdentificationService;
        _rescanReattachService = rescanReattachService;
        _unknownTvSeasonAppendService = unknownTvSeasonAppendService;
        _subtitleBindingService = subtitleBindingService;
        _aiClassificationService = aiClassificationService;
    }

    public async Task<ScanOverviewModel> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _settingsService.GetLocalConnectionAsync(cancellationToken);
        if (connection is null)
        {
            return new ScanOverviewModel();
        }

        var enabledScanPaths = (await _settingsService.GetLocalScanPathsAsync(cancellationToken))
            .Where(x => x.IsEnabled)
            .OrderByDescending(x => GetLocalPathDepth(x.Path))
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(
                x => new ScanPathSummaryItem
                {
                    Id = x.Id,
                    DisplayName = x.DisplayName,
                    Path = x.Path,
                    IsRecursive = x.IsRecursive
                })
            .ToList();

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var recentLogs = await dbContext.ScanTaskLogs
            .AsNoTracking()
            .Where(x => x.SourceConnectionId == connection.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(
                x => new ScanTaskLogItem
                {
                    Id = x.Id,
                    ScanPathId = x.ScanPathId,
                    ScanPathDisplayName = x.ScanPath != null ? x.ScanPath.DisplayName : string.Empty,
                    ScanPath = x.ScanPath != null ? x.ScanPath.Path : string.Empty,
                    TaskType = x.TaskType,
                    Status = x.Status,
                    StartedAt = ToLocalDisplayTime(x.StartedAt),
                    EndedAt = x.EndedAt.HasValue ? ToLocalDisplayTime(x.EndedAt.Value) : null,
                    ScannedCount = x.ScannedCount,
                    NewFileCount = x.NewFileCount,
                    UpdatedFileCount = x.UpdatedFileCount,
                    IgnoredFileCount = x.IgnoredFileCount,
                    ErrorCount = x.ErrorCount,
                    ErrorMessage = x.ErrorMessage ?? string.Empty,
                    ReasonSummaryJson = x.ReasonSummaryJson ?? string.Empty,
                    ReasonSummaryText = ScanReasonSummaryFormatter.FormatTotals(x.ReasonSummaryJson),
                    TopReasonSummaryText = ScanReasonSummaryFormatter.FormatTopReasons(x.ReasonSummaryJson, 3)
                })
            .ToListAsync(cancellationToken);

        return new ScanOverviewModel
        {
            HasConnection = true,
            ConnectionName = connection.Name,
            BaseUrl = connection.BaseUrl,
            LastScanAt = connection.LastScanAt.HasValue ? ToLocalDisplayTime(connection.LastScanAt.Value) : null,
            EnabledScanPaths = enabledScanPaths,
            RecentLogs = recentLogs
        };
    }

    public async Task<ScanExecutionResult> RunScanAsync(
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressUpdate>? progress = null)
    {
        var connection = await _settingsService.GetLocalConnectionAsync(cancellationToken);
        if (connection is null)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "请先添加本地目录配置。"
            };
        }

        if (!connection.IsEnabled)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "本地媒体来源已停用。"
            };
        }

        var enabledScanPaths = (await _settingsService.GetLocalScanPathsAsync(cancellationToken))
            .Where(x => x.IsEnabled)
            .OrderByDescending(x => GetLocalPathDepth(x.Path))
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (enabledScanPaths.Count == 0)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "当前没有启用的本地目录。"
            };
        }

        return await RunScanCoreAsync(connection.Id, enabledScanPaths, cancellationToken, progress);
    }

    public async Task<ScanExecutionResult> RunScanPathAsync(
        int scanPathId,
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressUpdate>? progress = null)
    {
        var connection = await _settingsService.GetLocalConnectionAsync(cancellationToken);
        if (connection is null)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "请先添加本地目录配置。"
            };
        }

        var scanPath = (await _settingsService.GetLocalScanPathsAsync(cancellationToken))
            .FirstOrDefault(x => x.Id == scanPathId);

        if (scanPath is null)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "本地目录配置不存在。"
            };
        }

        if (!scanPath.IsEnabled)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "本地目录已停用。"
            };
        }

        return await RunScanCoreAsync(connection.Id, [scanPath], cancellationToken, progress);
    }

    private async Task<ScanExecutionResult> RunScanCoreAsync(
        int sourceConnectionId,
        IReadOnlyList<ScanPath> scanPaths,
        CancellationToken cancellationToken,
        IProgress<ScanProgressUpdate>? progress)
    {
        var progressReporter = new ScanProgressReporter(progress);
        progressReporter.Report("prepare", "准备扫描", force: true);
        var seenFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var postProcessVideoMediaFileIds = new HashSet<int>();
        var reattachCandidateMediaFileIds = new HashSet<int>();
        var localDefaultCandidateMediaFileIds = new HashSet<int>();
        var subtitleBindingVideoMediaFileIds = new HashSet<int>();
        var videoStats = new ScanVideoClassificationStats();
        var completedLogIds = new List<int>();
        var scanStartedAtUtc = DateTime.UtcNow;
        var reasonSummary = new ScanReasonSummaryBuilder();
        var totalResult = new ScanExecutionResult
        {
            ProcessedPathCount = scanPaths.Count
        };

        foreach (var scanPath in scanPaths)
        {
            var pathResult = await ScanSinglePathAsync(
                sourceConnectionId,
                scanPath,
                seenFilePaths,
                postProcessVideoMediaFileIds,
                reattachCandidateMediaFileIds,
                localDefaultCandidateMediaFileIds,
                subtitleBindingVideoMediaFileIds,
                videoStats,
                progressReporter,
                cancellationToken);

            totalResult.TotalScannedCount += pathResult.TotalScannedCount;
            totalResult.NewFileCount += pathResult.NewFileCount;
            totalResult.UpdatedFileCount += pathResult.UpdatedFileCount;
            totalResult.IgnoredFileCount += pathResult.IgnoredFileCount;
            totalResult.ErrorCount += pathResult.ErrorCount;

            if (pathResult.IsCompleted && pathResult.LogId > 0)
            {
                completedLogIds.Add(pathResult.LogId);
            }

            if (pathResult.CanMarkMissing)
            {
                progressReporter.Report("mark-missing", "标记缺失文件", force: true);
                var deletedSubtitleAffectedVideoIds = await MarkMissingFilesDeletedAsync(
                    sourceConnectionId,
                    scanPath,
                    seenFilePaths,
                    cancellationToken);
                foreach (var mediaFileId in deletedSubtitleAffectedVideoIds)
                {
                    subtitleBindingVideoMediaFileIds.Add(mediaFileId);
                }
            }
        }

        var postStage = new PostScanStageResult();
        try
        {
            progressReporter.Report("movie-retry", "Movie 重试候选", force: true);
            var retryMovieResult = await CollectFailedMoviePlaceholderRetryCandidatesAsync(
                sourceConnectionId,
                scanPaths.Select(x => x.Id).ToArray(),
                seenFilePaths,
                scanStartedAtUtc,
                "local",
                cancellationToken);
            foreach (var mediaFileId in retryMovieResult.QueuedMediaFileIds)
            {
                postProcessVideoMediaFileIds.Add(mediaFileId);
            }

            ScanIdentificationDiagnostics.Write(
                $"event=unchanged-unbound-requeued-for-identification source=local count={videoStats.UnchangedUnboundRequeuedForIdentificationCount}");
            var tmdbSearchCache = new ScanTmdbSearchCache();
            ScanIdentificationDiagnostics.Write(
                $"event=scan-identification-stage-start source=local videoIds={postProcessVideoMediaFileIds.Count}");
            progressReporter.Report("tv-directory-analysis", "TV 目录预分析", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            var tvDirectoryAnalysis = await _tvScanDirectoryAnalysisService.AnalyzeAsync(
                postProcessVideoMediaFileIds.ToArray(),
                cancellationToken);
            var aiCandidateRangesWithFiles = tvDirectoryAnalysis.AiCandidateRanges.Count(x => x.MediaFileIds.Count > 0);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-ai-candidate-ranges source=local count={tvDirectoryAnalysis.AiCandidateRanges.Count} uniqueAiCandidateDirs={tvDirectoryAnalysis.AiCandidateRanges.Count} mergedRangeCount={tvDirectoryAnalysis.AiCandidateRangeMergedCount} deduplicatedEntryCount={tvDirectoryAnalysis.AiCandidateRangeDeduplicatedEntryCount} rangesWithFiles={aiCandidateRangesWithFiles} rangesWithoutFiles={Math.Max(0, tvDirectoryAnalysis.AiCandidateRanges.Count - aiCandidateRangesWithFiles)} fullAiRangeAnalysis=disabled reason=deferred-to-ai-on-uncertain");
            var existingEpisodeBindingPreservedCount = await CountExistingEpisodeBindingsAsync(
                postProcessVideoMediaFileIds,
                cancellationToken);
            progressReporter.Report("tv-identification", "TV 识别", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            var tvIdentificationResult = await _tvSeasonIdentificationService.IdentifyMediaFilesAsync(
                postProcessVideoMediaFileIds.ToArray(),
                tvDirectoryAnalysis,
                tmdbSearchCache,
                cancellationToken);
            postStage.Absorb(tvIdentificationResult.Summary);
            var tvHandledMediaFileIds = new HashSet<int>(tvIdentificationResult.HandledMediaFileIds);
            var tvAttemptedCount = tvIdentificationResult.Summary.AttemptedCount;
            var tvBoundCount = tvIdentificationResult.Summary.BoundCount;
            var tvPlaceholderCount = tvIdentificationResult.Summary.PlaceholderCount;
            var tvWarningCount = tvIdentificationResult.Summary.WarningCount;
            var tvErrorCount = tvIdentificationResult.Summary.ErrorCount;
            ScanIdentificationDiagnostics.Write(
                $"event=scan-tv-first-pass-complete source=local tvIdentifyFirstPassRequested={postProcessVideoMediaFileIds.Count} handled={tvHandledMediaFileIds.Count} attempted={tvIdentificationResult.Summary.AttemptedCount} bound={tvIdentificationResult.Summary.BoundCount} placeholders={tvIdentificationResult.Summary.PlaceholderCount} warnings={tvIdentificationResult.Summary.WarningCount} errors={tvIdentificationResult.Summary.ErrorCount}");
            progressReporter.Report("ai-on-uncertain", "AI 不确定项处理", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            var aiOnUncertainApplyResult = await _tvScanDirectoryAnalysisService.ApplyAiOnUncertainAsync(
                postProcessVideoMediaFileIds.ToArray(),
                tvDirectoryAnalysis,
                cancellationToken);
            if (aiOnUncertainApplyResult.HasBatchFailure)
            {
                postStage.AddWarning("AI.OnUncertain", "AI 辅助部分失败，部分项目已保留为未识别/待修正。");
                tvWarningCount++;
                ScanIdentificationDiagnostics.Write(
                    $"event=scan-ai-on-uncertain-warning source=local failedBatches={aiOnUncertainApplyResult.FailedBatchCount} failedRangeCount={aiOnUncertainApplyResult.FailedRangeCount} warning=partial-ai-failure warningsIncludedInErrorCount=false");
            }

            var aiAffectedMediaFileIds = aiOnUncertainApplyResult.AffectedMediaFileIds
                .Where(postProcessVideoMediaFileIds.Contains)
                .Distinct()
                .ToArray();
            if (aiAffectedMediaFileIds.Length > 0)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=scan-tv-ai-on-uncertain-retry source=local appliedFiles={aiOnUncertainApplyResult.AppliedFiles} aiAffectedMediaFiles={aiAffectedMediaFileIds.Length} tvIdentifySecondPassRequested={aiAffectedMediaFileIds.Length} tvIdentifySecondPassScope=ai-affected-files validation=tv-parser-tmdb-safety-gates");
                var aiTvIdentificationResult = await _tvSeasonIdentificationService.IdentifyMediaFilesAsync(
                    aiAffectedMediaFileIds,
                    tvDirectoryAnalysis,
                    tmdbSearchCache,
                    cancellationToken);
                postStage.Absorb(aiTvIdentificationResult.Summary);
                tvHandledMediaFileIds.UnionWith(aiTvIdentificationResult.HandledMediaFileIds);
                tvAttemptedCount += aiTvIdentificationResult.Summary.AttemptedCount;
                tvBoundCount += aiTvIdentificationResult.Summary.BoundCount;
                tvPlaceholderCount += aiTvIdentificationResult.Summary.PlaceholderCount;
                tvWarningCount += aiTvIdentificationResult.Summary.WarningCount;
                tvErrorCount += aiTvIdentificationResult.Summary.ErrorCount;
            }
            else
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=scan-tv-ai-on-uncertain-retry-skipped source=local appliedFiles={aiOnUncertainApplyResult.AppliedFiles} aiAffectedMediaFiles=0 tvIdentifySecondPassRequested=0 tvIdentifySecondPassScope=none tvIdentifySecondPassSkippedReason=no-ai-affected-files");
            }

            ScanIdentificationDiagnostics.WriteFinalAiCandidateRanges("local", tvDirectoryAnalysis);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-tv-stage-complete source=local requested={postProcessVideoMediaFileIds.Count} handled={tvHandledMediaFileIds.Count} attempted={tvAttemptedCount} bound={tvBoundCount} placeholders={tvPlaceholderCount} warnings={tvWarningCount} errors={tvErrorCount}");

            var movieMediaFileIds = postProcessVideoMediaFileIds
                .Except(tvHandledMediaFileIds)
                .Except(tvDirectoryAnalysis.MovieFallbackBlockedMediaFileIds)
                .ToArray();
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-stage-start source=local requested={movieMediaFileIds.Length} movieFallbackBlockedByTvRisk={tvDirectoryAnalysis.MovieFallbackBlockedMediaFileIds.Count}");
            progressReporter.Report("movie-identification", "Movie 识别", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            var identificationResult = await _movieIdentificationService.IdentifyMediaFilesAsync(
                movieMediaFileIds,
                tmdbSearchCache,
                cancellationToken);
            postStage.Absorb(identificationResult);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-stage-complete source=local requested={movieMediaFileIds.Length} attempted={identificationResult.AttemptedCount} bound={identificationResult.BoundCount} placeholders={identificationResult.PlaceholderCount} warnings={identificationResult.WarningCount} errors={identificationResult.ErrorCount}");
            RescanReattachResult reattachResult;
            try
            {
                progressReporter.Report("rescan-reattach", "重扫恢复", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
                reattachResult = await _rescanReattachService.TryReattachAsync(
                    postProcessVideoMediaFileIds.Concat(reattachCandidateMediaFileIds).ToArray(),
                    "local",
                    cancellationToken);
            }
            catch (Exception reattachException)
            {
                reattachResult = new RescanReattachResult();
                postStage.AddWarning("Rescan.Reattach", reattachException.GetType().Name);
                reasonSummary.AddWarning("rescan-reattach-warning", "重扫恢复部分失败", 1);
                ScanIdentificationDiagnostics.Write(
                    $"event=rescan-reattach-error sourceKind=local error={ScanIdentificationDiagnostics.FormatValue(reattachException.GetType().Name)} fallbackToPlaceholderGrouping=true");
            }

            ScanIdentificationDiagnostics.Write(
                $"event=scan-video-classification source=local newVideoCount={videoStats.NewVideoCount} deletedReappearedVideoCount={videoStats.DeletedReappearedVideoCount} changedVideoCount={videoStats.ChangedVideoCount} unchangedUnboundVideoCount={videoStats.UnchangedUnboundVideoCount} unchangedUnboundRequeuedForIdentificationCount={videoStats.UnchangedUnboundRequeuedForIdentificationCount} postProcessVideoCount={postProcessVideoMediaFileIds.Count} reattachCandidateCount={reattachResult.CandidateCount} reattachSucceededCount={reattachResult.SucceededCount} reattachSkippedCount={reattachResult.SkippedCount} placeholderFallbackCount={reattachResult.PlaceholderFallbackCount}");
            UnknownTvSeasonAppendResult unknownAppendResult = new();
            try
            {
                progressReporter.Report("unknown-append", "unknown append", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
                unknownAppendResult = await _unknownTvSeasonAppendService.TryAppendScanPathsAsync(
                    scanPaths.Select(x => x.Id).ToArray(),
                    "local",
                    cancellationToken);
                ScanIdentificationDiagnostics.Write(
                    $"event=scan-unknown-season-append-complete source=local candidateCount={unknownAppendResult.CandidateCount} episodeCandidateCount={unknownAppendResult.EpisodeCandidateCount} succeeded={unknownAppendResult.SucceededCount} skipped={unknownAppendResult.SkippedCount} createdEpisodeCount={unknownAppendResult.CreatedEpisodeCount} appendedSourceCount={unknownAppendResult.AppendedSourceCount}");
            }
            catch (Exception appendException)
            {
                postStage.AddWarning("UnknownTv.Append", FormatExceptionType(appendException));
                reasonSummary.AddWarning("unknown-append-warning", "unknown append 部分失败", 1);
                ScanIdentificationDiagnostics.Write(
                    $"event=unknown-season-append-error sourceKind=local error={ScanIdentificationDiagnostics.FormatValue(FormatExceptionType(appendException))} fallbackToPlaceholderGrouping=true");
            }

            progressReporter.Report("orphan-grouping", "placeholder / orphan 聚合", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            var orphanGroupingResult = await _movieIdentificationService.AggregateUnidentifiedMediaFilesAsync(
                scanPaths.Select(x => x.Id).ToArray(),
                cancellationToken,
                "local");
            ScanIdentificationDiagnostics.Write(
                $"event=tmdb-search-cache-summary source=local tmdbTvSearchCacheHit={tmdbSearchCache.TvSearchCacheHits} tmdbTvSearchCacheMiss={tmdbSearchCache.TvSearchCacheMisses} tmdbMovieSearchCacheHit={tmdbSearchCache.MovieSearchCacheHits} tmdbMovieSearchCacheMiss={tmdbSearchCache.MovieSearchCacheMisses} tmdbTvSearchCacheEntries={tmdbSearchCache.TvSearchCacheEntries} tmdbMovieSearchCacheEntries={tmdbSearchCache.MovieSearchCacheEntries} duplicateSearchAvoided={tmdbSearchCache.DuplicateSearchAvoided}");
            var sourceOutcomes = await CountSourceLevelOutcomesAsync(
                postProcessVideoMediaFileIds,
                cancellationToken);
            AddReasonSummary(
                reasonSummary,
                totalResult,
                videoStats,
                retryMovieResult,
                tvDirectoryAnalysis,
                aiOnUncertainApplyResult,
                sourceOutcomes,
                tvPlaceholderCount,
                identificationResult,
                reattachResult,
                unknownAppendResult,
                orphanGroupingResult,
                existingEpisodeBindingPreservedCount,
                postStage);
        }
        catch (Exception exception)
        {
            postStage.AddError("Identify.Stage", FormatExceptionType(exception));
            AddReasonSummary(reasonSummary, totalResult, videoStats, postStage);
            reasonSummary.AddError("identify-stage-error", "识别阶段异常", 1);
        }

        try
        {
            progressReporter.Report("local-default-source", "本地默认播放源", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            await PromoteLocalDefaultSourcesAsync(localDefaultCandidateMediaFileIds, cancellationToken);
        }
        catch (Exception exception)
        {
            postStage.AddWarning("Local.DefaultSource", FormatExceptionType(exception));
            reasonSummary.AddWarning("local-default-source-warning", "本地默认播放源部分失败", 1);
        }

        try
        {
            progressReporter.Report("subtitle-binding", "字幕绑定重建", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
            await _subtitleBindingService.RebuildBindingsAsync(
                sourceConnectionId,
                subtitleBindingVideoMediaFileIds.ToArray(),
                cancellationToken);
        }
        catch (Exception exception)
        {
            postStage.AddWarning("Subtitle.Binding", FormatExceptionType(exception));
            reasonSummary.AddWarning("subtitle-binding-warning", "字幕绑定重建部分失败", 1);
        }

        if (completedLogIds.Count > 0 && postStage.HasIssues)
        {
            await MarkLogsAsPartialSuccessAsync(completedLogIds, postStage, cancellationToken);
            totalResult.ErrorCount += postStage.ErrorCount;
        }

        if (completedLogIds.Count > 0)
        {
            var completedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(reasonSummary.ToJson()))
            {
                AddReasonSummary(reasonSummary, totalResult, videoStats, postStage);
            }

            await FinalizeCompletedLogsAsync(completedLogIds, completedAt, reasonSummary.ToJson(), cancellationToken);
            await UpdateConnectionLastScanAtAsync(sourceConnectionId, completedAt, cancellationToken);
        }

        QueueMovieClassification(postProcessVideoMediaFileIds);
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-scan-skipped source=local changedVideoFiles={postProcessVideoMediaFileIds.Count} reason=detail-lazy-and-manual-only");

        totalResult.StatusMessage = BuildStatusMessage(totalResult, postStage);
        ScanIdentificationDiagnostics.Write(
            $"event=scan-run-complete source=local scanned={totalResult.TotalScannedCount} new={totalResult.NewFileCount} updated={totalResult.UpdatedFileCount} ignored={totalResult.IgnoredFileCount} errors={totalResult.ErrorCount} warnings={postStage.WarningCount} scanErrorCount={totalResult.ErrorCount} scanWarningCount={postStage.WarningCount} rawWarningCount={postStage.RawWarningCount} tvParseRawWarningCount={postStage.RawWarningCount} warningDedupedCount={Math.Max(0, postStage.RawWarningCount - postStage.WarningCount)} tvParseDeduplicatedWarningCount={postStage.WarningCount} warningsIncludedInErrorCount=false finalScanStatus={(totalResult.ErrorCount > 0 ? "partial-success" : "success-with-warnings-or-success")} backgroundAiClassification=queued");
        progressReporter.Report("complete", "完成", scannedCount: totalResult.TotalScannedCount, newFileCount: totalResult.NewFileCount, updatedFileCount: totalResult.UpdatedFileCount, ignoredFileCount: totalResult.IgnoredFileCount, errorCount: totalResult.ErrorCount, force: true);
        return totalResult;
    }

    private async Task<PathScanExecutionResult> ScanSinglePathAsync(
        int sourceConnectionId,
        ScanPath scanPath,
        HashSet<string> seenFilePaths,
        HashSet<int> postProcessVideoMediaFileIds,
        HashSet<int> reattachCandidateMediaFileIds,
        HashSet<int> localDefaultCandidateMediaFileIds,
        HashSet<int> subtitleBindingVideoMediaFileIds,
        ScanVideoClassificationStats videoStats,
        ScanProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var now = DateTime.UtcNow;
        var log = new ScanTaskLog
        {
            SourceConnectionId = sourceConnectionId,
            ScanPathId = scanPath.Id,
            TaskType = ScanTaskType.Refresh,
            Status = ScanTaskStatus.Running,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ScanTaskLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = new PathScanExecutionResult
        {
            LogId = log.Id
        };

        var localEntries = new List<LocalFileEntry>();
        try
        {
            progressReporter.Report("enumerating-files", "枚举文件", force: true);
            CollectLocalFiles(scanPath, localEntries, result, progressReporter, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await CompletePathLogAsync(
                log,
                result,
                ScanTaskStatus.Cancelled,
                string.Empty,
                dbContext,
                CancellationToken.None);
            throw;
        }
        catch
        {
            result.ErrorCount++;
            result.IgnoredFiles.WriteDiagnostics("local", result.IgnoredFileCount);
            await CompletePathLogAsync(
                log,
                result,
                ScanTaskStatus.Failed,
                "[Local.Directory] 本地目录读取失败",
                dbContext,
                cancellationToken);
            return result;
        }

        if (!result.RootWasReadable)
        {
            result.IgnoredFiles.WriteDiagnostics("local", result.IgnoredFileCount);
            await CompletePathLogAsync(
                log,
                result,
                ScanTaskStatus.Failed,
                "[Local.Directory] 本地目录不可访问",
                dbContext,
                cancellationToken);
            return result;
        }

        try
        {
            progressReporter.Report("comparing-files", "比对文件变化", force: true);
            var existingFiles = await LoadExistingFilesForPathAsync(
                dbContext,
                sourceConnectionId,
                scanPath.Path,
                cancellationToken);
            var discoveredVideoFiles = new List<MediaFile>();
            var changedVideoFiles = new List<MediaFile>();
            var changedSubtitleDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in localEntries.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                progressReporter.Report(
                    "comparing-files",
                    "比对文件变化",
                    entry.FileName,
                    result.TotalScannedCount,
                    result.NewFileCount,
                    result.UpdatedFileCount,
                    result.IgnoredFileCount,
                    result.ErrorCount);
                if (!seenFilePaths.Add(entry.FilePath))
                {
                    result.RecordIgnored("duplicate-path", entry.FileName);
                    continue;
                }

                var isNewFile = false;
                var hasMaterialChange = false;
                var wasDeleted = false;
                if (!existingFiles.TryGetValue(entry.FilePath, out var mediaFile))
                {
                    isNewFile = true;
                    mediaFile = new MediaFile
                    {
                        SourceConnectionId = sourceConnectionId,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.MediaFiles.Add(mediaFile);
                    existingFiles[entry.FilePath] = mediaFile;
                    result.NewFileCount++;
                }
                else
                {
                    wasDeleted = mediaFile.IsDeleted;
                    hasMaterialChange = HasMaterialChange(mediaFile, entry, scanPath.Id);
                    if (!hasMaterialChange)
                    {
                        mediaFile.LastSeenAt = DateTime.UtcNow;
                        if (entry.MediaType == MediaType.Video)
                        {
                            discoveredVideoFiles.Add(mediaFile);
                            if (IsActiveUnboundVideo(mediaFile))
                            {
                                reattachCandidateMediaFileIds.Add(mediaFile.Id);
                                if (postProcessVideoMediaFileIds.Add(mediaFile.Id))
                                {
                                    videoStats.UnchangedUnboundRequeuedForIdentificationCount++;
                                }

                                videoStats.UnchangedUnboundVideoCount++;
                            }
                            else if (!mediaFile.IsDeleted)
                            {
                                videoStats.UnchangedBoundVideoCount++;
                            }
                        }

                        continue;
                    }

                    result.UpdatedFileCount++;
                }

                mediaFile.ScanPathId = scanPath.Id;
                mediaFile.FileName = entry.FileName;
                mediaFile.FilePath = entry.FilePath;
                mediaFile.RemoteUri = null;
                mediaFile.Extension = Path.GetExtension(entry.FileName).ToLowerInvariant();
                mediaFile.FileSize = entry.FileSize;
                mediaFile.LastModifiedAt = entry.LastModifiedAt;
                mediaFile.MediaType = entry.MediaType;
                mediaFile.IsDeleted = false;
                mediaFile.LastSeenAt = DateTime.UtcNow;
                mediaFile.UpdatedAt = DateTime.UtcNow;

                if (entry.MediaType == MediaType.Video)
                {
                    if (isNewFile)
                    {
                        videoStats.NewVideoCount++;
                    }
                    else if (wasDeleted)
                    {
                        videoStats.DeletedReappearedVideoCount++;
                    }
                    else
                    {
                        videoStats.ChangedVideoCount++;
                    }

                    discoveredVideoFiles.Add(mediaFile);
                    changedVideoFiles.Add(mediaFile);
                }
                else if (entry.MediaType == MediaType.Subtitle && (isNewFile || hasMaterialChange))
                {
                    changedSubtitleDirectories.Add(GetDirectoryPath(entry.FilePath));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var mediaFileId in discoveredVideoFiles.Select(x => x.Id).Where(x => x > 0))
            {
                localDefaultCandidateMediaFileIds.Add(mediaFileId);
            }

            foreach (var mediaFileId in changedVideoFiles.Select(x => x.Id).Where(x => x > 0))
            {
                postProcessVideoMediaFileIds.Add(mediaFileId);
                subtitleBindingVideoMediaFileIds.Add(mediaFileId);
            }

            if (changedSubtitleDirectories.Count > 0)
            {
                foreach (var mediaFileId in existingFiles.Values
                             .Where(x => x.MediaType == MediaType.Video
                                         && !x.IsDeleted
                                         && changedSubtitleDirectories.Contains(GetDirectoryPath(x.FilePath)))
                             .Select(x => x.Id)
                             .Where(x => x > 0))
                {
                    subtitleBindingVideoMediaFileIds.Add(mediaFileId);
                }
            }

            result.IsCompleted = true;
            result.CanMarkMissing = result.ErrorCount == 0;
            result.IgnoredFiles.WriteDiagnostics("local", result.IgnoredFileCount);
            await CompletePathLogAsync(
                log,
                result,
                result.ErrorCount == 0 ? ScanTaskStatus.Success : ScanTaskStatus.PartialSuccess,
                result.ErrorCount == 0 ? string.Empty : "[Local.Directory] 部分本地目录或文件无法访问",
                dbContext,
                cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            await CompletePathLogAsync(
                log,
                result,
                ScanTaskStatus.Cancelled,
                string.Empty,
                dbContext,
                CancellationToken.None);
            throw;
        }
        catch
        {
            result.ErrorCount++;
            result.IgnoredFiles.WriteDiagnostics("local", result.IgnoredFileCount);
            await CompletePathLogAsync(
                log,
                result,
                ScanTaskStatus.Failed,
                "[Local.MediaFile] 本地文件入库失败",
                dbContext,
                cancellationToken);
            return result;
        }
    }

    private static void CollectLocalFiles(
        ScanPath scanPath,
        List<LocalFileEntry> entries,
        PathScanExecutionResult result,
        ScanProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        var root = new DirectoryInfo(scanPath.Path);
        if (!root.Exists || ShouldSkipDirectory(root))
        {
            result.ErrorCount++;
            return;
        }

        result.RootWasReadable = true;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            FileInfo[] files;
            try
            {
                files = directory.GetFiles();
            }
            catch
            {
                result.ErrorCount++;
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progressReporter.Report(
                    "enumerating-files",
                    "枚举文件",
                    file.Name,
                    result.TotalScannedCount,
                    result.NewFileCount,
                    result.UpdatedFileCount,
                    result.IgnoredFileCount,
                    result.ErrorCount);
                try
                {
                    if (MediaFileRules.IsIgnoredSystemFile(file.Name))
                    {
                        result.RecordIgnored("macos-resource-fork", file.Name);
                        continue;
                    }

                    if (ShouldSkipFile(file))
                    {
                        result.RecordIgnored("other", file.Name);
                        continue;
                    }

                    var mediaType = MediaFileRules.GetMediaType(file.Name);
                    if (mediaType == MediaType.Other)
                    {
                        result.RecordIgnored("unsupported-extension", file.Name);
                        continue;
                    }

                    result.TotalScannedCount++;
                    entries.Add(
                        new LocalFileEntry(
                            file.Name,
                            file.FullName,
                            file.Length,
                            file.LastWriteTimeUtc,
                            mediaType));
                }
                catch
                {
                    result.ErrorCount++;
                }
            }

            if (!scanPath.IsRecursive)
            {
                continue;
            }

            DirectoryInfo[] directories;
            try
            {
                directories = directory.GetDirectories();
            }
            catch
            {
                result.ErrorCount++;
                continue;
            }

            foreach (var child in directories)
            {
                if (ShouldSkipDirectory(child))
                {
                    result.RecordIgnored("other", child.Name);
                    continue;
                }

                pending.Push(child);
            }
        }
    }

    private static bool ShouldSkipDirectory(DirectoryInfo directory)
    {
        try
        {
            return directory.Attributes.HasFlag(FileAttributes.Hidden)
                   || directory.Attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return true;
        }
    }

    private static bool ShouldSkipFile(FileInfo file)
    {
        try
        {
            return file.Attributes.HasFlag(FileAttributes.Hidden)
                   || file.Attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return true;
        }
    }

    private static async Task<Dictionary<string, MediaFile>> LoadExistingFilesForPathAsync(
        AppDbContext dbContext,
        int sourceConnectionId,
        string scanPath,
        CancellationToken cancellationToken)
    {
        var normalizedScanPath = NormalizeLocalPath(scanPath);
        var candidates = await dbContext.MediaFiles
            .Where(x => x.SourceConnectionId == sourceConnectionId)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(x => IsUnderLocalPath(x.FilePath, normalizedScanPath))
            .ToDictionary(x => x.FilePath, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasMaterialChange(MediaFile mediaFile, LocalFileEntry entry, int scanPathId)
    {
        return mediaFile.ScanPathId != scanPathId
               || !string.Equals(mediaFile.FileName, entry.FileName, StringComparison.Ordinal)
               || !string.IsNullOrWhiteSpace(mediaFile.RemoteUri)
               || mediaFile.FileSize != entry.FileSize
               || mediaFile.LastModifiedAt != entry.LastModifiedAt
               || mediaFile.MediaType != entry.MediaType
               || mediaFile.IsDeleted;
    }

    private static bool IsActiveUnboundVideo(MediaFile mediaFile)
    {
        return mediaFile.MediaType == MediaType.Video
               && !mediaFile.IsDeleted
               && !mediaFile.MovieId.HasValue
               && !mediaFile.EpisodeId.HasValue;
    }

    private static async Task<FailedMoviePlaceholderRetryResult> CollectFailedMoviePlaceholderRetryCandidatesAsync(
        int sourceConnectionId,
        IReadOnlyCollection<int> scanPathIds,
        HashSet<string> seenFilePaths,
        DateTime scanStartedAtUtc,
        string sourceKind,
        CancellationToken cancellationToken)
    {
        if (scanPathIds.Count == 0 || seenFilePaths.Count == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-retry-candidates source={sourceKind} previousTmdbSearchErrorScanPaths=0 candidateCount=0 queuedCount=0 skippedCount=0 reason=no-scan-scope");
            return FailedMoviePlaceholderRetryResult.Empty;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var previousLogs = await dbContext.ScanTaskLogs
            .AsNoTracking()
            .Where(
                x => x.SourceConnectionId == sourceConnectionId
                     && x.ScanPathId.HasValue
                     && scanPathIds.Contains(x.ScanPathId.Value)
                     && x.StartedAt < scanStartedAtUtc)
            .OrderByDescending(x => x.StartedAt)
            .Select(
                x => new RetryScanLogWindow
                {
                    ScanPathId = x.ScanPathId!.Value,
                    StartedAt = x.StartedAt,
                    ErrorCount = x.ErrorCount,
                    ErrorMessage = x.ErrorMessage ?? string.Empty
                })
            .ToListAsync(cancellationToken);

        var retryWindows = previousLogs
            .GroupBy(x => x.ScanPathId)
            .Select(x => x.First())
            .Where(x => x.ErrorCount > 0
                        && x.ErrorMessage.Contains("TMDB.Search", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (retryWindows.Count == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-retry-candidates source={sourceKind} previousTmdbSearchErrorScanPaths=0 candidateCount=0 queuedCount=0 skippedCount=0 reason=no-previous-tmdb-search-error");
            return FailedMoviePlaceholderRetryResult.Empty;
        }

        var retryPathIds = retryWindows.Select(x => x.ScanPathId).Distinct().ToArray();
        var retryWindowsByPath = retryWindows.ToDictionary(x => x.ScanPathId);
        var candidates = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => x.SourceConnectionId == sourceConnectionId
                     && x.ScanPathId.HasValue
                     && retryPathIds.Contains(x.ScanPathId.Value)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.EpisodeId.HasValue
                     && x.MovieId.HasValue
                     && x.Movie != null
                     && !x.Movie.TmdbId.HasValue
                     && x.Movie.IdentificationStatus == IdentificationStatus.Failed)
            .Select(
                x => new FailedMoviePlaceholderRetryCandidate
                {
                    MediaFileId = x.Id,
                    ScanPathId = x.ScanPathId!.Value,
                    FilePath = x.FilePath,
                    MovieUpdatedAt = x.Movie!.UpdatedAt,
                    IsHiddenFromLibrary = dbContext.UserMovieCollectionItems.Any(
                        item => item.MovieId == x.MovieId
                                && item.LibraryVisibilityState == LibraryVisibilityState.Hidden)
                })
            .ToListAsync(cancellationToken);

        var queuedIds = candidates
            .Where(x => !x.IsHiddenFromLibrary)
            .Where(x => seenFilePaths.Contains(x.FilePath))
            .Where(
                x => retryWindowsByPath.TryGetValue(x.ScanPathId, out var retryWindow)
                     && x.MovieUpdatedAt >= retryWindow.StartedAt
                     && x.MovieUpdatedAt < scanStartedAtUtc)
            .Select(x => x.MediaFileId)
            .Distinct()
            .ToArray();

        ScanIdentificationDiagnostics.Write(
            $"event=scan-movie-retry-candidates source={sourceKind} previousTmdbSearchErrorScanPaths={retryWindows.Count} candidateCount={candidates.Count} queuedCount={queuedIds.Length} skippedCount={Math.Max(0, candidates.Count - queuedIds.Length)} hiddenPlaceholderSkippedCount={candidates.Count(x => x.IsHiddenFromLibrary)} hiddenPlaceholderSkipReason={ScanCandidateVisibilityGuard.HiddenFailedPlaceholderSkipReason} reason=previous-tmdb-search-error");

        return new FailedMoviePlaceholderRetryResult(
            queuedIds,
            candidates.Count,
            candidates.Count(x => x.IsHiddenFromLibrary));
    }

    private static async Task<IReadOnlyCollection<int>> MarkMissingFilesDeletedAsync(
        int sourceConnectionId,
        ScanPath scanPath,
        HashSet<string> seenFilePaths,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var normalizedScanPath = NormalizeLocalPath(scanPath.Path);
        var candidates = await dbContext.MediaFiles
            .Where(x => x.SourceConnectionId == sourceConnectionId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var deletedSubtitleDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mediaFile in candidates.Where(x => IsUnderLocalPath(x.FilePath, normalizedScanPath)))
        {
            if (seenFilePaths.Contains(mediaFile.FilePath))
            {
                continue;
            }

            if (mediaFile.MediaType == MediaType.Subtitle)
            {
                deletedSubtitleDirectories.Add(GetDirectoryPath(mediaFile.FilePath));
            }

            mediaFile.IsDeleted = true;
            mediaFile.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (deletedSubtitleDirectories.Count == 0)
        {
            return [];
        }

        return candidates
            .Where(x => x.MediaType == MediaType.Video
                        && !x.IsDeleted
                        && deletedSubtitleDirectories.Contains(GetDirectoryPath(x.FilePath)))
            .Select(x => x.Id)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private async Task ClassifyAffectedMoviesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        if (mediaFileIds.Count == 0)
        {
            return;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movieIds = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => mediaFileIds.Contains(x.Id)
                     && x.MovieId.HasValue
                     && x.Movie != null
                     && x.Movie.TmdbId.HasValue
                     && (x.Movie.IdentificationStatus == IdentificationStatus.Matched
                         || x.Movie.IdentificationStatus == IdentificationStatus.ManualConfirmed))
            .Select(x => x.MovieId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var needsClassificationMovieIds = await dbContext.Movies
            .AsNoTracking()
            .Where(
                x => movieIds.Contains(x.Id)
                     && x.TmdbId.HasValue
                     && (x.IdentificationStatus == IdentificationStatus.Matched
                         || x.IdentificationStatus == IdentificationStatus.ManualConfirmed)
                     && (string.IsNullOrWhiteSpace(x.AiTagsText)
                         || string.IsNullOrWhiteSpace(x.EmotionTagsText)
                         || string.IsNullOrWhiteSpace(x.SceneTagsText)))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await _aiClassificationService.ClassifyMoviesAsync(
            needsClassificationMovieIds,
            "local",
            cancellationToken);
    }

    private void QueueMovieClassification(IReadOnlyCollection<int> mediaFileIds)
    {
        if (mediaFileIds.Count == 0)
        {
            return;
        }

        var ids = mediaFileIds.ToArray();
        ScanIdentificationDiagnostics.Write(
            $"event=scan-ai-classify-queued source=local mediaFiles={ids.Length} mode=background reason=non-blocking-scan-completion");
        _ = Task.Run(
            async () =>
            {
                var startedAt = DateTime.UtcNow;
                try
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=scan-ai-classify-start source=local mediaFiles={ids.Length}");
                    await ClassifyAffectedMoviesAsync(ids, CancellationToken.None);
                    var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                    ScanIdentificationDiagnostics.Write(
                        $"event=scan-ai-classify-complete source=local mediaFiles={ids.Length} durationMs={durationMs}");
                }
                catch (Exception exception)
                {
                    var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                    ScanIdentificationDiagnostics.Write(
                        $"event=scan-ai-classify-error source=local mediaFiles={ids.Length} durationMs={durationMs} error={ScanIdentificationDiagnostics.FormatValue(FormatExceptionType(exception), 180)}");
                }
            });
    }

    private static async Task CompletePathLogAsync(
        ScanTaskLog log,
        PathScanExecutionResult result,
        ScanTaskStatus status,
        string errorMessage,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        log.Status = status;
        log.ScannedCount = result.TotalScannedCount;
        log.NewFileCount = result.NewFileCount;
        log.UpdatedFileCount = result.UpdatedFileCount;
        log.IgnoredFileCount = result.IgnoredFileCount;
        log.ErrorCount = result.ErrorCount;
        log.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage;
        log.ReasonSummaryJson = BuildPathReasonSummaryJson(result, status);
        log.EndedAt = DateTime.UtcNow;
        log.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkLogsAsPartialSuccessAsync(
        IReadOnlyCollection<int> logIds,
        PostScanStageResult postStage,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var logs = await dbContext.ScanTaskLogs
            .Where(x => logIds.Contains(x.Id) && x.Status != ScanTaskStatus.Failed)
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return;
        }

        var summary = postStage.BuildSummary();
        foreach (var log in logs)
        {
            log.Status = ScanTaskStatus.PartialSuccess;
            log.ErrorCount += postStage.ErrorCount;
            log.ErrorMessage = string.IsNullOrWhiteSpace(log.ErrorMessage)
                ? summary
                : $"{log.ErrorMessage}; {summary}";
            log.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task FinalizeCompletedLogsAsync(
        IReadOnlyCollection<int> logIds,
        DateTime completedAt,
        string reasonSummaryJson,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var logs = await dbContext.ScanTaskLogs
            .Where(x => logIds.Contains(x.Id) && x.EndedAt.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var log in logs)
        {
            log.EndedAt = completedAt;
            log.ReasonSummaryJson = string.IsNullOrWhiteSpace(reasonSummaryJson)
                ? log.ReasonSummaryJson
                : reasonSummaryJson;
            log.UpdatedAt = completedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpdateConnectionLastScanAtAsync(
        int connectionId,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var connection = await dbContext.SourceConnections.FirstOrDefaultAsync(x => x.Id == connectionId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        connection.LastScanAt = completedAt;
        connection.UpdatedAt = completedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task PromoteLocalDefaultSourcesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        if (mediaFileIds.Count == 0)
        {
            return;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var candidates = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => mediaFileIds.Contains(x.Id)
                     && x.MovieId.HasValue
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && x.SourceConnection != null
                     && x.SourceConnection.ProtocolType == ProtocolType.Local)
            .Select(
                x => new
                {
                    x.Id,
                    MovieId = x.MovieId!.Value,
                    x.FilePath,
                    x.LastSeenAt,
                    x.UpdatedAt,
                    x.CreatedAt
                })
            .ToListAsync(cancellationToken);

        candidates = candidates
            .Where(x => IsExistingLocalFile(x.FilePath))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var movieIds = candidates.Select(x => x.MovieId).Distinct().ToArray();
        var movies = await dbContext.Movies
            .Where(x => movieIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var updated = false;
        foreach (var group in candidates.GroupBy(x => x.MovieId))
        {
            if (!movies.TryGetValue(group.Key, out var movie))
            {
                continue;
            }

            var preferredLocalSource = group
                .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .First();

            if (movie.DefaultMediaFileId == preferredLocalSource.Id)
            {
                continue;
            }

            movie.DefaultMediaFileId = preferredLocalSource.Id;
            movie.UpdatedAt = DateTime.UtcNow;
            updated = true;
        }

        if (updated)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<int> CountExistingEpisodeBindingsAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var ids = mediaFileIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        return await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.EpisodeId.HasValue && !x.IsDeleted)
            .CountAsync(cancellationToken);
    }

    private static async Task<ScanSourceLevelOutcomes> CountSourceLevelOutcomesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var ids = mediaFileIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return ScanSourceLevelOutcomes.Empty;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var rows = await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.Movie)
            .Where(x => ids.Contains(x.Id)
                        && x.MediaType == MediaType.Video
                        && !x.IsDeleted)
            .Select(
                x => new
                {
                    x.EpisodeId,
                    x.MovieId,
                    MovieStatus = x.Movie != null ? x.Movie.IdentificationStatus : (IdentificationStatus?)null
                })
            .ToListAsync(cancellationToken);

        return new ScanSourceLevelOutcomes(
            rows.Count(x => x.EpisodeId.HasValue),
            rows.Count(x => x.MovieId.HasValue
                            && x.MovieStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed));
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

    private static string BuildPathReasonSummaryJson(PathScanExecutionResult result, ScanTaskStatus status)
    {
        var summary = new ScanReasonSummaryBuilder();
        summary.AddSkipped("ignored-file", "文件已忽略", result.IgnoredFileCount);
        if (status == ScanTaskStatus.Cancelled)
        {
            summary.AddCancelled("scan-cancelled", "用户取消", 1);
        }
        else if (status == ScanTaskStatus.Failed)
        {
            summary.AddError("scan-path-failed", "扫描路径失败", Math.Max(1, result.ErrorCount));
        }
        else
        {
            summary.AddError("scan-path-error", "扫描路径错误", result.ErrorCount);
        }

        return summary.ToJson();
    }

    private static void AddReasonSummary(
        ScanReasonSummaryBuilder summary,
        ScanExecutionResult totalResult,
        ScanVideoClassificationStats videoStats,
        PostScanStageResult postStage)
    {
        summary.AddSkipped("ignored-file", "文件已忽略", totalResult.IgnoredFileCount);
        summary.AddSkipped("unchanged-stable-binding", "未变化且已绑定跳过", videoStats.UnchangedBoundVideoCount);
        summary.AddWarning("post-stage-warning", "识别/元数据警告", postStage.WarningCount);
        summary.AddError("task-error", "任务异常", totalResult.ErrorCount);
    }

    private static void AddReasonSummary(
        ScanReasonSummaryBuilder summary,
        ScanExecutionResult totalResult,
        ScanVideoClassificationStats videoStats,
        FailedMoviePlaceholderRetryResult retryMovieResult,
        TvScanDirectoryAnalysisResult tvDirectoryAnalysis,
        TvScanAiOnUncertainApplyResult aiOnUncertainApplyResult,
        ScanSourceLevelOutcomes sourceOutcomes,
        int tvPlaceholderCount,
        IdentificationRunResult movieIdentificationResult,
        RescanReattachResult reattachResult,
        UnknownTvSeasonAppendResult unknownAppendResult,
        MoviePlaceholderGroupingRunResult orphanGroupingResult,
        int existingEpisodeBindingPreservedCount,
        PostScanStageResult postStage)
    {
        AddReasonSummary(summary, totalResult, videoStats, postStage);
        summary.AddSuccess("tv-source-bound", "TV source 已绑定", sourceOutcomes.EpisodeSourceCount);
        summary.AddSuccess("movie-source-identified", "Movie source 已识别", sourceOutcomes.MatchedMovieSourceCount);
        summary.AddSuccess("rescan-reattach-succeeded", "重扫恢复成功", reattachResult.SucceededCount);
        summary.AddSuccess("unknown-append-succeeded", "追加到未识别季", unknownAppendResult.SucceededCount);
        summary.AddSuccess("placeholder-orphan-grouped", "placeholder/orphan 聚合", orphanGroupingResult.PersistedFiles);
        summary.AddSkipped("unchanged-unbound-requeued", "未变化未绑定重新识别", videoStats.UnchangedUnboundRequeuedForIdentificationCount);
        summary.AddSkipped("existing-episode-binding-preserved", "已有 Episode 绑定保持", existingEpisodeBindingPreservedCount);
        summary.AddSkipped("hidden-placeholder-skipped", "Hidden 项跳过", retryMovieResult.HiddenPlaceholderSkippedCount + orphanGroupingResult.HiddenPlaceholderSkippedCount);
        summary.AddSkipped("movie-fallback-blocked-by-tv-risk", "Movie fallback 被 TV 风险阻止", tvDirectoryAnalysis.MovieFallbackBlockedMediaFileIds.Count);
        summary.AddSkipped("ai-uncertain", "AI 不确定", aiOnUncertainApplyResult.IgnoredHints);
        summary.AddSkipped("confidence-or-review-placeholder", "未识别/待修正", tvPlaceholderCount + movieIdentificationResult.PlaceholderCount);
        summary.AddWarning("ai-request-failed", "AI 请求失败但扫描继续", aiOnUncertainApplyResult.FailedBatchCount);
    }

    private static string BuildStatusMessage(ScanExecutionResult totalResult, PostScanStageResult postStage)
    {
        if (totalResult.ErrorCount == 0 && !postStage.HasIssues)
        {
            return $"本地扫描完成，共扫描 {totalResult.TotalScannedCount} 个媒体文件。";
        }

        if (totalResult.ErrorCount == 0 && postStage.WarningCount > 0)
        {
            return $"本地文件入库完成，共扫描 {totalResult.TotalScannedCount} 个媒体文件；存在 {postStage.WarningCount} 个警告。{postStage.BuildSummary()}";
        }

        if (postStage.HasIssues && totalResult.ErrorCount == postStage.ErrorCount)
        {
            return $"本地文件入库完成，共扫描 {totalResult.TotalScannedCount} 个媒体文件；识别或元数据阶段存在问题。{postStage.BuildSummary()}";
        }

        return $"本地扫描完成，共扫描 {totalResult.TotalScannedCount} 个媒体文件；存在 {totalResult.ErrorCount} 个问题。{postStage.BuildSummary()}";
    }

    private static bool IsUnderLocalPath(string filePath, string scanPath)
    {
        var normalizedFilePath = NormalizeLocalPath(filePath);
        return string.Equals(normalizedFilePath, scanPath, StringComparison.OrdinalIgnoreCase)
               || normalizedFilePath.StartsWith(AppendDirectorySeparator(scanPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLocalPath(string path)
    {
        var fullPath = Path.GetFullPath((path ?? string.Empty).Trim().Trim('"'));
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
               || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex <= 0 ? "/" : normalized[..lastSeparatorIndex];
    }

    private static int GetLocalPathDepth(string path)
    {
        return NormalizeLocalPath(path)
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    private static DateTime ToLocalDisplayTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToLocalTime();
    }

    private static string FormatExceptionType(Exception exception)
    {
        return exception.GetType().Name;
    }

    private sealed record LocalFileEntry(
        string FileName,
        string FilePath,
        long FileSize,
        DateTime? LastModifiedAt,
        MediaType MediaType);

    private sealed class PathScanExecutionResult
    {
        public int LogId { get; set; }

        public bool RootWasReadable { get; set; }

        public bool IsCompleted { get; set; }

        public bool CanMarkMissing { get; set; }

        public int TotalScannedCount { get; set; }

        public int NewFileCount { get; set; }

        public int UpdatedFileCount { get; set; }

        public int IgnoredFileCount { get; set; }

        public int ErrorCount { get; set; }

        public ScanIgnoredFileStats IgnoredFiles { get; } = new();

        public void RecordIgnored(string reason, string fileNameOrPath)
        {
            IgnoredFileCount++;
            IgnoredFiles.Add(reason, fileNameOrPath);
        }
    }

    private sealed class ScanVideoClassificationStats
    {
        public int NewVideoCount { get; set; }

        public int DeletedReappearedVideoCount { get; set; }

        public int ChangedVideoCount { get; set; }

        public int UnchangedUnboundVideoCount { get; set; }

        public int UnchangedUnboundRequeuedForIdentificationCount { get; set; }

        public int UnchangedBoundVideoCount { get; set; }
    }

    private sealed class RetryScanLogWindow
    {
        public int ScanPathId { get; set; }

        public DateTime StartedAt { get; set; }

        public int ErrorCount { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;
    }

    private sealed class FailedMoviePlaceholderRetryCandidate
    {
        public int MediaFileId { get; set; }

        public int ScanPathId { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public DateTime MovieUpdatedAt { get; set; }

        public bool IsHiddenFromLibrary { get; set; }
    }

    private sealed record FailedMoviePlaceholderRetryResult(
        IReadOnlyCollection<int> QueuedMediaFileIds,
        int CandidateCount,
        int HiddenPlaceholderSkippedCount)
    {
        public static FailedMoviePlaceholderRetryResult Empty { get; } = new([], 0, 0);
    }

    private sealed record ScanSourceLevelOutcomes(int EpisodeSourceCount, int MatchedMovieSourceCount)
    {
        public static ScanSourceLevelOutcomes Empty { get; } = new(0, 0);
    }

    private sealed class PostScanStageResult
    {
        private readonly List<string> _messages = [];
        private readonly HashSet<string> _warningKeys = new(StringComparer.OrdinalIgnoreCase);

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public int RawWarningCount { get; private set; }

        public bool HasIssues => IssueCount > 0;

        public int IssueCount => ErrorCount + WarningCount;

        public void Absorb(IdentificationRunResult result)
        {
            ErrorCount += result.ErrorCount;
            RawWarningCount += result.WarningCount;
            var keyedWarningCount = 0;
            foreach (var warningKey in result.WarningKeys.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keyedWarningCount++;
                if (_warningKeys.Add(warningKey))
                {
                    WarningCount++;
                }
            }

            var unkeyedWarningCount = Math.Max(0, result.WarningCount - keyedWarningCount);
            WarningCount += unkeyedWarningCount;
            foreach (var message in result.Messages)
            {
                AddMessage(message);
            }
        }

        public void AddError(string stage, string message)
        {
            ErrorCount++;
            AddMessage($"[{stage}] {message}");
        }

        public void AddWarning(string stage, string message)
        {
            RawWarningCount++;
            WarningCount++;
            AddMessage($"[{stage}] {message}");
        }

        public string BuildSummary()
        {
            if (!HasIssues)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (ErrorCount > 0)
            {
                parts.Add($"错误 {ErrorCount}");
            }

            if (WarningCount > 0)
            {
                parts.Add($"警告 {WarningCount}");
            }

            var detail = _messages.Count > 0 ? $"：{string.Join("；", _messages)}" : string.Empty;
            return $"识别/元数据阶段{string.Join("；", parts)}{detail}";
        }

        private void AddMessage(string message)
        {
            if (_messages.Count >= 5 || _messages.Contains(message, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            _messages.Add(message);
        }
    }
}
