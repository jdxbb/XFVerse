namespace MediaLibrary.Core.Models.Entities;

public sealed class UserMovieStateChangeHistory
{
    public long Id { get; set; }

    public int TmdbId { get; set; }

    public int? MovieId { get; set; }

    public int? UserMovieCollectionItemId { get; set; }

    public string? Title { get; set; }

    public string StateType { get; set; } = string.Empty;

    public bool OldValue { get; set; }

    public bool NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
