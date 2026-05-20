using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TmdbService : ITmdbService
{
    private const string DefaultPrimaryApiBaseUrl = "https://api.tmdb.org/3/";
    private const string DefaultFallbackApiBaseUrl = "https://api.themoviedb.org/3/";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";
    private const string TmdbLanguage = "zh-CN";
    private const string TmdbProvider = "TMDB";
    private const string TmdbSearchCacheType = "Search";
    private const string TmdbDetailCacheType = "Detail";
    private const string TmdbExternalIdsCacheType = "ExternalIds";
    private const int DiscoveryPageSize = 20;
    internal const int HttpConcurrencyLimit = 3;
    private const int SearchCacheLimit = 300;
    private const int DetailCacheLimit = 600;
    private const int ExternalIdsCacheLimit = 600;
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan DetailCacheTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ExternalIdsCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SearchPersistentCacheTtl = TimeSpan.FromDays(3);
    private static readonly TimeSpan DetailPersistentCacheTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan ExternalIdsPersistentCacheTtl = TimeSpan.FromDays(30);
    private static readonly ConcurrentDictionary<string, CacheEntry<IReadOnlyList<MetadataSearchCandidate>>> SearchCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<MetadataSearchCandidate>> DetailCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<string>> ExternalIdsCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<TmdbTvSeriesSearchPage>> TvSeriesPageCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<TmdbTvSeriesDetailResult>> TvSeriesDetailCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<TmdbTvSeasonDetailResult>> TvSeasonDetailCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<TmdbTvSeriesExternalIdsResult>> TvSeriesExternalIdsCache = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim TmdbHttpLimiter = new(HttpConcurrencyLimit, HttpConcurrencyLimit);
    private static int TmdbHttpInFlight;

    private readonly HttpClient _httpClient = new(
        new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly ISettingsService _settingsService;

    public TmdbService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<MetadataSearchCandidate>> SearchMoviesAsync(
        string query,
        int? releaseYear,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return [];
        }

        var trimmedQuery = query.Trim();
        var cacheKey = BuildTmdbSearchCacheKey(trimmedQuery, releaseYear, options);
        if (!string.IsNullOrEmpty(cacheKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetCachedSearch(cacheKey, out var cachedResults))
            {
                AiPerfDiagnostics.RecordExternalCall("tmdb-search-cache-hit", TimeSpan.Zero, false);
                return cachedResults;
            }

            AiPerfDiagnostics.RecordExternalCall("tmdb-search-cache-miss", TimeSpan.Zero, false);
            var persistentSearch = await ExternalMetadataPersistentCache.TryGetAsync<List<MetadataSearchCandidate>>(
                TmdbProvider,
                TmdbSearchCacheType,
                cacheKey,
                cancellationToken);
            if (persistentSearch.IsHit && persistentSearch.Value is { Count: > 0 } persistentResults)
            {
                AiPerfDiagnostics.RecordExternalCall("tmdb-search-persistent-cache-hit", TimeSpan.Zero, false);
                var clonedResults = CloneCandidates(persistentResults);
                SetCacheValue(SearchCache, cacheKey, CloneCandidates(clonedResults), SearchCacheTtl, SearchCacheLimit);
                return clonedResults;
            }

            AiPerfDiagnostics.RecordExternalCall("tmdb-search-persistent-cache-miss", TimeSpan.Zero, false);
        }

        var queryString = $"search/movie?language={TmdbLanguage}&query={Uri.EscapeDataString(trimmedQuery)}";
        if (releaseYear.HasValue)
        {
            queryString += $"&year={releaseYear.Value}&primary_release_year={releaseYear.Value}";
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(queryString, options, cancellationToken);
            EnsureSuccessStatusCode(response);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            var results = new List<MetadataSearchCandidate>();
            if (!document.RootElement.TryGetProperty("results", out var resultArray))
            {
                return results;
            }

            foreach (var item in resultArray.EnumerateArray())
            {
                results.Add(new MetadataSearchCandidate
                {
                    TmdbId = item.GetProperty("id").GetInt32(),
                    Title = GetString(item, "title"),
                    OriginalTitle = GetString(item, "original_title"),
                    ReleaseYear = ParseYear(GetString(item, "release_date")),
                    Overview = GetString(item, "overview"),
                    PosterRemoteUrl = BuildPosterUrl(GetString(item, "poster_path")),
                    TmdbRating = GetDouble(item, "vote_average"),
                    TmdbVoteCount = GetInt(item, "vote_count")
                });
            }

            var finalResults = releaseYear.HasValue
                ? results
                    .OrderBy(candidate => GetYearSortGroup(releaseYear.Value, candidate.ReleaseYear))
                    .ThenBy(candidate => GetYearDistance(releaseYear.Value, candidate.ReleaseYear))
                    .ThenByDescending(candidate => candidate.TmdbVoteCount ?? 0)
                    .Take(10)
                    .ToList()
                : results.Take(10).ToList();
            if (!string.IsNullOrEmpty(cacheKey)
                && finalResults.Count > 0
                && !cancellationToken.IsCancellationRequested)
            {
                var cachedResults = CloneCandidates(finalResults).ToList();
                SetCacheValue(SearchCache, cacheKey, cachedResults, SearchCacheTtl, SearchCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    TmdbProvider,
                    TmdbSearchCacheType,
                    cacheKey,
                    cachedResults,
                    SearchPersistentCacheTtl,
                    cancellationToken);
            }

            return finalResults;
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-search", requestStopwatch.Elapsed, isError);
        }
    }

    public async Task<MetadataSearchCandidate?> GetMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return null;
        }

        var detailCacheKey = BuildTmdbDetailCacheKey(tmdbId, options);
        MetadataSearchCandidate detailsCandidate;
        var shouldWriteDetailCache = false;
        var externalIdsFailed = false;
        cancellationToken.ThrowIfCancellationRequested();
        if (TryGetCachedCandidate(DetailCache, detailCacheKey, out var cachedDetails))
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-detail-cache-hit", TimeSpan.Zero, false);
            detailsCandidate = cachedDetails;
        }
        else
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-detail-cache-miss", TimeSpan.Zero, false);
            var persistentDetails = await ExternalMetadataPersistentCache.TryGetAsync<MetadataSearchCandidate>(
                TmdbProvider,
                TmdbDetailCacheType,
                detailCacheKey,
                cancellationToken);
            if (persistentDetails.IsHit && persistentDetails.Value is not null)
            {
                AiPerfDiagnostics.RecordExternalCall("tmdb-detail-persistent-cache-hit", TimeSpan.Zero, false);
                detailsCandidate = CloneCandidate(persistentDetails.Value);
                SetCacheValue(DetailCache, detailCacheKey, CloneCandidate(detailsCandidate), DetailCacheTtl, DetailCacheLimit);
            }
            else
            {
                AiPerfDiagnostics.RecordExternalCall("tmdb-detail-persistent-cache-miss", TimeSpan.Zero, false);
                var detailStopwatch = Stopwatch.StartNew();
                var detailError = false;
                JsonDocument detailsDocument;
                try
                {
                    using var detailsResponse = await SendGetAsync($"movie/{tmdbId}?language={TmdbLanguage}", options, cancellationToken);
                    EnsureSuccessStatusCode(detailsResponse);

                    await using var detailsStream = await detailsResponse.Content.ReadAsStreamAsync(cancellationToken);
                    detailsDocument = await JsonDocument.ParseAsync(detailsStream, cancellationToken: cancellationToken);
                }
                catch
                {
                    detailError = true;
                    throw;
                }
                finally
                {
                    detailStopwatch.Stop();
                    AiPerfDiagnostics.RecordExternalCall("tmdb-detail", detailStopwatch.Elapsed, detailError);
                }

                using (detailsDocument)
                {
                    detailsCandidate = BuildDetailsCandidate(tmdbId, detailsDocument.RootElement);
                }

                shouldWriteDetailCache = true;
            }
        }

        string imdbId = detailsCandidate.ImdbId;
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            var externalCacheKey = BuildTmdbExternalIdsCacheKey(tmdbId, options);
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetCachedString(ExternalIdsCache, externalCacheKey, out var cachedImdbId))
            {
                AiPerfDiagnostics.RecordExternalCall("tmdb-external-ids-cache-hit", TimeSpan.Zero, false);
                imdbId = cachedImdbId;
            }
            else
            {
                AiPerfDiagnostics.RecordExternalCall("tmdb-external-ids-cache-miss", TimeSpan.Zero, false);
                var persistentExternalIds = await ExternalMetadataPersistentCache.TryGetAsync<TmdbExternalIdsPersistentPayload>(
                    TmdbProvider,
                    TmdbExternalIdsCacheType,
                    externalCacheKey,
                    cancellationToken);
                if (persistentExternalIds.IsHit
                    && !string.IsNullOrWhiteSpace(persistentExternalIds.Value?.ImdbId))
                {
                    AiPerfDiagnostics.RecordExternalCall("tmdb-external-ids-persistent-cache-hit", TimeSpan.Zero, false);
                    imdbId = persistentExternalIds.Value.ImdbId;
                    SetCacheValue(ExternalIdsCache, externalCacheKey, imdbId, ExternalIdsCacheTtl, ExternalIdsCacheLimit);
                }
                else
                {
                    AiPerfDiagnostics.RecordExternalCall("tmdb-external-ids-persistent-cache-miss", TimeSpan.Zero, false);
                    var externalStopwatch = Stopwatch.StartNew();
                    var externalError = false;
                    try
                    {
                        using var externalIdsResponse = await SendGetAsync($"movie/{tmdbId}/external_ids", options, cancellationToken);
                        externalError = !externalIdsResponse.IsSuccessStatusCode;
                        externalIdsFailed = externalError;
                        if (externalIdsResponse.IsSuccessStatusCode)
                        {
                            await using var externalStream = await externalIdsResponse.Content.ReadAsStreamAsync(cancellationToken);
                            using var externalDocument = await JsonDocument.ParseAsync(externalStream, cancellationToken: cancellationToken);
                            imdbId = GetString(externalDocument.RootElement, "imdb_id");
                            if (!string.IsNullOrWhiteSpace(imdbId) && !cancellationToken.IsCancellationRequested)
                            {
                                SetCacheValue(ExternalIdsCache, externalCacheKey, imdbId, ExternalIdsCacheTtl, ExternalIdsCacheLimit);
                                await ExternalMetadataPersistentCache.SetAsync(
                                    TmdbProvider,
                                    TmdbExternalIdsCacheType,
                                    externalCacheKey,
                                    new TmdbExternalIdsPersistentPayload(imdbId),
                                    ExternalIdsPersistentCacheTtl,
                                    cancellationToken);
                            }
                        }
                    }
                    catch
                    {
                        externalError = true;
                        externalIdsFailed = true;
                        throw;
                    }
                    finally
                    {
                        externalStopwatch.Stop();
                        AiPerfDiagnostics.RecordExternalCall("tmdb-external-ids", externalStopwatch.Elapsed, externalError);
                    }
                }
            }
        }

        detailsCandidate.ImdbId = imdbId;
        if (shouldWriteDetailCache
            && !externalIdsFailed
            && !cancellationToken.IsCancellationRequested)
        {
            var detailCacheValue = CloneCandidate(detailsCandidate);
            SetCacheValue(DetailCache, detailCacheKey, detailCacheValue, DetailCacheTtl, DetailCacheLimit);
            await ExternalMetadataPersistentCache.SetAsync(
                TmdbProvider,
                TmdbDetailCacheType,
                detailCacheKey,
                detailCacheValue,
                DetailPersistentCacheTtl,
                cancellationToken);
        }

        return detailsCandidate;
    }

    public async Task<TmdbMovieDiscoveryPage> SearchDiscoveryMoviesAsync(
        string query,
        int page,
        int? releaseYear = null,
        string region = "",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateEmptyDiscoveryPage(page);
        }

        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return CreateEmptyDiscoveryPage(page, "未配置 TMDB API。");
        }

        var safePage = Math.Clamp(page, 1, 500);
        var queryString = $"search/movie?language={TmdbLanguage}&include_adult=false&page={safePage}&query={Uri.EscapeDataString(query.Trim())}";
        if (releaseYear.HasValue)
        {
            queryString += $"&year={releaseYear.Value}&primary_release_year={releaseYear.Value}";
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            queryString += $"&region={Uri.EscapeDataString(region.Trim())}";
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(queryString, options, cancellationToken);
            EnsureSuccessStatusCode(response);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            return BuildDiscoveryPage(document.RootElement, safePage);
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-discovery-search", requestStopwatch.Elapsed, isError);
        }
    }

    public async Task<TmdbPersonSearchPage> SearchPeopleAsync(
        string query,
        int page,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateEmptyPersonSearchPage(page);
        }

        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return CreateEmptyPersonSearchPage(page, "未配置 TMDB API。");
        }

        var safePage = Math.Clamp(page, 1, 500);
        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(
                $"search/person?language={TmdbLanguage}&include_adult=false&page={safePage}&query={Uri.EscapeDataString(query.Trim())}",
                options,
                cancellationToken);
            EnsureSuccessStatusCode(response);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            return BuildPersonSearchPage(document.RootElement, safePage);
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-person-search", requestStopwatch.Elapsed, isError);
        }
    }

    public async Task<TmdbMovieDiscoveryPage> GetPersonMovieCreditsAsync(
        int personId,
        int page,
        string personName = "",
        CancellationToken cancellationToken = default)
    {
        if (personId <= 0)
        {
            return CreateEmptyDiscoveryPage(page, "未找到相关人物，可尝试英文名或原名。");
        }

        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return CreateEmptyDiscoveryPage(page, "未配置 TMDB API。");
        }

        var safePage = Math.Max(1, page);
        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var creditsResponse = await SendGetAsync($"person/{personId}/movie_credits?language={TmdbLanguage}", options, cancellationToken);
            EnsureSuccessStatusCode(creditsResponse);

            await using var creditsStream = await creditsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var creditsDocument = await JsonDocument.ParseAsync(creditsStream, cancellationToken: cancellationToken);
            var allCredits = BuildPersonMovieCredits(creditsDocument.RootElement);
            var totalResults = allCredits.Count;
            var totalPages = totalResults == 0 ? 0 : (int)Math.Ceiling(totalResults / (double)DiscoveryPageSize);
            var pageItems = allCredits
                .Skip((safePage - 1) * DiscoveryPageSize)
                .Take(DiscoveryPageSize)
                .ToList();

            var displayName = string.IsNullOrWhiteSpace(personName) ? "该人物" : personName.Trim();
            return new TmdbMovieDiscoveryPage
            {
                Results = pageItems,
                Page = safePage,
                TotalPages = totalPages,
                TotalResults = totalResults,
                ResultMessage = totalResults == 0
                    ? "未找到相关影片。"
                    : $"按人物“{displayName}”匹配到相关影片。"
            };
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-person-movie-credits", requestStopwatch.Elapsed, isError);
        }
    }

    public async Task<TmdbMovieDiscoveryPage> SearchDiscoveryMoviesByPersonAsync(
        string query,
        int page,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateEmptyDiscoveryPage(page);
        }

        var safePage = Math.Max(1, page);
        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            var people = await SearchPeopleAsync(query, 1, cancellationToken);
            var person = people.Results.FirstOrDefault();
            if (person is null)
            {
                return CreateEmptyDiscoveryPage(safePage, "未找到相关人物，可尝试英文名或原名。");
            }

            return await GetPersonMovieCreditsAsync(person.TmdbId, safePage, person.Name, cancellationToken);
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-discovery-person-search", requestStopwatch.Elapsed, isError);
        }
    }

    public Task<TmdbMovieDiscoveryPage> GetPopularMoviesAsync(
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        return GetDiscoveryListPageAsync(
            $"movie/popular?language={Uri.EscapeDataString(safeLanguage)}&page={safePage}",
            safePage,
            "tmdb-discovery-popular",
            cancellationToken);
    }

    public Task<TmdbMovieDiscoveryPage> GetTopRatedMoviesAsync(
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        return GetDiscoveryListPageAsync(
            $"movie/top_rated?language={Uri.EscapeDataString(safeLanguage)}&page={safePage}",
            safePage,
            "tmdb-discovery-top-rated",
            cancellationToken);
    }

    public Task<TmdbMovieDiscoveryPage> GetTrendingMoviesAsync(
        string timeWindow,
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        var safeWindow = string.Equals(timeWindow, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "day";
        return GetDiscoveryListPageAsync(
            $"trending/movie/{safeWindow}?language={Uri.EscapeDataString(safeLanguage)}&page={safePage}",
            safePage,
            $"tmdb-discovery-trending-{safeWindow}",
            cancellationToken);
    }

    public Task<TmdbTvSeriesSearchPage> SearchTvSeriesAsync(
        string query,
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(CreateEmptyTvSeriesPage(page));
        }

        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        var trimmedQuery = query.Trim();
        return GetTvSeriesPageAsync(
            $"search/tv?language={Uri.EscapeDataString(safeLanguage)}&include_adult=false&page={safePage}&query={Uri.EscapeDataString(trimmedQuery)}",
            safePage,
            BuildTmdbTvSearchCacheKey(trimmedQuery, safePage, safeLanguage),
            "tmdb-tv-search",
            cancellationToken);
    }

    public async Task<TmdbTvSeriesDetailResult?> GetTvSeriesDetailsAsync(
        int seriesId,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        if (seriesId <= 0)
        {
            return null;
        }

        var safeLanguage = NormalizeLanguage(language);
        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return null;
        }

        var cacheKey = BuildTmdbTvSeriesDetailCacheKey(seriesId, safeLanguage, options);
        if (TryGetCacheValue(TvSeriesDetailCache, cacheKey, out var cachedDetails))
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-series-detail-cache-hit", TimeSpan.Zero, false);
            return CloneTvSeriesDetail(cachedDetails);
        }

        var persistentDetails = await ExternalMetadataPersistentCache.TryGetAsync<TmdbTvSeriesDetailResult>(
            TmdbProvider,
            TmdbDetailCacheType,
            cacheKey,
            cancellationToken);
        if (persistentDetails.IsHit && persistentDetails.Value is not null)
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-series-detail-persistent-cache-hit", TimeSpan.Zero, false);
            var cloned = CloneTvSeriesDetail(persistentDetails.Value);
            SetCacheValue(TvSeriesDetailCache, cacheKey, CloneTvSeriesDetail(cloned), DetailCacheTtl, DetailCacheLimit);
            return cloned;
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(
                $"tv/{seriesId}?language={Uri.EscapeDataString(safeLanguage)}",
                options,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                isError = true;
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var result = BuildTvSeriesDetail(document.RootElement);
            if (result is not null && !cancellationToken.IsCancellationRequested)
            {
                var cacheValue = CloneTvSeriesDetail(result);
                SetCacheValue(TvSeriesDetailCache, cacheKey, cacheValue, DetailCacheTtl, DetailCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    TmdbProvider,
                    TmdbDetailCacheType,
                    cacheKey,
                    cacheValue,
                    DetailPersistentCacheTtl,
                    cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            isError = true;
            return null;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-series-detail", requestStopwatch.Elapsed, isError);
        }
    }

    public async Task<TmdbTvSeasonDetailResult?> GetTvSeasonDetailsAsync(
        int seriesId,
        int seasonNumber,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        if (seriesId <= 0 || seasonNumber < 0)
        {
            return null;
        }

        var safeLanguage = NormalizeLanguage(language);
        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return null;
        }

        var cacheKey = BuildTmdbTvSeasonDetailCacheKey(seriesId, seasonNumber, safeLanguage, options);
        if (TryGetCacheValue(TvSeasonDetailCache, cacheKey, out var cachedDetails))
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-season-detail-cache-hit", TimeSpan.Zero, false);
            return CloneTvSeasonDetail(cachedDetails);
        }

        var persistentDetails = await ExternalMetadataPersistentCache.TryGetAsync<TmdbTvSeasonDetailResult>(
            TmdbProvider,
            TmdbDetailCacheType,
            cacheKey,
            cancellationToken);
        if (persistentDetails.IsHit && persistentDetails.Value is not null)
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-season-detail-persistent-cache-hit", TimeSpan.Zero, false);
            var cloned = CloneTvSeasonDetail(persistentDetails.Value);
            SetCacheValue(TvSeasonDetailCache, cacheKey, CloneTvSeasonDetail(cloned), DetailCacheTtl, DetailCacheLimit);
            return cloned;
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(
                $"tv/{seriesId}/season/{seasonNumber}?language={Uri.EscapeDataString(safeLanguage)}",
                options,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                isError = true;
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var result = BuildTvSeasonDetail(seriesId, document.RootElement);
            if (result is not null && !cancellationToken.IsCancellationRequested)
            {
                var cacheValue = CloneTvSeasonDetail(result);
                SetCacheValue(TvSeasonDetailCache, cacheKey, cacheValue, DetailCacheTtl, DetailCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    TmdbProvider,
                    TmdbDetailCacheType,
                    cacheKey,
                    cacheValue,
                    DetailPersistentCacheTtl,
                    cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            isError = true;
            return null;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-season-detail", requestStopwatch.Elapsed, isError);
        }
    }

    public async Task<TmdbTvSeriesExternalIdsResult?> GetTvSeriesExternalIdsAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        if (seriesId <= 0)
        {
            return null;
        }

        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return null;
        }

        var cacheKey = BuildTmdbTvSeriesExternalIdsCacheKey(seriesId, options);
        if (TryGetCacheValue(TvSeriesExternalIdsCache, cacheKey, out var cachedExternalIds))
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-external-ids-cache-hit", TimeSpan.Zero, false);
            return CloneTvExternalIds(cachedExternalIds);
        }

        var persistentExternalIds = await ExternalMetadataPersistentCache.TryGetAsync<TmdbTvSeriesExternalIdsResult>(
            TmdbProvider,
            TmdbExternalIdsCacheType,
            cacheKey,
            cancellationToken);
        if (persistentExternalIds.IsHit && persistentExternalIds.Value is not null)
        {
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-external-ids-persistent-cache-hit", TimeSpan.Zero, false);
            var cloned = CloneTvExternalIds(persistentExternalIds.Value);
            SetCacheValue(TvSeriesExternalIdsCache, cacheKey, CloneTvExternalIds(cloned), ExternalIdsCacheTtl, ExternalIdsCacheLimit);
            return cloned;
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync($"tv/{seriesId}/external_ids", options, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                isError = true;
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var result = BuildTvExternalIds(document.RootElement);
            if (!cancellationToken.IsCancellationRequested)
            {
                var cacheValue = CloneTvExternalIds(result);
                SetCacheValue(TvSeriesExternalIdsCache, cacheKey, cacheValue, ExternalIdsCacheTtl, ExternalIdsCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    TmdbProvider,
                    TmdbExternalIdsCacheType,
                    cacheKey,
                    cacheValue,
                    ExternalIdsPersistentCacheTtl,
                    cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            isError = true;
            return null;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("tmdb-tv-external-ids", requestStopwatch.Elapsed, isError);
        }
    }

    public Task<TmdbTvSeriesSearchPage> GetPopularTvSeriesAsync(
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        return GetTvSeriesPageAsync(
            $"tv/popular?language={Uri.EscapeDataString(safeLanguage)}&page={safePage}",
            safePage,
            BuildTmdbTvListCacheKey("popular", safePage, safeLanguage),
            "tmdb-tv-popular",
            cancellationToken);
    }

    public Task<TmdbTvSeriesSearchPage> GetTopRatedTvSeriesAsync(
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        return GetTvSeriesPageAsync(
            $"tv/top_rated?language={Uri.EscapeDataString(safeLanguage)}&page={safePage}",
            safePage,
            BuildTmdbTvListCacheKey("top-rated", safePage, safeLanguage),
            "tmdb-tv-top-rated",
            cancellationToken);
    }

    public Task<TmdbTvSeriesSearchPage> GetTrendingTvSeriesAsync(
        string timeWindow,
        int page,
        string language = TmdbLanguage,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Clamp(page, 1, 500);
        var safeLanguage = NormalizeLanguage(language);
        var safeWindow = string.Equals(timeWindow, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "day";
        return GetTvSeriesPageAsync(
            $"trending/tv/{safeWindow}?language={Uri.EscapeDataString(safeLanguage)}&page={safePage}",
            safePage,
            BuildTmdbTvListCacheKey($"trending-{safeWindow}", safePage, safeLanguage),
            $"tmdb-tv-trending-{safeWindow}",
            cancellationToken);
    }

    private async Task<TmdbTvSeriesSearchPage> GetTvSeriesPageAsync(
        string queryString,
        int page,
        string cacheKey,
        string diagnosticsName,
        CancellationToken cancellationToken)
    {
        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return CreateEmptyTvSeriesPage(page, "TMDB API is not configured.");
        }

        var scopedCacheKey = $"{cacheKey}|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}";
        if (TryGetCacheValue(TvSeriesPageCache, scopedCacheKey, out var cachedPage))
        {
            AiPerfDiagnostics.RecordExternalCall($"{diagnosticsName}-cache-hit", TimeSpan.Zero, false);
            return CloneTvSeriesPage(cachedPage);
        }

        var persistentPage = await ExternalMetadataPersistentCache.TryGetAsync<TmdbTvSeriesSearchPage>(
            TmdbProvider,
            TmdbSearchCacheType,
            scopedCacheKey,
            cancellationToken);
        if (persistentPage.IsHit && persistentPage.Value is not null)
        {
            AiPerfDiagnostics.RecordExternalCall($"{diagnosticsName}-persistent-cache-hit", TimeSpan.Zero, false);
            var cloned = CloneTvSeriesPage(persistentPage.Value);
            SetCacheValue(TvSeriesPageCache, scopedCacheKey, CloneTvSeriesPage(cloned), SearchCacheTtl, SearchCacheLimit);
            return cloned;
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(queryString, options, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                isError = true;
                return CreateEmptyTvSeriesPage(page);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var result = BuildTvSeriesPage(document.RootElement, page);
            if (!cancellationToken.IsCancellationRequested)
            {
                var cacheValue = CloneTvSeriesPage(result);
                SetCacheValue(TvSeriesPageCache, scopedCacheKey, cacheValue, SearchCacheTtl, SearchCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    TmdbProvider,
                    TmdbSearchCacheType,
                    scopedCacheKey,
                    cacheValue,
                    SearchPersistentCacheTtl,
                    cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            isError = true;
            return CreateEmptyTvSeriesPage(page);
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall(diagnosticsName, requestStopwatch.Elapsed, isError);
        }
    }

    private async Task<TmdbMovieDiscoveryPage> GetDiscoveryListPageAsync(
        string queryString,
        int page,
        string diagnosticsName,
        CancellationToken cancellationToken)
    {
        var options = await GetRequestOptionsAsync(cancellationToken);
        if (!options.HasCredential)
        {
            return CreateEmptyDiscoveryPage(page, "未配置 TMDB API。");
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(queryString, options, cancellationToken);
            EnsureSuccessStatusCode(response);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            return BuildDiscoveryPage(document.RootElement, page);
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall(diagnosticsName, requestStopwatch.Elapsed, isError);
        }
    }

    private async Task<TmdbRequestOptions> GetRequestOptionsAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        return new TmdbRequestOptions(
            NormalizeApiBaseUrl(settings.TmdbBaseUrl),
            settings.TmdbReadAccessToken.Trim(),
            settings.TmdbApiKey.Trim());
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string requestUri,
        TmdbRequestOptions options,
        CancellationToken cancellationToken)
    {
        Exception? lastNetworkException = null;

        foreach (var baseUri in BuildApiBaseUris(options.ApiBaseUrl))
        {
            var effectiveRequestUri = options.UseBearerToken
                ? requestUri
                : AppendQueryParameter(requestUri, "api_key", options.ApiKey);

            var absoluteUri = new Uri(baseUri, effectiveRequestUri);
            using var request = new HttpRequestMessage(HttpMethod.Get, absoluteUri);
            if (options.UseBearerToken)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ReadAccessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            try
            {
                await TmdbHttpLimiter.WaitAsync(cancellationToken);
                var currentInFlight = Interlocked.Increment(ref TmdbHttpInFlight);
                AiPerfDiagnostics.RecordConcurrencySample("tmdb-http", currentInFlight);
                try
                {
                    return await _httpClient.SendAsync(request, cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref TmdbHttpInFlight);
                    TmdbHttpLimiter.Release();
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                                               && IsFallbackEligibleNetworkFailure(exception))
            {
                lastNetworkException = exception;
            }
        }

        throw new HttpRequestException("TMDB 请求失败，主地址和后备地址都不可用。", lastNetworkException);
    }

    private static void EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"TMDB 请求失败：{(int)response.StatusCode} {response.ReasonPhrase}",
            null,
            response.StatusCode);
    }

    private static IEnumerable<Uri> BuildApiBaseUris(string configuredBaseUrl)
    {
        var primary = new Uri(NormalizeApiBaseUrl(configuredBaseUrl), UriKind.Absolute);
        var fallback = new Uri(DefaultFallbackApiBaseUrl, UriKind.Absolute);

        yield return primary;
        if (!Uri.Compare(primary, fallback, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase).Equals(0))
        {
            yield return fallback;
        }
    }

    private static bool IsFallbackEligibleNetworkFailure(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException;
    }

    private static string NormalizeApiBaseUrl(string? baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? DefaultPrimaryApiBaseUrl : baseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        if (!normalized.EndsWith("/3/", StringComparison.Ordinal))
        {
            normalized = normalized.TrimEnd('/') + "/3/";
        }

        return normalized;
    }

    private static string NormalizeLanguage(string language)
    {
        return string.IsNullOrWhiteSpace(language) ? TmdbLanguage : language.Trim();
    }

    private static string AppendQueryParameter(string requestUri, string name, string value)
    {
        var separator = requestUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{requestUri}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
    }

    private static string BuildTmdbSearchCacheKey(
        string trimmedQuery,
        int? releaseYear,
        TmdbRequestOptions options)
    {
        return releaseYear.HasValue && !string.IsNullOrWhiteSpace(trimmedQuery)
            ? $"search|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}|language={TmdbLanguage}|year={releaseYear.Value}|query={trimmedQuery}"
            : string.Empty;
    }

    private static string BuildTmdbDetailCacheKey(int tmdbId, TmdbRequestOptions options)
    {
        return $"detail|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}|language={TmdbLanguage}|tmdb={tmdbId}";
    }

    private static string BuildTmdbExternalIdsCacheKey(int tmdbId, TmdbRequestOptions options)
    {
        return $"external-ids|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}|tmdb={tmdbId}";
    }

    private static string BuildTmdbTvSearchCacheKey(string trimmedQuery, int page, string language)
    {
        return $"tv-search|language={language}|page={page}|query-hash={HashCachePart(trimmedQuery)}";
    }

    private static string BuildTmdbTvListCacheKey(string listType, int page, string language)
    {
        return $"tv-list|type={listType}|language={language}|page={page}";
    }

    private static string BuildTmdbTvSeriesDetailCacheKey(int seriesId, string language, TmdbRequestOptions options)
    {
        return $"tv-series-detail|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}|language={language}|series={seriesId}";
    }

    private static string BuildTmdbTvSeasonDetailCacheKey(int seriesId, int seasonNumber, string language, TmdbRequestOptions options)
    {
        return $"tv-season-detail|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}|language={language}|series={seriesId}|season={seasonNumber}";
    }

    private static string BuildTmdbTvSeriesExternalIdsCacheKey(int seriesId, TmdbRequestOptions options)
    {
        return $"tv-external-ids|base={options.ApiBaseUrl}|auth={options.AuthFingerprint}|series={seriesId}";
    }

    private static string BuildCredentialFingerprint(string kind, string credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return $"{kind}:none";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
        return $"{kind}:{hash[..16]}";
    }

    private static string HashCachePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())));
        return hash[..16];
    }

    private static bool TryGetCachedSearch(
        string cacheKey,
        out IReadOnlyList<MetadataSearchCandidate> results)
    {
        if (TryGetCacheValue(SearchCache, cacheKey, out var cachedResults))
        {
            results = CloneCandidates(cachedResults);
            return true;
        }

        results = [];
        return false;
    }

    private static bool TryGetCachedCandidate(
        ConcurrentDictionary<string, CacheEntry<MetadataSearchCandidate>> cache,
        string cacheKey,
        out MetadataSearchCandidate candidate)
    {
        if (TryGetCacheValue(cache, cacheKey, out var cachedCandidate))
        {
            candidate = CloneCandidate(cachedCandidate);
            return true;
        }

        candidate = new MetadataSearchCandidate();
        return false;
    }

    private static bool TryGetCachedString(
        ConcurrentDictionary<string, CacheEntry<string>> cache,
        string cacheKey,
        out string value)
    {
        if (TryGetCacheValue(cache, cacheKey, out var cachedValue)
            && !string.IsNullOrWhiteSpace(cachedValue))
        {
            value = cachedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetCacheValue<T>(
        ConcurrentDictionary<string, CacheEntry<T>> cache,
        string cacheKey,
        out T value)
    {
        if (cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAtUtc > DateTime.UtcNow)
            {
                value = entry.Value;
                return true;
            }

            cache.TryRemove(cacheKey, out _);
        }

        value = default!;
        return false;
    }

    private static void SetCacheValue<T>(
        ConcurrentDictionary<string, CacheEntry<T>> cache,
        string cacheKey,
        T value,
        TimeSpan ttl,
        int capacity)
    {
        TrimCache(cache, capacity);
        cache[cacheKey] = new CacheEntry<T>(value, DateTime.UtcNow.Add(ttl));
    }

    private static void TrimCache<T>(
        ConcurrentDictionary<string, CacheEntry<T>> cache,
        int capacity)
    {
        var now = DateTime.UtcNow;
        foreach (var expiredKey in cache
                     .Where(pair => pair.Value.ExpiresAtUtc <= now)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            cache.TryRemove(expiredKey, out _);
        }

        var overflow = cache.Count - capacity + 1;
        if (overflow <= 0)
        {
            return;
        }

        foreach (var key in cache
                     .OrderBy(pair => pair.Value.ExpiresAtUtc)
                     .Take(overflow)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            cache.TryRemove(key, out _);
        }
    }

    private static MetadataSearchCandidate BuildDetailsCandidate(int tmdbId, JsonElement details)
    {
        return new MetadataSearchCandidate
        {
            TmdbId = tmdbId,
            Title = GetString(details, "title"),
            OriginalTitle = GetString(details, "original_title"),
            ReleaseYear = ParseYear(GetString(details, "release_date")),
            Overview = GetString(details, "overview"),
            PosterRemoteUrl = BuildPosterUrl(GetString(details, "poster_path")),
            GenresText = string.Join(" / ", EnumerateArrayStrings(details, "genres", "name")),
            Country = string.Join(" / ", EnumerateArrayStrings(details, "production_countries", "name")),
            Language = string.Join(" / ", EnumerateArrayStrings(details, "spoken_languages", "english_name")),
            RuntimeMinutes = GetInt(details, "runtime"),
            ImdbId = GetString(details, "imdb_id"),
            TmdbRating = GetDouble(details, "vote_average"),
            TmdbVoteCount = GetInt(details, "vote_count")
        };
    }

    private static TmdbMovieDiscoveryPage CreateEmptyDiscoveryPage(int page, string resultMessage = "")
    {
        return new TmdbMovieDiscoveryPage
        {
            Results = [],
            Page = Math.Max(1, page),
            TotalPages = 0,
            TotalResults = 0,
            ResultMessage = resultMessage
        };
    }

    private static TmdbPersonSearchPage CreateEmptyPersonSearchPage(int page, string resultMessage = "")
    {
        return new TmdbPersonSearchPage
        {
            Results = [],
            Page = Math.Max(1, page),
            TotalPages = 0,
            TotalResults = 0,
            ResultMessage = resultMessage
        };
    }

    private static TmdbPersonSearchPage BuildPersonSearchPage(JsonElement root, int fallbackPage)
    {
        var results = new List<TmdbPersonSearchItem>();
        if (root.TryGetProperty("results", out var resultArray) && resultArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultArray.EnumerateArray())
            {
                var personId = GetInt(item, "id");
                if (personId is not > 0)
                {
                    continue;
                }

                results.Add(
                    new TmdbPersonSearchItem
                    {
                        TmdbId = personId.Value,
                        Name = GetString(item, "name"),
                        OriginalName = GetString(item, "original_name"),
                        Popularity = GetDouble(item, "popularity") ?? 0d
                    });
            }
        }

        return new TmdbPersonSearchPage
        {
            Results = results,
            Page = GetInt(root, "page") ?? fallbackPage,
            TotalPages = GetInt(root, "total_pages") ?? 0,
            TotalResults = GetInt(root, "total_results") ?? results.Count
        };
    }

    private static TmdbMovieDiscoveryPage BuildDiscoveryPage(JsonElement root, int fallbackPage)
    {
        var results = new List<TmdbMovieDiscoveryItem>();
        if (root.TryGetProperty("results", out var resultArray) && resultArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultArray.EnumerateArray())
            {
                if (BuildDiscoveryItem(item) is { } movie)
                {
                    results.Add(movie);
                }
            }
        }

        return new TmdbMovieDiscoveryPage
        {
            Results = results,
            Page = GetInt(root, "page") ?? fallbackPage,
            TotalPages = GetInt(root, "total_pages") ?? 0,
            TotalResults = GetInt(root, "total_results") ?? results.Count
        };
    }

    private static TmdbTvSeriesSearchPage CreateEmptyTvSeriesPage(int page, string resultMessage = "")
    {
        return new TmdbTvSeriesSearchPage
        {
            Results = [],
            Page = Math.Max(1, page),
            TotalPages = 0,
            TotalResults = 0,
            ResultMessage = resultMessage
        };
    }

    private static TmdbTvSeriesSearchPage BuildTvSeriesPage(JsonElement root, int fallbackPage)
    {
        var results = new List<TmdbTvSeriesSearchItem>();
        if (root.TryGetProperty("results", out var resultArray) && resultArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultArray.EnumerateArray())
            {
                if (BuildTvSeriesSearchItem(item) is { } series)
                {
                    results.Add(series);
                }
            }
        }

        return new TmdbTvSeriesSearchPage
        {
            Results = results,
            Page = GetInt(root, "page") ?? fallbackPage,
            TotalPages = GetInt(root, "total_pages") ?? 0,
            TotalResults = GetInt(root, "total_results") ?? results.Count
        };
    }

    private static TmdbTvSeriesSearchItem? BuildTvSeriesSearchItem(JsonElement item)
    {
        var tmdbId = GetInt(item, "id");
        if (tmdbId is not > 0)
        {
            return null;
        }

        var firstAirDate = GetString(item, "first_air_date");
        return new TmdbTvSeriesSearchItem
        {
            TmdbId = tmdbId.Value,
            Name = GetString(item, "name"),
            OriginalName = GetString(item, "original_name"),
            Overview = GetString(item, "overview"),
            PosterRemoteUrl = BuildPosterUrl(GetString(item, "poster_path")),
            BackdropRemoteUrl = BuildImageUrl(GetString(item, "backdrop_path")),
            FirstAirDate = firstAirDate,
            FirstAirYear = ParseYear(firstAirDate),
            GenreIds = EnumerateIntArray(item, "genre_ids").ToList(),
            OriginalLanguage = GetString(item, "original_language"),
            OriginCountries = EnumerateStringArray(item, "origin_country").ToList(),
            TmdbRating = GetDouble(item, "vote_average"),
            TmdbVoteCount = GetInt(item, "vote_count"),
            Popularity = GetDouble(item, "popularity")
        };
    }

    private static TmdbTvSeriesDetailResult? BuildTvSeriesDetail(JsonElement root)
    {
        var tmdbId = GetInt(root, "id");
        if (tmdbId is not > 0)
        {
            return null;
        }

        var firstAirDate = GetString(root, "first_air_date");
        return new TmdbTvSeriesDetailResult
        {
            TmdbId = tmdbId.Value,
            Name = GetString(root, "name"),
            OriginalName = GetString(root, "original_name"),
            Overview = GetString(root, "overview"),
            PosterRemoteUrl = BuildPosterUrl(GetString(root, "poster_path")),
            BackdropRemoteUrl = BuildImageUrl(GetString(root, "backdrop_path")),
            FirstAirDate = firstAirDate,
            FirstAirYear = ParseYear(firstAirDate),
            GenresText = string.Join(" / ", EnumerateArrayStrings(root, "genres", "name")),
            OriginalLanguage = GetString(root, "original_language"),
            OriginCountries = EnumerateStringArray(root, "origin_country").ToList(),
            NumberOfSeasons = GetInt(root, "number_of_seasons"),
            NumberOfEpisodes = GetInt(root, "number_of_episodes"),
            TmdbRating = GetDouble(root, "vote_average"),
            TmdbVoteCount = GetInt(root, "vote_count"),
            Seasons = BuildTvSeasonSummaries(root)
        };
    }

    private static IReadOnlyList<TmdbTvSeasonSummaryItem> BuildTvSeasonSummaries(JsonElement root)
    {
        var seasons = new List<TmdbTvSeasonSummaryItem>();
        if (!root.TryGetProperty("seasons", out var seasonsArray) || seasonsArray.ValueKind != JsonValueKind.Array)
        {
            return seasons;
        }

        foreach (var item in seasonsArray.EnumerateArray())
        {
            seasons.Add(
                new TmdbTvSeasonSummaryItem
                {
                    TmdbId = GetInt(item, "id"),
                    SeasonNumber = GetInt(item, "season_number") ?? 0,
                    Name = GetString(item, "name"),
                    Overview = GetString(item, "overview"),
                    PosterRemoteUrl = BuildPosterUrl(GetString(item, "poster_path")),
                    AirDate = GetString(item, "air_date"),
                    EpisodeCount = GetInt(item, "episode_count"),
                    TmdbRating = GetDouble(item, "vote_average")
                });
        }

        return seasons;
    }

    private static TmdbTvSeasonDetailResult? BuildTvSeasonDetail(int seriesTmdbId, JsonElement root)
    {
        var tmdbId = GetInt(root, "id");
        if (tmdbId is not > 0)
        {
            return null;
        }

        var episodes = BuildTvEpisodeMetadataItems(root);
        return new TmdbTvSeasonDetailResult
        {
            SeriesTmdbId = seriesTmdbId,
            TmdbId = tmdbId.Value,
            SeasonNumber = GetInt(root, "season_number") ?? 0,
            Name = GetString(root, "name"),
            Overview = GetString(root, "overview"),
            PosterRemoteUrl = BuildPosterUrl(GetString(root, "poster_path")),
            AirDate = GetString(root, "air_date"),
            EpisodeCount = episodes.Count,
            TmdbRating = GetDouble(root, "vote_average"),
            TmdbVoteCount = GetInt(root, "vote_count"),
            Episodes = episodes
        };
    }

    private static IReadOnlyList<TmdbTvEpisodeMetadataItem> BuildTvEpisodeMetadataItems(JsonElement root)
    {
        var episodes = new List<TmdbTvEpisodeMetadataItem>();
        if (!root.TryGetProperty("episodes", out var episodesArray) || episodesArray.ValueKind != JsonValueKind.Array)
        {
            return episodes;
        }

        foreach (var item in episodesArray.EnumerateArray())
        {
            var tmdbId = GetInt(item, "id");
            if (tmdbId is not > 0)
            {
                continue;
            }

            episodes.Add(
                new TmdbTvEpisodeMetadataItem
                {
                    TmdbId = tmdbId.Value,
                    SeasonNumber = GetInt(item, "season_number") ?? 0,
                    EpisodeNumber = GetInt(item, "episode_number") ?? 0,
                    Name = GetString(item, "name"),
                    Overview = GetString(item, "overview"),
                    StillRemoteUrl = BuildImageUrl(GetString(item, "still_path")),
                    AirDate = GetString(item, "air_date"),
                    RuntimeMinutes = GetInt(item, "runtime"),
                    TmdbRating = GetDouble(item, "vote_average"),
                    TmdbVoteCount = GetInt(item, "vote_count")
                });
        }

        return episodes;
    }

    private static TmdbTvSeriesExternalIdsResult BuildTvExternalIds(JsonElement root)
    {
        return new TmdbTvSeriesExternalIdsResult
        {
            TmdbId = GetInt(root, "id") ?? 0,
            ImdbId = GetString(root, "imdb_id"),
            TvdbId = GetInt(root, "tvdb_id"),
            WikidataId = GetString(root, "wikidata_id"),
            FacebookId = GetString(root, "facebook_id"),
            InstagramId = GetString(root, "instagram_id"),
            TwitterId = GetString(root, "twitter_id")
        };
    }

    private static TmdbMovieDiscoveryItem? BuildDiscoveryItem(JsonElement item)
    {
        var tmdbId = GetInt(item, "id");
        if (tmdbId is not > 0)
        {
            return null;
        }

        var releaseDate = GetString(item, "release_date");
        return new TmdbMovieDiscoveryItem
        {
            TmdbId = tmdbId.Value,
            Title = GetString(item, "title"),
            OriginalTitle = GetString(item, "original_title"),
            ReleaseDate = releaseDate,
            ReleaseYear = ParseYear(releaseDate),
            Overview = GetString(item, "overview"),
            PosterRemoteUrl = BuildPosterUrl(GetString(item, "poster_path")),
            GenreIds = EnumerateIntArray(item, "genre_ids").ToList(),
            GenresText = string.Join(" / ", EnumerateArrayStrings(item, "genres", "name")),
            Country = string.Join(" / ", EnumerateArrayStrings(item, "production_countries", "name")),
            Language = string.Join(" / ", EnumerateArrayStrings(item, "spoken_languages", "english_name")),
            RuntimeMinutes = GetInt(item, "runtime"),
            ImdbId = GetString(item, "imdb_id"),
            OriginalLanguage = GetString(item, "original_language"),
            OriginCountries = EnumerateStringArray(item, "origin_country").ToList(),
            TmdbRating = GetDouble(item, "vote_average"),
            TmdbVoteCount = GetInt(item, "vote_count"),
            Popularity = GetDouble(item, "popularity")
        };
    }

    private static IReadOnlyList<TmdbMovieDiscoveryItem> BuildPersonMovieCredits(JsonElement root)
    {
        var candidates = new List<PersonCreditCandidate>();
        AddPersonCredits(root, "cast", rolePriority: 0, candidates);
        AddPersonCredits(root, "crew", rolePriority: 1, candidates);

        return candidates
            .GroupBy(candidate => candidate.Movie.TmdbId)
            .Select(
                group => group
                    .OrderBy(candidate => candidate.RolePriority)
                    .ThenBy(candidate => candidate.Order)
                    .ThenByDescending(candidate => candidate.Movie.Popularity ?? 0d)
                    .ThenByDescending(candidate => candidate.Movie.TmdbVoteCount ?? 0)
                    .First())
            .OrderByDescending(candidate => candidate.Movie.Popularity ?? 0d)
            .ThenByDescending(candidate => candidate.Movie.TmdbVoteCount ?? 0)
            .ThenByDescending(candidate => candidate.Movie.ReleaseYear ?? 0)
            .Select(candidate => candidate.Movie)
            .ToList();
    }

    private static void AddPersonCredits(
        JsonElement root,
        string propertyName,
        int rolePriority,
        ICollection<PersonCreditCandidate> candidates)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (BuildDiscoveryItem(item) is not { } movie)
            {
                continue;
            }

            var order = GetInt(item, "order") ?? int.MaxValue;
            if (rolePriority > 0)
            {
                var job = GetString(item, "job");
                order = IsPrimaryCrewJob(job) ? 0 : 50;
            }

            candidates.Add(new PersonCreditCandidate(movie, rolePriority, order));
        }
    }

    private static bool IsPrimaryCrewJob(string job)
    {
        return string.Equals(job, "Director", StringComparison.OrdinalIgnoreCase)
               || string.Equals(job, "Writer", StringComparison.OrdinalIgnoreCase)
               || string.Equals(job, "Screenplay", StringComparison.OrdinalIgnoreCase)
               || string.Equals(job, "Story", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<MetadataSearchCandidate> CloneCandidates(IEnumerable<MetadataSearchCandidate> candidates)
    {
        return candidates.Select(CloneCandidate).ToList();
    }

    private static MetadataSearchCandidate CloneCandidate(MetadataSearchCandidate candidate)
    {
        return new MetadataSearchCandidate
        {
            TmdbId = candidate.TmdbId,
            Title = candidate.Title,
            OriginalTitle = candidate.OriginalTitle,
            ReleaseYear = candidate.ReleaseYear,
            Overview = candidate.Overview,
            PosterRemoteUrl = candidate.PosterRemoteUrl,
            GenresText = candidate.GenresText,
            Country = candidate.Country,
            Language = candidate.Language,
            RuntimeMinutes = candidate.RuntimeMinutes,
            ImdbId = candidate.ImdbId,
            Confidence = candidate.Confidence,
            TmdbRating = candidate.TmdbRating,
            TmdbVoteCount = candidate.TmdbVoteCount
        };
    }

    private static TmdbTvSeriesSearchPage CloneTvSeriesPage(TmdbTvSeriesSearchPage page)
    {
        return new TmdbTvSeriesSearchPage
        {
            Results = page.Results.Select(CloneTvSeriesSearchItem).ToList(),
            Page = page.Page,
            TotalPages = page.TotalPages,
            TotalResults = page.TotalResults,
            ResultMessage = page.ResultMessage
        };
    }

    private static TmdbTvSeriesSearchItem CloneTvSeriesSearchItem(TmdbTvSeriesSearchItem item)
    {
        return new TmdbTvSeriesSearchItem
        {
            TmdbId = item.TmdbId,
            Name = item.Name,
            OriginalName = item.OriginalName,
            Overview = item.Overview,
            PosterRemoteUrl = item.PosterRemoteUrl,
            BackdropRemoteUrl = item.BackdropRemoteUrl,
            FirstAirDate = item.FirstAirDate,
            FirstAirYear = item.FirstAirYear,
            GenreIds = item.GenreIds.ToList(),
            OriginalLanguage = item.OriginalLanguage,
            OriginCountries = item.OriginCountries.ToList(),
            TmdbRating = item.TmdbRating,
            TmdbVoteCount = item.TmdbVoteCount,
            Popularity = item.Popularity
        };
    }

    private static TmdbTvSeriesDetailResult CloneTvSeriesDetail(TmdbTvSeriesDetailResult detail)
    {
        return new TmdbTvSeriesDetailResult
        {
            TmdbId = detail.TmdbId,
            Name = detail.Name,
            OriginalName = detail.OriginalName,
            Overview = detail.Overview,
            PosterRemoteUrl = detail.PosterRemoteUrl,
            BackdropRemoteUrl = detail.BackdropRemoteUrl,
            FirstAirDate = detail.FirstAirDate,
            FirstAirYear = detail.FirstAirYear,
            GenresText = detail.GenresText,
            OriginalLanguage = detail.OriginalLanguage,
            OriginCountries = detail.OriginCountries.ToList(),
            NumberOfSeasons = detail.NumberOfSeasons,
            NumberOfEpisodes = detail.NumberOfEpisodes,
            TmdbRating = detail.TmdbRating,
            TmdbVoteCount = detail.TmdbVoteCount,
            Seasons = detail.Seasons.Select(CloneTvSeasonSummary).ToList()
        };
    }

    private static TmdbTvSeasonSummaryItem CloneTvSeasonSummary(TmdbTvSeasonSummaryItem item)
    {
        return new TmdbTvSeasonSummaryItem
        {
            TmdbId = item.TmdbId,
            SeasonNumber = item.SeasonNumber,
            Name = item.Name,
            Overview = item.Overview,
            PosterRemoteUrl = item.PosterRemoteUrl,
            AirDate = item.AirDate,
            EpisodeCount = item.EpisodeCount,
            TmdbRating = item.TmdbRating
        };
    }

    private static TmdbTvSeasonDetailResult CloneTvSeasonDetail(TmdbTvSeasonDetailResult detail)
    {
        return new TmdbTvSeasonDetailResult
        {
            SeriesTmdbId = detail.SeriesTmdbId,
            TmdbId = detail.TmdbId,
            SeasonNumber = detail.SeasonNumber,
            Name = detail.Name,
            Overview = detail.Overview,
            PosterRemoteUrl = detail.PosterRemoteUrl,
            AirDate = detail.AirDate,
            EpisodeCount = detail.EpisodeCount,
            TmdbRating = detail.TmdbRating,
            TmdbVoteCount = detail.TmdbVoteCount,
            Episodes = detail.Episodes.Select(CloneTvEpisodeMetadataItem).ToList()
        };
    }

    private static TmdbTvEpisodeMetadataItem CloneTvEpisodeMetadataItem(TmdbTvEpisodeMetadataItem item)
    {
        return new TmdbTvEpisodeMetadataItem
        {
            TmdbId = item.TmdbId,
            SeasonNumber = item.SeasonNumber,
            EpisodeNumber = item.EpisodeNumber,
            Name = item.Name,
            Overview = item.Overview,
            StillRemoteUrl = item.StillRemoteUrl,
            AirDate = item.AirDate,
            RuntimeMinutes = item.RuntimeMinutes,
            TmdbRating = item.TmdbRating,
            TmdbVoteCount = item.TmdbVoteCount
        };
    }

    private static TmdbTvSeriesExternalIdsResult CloneTvExternalIds(TmdbTvSeriesExternalIdsResult item)
    {
        return new TmdbTvSeriesExternalIdsResult
        {
            TmdbId = item.TmdbId,
            ImdbId = item.ImdbId,
            TvdbId = item.TvdbId,
            WikidataId = item.WikidataId,
            FacebookId = item.FacebookId,
            InstagramId = item.InstagramId,
            TwitterId = item.TwitterId
        };
    }

    private static string BuildPosterUrl(string posterPath)
    {
        return BuildImageUrl(posterPath);
    }

    private static string BuildImageUrl(string imagePath)
    {
        return string.IsNullOrWhiteSpace(imagePath)
            ? string.Empty
            : $"{ImageBaseUrl}{imagePath}";
    }

    private static IEnumerable<string> EnumerateArrayStrings(JsonElement element, string arrayPropertyName, string itemPropertyName)
    {
        if (!element.TryGetProperty(arrayPropertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            var value = GetString(item, itemPropertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<int> EnumerateIntArray(JsonElement element, string arrayPropertyName)
    {
        if (!element.TryGetProperty(arrayPropertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> EnumerateStringArray(JsonElement element, string arrayPropertyName)
    {
        if (!element.TryGetProperty(arrayPropertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : item.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.ToString();
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return int.TryParse(property.ToString(), out value) ? value : null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
        {
            return value;
        }

        return double.TryParse(property.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            ? value
            : null;
    }

    private static int? ParseYear(string releaseDate)
    {
        if (DateTime.TryParse(releaseDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.Year;
        }

        return null;
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

    private sealed record CacheEntry<T>(T Value, DateTime ExpiresAtUtc);

    private sealed record TmdbExternalIdsPersistentPayload(string ImdbId);

    private sealed record PersonCreditCandidate(TmdbMovieDiscoveryItem Movie, int RolePriority, int Order);

    private sealed record TmdbRequestOptions(string ApiBaseUrl, string ReadAccessToken, string ApiKey)
    {
        public bool UseBearerToken => !string.IsNullOrWhiteSpace(ReadAccessToken);

        public bool HasCredential => UseBearerToken || !string.IsNullOrWhiteSpace(ApiKey);

        public string AuthFingerprint => UseBearerToken
            ? BuildCredentialFingerprint("bearer", ReadAccessToken)
            : BuildCredentialFingerprint("api-key", ApiKey);
    }
}
