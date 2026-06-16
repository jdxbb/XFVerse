using System.ComponentModel;
using System.Runtime.CompilerServices;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class AiRecommendationItem : INotifyPropertyChanged
{
    private const int RecommendationPosterTagDisplayLength = 54;
    private const string MissingRecommendationTagFallback = "-";
    private const string TagOverflowMarker = "..";

    private bool _isWatched;
    private bool _isFavorite;
    private bool _isWantToWatch;
    private bool _isNotInterested;
    private string _watchStateText = "未看";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int MovieId { get; set; }

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string DirectorText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public MovieRatingItem? OmdbRating { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

    public bool IsInLibrary { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public bool IsWatched
    {
        get => _isWatched;
        set
        {
            if (SetField(ref _isWatched, value))
            {
                OnPropertyChanged(nameof(CanAddWantToWatch));
                OnPropertyChanged(nameof(CanToggleWantToWatch));
                OnPropertyChanged(nameof(WantToWatchButtonText));
                WatchStateText = value ? "已看" : "未看";
            }
        }
    }

    public bool IsWantToWatch
    {
        get => _isWantToWatch;
        set
        {
            if (SetField(ref _isWantToWatch, value))
            {
                OnPropertyChanged(nameof(CanAddWantToWatch));
                OnPropertyChanged(nameof(WantToWatchButtonText));
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public bool IsNotInterested
    {
        get => _isNotInterested;
        set
        {
            if (SetField(ref _isNotInterested, value))
            {
                OnPropertyChanged(nameof(NotInterestedButtonText));
            }
        }
    }

    public string ScopeText { get; set; } = "AI 推荐";

    public string AvailabilityText { get; set; } = "外部候选";

    public string WatchStateText
    {
        get => _watchStateText;
        set => SetField(ref _watchStateText, value);
    }

    public string DetailButtonText => MovieId > 0
        ? IsInLibrary ? "查看详情并播放" : "查看详情（暂无播放源）"
        : "查看详情（外部候选）";

    public bool CanAddWantToWatch => !IsWatched && !IsWantToWatch;

    public bool CanToggleWantToWatch => !IsWatched;

    public string WantToWatchButtonText => IsWantToWatch ? "取消想看" : "+ 想看";

    public string NotInterestedButtonText => IsNotInterested ? "取消不想看" : "不想看";

    public string ReleaseDateText => ReleaseDate.HasValue
        ? ReleaseDate.Value.ToString("yyyy-MM-dd")
        : ReleaseYear?.ToString() ?? "年份 -";

    public string TitleOriginalLineText => string.IsNullOrWhiteSpace(OriginalTitle)
        ? Title
        : $"{Title} | {OriginalTitle}";

    public string OriginalTitleDisplayText => FormatDisplayPart(OriginalTitle);

    public string TitleOriginalSeparatorText => string.IsNullOrWhiteSpace(OriginalTitle) ? string.Empty : " | ";

    public string RecommendationTagLineText => JoinVisibleGroups(BuildRecommendationTagGroups(null));

    public string RecommendationTagToolTipText => BuildGroupedTagToolTipText(
        [
            ("类型", Tags),
            ("情绪", EmotionTagsText),
            ("场景", SceneTagsText)
        ],
        RecommendationTagLineText);

    public string RecommendationTagGroupOneText => BuildRecommendationTagGroups(RecommendationPosterTagDisplayLength)[0];

    public string RecommendationTagSeparatorAfterOneText => BuildSeparator(
        RecommendationTagGroupOneText,
        RecommendationTagGroupTwoText,
        RecommendationTagGroupThreeText);

    public string RecommendationTagGroupTwoText => BuildRecommendationTagGroups(RecommendationPosterTagDisplayLength)[1];

    public string RecommendationTagSeparatorAfterTwoText => BuildSeparator(
        RecommendationTagGroupTwoText,
        RecommendationTagGroupThreeText);

    public string RecommendationTagGroupThreeText => BuildRecommendationTagGroups(RecommendationPosterTagDisplayLength)[2];

    public string DirectorDisplayText => $"导演：{FormatDisplayValue(DirectorText)}";

    public string ActorsDisplayText => $"演员：{FormatDisplayValue(ActorsText)}";

    public string WeightedAverageRatingText
    {
        get
        {
            if (!TryGetWeightedAverageRating(out var score))
            {
                return "--";
            }

            return $"{RoundRatingForDisplay(score):0.0}";
        }
    }

    public bool IsHighWeightedAverageRating => TryGetWeightedAverageRating(out var score)
                                                && RoundRatingForDisplay(score) >= 8d;

    public bool ApplyMetadataDetails(MetadataSearchCandidate details)
    {
        var changed = false;

        if (details.TmdbId > 0 && TmdbId is not > 0)
        {
            TmdbId = details.TmdbId;
            changed = true;
        }

        if (ShouldFillText(Title, details.Title))
        {
            Title = details.Title.Trim();
            changed = true;
        }

        if (ShouldFillText(OriginalTitle, details.OriginalTitle))
        {
            OriginalTitle = details.OriginalTitle.Trim();
            changed = true;
        }

        if (!ReleaseYear.HasValue && details.ReleaseYear.HasValue)
        {
            ReleaseYear = details.ReleaseYear;
            changed = true;
        }

        if (!ReleaseDate.HasValue && details.ReleaseDate.HasValue)
        {
            ReleaseDate = details.ReleaseDate;
            changed = true;
        }

        if (ShouldFillText(PosterRemoteUrl, details.PosterRemoteUrl))
        {
            PosterRemoteUrl = details.PosterRemoteUrl.Trim();
            changed = true;
        }

        if (ShouldFillText(Overview, details.Overview))
        {
            Overview = details.Overview.Trim();
            changed = true;
        }

        if (ShouldFillText(DirectorText, details.DirectorText))
        {
            DirectorText = details.DirectorText.Trim();
            changed = true;
        }

        if (ShouldFillText(ActorsText, details.ActorsText))
        {
            ActorsText = details.ActorsText.Trim();
            changed = true;
        }

        if (ShouldFillText(Country, details.Country))
        {
            Country = details.Country.Trim();
            changed = true;
        }

        if (ShouldFillText(Language, details.Language))
        {
            Language = details.Language.Trim();
            changed = true;
        }

        if (!RuntimeMinutes.HasValue && details.RuntimeMinutes.HasValue)
        {
            RuntimeMinutes = details.RuntimeMinutes;
            changed = true;
        }

        if (ShouldFillText(ImdbId, details.ImdbId))
        {
            ImdbId = details.ImdbId.Trim();
            changed = true;
        }

        if (!TmdbRating.HasValue && details.TmdbRating.HasValue)
        {
            TmdbRating = details.TmdbRating;
            changed = true;
        }

        if (!TmdbVoteCount.HasValue && details.TmdbVoteCount.HasValue)
        {
            TmdbVoteCount = details.TmdbVoteCount;
            changed = true;
        }

        if (ShouldFillText(Tags, details.GenresText))
        {
            Tags = details.GenresText.Trim();
            changed = true;
        }

        if (changed)
        {
            NotifyDisplayMetadataChanged();
        }

        return changed;
    }

    public bool ApplyCollectionMetadata(CollectionMovieItem item)
    {
        if (item.IsTvSeason)
        {
            return false;
        }

        var changed = false;
        if (item.MovieId is > 0 && MovieId != item.MovieId.Value)
        {
            MovieId = item.MovieId.Value;
            changed = true;
        }

        if (item.TmdbId is > 0 && TmdbId != item.TmdbId)
        {
            TmdbId = item.TmdbId;
            changed = true;
        }

        if (ApplyText(item.Title, value => Title = value, Title))
        {
            changed = true;
        }

        if (ApplyText(item.OriginalTitle, value => OriginalTitle = value, OriginalTitle))
        {
            changed = true;
        }

        if (item.ReleaseYear.HasValue && ReleaseYear != item.ReleaseYear)
        {
            ReleaseYear = item.ReleaseYear;
            changed = true;
        }

        if (item.ReleaseDate.HasValue && ReleaseDate != item.ReleaseDate)
        {
            ReleaseDate = item.ReleaseDate;
            changed = true;
        }

        if (ApplyText(item.PosterRemoteUrl, value => PosterRemoteUrl = value, PosterRemoteUrl))
        {
            changed = true;
        }

        if (ApplyText(item.Overview, value => Overview = value, Overview))
        {
            changed = true;
        }

        if (ApplyText(item.Country, value => Country = value, Country))
        {
            changed = true;
        }

        if (ApplyText(item.Language, value => Language = value, Language))
        {
            changed = true;
        }

        if (item.RuntimeMinutes is > 0 && RuntimeMinutes != item.RuntimeMinutes)
        {
            RuntimeMinutes = item.RuntimeMinutes;
            changed = true;
        }

        if (ApplyText(item.ImdbId, value => ImdbId = value, ImdbId))
        {
            changed = true;
        }

        var nextTags = string.IsNullOrWhiteSpace(item.AiTagsText) ? item.GenresText : item.AiTagsText;
        if (ApplyText(nextTags, value => Tags = value, Tags))
        {
            changed = true;
        }

        if (ApplyText(item.EmotionTagsText, value => EmotionTagsText = value, EmotionTagsText))
        {
            changed = true;
        }

        if (ApplyText(item.SceneTagsText, value => SceneTagsText = value, SceneTagsText))
        {
            changed = true;
        }

        if (item.TmdbRating.HasValue && TmdbRating != item.TmdbRating)
        {
            TmdbRating = item.TmdbRating;
            changed = true;
        }

        if (item.TmdbVoteCount.HasValue && TmdbVoteCount != item.TmdbVoteCount)
        {
            TmdbVoteCount = item.TmdbVoteCount;
            changed = true;
        }

        var nextOmdbRating = item.OmdbScoreValue.HasValue
            ? new MovieRatingItem
            {
                SourceName = "OMDb",
                ScoreValue = item.OmdbScoreValue.Value,
                ScoreScale = item.OmdbScoreScale is > 0 ? item.OmdbScoreScale.Value : 10d,
                VoteCount = item.OmdbVoteCount,
                SourceUrl = item.OmdbSourceUrl,
                LastUpdatedAt = item.OmdbLastUpdatedAt
            }
            : null;
        if (!AreSameRating(OmdbRating, nextOmdbRating))
        {
            OmdbRating = nextOmdbRating;
            changed = true;
        }

        if (IsInLibrary != item.IsInLibrary)
        {
            IsInLibrary = item.IsInLibrary;
            changed = true;
        }

        var availabilityText = item.AvailabilityText;
        if (!string.Equals(AvailabilityText, availabilityText, StringComparison.Ordinal))
        {
            AvailabilityText = availabilityText;
            changed = true;
        }

        if (changed)
        {
            NotifyDisplayMetadataChanged();
        }

        return changed;

        static bool ApplyText(string? nextValue, Action<string> apply, string? currentValue)
        {
            if (string.IsNullOrWhiteSpace(nextValue))
            {
                return false;
            }

            var trimmed = nextValue.Trim();
            if (string.Equals(currentValue?.Trim(), trimmed, StringComparison.Ordinal))
            {
                return false;
            }

            apply(trimmed);
            return true;
        }
    }

    public bool ApplyMovieDetailMetadata(MovieDetailModel detail)
    {
        var changed = false;
        if (detail.MovieId > 0 && MovieId != detail.MovieId)
        {
            MovieId = detail.MovieId;
            changed = true;
        }

        if (detail.TmdbId is > 0 && TmdbId != detail.TmdbId)
        {
            TmdbId = detail.TmdbId;
            changed = true;
        }

        if (ApplyText(detail.Title, value => Title = value, Title))
        {
            changed = true;
        }

        if (ApplyText(detail.OriginalTitle, value => OriginalTitle = value, OriginalTitle))
        {
            changed = true;
        }

        if (detail.ReleaseYear.HasValue && ReleaseYear != detail.ReleaseYear)
        {
            ReleaseYear = detail.ReleaseYear;
            changed = true;
        }

        if (detail.ReleaseDate.HasValue && ReleaseDate != detail.ReleaseDate)
        {
            ReleaseDate = detail.ReleaseDate;
            changed = true;
        }

        if (ApplyText(detail.PosterRemoteUrl, value => PosterRemoteUrl = value, PosterRemoteUrl)
            || ApplyText(detail.Overview, value => Overview = value, Overview)
            || ApplyText(detail.DirectorText, value => DirectorText = value, DirectorText)
            || ApplyText(detail.ActorsText, value => ActorsText = value, ActorsText)
            || ApplyText(detail.Country, value => Country = value, Country)
            || ApplyText(detail.Language, value => Language = value, Language)
            || ApplyText(detail.ImdbId, value => ImdbId = value, ImdbId))
        {
            changed = true;
        }

        if (detail.RuntimeMinutes is > 0 && RuntimeMinutes != detail.RuntimeMinutes)
        {
            RuntimeMinutes = detail.RuntimeMinutes;
            changed = true;
        }

        var nextTags = string.IsNullOrWhiteSpace(detail.AiTagsText) ? detail.GenresText : detail.AiTagsText;
        if (ApplyText(nextTags, value => Tags = value, Tags)
            || ApplyText(detail.EmotionTagsText, value => EmotionTagsText = value, EmotionTagsText)
            || ApplyText(detail.SceneTagsText, value => SceneTagsText = value, SceneTagsText))
        {
            changed = true;
        }

        var tmdbRating = detail.Ratings.FirstOrDefault(
            rating => string.Equals(rating.SourceName, "TMDB", StringComparison.OrdinalIgnoreCase));
        if (tmdbRating is { ScoreValue: > 0 })
        {
            if (TmdbRating != tmdbRating.ScoreValue)
            {
                TmdbRating = tmdbRating.ScoreValue;
                changed = true;
            }

            if (TmdbVoteCount != tmdbRating.VoteCount)
            {
                TmdbVoteCount = tmdbRating.VoteCount;
                changed = true;
            }
        }

        var nextOmdbRating = detail.Ratings.FirstOrDefault(
            rating => string.Equals(rating.SourceName, "OMDb", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(rating.SourceName, "IMDb", StringComparison.OrdinalIgnoreCase));
        if (!AreSameRating(OmdbRating, nextOmdbRating))
        {
            OmdbRating = nextOmdbRating;
            changed = true;
        }

        var hasPlaybackSource = detail.Sources.Count > 0;
        if (IsInLibrary != hasPlaybackSource)
        {
            IsInLibrary = hasPlaybackSource;
            changed = true;
        }

        if (IsVisibleInLibrary != detail.IsVisibleInLibrary)
        {
            IsVisibleInLibrary = detail.IsVisibleInLibrary;
            changed = true;
        }

        if (LibraryVisibilityState != detail.LibraryVisibilityState)
        {
            LibraryVisibilityState = detail.LibraryVisibilityState;
            changed = true;
        }

        if (IsWatched != detail.IsWatched)
        {
            IsWatched = detail.IsWatched;
            changed = true;
        }

        if (IsWantToWatch != detail.IsWantToWatch)
        {
            IsWantToWatch = detail.IsWantToWatch;
            changed = true;
        }

        if (IsFavorite != detail.IsFavorite)
        {
            IsFavorite = detail.IsFavorite;
            changed = true;
        }

        if (IsNotInterested != detail.IsNotInterested)
        {
            IsNotInterested = detail.IsNotInterested;
            changed = true;
        }

        var availabilityText = hasPlaybackSource ? "有播放源" : "暂无播放源";
        if (!string.Equals(AvailabilityText, availabilityText, StringComparison.Ordinal))
        {
            AvailabilityText = availabilityText;
            changed = true;
        }

        if (changed)
        {
            NotifyDisplayMetadataChanged();
        }

        return changed;

        static bool ApplyText(string? nextValue, Action<string> apply, string? currentValue)
        {
            if (string.IsNullOrWhiteSpace(nextValue))
            {
                return false;
            }

            var trimmed = nextValue.Trim();
            if (string.Equals(currentValue?.Trim(), trimmed, StringComparison.Ordinal))
            {
                return false;
            }

            apply(trimmed);
            return true;
        }
    }

    private bool TryGetWeightedAverageRating(out double score)
    {
        var ratings = new List<(double Score, int Votes)>();
        if (TmdbRating.HasValue)
        {
            ratings.Add((TmdbRating.Value, Math.Max(TmdbVoteCount ?? 0, 0)));
        }

        if (OmdbRating is { ScoreValue: > 0, ScoreScale: > 0 } omdbRating)
        {
            var normalizedScore = omdbRating.ScoreValue / omdbRating.ScoreScale * 10d;
            ratings.Add((normalizedScore, Math.Max(omdbRating.VoteCount ?? 0, 0)));
        }

        if (ratings.Count == 0)
        {
            score = 0d;
            return false;
        }

        if (ratings.Count == 1)
        {
            score = ratings[0].Score;
            return true;
        }

        var totalVotes = ratings.Sum(x => x.Votes);
        score = totalVotes > 0
            ? ratings.Sum(x => x.Score * x.Votes) / totalVotes
            : ratings.Average(x => x.Score);
        return true;
    }

    private static double RoundRatingForDisplay(double score)
    {
        return Math.Round(score, 1, MidpointRounding.AwayFromZero);
    }

    private string[] BuildRecommendationTagGroups(int? maxDisplayLength)
    {
        var groups = BuildRecommendationTagLists();

        if (groups.All(group => group.Count == 0))
        {
            return [MissingRecommendationTagFallback, string.Empty, string.Empty];
        }

        var formatted = groups.Select(FormatTags).ToArray();
        if (!maxDisplayLength.HasValue || FitsDisplayLength(JoinVisibleGroups(formatted), maxDisplayLength.Value))
        {
            return formatted;
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
                if (index >= groups[groupIndex].Count)
                {
                    continue;
                }

                var candidate = CloneSelectedGroups(selected);
                candidate[groupIndex].Add(groups[groupIndex][index]);
                var candidateOrder = displayOrder.Concat([groupIndex]).ToList();
                if (FitsDisplayLength(JoinVisibleGroups(candidate.Select(FormatTags).ToArray()), maxDisplayLength.Value))
                {
                    selected[groupIndex].Add(groups[groupIndex][index]);
                    displayOrder.Add(groupIndex);
                    continue;
                }

                return FormatOverflowTagGroups(candidate, groups, candidateOrder, maxDisplayLength.Value);
            }
        }

        return selected.Select(FormatTags).ToArray();
    }

    private IReadOnlyList<string>[] BuildRecommendationTagLists()
    {
        return
        [
            ParseTags(Tags),
            ParseTags(EmotionTagsText),
            ParseTags(SceneTagsText)
        ];
    }

    private static List<string>[] CloneSelectedGroups(IEnumerable<List<string>> groups)
    {
        return groups.Select(group => group.ToList()).ToArray();
    }

    private static string[] FormatOverflowTagGroups(
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

    private static IReadOnlyList<string> ParseTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(['/', '\u3001', ',', '\uFF0C', '|', ';', '\uFF1B'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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

    private static string TruncateForDisplay(string value, int maxDisplayLength)
    {
        if (FitsDisplayLength(value, maxDisplayLength))
        {
            return value;
        }

        var characters = new List<char>();
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character) && characters.Count >= Math.Max(1, maxDisplayLength - TagOverflowMarker.Length))
            {
                break;
            }

            characters.Add(character);
        }

        return $"{new string(characters.ToArray()).TrimEnd()}{TagOverflowMarker}";
    }

    private static string BuildGroupedTagToolTipText(IEnumerable<(string Label, string? Value)> groups, string fallback)
    {
        var lines = groups
            .Select(group => FormatGroupedTagToolTipLine(group.Label, group.Value))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return lines.Length == 0 ? fallback : string.Join(Environment.NewLine, lines);
    }

    private static string FormatGroupedTagToolTipLine(string label, string? value)
    {
        var text = FormatDisplayPart(value);
        return IsMissingDisplayValue(text) ? string.Empty : $"{label}: {text}";
    }

    private static string FormatDisplayPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string FormatDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private static bool ShouldFillText(string? currentValue, string? nextValue)
    {
        return IsMissingDisplayValue(currentValue) && !IsMissingDisplayValue(nextValue);
    }

    private static bool AreSameRating(MovieRatingItem? current, MovieRatingItem? next)
    {
        if (current is null || next is null)
        {
            return current is null && next is null;
        }

        return string.Equals(current.SourceName, next.SourceName, StringComparison.Ordinal)
               && current.ScoreValue.Equals(next.ScoreValue)
               && current.ScoreScale.Equals(next.ScoreScale)
               && current.VoteCount == next.VoteCount
               && string.Equals(current.SourceUrl, next.SourceUrl, StringComparison.Ordinal)
               && current.LastUpdatedAt == next.LastUpdatedAt;
    }

    private static bool IsMissingDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "-", StringComparison.Ordinal);
    }

    private void NotifyDisplayMetadataChanged()
    {
        OnPropertyChanged(nameof(TmdbId));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(OriginalTitle));
        OnPropertyChanged(nameof(ReleaseYear));
        OnPropertyChanged(nameof(ReleaseDate));
        OnPropertyChanged(nameof(PosterRemoteUrl));
        OnPropertyChanged(nameof(Overview));
        OnPropertyChanged(nameof(DirectorText));
        OnPropertyChanged(nameof(ActorsText));
        OnPropertyChanged(nameof(Country));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(RuntimeMinutes));
        OnPropertyChanged(nameof(ImdbId));
        OnPropertyChanged(nameof(TmdbRating));
        OnPropertyChanged(nameof(TmdbVoteCount));
        OnPropertyChanged(nameof(OmdbRating));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(EmotionTagsText));
        OnPropertyChanged(nameof(SceneTagsText));
        OnPropertyChanged(nameof(IsInLibrary));
        OnPropertyChanged(nameof(IsVisibleInLibrary));
        OnPropertyChanged(nameof(LibraryVisibilityState));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(DetailButtonText));
        OnPropertyChanged(nameof(ReleaseDateText));
        OnPropertyChanged(nameof(TitleOriginalLineText));
        OnPropertyChanged(nameof(OriginalTitleDisplayText));
        OnPropertyChanged(nameof(TitleOriginalSeparatorText));
        OnPropertyChanged(nameof(RecommendationTagLineText));
        OnPropertyChanged(nameof(RecommendationTagToolTipText));
        OnPropertyChanged(nameof(RecommendationTagGroupOneText));
        OnPropertyChanged(nameof(RecommendationTagSeparatorAfterOneText));
        OnPropertyChanged(nameof(RecommendationTagGroupTwoText));
        OnPropertyChanged(nameof(RecommendationTagSeparatorAfterTwoText));
        OnPropertyChanged(nameof(RecommendationTagGroupThreeText));
        OnPropertyChanged(nameof(DirectorDisplayText));
        OnPropertyChanged(nameof(ActorsDisplayText));
        OnPropertyChanged(nameof(WeightedAverageRatingText));
        OnPropertyChanged(nameof(IsHighWeightedAverageRating));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
