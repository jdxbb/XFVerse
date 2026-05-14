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
}
