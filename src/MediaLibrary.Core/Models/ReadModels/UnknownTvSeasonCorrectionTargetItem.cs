namespace MediaLibrary.Core.Models.ReadModels;

public sealed class UnknownTvSeasonCorrectionTargetItem
{
    public int SeasonId { get; set; }

    public string SeriesTitle { get; set; } = string.Empty;

    public string SeasonTitle { get; set; } = string.Empty;

    public int SeasonNumber { get; set; }

    public string EpisodeRangeText { get; set; } = string.Empty;

    public int SourceCount { get; set; }

    public string SourceKindSummary { get; set; } = string.Empty;

    public string ContextHint { get; set; } = string.Empty;

    public string DisplayTitle => string.Join(" / ", new[] { SeriesTitle, SeasonTitle }
        .Where(x => !string.IsNullOrWhiteSpace(x)));

    public string DisplaySubtitle =>
        $"S{SeasonNumber:00} · {EpisodeRangeText} · {SourceCount} sources · {SourceKindSummary} · {ContextHint}";

    public string SeriesAndSeasonTitle => $"{SeriesTitle}  {(SeasonNumber == 0 ? "特别篇" : $"第 {SeasonNumber} 季")}";

    public string SourceCountText => $"共 {SourceCount} 个播放源";
}
