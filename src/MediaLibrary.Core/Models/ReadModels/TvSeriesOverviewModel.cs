using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TvSeriesOverviewModel
{
    public int SeriesId { get; set; }

    public int? TmdbSeriesId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string PosterLocalPath { get; set; } = string.Empty;

    public string PosterDisplayUrl { get; set; } = string.Empty;

    public DateTime? FirstAirDate { get; set; }

    public int? FirstAirYear { get; set; }

    public string GenresText { get; set; } = string.Empty;

    public string SourceSummary { get; set; } = string.Empty;

    public int TotalSeasonCount { get; set; }

    public int InLibrarySeasonCount { get; set; }

    public IReadOnlyList<TvSeriesSeasonListItem> Seasons { get; set; } = [];

    public string FirstAirDateText => FirstAirDate.HasValue
        ? FirstAirDate.Value.ToString("yyyy-MM-dd")
        : FirstAirYear?.ToString() ?? "-";

    public string SeasonCountText => $"{InLibrarySeasonCount} / {TotalSeasonCount} 季";
}

public sealed class TvSeriesSeasonListItem
{
    public int SeasonId { get; set; }

    public int SeasonNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string PosterLocalPath { get; set; } = string.Empty;

    public DateTime? AirDate { get; set; }

    public int? AirYear { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public int InLibraryEpisodeCount { get; set; }

    public string SourceSummary { get; set; } = string.Empty;

    public IdentificationStatus IdentificationStatus { get; set; }

    public string SeasonNumberText => $"S{SeasonNumber:D2}";

    public string ProgressText => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount}";

    public string InLibraryText => $"已入库 {InLibraryEpisodeCount} 集";

    public string AirDateText => AirDate.HasValue
        ? AirDate.Value.ToString("yyyy-MM-dd")
        : AirYear?.ToString() ?? "-";

    public string IdentificationStatusText => TvDetailDisplayText.FormatIdentificationStatus(IdentificationStatus);
}
