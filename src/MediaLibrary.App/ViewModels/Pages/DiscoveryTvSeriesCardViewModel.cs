using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class DiscoveryTvSeriesCardViewModel : ObservableObject
{
    private const int PosterTagDisplayLength = 18;
    private const int ListTagDisplayLength = 76;
    private const string TagOverflowMarker = "..";

    private bool _isInLibrary;
    private int _inLibrarySeasonCount;
    private bool _hasWantToWatchSeason;
    private bool _hasFavoriteSeason;
    private bool _hasNotInterestedSeason;
    private bool _isWatched;
    private bool _isVisibleInLibrary;
    private bool _hasHiddenSeason;
    private LibraryVisibilityState _libraryVisibilityState = LibraryVisibilityState.Auto;
    private int? _totalSeasonCount;
    private bool _hasLoadedSeasonCount;
    private string _directorText = string.Empty;
    private string _actorsText = string.Empty;
    private double? _tmdbRating;
    private int? _tmdbVoteCount;
    private MovieRatingItem? _omdbRating;
    private string _ratingText = "--";
    private double? _ratingValue;

    public DiscoveryTvSeriesCardViewModel(TmdbTvSeriesSearchItem source, int searchOrder, bool showRank = false)
    {
        TmdbSeriesId = source.TmdbId;
        Name = source.Name;
        OriginalName = source.OriginalName;
        Overview = source.Overview;
        PosterRemoteUrl = source.PosterRemoteUrl;
        FirstAirYear = source.FirstAirYear;
        FirstAirDate = source.FirstAirDate;
        GenreIds = source.GenreIds;
        GenresText = TmdbTvGenreMapper.MapGenreIds(source.GenreIds);
        OriginalLanguage = source.OriginalLanguage;
        OriginCountries = source.OriginCountries;
        _tmdbRating = source.TmdbRating;
        _tmdbVoteCount = source.TmdbVoteCount;
        Popularity = source.Popularity;
        SearchOrder = searchOrder;
        HasRank = showRank;
        RefreshRating();
    }

    public int TmdbSeriesId { get; }

    public int? TvSeriesId { get; private set; }

    public string Name { get; private set; }

    public string OriginalName { get; private set; }

    public string Overview { get; private set; }

    public string PosterRemoteUrl { get; private set; }

    public int? FirstAirYear { get; private set; }

    public string FirstAirDate { get; }

    public IReadOnlyList<int> GenreIds { get; }

    public string GenresText { get; private set; }

    public string OriginalLanguage { get; }

    public IReadOnlyList<string> OriginCountries { get; }

    public double? TmdbRating
    {
        get => _tmdbRating;
        private set => SetProperty(ref _tmdbRating, value);
    }

    public int? TmdbVoteCount
    {
        get => _tmdbVoteCount;
        private set => SetProperty(ref _tmdbVoteCount, value);
    }

    public double? Popularity { get; }

    public int SearchOrder { get; }

    public bool HasRank { get; }

    public int? TotalSeasonCount
    {
        get => _totalSeasonCount;
        private set
        {
            if (SetProperty(ref _totalSeasonCount, value))
            {
                OnPropertyChanged(nameof(SeasonCountText));
                OnPropertyChanged(nameof(ListDateRuntimeText));
            }
        }
    }

    public bool HasLoadedSeasonCount
    {
        get => _hasLoadedSeasonCount;
        private set
        {
            if (SetProperty(ref _hasLoadedSeasonCount, value))
            {
                OnPropertyChanged(nameof(SeasonCountText));
                OnPropertyChanged(nameof(ListDateRuntimeText));
            }
        }
    }

    public bool IsInLibrary
    {
        get => _isInLibrary;
        private set
        {
            if (SetProperty(ref _isInLibrary, value))
            {
                OnPropertyChanged(nameof(AvailabilityText));
                OnPropertyChanged(nameof(LibraryStatusText));
            }
        }
    }

    public int InLibrarySeasonCount
    {
        get => _inLibrarySeasonCount;
        private set
        {
            if (SetProperty(ref _inLibrarySeasonCount, value))
            {
                OnPropertyChanged(nameof(LibraryStatusText));
            }
        }
    }

    public bool IsVisibleInLibrary
    {
        get => _isVisibleInLibrary;
        private set
        {
            if (SetProperty(ref _isVisibleInLibrary, value))
            {
                OnPropertyChanged(nameof(CanAddToLibrary));
                OnPropertyChanged(nameof(AddToLibraryButtonText));
            }
        }
    }

    public bool HasHiddenSeason
    {
        get => _hasHiddenSeason;
        private set
        {
            if (SetProperty(ref _hasHiddenSeason, value))
            {
                OnPropertyChanged(nameof(CanAddToLibrary));
                OnPropertyChanged(nameof(AddToLibraryButtonText));
            }
        }
    }

    public LibraryVisibilityState LibraryVisibilityState
    {
        get => _libraryVisibilityState;
        private set
        {
            if (SetProperty(ref _libraryVisibilityState, value))
            {
                OnPropertyChanged(nameof(AddToLibraryButtonText));
            }
        }
    }

    public bool HasWantToWatchSeason
    {
        get => _hasWantToWatchSeason;
        private set
        {
            if (SetProperty(ref _hasWantToWatchSeason, value))
            {
                OnPropertyChanged(nameof(SeasonStateSummaryText));
                OnPropertyChanged(nameof(HasSeasonStateSummary));
                OnPropertyChanged(nameof(HasWantToWatchSeasonTag));
                OnPropertyChanged(nameof(CurrentWantTagText));
            }
        }
    }

    public bool HasFavoriteSeason
    {
        get => _hasFavoriteSeason;
        private set
        {
            if (SetProperty(ref _hasFavoriteSeason, value))
            {
                OnPropertyChanged(nameof(SeasonStateSummaryText));
                OnPropertyChanged(nameof(HasSeasonStateSummary));
            }
        }
    }

    public bool HasNotInterestedSeason
    {
        get => _hasNotInterestedSeason;
        private set
        {
            if (SetProperty(ref _hasNotInterestedSeason, value))
            {
                OnPropertyChanged(nameof(SeasonStateSummaryText));
                OnPropertyChanged(nameof(HasSeasonStateSummary));
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
                OnPropertyChanged(nameof(WatchStateText));
            }
        }
    }

    public string Title => Name;

    public string YearText => FirstAirYear?.ToString() ?? "-";

    public string ReleaseDateText => string.IsNullOrWhiteSpace(FirstAirDate) ? YearText : FirstAirDate;

    public string RankText => $"#{SearchOrder}";

    public string OriginalTitleText => string.IsNullOrWhiteSpace(OriginalName) ? string.Empty : OriginalName;

    public string TitleWithOriginalText => string.IsNullOrWhiteSpace(OriginalName)
                                           || string.Equals(Name, OriginalName, StringComparison.OrdinalIgnoreCase)
        ? Name
        : $"{Name} · {OriginalName}";

    public string DisplayTags => string.IsNullOrWhiteSpace(GenresText) ? "暂无类型" : GenresText;

    public string DirectorText => $"导演 {FormatCrewText(_directorText)}";

    public string CastText => $"演员 {FormatCrewText(_actorsText)}";

    public string OverviewText => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public bool HasPoster => !string.IsNullOrWhiteSpace(PosterRemoteUrl);

    public string CategoryTagText => "电视剧";

    public string DetailHintText => "电视剧";

    public string AvailabilityText => IsInLibrary ? "有播放源" : "无播放源";

    public string LibraryStatusText => IsInLibrary
        ? InLibrarySeasonCount > 0 ? $"有播放源 {InLibrarySeasonCount} 季" : "有播放源"
        : "无播放源";

    public string WatchStateText => IsWatched ? "已看" : "未看";

    public string SeasonCountText
    {
        get
        {
            if (!HasLoadedSeasonCount)
            {
                return "季数加载中";
            }

            return TotalSeasonCount is > 0 ? $"共 {TotalSeasonCount.Value} 季" : "季数未知";
        }
    }

    public MovieRatingItem? OmdbRating
    {
        get => _omdbRating;
        private set => SetProperty(ref _omdbRating, value);
    }

    public string RatingText
    {
        get => _ratingText;
        private set => SetProperty(ref _ratingText, value);
    }

    public string RatingBadgeText => RatingText;

    public string WeightedAverageRatingText => RatingBadgeText;

    public string RatingDisplayText => RatingBadgeText;

    public double? RatingValue
    {
        get => _ratingValue;
        private set => SetProperty(ref _ratingValue, value);
    }

    public bool IsHighRating => DiscoveryRatingPresenter.IsHighDisplayRating(RatingValue);

    public bool IsHighWeightedAverageRating => IsHighRating;

    public bool HasWantToWatchSeasonTag => HasWantToWatchSeason;

    public string CurrentWantTagText => "当前想看";

    public string FullTagLine => BuildLimitedTagLine(DisplayTags, ListTagDisplayLength);

    public string PosterTagLine => PosterTagGroupOneText;

    public string PosterTagToolTipText => DisplayTags;

    public string ListTagToolTipText => DisplayTags;

    public string PosterTagGroupOneText => BuildLimitedTagLine(DisplayTags, PosterTagDisplayLength);

    public string PosterTagGroupTwoText => string.Empty;

    public string PosterTagGroupThreeText => string.Empty;

    public string PosterTagSeparatorAfterOneText => string.Empty;

    public string PosterTagSeparatorAfterTwoText => string.Empty;

    public string ListTagGroupOneText => BuildLimitedTagLine(DisplayTags, ListTagDisplayLength);

    public string ListTagGroupTwoText => string.Empty;

    public string ListTagGroupThreeText => string.Empty;

    public string ListTagSeparatorAfterOneText => string.Empty;

    public string ListTagSeparatorAfterTwoText => string.Empty;

    public string ListDateRuntimeText => $"{ReleaseDateText} | {SeasonCountText}";

    public string ListDateAndTagSpacingText => "      ";

    public string ListTagLine => ListTagGroupOneText;

    public string SeasonStateSummaryText
    {
        get
        {
            var states = new List<string>();
            if (HasWantToWatchSeason)
            {
                states.Add("有想看季");
            }

            if (HasFavoriteSeason)
            {
                states.Add("有喜爱季");
            }

            if (HasNotInterestedSeason)
            {
                states.Add("有不想看季");
            }

            return string.Join("、", states);
        }
    }

    public bool HasSeasonStateSummary => !string.IsNullOrWhiteSpace(SeasonStateSummaryText);

    public bool CanAddToLibrary => !IsVisibleInLibrary || HasHiddenSeason;

    public string AddToLibraryButtonText => HasHiddenSeason ? "恢复到媒体库" : "加入媒体库";

    public void ApplyStatus(DiscoveryTvSeriesStatus status)
    {
        TvSeriesId = status.TvSeriesId;
        IsInLibrary = status.IsInLibrary;
        IsVisibleInLibrary = status.IsVisibleInLibrary;
        HasHiddenSeason = status.HasHiddenSeason;
        LibraryVisibilityState = status.LibraryVisibilityState;
        InLibrarySeasonCount = status.InLibrarySeasonCount;
        IsWatched = status.IsWatched;
        HasWantToWatchSeason = status.HasWantToWatchSeason;
        HasFavoriteSeason = status.HasFavoriteSeason;
        HasNotInterestedSeason = status.HasNotInterestedSeason;

        if (status.IsInLibrary)
        {
            Name = string.IsNullOrWhiteSpace(status.Name) ? Name : status.Name;
            OriginalName = string.IsNullOrWhiteSpace(status.OriginalName) ? OriginalName : status.OriginalName;
            Overview = string.IsNullOrWhiteSpace(status.Overview) ? Overview : status.Overview;
            PosterRemoteUrl = string.IsNullOrWhiteSpace(status.PosterRemoteUrl) ? PosterRemoteUrl : status.PosterRemoteUrl;
            GenresText = string.IsNullOrWhiteSpace(status.GenresText) ? GenresText : status.GenresText;
            FirstAirYear = status.FirstAirYear ?? FirstAirYear;
            _directorText = string.IsNullOrWhiteSpace(status.DirectorText) ? _directorText : status.DirectorText;
            _actorsText = string.IsNullOrWhiteSpace(status.ActorsText) ? _actorsText : status.ActorsText;
        }

        OnPropertyChanged(nameof(TvSeriesId));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(OriginalName));
        OnPropertyChanged(nameof(OriginalTitleText));
        OnPropertyChanged(nameof(TitleWithOriginalText));
        OnPropertyChanged(nameof(Overview));
        OnPropertyChanged(nameof(OverviewText));
        OnPropertyChanged(nameof(PosterRemoteUrl));
        OnPropertyChanged(nameof(HasPoster));
        OnPropertyChanged(nameof(GenresText));
        OnPropertyChanged(nameof(DisplayTags));
        OnPropertyChanged(nameof(DirectorText));
        OnPropertyChanged(nameof(CastText));
        NotifyTagPresentationChanged();
        OnPropertyChanged(nameof(FirstAirYear));
        OnPropertyChanged(nameof(YearText));
        OnPropertyChanged(nameof(ReleaseDateText));
        OnPropertyChanged(nameof(ListDateRuntimeText));
    }

    public void ApplyDetails(TmdbTvSeriesDetailResult details)
    {
        if (details.TmdbId != TmdbSeriesId)
        {
            return;
        }

        var seasonCount = details.Seasons.Count(season => season.SeasonNumber > 0);
        TotalSeasonCount = seasonCount > 0 ? seasonCount : details.NumberOfSeasons;
        HasLoadedSeasonCount = true;

        if (string.IsNullOrWhiteSpace(Overview) && !string.IsNullOrWhiteSpace(details.Overview))
        {
            Overview = details.Overview;
            OnPropertyChanged(nameof(Overview));
            OnPropertyChanged(nameof(OverviewText));
        }

        if (string.IsNullOrWhiteSpace(GenresText) && !string.IsNullOrWhiteSpace(details.GenresText))
        {
            GenresText = details.GenresText;
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

        if (!FirstAirYear.HasValue && details.FirstAirYear.HasValue)
        {
            FirstAirYear = details.FirstAirYear;
            OnPropertyChanged(nameof(FirstAirYear));
            OnPropertyChanged(nameof(YearText));
            OnPropertyChanged(nameof(ReleaseDateText));
            OnPropertyChanged(nameof(ListDateRuntimeText));
        }

        if (details.TmdbRating.HasValue)
        {
            TmdbRating = details.TmdbRating;
        }

        if (details.TmdbVoteCount.HasValue)
        {
            TmdbVoteCount = details.TmdbVoteCount;
        }

        RefreshRating();
    }

    public void SetOmdbRating(MovieRatingItem? rating)
    {
        OmdbRating = rating;
        RefreshRating();
    }

    public void MarkSeasonCountUnavailable()
    {
        TotalSeasonCount = null;
        HasLoadedSeasonCount = true;
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

    private static string BuildLimitedTagLine(string? value, int maxDisplayLength)
    {
        var tags = ParseTags(value);
        if (tags.Count == 0)
        {
            return "暂无类型";
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

        return selected.Count == 0
            ? TruncateForDisplay(tags[0], maxDisplayLength)
            : $"{FormatTags(selected)}{TagOverflowMarker}";
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
}
