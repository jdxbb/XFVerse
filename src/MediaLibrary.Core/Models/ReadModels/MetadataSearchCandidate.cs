namespace MediaLibrary.Core.Models.ReadModels;

public sealed class MetadataSearchCandidate
{
    public int TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string DirectorText { get; set; } = string.Empty;

    public string WriterText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public string ProductionCompanyText { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public bool IsCurrentMatchedMovie { get; set; }

    public string ReleaseDateText => ReleaseDate?.ToString("yyyy-MM-dd")
                                     ?? ReleaseYear?.ToString()
                                     ?? "-";

    public string RuntimeText => RuntimeMinutes is > 0
        ? $"{RuntimeMinutes.Value / 60:00}:{RuntimeMinutes.Value % 60:00}:00"
        : "-";

    public string OriginalTitleDisplayText => string.IsNullOrWhiteSpace(OriginalTitle) ? "-" : OriginalTitle;

    public string OverviewDisplayText => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public string DirectorDisplayText => string.IsNullOrWhiteSpace(DirectorText) ? "-" : DirectorText;

    public string CountryDisplayText => string.IsNullOrWhiteSpace(Country) ? "-" : Country;

    public string LanguageDisplayText => string.IsNullOrWhiteSpace(Language) ? "-" : Language;
}
