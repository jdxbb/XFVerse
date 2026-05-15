using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class DiscoveryMovieCardViewModel : ObservableObject
{
    private bool _isWantToWatch;
    private bool _isWatched;
    private bool _isFavorite;
    private bool _isNotInterested;
    private string _ratingText = "暂无评分";
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

    public string RatingText
    {
        get => _ratingText;
        private set => SetProperty(ref _ratingText, value);
    }

    public double? RatingValue
    {
        get => _ratingValue;
        private set => SetProperty(ref _ratingValue, value);
    }

    public string YearText => ReleaseYear?.ToString() ?? "-";

    public string RankText => $"#{SearchOrder}";

    public string OriginalTitleText => string.IsNullOrWhiteSpace(OriginalTitle) ? string.Empty : OriginalTitle;

    public string TitleWithOriginalText => string.IsNullOrWhiteSpace(OriginalTitle)
                                           || string.Equals(Title, OriginalTitle, StringComparison.OrdinalIgnoreCase)
        ? Title
        : $"{Title}｜{OriginalTitle}";

    public string OverviewText => string.IsNullOrWhiteSpace(Overview) ? "暂无简介" : Overview;

    public string AvailabilityText => IsInLibrary ? "有播放源" : "无播放源";

    public string WatchStateText => IsWatched ? "已看" : "未看";

    public bool CanToggleWantToWatch => !IsWatched;

    public string WantToWatchButtonText => IsWatched ? "已看" : IsWantToWatch ? "想看" : "+ 想看";

    public bool CanAddToLibrary => !IsVisibleInLibrary;

    public string AddToLibraryButtonText => LibraryVisibilityState == LibraryVisibilityState.Hidden ? "恢复到媒体库" : "加入媒体库";

    public void ApplyStatus(DiscoveryMovieStatus status)
    {
        MovieId = status.MovieId;
        IsInLibrary = status.IsInLibrary;
        IsVisibleInLibrary = status.IsVisibleInLibrary;
        LibraryVisibilityState = status.LibraryVisibilityState;
        IsWatched = status.IsWatched;
        IsWantToWatch = status.IsWantToWatch;
        IsFavorite = status.IsFavorite;
        IsNotInterested = status.IsNotInterested;

        if (status.IsInLibrary)
        {
            Title = string.IsNullOrWhiteSpace(status.Title) ? Title : status.Title;
            OriginalTitle = string.IsNullOrWhiteSpace(status.OriginalTitle) ? OriginalTitle : status.OriginalTitle;
            ReleaseYear = status.ReleaseYear ?? ReleaseYear;
            PosterRemoteUrl = string.IsNullOrWhiteSpace(status.PosterRemoteUrl) ? PosterRemoteUrl : status.PosterRemoteUrl;
            Overview = string.IsNullOrWhiteSpace(status.Overview) ? Overview : status.Overview;
            GenresText = string.IsNullOrWhiteSpace(status.GenresText) ? GenresText : status.GenresText;
            DisplayTags = !string.IsNullOrWhiteSpace(status.LocalTypeTags)
                ? status.LocalTypeTags
                : !string.IsNullOrWhiteSpace(status.AiTagsText)
                    ? status.AiTagsText
                    : GenresText;
            EmotionTagsText = !string.IsNullOrWhiteSpace(status.LocalEmotionTags) ? status.LocalEmotionTags : status.EmotionTagsText;
            SceneTagsText = status.SceneTagsText;
            Country = string.IsNullOrWhiteSpace(status.Country) ? Country : status.Country;
            Language = string.IsNullOrWhiteSpace(status.Language) ? Language : status.Language;
            RuntimeMinutes = status.RuntimeMinutes ?? RuntimeMinutes;
            ImdbId = string.IsNullOrWhiteSpace(status.ImdbId) ? ImdbId : status.ImdbId;
            TmdbRating = status.TmdbRating ?? TmdbRating;
            TmdbVoteCount = status.TmdbVoteCount ?? TmdbVoteCount;
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
                RefreshRating();
            }

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(OriginalTitle));
            OnPropertyChanged(nameof(OriginalTitleText));
            OnPropertyChanged(nameof(TitleWithOriginalText));
            OnPropertyChanged(nameof(ReleaseYear));
            OnPropertyChanged(nameof(YearText));
            OnPropertyChanged(nameof(PosterRemoteUrl));
            OnPropertyChanged(nameof(Overview));
            OnPropertyChanged(nameof(OverviewText));
            OnPropertyChanged(nameof(GenresText));
            OnPropertyChanged(nameof(DisplayTags));
            OnPropertyChanged(nameof(EmotionTagsText));
            OnPropertyChanged(nameof(SceneTagsText));
            OnPropertyChanged(nameof(IsInLibrary));
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
            OnPropertyChanged(nameof(MovieId));
            OnPropertyChanged(nameof(AvailabilityText));
            OnPropertyChanged(nameof(CanAddToLibrary));
            OnPropertyChanged(nameof(AddToLibraryButtonText));
        }
    }

    public void ApplyWantToWatchState(bool isWantToWatch)
    {
        IsWantToWatch = isWantToWatch;
        if (isWantToWatch)
        {
            IsNotInterested = false;
        }
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

        if (!RuntimeMinutes.HasValue && details.RuntimeMinutes.HasValue)
        {
            RuntimeMinutes = details.RuntimeMinutes;
            OnPropertyChanged(nameof(RuntimeMinutes));
        }

        if (!TmdbRating.HasValue && details.TmdbRating.HasValue)
        {
            TmdbRating = details.TmdbRating;
            OnPropertyChanged(nameof(TmdbRating));
        }

        if (!TmdbVoteCount.HasValue && details.TmdbVoteCount.HasValue)
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
    }
}
