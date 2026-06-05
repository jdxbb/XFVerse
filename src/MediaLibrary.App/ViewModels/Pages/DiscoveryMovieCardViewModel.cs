using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class DiscoveryMovieCardViewModel : ObservableObject
{
    private const int PosterMovieTagDisplayLength = 18;
    private const int ListMovieTagDisplayLength = 76;
    private const string TagOverflowMarker = "..";

    private bool _isWantToWatch;
    private bool _isWatched;
    private bool _isFavorite;
    private bool _isNotInterested;
    private string _ratingText = "--";
    private string _directorText = string.Empty;
    private string _actorsText = string.Empty;
    private double? _ratingValue;
    private MovieRatingItem? _omdbRating;

    public DiscoveryMovieCardViewModel(TmdbMovieDiscoveryItem source, int searchOrder)
    {
        TmdbId = source.TmdbId;
        Title = source.Title;
        OriginalTitle = source.OriginalTitle;
        ReleaseYear = source.ReleaseYear;
        ReleaseDate = source.ReleaseDate;
        Overview = source.Overview;
        PosterRemoteUrl = source.PosterRemoteUrl;
        GenreIds = source.GenreIds;
        GenresText = string.IsNullOrWhiteSpace(source.GenresText)
            ? TmdbGenreMapper.MapGenreIds(source.GenreIds)
            : source.GenresText;
        DisplayTags = GenresText;
        OriginalLanguage = source.OriginalLanguage;
        OriginCountries = source.OriginCountries;
        Country = source.Country;
        Language = source.Language;
        RuntimeMinutes = source.RuntimeMinutes;
        ImdbId = source.ImdbId;
        TmdbRating = source.TmdbRating;
        TmdbVoteCount = source.TmdbVoteCount;
        Popularity = source.Popularity;
        SearchOrder = searchOrder;
        RefreshRating();
    }

    public int TmdbId { get; }

    public int? MovieId { get; private set; }

    public bool HasLocalMovie => MovieId is > 0;

    public int ActiveSourceCount { get; private set; }

    public string Title { get; private set; }

    public string OriginalTitle { get; private set; }

    public int? ReleaseYear { get; private set; }

    public string ReleaseDate { get; private set; }

    public string Overview { get; private set; }

    public string PosterRemoteUrl { get; private set; }

    public IReadOnlyList<int> GenreIds { get; }

    public string GenresText { get; private set; }

    public string DisplayTags { get; private set; }

    public string EmotionTagsText { get; private set; } = string.Empty;

    public string SceneTagsText { get; private set; } = string.Empty;

    public string Country { get; private set; }

    public string Language { get; private set; }

    public string DirectorText => $"导演 {FormatCrewText(_directorText)}";

    public string CastText => $"演员 {FormatCrewText(_actorsText)}";

    public bool NeedsDetailsSnapshot => string.IsNullOrWhiteSpace(_directorText)
                                        || string.IsNullOrWhiteSpace(_actorsText)
                                        || string.IsNullOrWhiteSpace(GenresText);

    public int? RuntimeMinutes { get; private set; }

    public string ImdbId { get; private set; }

    public string OriginalLanguage { get; }

    public IReadOnlyList<string> OriginCountries { get; }

    public double? TmdbRating { get; private set; }

    public int? TmdbVoteCount { get; private set; }

    public double? Popularity { get; }

    public int SearchOrder { get; }

    public bool IsInLibrary { get; private set; }

    public bool IsVisibleInLibrary { get; private set; }

    public LibraryVisibilityState LibraryVisibilityState { get; private set; } = LibraryVisibilityState.Auto;

    public bool IsWantToWatch
    {
        get => _isWantToWatch;
        private set
        {
            if (SetProperty(ref _isWantToWatch, value))
            {
                OnPropertyChanged(nameof(WantToWatchButtonText));
                OnPropertyChanged(nameof(CanToggleWantToWatch));
            }
        }
    }

    public bool IsWatched
    {
        get => _isWatched;
        private set
        {
            if (SetProperty(ref _isWatched, value))
            {
                OnPropertyChanged(nameof(WantToWatchButtonText));
                OnPropertyChanged(nameof(CanToggleWantToWatch));
                OnPropertyChanged(nameof(WatchStateText));
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        private set => SetProperty(ref _isFavorite, value);
    }

    public bool IsNotInterested
    {
        get => _isNotInterested;
        private set => SetProperty(ref _isNotInterested, value);
    }

    public MovieRatingItem? OmdbRating
    {
        get => _omdbRating;
        private set => SetProperty(ref _omdbRating, value);
    }

    public bool CanEnrichExternalOmdbRating => HasLocalMovie || OmdbRating is null;

    public string RatingText
    {
        get => _ratingText;
        private set => SetProperty(ref _ratingText, value);
    }

    public string RatingBadgeText => RatingText;

    public string WeightedAverageRatingText => RatingText;

    public string RatingDisplayText => RatingText;

    public double? RatingValue
    {
        get => _ratingValue;
        private set => SetProperty(ref _ratingValue, value);
    }

    public bool IsHighRating => DiscoveryRatingPresenter.IsHighDisplayRating(RatingValue);

    public bool IsHighWeightedAverageRating => IsHighRating;

    public string YearText => ReleaseYear?.ToString() ?? "-";

    public string ReleaseDateText => string.IsNullOrWhiteSpace(ReleaseDate) ? YearText : ReleaseDate;

    public string RankText => $"#{SearchOrder}";

    public string OriginalTitleText => string.IsNullOrWhiteSpace(OriginalTitle) ? string.Empty : OriginalTitle;

    public string TitleWithOriginalText => string.IsNullOrWhiteSpace(OriginalTitle)
                                           || string.Equals(Title, OriginalTitle, StringComparison.OrdinalIgnoreCase)
        ? Title
        : $"{Title}｜{OriginalTitle}";

    public string OverviewText => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public bool HasPoster => !string.IsNullOrWhiteSpace(PosterRemoteUrl);

    public string CategoryTagText => "电影";

    public string DetailHintText => "电影";

    public string AvailabilityText => IsInLibrary
        ? "有播放源"
        : HasLocalMovie || IsVisibleInLibrary
            ? "暂无播放源"
            : "未加入媒体库";

    public string WatchStateText => IsWatched ? "已看" : "未看";

    public bool CanToggleWantToWatch => !IsWatched;

    public string WantToWatchButtonText => IsWatched ? "已看" : IsWantToWatch ? "取消想看" : "+ 想看";

    public string FullTagLine => JoinVisibleGroups(BuildTagGroups(null));

    public string PosterTagLine => JoinVisibleGroups(PosterTagGroupOneText, PosterTagGroupTwoText, PosterTagGroupThreeText);

    public string PosterTagToolTipText => FullTagLine;

    public string ListTagToolTipText => FullTagLine;

    public string PosterTagGroupOneText => BuildTagGroups(PosterMovieTagDisplayLength)[0];

    public string PosterTagGroupTwoText => BuildTagGroups(PosterMovieTagDisplayLength)[1];

    public string PosterTagGroupThreeText => BuildTagGroups(PosterMovieTagDisplayLength)[2];

    public string PosterTagSeparatorAfterOneText => BuildSeparator(PosterTagGroupOneText, PosterTagGroupTwoText, PosterTagGroupThreeText);

    public string PosterTagSeparatorAfterTwoText => BuildSeparator(PosterTagGroupTwoText, PosterTagGroupThreeText);

    public string ListTagGroupOneText => BuildTagGroups(ListMovieTagDisplayLength)[0];

    public string ListTagGroupTwoText => BuildTagGroups(ListMovieTagDisplayLength)[1];

    public string ListTagGroupThreeText => BuildTagGroups(ListMovieTagDisplayLength)[2];

    public string ListTagSeparatorAfterOneText => BuildSeparator(ListTagGroupOneText, ListTagGroupTwoText, ListTagGroupThreeText);

    public string ListTagSeparatorAfterTwoText => BuildSeparator(ListTagGroupTwoText, ListTagGroupThreeText);

    public string ListDateRuntimeText => RuntimeMinutes is > 0
        ? $"{ReleaseDateText} | {RuntimeText}"
        : ReleaseDateText;

    public string ListDateAndTagSpacingText => "      ";

    public string ListTagLine => JoinVisibleGroups(ListTagGroupOneText, ListTagGroupTwoText, ListTagGroupThreeText);

    public string RuntimeText => RuntimeMinutes is > 0
        ? $"{RuntimeMinutes.Value / 60:00}:{RuntimeMinutes.Value % 60:00}:00"
        : "--:--:--";

    public bool CanAddToLibrary => LibraryVisibilityState == LibraryVisibilityState.Hidden
                                   || (!HasLocalMovie && !IsVisibleInLibrary);

    public string AddToLibraryButtonText => LibraryVisibilityState == LibraryVisibilityState.Hidden ? "恢复到媒体库" : "加入媒体库";

    public void ApplyStatus(DiscoveryMovieStatus status)
    {
        MovieId = status.MovieId;
        ActiveSourceCount = status.ActiveSourceCount;
        IsInLibrary = status.IsInLibrary;
        IsVisibleInLibrary = status.IsVisibleInLibrary;
        LibraryVisibilityState = status.LibraryVisibilityState;
        IsWatched = status.IsWatched;
        IsWantToWatch = status.IsWantToWatch;
        IsFavorite = status.IsFavorite;
        IsNotInterested = status.IsNotInterested;
        if (status.HasLocalMovie)
        {
            Title = string.IsNullOrWhiteSpace(status.Title) ? Title : status.Title;
            OriginalTitle = string.IsNullOrWhiteSpace(status.OriginalTitle) ? OriginalTitle : status.OriginalTitle;
            ReleaseYear = status.ReleaseYear ?? ReleaseYear;
            ReleaseDate = status.ReleaseDate?.ToString("yyyy-MM-dd") ?? ReleaseDate;
            PosterRemoteUrl = string.IsNullOrWhiteSpace(status.PosterRemoteUrl) ? PosterRemoteUrl : status.PosterRemoteUrl;
            Overview = string.IsNullOrWhiteSpace(status.Overview) ? Overview : status.Overview;
            GenresText = string.IsNullOrWhiteSpace(status.GenresText) ? GenresText : status.GenresText;
            DisplayTags = GenresText;
            EmotionTagsText = string.Empty;
            SceneTagsText = string.Empty;
            Country = string.IsNullOrWhiteSpace(status.Country) ? Country : status.Country;
            Language = string.IsNullOrWhiteSpace(status.Language) ? Language : status.Language;
            _directorText = string.IsNullOrWhiteSpace(status.DirectorText) ? _directorText : status.DirectorText;
            _actorsText = string.IsNullOrWhiteSpace(status.ActorsText) ? _actorsText : status.ActorsText;
            RuntimeMinutes = status.RuntimeMinutes ?? RuntimeMinutes;
            ImdbId = string.IsNullOrWhiteSpace(status.ImdbId) ? ImdbId : status.ImdbId;
            TmdbRating ??= status.TmdbRating;
            TmdbVoteCount ??= status.TmdbVoteCount;
            if (status.OmdbScoreValue.HasValue)
            {
                SetOmdbRating(
                    new MovieRatingItem
                    {
                        SourceName = "OMDb",
                        ScoreValue = status.OmdbScoreScale is > 0
                            ? Math.Clamp(status.OmdbScoreValue.Value / status.OmdbScoreScale.Value * 10d, 0d, 10d)
                            : status.OmdbScoreValue.Value,
                        ScoreScale = 10d,
                        VoteCount = status.OmdbVoteCount,
                        SourceUrl = status.OmdbSourceUrl,
                        LastUpdatedAt = status.OmdbLastUpdatedAt
                    });
            }
            else
            {
                SetOmdbRating(null);
            }

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(OriginalTitle));
            OnPropertyChanged(nameof(OriginalTitleText));
            OnPropertyChanged(nameof(TitleWithOriginalText));
            OnPropertyChanged(nameof(ReleaseYear));
            OnPropertyChanged(nameof(ReleaseDate));
            OnPropertyChanged(nameof(YearText));
            OnPropertyChanged(nameof(ReleaseDateText));
            OnPropertyChanged(nameof(PosterRemoteUrl));
            OnPropertyChanged(nameof(HasPoster));
            OnPropertyChanged(nameof(Overview));
            OnPropertyChanged(nameof(OverviewText));
            OnPropertyChanged(nameof(GenresText));
            OnPropertyChanged(nameof(DisplayTags));
            OnPropertyChanged(nameof(EmotionTagsText));
            OnPropertyChanged(nameof(SceneTagsText));
            NotifyTagPresentationChanged();
            OnPropertyChanged(nameof(DirectorText));
            OnPropertyChanged(nameof(CastText));
            OnPropertyChanged(nameof(ListDateRuntimeText));
            OnPropertyChanged(nameof(RuntimeText));
            OnPropertyChanged(nameof(IsInLibrary));
            OnPropertyChanged(nameof(HasLocalMovie));
            OnPropertyChanged(nameof(ActiveSourceCount));
            OnPropertyChanged(nameof(MovieId));
            OnPropertyChanged(nameof(AvailabilityText));
            OnPropertyChanged(nameof(CanAddToLibrary));
            OnPropertyChanged(nameof(AddToLibraryButtonText));
            OnPropertyChanged(nameof(WatchStateText));
        }
        else
        {
            RefreshRating();
            OnPropertyChanged(nameof(IsInLibrary));
            OnPropertyChanged(nameof(HasLocalMovie));
            OnPropertyChanged(nameof(ActiveSourceCount));
            OnPropertyChanged(nameof(MovieId));
            OnPropertyChanged(nameof(AvailabilityText));
            OnPropertyChanged(nameof(CanAddToLibrary));
            OnPropertyChanged(nameof(AddToLibraryButtonText));
        }

        NotifyLibraryStatusChanged();
    }

    public void ApplyMissingStatus()
    {
        MovieId = null;
        ActiveSourceCount = 0;
        IsInLibrary = false;
        IsVisibleInLibrary = false;
        LibraryVisibilityState = LibraryVisibilityState.Auto;
        IsWatched = false;
        IsWantToWatch = false;
        IsFavorite = false;
        IsNotInterested = false;
        NotifyLibraryStatusChanged();
        OnPropertyChanged(nameof(WatchStateText));
    }

    public void ApplyWantToWatchState(bool isWantToWatch)
    {
        IsWantToWatch = isWantToWatch;
        if (isWantToWatch)
        {
            IsNotInterested = false;
        }
    }

    private void NotifyLibraryStatusChanged()
    {
        OnPropertyChanged(nameof(MovieId));
        OnPropertyChanged(nameof(ActiveSourceCount));
        OnPropertyChanged(nameof(IsInLibrary));
        OnPropertyChanged(nameof(IsVisibleInLibrary));
        OnPropertyChanged(nameof(LibraryVisibilityState));
        OnPropertyChanged(nameof(HasLocalMovie));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(CanAddToLibrary));
        OnPropertyChanged(nameof(AddToLibraryButtonText));
    }

    public void SetImdbId(string imdbId)
    {
        if (string.IsNullOrWhiteSpace(ImdbId) && !string.IsNullOrWhiteSpace(imdbId))
        {
            ImdbId = imdbId;
            OnPropertyChanged(nameof(ImdbId));
        }
    }

    public void SetDetailsSnapshot(MetadataSearchCandidate details)
    {
        SetImdbId(details.ImdbId);
        if (string.IsNullOrWhiteSpace(Overview) && !string.IsNullOrWhiteSpace(details.Overview))
        {
            Overview = details.Overview;
            OnPropertyChanged(nameof(Overview));
            OnPropertyChanged(nameof(OverviewText));
        }

        if (string.IsNullOrWhiteSpace(Country) && !string.IsNullOrWhiteSpace(details.Country))
        {
            Country = details.Country;
            OnPropertyChanged(nameof(Country));
        }

        if (string.IsNullOrWhiteSpace(Language) && !string.IsNullOrWhiteSpace(details.Language))
        {
            Language = details.Language;
            OnPropertyChanged(nameof(Language));
        }

        if (string.IsNullOrWhiteSpace(GenresText) && !string.IsNullOrWhiteSpace(details.GenresText))
        {
            GenresText = details.GenresText;
            DisplayTags = GenresText;
            OnPropertyChanged(nameof(GenresText));
            OnPropertyChanged(nameof(DisplayTags));
            NotifyTagPresentationChanged();
        }

        if (string.IsNullOrWhiteSpace(_directorText) && !string.IsNullOrWhiteSpace(details.DirectorText))
        {
            _directorText = details.DirectorText;
            OnPropertyChanged(nameof(DirectorText));
        }

        if (string.IsNullOrWhiteSpace(_actorsText) && !string.IsNullOrWhiteSpace(details.ActorsText))
        {
            _actorsText = details.ActorsText;
            OnPropertyChanged(nameof(CastText));
        }

        if (!RuntimeMinutes.HasValue && details.RuntimeMinutes.HasValue)
        {
            RuntimeMinutes = details.RuntimeMinutes;
            OnPropertyChanged(nameof(RuntimeMinutes));
            OnPropertyChanged(nameof(RuntimeText));
            OnPropertyChanged(nameof(ListDateRuntimeText));
        }

        if (details.TmdbRating.HasValue)
        {
            TmdbRating = details.TmdbRating;
            OnPropertyChanged(nameof(TmdbRating));
        }

        if (details.TmdbVoteCount.HasValue)
        {
            TmdbVoteCount = details.TmdbVoteCount;
            OnPropertyChanged(nameof(TmdbVoteCount));
        }

        RefreshRating();
    }

    public void SetOmdbRating(MovieRatingItem? rating)
    {
        OmdbRating = rating;
        RefreshRating();
    }

    private void RefreshRating()
    {
        var presentation = DiscoveryRatingPresenter.Build(TmdbRating, TmdbVoteCount, OmdbRating);
        RatingValue = presentation.Value;
        RatingText = presentation.Text;
        OnPropertyChanged(nameof(RatingBadgeText));
        OnPropertyChanged(nameof(WeightedAverageRatingText));
        OnPropertyChanged(nameof(RatingDisplayText));
        OnPropertyChanged(nameof(IsHighRating));
        OnPropertyChanged(nameof(IsHighWeightedAverageRating));
    }

    private void NotifyTagPresentationChanged()
    {
        OnPropertyChanged(nameof(FullTagLine));
        OnPropertyChanged(nameof(PosterTagLine));
        OnPropertyChanged(nameof(PosterTagToolTipText));
        OnPropertyChanged(nameof(ListTagToolTipText));
        OnPropertyChanged(nameof(PosterTagGroupOneText));
        OnPropertyChanged(nameof(PosterTagGroupTwoText));
        OnPropertyChanged(nameof(PosterTagGroupThreeText));
        OnPropertyChanged(nameof(PosterTagSeparatorAfterOneText));
        OnPropertyChanged(nameof(PosterTagSeparatorAfterTwoText));
        OnPropertyChanged(nameof(ListTagGroupOneText));
        OnPropertyChanged(nameof(ListTagGroupTwoText));
        OnPropertyChanged(nameof(ListTagGroupThreeText));
        OnPropertyChanged(nameof(ListTagSeparatorAfterOneText));
        OnPropertyChanged(nameof(ListTagSeparatorAfterTwoText));
        OnPropertyChanged(nameof(ListTagLine));
    }

    private string[] BuildTagGroups(int? maxDisplayLength)
    {
        var groups = new[]
        {
            ParseTags(DisplayTags),
            ParseTags(EmotionTagsText),
            ParseTags(SceneTagsText)
        };

        if (groups.All(group => group.Count == 0))
        {
            return ["暂无类型", string.Empty, string.Empty];
        }

        var formatted = groups.Select(FormatTags).ToArray();
        if (!maxDisplayLength.HasValue || FitsDisplayLength(JoinVisibleGroups(formatted), maxDisplayLength.Value))
        {
            return formatted;
        }

        return TruncateVisibleGroupsForDisplay(formatted, maxDisplayLength.Value);
    }

    private static IReadOnlyList<string> ParseTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(['/', '、', ',', '，', '|', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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
