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

public sealed class AiClassificationService : IAiClassificationService
{
    private readonly IAiService _aiService;

    public AiClassificationService(IAiService aiService)
    {
        _aiService = aiService;
    }

    public async Task ClassifyMovieAsync(int movieId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is null)
        {
            return;
        }

        if (!movie.TmdbId.HasValue
            || movie.IdentificationStatus is not (IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed))
        {
            return;
        }

        var local = BuildLocalTags(movie.GenresText, movie.Overview);
        movie.AiTagsText = null;
        movie.EmotionTagsText = null;
        movie.SceneTagsText = null;
        try
        {
            var text = await _aiService.GenerateTextAsync(
                $$"""
                你是影音库标签助手。只能从下面的固定词表中选择标签，不能创造新标签、近义标签或英文标签。
                类型标签词表：{{string.Join("、", AiTagVocabulary.TypeTags)}}
                情绪标签词表：{{string.Join("、", AiTagVocabulary.EmotionTags)}}
                观看场景词表：{{string.Join("、", AiTagVocabulary.SceneTags)}}
                输出要求：
                1. 只返回 JSON，不要解释。
                2. 字段固定为 aiTags、emotionTags、sceneTags。
                3. 每个字段都是中文字符串数组。
                4. 每类选择 1 到 4 个标签。
                5. 所有标签必须来自对应词表。
                示例：{"aiTags":["剧情"],"emotionTags":["温暖"],"sceneTags":["独自观看"]}
                """,
                $"片名：{movie.Title}\n年份：{movie.ReleaseYear}\n类型：{movie.GenresText}\n简介：{movie.Overview}",
                AiRequestOptions.MovieTaggingFlash,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(text))
            {
                var parsed = ParseTags(text);
                movie.AiTagsText = parsed.aiTags.Count > 0 ? string.Join("、", parsed.aiTags) : local.aiTags;
                movie.EmotionTagsText = parsed.emotionTags.Count > 0 ? string.Join("、", parsed.emotionTags) : local.emotionTags;
                movie.SceneTagsText = parsed.sceneTags.Count > 0 ? string.Join("、", parsed.sceneTags) : local.sceneTags;
            }
            else
            {
                ApplyLocalTags(movie, local);
            }
        }
        catch
        {
            ApplyLocalTags(movie, local);
        }

        movie.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClassifyMoviesAsync(
        IReadOnlyCollection<int> movieIds,
        string sourceKind,
        CancellationToken cancellationToken = default)
    {
        var ids = movieIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-ai-concurrency-summary sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} movieCount=0 success=0 skipped=0 failed=0 finalConcurrency=0 retryableErrorCount=0 retryScheduledCount=0 retryExhaustedCount=0");
            return;
        }

        var executor = new AdaptiveAiBatchExecutor($"scan-movie-ai-tagging:{sourceKind}", ids.Length);
        using var saveGate = new SemaphoreSlim(1, 1);
        var countGate = new object();
        var successCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var tasks = ids
            .Select(
                async movieId =>
                {
                    var outcome = await ClassifyScanMovieAsync(movieId, sourceKind, executor, saveGate, cancellationToken);
                    lock (countGate)
                    {
                        switch (outcome.Status)
                        {
                            case "success":
                                successCount++;
                                break;
                            case "skipped":
                                skippedCount++;
                                break;
                            default:
                                failedCount++;
                                break;
                        }
                    }
                })
            .ToArray();

        await Task.WhenAll(tasks);
        ScanIdentificationDiagnostics.Write(
            $"event=scan-movie-ai-concurrency-summary sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} movieCount={ids.Length} success={successCount} skipped={skippedCount} failed={failedCount} finalConcurrency={executor.CurrentConcurrency} aiRequestSuccessCount={executor.SuccessCount} retryableErrorCount={executor.RetryableErrorCount} retryScheduledCount={executor.RetryScheduledCount} retryExhaustedCount={executor.RetryExhaustedCount}");
    }

    private async Task<MovieClassificationOutcome> ClassifyScanMovieAsync(
        int movieId,
        string sourceKind,
        AdaptiveAiBatchExecutor executor,
        SemaphoreSlim saveGate,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
            if (movie is null)
            {
                return new MovieClassificationOutcome("skipped", "movie-not-found");
            }

            if (!movie.TmdbId.HasValue
                || movie.IdentificationStatus is not (IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed))
            {
                return new MovieClassificationOutcome("skipped", "movie-not-recognized");
            }

            var local = BuildLocalTags(movie.GenresText, movie.Overview);
            movie.AiTagsText = null;
            movie.EmotionTagsText = null;
            movie.SceneTagsText = null;
            var status = "success";
            var reason = string.Empty;
            try
            {
                var text = await executor.ExecuteAsync(
                    "scan-movie-tagging",
                    $"movie:{movieId}",
                    (_, token) => GenerateScanMovieTagsTextAsync(movie, token),
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var parsed = ParseTags(text);
                    movie.AiTagsText = parsed.aiTags.Count > 0 ? string.Join(",", parsed.aiTags) : local.aiTags;
                    movie.EmotionTagsText = parsed.emotionTags.Count > 0 ? string.Join(",", parsed.emotionTags) : local.emotionTags;
                    movie.SceneTagsText = parsed.sceneTags.Count > 0 ? string.Join(",", parsed.sceneTags) : local.sceneTags;
                }
                else
                {
                    ApplyLocalTags(movie, local);
                    status = "skipped";
                    reason = "ai-returned-empty";
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ApplyLocalTags(movie, local);
                status = "failed";
                reason = TrimMessage(exception.Message);
            }

            movie.UpdatedAt = DateTime.UtcNow;
            await saveGate.WaitAsync(cancellationToken);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                saveGate.Release();
            }

            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-ai-classify-item-complete sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} movieId={movieId} status={ScanIdentificationDiagnostics.FormatValue(status)} reason={ScanIdentificationDiagnostics.FormatValue(reason, 180)}");
            return new MovieClassificationOutcome(status, reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = TrimMessage(exception.Message);
            ScanIdentificationDiagnostics.Write(
                $"event=scan-movie-ai-classify-item-failed sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} movieId={movieId} failureReason={ScanIdentificationDiagnostics.FormatValue(reason, 180)}");
            return new MovieClassificationOutcome("failed", reason);
        }
    }

    private Task<string?> GenerateScanMovieTagsTextAsync(Movie movie, CancellationToken cancellationToken)
    {
        return _aiService.GenerateTextAsync(
            $$"""
            You are a movie library tagging assistant.
            Choose tags only from the fixed vocabularies below. Do not invent new tags, synonyms, or English tags.
            Type tag vocabulary: {{string.Join(",", AiTagVocabulary.TypeTags)}}
            Emotion tag vocabulary: {{string.Join(",", AiTagVocabulary.EmotionTags)}}
            Viewing scene vocabulary: {{string.Join(",", AiTagVocabulary.SceneTags)}}
            Return JSON only with fixed keys: aiTags, emotionTags, sceneTags.
            Each field must be a string array and should contain 1 to 4 Chinese tags from its matching vocabulary.
            Example: {"aiTags":["tag"],"emotionTags":["tag"],"sceneTags":["tag"]}
            """,
            $"title={movie.Title}\nyear={movie.ReleaseYear}\ngenres={movie.GenresText}\noverview={movie.Overview}",
            AiRequestOptions.MovieTaggingFlash,
            cancellationToken);
    }

    public async Task<AiMovieTags> ClassifyExternalMovieAsync(
        AiRecommendationItem recommendation,
        CancellationToken cancellationToken = default)
    {
        var local = BuildLocalTags(recommendation.Tags, recommendation.Overview);
        try
        {
            var text = await _aiService.GenerateTextAsync(
                $$"""
                你是影音库标签助手。只能从下面的固定词表中选择标签，不能创造新标签、近义标签或英文标签。
                类型标签词表：{{string.Join("、", AiTagVocabulary.TypeTags)}}
                情绪标签词表：{{string.Join("、", AiTagVocabulary.EmotionTags)}}
                观看场景词表：{{string.Join("、", AiTagVocabulary.SceneTags)}}
                输出要求：
                1. 只返回 JSON，不要解释。
                2. 字段固定为 aiTags、emotionTags、sceneTags。
                3. 每个字段都是中文字符串数组。
                4. 每类选择 1 到 4 个标签。
                5. 所有标签必须来自对应词表。
                示例：{"aiTags":["剧情"],"emotionTags":["温暖"],"sceneTags":["独自观看"]}
                """,
                $"片名：{recommendation.Title}\n原名：{recommendation.OriginalTitle}\n年份：{recommendation.ReleaseYear}\n类型：{recommendation.Tags}\n简介：{recommendation.Overview}",
                AiRequestOptions.MovieTaggingFlash,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(text))
            {
                var parsed = ParseTags(text);
                return new AiMovieTags
                {
                    AiTagsText = parsed.aiTags.Count > 0 ? string.Join("、", parsed.aiTags) : local.aiTags,
                    EmotionTagsText = parsed.emotionTags.Count > 0 ? string.Join("、", parsed.emotionTags) : local.emotionTags,
                    SceneTagsText = parsed.sceneTags.Count > 0 ? string.Join("、", parsed.sceneTags) : local.sceneTags
                };
            }
        }
        catch
        {
        }

        return new AiMovieTags
        {
            AiTagsText = local.aiTags,
            EmotionTagsText = local.emotionTags,
            SceneTagsText = local.sceneTags
        };
    }

    public async Task<AiSearchSuggestion> SuggestSearchQueryAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var result = await SuggestSearchQueryWithStatusAsync(movieId, cancellationToken);
        return result.Status == AiSearchSuggestionStatus.Success
            ? result.Suggestion
            : result.FallbackSuggestion;
    }

    public async Task<AiSearchSuggestionResult> SuggestSearchQueryWithStatusAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var movie = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.Id == movieId)
            .Select(
                x => new SearchSuggestionMovieContext
                {
                    Title = x.Title,
                    ReleaseYear = x.ReleaseYear,
                    Overview = x.Overview,
                    SourceFileName = x.MediaFiles
                        .Where(media => !media.IsDeleted && media.MediaType == MediaType.Video)
                        .OrderBy(media => media.Id)
                        .Select(media => media.FileName)
                        .FirstOrDefault(),
                    SourceFilePath = x.MediaFiles
                        .Where(media => !media.IsDeleted && media.MediaType == MediaType.Video)
                        .OrderBy(media => media.Id)
                        .Select(media => media.FilePath)
                        .FirstOrDefault()
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Failed,
                Message = "影片不存在。"
            };
        }

        var fallbackSuggestion = BuildFallbackSearchSuggestion(movie);

        try
        {
            var text = await _aiService.GenerateTextAsync(
                """
                You are a movie identification search assistant.
                Generate only a TMDB movie search title and optional year.
                Prioritize sourcePathHint and sourceFileName. Treat currentTitle, currentYear, and overview only as weak hints because they may be wrong.
                Use sourcePathHint and sourceFileName to identify the work, but never use the language or script of a file name to decide the TMDB original_title language or spelling.
                English, localized, or romanized file names can still belong to a non-English TMDB original title.
                If sourceFileName contains a specific work title or numbered part, prefer that specific title over a collection, franchise, pack, or parent-folder title.
                If sourceFileName or sourcePathHint contains both a localized title and an original title, return the original title.
                The title must match TMDB original_title semantics: the work's official original title stored by TMDB, not a translated/localized/marketing alias.
                Romanized/transliterated titles are aliases unless TMDB original_title itself is romanized.
                Return an English title only when TMDB original_title itself is English. If you only know an English/international/localized alias for a non-English-original movie, return an empty title instead of guessing.
                Never return TMDB ids. The app will search TMDB locally from the returned title.
                Return JSON only:
                {"title":"TMDB original_title-style movie search title","year":2002}
                Use null for year when it cannot be inferred safely.
                """,
                string.Join(
                    '\n',
                    $"sourceFileName={movie.SourceFileName}",
                    $"sourcePathHint={AiSourceContextFormatter.BuildPathHint(movie.SourceFilePath)}",
                    $"currentTitle={movie.Title}",
                    $"currentYear={movie.ReleaseYear}",
                    $"overview={movie.Overview}"),
                AiRequestOptions.CorrectionPro,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiSearchSuggestionResult
                {
                    Status = AiSearchSuggestionStatus.NoResult,
                    FallbackSuggestion = fallbackSuggestion,
                    Message = "AI 未返回可用搜索词。"
                };
            }

            var suggestion = ParseSearchSuggestion(text);
            if (string.IsNullOrWhiteSpace(suggestion.Query))
            {
                return new AiSearchSuggestionResult
                {
                    Status = AiSearchSuggestionStatus.NoResult,
                    FallbackSuggestion = fallbackSuggestion,
                    Message = "AI 未返回可用搜索标题。"
                };
            }

            suggestion.ReleaseYear ??= fallbackSuggestion.ReleaseYear;
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Success,
                Suggestion = suggestion,
                FallbackSuggestion = fallbackSuggestion
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Failed,
                FallbackSuggestion = fallbackSuggestion,
                Message = "AI 请求超时，请稍后重试。"
            };
        }
        catch (Exception exception)
        {
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Failed,
                FallbackSuggestion = fallbackSuggestion,
                Message = TrimMessage(exception.Message)
            };
        }
    }

    public Task<AiSearchSuggestionResult> SuggestMovieCorrectionSearchQueryAsync(
        string currentTitle,
        string? sourceFileName,
        int? releaseYear = null,
        string? overview = null,
        string? sourcePath = null,
        CancellationToken cancellationToken = default)
    {
        var fallbackSuggestion = BuildFallbackSearchSuggestion(
            currentTitle,
            sourceFileName,
            releaseYear,
            preferSourceFileName: true,
            cleanForTv: false);

        var context = string.Join(
            '\n',
            $"targetKind=Movie",
            $"sourceFileName={sourceFileName}",
            $"sourcePathHint={AiSourceContextFormatter.BuildPathHint(sourcePath)}",
            $"currentTitle={currentTitle}",
            $"releaseYear={releaseYear}",
            $"overview={overview}");

        return SuggestCorrectionSearchQueryAsync(
            """
            You are a single-source movie correction assistant.
            The user has already selected targetKind=Movie.
            Only generate a TMDB movie search title and optional year.
            Prioritize sourcePathHint and sourceFileName. Treat currentTitle and overview only as weak hints because they may be wrong.
            Use sourcePathHint and sourceFileName to identify the work, but never use the language or script of a file name to decide the TMDB original_title language or spelling.
            English, localized, or romanized file names can still belong to a non-English TMDB original title.
            If sourceFileName contains a specific work title or numbered part, prefer that specific title over a collection, franchise, pack, or parent-folder title.
            If sourceFileName or sourcePathHint contains both a localized title and an original title, return the original title.
            The title must match TMDB original_title semantics: the work's official original title stored by TMDB, not a translated/localized/marketing alias.
            Romanized/transliterated titles are aliases unless TMDB original_title itself is romanized.
            Return an English title only when TMDB original_title itself is English. If you only know an English/international/localized alias for a non-English-original movie, return an empty title instead of guessing.
            Never return TMDB ids. The app will search TMDB locally from the returned title.
            Special/SP/OVA/OAD/special episode/theatrical wording is not an automatic skip for a Movie target. Return a movie title when it safely maps to a standalone movie; otherwise return an empty title instead of forcing another target kind.
            Do not classify the item as TV, do not suggest TV fields, and do not switch target kind.
            Return JSON only:
            {"title":"TMDB original_title-style movie search title","year":2002}
            Use null for year when it cannot be inferred safely.
            """,
            context,
            fallbackSuggestion,
            AiRequestOptions.CorrectionPro,
            cancellationToken);
    }

    public Task<AiSearchSuggestionResult> SuggestTvEpisodeCorrectionSearchQueryAsync(
        string currentTitle,
        string? sourceFileName,
        string? seriesTitle = null,
        int? seasonNumber = null,
        int? episodeNumber = null,
        string? overview = null,
        string? sourcePath = null,
        CancellationToken cancellationToken = default)
    {
        var fallbackTitle = FirstNonEmpty(seriesTitle, currentTitle);
        var fallbackSuggestion = BuildFallbackSearchSuggestion(
            fallbackTitle,
            sourceFileName,
            releaseYear: null,
            preferSourceFileName: false,
            cleanForTv: true,
            seasonNumber,
            episodeNumber);

        var context = string.Join(
            '\n',
            $"targetKind=TvEpisode",
            $"sourceFileName={sourceFileName}",
            $"sourcePathHint={AiSourceContextFormatter.BuildPathHint(sourcePath)}",
            $"currentTitle={currentTitle}",
            $"seriesTitle={seriesTitle}",
            $"seasonNumber={seasonNumber}",
            $"episodeNumber={episodeNumber}",
            $"overview={overview}");

        return SuggestCorrectionSearchQueryAsync(
            """
            You are a single-source TV episode correction assistant.
            The user has already selected targetKind=TvEpisode.
            Generate a TMDB TV series search title plus the most likely season and episode numbers.
            Prioritize sourcePathHint and sourceFileName. Treat currentTitle, seriesTitle, seasonNumber, and episodeNumber only as weak hints because they may be wrong.
            Use sourcePathHint and sourceFileName to identify the work and episode evidence, but never use the language or script of a file name to decide the TMDB original_name language or spelling.
            English, localized, or romanized file names can still belong to a non-English TMDB original name.
            Do not let currentTitle or seriesTitle override clearer sourcePathHint/sourceFileName evidence.
            The title must match TMDB original_name semantics: the series' official original name stored by TMDB, not a translated/localized/marketing alias.
            If the original language title is Japanese, Korean, Chinese, Spanish, French, German, or another non-English title, return that original spelling/script, not the English/international title.
            Romanized/transliterated series names are aliases unless TMDB original_name itself is romanized. If a romanized alias confidently identifies the native-script official original_name, return the native-script original_name; otherwise return an empty title.
            Return an English series title only when TMDB original_name itself is English. If you only know an English/international/localized alias for a non-English-original series, return an empty title instead of guessing.
            Final-season wording such as 最终季, 完结篇, final season, or the final season is a season-number clue. Use it to return the correct TMDB seasonNumber only when you are confident; otherwise return null instead of guessing.
            If sourceFileName contains explicit SxxEyy or a clear ordinary episode number, do not treat final/chapter/part wording as OVA, special, or movie by itself. Use the explicit season/episode when safe.
            Never return TMDB ids. The app will search TMDB locally from the returned title.
            Do not classify the item as a movie.
            Do not switch target kind and do not return movie candidates.
            Special/SP/OVA/OAD/special episode/theatrical wording is not an automatic skip for a TV Episode target. Return title, seasonNumber, and episodeNumber only when it can safely be represented as a TV episode; otherwise return an empty title or null numbers.
            Return JSON only:
            {"title":"TMDB original_name-style tv series search title","year":null,"seasonNumber":1,"episodeNumber":2}
            Use null for year unless a first-air year is clearly useful for search.
            Use null for seasonNumber or episodeNumber only when it cannot be inferred safely.
            """,
            context,
            fallbackSuggestion,
            AiRequestOptions.CorrectionPro,
            cancellationToken);
    }

    public Task<AiSearchSuggestionResult> SuggestTvSeasonCorrectionSearchQueryAsync(
        string currentTitle,
        IReadOnlyCollection<string> sourceFileNames,
        string? seriesTitle = null,
        int? seasonNumber = null,
        string? overview = null,
        CancellationToken cancellationToken = default)
    {
        var sampledFileNames = sourceFileNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Path.GetFileName(x.Trim()))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(9)
            .ToArray();
        var fallbackTitle = FirstNonEmpty(seriesTitle, currentTitle);
        var fallbackSuggestion = BuildFallbackSearchSuggestion(
            fallbackTitle,
            sampledFileNames.FirstOrDefault(),
            releaseYear: null,
            preferSourceFileName: false,
            cleanForTv: true,
            seasonNumber,
            episodeNumber: null);

        var context = string.Join(
            '\n',
            $"targetKind=TvSeason",
            $"currentTitle={currentTitle}",
            $"seriesTitle={seriesTitle}",
            $"seasonNumber={seasonNumber}",
            $"overview={overview}",
            $"sampleFileNames={string.Join(" | ", sampledFileNames)}");

        return SuggestCorrectionSearchQueryAsync(
            """
            You are a TV season correction assistant.
            The user has already selected targetKind=TvSeason.
            Generate a TMDB TV series search title plus the most likely season number.
            Prioritize sampled source file names. Treat currentTitle, seriesTitle, and seasonNumber only as weak hints because they may be wrong.
            Use sampled episode numbers and folder-like title clues to identify one target TV season.
            Do not assume Part 1, Part 2, cour, half-season, final part, or release-part wording means multiple TMDB seasons. Many releases split one TMDB season into parts.
            The title must match TMDB original_name semantics: the series' official original name stored by TMDB, not a translated/localized/marketing alias.
            If the original language title is Japanese, Korean, Chinese, Spanish, French, German, or another non-English title, return that original spelling/script, not the English/international title.
            Romanized/transliterated series names are aliases unless TMDB original_name itself is romanized. If a romanized alias confidently identifies the native-script official original_name, return the native-script original_name; otherwise return an empty title.
            Return an English series title only when TMDB original_name itself is English. If you only know an English/international/localized alias for a non-English-original series, return an empty title instead of guessing.
            Never return TMDB ids. The app will search TMDB locally from the returned title.
            Special/SP/OVA/OAD/special episode/theatrical wording is not an automatic skip for a TV Season target. Return a season only when sampled rows safely represent one TV season; otherwise return an empty title or null seasonNumber.
            Return JSON only:
            {"title":"TMDB original_name-style tv series search title","year":null,"seasonNumber":1}
            Use null for year unless a first-air year is clearly useful for search.
            Use null for seasonNumber only when it cannot be inferred safely.
            """,
            context,
            fallbackSuggestion,
            AiRequestOptions.CorrectionPro,
            cancellationToken);
    }

    private async Task<AiSearchSuggestionResult> SuggestCorrectionSearchQueryAsync(
        string instruction,
        string context,
        AiSearchSuggestion fallbackSuggestion,
        AiRequestOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await _aiService.GenerateTextAsync(
                instruction,
                context,
                options,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiSearchSuggestionResult
                {
                    Status = AiSearchSuggestionStatus.NoResult,
                    FallbackSuggestion = fallbackSuggestion,
                    Message = "AI 未返回可用搜索词。"
                };
            }

            var suggestion = ParseSearchSuggestion(text);
            if (string.IsNullOrWhiteSpace(suggestion.Query))
            {
                return new AiSearchSuggestionResult
                {
                    Status = AiSearchSuggestionStatus.NoResult,
                    FallbackSuggestion = fallbackSuggestion,
                    Message = "AI 未返回可用搜索标题。"
                };
            }

            suggestion.ReleaseYear ??= fallbackSuggestion.ReleaseYear;
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Success,
                Suggestion = suggestion,
                FallbackSuggestion = fallbackSuggestion
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Failed,
                FallbackSuggestion = fallbackSuggestion,
                Message = "AI 请求超时，请稍后重试。"
            };
        }
        catch (Exception exception)
        {
            return new AiSearchSuggestionResult
            {
                Status = AiSearchSuggestionStatus.Failed,
                FallbackSuggestion = fallbackSuggestion,
                Message = TrimMessage(exception.Message)
            };
        }
    }

    private static AiSearchSuggestion BuildFallbackSearchSuggestion(SearchSuggestionMovieContext movie)
    {
        var parsedFileName = !string.IsNullOrWhiteSpace(movie.SourceFileName)
            ? MovieFileNameParser.Parse(movie.SourceFileName)
            : new ParsedMovieName();
        var fallbackQuery = !string.IsNullOrWhiteSpace(parsedFileName.CleanTitle)
            ? parsedFileName.CleanTitle
            : movie.Title;

        return new AiSearchSuggestion
        {
            Query = fallbackQuery,
            ReleaseYear = parsedFileName.ReleaseYear
        };
    }

    private static AiSearchSuggestion BuildFallbackSearchSuggestion(
        string currentTitle,
        string? sourceFileName,
        int? releaseYear,
        bool preferSourceFileName,
        bool cleanForTv,
        int? seasonNumber = null,
        int? episodeNumber = null)
    {
        var parsedQuery = string.Empty;
        int? parsedYear = null;
        int? parsedSeasonNumber = null;
        int? parsedEpisodeNumber = null;
        if (!string.IsNullOrWhiteSpace(sourceFileName))
        {
            if (cleanForTv)
            {
                parsedQuery = TvEpisodeFileNameParser.CleanSeriesNameCandidate(sourceFileName);
                var parsedEpisode = TvEpisodeFileNameParser.Parse(
                    sourceFileName,
                    allowSeasonContextOnly: true,
                    seasonNumberHint: seasonNumber,
                    allowStrongContextFallbacks: true);
                if (parsedEpisode.IsEpisodeLike && !parsedEpisode.IsMultiEpisode)
                {
                    parsedSeasonNumber = parsedEpisode.SeasonNumber > 0 ? parsedEpisode.SeasonNumber : null;
                    parsedEpisodeNumber = parsedEpisode.EpisodeNumber > 0 ? parsedEpisode.EpisodeNumber : null;
                }
            }
            else
            {
                var parsedFileName = MovieFileNameParser.Parse(sourceFileName);
                parsedQuery = parsedFileName.CleanTitle;
                parsedYear = parsedFileName.ReleaseYear;
            }
        }

        var fallbackQuery = preferSourceFileName
            ? FirstNonEmpty(parsedQuery, currentTitle)
            : FirstNonEmpty(currentTitle, parsedQuery);

        return new AiSearchSuggestion
        {
            Query = fallbackQuery,
            ReleaseYear = parsedYear ?? releaseYear,
            SeasonNumber = parsedSeasonNumber ?? seasonNumber,
            EpisodeNumber = parsedEpisodeNumber ?? episodeNumber
        };
    }

    private static AiSearchSuggestion ParseSearchSuggestion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AiSearchSuggestion();
        }

        try
        {
            var jsonText = text;
            var start = jsonText.IndexOf('{');
            var end = jsonText.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                jsonText = jsonText[start..(end + 1)];
            }

            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            var title = root.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == JsonValueKind.String
                ? titleProperty.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            int? year = null;
            var seasonNumber = ReadNullableInt(root, "seasonNumber")
                               ?? ReadNullableInt(root, "season")
                               ?? ReadNullableInt(root, "season_no");
            var episodeNumber = ReadNullableInt(root, "episodeNumber")
                                ?? ReadNullableInt(root, "episode")
                                ?? ReadNullableInt(root, "episode_no");
            if (root.TryGetProperty("year", out var yearProperty))
            {
                if (yearProperty.ValueKind == JsonValueKind.Number && yearProperty.TryGetInt32(out var parsedYear))
                {
                    year = parsedYear;
                }
                else if (yearProperty.ValueKind == JsonValueKind.String && int.TryParse(yearProperty.GetString(), out parsedYear))
                {
                    year = parsedYear;
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                var parsedTitle = MovieFileNameParser.Parse(text);
                title = parsedTitle.CleanTitle;
                year ??= parsedTitle.ReleaseYear;
            }

            return new AiSearchSuggestion
            {
                Query = title,
                ReleaseYear = year,
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber
            };
        }
        catch
        {
            var parsed = MovieFileNameParser.Parse(text);
            return new AiSearchSuggestion
            {
                Query = parsed.CleanTitle,
                ReleaseYear = parsed.ReleaseYear
            };
        }
    }

    private static int? ReadNullableInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var parsedNumber))
        {
            return parsedNumber;
        }

        return property.ValueKind == JsonValueKind.String
               && int.TryParse(property.GetString(), out parsedNumber)
            ? parsedNumber
            : null;
    }

    private static void ApplyLocalTags(Movie movie, (string aiTags, string emotionTags, string sceneTags) local)
    {
        movie.AiTagsText = local.aiTags;
        movie.EmotionTagsText = local.emotionTags;
        movie.SceneTagsText = local.sceneTags;
    }

    private static (string aiTags, string emotionTags, string sceneTags) BuildLocalTags(string? genresText, string? overview)
    {
        var aiTags = AiTagVocabulary.PickFromText(genresText, AiTagVocabulary.TypeTags, ["剧情"]);
        var overviewText = overview ?? string.Empty;
        IReadOnlyList<string> emotionTags = overviewText.Contains("魔法", StringComparison.OrdinalIgnoreCase)
            ? ["梦幻", "温暖"]
            : overviewText.Contains("梦", StringComparison.OrdinalIgnoreCase)
                ? ["思考向", "悬疑"]
                : ["思考向", "温暖"];
        IReadOnlyList<string> sceneTags = aiTags.Contains("动画") || aiTags.Contains("家庭")
            ? ["亲子", "家人"]
            : aiTags.Contains("动作")
                ? ["周末", "朋友"]
                : ["独自观看"];

        return (
            string.Join("、", aiTags),
            string.Join("、", AiTagVocabulary.Filter(emotionTags, AiTagVocabulary.EmotionTags).DefaultIfEmpty("思考向")),
            string.Join("、", AiTagVocabulary.Filter(sceneTags, AiTagVocabulary.SceneTags).DefaultIfEmpty("独自观看")));
    }

    private static (List<string> aiTags, List<string> emotionTags, List<string> sceneTags) ParseTags(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            text = text[start..(end + 1)];
        }

        using var document = JsonDocument.Parse(text);
        return (
            AiTagVocabulary.Filter(ReadArray(document.RootElement, "aiTags"), AiTagVocabulary.TypeTags).ToList(),
            AiTagVocabulary.Filter(ReadArray(document.RootElement, "emotionTags"), AiTagVocabulary.EmotionTags).ToList(),
            AiTagVocabulary.Filter(ReadArray(document.RootElement, "sceneTags"), AiTagVocabulary.SceneTags).ToList());
    }

    private static List<string> ReadArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
    }

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private sealed record MovieClassificationOutcome(string Status, string Reason);

    private sealed class SearchSuggestionMovieContext
    {
        public string Title { get; set; } = string.Empty;

        public int? ReleaseYear { get; set; }

        public string? Overview { get; set; }

        public string? SourceFileName { get; set; }

        public string? SourceFilePath { get; set; }
    }
}
