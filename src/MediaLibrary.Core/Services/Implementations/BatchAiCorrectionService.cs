using System.Globalization;
using System.Text.Json;
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

public sealed partial class BatchAiCorrectionService : IBatchAiCorrectionService
{
    private const string StatusSuccess = "success";
    private const string StatusSkipped = "skipped";
    private const string StatusFailed = "failed";
    private const string StatusCancelled = "cancelled";
    private const string UnitKindSingleSource = "single-source";
    private const string UnitKindSeason = "season";
    private const double MovieConfidenceThreshold = 0.80d;
    private const double MovieNeutralYearScore = 0.70d;
    private const double MovieUniqueStrongTitleThreshold = 0.86d;
    private const double MovieUniqueStrongTitleMargin = 0.12d;

    private readonly IAiService _aiService;
    private readonly ITmdbService _tmdbService;
    private readonly ISingleSourceCorrectionService _singleSourceCorrectionService;
    private readonly IUnknownSeasonCorrectionService _seasonCorrectionService;
    private readonly SemaphoreSlim _applyGate = new(1, 1);

    public BatchAiCorrectionService(
        IAiService aiService,
        ITmdbService tmdbService,
        ISingleSourceCorrectionService singleSourceCorrectionService,
        IUnknownSeasonCorrectionService seasonCorrectionService)
    {
        _aiService = aiService;
        _tmdbService = tmdbService;
        _singleSourceCorrectionService = singleSourceCorrectionService;
        _seasonCorrectionService = seasonCorrectionService;
    }

    public async Task<BatchAiCorrectionRunResult> CorrectAsync(
        IReadOnlyCollection<BatchAiCorrectionSelectionItem> selections,
        IProgress<BatchAiCorrectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (selections.Count == 0)
        {
            return new BatchAiCorrectionRunResult();
        }

        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-started selectedCount={selections.Count} concurrencyMode=adaptive initialConcurrency=5");

        var expansion = await ExpandSelectionsAsync(selections, cancellationToken);
        var units = expansion.Units;
        var aiExecutor = new AdaptiveAiBatchExecutor("batch-ai-correction", units.Count);
        var results = new List<BatchAiCorrectionUnitResult>();
        var successCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var cancelledCount = 0;
        var processedCount = 0;
        var totalCount = expansion.PrecomputedResults.Count + units.Count;

        foreach (var result in expansion.PrecomputedResults)
        {
            results.Add(result);
            skippedCount++;
            processedCount++;
            ReportProgress(progress, totalCount, processedCount, successCount, skippedCount, failedCount, cancelledCount, result.Title);
        }

        var resultSlots = new BatchAiCorrectionUnitResult?[units.Count];
        var stateGate = new object();
        var processingTasks = units
            .Select((unit, index) => ProcessUnitSlotAsync(unit, index))
            .ToArray();
        await Task.WhenAll(processingTasks);
        results.AddRange(resultSlots.Where(result => result is not null).Select(result => result!));

        async Task ProcessUnitSlotAsync(BatchAiCorrectionUnit unit, int index)
        {
            BatchAiCorrectionUnitResult result;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await ProcessUnitAsync(
                    unit,
                    (requestUnit, token) => aiExecutor.ExecuteAsync(
                        "batch-ai-correction-unit",
                        FormatAiUnitKey(requestUnit, index),
                        (_, requestToken) => RequestAiTargetAsync(requestUnit, requestToken),
                        token),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = BuildResult(unit, StatusCancelled, "cancelled", "Batch AI correction was cancelled.");
                ScanIdentificationDiagnostics.Write(
                    $"event=batch-ai-correction-cancelled unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} cancellationSource=\"user-request\"");
            }
            catch (OperationCanceledException exception)
            {
                var message = "AI request timed out.";
                result = BuildResult(unit, StatusFailed, "failed", message);
                ScanIdentificationDiagnostics.Write(
                    $"event=batch-ai-correction-failed unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} failureReason={ScanIdentificationDiagnostics.FormatValue(message)} errorType={ScanIdentificationDiagnostics.FormatValue(exception.GetType().Name)} cancellationSource=\"request-timeout\"");
            }
            catch (Exception exception)
            {
                var message = DescribeException(exception);
                result = BuildResult(unit, StatusFailed, "failed", message);
                ScanIdentificationDiagnostics.Write(
                    $"event=batch-ai-correction-failed unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} failureReason={ScanIdentificationDiagnostics.FormatValue(message, 260)}");
            }

            lock (stateGate)
            {
                resultSlots[index] = result;
                switch (result.Status)
                {
                    case StatusSuccess:
                        successCount++;
                        break;
                    case StatusSkipped:
                        skippedCount++;
                        break;
                    case StatusCancelled:
                        cancelledCount++;
                        break;
                    default:
                        failedCount++;
                        break;
                }
                processedCount++;
                ReportProgress(progress, totalCount, processedCount, successCount, skippedCount, failedCount, cancelledCount, unit.Title);
            }
        }

        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-summary selectedCount={selections.Count} totalUnits={totalCount} success={successCount} skipped={skippedCount} failed={failedCount} cancelled={cancelledCount}");
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-concurrency-summary totalUnits={units.Count} finalConcurrency={aiExecutor.CurrentConcurrency} aiRequestSuccessCount={aiExecutor.SuccessCount} retryableErrorCount={aiExecutor.RetryableErrorCount} retryScheduledCount={aiExecutor.RetryScheduledCount} retryExhaustedCount={aiExecutor.RetryExhaustedCount} success={successCount} skipped={skippedCount} failed={failedCount} cancelled={cancelledCount}");

        return new BatchAiCorrectionRunResult
        {
            UnitResults = results,
            TotalCount = totalCount,
            SuccessCount = successCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            CancelledCount = cancelledCount
        };
    }

    private async Task<BatchAiCorrectionUnitResult> ProcessUnitAsync(
        BatchAiCorrectionUnit unit,
        Func<BatchAiCorrectionUnit, CancellationToken, Task<BatchAiTarget>> requestAiTargetAsync,
        CancellationToken cancellationToken)
    {
        var aiTarget = await requestAiTargetAsync(unit, cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-ai-result unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} aiTargetKind={ScanIdentificationDiagnostics.FormatValue(aiTarget.TargetKind)} aiMovieTitle={ScanIdentificationDiagnostics.FormatValue(aiTarget.MovieTitle, 160)} aiSeriesTitle={ScanIdentificationDiagnostics.FormatValue(aiTarget.SeriesTitle, 160)} targetSeasonNumber={ScanIdentificationDiagnostics.FormatNullable(aiTarget.SeasonNumber)} targetEpisodeNumber={ScanIdentificationDiagnostics.FormatNullable(aiTarget.EpisodeNumber)} aiReason={ScanIdentificationDiagnostics.FormatValue(aiTarget.Reason, 160)}");

        if (aiTarget.IsSkip)
        {
            return Skip(unit, string.IsNullOrWhiteSpace(aiTarget.Reason) ? "ai-returned-empty" : aiTarget.Reason);
        }

        if (unit.UnitKind == UnitKindSingleSource)
        {
            return await ApplySingleSourceUnitAsync(unit, aiTarget, cancellationToken);
        }

        return await ApplySeasonUnitAsync(unit, aiTarget, cancellationToken);
    }

    private async Task<BatchAiCorrectionUnitResult> ApplySingleSourceUnitAsync(
        BatchAiCorrectionUnit unit,
        BatchAiTarget aiTarget,
        CancellationToken cancellationToken)
    {
        if (!unit.MediaFileId.HasValue)
        {
            return Skip(unit, "single-source-unit-missing-mediafile");
        }

        if (aiTarget.TargetKind == "Movie")
        {
            var movieResolution = await ResolveMovieTargetAsync(aiTarget, cancellationToken);
            if (!movieResolution.TmdbId.HasValue)
            {
                return Skip(unit, movieResolution.SkipReason);
            }

            await RunSerializedApplyAsync(
                () => _singleSourceCorrectionService.ApplyMovieCorrectionAsync(
                    unit.MediaFileId.Value,
                    movieResolution.TmdbId.Value,
                    cancellationToken),
                cancellationToken);

            return Applied(unit, "Movie", $"Applied movie correction tmdbId={movieResolution.TmdbId.Value}.");
        }

        if (aiTarget.TargetKind == "TvEpisode")
        {
            if (!aiTarget.SeasonNumber.HasValue || aiTarget.SeasonNumber.Value < 0)
            {
                return Skip(unit, "tv-episode-target-missing-season-number");
            }

            if (!aiTarget.EpisodeNumber.HasValue || aiTarget.EpisodeNumber.Value <= 0)
            {
                return Skip(unit, "tv-episode-target-missing-episode-number");
            }

            var seriesTmdbId = await ResolveSeriesTmdbIdAsync(aiTarget, cancellationToken);
            if (!seriesTmdbId.HasValue)
            {
                return Skip(unit, "tv-episode-target-series-not-resolved");
            }

            var seasonZeroSkipReason = await ValidateSeasonZeroTargetAsync(
                unit,
                seriesTmdbId.Value,
                aiTarget.SeasonNumber.Value,
                aiTarget.EpisodeNumber,
                "tv-episode",
                cancellationToken);
            if (seasonZeroSkipReason is not null)
            {
                return Skip(unit, seasonZeroSkipReason);
            }

            await RunSerializedApplyAsync(
                () => _singleSourceCorrectionService.ApplyTvEpisodeCorrectionAsync(
                    unit.MediaFileId.Value,
                    seriesTmdbId.Value,
                    aiTarget.SeasonNumber.Value,
                    aiTarget.EpisodeNumber.Value,
                    cancellationToken),
                cancellationToken);

            return Applied(unit, "TvEpisode", $"Applied TV episode correction seriesTmdbId={seriesTmdbId.Value}.");
        }

        return Skip(unit, "single-source-target-kind-not-supported");
    }

    private async Task<BatchAiCorrectionUnitResult> ApplySeasonUnitAsync(
        BatchAiCorrectionUnit unit,
        BatchAiTarget aiTarget,
        CancellationToken cancellationToken)
    {
        if (!unit.SeasonId.HasValue)
        {
            return Skip(unit, "season-unit-missing-season");
        }

        if (aiTarget.TargetKind != "TvSeason")
        {
            return Skip(unit, "season-unit-target-kind-not-supported");
        }

        if (!aiTarget.SeasonNumber.HasValue || aiTarget.SeasonNumber.Value < 0)
        {
            return Skip(unit, "tv-season-target-missing-season-number");
        }

        var seriesTmdbId = await ResolveSeriesTmdbIdAsync(aiTarget, cancellationToken);
        if (!seriesTmdbId.HasValue)
        {
            return Skip(unit, "tv-season-target-series-not-resolved");
        }

        var targetSeasonNumber = aiTarget.SeasonNumber.Value;
        var seasonZeroSkipReason = await ValidateSeasonZeroTargetAsync(
            unit,
            seriesTmdbId.Value,
            targetSeasonNumber,
            episodeNumber: null,
            targetKind: "tv-season",
            cancellationToken);
        if (seasonZeroSkipReason is not null)
        {
            return Skip(unit, seasonZeroSkipReason);
        }

        await RunSerializedApplyAsync(
            () => _seasonCorrectionService.ApplySeasonToRecognizedSeasonAsync(
                unit.SeasonId.Value,
                seriesTmdbId.Value,
                targetSeasonNumber,
                episodeMappings: null,
                cancellationToken),
            cancellationToken);

        return Applied(unit, "TvSeason", $"Applied TV season correction seriesTmdbId={seriesTmdbId.Value}.");
    }

    private async Task<string?> ValidateSeasonZeroTargetAsync(
        BatchAiCorrectionUnit unit,
        int seriesTmdbId,
        int seasonNumber,
        int? episodeNumber,
        string targetKind,
        CancellationToken cancellationToken)
    {
        if (seasonNumber != 0)
        {
            return null;
        }

        var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
            seriesTmdbId,
            seasonNumber,
            cancellationToken: cancellationToken);
        if (seasonDetails is null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=batch-ai-correction-season-zero-target-skipped unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} targetKind={ScanIdentificationDiagnostics.FormatValue(targetKind)} seriesTmdbId={seriesTmdbId} targetSeasonNumber={seasonNumber} targetEpisodeNumber={ScanIdentificationDiagnostics.FormatNullable(episodeNumber)} skippedReason=\"season-zero-tmdb-season-unavailable\"");
            return "season-zero-tmdb-season-unavailable";
        }

        if (episodeNumber.HasValue
            && !seasonDetails.Episodes.Any(x => x.EpisodeNumber == episodeNumber.Value))
        {
            ScanIdentificationDiagnostics.Write(
                $"event=batch-ai-correction-season-zero-target-skipped unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} targetKind={ScanIdentificationDiagnostics.FormatValue(targetKind)} seriesTmdbId={seriesTmdbId} targetSeasonNumber={seasonNumber} targetEpisodeNumber={ScanIdentificationDiagnostics.FormatNullable(episodeNumber)} seasonEpisodeCount={seasonDetails.EpisodeCount} skippedReason=\"season-zero-tmdb-episode-unavailable\"");
            return "season-zero-tmdb-episode-unavailable";
        }

        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-season-zero-target-validated unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} targetKind={ScanIdentificationDiagnostics.FormatValue(targetKind)} seriesTmdbId={seriesTmdbId} targetSeasonNumber={seasonNumber} targetEpisodeNumber={ScanIdentificationDiagnostics.FormatNullable(episodeNumber)} seasonEpisodeCount={seasonDetails.EpisodeCount}");
        return null;
    }

    private async Task<T> RunSerializedApplyAsync<T>(
        Func<Task<T>> applyAsync,
        CancellationToken cancellationToken)
    {
        await _applyGate.WaitAsync(cancellationToken);
        try
        {
            return await applyAsync();
        }
        finally
        {
            _applyGate.Release();
        }
    }

    private async Task<BatchAiTarget> RequestAiTargetAsync(
        BatchAiCorrectionUnit unit,
        CancellationToken cancellationToken)
    {
        var allowedTargets = unit.UnitKind == UnitKindSeason
            ? "TvSeason, Skip"
            : "Movie, TvEpisode, Skip";
        var text = await _aiService.GenerateTextAsync(
            """
            You are a cautious batch media correction assistant.
            Return JSON only. Do not explain.
            targetKind must be one of Movie, TvEpisode, TvSeason, Skip.
            Choose only from allowedTargetKinds in the user context. For unitKind=season, only TvSeason or Skip is allowed; never return Movie or TvEpisode for a Season unit.
            Prioritize source path hints and file names. Treat current titles and current season or episode numbers only as weak hints because they may be wrong.
            Use source path hints and file names to identify the work and the episode/season evidence, but never use the language or script of a file name to decide the TMDB original_title/original_name language or spelling.
            English, localized, or romanized file names can still belong to a non-English TMDB original title/name.
            If sourceFileName contains a specific work title or numbered part, prefer that specific source title over collection/franchise/pack/folder names.
            Titles used for lookup must match TMDB original_title/original_name semantics: the work's official original title/name stored by TMDB, not a translated/localized/marketing alias.
            For TV targets, seriesTitle must be the exact TMDB original_name. If the original language title is Japanese, Korean, Chinese, Spanish, French, German, or another non-English title, return that original spelling/script, not the English/international title.
            Romanized/transliterated TV names are aliases unless TMDB original_name itself is romanized. If a romanized alias confidently identifies the native-script official original_name, return the native-script original_name; otherwise return Skip.
            Return an English TV seriesTitle only when TMDB original_name itself is English. If you only know an English/international/localized alias for a non-English-original series, return Skip instead of guessing.
            Never return TMDB ids. The app will search TMDB locally from the returned titles.
            Final-season wording such as 最终季, 完结篇, final season, or the final season is a season-number clue. Use it to return the correct TMDB seasonNumber only when you are confident; otherwise return Skip instead of guessing.
            If a single-source filename contains explicit SxxEyy or another clear ordinary TV episode pattern, prefer TvEpisode over Movie unless it is clearly a standalone theatrical/TV movie without ordinary episode mapping.
            For Season units, return TvSeason only when the sampled source rows represent one target TV season.
            Do not assume Part 1, Part 2, cour, half-season, final part, or release-part wording means multiple TMDB seasons. Many releases split one TMDB season into parts.
            For Season units, use sampled episode numbers, source row distribution, and common TMDB season structure. If all sampled rows form one ordinary continuous range that safely belongs to one TMDB season, return that season even when the release title mentions Part 1 or Part 2.
            Return Skip for a Season unit only when sampled rows clearly mix different works or episode ranges that cannot be safely reduced to one supported target season.
            Do not let current/old titles or nearby sibling special folders override the sampled source rows.
            Special/SP/OVA/OAD/special episode/theatrical wording is not an automatic skip. First decide whether the item has a safe supported target: Movie, TvEpisode with complete series/season/episode, or TvSeason when sampled rows represent one season. Return that target when safe; return Skip only when it cannot be safely represented as Movie, TvEpisode, or TvSeason.
            For Movie, return TMDB original_title-style title plus optional year.
            For TvEpisode, return TMDB original_name-style seriesTitle, seasonNumber, and episodeNumber. Season 0 is valid for TMDB specials; missing season or episode means Skip.
            For TvSeason, return TMDB original_name-style seriesTitle and seasonNumber. Season 0 is valid for TMDB specials when the sampled rows represent one special season. Missing season means Skip.
            Never invent missing season or episode numbers for special content; if no safe Movie/TvEpisode/TvSeason mapping exists, return Skip.
            """,
            $"""
            allowedTargetKinds={allowedTargets}
            unitKind={unit.UnitKind}
            title={unit.Title}
            currentMovieTitle={unit.Source?.CurrentMovieTitle}
            currentSeriesTitle={unit.Source?.CurrentSeriesTitle ?? unit.Season?.SeriesTitle}
            currentSeasonTitle={unit.Season?.SeasonTitle}
            currentSeasonNumber={unit.Source?.CurrentSeasonNumber ?? unit.Season?.SeasonNumber}
            currentEpisodeNumber={unit.Source?.CurrentEpisodeNumber}
            sourceFileName={unit.Source?.FileName}
            sourcePathHint={AiSourceContextFormatter.BuildPathHint(unit.Source?.FilePath, unit.Source?.RemoteUri)}
            sampleSources={string.Join(" | ", unit.Season?.SampleSources.Select(x => x.DisplayText) ?? [])}
            Return JSON keys: targetKind, title, year, seriesTitle, seasonNumber, episodeNumber, reason.
            """,
            AiRequestOptions.BatchCorrectionPro,
            cancellationToken);

        return ParseAiTarget(text);
    }

    private async Task<MovieTargetResolution> ResolveMovieTargetAsync(
        BatchAiTarget target,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.MovieTitle))
        {
            return MovieTargetResolution.Skip("movie-target-not-resolved");
        }

        var candidates = await _tmdbService.SearchMoviesAsync(target.MovieTitle, target.Year, cancellationToken);
        var scoredCandidates = candidates
            .Select(candidate => ScoreMovieCandidate(target.MovieTitle, target.Year, candidate))
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.TitleScore)
            .ToList();
        var bestScoredCandidate = scoredCandidates.FirstOrDefault();
        var secondTitleScore = scoredCandidates
            .Skip(1)
            .Select(candidate => candidate.TitleScore)
            .DefaultIfEmpty(0d)
            .Max();
        var titleMargin = bestScoredCandidate is null ? 0d : bestScoredCandidate.TitleScore - secondTitleScore;
        var hasYearConflict = bestScoredCandidate?.HasYearConflict == true;
        var uniqueStrongMatch = bestScoredCandidate is not null
                                && !hasYearConflict
                                && bestScoredCandidate.TitleScore >= MovieUniqueStrongTitleThreshold
                                && titleMargin >= MovieUniqueStrongTitleMargin;
        var singleExactYearResult = bestScoredCandidate is not null
                                    && candidates.Count == 1
                                    && !hasYearConflict
                                    && target.Year.HasValue
                                    && bestScoredCandidate.Candidate.ReleaseYear.HasValue
                                    && target.Year.Value == bestScoredCandidate.Candidate.ReleaseYear.Value;
        if (bestScoredCandidate is null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=batch-ai-correction-movie-title-rejected aiMovieTitle={ScanIdentificationDiagnostics.FormatValue(target.MovieTitle, 160)} aiYear={ScanIdentificationDiagnostics.FormatNullable(target.Year)} resultCount={candidates.Count} skippedReason=\"movie-target-not-resolved\"");
            return MovieTargetResolution.Skip("movie-target-not-resolved");
        }

        if (hasYearConflict)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=batch-ai-correction-movie-title-rejected aiMovieTitle={ScanIdentificationDiagnostics.FormatValue(target.MovieTitle, 160)} aiYear={ScanIdentificationDiagnostics.FormatNullable(target.Year)} resultCount={candidates.Count} topTitle={ScanIdentificationDiagnostics.FormatValue(bestScoredCandidate.Candidate.Title, 160)} topOriginalTitle={ScanIdentificationDiagnostics.FormatValue(bestScoredCandidate.Candidate.OriginalTitle, 160)} tmdbYear={ScanIdentificationDiagnostics.FormatNullable(bestScoredCandidate.Candidate.ReleaseYear)} titleScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.TitleScore)} yearScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.YearScore)} confidence={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.Confidence)} top1TitleScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.TitleScore)} top2TitleScore={ScanIdentificationDiagnostics.FormatConfidence(secondTitleScore)} titleMargin={ScanIdentificationDiagnostics.FormatConfidence(titleMargin)} uniqueStrongMatch=false singleExactYearResult=false skippedReason=\"movie-year-conflict\"");
            return MovieTargetResolution.Skip("movie-year-conflict");
        }

        var passesConfidence = bestScoredCandidate.Confidence >= MovieConfidenceThreshold;
        if (!passesConfidence && !uniqueStrongMatch && !singleExactYearResult)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=batch-ai-correction-movie-title-rejected aiMovieTitle={ScanIdentificationDiagnostics.FormatValue(target.MovieTitle, 160)} aiYear={ScanIdentificationDiagnostics.FormatNullable(target.Year)} resultCount={candidates.Count} topTitle={ScanIdentificationDiagnostics.FormatValue(bestScoredCandidate.Candidate.Title, 160)} topOriginalTitle={ScanIdentificationDiagnostics.FormatValue(bestScoredCandidate.Candidate.OriginalTitle, 160)} tmdbYear={ScanIdentificationDiagnostics.FormatNullable(bestScoredCandidate.Candidate.ReleaseYear)} titleScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.TitleScore)} yearScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.YearScore)} confidence={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.Confidence)} top1TitleScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.TitleScore)} top2TitleScore={ScanIdentificationDiagnostics.FormatConfidence(secondTitleScore)} titleMargin={ScanIdentificationDiagnostics.FormatConfidence(titleMargin)} uniqueStrongMatch=false singleExactYearResult=false skippedReason=\"movie-title-not-confident\"");
            return MovieTargetResolution.Skip("movie-title-not-confident");
        }

        var resolution = singleExactYearResult && !passesConfidence && !uniqueStrongMatch
            ? "movie-single-exact-year-result-applied"
            : uniqueStrongMatch && !passesConfidence
                ? "movie-unique-strong-match-applied"
                : "movie-confidence-threshold";
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-movie-title-resolved aiMovieTitle={ScanIdentificationDiagnostics.FormatValue(target.MovieTitle, 160)} aiYear={ScanIdentificationDiagnostics.FormatNullable(target.Year)} resultCount={candidates.Count} topTitle={ScanIdentificationDiagnostics.FormatValue(bestScoredCandidate.Candidate.Title, 160)} topOriginalTitle={ScanIdentificationDiagnostics.FormatValue(bestScoredCandidate.Candidate.OriginalTitle, 160)} tmdbYear={ScanIdentificationDiagnostics.FormatNullable(bestScoredCandidate.Candidate.ReleaseYear)} titleScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.TitleScore)} yearScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.YearScore)} confidence={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.Confidence)} top1TitleScore={ScanIdentificationDiagnostics.FormatConfidence(bestScoredCandidate.TitleScore)} top2TitleScore={ScanIdentificationDiagnostics.FormatConfidence(secondTitleScore)} titleMargin={ScanIdentificationDiagnostics.FormatConfidence(titleMargin)} uniqueStrongMatch={uniqueStrongMatch.ToString().ToLowerInvariant()} singleExactYearResult={singleExactYearResult.ToString().ToLowerInvariant()} resolution={ScanIdentificationDiagnostics.FormatValue(resolution)}");
        return MovieTargetResolution.Resolved(bestScoredCandidate.Candidate.TmdbId);
    }

    private static MovieCandidateScore ScoreMovieCandidate(
        string expectedTitle,
        int? expectedYear,
        MetadataSearchCandidate candidate)
    {
        var titleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.Title);
        var originalTitleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.OriginalTitle);
        var bestTitleScore = Math.Max(titleSimilarity, originalTitleSimilarity);

        var hasYearConflict = false;
        var yearScore = MovieNeutralYearScore;
        if (expectedYear.HasValue && candidate.ReleaseYear.HasValue)
        {
            var yearDelta = Math.Abs(expectedYear.Value - candidate.ReleaseYear.Value);
            yearScore = yearDelta == 0
                ? 1d
                : yearDelta == 1
                    ? 0.5d
                    : 0d;
            hasYearConflict = yearDelta > 1;
        }

        candidate.Confidence = Math.Clamp((bestTitleScore * 0.8d) + (yearScore * 0.2d), 0d, 1d);
        return new MovieCandidateScore(candidate, bestTitleScore, yearScore, candidate.Confidence, hasYearConflict);
    }

    private async Task<int?> ResolveSeriesTmdbIdAsync(
        BatchAiTarget target,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.SeriesTitle))
        {
            return null;
        }

        var candidates = await _tmdbService.SearchTvSeriesAsync(target.SeriesTitle, 1, "zh-CN", cancellationToken);
        var topCandidate = candidates.Results.FirstOrDefault();
        if (topCandidate is null)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=batch-ai-correction-series-title-rejected aiSeriesTitle={ScanIdentificationDiagnostics.FormatValue(target.SeriesTitle, 160)} resultCount=0 skippedReason=\"series-title-no-tmdb-result\"");
            return null;
        }

        var originalNameMatched = string.Equals(
            target.SeriesTitle.Trim(),
            topCandidate.OriginalName?.Trim(),
            StringComparison.OrdinalIgnoreCase);
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-series-title-resolved aiSeriesTitle={ScanIdentificationDiagnostics.FormatValue(target.SeriesTitle, 160)} topTitle={ScanIdentificationDiagnostics.FormatValue(topCandidate.Name, 160)} topOriginalName={ScanIdentificationDiagnostics.FormatValue(topCandidate.OriginalName, 160)} resultCount={candidates.Results.Count} originalNameMatched={originalNameMatched.ToString().ToLowerInvariant()} resolution=\"top-search-result\"");
        return topCandidate.TmdbId;
    }

    private async Task<ExpansionResult> ExpandSelectionsAsync(
        IReadOnlyCollection<BatchAiCorrectionSelectionItem> selections,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var units = new List<BatchAiCorrectionUnit>();
        var results = new List<BatchAiCorrectionUnitResult>();
        var seenMediaFileIds = new HashSet<int>();

        foreach (var selection in selections)
        {
            if (selection.ItemKind == LibraryMediaItemKind.Series
                || (selection.SeriesId > 0
                    && selection.SeasonId <= 0
                    && selection.MovieId <= 0
                    && selection.OrphanMediaFileId <= 0
                    && selection.GroupedRangeMediaFileIds.Count == 0))
            {
                results.Add(BuildSkippedSelection(selection, "series-unit-not-supported"));
                continue;
            }

            if (selection.SeasonId > 0)
            {
                var season = await LoadSeasonContextAsync(dbContext, selection.SeasonId, cancellationToken);
                if (season is null)
                {
                    results.Add(BuildSkippedSelection(selection, "season-not-found"));
                    continue;
                }

                if (season.ActiveSourceCount == 0)
                {
                    results.Add(BuildSkippedSelection(selection, "no-source-season-not-supported"));
                    continue;
                }

                units.Add(new BatchAiCorrectionUnit(selection.SelectionKey, selection.Title, UnitKindSeason, null, season));
                LogUnitCreated(selection.SelectionKey, UnitKindSeason, null, season.Id);
                continue;
            }

            if (selection.OrphanMediaFileId > 0)
            {
                await AddSourceUnitsAsync(
                    dbContext,
                    units,
                    results,
                    seenMediaFileIds,
                    selection,
                    [selection.OrphanMediaFileId],
                    "orphan-source-not-found",
                    cancellationToken);
                continue;
            }

            if (selection.GroupedRangeMediaFileIds.Count > 0)
            {
                await AddSourceUnitsAsync(
                    dbContext,
                    units,
                    results,
                    seenMediaFileIds,
                    selection,
                    selection.GroupedRangeMediaFileIds,
                    "grouped-range-has-no-active-source",
                    cancellationToken);
                continue;
            }

            if (selection.MovieId > 0)
            {
                if (!selection.HasActiveSource)
                {
                    results.Add(BuildSkippedSelection(selection, "no-source-movie-not-supported"));
                    continue;
                }

                var mediaFileIds = await dbContext.MediaFiles
                    .AsNoTracking()
                    .Where(x => x.MovieId == selection.MovieId
                                && x.MediaType == MediaType.Video
                                && !x.IsDeleted)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
                await AddSourceUnitsAsync(
                    dbContext,
                    units,
                    results,
                    seenMediaFileIds,
                    selection,
                    mediaFileIds,
                    "movie-has-no-active-source",
                    cancellationToken);
                continue;
            }

            results.Add(BuildSkippedSelection(selection, "selected-unit-not-supported"));
        }

        return new ExpansionResult(units, results);
    }

    private static async Task AddSourceUnitsAsync(
        AppDbContext dbContext,
        ICollection<BatchAiCorrectionUnit> units,
        ICollection<BatchAiCorrectionUnitResult> results,
        ISet<int> seenMediaFileIds,
        BatchAiCorrectionSelectionItem selection,
        IReadOnlyCollection<int> mediaFileIds,
        string emptyReason,
        CancellationToken cancellationToken)
    {
        if (mediaFileIds.Count == 0)
        {
            results.Add(BuildSkippedSelection(selection, emptyReason));
            return;
        }

        var sources = await LoadSourceContextsAsync(dbContext, mediaFileIds, cancellationToken);
        if (sources.Count == 0)
        {
            results.Add(BuildSkippedSelection(selection, emptyReason));
            return;
        }

        foreach (var source in sources)
        {
            if (!seenMediaFileIds.Add(source.Id))
            {
                continue;
            }

            var title = FirstNonEmpty(selection.Title, source.CurrentMovieTitle, source.CurrentSeriesTitle, source.FileName);
            units.Add(new BatchAiCorrectionUnit(selection.SelectionKey, title, UnitKindSingleSource, source, null));
            LogUnitCreated(selection.SelectionKey, UnitKindSingleSource, source.Id, null);
        }
    }

    private static async Task<IReadOnlyList<SourceUnitContext>> LoadSourceContextsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => mediaFileIds.Contains(x.Id)
                        && x.MediaType == MediaType.Video
                        && !x.IsDeleted)
            .OrderBy(x => x.Id)
            .Select(x => new SourceUnitContext
            {
                Id = x.Id,
                FileName = x.FileName,
                FilePath = x.FilePath,
                RemoteUri = x.RemoteUri,
                CurrentMovieTitle = x.Movie != null ? x.Movie.Title : string.Empty,
                CurrentSeriesTitle = x.Episode != null
                                     && x.Episode.Season != null
                                     && x.Episode.Season.Series != null
                    ? x.Episode.Season.Series.Name
                    : string.Empty,
                CurrentSeasonTitle = x.Episode != null && x.Episode.Season != null
                    ? x.Episode.Season.Name
                    : string.Empty,
                CurrentSeasonNumber = x.Episode != null && x.Episode.Season != null
                    ? x.Episode.Season.SeasonNumber
                    : null,
                CurrentEpisodeNumber = x.Episode != null
                    ? x.Episode.EpisodeNumber
                    : null
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<SeasonUnitContext?> LoadSeasonContextAsync(
        AppDbContext dbContext,
        int seasonId,
        CancellationToken cancellationToken)
    {
        var season = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => x.Id == seasonId)
            .Select(x => new SeasonUnitContext
            {
                Id = x.Id,
                SeriesTitle = x.Series != null ? x.Series.Name : string.Empty,
                SeasonTitle = x.Name,
                SeasonNumber = x.SeasonNumber,
                IsRecognized = x.Series != null && x.Series.TmdbSeriesId.HasValue && x.TmdbSeasonId.HasValue,
                ActiveSourceCount = x.Episodes
                    .SelectMany(episode => episode.MediaFiles)
                    .Count(mediaFile => mediaFile.MediaType == MediaType.Video && !mediaFile.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (season is null)
        {
            return null;
        }

        var sourceSamples = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(x => x.Episode != null
                        && x.Episode.TvSeasonId == seasonId
                        && x.MediaType == MediaType.Video
                        && !x.IsDeleted)
            .OrderBy(x => x.Episode!.EpisodeNumber)
            .ThenBy(x => x.Id)
            .Select(x => new SeasonSourceSampleContext
            {
                EpisodeNumber = x.Episode!.EpisodeNumber,
                FileName = x.FileName,
                FilePath = x.FilePath,
                RemoteUri = x.RemoteUri
            })
            .ToListAsync(cancellationToken);
        season.SampleSources = SelectEvenlyDistributedSamples(sourceSamples, 18)
            .Select(x => x with
            {
                DisplayText = AiSourceContextFormatter.BuildSourceLine(
                    x.EpisodeNumber,
                    x.FileName,
                    x.FilePath,
                    x.RemoteUri)
            })
            .ToList();
        return season;
    }

    private static IReadOnlyList<SeasonSourceSampleContext> SelectEvenlyDistributedSamples(
        IReadOnlyList<SeasonSourceSampleContext> sources,
        int maxCount)
    {
        if (sources.Count <= maxCount)
        {
            return sources;
        }

        var selectedIndexes = new SortedSet<int>();
        for (var index = 0; index < maxCount; index++)
        {
            var sourceIndex = (int)Math.Round(index * (sources.Count - 1) / (double)(maxCount - 1), MidpointRounding.AwayFromZero);
            selectedIndexes.Add(Math.Clamp(sourceIndex, 0, sources.Count - 1));
        }

        return selectedIndexes.Select(index => sources[index]).ToList();
    }

    private static BatchAiTarget ParseAiTarget(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BatchAiTarget.Skip("ai-returned-empty");
        }

        try
        {
            var jsonText = text.Trim();
            var start = jsonText.IndexOf('{');
            var end = jsonText.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                jsonText = jsonText[start..(end + 1)];
            }

            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            var kind = NormalizeTargetKind(ReadString(root, "targetKind") ?? ReadString(root, "kind") ?? ReadString(root, "type"));
            if (kind is "Skip" or "Unsupported" or "")
            {
                return BatchAiTarget.Skip(ReadString(root, "reason") ?? "ai-returned-skip");
            }

            var title = ReadString(root, "title");
            return new BatchAiTarget
            {
                TargetKind = kind,
                MovieTitle = ReadString(root, "movieTitle") ?? (kind == "Movie" ? title : null),
                SeriesTitle = ReadString(root, "seriesTitle") ?? ReadString(root, "tvTitle") ?? (kind is "TvEpisode" or "TvSeason" ? title : null),
                Year = ReadNullableInt(root, "year") ?? ReadNullableInt(root, "releaseYear"),
                SeasonNumber = ReadNullableInt(root, "seasonNumber") ?? ReadNullableInt(root, "season"),
                EpisodeNumber = ReadNullableInt(root, "episodeNumber") ?? ReadNullableInt(root, "episode"),
                Reason = ReadString(root, "reason")
            };
        }
        catch
        {
            return BatchAiTarget.Skip("ai-result-json-invalid");
        }
    }

    private static string NormalizeTargetKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.ToLowerInvariant() switch
        {
            "movie" => "Movie",
            "film" => "Movie",
            "tvepisode" => "TvEpisode",
            "episode" => "TvEpisode",
            "tvseason" => "TvSeason",
            "season" => "TvSeason",
            "unsupported" => "Unsupported",
            "skip" => "Skip",
            "empty" => "Skip",
            "none" => "Skip",
            _ => string.Empty
        };
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;
    }

    private static int? ReadNullableInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static BatchAiCorrectionUnitResult Applied(BatchAiCorrectionUnit unit, string targetKind, string message)
    {
        var result = BuildResult(unit, StatusSuccess, targetKind, message);
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-applied unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} aiTargetKind={ScanIdentificationDiagnostics.FormatValue(targetKind)}");
        return result;
    }

    private static BatchAiCorrectionUnitResult Skip(BatchAiCorrectionUnit unit, string reason)
    {
        var result = BuildResult(unit, StatusSkipped, "Skip", reason);
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-skipped unitKind={ScanIdentificationDiagnostics.FormatValue(unit.UnitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(unit.MediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(unit.SeasonId)} skippedReason={ScanIdentificationDiagnostics.FormatValue(reason)}");
        return result;
    }

    private static BatchAiCorrectionUnitResult BuildSkippedSelection(
        BatchAiCorrectionSelectionItem selection,
        string reason)
    {
        var result = new BatchAiCorrectionUnitResult
        {
            SelectionKey = selection.SelectionKey,
            Title = selection.Title,
            UnitKind = selection.ItemKind.ToString(),
            TargetKind = "Skip",
            Status = StatusSkipped,
            Message = reason
        };
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-skipped selectedItemKind={ScanIdentificationDiagnostics.FormatValue(selection.ItemKind.ToString())} movieId={selection.MovieId} seriesId={selection.SeriesId} seasonId={selection.SeasonId} skippedReason={ScanIdentificationDiagnostics.FormatValue(reason)}");
        return result;
    }

    private static BatchAiCorrectionUnitResult BuildResult(
        BatchAiCorrectionUnit unit,
        string status,
        string targetKind,
        string message)
    {
        return new BatchAiCorrectionUnitResult
        {
            SelectionKey = unit.SelectionKey,
            Title = unit.Title,
            UnitKind = unit.UnitKind,
            TargetKind = targetKind,
            Status = status,
            Message = message,
            MediaFileId = unit.MediaFileId,
            SeasonId = unit.SeasonId
        };
    }

    private static void ReportProgress(
        IProgress<BatchAiCorrectionProgress>? progress,
        int total,
        int processed,
        int success,
        int skipped,
        int failed,
        int cancelled,
        string currentTitle)
    {
        progress?.Report(new BatchAiCorrectionProgress
        {
            TotalCount = total,
            ProcessedCount = processed,
            SuccessCount = success,
            SkippedCount = skipped,
            FailedCount = failed,
            CancelledCount = cancelled,
            CurrentTitle = currentTitle
        });
    }

    private static void LogUnitCreated(string selectionKey, string unitKind, int? mediaFileId, int? seasonId)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=batch-ai-correction-unit-created selectionKeyKind={ScanIdentificationDiagnostics.FormatValue(FormatSelectionKeyKind(selectionKey))} unitKind={ScanIdentificationDiagnostics.FormatValue(unitKind)} mediaFileId={ScanIdentificationDiagnostics.FormatNullable(mediaFileId)} seasonId={ScanIdentificationDiagnostics.FormatNullable(seasonId)}");
    }

    private static string FormatAiUnitKey(BatchAiCorrectionUnit unit, int index)
    {
        if (unit.MediaFileId.HasValue)
        {
            return $"mediaFile:{unit.MediaFileId.Value}";
        }

        if (unit.SeasonId.HasValue)
        {
            return $"season:{unit.SeasonId.Value}";
        }

        return $"unit:{index}";
    }

    private static string FormatSelectionKeyKind(string selectionKey)
    {
        var index = selectionKey.IndexOf(':', StringComparison.Ordinal);
        return index > 0 ? selectionKey[..index] : "unknown";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string DescribeException(Exception exception)
    {
        return exception.InnerException?.Message ?? exception.Message;
    }

    private sealed record ExpansionResult(
        IReadOnlyList<BatchAiCorrectionUnit> Units,
        IReadOnlyList<BatchAiCorrectionUnitResult> PrecomputedResults);

    private sealed record BatchAiCorrectionUnit(
        string SelectionKey,
        string Title,
        string UnitKind,
        SourceUnitContext? Source,
        SeasonUnitContext? Season)
    {
        public int? MediaFileId => Source?.Id;

        public int? SeasonId => Season?.Id;
    }

    private sealed class SourceUnitContext
    {
        public int Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string? RemoteUri { get; set; }

        public string CurrentMovieTitle { get; set; } = string.Empty;

        public string CurrentSeriesTitle { get; set; } = string.Empty;

        public string CurrentSeasonTitle { get; set; } = string.Empty;

        public int? CurrentSeasonNumber { get; set; }

        public int? CurrentEpisodeNumber { get; set; }
    }

    private sealed record SeasonSourceSampleContext
    {
        public int? EpisodeNumber { get; init; }

        public string FileName { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;

        public string? RemoteUri { get; init; }

        public string DisplayText { get; init; } = string.Empty;
    }

    private sealed class SeasonUnitContext
    {
        public int Id { get; set; }

        public string SeriesTitle { get; set; } = string.Empty;

        public string SeasonTitle { get; set; } = string.Empty;

        public int SeasonNumber { get; set; }

        public bool IsRecognized { get; set; }

        public int ActiveSourceCount { get; set; }

        public IReadOnlyList<SeasonSourceSampleContext> SampleSources { get; set; } = [];
    }

    private sealed record MovieTargetResolution(int? TmdbId, string SkipReason)
    {
        public static MovieTargetResolution Resolved(int tmdbId)
        {
            return new MovieTargetResolution(tmdbId, string.Empty);
        }

        public static MovieTargetResolution Skip(string reason)
        {
            return new MovieTargetResolution(null, reason);
        }
    }

    private sealed record MovieCandidateScore(
        MetadataSearchCandidate Candidate,
        double TitleScore,
        double YearScore,
        double Confidence,
        bool HasYearConflict);

    private sealed class BatchAiTarget
    {
        public string TargetKind { get; set; } = "Skip";

        public string? MovieTitle { get; set; }

        public string? SeriesTitle { get; set; }

        public int? Year { get; set; }

        public int? SeasonNumber { get; set; }

        public int? EpisodeNumber { get; set; }

        public string? Reason { get; set; }

        public bool IsSkip => TargetKind is "Skip" or "Unsupported" || string.IsNullOrWhiteSpace(TargetKind);

        public static BatchAiTarget Skip(string reason)
        {
            return new BatchAiTarget
            {
                TargetKind = "Skip",
                Reason = reason
            };
        }
    }
}
