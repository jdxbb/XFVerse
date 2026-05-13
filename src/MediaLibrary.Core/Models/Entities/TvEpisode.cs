namespace MediaLibrary.Core.Models.Entities;

public sealed class TvEpisode
{
    public int Id { get; set; }

    public int TvSeasonId { get; set; }

    public int? TmdbEpisodeId { get; set; }

    public int EpisodeNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Overview { get; set; }

    public string? StillLocalPath { get; set; }

    public string? StillRemoteUrl { get; set; }

    public DateTime? AirDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public bool IsWatched { get; set; }

    public DateTime? LastPlayedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public int DurationWatchedSeconds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TvSeason? Season { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();
}
