using System.Collections.ObjectModel;
using MediaLibrary.App.Helpers;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class TmdbTvSeriesCorrectionSeriesGroup
{
    public TmdbTvSeriesCorrectionSeriesGroup(
        TmdbTvSeriesSearchItem searchItem,
        TmdbTvSeriesDetailResult? details)
    {
        TmdbSeriesId = searchItem.TmdbId;
        SeriesTitle = FirstNonEmpty(details?.Name, searchItem.Name, $"TV {searchItem.TmdbId}");
        OriginalSeriesTitle = FirstNonEmpty(details?.OriginalName, searchItem.OriginalName);
        FirstAirDate = details?.FirstAirDate ?? searchItem.FirstAirDate;
        FirstAirYear = details?.FirstAirYear ?? searchItem.FirstAirYear;
        Overview = FirstNonEmpty(details?.Overview, searchItem.Overview);
        DirectorText = FirstNonEmpty(details?.DirectorText, "-");
        GenresText = TmdbTvGenreMapper.NormalizeGenreNames(
            FirstNonEmpty(details?.GenresText, TmdbTvGenreMapper.MapGenreIds(searchItem.GenreIds)));
        CountryText = MovieMetadataDisplayText.LocalizeCountries(details?.OriginCountries.Count > 0
            ? string.Join(" / ", details.OriginCountries)
            : FirstNonEmpty(searchItem.OriginCountries.Count > 0 ? string.Join(" / ", searchItem.OriginCountries) : string.Empty));
        LanguageText = MovieMetadataDisplayText.LocalizeLanguages(
            FirstNonEmpty(details?.OriginalLanguage, searchItem.OriginalLanguage));
        SeasonCount = details?.NumberOfSeasons;
        EpisodeCount = details?.NumberOfEpisodes;

        foreach (var season in (details?.Seasons ?? [])
                     .Where(x => x.SeasonNumber >= 0)
                     .OrderBy(x => x.SeasonNumber)
                     .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Seasons.Add(new TmdbTvSeasonCorrectionSeasonItem(this, season));
        }

        HeaderSubtitle = BuildHeaderSubtitle();
    }

    public int TmdbSeriesId { get; }

    public string SeriesTitle { get; }

    public string OriginalSeriesTitle { get; }

    public string FirstAirDate { get; }

    public int? FirstAirYear { get; }

    public string Overview { get; }

    public string GenresText { get; }

    public string DirectorText { get; }

    public string CountryText { get; }

    public string LanguageText { get; }

    public int? SeasonCount { get; }

    public int? EpisodeCount { get; }

    public string HeaderSubtitle { get; }

    public ObservableCollection<TmdbTvSeasonCorrectionSeasonItem> Seasons { get; } = [];

    public bool HasSeasons => Seasons.Count > 0;

    public string DisplayTitle => FirstAirYear.HasValue ? $"{SeriesTitle} ({FirstAirYear.Value})" : SeriesTitle;

    public string OriginalSeriesTitleDisplayText =>
        string.IsNullOrWhiteSpace(OriginalSeriesTitle)
        || string.Equals(OriginalSeriesTitle, SeriesTitle, StringComparison.CurrentCultureIgnoreCase)
            ? string.Empty
            : OriginalSeriesTitle;

    public string FirstAirDateDisplayText => string.IsNullOrWhiteSpace(FirstAirDate) ? "-" : FirstAirDate;

    public string SeasonCountText => SeasonCount is > 0 ? $"共 {SeasonCount.Value} 季" : "季数未知";

    public string GenresDisplayText => string.IsNullOrWhiteSpace(GenresText) ? "-" : GenresText;

    public string CountryDisplayText => string.IsNullOrWhiteSpace(CountryText) ? "-" : CountryText;

    public string LanguageDisplayText => string.IsNullOrWhiteSpace(LanguageText) ? "-" : LanguageText;

    private string BuildHeaderSubtitle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(OriginalSeriesTitle)
            && !string.Equals(OriginalSeriesTitle.Trim(), SeriesTitle, StringComparison.CurrentCultureIgnoreCase))
        {
            parts.Add(OriginalSeriesTitle.Trim());
        }

        if (SeasonCount is > 0)
        {
            parts.Add($"{SeasonCount.Value} seasons");
        }

        if (EpisodeCount is > 0)
        {
            parts.Add($"{EpisodeCount.Value} episodes");
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(Overview))
        {
            parts.Add(Overview.Length > 80 ? Overview[..80] + "..." : Overview);
        }

        return parts.Count == 0 ? "TMDB TV series" : string.Join(" | ", parts);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}

public sealed class TmdbTvSeasonCorrectionSeasonItem
{
    public TmdbTvSeasonCorrectionSeasonItem(
        TmdbTvSeriesCorrectionSeriesGroup series,
        TmdbTvSeasonSummaryItem season)
    {
        Series = series;
        TmdbSeriesId = series.TmdbSeriesId;
        SeriesTitle = series.SeriesTitle;
        SeasonNumber = season.SeasonNumber;
        SeasonTitle = string.IsNullOrWhiteSpace(season.Name)
            ? (season.SeasonNumber == 0 ? "Specials" : $"Season {season.SeasonNumber}")
            : season.Name.Trim();
        EpisodeCount = season.EpisodeCount;
        AirDate = season.AirDate;
    }

    public TmdbTvSeriesCorrectionSeriesGroup Series { get; }

    public int TmdbSeriesId { get; }

    public string SeriesTitle { get; }

    public int SeasonNumber { get; }

    public string SeasonTitle { get; }

    public int? EpisodeCount { get; }

    public string AirDate { get; }

    public string DisplayTitle => $"S{SeasonNumber:D2} {SeasonTitle}";

    public string SeriesAndSeasonTitle => $"{SeriesTitle}  {(SeasonNumber == 0 ? "特别篇" : $"第 {SeasonNumber} 季")}";

    public string EpisodeCountText => EpisodeCount is > 0 ? $"共 {EpisodeCount.Value} 集" : "集数未知";

    public string AirDateDisplayText => string.IsNullOrWhiteSpace(AirDate) ? "-" : AirDate;

    public string DisplaySubtitle
    {
        get
        {
            var parts = new List<string> { $"S{SeasonNumber:D2}" };
            if (EpisodeCount is > 0)
            {
                parts.Add($"{EpisodeCount.Value} episodes");
            }

            if (!string.IsNullOrWhiteSpace(AirDate))
            {
                parts.Add(AirDate);
            }

            return string.Join(" | ", parts);
        }
    }
}
