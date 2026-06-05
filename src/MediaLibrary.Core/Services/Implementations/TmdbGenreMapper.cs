namespace MediaLibrary.Core.Services.Implementations;

public static class TmdbGenreMapper
{
    private static readonly IReadOnlyDictionary<int, string> GenreNames = new Dictionary<int, string>
    {
        [28] = "动作",
        [12] = "冒险",
        [16] = "动画",
        [35] = "喜剧",
        [80] = "犯罪",
        [99] = "纪录片",
        [18] = "剧情",
        [10751] = "家庭",
        [14] = "奇幻",
        [36] = "历史",
        [27] = "恐怖",
        [10402] = "音乐",
        [9648] = "悬疑",
        [10749] = "爱情",
        [878] = "科幻",
        [10770] = "电视电影",
        [53] = "惊悚",
        [10752] = "战争",
        [37] = "西部"
    };

    public static IReadOnlyList<string> GenreLabels { get; } =
    [
        "全部",
        "动作",
        "冒险",
        "动画",
        "喜剧",
        "犯罪",
        "纪录片",
        "剧情",
        "家庭",
        "奇幻",
        "历史",
        "恐怖",
        "音乐",
        "悬疑",
        "爱情",
        "科幻",
        "电视电影",
        "惊悚",
        "战争",
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
}
