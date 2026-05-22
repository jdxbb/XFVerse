using System.Collections.ObjectModel;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class UnknownTvSeasonCorrectionSeriesGroup
{
    public UnknownTvSeasonCorrectionSeriesGroup(
        string seriesTitle,
        IEnumerable<UnknownTvSeasonCorrectionTargetItem> seasons)
    {
        SeriesTitle = string.IsNullOrWhiteSpace(seriesTitle) ? "-" : seriesTitle.Trim();
        foreach (var season in seasons
                     .OrderBy(x => x.SeasonNumber)
                     .ThenBy(x => x.SeasonTitle, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(x => x.SeasonId))
        {
            Seasons.Add(season);
        }

        SourceCount = Seasons.Sum(x => x.SourceCount);
        HeaderSubtitle = $"{Seasons.Count} seasons | {SourceCount} sources | {BuildRangeSummary(Seasons)}";
    }

    public string SeriesTitle { get; }

    public int SourceCount { get; }

    public string HeaderSubtitle { get; }

    public ObservableCollection<UnknownTvSeasonCorrectionTargetItem> Seasons { get; } = [];

    public static IReadOnlyList<UnknownTvSeasonCorrectionSeriesGroup> FromTargets(
        IEnumerable<UnknownTvSeasonCorrectionTargetItem> targets)
    {
        return targets
            .GroupBy(x => string.IsNullOrWhiteSpace(x.SeriesTitle) ? "-" : x.SeriesTitle.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new UnknownTvSeasonCorrectionSeriesGroup(x.Key, x))
            .ToList();
    }

    private static string BuildRangeSummary(IEnumerable<UnknownTvSeasonCorrectionTargetItem> seasons)
    {
        var ranges = seasons
            .Select(x => x.EpisodeRangeText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(3)
            .ToArray();
        return ranges.Length == 0 ? "episodes:-" : string.Join(" / ", ranges);
    }
}
