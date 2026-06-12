using System.ComponentModel;
using System.Runtime.CompilerServices;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class AiRecommendationItem : INotifyPropertyChanged
{
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

    public string RecommendationTagLineText => JoinDisplayParts(Tags, EmotionTagsText, SceneTagsText);

    public string RecommendationTagToolTipText => BuildGroupedTagToolTipText(
        [
            ("类型", Tags),
            ("情绪", EmotionTagsText),
            ("场景", SceneTagsText)
        ],
        RecommendationTagLineText);

    public string RecommendationTagGroupOneText => GetRecommendationTagPart(0);

    public string RecommendationTagSeparatorAfterOneText => GetRecommendationTagParts().Length > 1 ? " | " : string.Empty;

    public string RecommendationTagGroupTwoText => GetRecommendationTagPart(1);

    public string RecommendationTagSeparatorAfterTwoText => GetRecommendationTagParts().Length > 2 ? " | " : string.Empty;

    public string RecommendationTagGroupThreeText => GetRecommendationTagPart(2);

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

    private static string JoinDisplayParts(params string?[] values)
    {
        var parts = values
            .Select(FormatDisplayPart)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? "-" : string.Join(" | ", parts);
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

    private string GetRecommendationTagPart(int index)
    {
        var parts = GetRecommendationTagParts();
        if (parts.Length == 0)
        {
            return index == 0 ? "-" : string.Empty;
        }

        return index < parts.Length ? parts[index] : string.Empty;
    }

    private string[] GetRecommendationTagParts()
    {
        return
        [
            .. new[] { Tags, EmotionTagsText, SceneTagsText }
                .Select(FormatDisplayPart)
                .Where(part => !string.IsNullOrWhiteSpace(part))
        ];
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
        OnPropertyChanged(nameof(Tags));
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
