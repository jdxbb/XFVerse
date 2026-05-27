using System.Collections.Concurrent;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Helpers;

public static class ExternalMovieTagCache
{
    private static readonly ConcurrentDictionary<string, AiMovieTags> TagsByKey = new(StringComparer.OrdinalIgnoreCase);

    public static void Set(AiRecommendationItem recommendation)
    {
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

        foreach (var key in BuildKeys(recommendation.TmdbId, recommendation.ImdbId, recommendation.Title, recommendation.ReleaseYear))
        {
            TagsByKey[key] = tags;
        }
    }

    public static bool TryGet(int? tmdbId, string? imdbId, string? title, int? releaseYear, out AiMovieTags tags)
    {
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
}
