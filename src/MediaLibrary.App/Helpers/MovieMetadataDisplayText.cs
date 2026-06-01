namespace MediaLibrary.App.Helpers;

public static class MovieMetadataDisplayText
{
    private static readonly IReadOnlyDictionary<string, string> CountryNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CN"] = "中国大陆",
            ["China"] = "中国大陆",
            ["Hong Kong"] = "中国香港",
            ["HK"] = "中国香港",
            ["Taiwan"] = "中国台湾",
            ["TW"] = "中国台湾",
            ["United States of America"] = "美国",
            ["United States"] = "美国",
            ["US"] = "美国",
            ["United Kingdom"] = "英国",
            ["GB"] = "英国",
            ["Japan"] = "日本",
            ["JP"] = "日本",
            ["South Korea"] = "韩国",
            ["Korea, Republic of"] = "韩国",
            ["KR"] = "韩国",
            ["France"] = "法国",
            ["FR"] = "法国",
            ["Germany"] = "德国",
            ["DE"] = "德国",
            ["Italy"] = "意大利",
            ["IT"] = "意大利",
            ["Spain"] = "西班牙",
            ["ES"] = "西班牙",
            ["Canada"] = "加拿大",
            ["CA"] = "加拿大",
            ["Australia"] = "澳大利亚",
            ["AU"] = "澳大利亚",
            ["India"] = "印度",
            ["IN"] = "印度",
            ["Thailand"] = "泰国",
            ["TH"] = "泰国",
            ["Russia"] = "俄罗斯",
            ["Russian Federation"] = "俄罗斯",
            ["RU"] = "俄罗斯",
            ["Brazil"] = "巴西",
            ["BR"] = "巴西",
            ["Mexico"] = "墨西哥",
            ["MX"] = "墨西哥",
            ["Netherlands"] = "荷兰",
            ["NL"] = "荷兰",
            ["Sweden"] = "瑞典",
            ["SE"] = "瑞典",
            ["Denmark"] = "丹麦",
            ["DK"] = "丹麦",
            ["Norway"] = "挪威",
            ["NO"] = "挪威",
            ["Finland"] = "芬兰",
            ["FI"] = "芬兰",
            ["Poland"] = "波兰",
            ["PL"] = "波兰",
            ["Belgium"] = "比利时",
            ["BE"] = "比利时",
            ["Switzerland"] = "瑞士",
            ["CH"] = "瑞士",
            ["Austria"] = "奥地利",
            ["AT"] = "奥地利",
            ["Ireland"] = "爱尔兰",
            ["IE"] = "爱尔兰",
            ["New Zealand"] = "新西兰",
            ["NZ"] = "新西兰",
            ["Singapore"] = "新加坡",
            ["SG"] = "新加坡",
            ["Malaysia"] = "马来西亚",
            ["MY"] = "马来西亚",
            ["Indonesia"] = "印度尼西亚",
            ["ID"] = "印度尼西亚",
            ["Philippines"] = "菲律宾",
            ["PH"] = "菲律宾",
            ["Vietnam"] = "越南",
            ["VN"] = "越南"
        };

    private static readonly IReadOnlyDictionary<string, string> LanguageNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh"] = "中文",
            ["Chinese"] = "中文",
            ["Mandarin"] = "普通话",
            ["Cantonese"] = "粤语",
            ["en"] = "英语",
            ["English"] = "英语",
            ["ja"] = "日语",
            ["Japanese"] = "日语",
            ["ko"] = "韩语",
            ["Korean"] = "韩语",
            ["fr"] = "法语",
            ["French"] = "法语",
            ["de"] = "德语",
            ["German"] = "德语",
            ["es"] = "西班牙语",
            ["Spanish"] = "西班牙语",
            ["it"] = "意大利语",
            ["Italian"] = "意大利语",
            ["pt"] = "葡萄牙语",
            ["Portuguese"] = "葡萄牙语",
            ["ru"] = "俄语",
            ["Russian"] = "俄语",
            ["hi"] = "印地语",
            ["Hindi"] = "印地语",
            ["th"] = "泰语",
            ["Thai"] = "泰语",
            ["vi"] = "越南语",
            ["Vietnamese"] = "越南语",
            ["id"] = "印度尼西亚语",
            ["Indonesian"] = "印度尼西亚语",
            ["ms"] = "马来语",
            ["Malay"] = "马来语",
            ["ar"] = "阿拉伯语",
            ["Arabic"] = "阿拉伯语",
            ["tr"] = "土耳其语",
            ["Turkish"] = "土耳其语",
            ["pl"] = "波兰语",
            ["Polish"] = "波兰语",
            ["nl"] = "荷兰语",
            ["Dutch"] = "荷兰语",
            ["sv"] = "瑞典语",
            ["Swedish"] = "瑞典语",
            ["da"] = "丹麦语",
            ["Danish"] = "丹麦语",
            ["no"] = "挪威语",
            ["Norwegian"] = "挪威语",
            ["fi"] = "芬兰语",
            ["Finnish"] = "芬兰语",
            ["cs"] = "捷克语",
            ["Czech"] = "捷克语",
            ["el"] = "希腊语",
            ["Greek"] = "希腊语",
            ["he"] = "希伯来语",
            ["Hebrew"] = "希伯来语"
        };

    public static string LocalizeCountries(string? value)
    {
        return LocalizeList(value, CountryNames);
    }

    public static string LocalizeLanguages(string? value)
    {
        return LocalizeList(value, LanguageNames);
    }

    public static string LocalizeTvProductionStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Trim() switch
        {
            "Returning Series" => "播出中",
            "Ended" => "已完结",
            "Canceled" => "已取消",
            "In Production" => "制作中",
            "Planned" => "计划中",
            "Pilot" => "试播",
            _ => value.Trim()
        };
    }

    private static string LocalizeList(string? value, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return string.Join(
            " / ",
            value.Split(['/', ',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(item => names.TryGetValue(item, out var localized) ? localized : item)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
