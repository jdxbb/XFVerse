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

    public string DirectorText { get; set; } = string.Empty;

    public string WriterText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public string ProductionStatus { get; set; } = string.Empty;

    public string NetworksText { get; set; } = string.Empty;

    public string ProductionCompaniesText { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string SourceSummary { get; set; } = string.Empty;

    public int TotalSeasonCount { get; set; }

    public int InLibrarySeasonCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public IReadOnlyList<TvSeriesSeasonListItem> Seasons { get; set; } = [];

    public string FirstAirDateText => FirstAirDate.HasValue
        ? FirstAirDate.Value.ToString("yyyy-MM-dd")
        : FirstAirYear?.ToString() ?? "-";

    public string SeasonCountText => $"{InLibrarySeasonCount} / {TotalSeasonCount} 季有播放源";

    public string EpisodeCountText => $"共 {TotalEpisodeCount} 集";
}

public sealed class TvSeriesSeasonListItem
{
    public int SeasonId { get; set; }

    public int SeasonNumber { get; set; }

    public int? TmdbSeasonId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string PosterLocalPath { get; set; } = string.Empty;

    public string PosterDisplayUrl => string.IsNullOrWhiteSpace(PosterRemoteUrl) ? PosterLocalPath : PosterRemoteUrl;

    public DateTime? AirDate { get; set; }

    public int? AirYear { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public int InLibraryEpisodeCount { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public string SourceSummary { get; set; } = string.Empty;

    public IdentificationStatus IdentificationStatus { get; set; }

    public bool HasPlayableSource => InLibraryEpisodeCount > 0;

    public string SeasonNumberText => SeasonNumber == 0 ? "特别篇" : $"S{SeasonNumber:D2}";

    public string SeasonDisplayText => SeasonNumber == 0 ? "特别篇" : $"第{SeasonNumber}季";

    public string SeasonNameDisplayText => TvDetailDisplayText.ShouldHideSeasonName(SeasonDisplayText, Name)
        ? UnidentifiedSeasonSuffix
        : $"{Name}{UnidentifiedSeasonSuffix}";

    public bool IsUnidentified => IdentificationStatus == IdentificationStatus.Failed || !TmdbSeasonId.HasValue;

    private string UnidentifiedSeasonSuffix => IsUnidentified ? "（未识别）" : string.Empty;

    public string ProgressText => $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount}";

    public string InLibraryText => HasPlayableSource ? $"有播放源 {InLibraryEpisodeCount} 集" : "暂无播放源";

    public string EpisodeCountText => $"共 {TotalEpisodeCount} 集";

    public string SourceCountText => HasPlayableSource
        ? $"{InLibraryEpisodeCount} / {TotalEpisodeCount} 集有播放源"
        : "无播放源";

    public string WatchedSummaryText => WatchedEpisodeCount > 0
        ? $"已看 {WatchedEpisodeCount} / {TotalEpisodeCount} 集"
        : "未看";

    public string AirDateText => AirDate.HasValue
        ? AirDate.Value.ToString("yyyy-MM-dd")
        : AirYear?.ToString() ?? "-";

    public string IdentificationStatusText => TvDetailDisplayText.FormatIdentificationStatus(IdentificationStatus);
}
