namespace MediaLibrary.Core.Models.ReadModels;

public sealed class IdentificationRunResult
{
    private readonly List<string> _messages = [];

    public int AttemptedCount { get; set; }

    public int BoundCount { get; set; }

    public int PlaceholderCount { get; set; }

    public int ErrorCount { get; private set; }

    public int WarningCount { get; private set; }

    public IReadOnlyList<string> Messages => _messages;

    public bool HasIssues => ErrorCount > 0 || WarningCount > 0;

    public void AddError(string stage, string message)
    {
        ErrorCount++;
        AddMessage(stage, message);
    }

    public void AddWarning(string stage, string message)
    {
        WarningCount++;
        AddMessage(stage, message);
    }

    public string BuildSummary()
    {
        if (!HasIssues)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (ErrorCount > 0)
        {
            parts.Add($"错误 {ErrorCount}");
        }

        if (WarningCount > 0)
        {
            parts.Add($"警告 {WarningCount}");
        }

        var detail = _messages.Count > 0 ? $"：{string.Join("；", _messages)}" : string.Empty;
        return $"识别/元数据阶段{string.Join("，", parts)}{detail}";
    }

    private void AddMessage(string stage, string message)
    {
        if (_messages.Count >= 5)
        {
            return;
        }

        var normalized = $"[{stage}] {message}";
        if (_messages.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _messages.Add(normalized);
    }
}
