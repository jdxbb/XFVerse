using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed partial class MovieIdentificationService : IMovieIdentificationService
{
    private const double MinimumAutoMatchConfidence = 0.55d;
    private const double MatchedConfidence = 0.80d;
    private const double NeutralYearScore = 0.70d;
    private const int MovieTitleMaxLength = 300;
    private const string UnidentifiedTvSeriesCandidateTitle = "未识别剧集候选";
    private const string UnidentifiedTvSeasonCandidateTitle = "未识别电视剧季";

    private readonly ISettingsService _settingsService;
    private readonly ITmdbService _tmdbService;
    private readonly IOmdbService _omdbService;
    private readonly IAiClassificationService _aiClassificationService;

    public MovieIdentificationService(
        ISettingsService settingsService,
        ITmdbService tmdbService,
        IOmdbService omdbService,
        IAiClassificationService aiClassificationService)
    {
        _settingsService = settingsService;
        _tmdbService = tmdbService;
        _omdbService = omdbService;
        _aiClassificationService = aiClassificationService;
    }

    public async Task<IdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        return await IdentifyMediaFilesAsync(mediaFileIds, tmdbSearchCache: null, cancellationToken);
    }

    public async Task<IdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        ScanTmdbSearchCache? tmdbSearchCache,
        CancellationToken cancellationToken = default)
    {
        var result = new IdentificationRunResult();
        var distinctIds = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        ScanIdentificationDiagnostics.Write($"event=movie-identify-start requested={distinctIds.Length}");
        if (distinctIds.Length == 0)
        {
            ScanIdentificationDiagnostics.Write("event=movie-identify-complete requested=0 reason=no-media-file-ids");
            return result;
        }

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        var hasTmdbCredential = !string.IsNullOrWhiteSpace(settings.TmdbReadAccessToken)
                                || !string.IsNullOrWhiteSpace(settings.TmdbApiKey);
        var placeholderGroupingCandidates = new List<MoviePlaceholderGroupingInput>();

        foreach (var mediaFileId in distinctIds)
        {
            result.AttemptedCount++;
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var mediaFile = await dbContext.MediaFiles
                .Include(x => x.SourceConnection)
                .Include(x => x.Movie)
                .ThenInclude(x => x!.RatingSources)
                .FirstOrDefaultAsync(
                    x => x.Id == mediaFileId
                         && x.MediaType == MediaType.Video
                         && !x.IsDeleted,
                    cancellationToken);

            if (mediaFile is null)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-skip mediaFileId={mediaFileId} reason=missing-or-deleted-video");
                continue;
            }

            if (mediaFile.EpisodeId.HasValue)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-skip mediaFileId={mediaFileId} reason=already-bound-tv-episode episodeId={mediaFile.EpisodeId.Value}");
                continue;
            }

            var hiddenMovieIds = await ScanCandidateVisibilityGuard.LoadHiddenMovieIdsAsync(
                dbContext,
                [mediaFile.MovieId],
                cancellationToken);
            if (ScanCandidateVisibilityGuard.IsHiddenFailedMoviePlaceholder(mediaFile, hiddenMovieIds))
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-skip mediaFileId={mediaFileId} movieId={mediaFile.MovieId!.Value} reason={ScanCandidateVisibilityGuard.HiddenFailedPlaceholderSkipReason}");
                continue;
            }

            if (mediaFile.MovieId.HasValue
                && mediaFile.Movie is not null
                && mediaFile.Movie.TmdbId.HasValue
                && mediaFile.Movie.IdentificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-skip mediaFileId={mediaFileId} reason=already-stable-movie movieId={mediaFile.MovieId.Value} tmdbId={mediaFile.Movie.TmdbId.Value}");
                continue;
            }

            var parsedName = MovieFileNameParser.Parse(mediaFile.FileName);
            var candidateTitle = string.IsNullOrWhiteSpace(parsedName.CleanTitle)
                ? Path.GetFileNameWithoutExtension(mediaFile.FileName)
                : parsedName.CleanTitle;
            var placeholderTitle = BuildUnidentifiedMovieTitle(mediaFile.FileName);
            var lowInformationReason = GetLowInformationMovieQueryReason(candidateTitle, parsedName.ReleaseYear);
            ScanIdentificationDiagnostics.Write(
                $"event=movie-candidate mediaFileId={mediaFileId} path={ScanIdentificationDiagnostics.FormatPath(mediaFile.FilePath)} file={ScanIdentificationDiagnostics.FormatFileName(mediaFile.FileName)} rawMovieTitle={ScanIdentificationDiagnostics.FormatValue(Path.GetFileNameWithoutExtension(mediaFile.FileName))} cleanedMovieTitle={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} removedNoiseCategory={ScanIdentificationDiagnostics.FormatValue(string.Join('|', parsedName.RemovedNoiseCategories))} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(parsedName.ReleaseYear)} existingMovieId={ScanIdentificationDiagnostics.FormatNullable(mediaFile.MovieId)} movieQueryQuality={ScanIdentificationDiagnostics.FormatValue(string.IsNullOrWhiteSpace(lowInformationReason) ? "usable" : "low-information")} movieLowInformationQuery={(!string.IsNullOrWhiteSpace(lowInformationReason)).ToString().ToLowerInvariant()} movieAutoMatchBlockedReason={ScanIdentificationDiagnostics.FormatValue(lowInformationReason)}");

            if (!string.IsNullOrWhiteSpace(lowInformationReason))
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-placeholder mediaFileId={mediaFileId} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} placeholderTitle={ScanIdentificationDiagnostics.FormatFileName(placeholderTitle)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(parsedName.ReleaseYear)} reason=movie-low-information-query movieLowInformationQuery=true movieAutoMatchBlockedReason={ScanIdentificationDiagnostics.FormatValue(lowInformationReason)} finalDecision=movie-placeholder");
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, placeholderTitle, parsedName.ReleaseYear, cancellationToken);
                AddMoviePlaceholderGroupingCandidate(placeholderGroupingCandidates, mediaFile, candidateTitle, "movie-low-information-query");
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                continue;
            }

            if (!hasTmdbCredential)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-placeholder mediaFileId={mediaFileId} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} placeholderTitle={ScanIdentificationDiagnostics.FormatFileName(placeholderTitle)} reason=missing-tmdb-credential");
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, placeholderTitle, parsedName.ReleaseYear, cancellationToken);
                AddMoviePlaceholderGroupingCandidate(placeholderGroupingCandidates, mediaFile, candidateTitle, "missing-tmdb-credential");
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                result.AddWarning("TMDB.Auth", "TMDB 认证未配置，资源已保留为识别失败，可后续重试。");
                continue;
            }

            List<MetadataSearchCandidate> searchResults;
            try
            {
                searchResults = (await SearchMoviesAsync(candidateTitle, parsedName.ReleaseYear, tmdbSearchCache, cancellationToken)).ToList();
            }
            catch (Exception exception)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-search-error mediaFileId={mediaFileId} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(parsedName.ReleaseYear)} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, placeholderTitle, parsedName.ReleaseYear, cancellationToken);
                AddMoviePlaceholderGroupingCandidate(placeholderGroupingCandidates, mediaFile, candidateTitle, "movie-search-error");
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                result.AddError("TMDB.Search", TrimMessage(exception.Message));
                continue;
            }

            var bestCandidate = searchResults
                .Select(
                    candidate =>
                    {
                        candidate.Confidence = CalculateConfidence(candidateTitle, parsedName.ReleaseYear, candidate);
                        return candidate;
                    })
                .OrderByDescending(candidate => candidate.Confidence)
                .FirstOrDefault();

            ScanIdentificationDiagnostics.Write(
                $"event=movie-search-complete mediaFileId={mediaFileId} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(parsedName.ReleaseYear)} resultCount={searchResults.Count} topTitle={ScanIdentificationDiagnostics.FormatValue(bestCandidate?.Title)} topOriginal={ScanIdentificationDiagnostics.FormatValue(bestCandidate?.OriginalTitle)} topTmdbId={ScanIdentificationDiagnostics.FormatNullable(bestCandidate?.TmdbId)} topYear={ScanIdentificationDiagnostics.FormatNullable(bestCandidate?.ReleaseYear)} topConfidence={ScanIdentificationDiagnostics.FormatConfidence(bestCandidate?.Confidence)} movieLowInformationQuery=false movieResultStatus={ScanIdentificationDiagnostics.FormatValue(GetMovieResultStatus(bestCandidate))} movieAutoApply={(bestCandidate?.Confidence >= MatchedConfidence).ToString().ToLowerInvariant()} movieAutoApplyBlockedReason={ScanIdentificationDiagnostics.FormatValue(GetMovieAutoApplyBlockedReason(bestCandidate))} decision={ScanIdentificationDiagnostics.FormatValue(GetMovieSearchDecision(bestCandidate))}");
            if (bestCandidate is null || bestCandidate.Confidence < MinimumAutoMatchConfidence)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-placeholder mediaFileId={mediaFileId} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} placeholderTitle={ScanIdentificationDiagnostics.FormatFileName(placeholderTitle)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(parsedName.ReleaseYear)} reason={(bestCandidate is null ? "movie-no-result" : "movie-low-confidence")} movieResultStatus={ScanIdentificationDiagnostics.FormatValue(GetMovieResultStatus(bestCandidate))} movieAutoApply=false movieAutoApplyBlockedReason={ScanIdentificationDiagnostics.FormatValue(GetMovieAutoApplyBlockedReason(bestCandidate))} finalDecision=movie-placeholder");
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, placeholderTitle, parsedName.ReleaseYear, cancellationToken);
                AddMoviePlaceholderGroupingCandidate(placeholderGroupingCandidates, mediaFile, candidateTitle, bestCandidate is null ? "movie-no-result" : "movie-low-confidence");
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                continue;
            }

            if (bestCandidate.Confidence < MatchedConfidence)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-placeholder mediaFileId={mediaFileId} candidate={ScanIdentificationDiagnostics.FormatValue(candidateTitle)} placeholderTitle={ScanIdentificationDiagnostics.FormatFileName(placeholderTitle)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(parsedName.ReleaseYear)} topTmdbId={bestCandidate.TmdbId} topTitle={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Title)} topConfidence={ScanIdentificationDiagnostics.FormatConfidence(bestCandidate.Confidence)} reason=movie-needs-review movieResultStatus=NeedsReview movieAutoApply=false movieAutoApplyBlockedReason=needs-review-not-auto-applied finalDecision=movie-placeholder");
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, placeholderTitle, parsedName.ReleaseYear, cancellationToken);
                AddMoviePlaceholderGroupingCandidate(placeholderGroupingCandidates, mediaFile, candidateTitle, "movie-needs-review");
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                continue;
            }

            var effectiveCandidate = bestCandidate;
            try
            {
                var details = await _tmdbService.GetMovieDetailsAsync(bestCandidate.TmdbId, cancellationToken);
                if (details is not null)
                {
                    details.Confidence = bestCandidate.Confidence;
                    effectiveCandidate = details;
                }
                else
                {
                    ScanIdentificationDiagnostics.Write(
                        $"event=movie-detail-empty mediaFileId={mediaFileId} tmdbId={bestCandidate.TmdbId} title={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Title)}");
                    result.AddWarning("TMDB.Detail", $"TMDB 详情为空：{bestCandidate.Title}，已使用搜索结果继续绑定。");
                }
            }
            catch (Exception exception)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-detail-error mediaFileId={mediaFileId} tmdbId={bestCandidate.TmdbId} title={ScanIdentificationDiagnostics.FormatValue(bestCandidate.Title)} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
                result.AddWarning(
                    "TMDB.Detail",
                    $"{bestCandidate.Title} 详情读取失败，已退回搜索结果继续绑定：{TrimMessage(exception.Message)}");
            }

            var status = IdentificationStatus.Matched;

            try
            {
                await ApplyCandidateAsync(dbContext, mediaFile, effectiveCandidate, status, result, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                result.BoundCount++;
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-apply-complete mediaFileId={mediaFileId} tmdbId={effectiveCandidate.TmdbId} title={ScanIdentificationDiagnostics.FormatValue(effectiveCandidate.Title)} status={status} movieResultStatus={status} movieAutoApply=true movieAutoApplyBlockedReason=(none)");
            }
            catch (Exception exception)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=movie-apply-error mediaFileId={mediaFileId} tmdbId={effectiveCandidate.TmdbId} title={ScanIdentificationDiagnostics.FormatValue(effectiveCandidate.Title)} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, placeholderTitle, parsedName.ReleaseYear, cancellationToken);
                AddMoviePlaceholderGroupingCandidate(placeholderGroupingCandidates, mediaFile, candidateTitle, "movie-apply-error");
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                result.AddError("Identify.Apply", TrimMessage(exception.Message));
            }
        }

        await PersistMoviePlaceholderGroupingAsync(placeholderGroupingCandidates, "movie-identify", cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=movie-identify-complete requested={distinctIds.Length} attempted={result.AttemptedCount} bound={result.BoundCount} placeholders={result.PlaceholderCount} warnings={result.WarningCount} errors={result.ErrorCount}");
        return result;
    }

    public async Task<MoviePlaceholderGroupingRunResult> AggregateUnidentifiedMediaFilesAsync(
        IReadOnlyCollection<int> scanPathIds,
        CancellationToken cancellationToken = default,
        string sourceKind = "unknown")
    {
        var ids = scanPathIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            ScanIdentificationDiagnostics.Write(
                "event=orphan-grouping-summary orphanGroupingAttempted=false orphanGroupingMediaFileCount=0 orphanGroupingCreatedSeasonCount=0 orphanGroupingSkippedReason=no-scan-paths");
            return MoviePlaceholderGroupingRunResult.Empty;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var hiddenPlaceholderSkippedCount = await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.Movie)
            .Where(
                x => x.ScanPathId.HasValue
                     && ids.Contains(x.ScanPathId.Value)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.EpisodeId.HasValue
                     && x.MovieId.HasValue
                     && x.Movie != null
                     && x.Movie.IdentificationStatus == IdentificationStatus.Failed
                     && dbContext.UserMovieCollectionItems.Any(
                         item => item.MovieId == x.MovieId
                                 && item.LibraryVisibilityState == LibraryVisibilityState.Hidden))
            .CountAsync(cancellationToken);
        var candidates = await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.Movie)
            .Where(
                x => x.ScanPathId.HasValue
                     && ids.Contains(x.ScanPathId.Value)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.EpisodeId.HasValue
                     && (!x.MovieId.HasValue
                         || (x.Movie != null
                             && x.Movie.IdentificationStatus == IdentificationStatus.Failed
                             && !dbContext.UserMovieCollectionItems.Any(
                                 item => item.MovieId == x.MovieId
                                         && item.LibraryVisibilityState == LibraryVisibilityState.Hidden))))
            .Select(
                x => new MoviePlaceholderGroupingInput(
                    x.Id,
                    x.FileName,
                    x.FilePath,
                    MoviePlaceholderGroupingHelper.GetDirectParentPath(x.FilePath),
                    Path.GetFileNameWithoutExtension(x.FileName),
                    x.MovieId.HasValue ? "movie-placeholder-existing" : "orphan-media-file"))
            .ToListAsync(cancellationToken);

        ScanIdentificationDiagnostics.Write(
            $"event=orphan-grouping-start orphanGroupingAttempted=true scanPaths={ids.Length} orphanGroupingMediaFileCount={candidates.Count} hiddenPlaceholderSkippedCount={hiddenPlaceholderSkippedCount} hiddenPlaceholderSkipReason={ScanCandidateVisibilityGuard.HiddenFailedPlaceholderSkipReason}");
        var summary = await PersistMoviePlaceholderGroupingAsync(candidates, sourceKind, cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=orphan-grouping-summary orphanGroupingAttempted=true orphanGroupingMediaFileCount={summary.CandidateFiles} orphanGroupingCreatedSeasonCount={summary.PersistedRanges} orphanGroupingGroupedMediaFileCount={summary.PersistedFiles} orphanGroupingSkippedReason={ScanIdentificationDiagnostics.FormatValue(summary.SkippedReasons)}");
        return new MoviePlaceholderGroupingRunResult
        {
            CandidateFiles = summary.CandidateFiles,
            PersistedRanges = summary.PersistedRanges,
            PersistedFiles = summary.PersistedFiles,
            HiddenPlaceholderSkippedCount = hiddenPlaceholderSkippedCount,
            SkippedReasons = summary.SkippedReasons
        };
    }

    private static void AddMoviePlaceholderGroupingCandidate(
        ICollection<MoviePlaceholderGroupingInput> candidates,
        MediaFile mediaFile,
        string candidateTitle,
        string placeholderReason)
    {
        candidates.Add(
            new MoviePlaceholderGroupingInput(
                mediaFile.Id,
                mediaFile.FileName,
                mediaFile.FilePath,
                MoviePlaceholderGroupingHelper.GetDirectParentPath(mediaFile.FilePath),
                candidateTitle,
                placeholderReason));
    }

    private static string BuildUnidentifiedMovieTitle(string fileName)
    {
        var title = string.IsNullOrWhiteSpace(fileName)
            ? "-"
            : fileName.Trim();
        return title.Length <= MovieTitleMaxLength ? title : title[..MovieTitleMaxLength];
    }

    private static async Task<MoviePlaceholderGroupingPersistenceSummary> PersistMoviePlaceholderGroupingAsync(
        IReadOnlyCollection<MoviePlaceholderGroupingInput> placeholders,
        string sourceKind,
        CancellationToken cancellationToken)
    {
        var groupingResult = MoviePlaceholderGroupingHelper.BuildRanges(placeholders);
        if (groupingResult.CandidateFiles == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=unknown-season-grouping-summary sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} candidateFiles=0 groupedRangeCount=0 persistedRangeCount=0 groupedMediaFileCount=0 reusedSeriesCount=0 createdSeriesCount=0 reusedSeasonCount=0 createdSeasonCount=0 skippedReasons=(none)");
            ScanIdentificationDiagnostics.Write(
                "event=movie-placeholder-grouping-summary moviePlaceholderGroupingAttempted=false moviePlaceholderGroupingCandidateFiles=0 groupedMoviePlaceholdersCount=0 groupedTvLikePlaceholderRangesCount=0 groupedPlaceholdersHiddenFromMovieList=0 groupedRangesVisibleToLibrary=0 groupedRangeProjectionMode=unidentified-tv-season moviePlaceholderGroupingPersistence=unidentified-tv-season groupingSkippedReasons=(none)");
            return new MoviePlaceholderGroupingPersistenceSummary(0, 0, 0, "(none)");
        }

        var persistedRanges = 0;
        var persistedFiles = 0;
        var reusedSeriesCount = 0;
        var createdSeriesCount = 0;
        var reusedSeasonCount = 0;
        var createdSeasonCount = 0;
        var skippedRanges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using (var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create()))
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var range in groupingResult.Ranges)
            {
                var persistenceResult = await PersistGroupedTvLikePlaceholderRangeAsync(dbContext, range, sourceKind, cancellationToken);
                if (persistenceResult.Created)
                {
                    persistedRanges++;
                    persistedFiles += persistenceResult.FileCount;
                    if (persistenceResult.ReusedSeries)
                    {
                        reusedSeriesCount++;
                    }
                    else
                    {
                        createdSeriesCount++;
                    }

                    if (persistenceResult.ReusedSeason)
                    {
                        reusedSeasonCount++;
                    }
                    else
                    {
                        createdSeasonCount++;
                    }
                }
                else
                {
                    AddCount(skippedRanges, persistenceResult.SkippedReason);
                }

                LogMoviePlaceholderGroupingRange(range, persistenceResult);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var allSkippedReasons = groupingResult.SkippedReasons
            .Concat(skippedRanges)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Value), StringComparer.OrdinalIgnoreCase);
        var skippedText = allSkippedReasons.Count == 0
            ? "(none)"
            : string.Join("|", allSkippedReasons.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}"));

        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-grouping-summary sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} candidateFiles={groupingResult.CandidateFiles} groupedRangeCount={groupingResult.Ranges.Count} persistedRangeCount={persistedRanges} groupedMediaFileCount={persistedFiles} reusedSeriesCount={reusedSeriesCount} createdSeriesCount={createdSeriesCount} reusedSeasonCount={reusedSeasonCount} createdSeasonCount={createdSeasonCount} skippedReasons={ScanIdentificationDiagnostics.FormatValue(skippedText)}");
        ScanIdentificationDiagnostics.Write(
            $"event=movie-placeholder-grouping-summary moviePlaceholderGroupingAttempted=true moviePlaceholderGroupingCandidateFiles={groupingResult.CandidateFiles} parsedEpisodeLikePlaceholderFiles={groupingResult.ParsedEpisodeLikeFiles} groupedMoviePlaceholdersCount={persistedFiles} groupedTvLikePlaceholderRangesCount={persistedRanges} groupedPlaceholdersHiddenFromMovieList={persistedFiles} groupedRangesVisibleToLibrary={persistedRanges} groupedRangeProjectionMode=unidentified-tv-season moviePlaceholderGroupingPersistence=unidentified-tv-season groupingSkippedReasons={ScanIdentificationDiagnostics.FormatValue(skippedText)}");
        return new MoviePlaceholderGroupingPersistenceSummary(
            groupingResult.CandidateFiles,
            persistedRanges,
            persistedFiles,
            skippedText);
    }

    private static void LogMoviePlaceholderGroupingRange(
        MoviePlaceholderGroupingRange range,
        GroupedTvLikePlaceholderPersistenceResult result)
    {
        var sampleFiles = string.Join(
            "|",
            range.SampleFileNames.Select(ScanIdentificationDiagnostics.FormatFileName));
        var reasons = string.Join("|", range.PlaceholderReasons);
        var isMixedPattern = string.Equals(range.PatternKey, "mixed-episode-sequence", StringComparison.OrdinalIgnoreCase);
        var isLongRunning = string.Equals(range.Pattern, "long-running-range", StringComparison.OrdinalIgnoreCase);
        var missingNumbers = range.MissingNumbers is { Count: > 0 }
            ? string.Join(",", range.MissingNumbers.Take(20))
            : string.Empty;

        ScanIdentificationDiagnostics.Write(
            $"event=movie-placeholder-grouping parentDir={ScanIdentificationDiagnostics.FormatPath(range.ParentPath)} moviePlaceholderGroupingAttempted=true moviePlaceholderGroupingFileCount={range.FileCount} groupedRangeMediaFileCount={range.MediaFileIds.Count} moviePlaceholderGroupingPattern={ScanIdentificationDiagnostics.FormatValue(range.Pattern)} moviePlaceholderGroupingPatternKey={ScanIdentificationDiagnostics.FormatValue(range.PatternKey)} mixedPatternGrouping={isMixedPattern.ToString().ToLowerInvariant()} longRunningRangeGrouping={isLongRunning.ToString().ToLowerInvariant()} longRunningRangeMissingNumbers={ScanIdentificationDiagnostics.FormatValue(missingNumbers)} missingCount={range.MissingCount} patterns={ScanIdentificationDiagnostics.FormatValue(range.Pattern)} moviePlaceholderGroupingStartNumber={range.StartNumber} moviePlaceholderGroupingEndNumber={range.EndNumber} groupedRangeNumberStart={range.StartNumber} groupedRangeNumberEnd={range.EndNumber} groupedRangeParentDisplay={ScanIdentificationDiagnostics.FormatValue(MoviePlaceholderGroupingHelper.GetParentFolderDisplay(range.ParentPath))} moviePlaceholderGroupingCreated={result.Created.ToString().ToLowerInvariant()} unknownSeriesReused={result.ReusedSeries.ToString().ToLowerInvariant()} unknownSeasonReused={result.ReusedSeason.ToString().ToLowerInvariant()} moviePlaceholderGroupingPersistence=unidentified-tv-season moviePlaceholderGroupingSkippedReason={ScanIdentificationDiagnostics.FormatValue(result.SkippedReason)} groupedMoviePlaceholdersCount={result.FileCount} groupedTvLikePlaceholderRangesCount={(result.Created ? 1 : 0)} persistedTvSeriesId={ScanIdentificationDiagnostics.FormatNullable(result.TvSeriesId)} persistedTvSeasonId={ScanIdentificationDiagnostics.FormatNullable(result.TvSeasonId)} persistedTvEpisodes={result.EpisodeCount} placeholderReasons={ScanIdentificationDiagnostics.FormatValue(reasons)} sampleDirectVideoFiles={ScanIdentificationDiagnostics.FormatValue(sampleFiles)} finalDecision={(result.Created ? "tv-like-placeholder-season-persisted" : "tv-like-placeholder-season-skipped")}");
    }

    private static async Task<GroupedTvLikePlaceholderPersistenceResult> PersistGroupedTvLikePlaceholderRangeAsync(
        AppDbContext dbContext,
        MoviePlaceholderGroupingRange range,
        string sourceKind,
        CancellationToken cancellationToken)
    {
        var requestedIds = range.MediaFileIds.Where(x => x > 0).Distinct().ToArray();
        if (requestedIds.Length < 3)
        {
            return GroupedTvLikePlaceholderPersistenceResult.Skipped("range-too-small");
        }

        var mediaFiles = await dbContext.MediaFiles
            .Include(x => x.Movie)
            .Where(x => requestedIds.Contains(x.Id))
            .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var mediaFileIndex = mediaFiles.ToDictionary(x => x.Id);
        var orderedItems = range.NumberedItems
            .Select(item => new GroupedTvLikePlaceholderEpisodeCandidate(item.Input, item.EpisodeNumber))
            .Where(x => mediaFileIndex.TryGetValue(x.Input.MediaFileId, out var mediaFile)
                        && mediaFile.EpisodeId is null
                        && (!mediaFile.MovieId.HasValue
                            || mediaFile.Movie?.IdentificationStatus == IdentificationStatus.Failed))
            .ToList();
        if (orderedItems.Count < 3)
        {
            return GroupedTvLikePlaceholderPersistenceResult.Skipped("not-enough-eligible-unidentified-sources");
        }

        var now = DateTime.UtcNow;
        var orderedMediaFiles = orderedItems
            .Select(x => mediaFileIndex[x.Input.MediaFileId])
            .ToArray();
        var episodeNumbers = orderedItems.Select(x => x.EpisodeNumber).Distinct().ToArray();
        var hasGroupingContext = UnknownTvGroupingKeyHelper.TryBuildContext(
            orderedMediaFiles,
            range.ParentPath,
            range.StartNumber,
            range.EndNumber,
            out var groupingContext,
            out var groupingSkippedReason);

        if (hasGroupingContext)
        {
            WriteUnknownSeriesGroupingCandidate(sourceKind, groupingContext);
        }
        else
        {
            WriteUnknownSeriesSkipped(sourceKind, groupingContext, groupingSkippedReason, 0);
        }

        if (hasGroupingContext && UnknownTvGroupingKeyHelper.HasSpecialDirectoryToken(groupingContext))
        {
            const string specialSkippedReason = "special-directory-auto-append-disabled";
            WriteUnknownSeriesSkipped(sourceKind, groupingContext, specialSkippedReason, 0);
            WriteUnknownSeasonSkipped(sourceKind, groupingContext, specialSkippedReason, 0, 0);
            return GroupedTvLikePlaceholderPersistenceResult.Skipped(specialSkippedReason);
        }

        var fallbackSeriesName = MoviePlaceholderGroupingHelper.GetParentFolderDisplay(range.ParentPath);
        var seriesResolution = await ResolveUnknownSeriesForGroupedRangeAsync(
            dbContext,
            groupingContext,
            hasGroupingContext,
            groupingSkippedReason,
            fallbackSeriesName,
            sourceKind,
            now,
            cancellationToken);
        var tvSeries = seriesResolution.Series;

        var seriesNamePreserved = seriesResolution.Reused && !string.IsNullOrWhiteSpace(tvSeries.Name);
        if (!seriesNamePreserved)
        {
            tvSeries.Name = TruncateRequired(BuildGroupedUnknownSeriesName(groupingContext, hasGroupingContext, fallbackSeriesName), 300);
        }

        tvSeries.OriginalName = null;
        tvSeries.Overview = "连续编号未识别文件聚合生成的剧集候选。";
        tvSeries.PosterRemoteUrl = null;
        tvSeries.Country = null;
        tvSeries.Language = null;
        tvSeries.FirstAirDate = null;
        tvSeries.FirstAirYear = null;
        tvSeries.GenresText = null;
        tvSeries.UpdatedAt = now;

        var seasonResolution = await ResolveUnknownSeasonForGroupedRangeAsync(
            dbContext,
            tvSeries,
            groupingContext,
            hasGroupingContext,
            groupingSkippedReason,
            episodeNumbers,
            sourceKind,
            now,
            cancellationToken);
        var tvSeason = seasonResolution.Season;

        tvSeason.TmdbSeasonId = null;
        var seasonNamePreserved = seasonResolution.Reused && !string.IsNullOrWhiteSpace(tvSeason.Name);
        if (!seasonNamePreserved)
        {
            tvSeason.Name = TruncateRequired(BuildGroupedUnknownSeasonName(groupingContext, hasGroupingContext), 300);
        }

        tvSeason.Overview = "由同一目录下严格连续编号的未识别文件聚合生成，尚未绑定 TMDB。";
        tvSeason.PosterRemoteUrl = null;
        tvSeason.AirDate = null;
        tvSeason.TmdbEpisodeCount = Math.Max(
            tvSeason.TmdbEpisodeCount.GetValueOrDefault(),
            tvSeason.Episodes.Select(x => x.EpisodeNumber).Concat(episodeNumbers).Distinct().Count());
        tvSeason.IdentifiedConfidence = null;
        tvSeason.IdentificationStatus = IdentificationStatus.Failed;
        tvSeason.UpdatedAt = now;

        var oldMovieIds = new HashSet<int>();
        var movedMediaFileIds = new HashSet<int>();
        foreach (var item in orderedItems)
        {
            var mediaFile = mediaFileIndex[item.Input.MediaFileId];
            if (mediaFile.MovieId.HasValue)
            {
                oldMovieIds.Add(mediaFile.MovieId.Value);
            }

            var episode = await UpsertGroupedPlaceholderEpisodeAsync(
                dbContext,
                tvSeason,
                item.EpisodeNumber,
                item.Input.FileName,
                now,
                cancellationToken);
            mediaFile.MovieId = null;
            mediaFile.Movie = null;
            mediaFile.Episode = episode;
            mediaFile.EpisodeId = episode.Id;
            mediaFile.UpdatedAt = now;
            episode.DefaultMediaFileId ??= mediaFile.Id;
            movedMediaFileIds.Add(mediaFile.Id);
        }

        await ReconcileMovieDefaultsAfterMovingFilesAsync(dbContext, oldMovieIds, movedMediaFileIds, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var movieId in oldMovieIds)
        {
            await CleanupMovieIfOrphanedAsync(dbContext, movieId, cancellationToken);
        }

        return new GroupedTvLikePlaceholderPersistenceResult(
            true,
            string.Empty,
            tvSeries.Id,
            tvSeason.Id,
            orderedItems.Count,
            orderedItems.Count,
            seriesResolution.Reused,
            seasonResolution.Reused);
    }

    private static async Task<UnknownSeriesResolution> ResolveUnknownSeriesForGroupedRangeAsync(
        AppDbContext dbContext,
        UnknownTvGroupingContext context,
        bool hasGroupingContext,
        string groupingSkippedReason,
        string fallbackSeriesName,
        string sourceKind,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (hasGroupingContext)
        {
            var candidateSeries = await dbContext.TvSeries
                .Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .Where(
                    x => !x.TmdbSeriesId.HasValue
                         && x.Seasons.Any(
                             season => !season.TmdbSeasonId.HasValue
                                       && season.IdentificationStatus == IdentificationStatus.Failed
                                       && season.Episodes.Any(
                                           episode => episode.MediaFiles.Any(
                                               source => source.SourceConnectionId == context.SourceConnectionId
                                                         && source.ScanPathId == context.ScanPathId
                                                         && source.MediaType == MediaType.Video
                                                         && !source.IsDeleted))))
                .ToListAsync(cancellationToken);

            var compatibleSeries = new List<TvSeries>();
            var skippedReasons = new List<string>();
            foreach (var series in candidateSeries)
            {
                if (UnknownTvGroupingKeyHelper.IsCompatibleSeries(series, context, out var skippedReason))
                {
                    compatibleSeries.Add(series);
                }
                else if (!string.IsNullOrWhiteSpace(skippedReason))
                {
                    skippedReasons.Add(skippedReason);
                }
            }

            if (compatibleSeries.Count == 1)
            {
                var reusedSeries = compatibleSeries[0];
                WriteUnknownSeriesReused(sourceKind, context, reusedSeries.Id);
                return new UnknownSeriesResolution(reusedSeries, true);
            }

            if (compatibleSeries.Count != 1 && candidateSeries.Count > 0)
            {
                WriteUnknownSeriesSkipped(
                    sourceKind,
                    context,
                    compatibleSeries.Count > 1
                        ? "multiple-compatible-unknown-series"
                        : SelectUnknownGroupingSkippedReason(skippedReasons, "no-compatible-unknown-series"),
                    candidateSeries.Count);
            }
        }
        else
        {
            WriteUnknownSeriesSkipped(sourceKind, context, groupingSkippedReason, 0);
        }

        var seriesName = TruncateRequired(BuildGroupedUnknownSeriesName(context, hasGroupingContext, fallbackSeriesName), 300);
        var tvSeries = new TvSeries
        {
            Name = seriesName,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.TvSeries.Add(tvSeries);
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteUnknownSeriesCreated(sourceKind, context, tvSeries.Id, hasGroupingContext ? string.Empty : groupingSkippedReason);
        return new UnknownSeriesResolution(tvSeries, false);
    }

    private static async Task<UnknownSeasonResolution> ResolveUnknownSeasonForGroupedRangeAsync(
        AppDbContext dbContext,
        TvSeries tvSeries,
        UnknownTvGroupingContext context,
        bool hasGroupingContext,
        string groupingSkippedReason,
        IReadOnlyCollection<int> episodeNumbers,
        string sourceKind,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (hasGroupingContext)
        {
            var candidateSeasons = await dbContext.TvSeasons
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .Where(
                    x => x.TvSeriesId == tvSeries.Id
                         && !x.TmdbSeasonId.HasValue
                         && x.IdentificationStatus == IdentificationStatus.Failed)
                .ToListAsync(cancellationToken);
            var compatibleSeasons = new List<TvSeason>();
            var skippedReasons = new List<string>();
            foreach (var season in candidateSeasons)
            {
                if (UnknownTvGroupingKeyHelper.IsCompatibleSeason(season, context, episodeNumbers, out var skippedReason))
                {
                    compatibleSeasons.Add(season);
                }
                else if (!string.IsNullOrWhiteSpace(skippedReason))
                {
                    skippedReasons.Add(skippedReason);
                }
            }

            if (compatibleSeasons.Count == 1)
            {
                var reusedSeason = compatibleSeasons[0];
                WriteUnknownSeasonReused(sourceKind, context, reusedSeason.TvSeriesId, reusedSeason.Id);
                return new UnknownSeasonResolution(reusedSeason, true);
            }

            if (compatibleSeasons.Count != 1 && candidateSeasons.Count > 0)
            {
                WriteUnknownSeasonSkipped(
                    sourceKind,
                    context,
                    compatibleSeasons.Count > 1
                        ? "multiple-compatible-unknown-seasons"
                        : SelectUnknownGroupingSkippedReason(skippedReasons, "no-compatible-unknown-season"),
                    candidateSeasons.Count,
                    tvSeries.Id);
            }
        }
        else
        {
            WriteUnknownSeasonSkipped(sourceKind, context, groupingSkippedReason, 0, tvSeries.Id);
        }

        var seasonNumber = await ResolveGroupedPlaceholderSeasonNumberAsync(dbContext, tvSeries.Id, cancellationToken);
        var tvSeason = new TvSeason
        {
            TvSeriesId = tvSeries.Id,
            SeasonNumber = seasonNumber,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.TvSeasons.Add(tvSeason);
        await dbContext.SaveChangesAsync(cancellationToken);
        WriteUnknownSeasonCreated(sourceKind, context, tvSeries.Id, tvSeason.Id, seasonNumber);
        return new UnknownSeasonResolution(tvSeason, false);
    }

    private static string SelectUnknownGroupingSkippedReason(
        IReadOnlyCollection<string> skippedReasons,
        string fallbackReason)
    {
        if (skippedReasons.Contains("ambiguous-existing-unknown-season-context", StringComparer.OrdinalIgnoreCase))
        {
            return "ambiguous-existing-unknown-season-context";
        }

        if (skippedReasons.Contains("ambiguous-existing-unknown-series-context", StringComparer.OrdinalIgnoreCase))
        {
            return "ambiguous-existing-unknown-series-context";
        }

        if (skippedReasons.Contains("strict-season-key-mismatch", StringComparer.OrdinalIgnoreCase))
        {
            return "strict-season-key-mismatch";
        }

        if (skippedReasons.Contains("strict-series-key-mismatch", StringComparer.OrdinalIgnoreCase))
        {
            return "strict-series-key-mismatch";
        }

        if (skippedReasons.Contains("no-existing-unknown-season-context", StringComparer.OrdinalIgnoreCase))
        {
            return "no-existing-unknown-season-context";
        }

        if (skippedReasons.Contains("no-existing-unknown-series-context", StringComparer.OrdinalIgnoreCase))
        {
            return "no-existing-unknown-series-context";
        }

        return fallbackReason;
    }

    private static string BuildGroupedUnknownSeriesName(
        UnknownTvGroupingContext context,
        bool hasGroupingContext,
        string fallbackSeriesName)
    {
        if (hasGroupingContext && !string.IsNullOrWhiteSpace(context.SeriesDisplayTitle))
        {
            return context.SeriesDisplayTitle;
        }

        return string.IsNullOrWhiteSpace(fallbackSeriesName)
            ? UnidentifiedTvSeriesCandidateTitle
            : fallbackSeriesName;
    }

    private static string BuildGroupedUnknownSeasonName(
        UnknownTvGroupingContext context,
        bool hasGroupingContext)
    {
        if (hasGroupingContext)
        {
            if (!string.IsNullOrWhiteSpace(context.SeasonDisplayTitle))
            {
                return context.SeasonDisplayTitle;
            }

            if (!string.IsNullOrWhiteSpace(context.SeasonRange))
            {
                return context.SeasonRange;
            }
        }

        return UnidentifiedTvSeasonCandidateTitle;
    }

    private static void WriteUnknownSeriesGroupingCandidate(string sourceKind, UnknownTvGroupingContext context)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-series-grouping-candidate sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={context.SourceConnectionId} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeriesTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeriesGroupingKeyHash)} seasonRange={ScanIdentificationDiagnostics.FormatValue(context.SeasonRange)}");
    }

    private static void WriteUnknownSeriesReused(
        string sourceKind,
        UnknownTvGroupingContext context,
        int targetSeriesId)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-series-reused sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={context.SourceConnectionId} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeriesTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeriesGroupingKeyHash)} targetSeriesId={targetSeriesId} namePreserved=true");
    }

    private static void WriteUnknownSeriesCreated(
        string sourceKind,
        UnknownTvGroupingContext context,
        int targetSeriesId,
        string skippedReason)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-series-created sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={ScanIdentificationDiagnostics.FormatNullable(context.SourceConnectionId > 0 ? context.SourceConnectionId : null)} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeriesTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeriesGroupingKeyHash)} targetSeriesId={targetSeriesId} skippedReason={ScanIdentificationDiagnostics.FormatValue(skippedReason)}");
    }

    private static void WriteUnknownSeriesSkipped(
        string sourceKind,
        UnknownTvGroupingContext context,
        string skippedReason,
        int existingSeriesCandidates)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-series-skipped sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={ScanIdentificationDiagnostics.FormatNullable(context.SourceConnectionId > 0 ? context.SourceConnectionId : null)} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeriesTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeriesGroupingKeyHash)} existingSeriesCandidates={existingSeriesCandidates} skippedReason={ScanIdentificationDiagnostics.FormatValue(skippedReason)}");
    }

    private static void WriteUnknownSeasonReused(
        string sourceKind,
        UnknownTvGroupingContext context,
        int targetSeriesId,
        int targetSeasonId)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-reused sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={context.SourceConnectionId} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeasonTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeasonGroupingKeyHash)} seasonRange={ScanIdentificationDiagnostics.FormatValue(context.SeasonRange)} targetSeriesId={targetSeriesId} targetSeasonId={targetSeasonId} namePreserved=true");
    }

    private static void WriteUnknownSeasonCreated(
        string sourceKind,
        UnknownTvGroupingContext context,
        int targetSeriesId,
        int targetSeasonId,
        int seasonNumber)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-created sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={ScanIdentificationDiagnostics.FormatNullable(context.SourceConnectionId > 0 ? context.SourceConnectionId : null)} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeasonTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeasonGroupingKeyHash)} seasonRange={ScanIdentificationDiagnostics.FormatValue(context.SeasonRange)} targetSeriesId={targetSeriesId} targetSeasonId={targetSeasonId} seasonNumber={seasonNumber}");
    }

    private static void WriteUnknownSeasonSkipped(
        string sourceKind,
        UnknownTvGroupingContext context,
        string skippedReason,
        int existingSeasonCandidates,
        int targetSeriesId)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-skipped sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} sourceConnectionId={ScanIdentificationDiagnostics.FormatNullable(context.SourceConnectionId > 0 ? context.SourceConnectionId : null)} scanPathId={ScanIdentificationDiagnostics.FormatNullable(context.ScanPathId)} parentDirectoryHash={ScanIdentificationDiagnostics.FormatValue(context.ParentDirectoryHash)} normalizedTitle={ScanIdentificationDiagnostics.FormatValue(context.NormalizedSeasonTitle)} groupingKey={ScanIdentificationDiagnostics.FormatValue(context.SeasonGroupingKeyHash)} seasonRange={ScanIdentificationDiagnostics.FormatValue(context.SeasonRange)} targetSeriesId={targetSeriesId} existingSeasonCandidates={existingSeasonCandidates} skippedReason={ScanIdentificationDiagnostics.FormatValue(skippedReason)}");
    }

    private static async Task<TvEpisode> UpsertGroupedPlaceholderEpisodeAsync(
        AppDbContext dbContext,
        TvSeason tvSeason,
        int episodeNumber,
        string fileName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.TvEpisodes
            .FirstOrDefaultAsync(
                x => x.TvSeasonId == tvSeason.Id
                     && x.EpisodeNumber == episodeNumber,
                cancellationToken);
        if (episode is null)
        {
            episode = new TvEpisode
            {
                TvSeasonId = tvSeason.Id,
                EpisodeNumber = episodeNumber,
                DefaultMediaFileId = null,
                CreatedAt = now
            };
            dbContext.TvEpisodes.Add(episode);
            tvSeason.Episodes.Add(episode);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        episode.TmdbEpisodeId = null;
        episode.Title = TruncateRequired(string.IsNullOrWhiteSpace(fileName) ? $"Episode {episodeNumber}" : fileName.Trim(), 300);
        episode.Overview = null;
        episode.StillRemoteUrl = null;
        episode.AirDate = null;
        episode.RuntimeMinutes = null;
        episode.DefaultMediaFileId ??= tvSeason.Episodes
            .Where(x => x.Id == episode.Id)
            .SelectMany(x => x.MediaFiles)
            .OrderBy(x => x.FileName)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        episode.UpdatedAt = now;
        return episode;
    }

    private static async Task<int> ResolveGroupedPlaceholderSeasonNumberAsync(
        AppDbContext dbContext,
        int tvSeriesId,
        CancellationToken cancellationToken)
    {
        var usedSeasonNumbers = await dbContext.TvSeasons
            .Where(x => x.TvSeriesId == tvSeriesId)
            .Select(x => x.SeasonNumber)
            .ToListAsync(cancellationToken);
        return usedSeasonNumbers.Count == 0 ? 1 : usedSeasonNumbers.Max() + 1;
    }

    private static async Task ReconcileMovieDefaultsAfterMovingFilesAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> movieIds,
        IReadOnlySet<int> movedMediaFileIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0 || movedMediaFileIds.Count == 0)
        {
            return;
        }

        var movies = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Where(x => movieIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var movie in movies)
        {
            if (!movie.DefaultMediaFileId.HasValue || !movedMediaFileIds.Contains(movie.DefaultMediaFileId.Value))
            {
                continue;
            }

            movie.DefaultMediaFileId = movie.MediaFiles
                .Where(x => !movedMediaFileIds.Contains(x.Id) && x.MediaType == MediaType.Video && !x.IsDeleted)
                .OrderBy(x => x.FileName)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
            movie.UpdatedAt = now;
        }
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

    private static bool TryParseMoviePlaceholderEpisodePattern(
        string fileName,
        out MoviePlaceholderEpisodePattern pattern,
        out string skippedReason)
    {
        pattern = new MoviePlaceholderEpisodePattern(string.Empty, string.Empty, 0);
        skippedReason = string.Empty;

        var name = System.Net.WebUtility.HtmlDecode(Path.GetFileNameWithoutExtension(fileName)).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            skippedReason = "empty-name";
            return false;
        }

        if (MoviePlaceholderExcludedTokenRegex().IsMatch(name))
        {
            skippedReason = "excluded-non-episode-token";
            return false;
        }

        var bareNumberMatch = MoviePlaceholderBareNumberRegex().Match(name);
        if (bareNumberMatch.Success && TryReadPositiveEpisodeNumber(bareNumberMatch.Groups["episode"].Value, out var bareNumber))
        {
            pattern = new MoviePlaceholderEpisodePattern("bare-number", "bare-number", bareNumber);
            return true;
        }

        var markerMatch = MoviePlaceholderMarkerEpisodeRegex().Match(name);
        if (markerMatch.Success && TryReadPositiveEpisodeNumber(markerMatch.Groups["episode"].Value, out var markerNumber))
        {
            pattern = new MoviePlaceholderEpisodePattern("episode-marker", "episode-marker", markerNumber);
            return true;
        }

        var chineseMatch = MoviePlaceholderChineseEpisodeRegex().Match(name);
        if (chineseMatch.Success && TryReadPositiveEpisodeNumber(chineseMatch.Groups["episode"].Value, out var chineseNumber))
        {
            pattern = new MoviePlaceholderEpisodePattern("chinese-episode-marker", "chinese-episode-marker", chineseNumber);
            return true;
        }

        var titleNumberName = MoviePlaceholderWhitespaceRegex()
            .Replace(
                MoviePlaceholderSeparatorRegex().Replace(
                    MoviePlaceholderBracketedContentRegex().Replace(name, " "),
                    " "),
                " ")
            .Trim();
        var titleNumberMatch = MoviePlaceholderTitleNumberRegex().Match(titleNumberName);
        if (titleNumberMatch.Success
            && TryReadPositiveEpisodeNumber(titleNumberMatch.Groups["episode"].Value, out var titleNumber)
            && TryNormalizeMoviePlaceholderTitlePrefix(titleNumberMatch.Groups["prefix"].Value, out var prefixKey))
        {
            pattern = new MoviePlaceholderEpisodePattern($"title-number:{prefixKey}", "title-number", titleNumber);
            return true;
        }

        skippedReason = "no-supported-episode-number";
        return false;
    }

    private static bool TryNormalizeMoviePlaceholderTitlePrefix(string value, out string prefixKey)
    {
        prefixKey = string.Empty;
        var normalized = NormalizeQueryToken(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var meaningfulTokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !IsLowInformationMovieToken(token, null))
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

    private static bool TryReadPositiveEpisodeNumber(string value, out int number)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number > 0;
    }

    private static string GetDirectParentPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        var normalized = filePath.Replace('\\', '/').TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index > 0 ? normalized[..index] : string.Empty;
    }

    private static void AddCount(IDictionary<string, int> counts, string key)
    {
        key = string.IsNullOrWhiteSpace(key) ? "unknown" : key;
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }

    private async Task<IReadOnlyList<MetadataSearchCandidate>> SearchMoviesAsync(
        string query,
        int? releaseYear,
        ScanTmdbSearchCache? tmdbSearchCache,
        CancellationToken cancellationToken)
    {
        if (tmdbSearchCache is not null
            && tmdbSearchCache.TryGetMovieSearch(query, releaseYear, out var cachedResult))
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tmdb-movie-search-cache-hit query={ScanIdentificationDiagnostics.FormatValue(query)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(releaseYear)} tmdbMovieSearchCacheHit={tmdbSearchCache.MovieSearchCacheHits} tmdbMovieSearchCacheMiss={tmdbSearchCache.MovieSearchCacheMisses} tmdbSearchCacheEntries={tmdbSearchCache.MovieSearchCacheEntries} duplicateSearchAvoided={tmdbSearchCache.DuplicateSearchAvoided}");
            return cachedResult;
        }

        if (tmdbSearchCache is not null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=tmdb-movie-search-cache-miss query={ScanIdentificationDiagnostics.FormatValue(query)} releaseYear={ScanIdentificationDiagnostics.FormatNullable(releaseYear)} tmdbMovieSearchCacheHit={tmdbSearchCache.MovieSearchCacheHits} tmdbMovieSearchCacheMiss={tmdbSearchCache.MovieSearchCacheMisses} tmdbSearchCacheEntries={tmdbSearchCache.MovieSearchCacheEntries}");
        }

        var result = await _tmdbService.SearchMoviesAsync(query, releaseYear, cancellationToken);
        tmdbSearchCache?.SetMovieSearch(query, releaseYear, result);
        return result;
    }

    public async Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(
        string query,
        int? releaseYear,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var candidates = await _tmdbService.SearchMoviesAsync(normalizedQuery, releaseYear, cancellationToken);
        foreach (var candidate in candidates)
        {
            candidate.Confidence = CalculateConfidence(normalizedQuery, releaseYear, candidate);
        }

        return releaseYear.HasValue
            ? candidates
                .OrderBy(candidate => GetYearSortGroup(releaseYear.Value, candidate.ReleaseYear))
                .ThenBy(candidate => GetYearDistance(releaseYear.Value, candidate.ReleaseYear))
                .ThenByDescending(candidate => candidate.Confidence)
                .ToList()
            : candidates
                .OrderByDescending(candidate => candidate.Confidence)
                .ToList();
    }

    public async Task<AutoIdentifyResult> AutoIdentifyWithFirstResultAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        if (movieId <= 0)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Failed, "影片不存在。");
        }

        AiSearchSuggestionResult suggestionResult;
        var suggestStopwatch = Stopwatch.StartNew();
        try
        {
            suggestionResult = await _aiClassificationService.SuggestSearchQueryWithStatusAsync(movieId, cancellationToken);
            suggestStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-suggest-complete movieId={movieId} elapsedMs={suggestStopwatch.ElapsedMilliseconds} status={FormatStatus(suggestionResult.Status)}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            suggestStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-suggest-complete movieId={movieId} elapsedMs={suggestStopwatch.ElapsedMilliseconds} status=cancelled");
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Cancelled, "操作已取消。");
        }
        catch (Exception exception)
        {
            suggestStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-suggest-complete movieId={movieId} elapsedMs={suggestStopwatch.ElapsedMilliseconds} status=failed");
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Failed, $"AI 搜索词生成失败：{TrimMessage(exception.Message)}");
        }

        if (suggestionResult.Status == AiSearchSuggestionStatus.NoResult)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.NoResult, suggestionResult.Message);
        }

        if (suggestionResult.Status == AiSearchSuggestionStatus.Failed)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Failed, suggestionResult.Message);
        }

        var query = suggestionResult.Suggestion.Query.Trim();
        var releaseYear = suggestionResult.Suggestion.ReleaseYear;
        if (string.IsNullOrWhiteSpace(query))
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.NoResult, "AI 未返回可用搜索标题。");
        }

        IReadOnlyList<MetadataSearchCandidate> candidates;
        var searchStopwatch = Stopwatch.StartNew();
        try
        {
            candidates = await SearchCandidatesAsync(query, releaseYear, cancellationToken);
            searchStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-search-complete movieId={movieId} elapsedMs={searchStopwatch.ElapsedMilliseconds} candidateCount={candidates.Count} status=success");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            searchStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-search-complete movieId={movieId} elapsedMs={searchStopwatch.ElapsedMilliseconds} candidateCount=0 status=cancelled");
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Cancelled, "操作已取消。", query, releaseYear);
        }
        catch (Exception exception)
        {
            searchStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-search-complete movieId={movieId} elapsedMs={searchStopwatch.ElapsedMilliseconds} candidateCount=0 status=failed");
            return BuildAutoIdentifyResult(
                movieId,
                AutoIdentifyStatus.Failed,
                $"TMDB 搜索失败：{TrimMessage(exception.Message)}",
                query,
                releaseYear);
        }

        var firstCandidate = candidates.FirstOrDefault();
        if (firstCandidate is null)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.NoResult, "没有找到符合条件的 TMDB 结果。", query, releaseYear);
        }

        if (firstCandidate.TmdbId <= 0)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Failed, "第一个 TMDB 候选无效。", query, releaseYear);
        }

        MetadataSearchCandidate? details;
        var detailStopwatch = Stopwatch.StartNew();
        try
        {
            details = await _tmdbService.GetMovieDetailsAsync(firstCandidate.TmdbId, cancellationToken);
            detailStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-detail-complete movieId={movieId} elapsedMs={detailStopwatch.ElapsedMilliseconds} status={(details is null ? "no-result" : "success")}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            detailStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-detail-complete movieId={movieId} elapsedMs={detailStopwatch.ElapsedMilliseconds} status=cancelled");
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Cancelled, "操作已取消。", query, releaseYear);
        }
        catch (Exception exception)
        {
            detailStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-detail-complete movieId={movieId} elapsedMs={detailStopwatch.ElapsedMilliseconds} status=failed");
            return BuildAutoIdentifyResult(
                movieId,
                AutoIdentifyStatus.Failed,
                $"TMDB 详情读取失败：{TrimMessage(exception.Message)}",
                query,
                releaseYear,
                firstCandidate);
        }

        if (details is null)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Failed, "无法读取 TMDB 影片详情。", query, releaseYear, firstCandidate);
        }

        details.Confidence = firstCandidate.Confidence;
        if (cancellationToken.IsCancellationRequested)
        {
            return BuildAutoIdentifyResult(movieId, AutoIdentifyStatus.Cancelled, "操作已取消。", query, releaseYear, firstCandidate);
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var applyStopwatch = Stopwatch.StartNew();
        var applyDbStopwatch = new Stopwatch();
        WriteBatch2Event($"event=batch2-ai-identify-apply-start movieId={movieId} includeOmdbRating=false");
        if (!string.IsNullOrWhiteSpace(details.ImdbId))
        {
            WriteBatch2Event($"event=batch2-ai-identify-omdb-skip movieId={movieId} reason=batch2-critical-path");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        try
        {
            var currentMovie = await LoadMovieAggregateAsync(dbContext, movieId, CancellationToken.None)
                ?? throw new InvalidOperationException("待识别的影片不存在。");
            applyDbStopwatch.Start();
            var targetMovieId = await ApplyManualMatchCoreAsync(
                dbContext,
                currentMovie,
                details,
                CancellationToken.None,
                includeOmdbRating: false);
            applyDbStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-apply-db-complete movieId={movieId} elapsedMs={applyDbStopwatch.ElapsedMilliseconds} status=success");
            var commitStopwatch = Stopwatch.StartNew();
            await transaction.CommitAsync(CancellationToken.None);
            commitStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-apply-commit-complete movieId={movieId} elapsedMs={commitStopwatch.ElapsedMilliseconds} status=success");
            applyStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-apply-complete movieId={movieId} elapsedMs={applyStopwatch.ElapsedMilliseconds} status=success");
            return BuildAutoIdentifyResult(
                movieId,
                AutoIdentifyStatus.Success,
                $"已应用第一候选：{details.Title}",
                query,
                releaseYear,
                details,
                targetMovieId);
        }
        catch (Exception exception)
        {
            if (applyDbStopwatch.IsRunning)
            {
                applyDbStopwatch.Stop();
            }

            WriteBatch2Event(
                $"event=batch2-ai-identify-apply-db-complete movieId={movieId} elapsedMs={applyDbStopwatch.ElapsedMilliseconds} status=failed");
            await transaction.RollbackAsync(CancellationToken.None);
            applyStopwatch.Stop();
            WriteBatch2Event(
                $"event=batch2-ai-identify-apply-complete movieId={movieId} elapsedMs={applyStopwatch.ElapsedMilliseconds} status=failed");
            return BuildAutoIdentifyResult(
                movieId,
                AutoIdentifyStatus.Failed,
                $"应用第一候选失败：{TrimMessage(exception.Message)}",
                query,
                releaseYear,
                details);
        }
    }

    public async Task<int> ApplyManualMatchAsync(
        int movieId,
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var currentMovie = await LoadMovieAggregateAsync(dbContext, movieId, cancellationToken)
            ?? throw new InvalidOperationException("待修正的影片不存在。");

        var details = await _tmdbService.GetMovieDetailsAsync(tmdbId, cancellationToken)
            ?? throw new InvalidOperationException("无法读取 TMDB 影片详情。");

        return await ApplyManualMatchCoreAsync(dbContext, currentMovie, details, cancellationToken);
    }

    public async Task<int> ApplyManualMediaFileMatchAsync(
        int mediaFileId,
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaFileId));
        }

        if (tmdbId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tmdbId));
        }

        var details = await _tmdbService.GetMovieDetailsAsync(tmdbId, cancellationToken)
            ?? throw new InvalidOperationException("无法读取 TMDB 影片详情。");

        ScanIdentificationDiagnostics.Write(
            $"event=correction-movie-details-loaded mediaFileId={mediaFileId} tmdbId={tmdbId} hasImdbId={(!string.IsNullOrWhiteSpace(details.ImdbId)).ToString().ToLowerInvariant()}");

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=correction-movie-db-transaction-started mediaFileId={mediaFileId} tmdbId={tmdbId} includeOmdbRating=false");
        var mediaFile = await dbContext.MediaFiles
            .Include(x => x.SourceConnection)
            .Include(x => x.Movie)
            .ThenInclude(x => x!.RatingSources)
            .Include(x => x.Episode)
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("待修正的播放源不存在。");

        var previousMovieId = mediaFile.MovieId;
        var previousEpisodeId = mediaFile.EpisodeId;
        mediaFile.EpisodeId = null;
        mediaFile.Episode = null;
        await ApplyCandidateAsync(
            dbContext,
            mediaFile,
            details,
            IdentificationStatus.ManualConfirmed,
            new IdentificationRunResult(),
            cancellationToken,
            includeOmdbRating: false);
        ScanIdentificationDiagnostics.Write(
            $"event=correction-movie-db-applied mediaFileId={mediaFileId} tmdbId={tmdbId}");

        var targetMovieId = mediaFile.MovieId ?? mediaFile.Movie?.Id ?? throw new InvalidOperationException("影片修正结果无效。");
        if (previousMovieId.HasValue && previousMovieId.Value != targetMovieId)
        {
            await ReconcileMovieAfterSourceMoveAsync(dbContext, previousMovieId.Value, mediaFile.Id, cancellationToken);
        }

        if (previousEpisodeId.HasValue)
        {
            await ReconcileEpisodeAfterSourceMoveAsync(dbContext, previousEpisodeId.Value, mediaFile.Id, cancellationToken);
        }

        await SetMovieDefaultSourceAsync(dbContext, targetMovieId, mediaFile.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=correction-movie-db-committed mediaFileId={mediaFileId} targetMovieId={targetMovieId}");
        return targetMovieId;
    }

    private async Task<int> ApplyManualMatchCoreAsync(
        AppDbContext dbContext,
        Movie currentMovie,
        MetadataSearchCandidate details,
        CancellationToken cancellationToken,
        bool includeOmdbRating = true)
    {
        var currentMovieOriginalTmdbId = currentMovie.TmdbId;
        var currentMovieHasStableIdentity = IsStableIdentifiedMovie(currentMovie);
        var canPreserveSourceState = currentMovieHasStableIdentity && currentMovieOriginalTmdbId == details.TmdbId;
        var existingMatchedMovie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .ThenInclude(x => x.SourceConnection)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.TmdbId == details.TmdbId && x.Id != currentMovie.Id, cancellationToken);

        if (existingMatchedMovie is not null && currentMovie.DefaultMediaFileId.HasValue)
        {
            currentMovie.DefaultMediaFileId = null;
            currentMovie.AiTagsText = null;
            currentMovie.EmotionTagsText = null;
            currentMovie.SceneTagsText = null;
            currentMovie.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var targetMovie = existingMatchedMovie ?? currentMovie;
        var noop = new IdentificationRunResult();
        await ApplyMetadataAsync(
            dbContext,
            targetMovie,
            details,
            IdentificationStatus.ManualConfirmed,
            1d,
            noop,
            cancellationToken,
            includeOmdbRating);

        if (existingMatchedMovie is not null)
        {
            if (canPreserveSourceState)
            {
                targetMovie.IsFavorite |= currentMovie.IsFavorite;
                targetMovie.IsWatched |= currentMovie.IsWatched;
                targetMovie.UserRating ??= currentMovie.UserRating;
                targetMovie.LastPlayedAt = MaxDate(targetMovie.LastPlayedAt, currentMovie.LastPlayedAt);
                targetMovie.AutoWatchedBaselineAtUtc = MaxDate(targetMovie.AutoWatchedBaselineAtUtc, currentMovie.AutoWatchedBaselineAtUtc);
            }

            currentMovie.IsFavorite = false;
            currentMovie.IsWatched = false;
            currentMovie.UserRating = null;
            currentMovie.LastPlayedAt = null;
            currentMovie.AutoWatchedBaselineAtUtc = null;

            var currentMediaFiles = await dbContext.MediaFiles
                .Include(x => x.SourceConnection)
                .Where(x => x.MovieId == currentMovie.Id)
                .ToListAsync(cancellationToken);
            var currentMediaFileIds = currentMediaFiles.Select(x => x.Id).ToArray();
            var staleDefaultMovies = await dbContext.Movies
                .Where(x => x.Id != currentMovie.Id
                            && x.Id != targetMovie.Id
                            && x.DefaultMediaFileId.HasValue
                            && currentMediaFileIds.Contains(x.DefaultMediaFileId.Value))
                .ToListAsync(cancellationToken);

            foreach (var staleDefaultMovie in staleDefaultMovies)
            {
                staleDefaultMovie.DefaultMediaFileId = null;
                staleDefaultMovie.UpdatedAt = DateTime.UtcNow;
            }

            if (staleDefaultMovies.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            foreach (var mediaFile in currentMediaFiles)
            {
                mediaFile.MovieId = targetMovie.Id;
                mediaFile.UpdatedAt = DateTime.UtcNow;
            }

            var currentWatchHistories = await dbContext.WatchHistories
                .Where(x => x.MovieId == currentMovie.Id)
                .ToListAsync(cancellationToken);

            foreach (var history in currentWatchHistories)
            {
                history.MovieId = targetMovie.Id;
            }

            var currentCollectionItems = await dbContext.UserMovieCollectionItems
                .Where(x => x.MovieId == currentMovie.Id)
                .ToListAsync(cancellationToken);

            foreach (var collectionItem in currentCollectionItems)
            {
                if (canPreserveSourceState || collectionItem.TmdbId == targetMovie.TmdbId)
                {
                    collectionItem.MovieId = targetMovie.Id;
                    ApplyIdentificationSnapshot(collectionItem, targetMovie);
                    collectionItem.IsInLibrary = targetMovie.MediaFiles.Any(x => x.MediaType == MediaType.Video && !x.IsDeleted)
                                                 || currentMediaFiles.Any(x => x.MediaType == MediaType.Video && !x.IsDeleted);
                    collectionItem.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                ClearUnidentifiedCollectionState(dbContext, collectionItem, DateTime.UtcNow);
            }

            if (!PromoteLocalDefaultSource(targetMovie, currentMediaFiles.Concat(targetMovie.MediaFiles))
                && !targetMovie.DefaultMediaFileId.HasValue)
            {
                targetMovie.DefaultMediaFileId = currentMovie.DefaultMediaFileId
                    ?? currentMediaFiles.FirstOrDefault(x => x.MediaType == MediaType.Video)?.Id
                    ?? targetMovie.MediaFiles.FirstOrDefault(x => x.MediaType == MediaType.Video)?.Id;
            }
        }
        else if (!PromoteLocalDefaultSource(targetMovie, targetMovie.MediaFiles)
                 && !targetMovie.DefaultMediaFileId.HasValue)
        {
            targetMovie.DefaultMediaFileId = targetMovie.MediaFiles
                .Where(x => x.MediaType == MediaType.Video)
                .Select(x => x.Id)
                .FirstOrDefault();
        }

        if (!canPreserveSourceState && existingMatchedMovie is null)
        {
            var now = DateTime.UtcNow;
            ClearUnidentifiedMovieState(targetMovie, now);
            await ReconcileCollectionItemsAfterIdentificationAsync(
                dbContext,
                targetMovie,
                preserveSourceState: false,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (existingMatchedMovie is not null)
        {
            await CleanupMovieIfOrphanedAsync(dbContext, currentMovie.Id, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return targetMovie.Id;
    }

    private async Task ApplyCandidateAsync(
        AppDbContext dbContext,
        MediaFile mediaFile,
        MetadataSearchCandidate candidate,
        IdentificationStatus status,
        IdentificationRunResult result,
        CancellationToken cancellationToken,
        bool includeOmdbRating = true)
    {
        var currentMovie = mediaFile.MovieId.HasValue
            ? await LoadMovieAggregateAsync(dbContext, mediaFile.MovieId.Value, cancellationToken)
            : null;
        var currentMovieOriginalTmdbId = currentMovie?.TmdbId;
        var currentMovieHasStableIdentity = currentMovie is not null && IsStableIdentifiedMovie(currentMovie);
        var canPreserveSourceState = currentMovieHasStableIdentity && currentMovieOriginalTmdbId == candidate.TmdbId;

        var targetMovie = !string.IsNullOrWhiteSpace(candidate.ImdbId) || candidate.TmdbId > 0
            ? await dbContext.Movies
                .Include(x => x.MediaFiles)
                .ThenInclude(x => x.SourceConnection)
                .Include(x => x.RatingSources)
                .Include(x => x.WatchHistories)
                .FirstOrDefaultAsync(x => x.TmdbId == candidate.TmdbId, cancellationToken)
            : null;

        if (targetMovie is null)
        {
            if (currentMovie is not null
                && !currentMovie.TmdbId.HasValue
                && currentMovie.IdentificationStatus != IdentificationStatus.ManualConfirmed)
            {
                targetMovie = currentMovie;
            }
            else
            {
                targetMovie = new Movie
                {
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Movies.Add(targetMovie);
            }
        }

        await ApplyMetadataAsync(
            dbContext,
            targetMovie,
            candidate,
            status,
            candidate.Confidence,
            result,
            cancellationToken,
            includeOmdbRating);

        if (targetMovie.Id == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            ScanIdentificationDiagnostics.Write(
                $"event=correction-movie-target-materialized mediaFileId={mediaFile.Id} targetMovieId={targetMovie.Id}");
        }

        if (currentMovie is not null && currentMovie.Id != targetMovie.Id)
        {
            if (currentMovie.DefaultMediaFileId == mediaFile.Id)
            {
                currentMovie.DefaultMediaFileId = SelectPreferredDefaultMediaFileId(currentMovie.MediaFiles, mediaFile.Id);
                currentMovie.UpdatedAt = DateTime.UtcNow;
                ScanIdentificationDiagnostics.Write(
                    $"event=correction-movie-old-default-recalculated oldMovieId={currentMovie.Id} movedMediaFileId={mediaFile.Id} fallbackMediaFileId={ScanIdentificationDiagnostics.FormatNullable(currentMovie.DefaultMediaFileId)}");
            }

            var transferWholeCurrentMovie = currentMovie.MediaFiles
                .Where(x => x.MediaType == MediaType.Video && !x.IsDeleted)
                .All(x => x.Id == mediaFile.Id);
            var currentWatchHistories = await dbContext.WatchHistories
                .Where(x => x.MovieId == currentMovie.Id && x.MediaFileId == mediaFile.Id)
                .ToListAsync(cancellationToken);
            foreach (var history in currentWatchHistories)
            {
                history.MovieId = targetMovie.Id;
            }

            if (transferWholeCurrentMovie)
            {
                if (canPreserveSourceState)
                {
                    targetMovie.IsFavorite |= currentMovie.IsFavorite;
                    targetMovie.IsWatched |= currentMovie.IsWatched;
                    targetMovie.UserRating ??= currentMovie.UserRating;
                    targetMovie.LastPlayedAt = MaxDate(targetMovie.LastPlayedAt, currentMovie.LastPlayedAt);
                    targetMovie.AutoWatchedBaselineAtUtc = MaxDate(targetMovie.AutoWatchedBaselineAtUtc, currentMovie.AutoWatchedBaselineAtUtc);
                }

                var currentCollectionItems = await dbContext.UserMovieCollectionItems
                    .Where(x => x.MovieId == currentMovie.Id)
                    .ToListAsync(cancellationToken);
                var now = DateTime.UtcNow;
                foreach (var collectionItem in currentCollectionItems)
                {
                    if (canPreserveSourceState || collectionItem.TmdbId == targetMovie.TmdbId)
                    {
                        collectionItem.MovieId = targetMovie.Id;
                        ApplyIdentificationSnapshot(collectionItem, targetMovie);
                        collectionItem.IsInLibrary = targetMovie.MediaFiles.Any(x => x.MediaType == MediaType.Video && !x.IsDeleted)
                                                     || mediaFile.MediaType == MediaType.Video;
                        collectionItem.UpdatedAt = now;
                        continue;
                    }

                    ClearUnidentifiedCollectionState(dbContext, collectionItem, now);
                }

                currentMovie.IsFavorite = false;
                currentMovie.IsWatched = false;
                currentMovie.UserRating = null;
                currentMovie.LastPlayedAt = null;
                currentMovie.AutoWatchedBaselineAtUtc = null;
            }
        }

        mediaFile.EpisodeId = null;
        mediaFile.Episode = null;
        mediaFile.Movie = targetMovie;
        mediaFile.UpdatedAt = DateTime.UtcNow;

        if (!PromoteLocalDefaultSource(targetMovie, [mediaFile])
            && !targetMovie.DefaultMediaFileId.HasValue)
        {
            targetMovie.DefaultMediaFileId = mediaFile.Id;
        }

        if (!canPreserveSourceState && currentMovie is not null && currentMovie.Id == targetMovie.Id)
        {
            var now = DateTime.UtcNow;
            ClearUnidentifiedMovieState(targetMovie, now);
            await ReconcileCollectionItemsAfterIdentificationAsync(
                dbContext,
                targetMovie,
                preserveSourceState: false,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (currentMovie is not null && currentMovie.Id != targetMovie.Id)
        {
            await CleanupMovieIfOrphanedAsync(dbContext, currentMovie.Id, cancellationToken);
        }
    }

    private static bool IsStableIdentifiedMovie(Movie movie)
    {
        return movie.TmdbId is > 0
               && (movie.IdentificationStatus == IdentificationStatus.Matched
                   || movie.IdentificationStatus == IdentificationStatus.ManualConfirmed);
    }

    private static void ClearUnidentifiedMovieState(Movie movie, DateTime updatedAtUtc)
    {
        movie.IsWatched = false;
        movie.IsFavorite = false;
        movie.UserRating = null;
        movie.LastPlayedAt = null;
        movie.AutoWatchedBaselineAtUtc = null;
        movie.UpdatedAt = updatedAtUtc;
    }

    private static async Task ReconcileCollectionItemsAfterIdentificationAsync(
        AppDbContext dbContext,
        Movie movie,
        bool preserveSourceState,
        CancellationToken cancellationToken)
    {
        if (movie.TmdbId is not > 0)
        {
            return;
        }

        var collectionItems = await dbContext.UserMovieCollectionItems
            .Where(x => x.MovieId == movie.Id || x.TmdbId == movie.TmdbId.Value)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var collectionItem in collectionItems)
        {
            if (preserveSourceState || collectionItem.TmdbId == movie.TmdbId.Value)
            {
                ApplyIdentificationSnapshot(collectionItem, movie);
                collectionItem.UpdatedAt = now;
                continue;
            }

            ClearUnidentifiedCollectionState(dbContext, collectionItem, now);
        }
    }

    private static void ClearUnidentifiedCollectionState(
        AppDbContext dbContext,
        UserMovieCollectionItem collectionItem,
        DateTime updatedAtUtc)
    {
        collectionItem.IsWatched = false;
        collectionItem.IsWantToWatch = false;
        collectionItem.IsNotInterested = false;
        collectionItem.IsInLibrary = false;
        collectionItem.UpdatedAt = updatedAtUtc;
        if (!collectionItem.IsWatched && !collectionItem.IsWantToWatch && !collectionItem.IsNotInterested)
        {
            dbContext.UserMovieCollectionItems.Remove(collectionItem);
        }
    }

    private static void ApplyIdentificationSnapshot(
        UserMovieCollectionItem collectionItem,
        Movie movie)
    {
        collectionItem.MovieId = movie.Id;
        collectionItem.TmdbId = movie.TmdbId;
        collectionItem.Title = movie.Title;
        collectionItem.OriginalTitle = movie.OriginalTitle ?? string.Empty;
        collectionItem.ReleaseYear = movie.ReleaseYear;
        collectionItem.ReleaseDate = movie.ReleaseDate;
        collectionItem.PosterRemoteUrl = movie.PosterRemoteUrl ?? string.Empty;
        collectionItem.Overview = movie.Overview ?? string.Empty;
        collectionItem.GenresText = movie.GenresText ?? string.Empty;
        collectionItem.Country = movie.Country ?? string.Empty;
        collectionItem.Language = movie.Language ?? string.Empty;
        collectionItem.RuntimeMinutes = movie.RuntimeMinutes;
        collectionItem.ImdbId = movie.ImdbId ?? string.Empty;
        collectionItem.IsInLibrary = movie.MediaFiles.Any(media => media.MediaType == MediaType.Video && !media.IsDeleted);

        var tmdbRating = movie.RatingSources
            .FirstOrDefault(x => string.Equals(x.SourceName, "TMDB", StringComparison.OrdinalIgnoreCase));
        var omdbRating = movie.RatingSources
            .FirstOrDefault(x => string.Equals(x.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase));
        collectionItem.TmdbRating = tmdbRating?.ScoreValue;
        collectionItem.TmdbVoteCount = tmdbRating?.VoteCount;
        collectionItem.OmdbScoreValue = omdbRating?.ScoreValue;
        collectionItem.OmdbScoreScale = omdbRating?.ScoreScale;
        collectionItem.OmdbVoteCount = omdbRating?.VoteCount;
        collectionItem.OmdbSourceUrl = omdbRating?.SourceUrl ?? string.Empty;
        collectionItem.OmdbLastUpdatedAt = omdbRating?.LastUpdatedAt;
    }

    private async Task ApplyMetadataAsync(
        AppDbContext dbContext,
        Movie movie,
        MetadataSearchCandidate candidate,
        IdentificationStatus status,
        double? confidence,
        IdentificationRunResult result,
        CancellationToken cancellationToken,
        bool includeOmdbRating = true)
    {
        movie.Title = candidate.Title;
        movie.OriginalTitle = string.IsNullOrWhiteSpace(candidate.OriginalTitle) ? null : candidate.OriginalTitle;
        movie.ReleaseYear = candidate.ReleaseYear;
        movie.ReleaseDate = candidate.ReleaseDate;
        movie.Overview = string.IsNullOrWhiteSpace(candidate.Overview) ? null : candidate.Overview;
        movie.PosterRemoteUrl = string.IsNullOrWhiteSpace(candidate.PosterRemoteUrl) ? null : candidate.PosterRemoteUrl;
        movie.Country = string.IsNullOrWhiteSpace(candidate.Country) ? null : candidate.Country;
        movie.Language = string.IsNullOrWhiteSpace(candidate.Language) ? null : candidate.Language;
        movie.DirectorText = string.IsNullOrWhiteSpace(candidate.DirectorText) ? null : candidate.DirectorText;
        movie.WriterText = string.IsNullOrWhiteSpace(candidate.WriterText) ? null : candidate.WriterText;
        movie.ActorsText = string.IsNullOrWhiteSpace(candidate.ActorsText) ? null : candidate.ActorsText;
        movie.ProductionCompanyText = string.IsNullOrWhiteSpace(candidate.ProductionCompanyText) ? null : candidate.ProductionCompanyText;
        movie.RuntimeMinutes = candidate.RuntimeMinutes;
        movie.TmdbId = candidate.TmdbId;
        movie.ImdbId = string.IsNullOrWhiteSpace(candidate.ImdbId) ? null : candidate.ImdbId;
        movie.IdentifiedConfidence = confidence;
        movie.IdentificationStatus = status;
        movie.GenresText = string.IsNullOrWhiteSpace(candidate.GenresText) ? null : candidate.GenresText;
        movie.AiTagsText = null;
        movie.EmotionTagsText = null;
        movie.SceneTagsText = null;
        movie.UpdatedAt = DateTime.UtcNow;

        UpsertRating(movie, "TMDB", candidate.TmdbRating, 10d, candidate.TmdbVoteCount, $"https://www.themoviedb.org/movie/{candidate.TmdbId}");

        if (includeOmdbRating && !string.IsNullOrWhiteSpace(candidate.ImdbId))
        {
            try
            {
                var omdbRating = await _omdbService.GetRatingAsync(candidate.ImdbId, cancellationToken);
                if (omdbRating is not null)
                {
                    UpsertRating(
                        movie,
                        omdbRating.SourceName,
                        omdbRating.ScoreValue,
                        omdbRating.ScoreScale,
                        omdbRating.VoteCount,
                        omdbRating.SourceUrl);
                }
            }
            catch (Exception exception)
            {
                result.AddWarning("OMDb", TrimMessage(exception.Message));
            }
        }

        if (movie.Id > 0)
        {
            var staleRatings = await dbContext.RatingSources
                .Where(rating => rating.MovieId == movie.Id)
                .Where(rating => rating.SourceName != "TMDB" && rating.SourceName != "OMDb")
                .ToListAsync(cancellationToken);

            if (staleRatings.Count > 0)
            {
                dbContext.RatingSources.RemoveRange(staleRatings);
            }
        }
    }

    private static void UpsertRating(
        Movie movie,
        string sourceName,
        double? scoreValue,
        double scoreScale,
        int? voteCount,
        string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || !scoreValue.HasValue)
        {
            return;
        }

        var rating = movie.RatingSources.FirstOrDefault(
            x => string.Equals(x.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));

        if (rating is null)
        {
            rating = new RatingSource
            {
                SourceName = sourceName,
                CreatedAt = DateTime.UtcNow
            };
            movie.RatingSources.Add(rating);
        }

        rating.ScoreValue = scoreValue.Value;
        rating.ScoreScale = scoreScale;
        rating.VoteCount = voteCount;
        rating.SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl;
        rating.LastUpdatedAt = DateTime.UtcNow;
    }

    private async Task UpsertFailurePlaceholderAsync(
        AppDbContext dbContext,
        MediaFile mediaFile,
        string title,
        int? releaseYear,
        CancellationToken cancellationToken)
    {
        var currentMovie = mediaFile.MovieId.HasValue
            ? await LoadMovieAggregateAsync(dbContext, mediaFile.MovieId.Value, cancellationToken)
            : null;

        Movie targetMovie;
        if (currentMovie is not null && !currentMovie.TmdbId.HasValue)
        {
            targetMovie = currentMovie;
        }
        else
        {
            targetMovie = new Movie
            {
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Movies.Add(targetMovie);
        }

        targetMovie.Title = title;
        targetMovie.OriginalTitle = null;
        targetMovie.ReleaseYear = releaseYear;
        targetMovie.ReleaseDate = null;
        targetMovie.Overview = null;
        targetMovie.PosterRemoteUrl = null;
        targetMovie.Country = null;
        targetMovie.Language = null;
        targetMovie.RuntimeMinutes = null;
        targetMovie.TmdbId = null;
        targetMovie.ImdbId = null;
        targetMovie.IdentifiedConfidence = null;
        targetMovie.IdentificationStatus = IdentificationStatus.Failed;
        targetMovie.GenresText = null;
        targetMovie.AiTagsText = null;
        targetMovie.EmotionTagsText = null;
        targetMovie.SceneTagsText = null;
        targetMovie.UpdatedAt = DateTime.UtcNow;

        if (targetMovie.RatingSources.Count > 0)
        {
            dbContext.RatingSources.RemoveRange(targetMovie.RatingSources);
            targetMovie.RatingSources.Clear();
        }

        mediaFile.Movie = targetMovie;
        mediaFile.UpdatedAt = DateTime.UtcNow;

        if (!PromoteLocalDefaultSource(targetMovie, [mediaFile])
            && !targetMovie.DefaultMediaFileId.HasValue)
        {
            targetMovie.DefaultMediaFileId = mediaFile.Id;
        }

        if (currentMovie is not null && currentMovie.Id != targetMovie.Id)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await CleanupMovieIfOrphanedAsync(dbContext, currentMovie.Id, cancellationToken);
        }
    }

    private static AutoIdentifyResult BuildAutoIdentifyResult(
        int movieId,
        AutoIdentifyStatus status,
        string message,
        string query = "",
        int? queryYear = null,
        MetadataSearchCandidate? candidate = null,
        int? targetMovieId = null)
    {
        return new AutoIdentifyResult
        {
            MovieId = movieId,
            Status = status,
            TargetMovieId = targetMovieId,
            AppliedTmdbId = candidate?.TmdbId,
            Query = query,
            QueryYear = queryYear,
            AppliedTitle = candidate?.Title ?? string.Empty,
            Message = TrimMessage(message)
        };
    }

    private static void WriteBatch2Event(string message)
    {
        AiPerfDiagnostics.WriteEvent(message);
    }

    private static string FormatStatus(AiSearchSuggestionStatus status)
    {
        return status switch
        {
            AiSearchSuggestionStatus.Success => "success",
            AiSearchSuggestionStatus.NoResult => "no-result",
            AiSearchSuggestionStatus.Failed => "failed",
            _ => status.ToString().ToLowerInvariant()
        };
    }

    private static double CalculateConfidence(
        string expectedTitle,
        int? expectedYear,
        MetadataSearchCandidate candidate)
    {
        var titleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.Title);
        var originalTitleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.OriginalTitle);
        var bestTitleScore = Math.Max(titleSimilarity, originalTitleSimilarity);

        var yearScore = NeutralYearScore;
        if (expectedYear.HasValue && candidate.ReleaseYear.HasValue)
        {
            yearScore = expectedYear.Value == candidate.ReleaseYear.Value
                ? 1d
                : Math.Abs(expectedYear.Value - candidate.ReleaseYear.Value) == 1
                    ? 0.5d
                    : 0d;
        }

        return Math.Clamp((bestTitleScore * 0.8d) + (yearScore * 0.2d), 0d, 1d);
    }

    private static string GetMovieSearchDecision(MetadataSearchCandidate? candidate)
    {
        if (candidate is null)
        {
            return "placeholder-no-result";
        }

        if (candidate.Confidence < MinimumAutoMatchConfidence)
        {
            return "placeholder-low-confidence";
        }

        return candidate.Confidence >= MatchedConfidence
            ? "match"
            : "placeholder-needs-review";
    }

    private static string GetMovieResultStatus(MetadataSearchCandidate? candidate)
    {
        if (candidate is null || candidate.Confidence < MinimumAutoMatchConfidence)
        {
            return "none";
        }

        return candidate.Confidence >= MatchedConfidence
            ? nameof(IdentificationStatus.Matched)
            : nameof(IdentificationStatus.NeedsReview);
    }

    private static string GetMovieAutoApplyBlockedReason(MetadataSearchCandidate? candidate)
    {
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

    private static string GetLowInformationMovieQueryReason(string candidateTitle, int? releaseYear)
    {
        var normalized = NormalizeQueryToken(candidateTitle);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "empty-or-too-short-query";
        }

        if (PureNumberQueryRegex().IsMatch(normalized))
        {
            return "numeric-only-title";
        }

        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return "empty-or-too-short-query";
        }

        var titleTokens = tokens
            .Where(token => !IsLowInformationMovieToken(token, releaseYear))
            .ToArray();
        if (titleTokens.Length == 0)
        {
            return "release-metadata-only-query";
        }

        var hasCjk = titleTokens.Any(token => token.Any(IsCjk));
        var hasMeaningfulLatin = titleTokens.Any(token => token.Count(char.IsLetter) >= 3);
        var hasMeaningfulToken = hasCjk || hasMeaningfulLatin || titleTokens.Any(token => token.Length >= 3 && token.Any(char.IsLetter));
        if (!hasMeaningfulToken)
        {
            return "low-information-title";
        }

        if (normalized.Length <= 2 && !hasCjk)
        {
            return "empty-or-too-short-query";
        }

        return string.Empty;
    }

    private static bool IsLowInformationMovieToken(string token, int? releaseYear)
    {
        var normalized = token.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (releaseYear.HasValue && normalized == releaseYear.Value.ToString(CultureInfo.InvariantCulture))
        {
            return true;
        }

        return LowInformationMovieTokenRegex().IsMatch(normalized);
    }

    private static string NormalizeQueryToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value.Trim())
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

        return System.Text.RegularExpressions.Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static bool IsCjk(char ch)
    {
        return ch is >= '\u4e00' and <= '\u9fff';
    }

    private static async Task<Movie?> LoadMovieAggregateAsync(
        AppDbContext dbContext,
        int movieId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Movies
            .Include(x => x.MediaFiles)
            .ThenInclude(x => x.SourceConnection)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
    }

    private static bool PromoteLocalDefaultSource(Movie movie, IEnumerable<MediaFile> mediaFiles)
    {
        var localSource = mediaFiles
            .Where(IsPlayableLocalVideo)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        if (localSource is null)
        {
            return false;
        }

        if (movie.DefaultMediaFileId != localSource.Id)
        {
            movie.DefaultMediaFileId = localSource.Id;
            movie.UpdatedAt = DateTime.UtcNow;
        }

        return true;
    }

    private static bool IsPlayableLocalVideo(MediaFile mediaFile)
    {
        return mediaFile.MediaType == MediaType.Video
               && !mediaFile.IsDeleted
               && mediaFile.SourceConnection?.ProtocolType == ProtocolType.Local
               && IsExistingLocalFile(mediaFile.FilePath);
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

    private static async Task CleanupMovieIfOrphanedAsync(
        AppDbContext dbContext,
        int movieId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);

        if (movie is null)
        {
            return;
        }

        var isFailedPlaceholder = !movie.TmdbId.HasValue
                                  && movie.IdentificationStatus == IdentificationStatus.Failed;

        if (isFailedPlaceholder
            && movie.MediaFiles.Count == 0
            && movie.WatchHistories.Count == 0
            && !movie.IsFavorite
            && !movie.IsWatched)
        {
            if (movie.RatingSources.Count > 0)
            {
                dbContext.RatingSources.RemoveRange(movie.RatingSources);
            }

            dbContext.Movies.Remove(movie);
        }
    }

    private static async Task ReconcileEpisodeAfterSourceMoveAsync(
        AppDbContext dbContext,
        int episodeId,
        int movedMediaFileId,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.TvEpisodes
            .FirstOrDefaultAsync(x => x.Id == episodeId, cancellationToken);
        if (episode is null || episode.DefaultMediaFileId != movedMediaFileId)
        {
            return;
        }

        episode.DefaultMediaFileId = await dbContext.MediaFiles
            .Where(x => x.EpisodeId == episodeId
                        && x.Id != movedMediaFileId
                        && x.MediaType == MediaType.Video
                        && !x.IsDeleted)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        episode.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task ReconcileMovieAfterSourceMoveAsync(
        AppDbContext dbContext,
        int movieId,
        int movedMediaFileId,
        CancellationToken cancellationToken)
    {
        var trackedMovie = dbContext.ChangeTracker
            .Entries<Movie>()
            .FirstOrDefault(x => x.Entity.Id == movieId);
        if (trackedMovie?.State == EntityState.Deleted)
        {
            return;
        }

        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .ThenInclude(x => x.SourceConnection)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is null || movie.DefaultMediaFileId != movedMediaFileId)
        {
            return;
        }

        movie.DefaultMediaFileId = SelectPreferredDefaultMediaFileId(movie.MediaFiles, movedMediaFileId);
        movie.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task SetMovieDefaultSourceAsync(
        AppDbContext dbContext,
        int movieId,
        int mediaFileId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken)
            ?? throw new InvalidOperationException("目标影片不存在。");
        var belongsToTarget = await dbContext.MediaFiles
            .AnyAsync(
                x => x.Id == mediaFileId
                     && x.MovieId == movieId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken);
        if (!belongsToTarget)
        {
            throw new InvalidOperationException("修正后的播放源未绑定到目标影片。");
        }

        movie.DefaultMediaFileId = mediaFileId;
        movie.UpdatedAt = DateTime.UtcNow;
    }

    private static int? SelectPreferredDefaultMediaFileId(
        IEnumerable<MediaFile> mediaFiles,
        int excludedMediaFileId)
    {
        var candidates = mediaFiles
            .Where(x => x.Id != excludedMediaFileId && x.MediaType == MediaType.Video && !x.IsDeleted)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .Where(IsPlayableLocalVideo)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? candidates
                .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
    }

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
    }

    private static string TruncateRequired(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static DateTime? MaxDate(DateTime? left, DateTime? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return left.Value >= right.Value ? left : right;
    }

    private static int GetYearSortGroup(int expectedYear, int? candidateYear)
    {
        if (!candidateYear.HasValue)
        {
            return 3;
        }

        if (candidateYear.Value == expectedYear)
        {
            return 0;
        }

        if (Math.Abs(candidateYear.Value - expectedYear) == 1)
        {
            return 1;
        }

        return 2;
    }

    private static int GetYearDistance(int expectedYear, int? candidateYear)
    {
        return !candidateYear.HasValue
            ? int.MaxValue
            : Math.Abs(candidateYear.Value - expectedYear);
    }

    [GeneratedRegex(@"^\d{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex PureNumberQueryRegex();

    [GeneratedRegex(@"^(?:\d{1,4}|19\d{2}|20\d{2}|part|pt|disc|disk|cd|sample|trailer|teaser|preview|extras?|bonus|4k|8k|1080p|2160p|720p|480p|uhd|fhd|sd|hq|hdr|hdr10|dv|x264|x265|h264|h265|hevc|av1|bluray|blu|ray|brrip|webrip|webdl|web|dl|hdrip|dvdrip|bdrip|hdtv|remux|aac|ac3|eac3|dts|truehd|atmos|ddp\d?|flac|lpcm|pcm|ma|hd|fg[tm]|10bit|8bit|proper|repack|extended|limited|multi|subs?|subbed|dubbed|dual|audio|japanese|english|chinese|mandarin|cantonese|korean|amzn|nf|dsnp|hmax|itunes|group|team)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LowInformationMovieTokenRegex();

    [GeneratedRegex(@"\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderBracketedContentRegex();

    [GeneratedRegex(@"[._\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderSeparatorRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderWhitespaceRegex();

    [GeneratedRegex(@"^\s*(?<episode>\d{1,4})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderBareNumberRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:E|EP)(?<episode>\d{1,4})(?:$|[\s._\-\]\)])|\bEpisode\s*(?<episode>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderMarkerEpisodeRegex();

    [GeneratedRegex(@"\u7b2c\s*(?<episode>\d{1,4})\s*[\u96c6\u8bdd]", RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderChineseEpisodeRegex();

    [GeneratedRegex(@"^(?<prefix>.*?[\p{L}\u4e00-\u9fff].*?)[\s._-]*(?<episode>\d{1,4})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderTitleNumberRegex();

    [GeneratedRegex(@"(?:^|[\s._\-\[\(])(?:cd|disc|disk|part|sample|trailer|teaser|preview|extras?|bonus|featurette)\s*\d*(?:$|[\s._\-\]\)])|\u82b1\u7d6e|\u9884\u544a|\u7279\u5178|\u5e55\u540e|\u8bbf\u8c08|\u6837\u7247|\u7247\u6bb5|(?:^|[\s._\-\[\(])[\u4e0a\u4e0b](?:$|[\s._\-\]\)])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoviePlaceholderExcludedTokenRegex();

    private sealed record MoviePlaceholderGroupingCandidate(
        int MediaFileId,
        string FileName,
        string FilePath,
        string ParentPath,
        string CandidateTitle,
        string PlaceholderReason);

    private sealed record MoviePlaceholderEpisodePattern(string PatternKey, string Pattern, int Number);

    private sealed record MoviePlaceholderGroupingParsedCandidate(
        MoviePlaceholderGroupingCandidate Placeholder,
        MoviePlaceholderEpisodePattern Pattern);

    private sealed record GroupedTvLikePlaceholderEpisodeCandidate(
        MoviePlaceholderGroupingInput Input,
        int EpisodeNumber);

    private sealed record GroupedTvLikePlaceholderPersistenceResult(
        bool Created,
        string SkippedReason,
        int? TvSeriesId,
        int? TvSeasonId,
        int FileCount,
        int EpisodeCount,
        bool ReusedSeries,
        bool ReusedSeason)
    {
        public static GroupedTvLikePlaceholderPersistenceResult Skipped(string reason)
        {
            return new GroupedTvLikePlaceholderPersistenceResult(false, reason, null, null, 0, 0, false, false);
        }
    }

    private sealed record UnknownSeriesResolution(TvSeries Series, bool Reused);

    private sealed record UnknownSeasonResolution(TvSeason Season, bool Reused);

    private sealed record MoviePlaceholderGroupingPersistenceSummary(
        int CandidateFiles,
        int PersistedRanges,
        int PersistedFiles,
        string SkippedReasons);
}
