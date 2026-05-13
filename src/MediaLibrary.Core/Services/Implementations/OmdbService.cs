using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class OmdbService : IOmdbService
{
    private const string OmdbProvider = "OMDb";
    private const string OmdbRatingCacheType = "Rating";
    internal const int HttpConcurrencyLimit = 2;
    private const int RatingCacheLimit = 600;
    private static readonly TimeSpan RatingCacheTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan RatingPersistentCacheTtl = TimeSpan.FromDays(14);
    private static readonly ConcurrentDictionary<string, CacheEntry<MovieRatingItem>> RatingCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CacheEntry<OmdbSeasonRatingAuditResult>> SeasonRatingAuditCache = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim OmdbHttpLimiter = new(HttpConcurrencyLimit, HttpConcurrencyLimit);
    private static int OmdbHttpInFlight;

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://www.omdbapi.com/", UriKind.Absolute),
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly ISettingsService _settingsService;

    public OmdbService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<MovieRatingItem?> GetRatingAsync(
        string imdbId,
        CancellationToken cancellationToken = default)
    {
        return await GetRatingCoreAsync(
            imdbId,
            type: string.Empty,
            cacheScope: "rating",
            diagnosticsName: "omdb",
            sourceName: "OMDb",
            cancellationToken);
    }

    public async Task<MovieRatingItem?> GetSeriesRatingAsync(
        string imdbId,
        CancellationToken cancellationToken = default)
    {
        return await GetRatingCoreAsync(
            imdbId,
            type: "series",
            cacheScope: "series-rating",
            diagnosticsName: "omdb-series",
            sourceName: "IMDb",
            cancellationToken);
    }

    public async Task<OmdbSeasonRatingAuditResult> GetSeasonRatingAuditAsync(
        string imdbId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imdbId) || seasonNumber <= 0)
        {
            return new OmdbSeasonRatingAuditResult
            {
                ImdbId = imdbId.Trim(),
                SeasonNumber = seasonNumber,
                ResultMessage = "IMDb id or season number is invalid."
            };
        }

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.OmdbApiKey))
        {
            return new OmdbSeasonRatingAuditResult
            {
                ImdbId = imdbId.Trim(),
                SeasonNumber = seasonNumber,
                ResultMessage = "OMDb API key is not configured."
            };
        }

        var trimmedImdbId = imdbId.Trim();
        var cacheKey = BuildOmdbSeasonRatingAuditCacheKey(trimmedImdbId, seasonNumber, settings.OmdbApiKey.Trim());
        if (TryGetCacheValue(SeasonRatingAuditCache, cacheKey, out var cachedAudit))
        {
            AiPerfDiagnostics.RecordExternalCall("omdb-season-audit-cache-hit", TimeSpan.Zero, false);
            return CloneSeasonAudit(cachedAudit);
        }

        var persistentAudit = await ExternalMetadataPersistentCache.TryGetAsync<OmdbSeasonRatingAuditResult>(
            OmdbProvider,
            OmdbRatingCacheType,
            cacheKey,
            cancellationToken);
        if (persistentAudit.IsHit && persistentAudit.Value is not null)
        {
            AiPerfDiagnostics.RecordExternalCall("omdb-season-audit-persistent-cache-hit", TimeSpan.Zero, false);
            var cloned = CloneSeasonAudit(persistentAudit.Value);
            SetCacheValue(SeasonRatingAuditCache, cacheKey, CloneSeasonAudit(cloned), RatingCacheTtl, RatingCacheLimit);
            return cloned;
        }

        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            using var response = await SendGetAsync(
                $"?i={Uri.EscapeDataString(trimmedImdbId)}&Season={seasonNumber}&apikey={Uri.EscapeDataString(settings.OmdbApiKey)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                isError = true;
                return new OmdbSeasonRatingAuditResult
                {
                    ImdbId = trimmedImdbId,
                    SeasonNumber = seasonNumber,
                    ResultMessage = "OMDb request failed."
                };
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var audit = BuildSeasonRatingAudit(trimmedImdbId, seasonNumber, document.RootElement);
            if (!cancellationToken.IsCancellationRequested)
            {
                var cacheValue = CloneSeasonAudit(audit);
                SetCacheValue(SeasonRatingAuditCache, cacheKey, cacheValue, RatingCacheTtl, RatingCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    OmdbProvider,
                    OmdbRatingCacheType,
                    cacheKey,
                    cacheValue,
                    RatingPersistentCacheTtl,
                    cancellationToken);
            }

            return audit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            isError = true;
            return new OmdbSeasonRatingAuditResult
            {
                ImdbId = trimmedImdbId,
                SeasonNumber = seasonNumber,
                ResultMessage = "OMDb season rating audit failed."
            };
        }
        finally
        {
            requestStopwatch.Stop();
            AiPerfDiagnostics.RecordExternalCall("omdb-season-audit", requestStopwatch.Elapsed, isError);
        }
    }

    private async Task<MovieRatingItem?> GetRatingCoreAsync(
        string imdbId,
        string type,
        string cacheScope,
        string diagnosticsName,
        string sourceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return null;
        }

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.OmdbApiKey))
        {
            return null;
        }

        var trimmedImdbId = imdbId.Trim();
        var cacheKey = BuildOmdbRatingCacheKey(trimmedImdbId, settings.OmdbApiKey.Trim(), cacheScope);
        cancellationToken.ThrowIfCancellationRequested();
        if (TryGetCachedRating(cacheKey, out var cachedRating))
        {
            AiPerfDiagnostics.RecordExternalCall($"{diagnosticsName}-cache-hit", TimeSpan.Zero, false);
            return cachedRating;
        }

        AiPerfDiagnostics.RecordExternalCall($"{diagnosticsName}-cache-miss", TimeSpan.Zero, false);
        var persistentRating = await ExternalMetadataPersistentCache.TryGetAsync<MovieRatingItem>(
            OmdbProvider,
            OmdbRatingCacheType,
            cacheKey,
            cancellationToken);
        if (persistentRating.IsHit && persistentRating.Value is not null)
        {
            AiPerfDiagnostics.RecordExternalCall($"{diagnosticsName}-persistent-cache-hit", TimeSpan.Zero, false);
            var clonedPersistentRating = CloneRating(persistentRating.Value);
            SetCacheValue(RatingCache, cacheKey, CloneRating(clonedPersistentRating), RatingCacheTtl, RatingCacheLimit);
            return clonedPersistentRating;
        }

        AiPerfDiagnostics.RecordExternalCall($"{diagnosticsName}-persistent-cache-miss", TimeSpan.Zero, false);
        var requestStopwatch = Stopwatch.StartNew();
        var isError = false;
        try
        {
            var requestUri = $"?i={Uri.EscapeDataString(trimmedImdbId)}&apikey={Uri.EscapeDataString(settings.OmdbApiKey)}";
            if (!string.IsNullOrWhiteSpace(type))
            {
                requestUri += $"&type={Uri.EscapeDataString(type)}";
            }

            using var response = await SendGetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                isError = true;
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!string.Equals(GetString(root, "Response"), "True", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var imdbRating = GetString(root, "imdbRating");
            if (!double.TryParse(imdbRating, NumberStyles.Any, CultureInfo.InvariantCulture, out var scoreValue))
            {
                return null;
            }

            var voteCount = ParseVoteCount(GetString(root, "imdbVotes"));

            var rating = new MovieRatingItem
            {
                SourceName = sourceName,
                ScoreValue = scoreValue,
                ScoreScale = 10d,
                VoteCount = voteCount,
                SourceUrl = $"https://www.imdb.com/title/{trimmedImdbId}/",
                LastUpdatedAt = DateTime.UtcNow
            };

            if (!cancellationToken.IsCancellationRequested)
            {
                var ratingCacheValue = CloneRating(rating);
                SetCacheValue(RatingCache, cacheKey, ratingCacheValue, RatingCacheTtl, RatingCacheLimit);
                await ExternalMetadataPersistentCache.SetAsync(
                    OmdbProvider,
                    OmdbRatingCacheType,
                    cacheKey,
                    ratingCacheValue,
                    RatingPersistentCacheTtl,
                    cancellationToken);
            }

            return rating;
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

    private async Task<HttpResponseMessage> SendGetAsync(string requestUri, CancellationToken cancellationToken)
    {
        await OmdbHttpLimiter.WaitAsync(cancellationToken);
        var currentInFlight = Interlocked.Increment(ref OmdbHttpInFlight);
        AiPerfDiagnostics.RecordConcurrencySample("omdb-http", currentInFlight);
        try
        {
            return await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref OmdbHttpInFlight);
            OmdbHttpLimiter.Release();
        }
    }

    private static string BuildOmdbRatingCacheKey(string imdbId, string apiKey, string cacheScope)
    {
        return $"{cacheScope}|auth={BuildCredentialFingerprint(apiKey)}|imdb={imdbId}";
    }

    private static string BuildOmdbSeasonRatingAuditCacheKey(string imdbId, int seasonNumber, string apiKey)
    {
        return $"season-rating-audit|auth={BuildCredentialFingerprint(apiKey)}|imdb={imdbId}|season={seasonNumber}";
    }

    private static string BuildCredentialFingerprint(string credential)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
        return hash[..16];
    }

    private static bool TryGetCachedRating(string cacheKey, out MovieRatingItem rating)
    {
        if (TryGetCacheValue(RatingCache, cacheKey, out var cachedRating))
        {
            rating = CloneRating(cachedRating);
            return true;
        }

        rating = new MovieRatingItem();
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

    private static MovieRatingItem CloneRating(MovieRatingItem rating)
    {
        return new MovieRatingItem
        {
            SourceName = rating.SourceName,
            ScoreValue = rating.ScoreValue,
            ScoreScale = rating.ScoreScale,
            VoteCount = rating.VoteCount,
            SourceUrl = rating.SourceUrl,
            LastUpdatedAt = rating.LastUpdatedAt
        };
    }

    private static OmdbSeasonRatingAuditResult BuildSeasonRatingAudit(
        string imdbId,
        int seasonNumber,
        JsonElement root)
    {
        if (!string.Equals(GetString(root, "Response"), "True", StringComparison.OrdinalIgnoreCase))
        {
            return new OmdbSeasonRatingAuditResult
            {
                ImdbId = imdbId,
                SeasonNumber = seasonNumber,
                HasResponse = false,
                ResultMessage = GetString(root, "Error")
            };
        }

        return new OmdbSeasonRatingAuditResult
        {
            ImdbId = imdbId,
            SeasonNumber = seasonNumber,
            HasResponse = true,
            SeasonRating = TryBuildRating(root, imdbId, "IMDb"),
            EpisodeRatings = BuildEpisodeRatingAuditItems(root),
            ResultMessage = "OMDb returned a season response; season-level rating availability depends on imdbRating presence."
        };
    }

    private static IReadOnlyList<OmdbEpisodeRatingAuditItem> BuildEpisodeRatingAuditItems(JsonElement root)
    {
        var episodes = new List<OmdbEpisodeRatingAuditItem>();
        if (!root.TryGetProperty("Episodes", out var episodesArray) || episodesArray.ValueKind != JsonValueKind.Array)
        {
            return episodes;
        }

        foreach (var item in episodesArray.EnumerateArray())
        {
            var episodeImdbId = GetString(item, "imdbID");
            episodes.Add(
                new OmdbEpisodeRatingAuditItem
                {
                    EpisodeNumber = GetInt(item, "Episode"),
                    Title = GetString(item, "Title"),
                    ImdbId = episodeImdbId,
                    Rating = TryBuildRating(item, episodeImdbId, "IMDb")
                });
        }

        return episodes;
    }

    private static MovieRatingItem? TryBuildRating(JsonElement element, string imdbId, string sourceName)
    {
        var imdbRating = GetString(element, "imdbRating");
        if (!double.TryParse(imdbRating, NumberStyles.Any, CultureInfo.InvariantCulture, out var scoreValue))
        {
            return null;
        }

        return new MovieRatingItem
        {
            SourceName = sourceName,
            ScoreValue = scoreValue,
            ScoreScale = 10d,
            VoteCount = ParseVoteCount(GetString(element, "imdbVotes")),
            SourceUrl = string.IsNullOrWhiteSpace(imdbId) ? string.Empty : $"https://www.imdb.com/title/{imdbId}/",
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    private static OmdbSeasonRatingAuditResult CloneSeasonAudit(OmdbSeasonRatingAuditResult audit)
    {
        return new OmdbSeasonRatingAuditResult
        {
            ImdbId = audit.ImdbId,
            SeasonNumber = audit.SeasonNumber,
            HasResponse = audit.HasResponse,
            SeasonRating = audit.SeasonRating is null ? null : CloneRating(audit.SeasonRating),
            EpisodeRatings = audit.EpisodeRatings.Select(CloneEpisodeAudit).ToList(),
            ResultMessage = audit.ResultMessage
        };
    }

    private static OmdbEpisodeRatingAuditItem CloneEpisodeAudit(OmdbEpisodeRatingAuditItem item)
    {
        return new OmdbEpisodeRatingAuditItem
        {
            EpisodeNumber = item.EpisodeNumber,
            Title = item.Title,
            ImdbId = item.ImdbId,
            Rating = item.Rating is null ? null : CloneRating(item.Rating)
        };
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

    private static int? ParseVoteCount(string rawVotes)
    {
        if (string.IsNullOrWhiteSpace(rawVotes))
        {
            return null;
        }

        var normalized = rawVotes.Replace(",", string.Empty, StringComparison.Ordinal);
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
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

        return int.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? value
            : null;
    }

    private sealed record CacheEntry<T>(T Value, DateTime ExpiresAtUtc);
}
