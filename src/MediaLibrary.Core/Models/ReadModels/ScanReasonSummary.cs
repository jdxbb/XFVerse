using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ScanReasonSummary
{
    public int Version { get; set; } = 1;

    public List<ScanReasonSummaryEntry> Entries { get; set; } = [];
}

public sealed class ScanReasonSummaryEntry
{
    public string Category { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }
}

public sealed class ScanReasonSummaryBuilder
{
    private readonly Dictionary<string, ScanReasonSummaryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void AddSuccess(string key, string label, int count)
    {
        Add("success", key, label, count);
    }

    public void AddSkipped(string key, string label, int count)
    {
        Add("skipped", key, label, count);
    }

    public void AddWarning(string key, string label, int count)
    {
        Add("warning", key, label, count);
    }

    public void AddCancelled(string key, string label, int count)
    {
        Add("cancelled", key, label, count);
    }

    public void AddError(string key, string label, int count)
    {
        Add("error", key, label, count);
    }

    public string ToJson()
    {
        var summary = new ScanReasonSummary
        {
            Entries = _entries.Values
                .Where(x => x.Count > 0)
                .OrderBy(x => CategoryOrder(x.Category))
                .ThenByDescending(x => x.Count)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        return summary.Entries.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(summary, ScanReasonSummaryJson.Options);
    }

    private void Add(string category, string key, string label, int count)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "skipped" : category.Trim();
        var normalizedKey = key.Trim();
        var entryKey = $"{normalizedCategory}:{normalizedKey}";
        if (_entries.TryGetValue(entryKey, out var existing))
        {
            existing.Count += count;
            return;
        }

        _entries[entryKey] = new ScanReasonSummaryEntry
        {
            Category = normalizedCategory,
            Key = normalizedKey,
            Label = string.IsNullOrWhiteSpace(label) ? normalizedKey : label.Trim(),
            Count = count
        };
    }

    private static int CategoryOrder(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "success" => 0,
            "skipped" => 1,
            "cancelled" => 2,
            "warning" => 3,
            "error" => 4,
            _ => 5
        };
    }
}

public static class ScanReasonSummaryFormatter
{
    public static string FormatTotals(string? json)
    {
        var summary = Parse(json);
        if (summary is null || summary.Entries.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        AddCategoryTotal(parts, summary, "success", "成功");
        AddCategoryTotal(parts, summary, "skipped", "跳过");
        AddCategoryTotal(parts, summary, "cancelled", "已取消");
        AddCategoryTotal(parts, summary, "warning", "警告");
        AddCategoryTotal(parts, summary, "error", "错误");
        return parts.Count == 0 ? string.Empty : $"原因摘要：{string.Join("、", parts)}";
    }

    public static string FormatTopReasons(string? json, int maxCount = 3)
    {
        var summary = Parse(json);
        if (summary is null || summary.Entries.Count == 0)
        {
            return string.Empty;
        }

        var reasons = summary.Entries
            .Where(x => x.Count > 0 && !string.Equals(x.Category, "success", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxCount))
            .Select(x => $"{x.Label} {x.Count}")
            .ToList();
        return reasons.Count == 0 ? string.Empty : $"主要原因：{string.Join("、", reasons)}";
    }

    public static ScanReasonSummary? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScanReasonSummary>(json, ScanReasonSummaryJson.Options);
        }
        catch
        {
            return null;
        }
    }

    private static void AddCategoryTotal(
        ICollection<string> parts,
        ScanReasonSummary summary,
        string category,
        string label)
    {
        var count = summary.Entries
            .Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);
        if (count > 0)
        {
            parts.Add($"{label} {count}");
        }
    }
}

internal static class ScanReasonSummaryJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
