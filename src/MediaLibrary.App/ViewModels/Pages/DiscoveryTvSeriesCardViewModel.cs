using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class DiscoveryTvSeriesCardViewModel : ObservableObject
{
    private bool _isInLibrary;
    private int _inLibrarySeasonCount;
    private bool _hasWantToWatchSeason;
    private bool _hasFavoriteSeason;
    private bool _hasNotInterestedSeason;
    private bool _isVisibleInLibrary;
    private bool _hasHiddenSeason;
    private LibraryVisibilityState _libraryVisibilityState = LibraryVisibilityState.Auto;
    private int? _totalSeasonCount;
    private bool _hasLoadedSeasonCount;

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
        TmdbRating = source.TmdbRating;
        TmdbVoteCount = source.TmdbVoteCount;
        Popularity = source.Popularity;
        SearchOrder = searchOrder;
        HasRank = showRank;
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

    public double? TmdbRating { get; }

    public int? TmdbVoteCount { get; }

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

    public string Title => Name;

    public string YearText => FirstAirYear?.ToString() ?? "-";

    public string RankText => $"#{SearchOrder}";

    public string OriginalTitleText => string.IsNullOrWhiteSpace(OriginalName) ? string.Empty : OriginalName;

    public string TitleWithOriginalText => string.IsNullOrWhiteSpace(OriginalName)
                                           || string.Equals(Name, OriginalName, StringComparison.OrdinalIgnoreCase)
        ? Name
        : $"{Name} · {OriginalName}";

    public string DisplayTags => string.IsNullOrWhiteSpace(GenresText) ? "暂无类型" : GenresText;

    public string OverviewText => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public string AvailabilityText => IsInLibrary ? "有播放源" : "无播放源";

    public string LibraryStatusText => IsInLibrary
        ? InLibrarySeasonCount > 0 ? $"有播放源 {InLibrarySeasonCount} 季" : "有播放源"
        : "无播放源";

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

    public string RatingText
    {
        get
        {
            if (TmdbRating is not > 0)
            {
                return "暂无评分";
            }

            var voteText = TmdbVoteCount is > 0 ? $" · {TmdbVoteCount} 票" : string.Empty;
            return $"TMDB 剧集评分 {TmdbRating.Value:0.0}{voteText}";
        }
    }

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
        OnPropertyChanged(nameof(GenresText));
        OnPropertyChanged(nameof(DisplayTags));
        OnPropertyChanged(nameof(FirstAirYear));
        OnPropertyChanged(nameof(YearText));
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
        }

        if (!FirstAirYear.HasValue && details.FirstAirYear.HasValue)
        {
            FirstAirYear = details.FirstAirYear;
            OnPropertyChanged(nameof(FirstAirYear));
            OnPropertyChanged(nameof(YearText));
        }
    }

    public void MarkSeasonCountUnavailable()
    {
        TotalSeasonCount = null;
        HasLoadedSeasonCount = true;
    }
}
