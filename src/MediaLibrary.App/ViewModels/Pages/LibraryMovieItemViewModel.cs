using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class LibraryMovieItemViewModel : ObservableObject
{
    private const int PosterMovieTagDisplayLength = 18;
    private const int ListMovieTagDisplayLength = 76;
    private const string TagOverflowMarker = "..";

    private bool _isBatchSelectionMode;
    private bool _isSelected;
    private double? _ratingOverrideValue;
    private string? _ratingOverrideSourceName;

    public LibraryMovieItemViewModel(
        LibraryMovieListItem movie,
        string selectionKey,
        bool isBatchSelectionMode,
        bool isSelected)
    {
        Movie = movie;
        SelectionKey = selectionKey;
        _isBatchSelectionMode = isBatchSelectionMode;
        _isSelected = isSelected;
    }

    public LibraryMovieListItem Movie { get; }

    public string SelectionKey { get; }

    public int MovieId => Movie.MovieId;

    public int SeriesId => Movie.SeriesId;

    public int SeasonId => Movie.SeasonId;

    public bool IsMovie => Movie.IsMovie;

    public bool IsSeries => Movie.IsSeries;

    public bool IsSeason => Movie.IsSeason;

    public string MediaKindText => Movie.MediaKindText;

    public string ProgressSummary => Movie.ProgressSummary;

    public double ProgressValue => Movie.ProgressValue;

    public bool HasProgressPercent => Movie.HasProgressPercent;

    public string ProgressLabel => Movie.ProgressLabel;

    public int? TmdbId => Movie.TmdbId;

    public string Title => Movie.Title;

    public string OriginalTitle => Movie.OriginalTitle;

    public string SeriesTitle => Movie.SeriesTitle;

    public int? ReleaseYear => Movie.ReleaseYear;

    public DateTime? ReleaseDate => Movie.ReleaseDate;

    public string PosterRemoteUrl => Movie.PosterRemoteUrl;

    public string GenresText => Movie.GenresText;

    public string Overview => Movie.Overview;

    public string Country => Movie.Country;

    public string Language => Movie.Language;

    public int? RuntimeMinutes => Movie.RuntimeMinutes;

    public string ImdbId => Movie.ImdbId;

    public IdentificationStatus IdentificationStatus => Movie.IdentificationStatus;

    public double? PrimaryRatingValue => _ratingOverrideValue ?? Movie.PrimaryRatingValue;

    public string PrimaryRatingSourceName => _ratingOverrideSourceName ?? Movie.PrimaryRatingSourceName;

    public int SourceCount => Movie.SourceCount;

    public bool HasActiveSource => Movie.HasActiveSource;

    public bool HasLocalSource => Movie.HasLocalSource;

    public bool HasWebDavSource => Movie.HasWebDavSource;

    public string SourceSummary => Movie.SourceSummary;

    public string SourceStatusText => Movie.HasActiveSource ? Movie.SourceSummary : "暂无播放源";

    public string SourceBadgeText => HasActiveSource ? SourceSummary : "无播放源";

    public bool HasNoActiveSource => !HasActiveSource;

    public bool HasPoster => !string.IsNullOrWhiteSpace(PosterRemoteUrl);

    public bool HasOriginalTitle => !string.IsNullOrWhiteSpace(OriginalTitle)
                                    && !string.Equals(OriginalTitle, Title, StringComparison.OrdinalIgnoreCase);

    public bool HasGenres => !string.IsNullOrWhiteSpace(GenresText);

    public string ReleaseYearText => ReleaseYear?.ToString() ?? "年份未知";

    public string ReleaseDateText => ReleaseDate.HasValue ? ReleaseDate.Value.ToString("yyyy-MM-dd") : ReleaseYearText;

    public bool HasRating => PrimaryRatingValue.HasValue;

    public string RatingDisplayText => HasRating ? FormatRatingDisplayText(PrimaryRatingValue!.Value) : "--";

    public bool IsHighRating => PrimaryRatingValue.HasValue
                                && RoundRatingForDisplay(PrimaryRatingValue.Value) >= 8d;

    public string RatingSourceText => string.IsNullOrWhiteSpace(PrimaryRatingSourceName) ? "评分" : PrimaryRatingSourceName;

    public string SourceCountText => HasActiveSource ? $"{SourceCount} 个播放源" : "无播放源";

    public string TagSummaryText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GenresText))
            {
                return GenresText;
            }

            if (!string.IsNullOrWhiteSpace(Movie.AiTagsText))
            {
                return Movie.AiTagsText;
            }

            if (!string.IsNullOrWhiteSpace(Movie.EmotionTagsText))
            {
                return Movie.EmotionTagsText;
            }

            if (!string.IsNullOrWhiteSpace(Movie.SceneTagsText))
            {
                return Movie.SceneTagsText;
            }

            return MediaKindText;
        }
    }

    public string CategoryTagText => MediaKindText;

    public string TypeTagSourceText => string.IsNullOrWhiteSpace(Movie.AiTagsText) ? GenresText : Movie.AiTagsText;

    public string TypeTagText => FormatTagGroup(TypeTagSourceText);

    public string MoodTagText => FormatTagGroup(Movie.EmotionTagsText);

    public string SceneTagText => FormatTagGroup(Movie.SceneTagsText);

    public bool HasMoodTag => !string.IsNullOrWhiteSpace(MoodTagText);

    public string SingleTagLine => FormatSingleTagLine(GenresText, MissingSingleTagFallback);

    public string FullTagLine => IsMovie ? MovieTagLine : SingleTagLine;

    public string PosterTagLine => JoinVisibleGroups(PosterTagGroupOneText, PosterTagGroupTwoText, PosterTagGroupThreeText);

    public string MovieTagLine => JoinVisibleGroups(BuildMovieTagGroups(null));

    public string PosterTagToolTipText => FullTagLine;

    public string ListTagToolTipText => FullTagLine;

    public string PosterTagGroupOneText => IsMovie
        ? BuildMovieTagGroups(PosterMovieTagDisplayLength)[0]
        : SingleTagLine;

    public string PosterTagGroupTwoText => IsMovie ? BuildMovieTagGroups(PosterMovieTagDisplayLength)[1] : string.Empty;

    public string PosterTagGroupThreeText => IsMovie ? BuildMovieTagGroups(PosterMovieTagDisplayLength)[2] : string.Empty;

    public string PosterTagSeparatorAfterOneText => BuildSeparator(PosterTagGroupOneText, PosterTagGroupTwoText, PosterTagGroupThreeText);

    public string PosterTagSeparatorAfterTwoText => BuildSeparator(PosterTagGroupTwoText, PosterTagGroupThreeText);

    public string ListTagGroupOneText => IsMovie
        ? BuildMovieTagGroups(ListMovieTagDisplayLength)[0]
        : SingleTagLine;

    public string ListTagGroupTwoText => IsMovie ? BuildMovieTagGroups(ListMovieTagDisplayLength)[1] : string.Empty;

    public string ListTagGroupThreeText => IsMovie ? BuildMovieTagGroups(ListMovieTagDisplayLength)[2] : string.Empty;

    public string ListTagSeparatorAfterOneText => BuildSeparator(ListTagGroupOneText, ListTagGroupTwoText, ListTagGroupThreeText);

    public string ListTagSeparatorAfterTwoText => BuildSeparator(ListTagGroupTwoText, ListTagGroupThreeText);

    public string ListTitleLineText
    {
        get
        {
            return $"{ListDisplayTitleText}      {ListTitleMetaText}";
        }
    }

    public string ListDisplayTitleText => HasOriginalTitle ? $"{Title} | {OriginalTitle}" : Title;

    public string OriginalTitleSeparatorText => HasOriginalTitle ? " | " : string.Empty;

    public string ListTitleMetaSpacingText => "      ";

    public string ListTitleMetaText => string.Join(
        "    ",
        new[]
        {
            DirectorText,
            CastText
        });

    public string ListDateRuntimeText => IsMovie && RuntimeMinutes is > 0
        ? $"{ReleaseDateText} | {RuntimeText}"
        : ReleaseDateText;

    public string ListDateAndTagSpacingText => "      ";

    public string ListDateAndTagLine => $"{ListDateRuntimeText}{ListDateAndTagSpacingText}{FullTagLine}";

    public string ListTagLine => JoinVisibleGroups(ListTagGroupOneText, ListTagGroupTwoText, ListTagGroupThreeText);

    public string DirectorText => $"导演 {FormatCrewText(Movie.DirectorText)}";

    public string CastText => $"演员 {FormatCrewText(Movie.ActorsText)}";

    public string RuntimeText => RuntimeMinutes is > 0
        ? $"{RuntimeMinutes.Value / 60:00}:{RuntimeMinutes.Value % 60:00}:00"
        : "--:--:--";

    public string ListMetadataText => string.Join(
        " | ",
        new[] { ListDateRuntimeText, ListTitleMetaText }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    public bool IsGroupedPlaceholder => Movie.IsOther
                                        && !string.IsNullOrWhiteSpace(Movie.GroupedRangeKey)
                                        && Movie.GroupedRangeMediaFileIds.Count > 0;

    public string DetailHintText => IsGroupedPlaceholder ? "未识别剧集候选" : MediaKindText;

    public string BatchModeHintText
    {
        get
        {
            if (IsGroupedPlaceholder)
            {
                return "未识别剧集候选";
            }

            var hint = Movie.IsOther ? DetailHintText : string.Empty;
            return string.Equals(hint, CategoryTagText, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : hint;
        }
    }

    public bool HasBatchModeHint => IsBatchSelectionMode && !string.IsNullOrWhiteSpace(BatchModeHintText);

    public string PrimaryMetadataText
    {
        get
        {
            var parts = new List<string> { ReleaseDateText, MediaKindText };
            if (RuntimeMinutes.HasValue && IsMovie)
            {
                parts.Add($"{RuntimeMinutes.Value} 分钟");
            }

            return string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string SecondarySummary
    {
        get
        {
            if (IsGroupedPlaceholder)
            {
                return string.IsNullOrWhiteSpace(Movie.GroupedRangeSampleFilesText)
                    ? ProgressSummary
                    : Movie.GroupedRangeSampleFilesText;
            }

            if (IsSeries || IsSeason)
            {
                return ProgressSummary;
            }

            return SourceStatusText;
        }
    }

    public string UserStateText
    {
        get
        {
            if (IsNotInterested)
            {
                return "不想看";
            }

            if (IsFavorite)
            {
                return "喜爱";
            }

            if (IsWantToWatch)
            {
                return "想看";
            }

            return IsWatched ? "已看" : "未看";
        }
    }

    public string StateSummary
    {
        get
        {
            var states = new List<string>();
            if (Movie.IsFavorite)
            {
                states.Add("喜爱");
            }

            if (Movie.IsWantToWatch)
            {
                states.Add("想看");
            }

            if (Movie.IsNotInterested)
            {
                states.Add("不想看");
            }

            if (Movie.IsWatched)
            {
                states.Add("已看");
            }

            return states.Count == 0 ? "无状态" : string.Join(" / ", states);
        }
    }

    public bool IsInLibrary => Movie.IsInLibrary;

    public bool IsFavorite => Movie.IsFavorite;

    public bool IsWatched => Movie.IsWatched;

    public bool IsWantToWatch => Movie.IsWantToWatch;

    public bool IsNotInterested => Movie.IsNotInterested;

    public DateTime UpdatedAt => Movie.UpdatedAt;

    public bool IsBatchSelectionMode
    {
        get => _isBatchSelectionMode;
        set
        {
            if (SetProperty(ref _isBatchSelectionMode, value))
            {
                OnPropertyChanged(nameof(SelectionDotVisible));
                OnPropertyChanged(nameof(BatchModeHintText));
                OnPropertyChanged(nameof(HasBatchModeHint));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool SelectionDotVisible => IsBatchSelectionMode;

    public void ApplyRatingOverride(double? value, string sourceName)
    {
        var normalizedSourceName = string.IsNullOrWhiteSpace(sourceName) ? string.Empty : sourceName.Trim();
        if (_ratingOverrideValue == value
            && string.Equals(_ratingOverrideSourceName, normalizedSourceName, StringComparison.Ordinal))
        {
            return;
        }

        _ratingOverrideValue = value;
        _ratingOverrideSourceName = normalizedSourceName;
        OnPropertyChanged(nameof(PrimaryRatingValue));
        OnPropertyChanged(nameof(PrimaryRatingSourceName));
        OnPropertyChanged(nameof(HasRating));
        OnPropertyChanged(nameof(RatingDisplayText));
        OnPropertyChanged(nameof(IsHighRating));
        OnPropertyChanged(nameof(RatingSourceText));
    }

    private static string FormatRatingDisplayText(double score)
    {
        return RoundRatingForDisplay(score).ToString("0.0");
    }

    private static double RoundRatingForDisplay(double score)
    {
        return Math.Round(score, 1, MidpointRounding.AwayFromZero);
    }

    private string MissingSingleTagFallback
    {
        get
        {
            if (IsMovie)
            {
                return "无影片标签";
            }

            if (IsSeries)
            {
                return "无电视剧标签";
            }

            if (IsSeason)
            {
                return "无电视剧季标签";
            }

            return "无媒体标签";
        }
    }

    private string[] BuildMovieTagGroups(int? maxDisplayLength)
    {
        var groups = new[]
        {
            ParseTags(TypeTagSourceText),
            ParseTags(Movie.EmotionTagsText),
            ParseTags(Movie.SceneTagsText)
        };

        if (groups.All(group => group.Count == 0))
        {
            return [MissingSingleTagFallback, string.Empty, string.Empty];
        }

        var fullGroups = groups.Select(FormatTags).ToArray();
        if (!maxDisplayLength.HasValue || FitsDisplayLength(JoinVisibleGroups(fullGroups), maxDisplayLength.Value))
        {
            return fullGroups;
        }

        var selected = new[]
        {
            new List<string>(),
            new List<string>(),
            new List<string>()
        };

        var displayOrder = new List<int>();
        var maxCount = groups.Max(group => group.Count);
        for (var index = 0; index < maxCount; index++)
        {
            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                if (index < groups[groupIndex].Count)
                {
                    var candidate = CloneSelectedGroups(selected);
                    candidate[groupIndex].Add(groups[groupIndex][index]);
                    var candidateOrder = displayOrder.Concat([groupIndex]).ToList();
                    if (FitsDisplayLength(JoinVisibleGroups(candidate.Select(FormatTags).ToArray()), maxDisplayLength.Value))
                    {
                        selected[groupIndex].Add(groups[groupIndex][index]);
                        displayOrder.Add(groupIndex);
                        continue;
                    }

                    return FormatOverflowMovieTagGroups(candidate, groups, candidateOrder, maxDisplayLength.Value);
                }
            }
        }

        return selected.Select(FormatTags).ToArray();
    }

    private static List<string>[] CloneSelectedGroups(IEnumerable<List<string>> groups)
    {
        return groups.Select(group => group.ToList()).ToArray();
    }

    private static string[] FormatOverflowMovieTagGroups(
        List<string>[] selected,
        IReadOnlyList<string>[] originalGroups,
        List<int> displayOrder,
        int maxDisplayLength)
    {
        EnsureAtLeastOneSelectedTag(selected, originalGroups, displayOrder);
        while (!FitsDisplayLength(JoinVisibleGroups(FormatGroupsWithOverflow(selected, originalGroups)), maxDisplayLength)
               && displayOrder.Count > 1)
        {
            var groupIndex = displayOrder[^1];
            displayOrder.RemoveAt(displayOrder.Count - 1);
            if (selected[groupIndex].Count == 0)
            {
                continue;
            }

            selected[groupIndex].RemoveAt(selected[groupIndex].Count - 1);
        }

        var formatted = FormatGroupsWithOverflow(selected, originalGroups);
        if (FitsDisplayLength(JoinVisibleGroups(formatted), maxDisplayLength))
        {
            return formatted;
        }

        return TruncateVisibleGroupsForDisplay(formatted, maxDisplayLength);
    }

    private static void EnsureAtLeastOneSelectedTag(
        IList<string>[] selected,
        IReadOnlyList<string>[] originalGroups,
        ICollection<int> displayOrder)
    {
        if (selected.Any(group => group.Count > 0))
        {
            return;
        }

        for (var index = 0; index < originalGroups.Length; index++)
        {
            if (originalGroups[index].Count == 0)
            {
                continue;
            }

            selected[index].Add(originalGroups[index][0]);
            displayOrder.Add(index);
            return;
        }
    }

    private static string[] FormatGroupsWithOverflow(
        IReadOnlyList<string>[] selected,
        IReadOnlyList<string>[] originalGroups)
    {
        var formatted = new string[selected.Length];
        for (var index = 0; index < selected.Length; index++)
        {
            if (selected[index].Count == 0)
            {
                formatted[index] = string.Empty;
                continue;
            }

            var groupText = FormatTags(selected[index]);
            formatted[index] = originalGroups[index].Count > selected[index].Count
                ? $"{groupText}{TagOverflowMarker}"
                : groupText;
        }

        return formatted;
    }

    private static string BuildLimitedSingleTagLine(string? value, string fallback, int maxDisplayLength)
    {
        var tags = ParseTags(value);
        if (tags.Count == 0)
        {
            return fallback;
        }

        var fullLine = FormatTags(tags);
        if (FitsDisplayLength(fullLine, maxDisplayLength))
        {
            return fullLine;
        }

        var selected = new List<string>();
        foreach (var tag in tags)
        {
            var candidate = selected.Concat([tag]).ToArray();
            if (!FitsDisplayLength(FormatTags(candidate), maxDisplayLength))
            {
                break;
            }

            selected.Add(tag);
        }

        if (selected.Count == 0)
        {
            return TruncateForDisplay(tags[0], maxDisplayLength);
        }

        return $"{FormatTags(selected)}{TagOverflowMarker}";
    }

    private static string[] TruncateVisibleGroupsForDisplay(string[] groups, int maxDisplayLength)
    {
        var result = groups.ToArray();
        while (!FitsDisplayLength(JoinVisibleGroups(result), maxDisplayLength))
        {
            var groupIndex = Enumerable.Range(0, result.Length)
                .Where(index => !string.IsNullOrWhiteSpace(result[index]))
                .LastOrDefault(-1);
            if (groupIndex < 0)
            {
                break;
            }

            var current = result[groupIndex];
            if (CalculateDisplayLength(current) <= TagOverflowMarker.Length + 1)
            {
                result[groupIndex] = string.Empty;
                continue;
            }

            result[groupIndex] = TruncateForDisplay(current, CalculateDisplayLength(current) - 1);
        }

        return result;
    }

    private static string FormatTagGroup(string? value)
    {
        var tags = ParseTags(value);
        return FormatTags(tags);
    }

    private static string FormatSingleTagLine(string? value, string fallback)
    {
        var tags = ParseTags(value);
        return tags.Count == 0 ? fallback : FormatTags(tags);
    }

    private static IReadOnlyList<string> ParseTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(new[] { '/', '、', ',', '，', '|', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatTags(IEnumerable<string> tags)
    {
        return string.Join(" / ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
    }

    private static string JoinVisibleGroups(params string[] groups)
    {
        return string.Join(" | ", groups.Where(group => !string.IsNullOrWhiteSpace(group)));
    }

    private static string BuildSeparator(string currentGroup, params string[] followingGroups)
    {
        return !string.IsNullOrWhiteSpace(currentGroup) && followingGroups.Any(group => !string.IsNullOrWhiteSpace(group))
            ? " | "
            : string.Empty;
    }

    private static bool FitsDisplayLength(string value, int maxDisplayLength)
    {
        return CalculateDisplayLength(value) <= maxDisplayLength;
    }

    private static int CalculateDisplayLength(string value)
    {
        return value.Count(character => !char.IsWhiteSpace(character));
    }

    private static string TruncateForDisplay(string value, int maxDisplayLength)
    {
        if (FitsDisplayLength(value, maxDisplayLength))
        {
            return value;
        }

        var remaining = Math.Max(1, maxDisplayLength - TagOverflowMarker.Length);
        var chars = value
            .Where(character => !char.IsWhiteSpace(character))
            .Take(remaining);
        return $"{new string(chars.ToArray())}{TagOverflowMarker}";
    }

    private static string FormatCrewText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
