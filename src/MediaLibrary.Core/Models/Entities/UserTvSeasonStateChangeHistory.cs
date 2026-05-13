namespace MediaLibrary.Core.Models.Entities;

public sealed class UserTvSeasonStateChangeHistory
{
    public long Id { get; set; }

    public int? TmdbSeriesId { get; set; }

    public int? TmdbSeasonId { get; set; }

    public int? TvSeriesId { get; set; }

    public int? TvSeasonId { get; set; }

    public int? UserTvSeasonCollectionItemId { get; set; }

    public int SeasonNumber { get; set; } = 1;

    public string? SeriesTitle { get; set; }

    public string? SeasonTitle { get; set; }

    public string StateType { get; set; } = string.Empty;

    public bool OldValue { get; set; }

    public bool NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
