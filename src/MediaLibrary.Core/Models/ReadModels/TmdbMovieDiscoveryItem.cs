namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbMovieDiscoveryItem
{
    public int TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string ReleaseDate { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public IReadOnlyList<int> GenreIds { get; set; } = [];

    public string GenresText { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public string OriginalLanguage { get; set; } = string.Empty;

    public IReadOnlyList<string> OriginCountries { get; set; } = [];

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public double? Popularity { get; set; }
}
