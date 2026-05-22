using System.Collections.ObjectModel;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class RecognizedTvSeasonCorrectionSeriesGroup
{
    public RecognizedTvSeasonCorrectionSeriesGroup(
        string seriesTitle,
        IEnumerable<RecognizedTvSeasonCorrectionTargetItem> seasons)
    {
        var orderedSeasons = seasons
            .OrderBy(x => x.SeasonNumber)
            .ThenBy(x => x.SeasonTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SeasonId)
            .ToList();
        var first = orderedSeasons.First();

        SeriesId = first.SeriesId;
        TmdbSeriesId = first.TmdbSeriesId;
        SeriesTitle = string.IsNullOrWhiteSpace(seriesTitle) ? "-" : seriesTitle.Trim();
        OriginalSeriesTitle = first.OriginalSeriesTitle ?? string.Empty;
        FirstAirYear = first.FirstAirYear;

        foreach (var season in orderedSeasons)
        {
            Seasons.Add(new RecognizedTvSeasonCorrectionSeasonItem(season));
        }

        HeaderSubtitle = BuildHeaderSubtitle();
    }

    public int SeriesId { get; }

    public int TmdbSeriesId { get; }

    public string SeriesTitle { get; }

    public string OriginalSeriesTitle { get; }

    public int? FirstAirYear { get; }

    public string HeaderSubtitle { get; }

    public ObservableCollection<RecognizedTvSeasonCorrectionSeasonItem> Seasons { get; } = [];

    public bool HasSeasons => Seasons.Count > 0;

    public string DisplayTitle => FirstAirYear.HasValue ? $"{SeriesTitle} ({FirstAirYear.Value})" : SeriesTitle;

    public static IReadOnlyList<RecognizedTvSeasonCorrectionSeriesGroup> FromTargets(
        IEnumerable<RecognizedTvSeasonCorrectionTargetItem> targets)
    {
        return targets
            .Where(x => x.TmdbSeriesId > 0 && x.SeasonNumber > 0)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.SeriesTitle) ? "-" : x.SeriesTitle.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new RecognizedTvSeasonCorrectionSeriesGroup(x.Key, x))
            .Where(x => x.HasSeasons)
            .ToList();
    }

    private string BuildHeaderSubtitle()
    {
        var parts = new List<string>
        {
            $"{Seasons.Count} 季"
        };
        if (!string.IsNullOrWhiteSpace(OriginalSeriesTitle)
            && !string.Equals(OriginalSeriesTitle.Trim(), SeriesTitle, StringComparison.CurrentCultureIgnoreCase))
        {
            parts.Add(OriginalSeriesTitle.Trim());
        }

        if (FirstAirYear.HasValue)
        {
            parts.Add(FirstAirYear.Value.ToString());
        }

        return string.Join(" · ", parts);
    }
}

public sealed class RecognizedTvSeasonCorrectionSeasonItem
{
    public RecognizedTvSeasonCorrectionSeasonItem(RecognizedTvSeasonCorrectionTargetItem target)
    {
        SeriesId = target.SeriesId;
        TmdbSeriesId = target.TmdbSeriesId;
        SeriesTitle = string.IsNullOrWhiteSpace(target.SeriesTitle) ? $"TV {target.TmdbSeriesId}" : target.SeriesTitle.Trim();
        OriginalSeriesTitle = target.OriginalSeriesTitle ?? string.Empty;
        FirstAirYear = target.FirstAirYear;
        SeasonId = target.SeasonId;
        SeasonNumber = target.SeasonNumber;
        SeasonTitle = string.IsNullOrWhiteSpace(target.SeasonTitle)
            ? $"Season {target.SeasonNumber}"
            : target.SeasonTitle.Trim();
        EpisodeCount = target.EpisodeCount;
        AirDate = target.AirDate;
    }

    public int SeriesId { get; }

    public int TmdbSeriesId { get; }

    public string SeriesTitle { get; }

    public string OriginalSeriesTitle { get; }

    public int? FirstAirYear { get; }

    public int SeasonId { get; }

    public int SeasonNumber { get; }

    public string SeasonTitle { get; }

    public int? EpisodeCount { get; }

    public DateTime? AirDate { get; }

    public string DisplayTitle => $"{SeriesTitle} / S{SeasonNumber:D2} {SeasonTitle}";

    public string DisplaySubtitle
    {
        get
        {
            var parts = new List<string>
            {
                $"S{SeasonNumber:D2}"
            };
            if (EpisodeCount.HasValue && EpisodeCount.Value > 0)
            {
                parts.Add($"{EpisodeCount.Value} 集");
            }

            if (AirDate.HasValue)
            {
                parts.Add(AirDate.Value.ToString("yyyy-MM-dd"));
            }

            return string.Join(" · ", parts);
        }
    }
}
