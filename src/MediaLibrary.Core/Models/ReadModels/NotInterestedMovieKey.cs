namespace MediaLibrary.Core.Models.ReadModels;

public sealed class NotInterestedMovieKey
{
    public int? MovieId { get; set; }

    public int? TmdbId { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string GenresText { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
