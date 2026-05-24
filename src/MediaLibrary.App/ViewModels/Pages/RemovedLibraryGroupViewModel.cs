using System.Collections.ObjectModel;
using MediaLibrary.App.ViewModels.Base;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class RemovedLibraryGroupViewModel : ObservableObject
{
    private bool _isExpanded;

    private RemovedLibraryGroupViewModel(
        string key,
        string title,
        string subtitle,
        bool isTvGroup,
        IEnumerable<LibraryMovieItemViewModel> items)
    {
        Key = key;
        Title = string.IsNullOrWhiteSpace(title) ? "-" : title.Trim();
        HeaderSubtitle = subtitle;
        IsTvGroup = isTvGroup;
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    public string Key { get; }

    public string Title { get; }

    public string HeaderSubtitle { get; }

    public bool IsTvGroup { get; }

    public bool IsMovieGroup => !IsTvGroup;

    public ObservableCollection<LibraryMovieItemViewModel> Items { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public static IReadOnlyList<RemovedLibraryGroupViewModel> FromItems(
        IEnumerable<LibraryMovieItemViewModel> items)
    {
        return items
            .GroupBy(BuildGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(CreateGroup)
            .OrderByDescending(x => x.IsTvGroup)
            .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static RemovedLibraryGroupViewModel CreateGroup(
        IGrouping<string, LibraryMovieItemViewModel> group)
    {
        var orderedItems = group
            .OrderBy(x => x.IsSeason ? x.Movie.SeasonNumber : int.MaxValue)
            .ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var first = orderedItems[0];
        var isTvGroup = orderedItems.Any(IsTvItem);
        var title = isTvGroup
            ? FirstNonEmpty(first.SeriesTitle, first.Title)
            : first.Title;
        var subtitle = BuildHeaderSubtitle(orderedItems, isTvGroup);
        return new RemovedLibraryGroupViewModel(group.Key, title, subtitle, isTvGroup, orderedItems);
    }

    private static string BuildGroupKey(LibraryMovieItemViewModel item)
    {
        if (IsTvItem(item) && item.SeriesId > 0)
        {
            return $"series:{item.SeriesId}";
        }

        if (item.MovieId > 0)
        {
            return $"movie:{item.MovieId}";
        }

        if (item.TmdbId is > 0)
        {
            return $"tmdb:{item.TmdbId.Value}";
        }

        return item.SelectionKey;
    }

    private static string BuildHeaderSubtitle(
        IReadOnlyCollection<LibraryMovieItemViewModel> items,
        bool isTvGroup)
    {
        var kindText = isTvGroup ? "电视剧" : items.First().MediaKindText;
        var itemText = isTvGroup ? $"{items.Count} 季" : $"{items.Count} 项";
        var sourceText = string.Join(
            " / ",
            items
                .Select(x => x.SourceStatusText)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Take(2));
        var stateText = string.Join(
            " / ",
            items
                .Select(x => x.StateSummary)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Take(2));
        var parts = new[] { kindText, itemText, sourceText, stateText }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" · ", parts);
    }

    private static bool IsTvItem(LibraryMovieItemViewModel item)
    {
        return item.IsSeries || item.IsSeason || (item.Movie.IsOther && item.SeriesId > 0);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }
}
