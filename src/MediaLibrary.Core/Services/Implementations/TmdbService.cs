using System.Collections.Concurrent;
using System.Globalization;
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
    private static readonly SemaphoreSlim TmdbHttpLimiter = new(HttpConcurrencyLimit, HttpConcurrencyLimit);
    private static int TmdbHttpInFlight;

    private readonly HttpClient _httpClient = new()
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

    private static string BuildCredentialFingerprint(string kind, string credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return $"{kind}:none";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
        return $"{kind}:{hash[..16]}";
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

    private static string BuildPosterUrl(string posterPath)
    {
        return string.IsNullOrWhiteSpace(posterPath)
            ? string.Empty
            : $"{ImageBaseUrl}{posterPath}";
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

    private sealed record TmdbRequestOptions(string ApiBaseUrl, string ReadAccessToken, string ApiKey)
    {
        public bool UseBearerToken => !string.IsNullOrWhiteSpace(ReadAccessToken);

        public bool HasCredential => UseBearerToken || !string.IsNullOrWhiteSpace(ApiKey);

        public string AuthFingerprint => UseBearerToken
            ? BuildCredentialFingerprint("bearer", ReadAccessToken)
            : BuildCredentialFingerprint("api-key", ApiKey);
    }
}
