using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class MediaScanService : IMediaScanService
{
    private readonly ISettingsService _settingsService;
    private readonly IWebDavService _webDavService;
    private readonly ITvScanDirectoryAnalysisService _tvScanDirectoryAnalysisService;
    private readonly ITvSeasonIdentificationService _tvSeasonIdentificationService;
    private readonly IMovieIdentificationService _movieIdentificationService;
    private readonly IRescanReattachService _rescanReattachService;
    private readonly ISubtitleBindingService _subtitleBindingService;
    private readonly IAiClassificationService _aiClassificationService;

    public MediaScanService(
        ISettingsService settingsService,
        IWebDavService webDavService,
        ITvScanDirectoryAnalysisService tvScanDirectoryAnalysisService,
        ITvSeasonIdentificationService tvSeasonIdentificationService,
        IMovieIdentificationService movieIdentificationService,
        IRescanReattachService rescanReattachService,
        ISubtitleBindingService subtitleBindingService,
        IAiClassificationService aiClassificationService)
    {
        _settingsService = settingsService;
        _webDavService = webDavService;
        _tvScanDirectoryAnalysisService = tvScanDirectoryAnalysisService;
        _tvSeasonIdentificationService = tvSeasonIdentificationService;
        _movieIdentificationService = movieIdentificationService;
        _rescanReattachService = rescanReattachService;
        _subtitleBindingService = subtitleBindingService;
        _aiClassificationService = aiClassificationService;
    }

    public async Task<ScanOverviewModel> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _settingsService.GetPrimaryConnectionAsync(cancellationToken);
        if (!connection.Id.HasValue)
        {
            return new ScanOverviewModel();
        }

        var enabledScanPaths = (await _settingsService.GetScanPathsAsync(connection.Id.Value, cancellationToken))
            .Where(x => x.IsEnabled)
            .OrderByDescending(x => WebDavPathHelper.MatchPathDepth(x.Path))
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
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
            .Where(x => x.SourceConnectionId == connection.Id.Value)
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
                    ErrorMessage = x.ErrorMessage ?? string.Empty
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

    public async Task<ScanExecutionResult> RunScanAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _settingsService.GetPrimaryConnectionAsync(cancellationToken);
        if (!connection.Id.HasValue)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "请先保存可用的 WebDAV 连接。"
            };
        }

        if (!connection.IsEnabled)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "当前 WebDAV 连接已停用。"
            };
        }

        var enabledScanPaths = (await _settingsService.GetScanPathsAsync(connection.Id.Value, cancellationToken))
            .Where(x => x.IsEnabled)
            .OrderByDescending(x => WebDavPathHelper.MatchPathDepth(x.Path))
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (enabledScanPaths.Count == 0)
        {
            return new ScanExecutionResult
            {
                StatusMessage = "当前没有启用的扫描路径。"
            };
        }

        var seenFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var postProcessVideoMediaFileIds = new HashSet<int>();
        var reattachCandidateMediaFileIds = new HashSet<int>();
        var subtitleBindingVideoMediaFileIds = new HashSet<int>();
        var videoStats = new ScanVideoClassificationStats();
        var successfulLogIds = new List<int>();
        var scanStartedAtUtc = DateTime.UtcNow;
        var totalResult = new ScanExecutionResult
        {
            ProcessedPathCount = enabledScanPaths.Count
        };

        foreach (var scanPath in enabledScanPaths)
        {
            var pathResult = await ScanSinglePathAsync(
                connection,
                scanPath,
                seenFilePaths,
                postProcessVideoMediaFileIds,
                reattachCandidateMediaFileIds,
                subtitleBindingVideoMediaFileIds,
                videoStats,
                cancellationToken);

            totalResult.TotalScannedCount += pathResult.TotalScannedCount;
            totalResult.NewFileCount += pathResult.NewFileCount;
            totalResult.UpdatedFileCount += pathResult.UpdatedFileCount;
            totalResult.IgnoredFileCount += pathResult.IgnoredFileCount;
            totalResult.ErrorCount += pathResult.ErrorCount;

            if (pathResult.IsSuccessful && pathResult.LogId > 0)
            {
                successfulLogIds.Add(pathResult.LogId);
            }
        }

        var postStage = new PostScanStageResult();
        var deletedSubtitleAffectedVideoIds = await MarkMissingFilesDeletedAsync(connection.Id.Value, enabledScanPaths, seenFilePaths, cancellationToken);
        foreach (var mediaFileId in deletedSubtitleAffectedVideoIds)
        {
            subtitleBindingVideoMediaFileIds.Add(mediaFileId);
        }

        try
        {
            var retryMovieMediaFileIds = await CollectFailedMoviePlaceholderRetryCandidatesAsync(
                connection.Id.Value,
                enabledScanPaths.Select(x => x.Id).ToArray(),
                seenFilePaths,
                scanStartedAtUtc,
                "webdav",
                cancellationToken);
            foreach (var mediaFileId in retryMovieMediaFileIds)
            {
                postProcessVideoMediaFileIds.Add(mediaFileId);
            }

            var tmdbSearchCache = new ScanTmdbSearchCache();
            ScanIdentificationDiagnostics.Write(
                $"event=scan-identification-stage-start source=webdav videoIds={postProcessVideoMediaFileIds.Count}");
            var tvDirectoryAnalysis = await _tvScanDirectoryAnalysisService.AnalyzeAsync(
                postProcessVideoMediaFileIds.ToArray(),
                cancellationToken);
            var aiCandidateRangesWithFiles = tvDirectoryAnalysis.AiCandidateRanges.Count(x => x.MediaFileIds.Count > 0);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-ai-candidate-ranges source=webdav count={tvDirectoryAnalysis.AiCandidateRanges.Count} uniqueAiCandidateDirs={tvDirectoryAnalysis.AiCandidateRanges.Count} mergedRangeCount={tvDirectoryAnalysis.AiCandidateRangeMergedCount} deduplicatedEntryCount={tvDirectoryAnalysis.AiCandidateRangeDeduplicatedEntryCount} rangesWithFiles={aiCandidateRangesWithFiles} rangesWithoutFiles={Math.Max(0, tvDirectoryAnalysis.AiCandidateRanges.Count - aiCandidateRangesWithFiles)} fullAiRangeAnalysis=disabled reason=deferred-to-ai-on-uncertain");
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
                $"event=scan-tv-first-pass-complete source=webdav tvIdentifyFirstPassRequested={postProcessVideoMediaFileIds.Count} handled={tvHandledMediaFileIds.Count} attempted={tvIdentificationResult.Summary.AttemptedCount} bound={tvIdentificationResult.Summary.BoundCount} placeholders={tvIdentificationResult.Summary.PlaceholderCount} warnings={tvIdentificationResult.Summary.WarningCount} errors={tvIdentificationResult.Summary.ErrorCount}");
            var aiOnUncertainApplyResult = await _tvScanDirectoryAnalysisService.ApplyAiOnUncertainAsync(
                postProcessVideoMediaFileIds.ToArray(),
                tvDirectoryAnalysis,
                cancellationToken);
            if (aiOnUncertainApplyResult.HasBatchFailure)
            {
                postStage.AddWarning("AI.OnUncertain", "AI 辅助部分失败，部分项目已保留为未识别/待修正。");
                tvWarningCount++;
                ScanIdentificationDiagnostics.Write(
                    $"event=scan-ai-on-uncertain-warning source=webdav failedBatches={aiOnUncertainApplyResult.FailedBatchCount} failedRangeCount={aiOnUncertainApplyResult.FailedRangeCount} warning=partial-ai-failure warningsIncludedInErrorCount=false");
            }

            var aiAffectedMediaFileIds = aiOnUncertainApplyResult.AffectedMediaFileIds
                .Where(postProcessVideoMediaFileIds.Contains)
                .Distinct()
                .ToArray();
            if (aiAffectedMediaFileIds.Length > 0)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=scan-tv-ai-on-uncertain-retry source=webdav appliedFiles={aiOnUncertainApplyResult.AppliedFiles} aiAffectedMediaFiles={aiAffectedMediaFileIds.Length} tvIdentifySecondPassRequested={aiAffectedMediaFileIds.Length} tvIdentifySecondPassScope=ai-affected-files validation=tv-parser-tmdb-safety-gates");
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
                    $"event=scan-tv-ai-on-uncertain-retry-skipped source=webdav appliedFiles={aiOnUncertainApplyResult.AppliedFiles} aiAffectedMediaFiles=0 tvIdentifySecondPassRequested=0 tvIdentifySecondPassScope=none tvIdentifySecondPassSkippedReason=no-ai-affected-files");
            }

            ScanIdentificationDiagnostics.WriteFinalAiCandidateRanges("webdav", tvDirectoryAnalysis);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-tv-stage-complete source=webdav requested={postProcessVideoMediaFileIds.Count} handled={tvHandledMediaFileIds.Count} attempted={tvAttemptedCount} bound={tvBoundCount} placeholders={tvPlaceholderCount} warnings={tvWarningCount} errors={tvErrorCount}");

            var movieMediaFileIds = postProcessVideoMediaFileIds
                .Except(tvHandledMediaFileIds)
                .Except(tvDirectoryAnalysis.MovieFallbackBlockedMediaFileIds)
                .ToArray();
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-stage-start source=webdav requested={movieMediaFileIds.Length} movieFallbackBlockedByTvRisk={tvDirectoryAnalysis.MovieFallbackBlockedMediaFileIds.Count}");
            var identificationResult = await _movieIdentificationService.IdentifyMediaFilesAsync(movieMediaFileIds, tmdbSearchCache, cancellationToken);
            postStage.Absorb(identificationResult);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-stage-complete source=webdav requested={movieMediaFileIds.Length} attempted={identificationResult.AttemptedCount} bound={identificationResult.BoundCount} placeholders={identificationResult.PlaceholderCount} warnings={identificationResult.WarningCount} errors={identificationResult.ErrorCount}");
            RescanReattachResult reattachResult;
            try
            {
                reattachResult = await _rescanReattachService.TryReattachAsync(
                    postProcessVideoMediaFileIds.Concat(reattachCandidateMediaFileIds).ToArray(),
                    "webdav",
                    cancellationToken);
            }
            catch (Exception reattachException)
            {
                reattachResult = new RescanReattachResult();
                postStage.AddWarning("Rescan.Reattach", reattachException.GetType().Name);
                ScanIdentificationDiagnostics.Write(
                    $"event=rescan-reattach-error sourceKind=webdav error={ScanIdentificationDiagnostics.FormatValue(reattachException.GetType().Name)} fallbackToPlaceholderGrouping=true");
            }

            ScanIdentificationDiagnostics.Write(
                $"event=scan-video-classification source=webdav newVideoCount={videoStats.NewVideoCount} deletedReappearedVideoCount={videoStats.DeletedReappearedVideoCount} changedVideoCount={videoStats.ChangedVideoCount} unchangedUnboundVideoCount={videoStats.UnchangedUnboundVideoCount} postProcessVideoCount={postProcessVideoMediaFileIds.Count} reattachCandidateCount={reattachResult.CandidateCount} reattachSucceededCount={reattachResult.SucceededCount} reattachSkippedCount={reattachResult.SkippedCount} placeholderFallbackCount={reattachResult.PlaceholderFallbackCount}");
            await _movieIdentificationService.AggregateUnidentifiedMediaFilesAsync(
                enabledScanPaths.Select(x => x.Id).ToArray(),
                cancellationToken);
            ScanIdentificationDiagnostics.Write(
                $"event=tmdb-search-cache-summary source=webdav tmdbTvSearchCacheHit={tmdbSearchCache.TvSearchCacheHits} tmdbTvSearchCacheMiss={tmdbSearchCache.TvSearchCacheMisses} tmdbMovieSearchCacheHit={tmdbSearchCache.MovieSearchCacheHits} tmdbMovieSearchCacheMiss={tmdbSearchCache.MovieSearchCacheMisses} tmdbTvSearchCacheEntries={tmdbSearchCache.TvSearchCacheEntries} tmdbMovieSearchCacheEntries={tmdbSearchCache.MovieSearchCacheEntries} duplicateSearchAvoided={tmdbSearchCache.DuplicateSearchAvoided}");
        }
        catch (Exception exception)
        {
            postStage.AddError("Identify.Stage", TrimMessage(exception.Message));
        }

        try
        {
            await _subtitleBindingService.RebuildBindingsAsync(connection.Id.Value, subtitleBindingVideoMediaFileIds.ToArray(), cancellationToken);
        }
        catch (Exception exception)
        {
            postStage.AddWarning("Subtitle.Binding", TrimMessage(exception.Message));
        }

        if (successfulLogIds.Count > 0 && postStage.HasIssues)
        {
            await MarkLogsAsPartialSuccessAsync(successfulLogIds, postStage, cancellationToken);
            totalResult.ErrorCount += postStage.ErrorCount;
        }

        if (successfulLogIds.Count > 0)
        {
            var completedAt = DateTime.UtcNow;
            await FinalizeSuccessfulLogsAsync(successfulLogIds, completedAt, cancellationToken);
            await UpdateConnectionLastScanAtAsync(connection.Id.Value, completedAt, cancellationToken);
        }

        QueueMovieClassification(postProcessVideoMediaFileIds);
        ScanIdentificationDiagnostics.Write(
            $"event=media-probe-scan-skipped source=webdav changedVideoFiles={postProcessVideoMediaFileIds.Count} reason=detail-lazy-and-manual-only");

        totalResult.StatusMessage = BuildStatusMessage(totalResult, postStage);
        ScanIdentificationDiagnostics.Write(
            $"event=scan-run-complete source=webdav scanned={totalResult.TotalScannedCount} new={totalResult.NewFileCount} updated={totalResult.UpdatedFileCount} ignored={totalResult.IgnoredFileCount} errors={totalResult.ErrorCount} warnings={postStage.WarningCount} scanErrorCount={totalResult.ErrorCount} scanWarningCount={postStage.WarningCount} rawWarningCount={postStage.RawWarningCount} tvParseRawWarningCount={postStage.RawWarningCount} warningDedupedCount={Math.Max(0, postStage.RawWarningCount - postStage.WarningCount)} tvParseDeduplicatedWarningCount={postStage.WarningCount} warningsIncludedInErrorCount=false finalScanStatus={(totalResult.ErrorCount > 0 ? "partial-success" : "success-with-warnings-or-success")} backgroundAiClassification=queued");
        return totalResult;
    }

    private void QueueMovieClassification(IReadOnlyCollection<int> mediaFileIds)
    {
        if (mediaFileIds.Count == 0)
        {
            return;
        }

        var ids = mediaFileIds.ToArray();
        ScanIdentificationDiagnostics.Write(
            $"event=scan-ai-classify-queued source=webdav mediaFiles={ids.Length} mode=background reason=non-blocking-scan-completion");
        _ = Task.Run(
            async () =>
            {
                var startedAt = DateTime.UtcNow;
                try
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=scan-ai-classify-start source=webdav mediaFiles={ids.Length}");
                    await ClassifyAffectedMoviesAsync(ids, CancellationToken.None);
                    var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                    ScanIdentificationDiagnostics.Write(
                        $"event=scan-ai-classify-complete source=webdav mediaFiles={ids.Length} durationMs={durationMs}");
                }
                catch (Exception exception)
                {
                    var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                    ScanIdentificationDiagnostics.Write(
                        $"event=scan-ai-classify-error source=webdav mediaFiles={ids.Length} durationMs={durationMs} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 180)}");
                }
            });
    }

    private async Task<PathScanExecutionResult> ScanSinglePathAsync(
        WebDavConnectionModel connection,
        ScanPath scanPath,
        HashSet<string> seenFilePaths,
        HashSet<int> postProcessVideoMediaFileIds,
        HashSet<int> reattachCandidateMediaFileIds,
        HashSet<int> subtitleBindingVideoMediaFileIds,
        ScanVideoClassificationStats videoStats,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var now = DateTime.UtcNow;
        var log = new ScanTaskLog
        {
            SourceConnectionId = connection.Id!.Value,
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

        List<RemoteEntry> remoteEntries;
        try
        {
            remoteEntries = await CollectRemoteFilesAsync(connection, scanPath, cancellationToken);
        }
        catch (Exception exception)
        {
            result.ErrorCount++;
            await CompletePathLogAsync(log, result, ScanTaskStatus.Failed, $"[WebDAV.List] {TrimMessage(exception.Message)}", dbContext, cancellationToken);
            return result;
        }

        try
        {
            var existingFiles = await LoadExistingFilesForPathAsync(dbContext, connection.Id.Value, scanPath.Path, cancellationToken);
            var changedVideoFiles = new List<MediaFile>();
            var changedSubtitleDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var remoteEntry in remoteEntries.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            {
                if (MediaFileRules.IsIgnoredSystemFile(remoteEntry.Name))
                {
                    result.RecordIgnored("macos-resource-fork", remoteEntry.Name);
                    continue;
                }

                var mediaType = MediaFileRules.GetMediaType(remoteEntry.Name);
                if (mediaType == MediaType.Other)
                {
                    result.RecordIgnored("unsupported-extension", remoteEntry.Name);
                    continue;
                }

                if (!seenFilePaths.Add(remoteEntry.Path))
                {
                    result.RecordIgnored("duplicate-path", remoteEntry.Name);
                    continue;
                }

                result.TotalScannedCount++;

                var isNewFile = false;
                var hasMaterialChange = false;
                var wasDeleted = false;
                if (!existingFiles.TryGetValue(remoteEntry.Path, out var mediaFile))
                {
                    isNewFile = true;
                    mediaFile = new MediaFile
                    {
                        SourceConnectionId = connection.Id.Value,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.MediaFiles.Add(mediaFile);
                    existingFiles[remoteEntry.Path] = mediaFile;
                    result.NewFileCount++;
                }
                else
                {
                    wasDeleted = mediaFile.IsDeleted;
                    hasMaterialChange = HasMaterialChange(mediaFile, remoteEntry, mediaType, scanPath.Id);
                    if (!hasMaterialChange)
                    {
                        if (mediaType == MediaType.Video && IsActiveUnboundVideo(mediaFile))
                        {
                            reattachCandidateMediaFileIds.Add(mediaFile.Id);
                            videoStats.UnchangedUnboundVideoCount++;
                        }

                        continue;
                    }

                    result.UpdatedFileCount++;
                }

                mediaFile.ScanPathId = scanPath.Id;
                mediaFile.FileName = remoteEntry.Name;
                mediaFile.FilePath = remoteEntry.Path;
                mediaFile.RemoteUri = string.IsNullOrWhiteSpace(remoteEntry.RemoteUri) ? null : remoteEntry.RemoteUri;
                mediaFile.Extension = Path.GetExtension(remoteEntry.Name).ToLowerInvariant();
                mediaFile.FileSize = remoteEntry.ContentLength ?? 0L;
                mediaFile.LastModifiedAt = remoteEntry.LastModifiedAt;
                mediaFile.MediaType = mediaType;
                mediaFile.IsDeleted = false;
                mediaFile.LastSeenAt = DateTime.UtcNow;
                mediaFile.UpdatedAt = DateTime.UtcNow;

                if (mediaType == MediaType.Video)
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

                    changedVideoFiles.Add(mediaFile);
                }
                else if (mediaType == MediaType.Subtitle && (isNewFile || hasMaterialChange))
                {
                    changedSubtitleDirectories.Add(GetDirectoryPath(remoteEntry.Path));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var mediaFileId in changedVideoFiles
                         .Select(x => x.Id)
                         .Where(x => x > 0))
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

            result.IsSuccessful = true;
            result.IgnoredFiles.WriteDiagnostics("webdav", result.IgnoredFileCount);
            await CompletePathLogAsync(log, result, ScanTaskStatus.Success, string.Empty, dbContext, cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            result.ErrorCount++;
            result.IgnoredFiles.WriteDiagnostics("webdav", result.IgnoredFileCount);
            await CompletePathLogAsync(log, result, ScanTaskStatus.Failed, $"[MediaFile.Upsert] {TrimMessage(exception.Message)}", dbContext, cancellationToken);
            return result;
        }
    }

    private async Task<List<RemoteEntry>> CollectRemoteFilesAsync(
        WebDavConnectionModel connection,
        ScanPath scanPath,
        CancellationToken cancellationToken)
    {
        var files = new List<RemoteEntry>();
        var queue = new Queue<PendingDirectory>();
        queue.Enqueue(new PendingDirectory(scanPath.Path, null));

        while (queue.Count > 0)
        {
            var currentDirectory = queue.Dequeue();
            var children = await _webDavService.ListDirectoryAsync(
                connection,
                currentDirectory.Path,
                currentDirectory.RemoteUri,
                cancellationToken);

            foreach (var child in children)
            {
                if (child.IsDirectory)
                {
                    if (scanPath.IsRecursive)
                    {
                        queue.Enqueue(new PendingDirectory(child.Path, child.RemoteUri));
                    }

                    continue;
                }

                files.Add(child);
            }
        }

        return files;
    }

    private sealed record PendingDirectory(string Path, string? RemoteUri);

    private static async Task<Dictionary<string, MediaFile>> LoadExistingFilesForPathAsync(
        AppDbContext dbContext,
        int sourceConnectionId,
        string scanPath,
        CancellationToken cancellationToken)
    {
        var normalizedPath = WebDavPathHelper.NormalizeVirtualPath(scanPath);
        var childPrefix = normalizedPath == "/" ? "/" : normalizedPath + "/";

        return await dbContext.MediaFiles
            .Where(
                x => x.SourceConnectionId == sourceConnectionId
                     && (normalizedPath == "/"
                         || x.FilePath == normalizedPath
                         || x.FilePath.StartsWith(childPrefix)))
            .ToDictionaryAsync(x => x.FilePath, StringComparer.OrdinalIgnoreCase, cancellationToken);
    }

    private static bool HasMaterialChange(MediaFile mediaFile, RemoteEntry remoteEntry, MediaType mediaType, int scanPathId)
    {
        return mediaFile.ScanPathId != scanPathId
               || !string.Equals(mediaFile.FileName, remoteEntry.Name, StringComparison.Ordinal)
               || !string.Equals(mediaFile.RemoteUri ?? string.Empty, remoteEntry.RemoteUri ?? string.Empty, StringComparison.Ordinal)
               || mediaFile.FileSize != (remoteEntry.ContentLength ?? 0L)
               || mediaFile.LastModifiedAt != remoteEntry.LastModifiedAt
               || mediaFile.MediaType != mediaType
               || mediaFile.IsDeleted;
    }

    private static bool IsActiveUnboundVideo(MediaFile mediaFile)
    {
        return mediaFile.MediaType == MediaType.Video
               && !mediaFile.IsDeleted
               && !mediaFile.MovieId.HasValue
               && !mediaFile.EpisodeId.HasValue;
    }

    private static async Task<IReadOnlyCollection<int>> CollectFailedMoviePlaceholderRetryCandidatesAsync(
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
            return [];
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
            return [];
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
                    MovieUpdatedAt = x.Movie!.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var queuedIds = candidates
            .Where(x => seenFilePaths.Contains(x.FilePath))
            .Where(
                x => retryWindowsByPath.TryGetValue(x.ScanPathId, out var retryWindow)
                     && x.MovieUpdatedAt >= retryWindow.StartedAt
                     && x.MovieUpdatedAt < scanStartedAtUtc)
            .Select(x => x.MediaFileId)
            .Distinct()
            .ToArray();

        ScanIdentificationDiagnostics.Write(
            $"event=scan-movie-retry-candidates source={sourceKind} previousTmdbSearchErrorScanPaths={retryWindows.Count} candidateCount={candidates.Count} queuedCount={queuedIds.Length} skippedCount={Math.Max(0, candidates.Count - queuedIds.Length)} reason=previous-tmdb-search-error");

        return queuedIds;
    }

    private static async Task<IReadOnlyCollection<int>> MarkMissingFilesDeletedAsync(
        int sourceConnectionId,
        IReadOnlyCollection<ScanPath> enabledScanPaths,
        HashSet<string> seenFilePaths,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var normalizedPaths = enabledScanPaths
            .Select(x => WebDavPathHelper.NormalizeVirtualPath(x.Path))
            .ToArray();

        var candidates = await dbContext.MediaFiles
            .Where(x => x.SourceConnectionId == sourceConnectionId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var deletedSubtitleDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mediaFile in candidates.Where(mediaFile => IsUnderEnabledPath(mediaFile.FilePath, normalizedPaths)))
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

    private static bool IsUnderEnabledPath(string filePath, IReadOnlyCollection<string> enabledPaths)
    {
        var normalizedFilePath = WebDavPathHelper.NormalizeVirtualPath(filePath);
        return enabledPaths.Any(
            enabledPath => enabledPath == "/"
                           || string.Equals(normalizedFilePath, enabledPath, StringComparison.OrdinalIgnoreCase)
                           || normalizedFilePath.StartsWith(enabledPath + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex <= 0 ? "/" : normalized[..lastSeparatorIndex];
    }

    private static async Task UpdateConnectionLastScanAtAsync(int connectionId, DateTime completedAt, CancellationToken cancellationToken)
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

        foreach (var movieId in movieIds)
        {
            var needsClassification = await dbContext.Movies
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == movieId
                         && x.TmdbId.HasValue
                         && (x.IdentificationStatus == IdentificationStatus.Matched
                             || x.IdentificationStatus == IdentificationStatus.ManualConfirmed)
                         && (string.IsNullOrWhiteSpace(x.AiTagsText)
                             || string.IsNullOrWhiteSpace(x.EmotionTagsText)
                             || string.IsNullOrWhiteSpace(x.SceneTagsText)),
                    cancellationToken);

            if (needsClassification)
            {
                await _aiClassificationService.ClassifyMovieAsync(movieId, cancellationToken);
            }
        }
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
            .Where(x => logIds.Contains(x.Id) && x.Status == ScanTaskStatus.Success)
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
                : $"{log.ErrorMessage}；{summary}";
            log.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task FinalizeSuccessfulLogsAsync(
        IReadOnlyCollection<int> logIds,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var logs = await dbContext.ScanTaskLogs
            .Where(x => logIds.Contains(x.Id) && x.EndedAt.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var log in logs)
        {
            log.EndedAt = completedAt;
            log.UpdatedAt = completedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildStatusMessage(ScanExecutionResult totalResult, PostScanStageResult postStage)
    {
        if (totalResult.ErrorCount == 0 && !postStage.HasIssues)
        {
            return $"扫描完成，共扫描 {totalResult.TotalScannedCount} 个文件。";
        }

        if (totalResult.ErrorCount == 0 && postStage.WarningCount > 0)
        {
            return $"扫描入库完成，共扫描 {totalResult.TotalScannedCount} 个文件；存在 {postStage.WarningCount} 个警告。{postStage.BuildSummary()}";
        }

        if (postStage.HasIssues && totalResult.ErrorCount == postStage.ErrorCount)
        {
            return $"扫描入库完成，共扫描 {totalResult.TotalScannedCount} 个文件；识别/元数据阶段存在问题。{postStage.BuildSummary()}";
        }

        return $"扫描完成，共扫描 {totalResult.TotalScannedCount} 个文件；存在 {totalResult.ErrorCount} 个问题。{postStage.BuildSummary()}";
    }

    private static DateTime ToLocalDisplayTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToLocalTime();
    }

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
    }

    private sealed class PathScanExecutionResult
    {
        public int LogId { get; set; }

        public bool IsSuccessful { get; set; }

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
            return $"识别/元数据阶段{string.Join("，", parts)}{detail}";
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
