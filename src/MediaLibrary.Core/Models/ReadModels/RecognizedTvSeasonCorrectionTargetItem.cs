namespace MediaLibrary.Core.Models.ReadModels;

public sealed class RecognizedTvSeasonCorrectionTargetItem
{
    public int SeriesId { get; set; }

    public int TmdbSeriesId { get; set; }

    public string SeriesTitle { get; set; } = string.Empty;

    public string OriginalSeriesTitle { get; set; } = string.Empty;

    public int? FirstAirYear { get; set; }

    public int SeasonId { get; set; }

    public int SeasonNumber { get; set; }

    public string SeasonTitle { get; set; } = string.Empty;

    public int? TmdbSeasonId { get; set; }

    public int? EpisodeCount { get; set; }

    public DateTime? AirDate { get; set; }
}
