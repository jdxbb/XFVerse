namespace MediaLibrary.Core.Models.Entities;

public sealed class WatchHistory
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public int MediaFileId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public int LastPlayPositionSeconds { get; set; }

    public int DurationWatchedSeconds { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Movie? Movie { get; set; }

    public MediaFile? MediaFile { get; set; }
}
