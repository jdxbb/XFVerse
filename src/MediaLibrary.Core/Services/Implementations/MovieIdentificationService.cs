using System.Diagnostics;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class MovieIdentificationService : IMovieIdentificationService
{
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
        var result = new IdentificationRunResult();
        var distinctIds = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return result;
        }

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        var hasTmdbCredential = !string.IsNullOrWhiteSpace(settings.TmdbReadAccessToken)
                                || !string.IsNullOrWhiteSpace(settings.TmdbApiKey);

        foreach (var mediaFileId in distinctIds)
        {
            result.AttemptedCount++;
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var mediaFile = await dbContext.MediaFiles
                .Include(x => x.Movie)
                .ThenInclude(x => x!.RatingSources)
                .FirstOrDefaultAsync(
                    x => x.Id == mediaFileId
                         && x.MediaType == MediaType.Video
                         && !x.IsDeleted,
                    cancellationToken);

            if (mediaFile is null)
            {
                continue;
            }

            if (mediaFile.MovieId.HasValue
                && mediaFile.Movie is not null
                && mediaFile.Movie.TmdbId.HasValue
                && mediaFile.Movie.IdentificationStatus is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed)
            {
                continue;
            }

            var parsedName = MovieFileNameParser.Parse(mediaFile.FileName);
            var candidateTitle = string.IsNullOrWhiteSpace(parsedName.CleanTitle)
                ? Path.GetFileNameWithoutExtension(mediaFile.FileName)
                : parsedName.CleanTitle;

            if (!hasTmdbCredential)
            {
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, candidateTitle, parsedName.ReleaseYear, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                result.AddWarning("TMDB.Auth", "TMDB 认证未配置，资源已保留为识别失败，可后续重试。");
                continue;
            }

            List<MetadataSearchCandidate> searchResults;
            try
            {
                searchResults = (await _tmdbService.SearchMoviesAsync(candidateTitle, parsedName.ReleaseYear, cancellationToken)).ToList();
            }
            catch (Exception exception)
            {
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, candidateTitle, parsedName.ReleaseYear, cancellationToken);
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

            if (bestCandidate is null || bestCandidate.Confidence < 0.55d)
            {
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, candidateTitle, parsedName.ReleaseYear, cancellationToken);
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
                    result.AddWarning("TMDB.Detail", $"TMDB 详情为空：{bestCandidate.Title}，已使用搜索结果继续绑定。");
                }
            }
            catch (Exception exception)
            {
                result.AddWarning(
                    "TMDB.Detail",
                    $"{bestCandidate.Title} 详情读取失败，已退回搜索结果继续绑定：{TrimMessage(exception.Message)}");
            }

            var status = effectiveCandidate.Confidence >= 0.80d
                ? IdentificationStatus.Matched
                : IdentificationStatus.NeedsReview;

            try
            {
                await ApplyCandidateAsync(dbContext, mediaFile, effectiveCandidate, status, result, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                result.BoundCount++;
            }
            catch (Exception exception)
            {
                await UpsertFailurePlaceholderAsync(dbContext, mediaFile, candidateTitle, parsedName.ReleaseYear, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                result.PlaceholderCount++;
                result.AddError("Identify.Apply", TrimMessage(exception.Message));
            }
        }

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

    private async Task<int> ApplyManualMatchCoreAsync(
        AppDbContext dbContext,
        Movie currentMovie,
        MetadataSearchCandidate details,
        CancellationToken cancellationToken,
        bool includeOmdbRating = true)
    {
        var currentMovieHadTmdb = currentMovie.TmdbId is > 0;
        var existingMatchedMovie = await dbContext.Movies
            .Include(x => x.MediaFiles)
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
        var oldTargetWatched = targetMovie.IsWatched;
        var oldTargetFavorite = targetMovie.IsFavorite;
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
            targetMovie.IsFavorite |= currentMovie.IsFavorite;
            targetMovie.IsWatched |= currentMovie.IsWatched;
            targetMovie.UserRating ??= currentMovie.UserRating;
            targetMovie.LastPlayedAt = MaxDate(targetMovie.LastPlayedAt, currentMovie.LastPlayedAt);
            targetMovie.AutoWatchedBaselineAtUtc = MaxDate(targetMovie.AutoWatchedBaselineAtUtc, currentMovie.AutoWatchedBaselineAtUtc);
            if (!currentMovieHadTmdb)
            {
                RecordMovieStateChangeOnIdentification(dbContext, targetMovie, oldTargetWatched, oldTargetFavorite);
            }

            currentMovie.IsFavorite = false;
            currentMovie.IsWatched = false;
            currentMovie.UserRating = null;
            currentMovie.LastPlayedAt = null;
            currentMovie.AutoWatchedBaselineAtUtc = null;

            var currentMediaFiles = await dbContext.MediaFiles
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
                var previousTmdbId = collectionItem.TmdbId;
                collectionItem.MovieId = targetMovie.Id;
                collectionItem.IsInLibrary = targetMovie.MediaFiles.Any(x => x.MediaType == MediaType.Video)
                                             || currentMediaFiles.Any(x => x.MediaType == MediaType.Video);
                ApplyIdentificationSnapshot(collectionItem, targetMovie);
                collectionItem.UpdatedAt = DateTime.UtcNow;
                if (!currentMovieHadTmdb)
                {
                    RecordCollectionStateActivationsOnIdentification(
                        dbContext,
                        targetMovie,
                        collectionItem,
                        previousTmdbId,
                        DateTime.UtcNow);
                }
            }

            if (!targetMovie.DefaultMediaFileId.HasValue)
            {
                targetMovie.DefaultMediaFileId = currentMovie.DefaultMediaFileId
                    ?? currentMediaFiles.FirstOrDefault(x => x.MediaType == MediaType.Video)?.Id
                    ?? targetMovie.MediaFiles.FirstOrDefault(x => x.MediaType == MediaType.Video)?.Id;
            }
        }
        else if (!targetMovie.DefaultMediaFileId.HasValue)
        {
            targetMovie.DefaultMediaFileId = targetMovie.MediaFiles
                .Where(x => x.MediaType == MediaType.Video)
                .Select(x => x.Id)
                .FirstOrDefault();
        }

        if (!currentMovieHadTmdb && existingMatchedMovie is null)
        {
            var now = DateTime.UtcNow;
            RecordMovieStateActivationsOnIdentification(dbContext, targetMovie, now);
            await NormalizeCollectionItemsForIdentifiedMovieAsync(dbContext, targetMovie, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var currentMovie = mediaFile.MovieId.HasValue
            ? await LoadMovieAggregateAsync(dbContext, mediaFile.MovieId.Value, cancellationToken)
            : null;
        var currentMovieHadTmdb = currentMovie?.TmdbId is > 0;

        var targetMovie = !string.IsNullOrWhiteSpace(candidate.ImdbId) || candidate.TmdbId > 0
            ? await dbContext.Movies
                .Include(x => x.MediaFiles)
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

        var oldTargetWatched = targetMovie.IsWatched;
        var oldTargetFavorite = targetMovie.IsFavorite;
        await ApplyMetadataAsync(dbContext, targetMovie, candidate, status, candidate.Confidence, result, cancellationToken);

        if (currentMovie is not null && currentMovie.Id != targetMovie.Id)
        {
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
                targetMovie.IsFavorite |= currentMovie.IsFavorite;
                targetMovie.IsWatched |= currentMovie.IsWatched;
                targetMovie.UserRating ??= currentMovie.UserRating;
                targetMovie.LastPlayedAt = MaxDate(targetMovie.LastPlayedAt, currentMovie.LastPlayedAt);
                targetMovie.AutoWatchedBaselineAtUtc = MaxDate(targetMovie.AutoWatchedBaselineAtUtc, currentMovie.AutoWatchedBaselineAtUtc);
                if (!currentMovieHadTmdb)
                {
                    RecordMovieStateChangeOnIdentification(dbContext, targetMovie, oldTargetWatched, oldTargetFavorite);
                }

                var currentCollectionItems = await dbContext.UserMovieCollectionItems
                    .Where(x => x.MovieId == currentMovie.Id)
                    .ToListAsync(cancellationToken);
                var now = DateTime.UtcNow;
                foreach (var collectionItem in currentCollectionItems)
                {
                    var previousTmdbId = collectionItem.TmdbId;
                    collectionItem.MovieId = targetMovie.Id;
                    collectionItem.IsInLibrary = targetMovie.MediaFiles.Any(x => x.MediaType == MediaType.Video) || mediaFile.MediaType == MediaType.Video;
                    ApplyIdentificationSnapshot(collectionItem, targetMovie);
                    collectionItem.UpdatedAt = now;
                    if (!currentMovieHadTmdb)
                    {
                        RecordCollectionStateActivationsOnIdentification(
                            dbContext,
                            targetMovie,
                            collectionItem,
                            previousTmdbId,
                            now);
                    }
                }

                currentMovie.IsFavorite = false;
                currentMovie.IsWatched = false;
                currentMovie.UserRating = null;
                currentMovie.LastPlayedAt = null;
                currentMovie.AutoWatchedBaselineAtUtc = null;
            }
        }

        mediaFile.Movie = targetMovie;
        mediaFile.UpdatedAt = DateTime.UtcNow;

        if (!targetMovie.DefaultMediaFileId.HasValue)
        {
            targetMovie.DefaultMediaFileId = mediaFile.Id;
        }

        if (!currentMovieHadTmdb && currentMovie is not null && currentMovie.Id == targetMovie.Id)
        {
            var now = DateTime.UtcNow;
            RecordMovieStateActivationsOnIdentification(dbContext, targetMovie, now);
            await NormalizeCollectionItemsForIdentifiedMovieAsync(dbContext, targetMovie, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (currentMovie is not null && currentMovie.Id != targetMovie.Id)
        {
            await CleanupMovieIfOrphanedAsync(dbContext, currentMovie.Id, cancellationToken);
        }
    }

    private static void RecordMovieStateChangeOnIdentification(
        AppDbContext dbContext,
        Movie targetMovie,
        bool oldWatched,
        bool oldFavorite)
    {
        var now = DateTime.UtcNow;
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            targetMovie.TmdbId,
            targetMovie.Id,
            collectionItemId: null,
            targetMovie.Title,
            UserMovieStateChangeHistoryRecorder.StateWatched,
            oldWatched,
            targetMovie.IsWatched,
            UserMovieStateChangeHistoryRecorder.SourceIdentification,
            now);
        UserMovieStateChangeHistoryRecorder.RecordIfChanged(
            dbContext,
            targetMovie.TmdbId,
            targetMovie.Id,
            collectionItemId: null,
            targetMovie.Title,
            UserMovieStateChangeHistoryRecorder.StateFavorite,
            oldFavorite,
            targetMovie.IsFavorite,
            UserMovieStateChangeHistoryRecorder.SourceIdentification,
            now);
    }

    private static void RecordMovieStateActivationsOnIdentification(
        AppDbContext dbContext,
        Movie movie,
        DateTime changedAtUtc)
    {
        if (movie.IsWatched)
        {
            UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                dbContext,
                movie.TmdbId,
                movie.Id,
                collectionItemId: null,
                movie.Title,
                UserMovieStateChangeHistoryRecorder.StateWatched,
                oldValue: false,
                newValue: true,
                source: UserMovieStateChangeHistoryRecorder.SourceIdentification,
                changedAtUtc: changedAtUtc);
        }

        if (movie.IsFavorite)
        {
            UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                dbContext,
                movie.TmdbId,
                movie.Id,
                collectionItemId: null,
                movie.Title,
                UserMovieStateChangeHistoryRecorder.StateFavorite,
                oldValue: false,
                newValue: true,
                source: UserMovieStateChangeHistoryRecorder.SourceIdentification,
                changedAtUtc: changedAtUtc);
        }
    }

    private static async Task NormalizeCollectionItemsForIdentifiedMovieAsync(
        AppDbContext dbContext,
        Movie movie,
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
            var previousTmdbId = collectionItem.TmdbId;
            ApplyIdentificationSnapshot(collectionItem, movie);
            collectionItem.UpdatedAt = now;
            RecordCollectionStateActivationsOnIdentification(
                dbContext,
                movie,
                collectionItem,
                previousTmdbId,
                now);
        }
    }

    private static void RecordCollectionStateActivationsOnIdentification(
        AppDbContext dbContext,
        Movie movie,
        UserMovieCollectionItem collectionItem,
        int? previousTmdbId,
        DateTime changedAtUtc)
    {
        if (movie.TmdbId is not > 0 || previousTmdbId == movie.TmdbId.Value)
        {
            return;
        }

        if (collectionItem.IsWatched)
        {
            UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                dbContext,
                movie.TmdbId,
                movie.Id,
                collectionItem.Id == 0 ? null : collectionItem.Id,
                collectionItem.Title,
                UserMovieStateChangeHistoryRecorder.StateWatched,
                oldValue: false,
                newValue: true,
                source: UserMovieStateChangeHistoryRecorder.SourceIdentification,
                changedAtUtc: changedAtUtc);
        }

        if (collectionItem.IsWantToWatch)
        {
            UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                dbContext,
                movie.TmdbId,
                movie.Id,
                collectionItem.Id == 0 ? null : collectionItem.Id,
                collectionItem.Title,
                UserMovieStateChangeHistoryRecorder.StateWantToWatch,
                oldValue: false,
                newValue: true,
                source: UserMovieStateChangeHistoryRecorder.SourceIdentification,
                changedAtUtc: changedAtUtc);
        }

        if (collectionItem.IsNotInterested)
        {
            UserMovieStateChangeHistoryRecorder.RecordIfChanged(
                dbContext,
                movie.TmdbId,
                movie.Id,
                collectionItem.Id == 0 ? null : collectionItem.Id,
                collectionItem.Title,
                UserMovieStateChangeHistoryRecorder.StateNotInterested,
                oldValue: false,
                newValue: true,
                source: UserMovieStateChangeHistoryRecorder.SourceIdentification,
                changedAtUtc: changedAtUtc);
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
        movie.Overview = string.IsNullOrWhiteSpace(candidate.Overview) ? null : candidate.Overview;
        movie.PosterRemoteUrl = string.IsNullOrWhiteSpace(candidate.PosterRemoteUrl) ? null : candidate.PosterRemoteUrl;
        movie.Country = string.IsNullOrWhiteSpace(candidate.Country) ? null : candidate.Country;
        movie.Language = string.IsNullOrWhiteSpace(candidate.Language) ? null : candidate.Language;
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

        if (!targetMovie.DefaultMediaFileId.HasValue)
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

        var yearScore = 0d;
        if (expectedYear.HasValue && candidate.ReleaseYear.HasValue)
        {
            yearScore = expectedYear.Value == candidate.ReleaseYear.Value
                ? 1d
                : Math.Abs(expectedYear.Value - candidate.ReleaseYear.Value) == 1
                    ? 0.5d
                    : 0d;
        }
        else if (!expectedYear.HasValue)
        {
            yearScore = 0.4d;
        }

        return Math.Clamp((bestTitleScore * 0.8d) + (yearScore * 0.2d), 0d, 1d);
    }

    private static async Task<Movie?> LoadMovieAggregateAsync(
        AppDbContext dbContext,
        int movieId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
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

        if (movie.MediaFiles.Count == 0
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

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
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
}
