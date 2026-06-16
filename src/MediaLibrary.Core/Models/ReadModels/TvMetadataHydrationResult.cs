namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvMetadataHydrationResult
{
    private readonly List<string> _errors = [];

    public int TmdbSeriesId { get; set; }

    public int? TvSeriesId { get; set; }

    public bool Skipped { get; set; }

    public int AddedSeasonCount { get; set; }

    public int UpdatedSeasonCount { get; set; }

    public int AddedEpisodeCount { get; set; }

    public int UpdatedEpisodeCount { get; set; }

    public bool SeriesChanged { get; set; }

    public IReadOnlyList<string> Errors => _errors;

    public bool HasErrors => _errors.Count > 0;

    public bool HasMetadata => TvSeriesId.HasValue;

    public bool Success => HasMetadata && !HasErrors;

    public bool PartialSuccess => HasMetadata && HasErrors;

    public bool HasChanges => SeriesChanged
                              || AddedSeasonCount > 0
                              || UpdatedSeasonCount > 0
                              || AddedEpisodeCount > 0
                              || UpdatedEpisodeCount > 0;

    public void AddError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_errors.Count >= 5)
        {
            return;
        }

        var normalized = message.Trim();
        if (!_errors.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            _errors.Add(normalized);
        }
    }

    public string BuildStatusMessage()
    {
        if (Skipped)
        {
            return "本轮已尝试补齐该剧 metadata，已复用本地结果。";
        }

        var summary = $"新增 {AddedSeasonCount} 季 / {AddedEpisodeCount} 集，更新 {UpdatedSeasonCount} 季 / {UpdatedEpisodeCount} 集";
        if (!HasErrors)
        {
            return summary;
        }

        return $"{summary}；部分 metadata 未补齐：{string.Join("；", Errors)}";
    }
}
