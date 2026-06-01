namespace MediaLibrary.Core.Models.ReadModels;

public sealed class TmdbTvSeriesDetailResult
{
    public int TmdbId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string BackdropRemoteUrl { get; set; } = string.Empty;

    public string FirstAirDate { get; set; } = string.Empty;

    public int? FirstAirYear { get; set; }

    public string GenresText { get; set; } = string.Empty;

    public string DirectorText { get; set; } = string.Empty;

    public string WriterText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public string ProductionStatus { get; set; } = string.Empty;

    public string NetworksText { get; set; } = string.Empty;

    public string ProductionCompaniesText { get; set; } = string.Empty;

    public string OriginalLanguage { get; set; } = string.Empty;

    public IReadOnlyList<string> OriginCountries { get; set; } = [];

    public int? NumberOfSeasons { get; set; }

    public int? NumberOfEpisodes { get; set; }

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public IReadOnlyList<TmdbTvSeasonSummaryItem> Seasons { get; set; } = [];
}
