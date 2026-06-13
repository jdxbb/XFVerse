using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Helpers;

public static class ExternalMovieTagCache
{
    private const int CacheDocumentVersion = 1;
    private const string CacheDirectoryName = "ExternalMovieTags";
    private const string CacheFileName = "external-movie-ai-tags.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<string, AiMovieTags> TagsByKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, ExternalMovieTagCacheEntry> EntriesByStorageKey = new(StringComparer.OrdinalIgnoreCase);
    private static bool _isLoaded;

    public static void Set(AiRecommendationItem recommendation)
    {
        EnsureLoaded();
        if (recommendation.TmdbId is not > 0
            && string.IsNullOrWhiteSpace(recommendation.ImdbId)
            && string.IsNullOrWhiteSpace(recommendation.Title))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(recommendation.Tags)
            && string.IsNullOrWhiteSpace(recommendation.EmotionTagsText)
            && string.IsNullOrWhiteSpace(recommendation.SceneTagsText))
        {
            return;
        }

        var tags = new AiMovieTags
        {
            AiTagsText = recommendation.Tags,
            EmotionTagsText = recommendation.EmotionTagsText,
            SceneTagsText = recommendation.SceneTagsText
        };

        var entry = new ExternalMovieTagCacheEntry
        {
            TmdbId = recommendation.TmdbId,
            ImdbId = recommendation.ImdbId,
            Title = recommendation.Title,
            ReleaseYear = recommendation.ReleaseYear,
            AiTagsText = tags.AiTagsText,
            EmotionTagsText = tags.EmotionTagsText,
            SceneTagsText = tags.SceneTagsText,
            UpdatedAtUtc = DateTime.UtcNow
        };
        EntriesByStorageKey[BuildStorageKey(recommendation.TmdbId, recommendation.ImdbId, recommendation.Title, recommendation.ReleaseYear)] = entry;

        foreach (var key in BuildKeys(recommendation.TmdbId, recommendation.ImdbId, recommendation.Title, recommendation.ReleaseYear))
        {
            TagsByKey[key] = tags;
        }

        Save();
    }

    public static bool TryGet(int? tmdbId, string? imdbId, string? title, int? releaseYear, out AiMovieTags tags)
    {
        EnsureLoaded();
        foreach (var key in BuildKeys(tmdbId, imdbId, title, releaseYear))
        {
            if (TagsByKey.TryGetValue(key, out tags!))
            {
                return true;
            }
        }

        tags = new AiMovieTags();
        return false;
    }

    private static void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_isLoaded)
            {
                return;
            }

            try
            {
                var path = GetCacheFilePath();
                if (File.Exists(path))
                {
                    var document = JsonSerializer.Deserialize<ExternalMovieTagCacheDocument>(
                        File.ReadAllText(path),
                        JsonOptions);
                    foreach (var entry in document?.Entries ?? [])
                    {
                        if (!HasAnyIdentity(entry) || !HasAnyTags(entry))
                        {
                            continue;
                        }

                        var tags = ToTags(entry);
                        EntriesByStorageKey[BuildStorageKey(entry.TmdbId, entry.ImdbId, entry.Title, entry.ReleaseYear)] = entry;
                        foreach (var key in BuildKeys(entry.TmdbId, entry.ImdbId, entry.Title, entry.ReleaseYear))
                        {
                            TagsByKey[key] = tags;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                AiPerfDiagnostics.WriteEvent(
                    $"event=external-ai-tag-cache-load status=failed error={AiPerfDiagnostics.SanitizeMessage(exception.Message)}");
            }
            finally
            {
                _isLoaded = true;
            }
        }
    }

    private static void Save()
    {
        lock (SyncRoot)
        {
            try
            {
                var path = GetCacheFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var document = new ExternalMovieTagCacheDocument
                {
                    Version = CacheDocumentVersion,
                    Entries = EntriesByStorageKey.Values
                        .OrderByDescending(entry => entry.UpdatedAtUtc)
                        .ToList()
                };
                File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
            }
            catch (Exception exception)
            {
                AiPerfDiagnostics.WriteEvent(
                    $"event=external-ai-tag-cache-save status=failed error={AiPerfDiagnostics.SanitizeMessage(exception.Message)}");
            }
        }
    }

    private static string GetCacheFilePath()
    {
        return Path.Combine(AppPaths.GetAppDataDirectory(), CacheDirectoryName, CacheFileName);
    }

    private static AiMovieTags ToTags(ExternalMovieTagCacheEntry entry)
    {
        return new AiMovieTags
        {
            AiTagsText = entry.AiTagsText,
            EmotionTagsText = entry.EmotionTagsText,
            SceneTagsText = entry.SceneTagsText
        };
    }

    private static bool HasAnyIdentity(ExternalMovieTagCacheEntry entry)
    {
        return entry.TmdbId is > 0
               || !string.IsNullOrWhiteSpace(entry.ImdbId)
               || !string.IsNullOrWhiteSpace(entry.Title);
    }

    private static bool HasAnyTags(ExternalMovieTagCacheEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.AiTagsText)
               || !string.IsNullOrWhiteSpace(entry.EmotionTagsText)
               || !string.IsNullOrWhiteSpace(entry.SceneTagsText);
    }

    private static IEnumerable<string> BuildKeys(int? tmdbId, string? imdbId, string? title, int? releaseYear)
    {
        if (tmdbId is > 0)
        {
            yield return $"tmdb:{tmdbId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            yield return $"imdb:{imdbId.Trim().ToLowerInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            yield return $"title:{title.Trim().ToLowerInvariant()}:{releaseYear?.ToString() ?? string.Empty}";
        }
    }

    private static string BuildStorageKey(int? tmdbId, string? imdbId, string? title, int? releaseYear)
    {
        if (tmdbId is > 0)
        {
            return $"tmdb:{tmdbId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            return $"imdb:{imdbId.Trim().ToLowerInvariant()}";
        }

        return $"title:{title?.Trim().ToLowerInvariant() ?? string.Empty}:{releaseYear?.ToString() ?? string.Empty}";
    }

    private sealed class ExternalMovieTagCacheDocument
    {
        public int Version { get; set; } = CacheDocumentVersion;

        public List<ExternalMovieTagCacheEntry> Entries { get; set; } = [];
    }

    private sealed class ExternalMovieTagCacheEntry
    {
        public int? TmdbId { get; set; }

        public string ImdbId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int? ReleaseYear { get; set; }

        public string AiTagsText { get; set; } = string.Empty;

        public string EmotionTagsText { get; set; } = string.Empty;

        public string SceneTagsText { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; }
    }
}
