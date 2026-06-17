namespace MediaLibrary.Core.Services.Implementations;

internal static class AiTagVocabulary
{
    public const int MinTagsPerCategory = 2;
    public const int MaxTagsPerCategory = 4;
    public const string MissingTagPlaceholder = "-";

    public static readonly string[] TypeTags =
    [
        "动作", "冒险", "动画", "喜剧", "犯罪", "纪录片", "剧情", "家庭", "奇幻", "历史",
        "恐怖", "音乐", "悬疑", "爱情", "科幻", "电视电影", "惊悚", "战争", "西部", "传记",
        "运动", "歌舞", "灾难", "武侠", "古装"
    ];

    public static readonly string[] EmotionTags =
    [
        "治愈", "温暖", "感动", "轻松", "欢乐", "浪漫", "热血", "紧张", "好奇", "压抑",
        "沉重", "震撼", "孤独", "荒诞", "黑色幽默", "催泪", "励志", "思考向", "爽感", "不安",
        "梦幻", "怀旧", "燃", "克制", "讽刺", "黑暗", "温柔"
    ];

    public static readonly string[] SceneTags =
    [
        "独自观看", "情侣", "朋友", "亲子", "家人", "深夜", "解压", "下饭", "周末", "聚会",
        "高专注", "背景播放", "二刷", "影院感", "通勤", "短时观看", "长片沉浸", "节日", "雨天", "睡前"
    ];

    private static readonly Dictionary<string, string> ContextualAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["悬疑"] = "好奇",
        ["烧脑"] = "好奇",
        ["悬念"] = "好奇",
        ["惊悚"] = "不安",
        ["恐惧"] = "不安",
        ["紧绷"] = "不安",
        ["放松"] = "解压",
        ["放松解压"] = "解压",
        ["片荒补完"] = "解压"
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "动作",
        ["Adventure"] = "冒险",
        ["Animation"] = "动画",
        ["Comedy"] = "喜剧",
        ["Crime"] = "犯罪",
        ["Documentary"] = "纪录片",
        ["纪录"] = "纪录片",
        ["Drama"] = "剧情",
        ["Family"] = "家庭",
        ["Fantasy"] = "奇幻",
        ["History"] = "历史",
        ["Horror"] = "恐怖",
        ["Music"] = "音乐",
        ["Mystery"] = "悬疑",
        ["Romance"] = "爱情",
        ["Science Fiction"] = "科幻",
        ["Sci-Fi"] = "科幻",
        ["TV Movie"] = "电视电影",
        ["Thriller"] = "惊悚",
        ["War"] = "战争",
        ["Western"] = "西部",
        ["Biography"] = "传记",
        ["Biopic"] = "传记",
        ["Sport"] = "运动",
        ["Sports"] = "运动",
        ["Musical"] = "歌舞",
        ["Disaster"] = "灾难",
        ["家庭观影"] = "家人",
        ["亲子观影"] = "亲子",
        ["情侣观影"] = "情侣",
        ["朋友观影"] = "朋友",
        ["独自观影"] = "独自观看",
        ["单人观影"] = "独自观看",
        ["朋友聚会"] = "朋友",
        ["晚间观影"] = "深夜",
        ["周末观影"] = "周末",
        ["放松解压"] = "解压",
        ["下饭观影"] = "下饭",
        ["复看经典"] = "二刷",
        ["片荒补完"] = "解压",
        ["高分补课"] = "高专注",
        ["爽快"] = "爽感",
        ["幽默"] = "欢乐",
        ["搞笑"] = "欢乐",
        ["温馨"] = "温暖",
        ["沉浸"] = "思考向",
        ["烧脑"] = "好奇",
        ["悲伤"] = "沉重",
        ["伤感"] = "感动"
    };

    public static IReadOnlyList<string> Filter(
        IEnumerable<string> tags,
        IReadOnlyCollection<string> allowedTags,
        int take = MaxTagsPerCategory)
    {
        var allowed = allowedTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tags
            .Select(x => x.Trim())
            .Select(x => NormalizeTag(x, allowed))
            .Where(x => allowed.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    public static IReadOnlyList<string> PickFromText(string? text, IReadOnlyCollection<string> allowedTags, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var picked = allowedTags
            .Where(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase))
            .Take(MaxTagsPerCategory)
            .ToList();

        if (picked.Count > 0)
        {
            return picked;
        }

        var aliasPicked = ContextualAliases
            .Concat(Aliases)
            .Where(alias => text.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
            .Select(alias => alias.Value)
            .Where(tag => allowedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTagsPerCategory)
            .ToList();

        return aliasPicked.Count > 0 ? aliasPicked : fallback;
    }

    public static string NormalizeText(
        string? text,
        IReadOnlyCollection<string> allowedTags,
        IReadOnlyList<string>? fallback = null,
        int take = MaxTagsPerCategory)
    {
        if (string.Equals(text?.Trim(), MissingTagPlaceholder, StringComparison.Ordinal))
        {
            return MissingTagPlaceholder;
        }

        var tags = SplitTags(text);
        var filtered = Filter(tags, allowedTags, take);
        if (filtered.Count > 0)
        {
            return string.Join("、", filtered);
        }

        return fallback is null || fallback.Count == 0
            ? string.Empty
            : string.Join("、", Filter(fallback, allowedTags, take));
    }

    private static IEnumerable<string> SplitTags(string? text)
    {
        return (text ?? string.Empty)
            .Split(['、', ',', '，', '/', '|', ';', '；', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeTag(string tag, IReadOnlyCollection<string> allowedTags)
    {
        var trimmed = tag.Trim();
        if (allowedTags.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (ContextualAliases.TryGetValue(trimmed, out var contextual)
            && allowedTags.Contains(contextual, StringComparer.OrdinalIgnoreCase))
        {
            return contextual;
        }

        return Aliases.TryGetValue(trimmed, out var normalized)
               && allowedTags.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : trimmed;
    }
}
