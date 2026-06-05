namespace MediaLibrary.Core.Services.Implementations;

public static class TmdbTvGenreMapper
{
    private static readonly IReadOnlyDictionary<int, string> GenreNames = new Dictionary<int, string>
    {
        [10759] = "动作冒险",
        [16] = "动画",
        [35] = "喜剧",
        [80] = "犯罪",
        [99] = "纪录片",
        [18] = "剧情",
        [10751] = "家庭",
        [10762] = "儿童",
        [9648] = "悬疑",
        [10763] = "新闻",
        [10764] = "真人秀",
        [10765] = "科幻奇幻",
        [10766] = "肥皂剧",
        [10767] = "脱口秀",
        [10768] = "战争政治",
        [37] = "西部"
    };

    private static readonly IReadOnlyDictionary<string, string> LocalizedGenreNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Action & Adventure"] = "动作冒险",
            ["Animation"] = "动画",
            ["Comedy"] = "喜剧",
            ["Crime"] = "犯罪",
            ["Documentary"] = "纪录片",
            ["Drama"] = "剧情",
            ["Family"] = "家庭",
            ["Kids"] = "儿童",
            ["Mystery"] = "悬疑",
            ["News"] = "新闻",
            ["Reality"] = "真人秀",
            ["Sci-Fi & Fantasy"] = "科幻奇幻",
            ["Soap"] = "肥皂剧",
            ["Talk"] = "脱口秀",
            ["War & Politics"] = "战争政治",
            ["Western"] = "西部"
        };

    public static IReadOnlyList<string> GenreLabels { get; } =
    [
        "全部",
        "动作冒险",
        "动画",
        "喜剧",
        "犯罪",
        "纪录片",
        "剧情",
        "家庭",
        "儿童",
        "悬疑",
        "新闻",
        "真人秀",
        "科幻奇幻",
        "肥皂剧",
        "脱口秀",
        "战争政治",
        "西部"
    ];

    public static string MapGenreIds(IEnumerable<int> genreIds)
    {
        var labels = genreIds
            .Select(id => GenreNames.TryGetValue(id, out var label) ? label : string.Empty)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return labels.Count == 0 ? string.Empty : string.Join("、", labels);
    }

    public static bool TryGetGenreId(string label, out int genreId)
    {
        foreach (var pair in GenreNames)
        {
            if (string.Equals(pair.Value, label, StringComparison.Ordinal))
            {
                genreId = pair.Key;
                return true;
            }
        }

        genreId = 0;
        return false;
    }

    public static string NormalizeGenreNames(string? genresText)
    {
        if (string.IsNullOrWhiteSpace(genresText))
        {
            return string.Empty;
        }

        var labels = genresText
            .Split(['、', '/', ',', '，', ';', '；', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(label => label.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => LocalizedGenreNames.TryGetValue(label, out var localized) ? localized : label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return labels.Count == 0 ? string.Empty : string.Join("、", labels);
    }
}
